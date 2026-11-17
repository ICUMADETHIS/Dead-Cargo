BEGIN_PROPERTIES
	[Header(ALBEDO)]
	[Toggle(_SGT_ALBEDO)] _SGT_Albedo("	Enable", Float) = 0
	
	[Header(LIGHTING)]
	[Toggle(_SGT_LIGHTING)] _SGT_Lighting("	Enable", Float) = 0

	[Header(SOLID CORE)]
	[Toggle(_SGT_SOLID_CORE)] _SGT_SolidCore("	Enable", Float) = 0
END_PROPERTIES

BEGIN_DEFINES
	#pragma shader_feature_local _SGT_ALBEDO_OFF _SGT_ALBEDO
	#pragma shader_feature_local _SGT_LIGHTING_OFF _SGT_LIGHTING
	#pragma shader_feature_local _SGT_SOLID_CORE_OFF _SGT_SOLID_CORE
	#pragma multi_compile __ CW_COMPRESS_POSITIONS
END_DEFINES

#pragma instancing_options procedural:SetupInstancing
#if _SSS_PASS_SHADOWCASTER || _SSS_PASS_META
	#pragma multi_compile_instancing // For some reason the ShadowCaster and Meta pass in BIRP doesn't have this line
#endif

#include "SgtSky.cginc"

float4x4 _SGT_ObjectToWorld;
float4x4 _SGT_WorldToObject;

void SetupInstancing()
{
	#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
		#ifdef unity_ObjectToWorld
			#undef unity_ObjectToWorld
		#endif

		#ifdef unity_WorldToObject
			#undef unity_WorldToObject
		#endif
		
		unity_ObjectToWorld = _SGT_ObjectToWorld;
		unity_WorldToObject = _SGT_WorldToObject;
	#endif
}

float GetWorldSceneDepth(float2 screenUV)
{
	#if CW_COMPRESS_POSITIONS
		return SSS_DecompressWorldDist(SSS_GetSceneWorldDistance(screenUV, SSS_GetSceneDepth(screenUV)));
	#else
		return SSS_GetSceneWorldDistance(screenUV, SSS_GetSceneDepth(screenUV));
	#endif
}

void SSS_Vert(inout SSS_VertexData v)
{
	#if CW_COMPRESS_POSITIONS
		float3 wpos = SSS_ObjectToWorld(v.position);
		v.extraV2F0.z = distance(_WorldSpaceCameraPos, wpos);
		wpos = SSS_CompressWorld(wpos);
		v.position = SSS_WorldToObject(wpos);
	#endif
}

void SSS_Frag(inout SSS_SurfaceData s, inout SSS_FragmentData d)
{
	#if CW_COMPRESS_POSITIONS
		d.worldSpacePosition = _WorldSpaceCameraPos - d.worldSpaceViewDir * d.extraV2F0.z;
	#endif
	
	float3 wdir          = -d.worldSpaceViewDir;
	float  wfar          = distance(_WorldSpaceCameraPos, d.worldSpacePosition);
	float  worldEyeDepth = GetWorldSceneDepth(d.screenUV);
	float3 tint          = 1.0f;
	float  dither        = SGT_DitherFast(d.screenUV);
	
	#if CW_COMPRESS_POSITIONS
		worldEyeDepth = SSS_ElongateDistance(worldEyeDepth);
	#endif
	  
	wfar = min(wfar, worldEyeDepth);
		
	// Convert to sky space
	float3 ocam = mul(_SGT_WorldToSky, float4(_WorldSpaceCameraPos, 1.0f)).xyz;
	float3 ofar = mul(_SGT_WorldToSky, float4(_WorldSpaceCameraPos + wdir * wfar, 1.0f)).xyz;
	float  omax = distance(ocam, ofar);
	float3 odir = normalize(mul(_SGT_WorldToSky, float4(wdir, 0.0f)).xyz);
	float3 odst = SGT_SphereTest(ocam, odir, 1.0f);
		
	odst.xy = max(odst.xy, 0.0f);
		
	#if _SGT_SOLID_CORE
		float3 ihit = SGT_SphereTest(ocam, odir, _SGT_SkyRadius.w);
		omax = ihit.z > 0.0f && ihit.x > 0.0f ? min(ihit.x, omax) : omax;
	#endif
		
	float3 rayPos = ocam + odir * odst.x;
	float3 rayDir = odir;
	float  rayFar = SGT_SphereTest(rayPos, rayDir, 1.0f).y;
	float  rayMax = min(odst.y, omax) - odst.x;
		
	#if _SGT_LIGHTING
		float3 sunDir = normalize(mul(_SGT_WorldToSky, float4(_SGT_LightDirection[0].xyz, 0.0f)).xyz);
	#else
		float3 sunDir = normalize(rayPos);
	#endif
	
	#if _SGT_ALBEDO
		float2 uv  = SGT_DirectionToEquirectangular(rayPos);
		float2 uvx = ddx(uv); uvx.x *= (abs(uvx.x) < 0.5f);
		float2 uvy = ddy(uv); uvy.x *= (abs(uvy.x) < 0.5f);
			
		tint = tex2Dgrad(_SGT_SkyAlbedoTex, uv, uvx, uvy).xyz;
	#endif
	
	float4 cloudColor;
	float  cloudDepth;
	
	CW_SampleVolumetrics(d.screenUV, wdir, worldEyeDepth, cloudColor, cloudDepth);
	
	#if _SGT_LIGHTING
		float4 lighting = float4(0.0f, 0.0f, 0.0f, 1.0f);
				
		for (int i = 0; i < _SGT_LightCount; i++)
		{
			lighting.xyz += _SGT_LightColor[i].xyz;
		}
	#else
		float4 lighting = float4(1.0f, 1.0f, 1.0f, 1.0f);
	#endif
	
	float4 color = SGT_Atmosphere(rayPos, rayDir, rayFar, rayMax, sunDir, _SGT_SkyExposure, dither, 1.0, 1.0);
	
	// Boost opacity when near ground
	float skyWeight = rayMax * 1.02f > rayFar;//saturate(rayMax / rayFar); skyWeight = pow(skyWeight,16);
	float luminance = dot(color.xyz, float3(0.2126f, 0.7152f, 0.0722f));
	float boost     = dot(SGT_GetDensity2(rayPos, 1.0), 1) * lerp(1.0f, luminance, _SGT_SkyRevealStars) * _SGT_SkyDepthOpaque;
	
	color.xyz *= lighting.xyz;
	color.w    = lerp(1.0f, color.a, exp(-boost * skyWeight));
	
	if (cloudColor.w > 0.0f)
	{
		float3 cloudWPos = CW_Depth2World(cloudDepth, wdir);
		float3 cloudSPos = mul(_SGT_WorldToSky, float4(cloudWPos, 1.0f)).xyz;
		
		color.rgb *= color.a;
		
		color.a += cloudColor.a * (1.0f - color.a);
		color.rgb = (cloudColor.rgb + color.rgb * (1.0 - cloudColor.a)) / max(color.a, 1e-6);
	}
	
	s.Albedo = color.xyz * tint;
	s.Alpha  = color.w;
	
	#if _SSS_HDRP
		s.Emission = s.Albedo; s.Albedo = 0.0;
	#endif
}