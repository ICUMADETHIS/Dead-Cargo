BEGIN_PROPERTIES
	[HideInInspector] _SGT_RadiusData("", Vector) = (0,0,0,0)
	[HideInInspector] _SGT_LightningBrightness("", Float) = 1.0
	[HideInInspector] _SGT_LightningThickness("", Float) = 1.0
	[HideInInspector] _SGT_AnimationMidpoint("", Float) = 0.1
	[HideInInspector] _SGT_AnimationBrightness("", Float) = 10.0
	
	[NoScaleOffset] _SGT_LightningTex("Lightning Texture Atlas", 2D) = "white" {}
	_SGT_AtlasCols("Atlas Columns", Float) = 1
	_SGT_AtlasRows("Atlas Rows", Float) = 1
END_PROPERTIES

BEGIN_CBUFFER
END_CBUFFER

BEGIN_DEFINES
	#pragma multi_compile __ CW_COMPRESS_POSITIONS
	
	#pragma instancing_options procedural:SetupInstancing
	#if _SSS_PASS_SHADOWCASTER || _SSS_PASS_META
		#pragma multi_compile_instancing // For some reason the ShadowCaster and Meta pass in BIRP doesn't have this line
	#endif
END_DEFINES

float4    _SGT_RadiusData;
float     _SGT_LightningBrightness;
float     _SGT_LightningThickness;
float     _SGT_AnimationMidpoint;
float     _SGT_AnimationBrightness;

sampler2D _SGT_LightningTex;
float     _SGT_AtlasCols;
float     _SGT_AtlasRows;

float4    _SGT_LightningPosition[32];
float4    _SGT_LightningData[32];

//float4x4  _SGT_ObjectToWorld;
//float4x4  _SGT_WorldToObject;

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

void SSS_Vert(inout SSS_VertexData v)
{
	int quadIndex = v.instanceID;
	float3 instancedPos  = _SGT_LightningPosition[quadIndex].xyz;
	float  atlasIndex    = _SGT_LightningData[quadIndex].x % (_SGT_AtlasCols * _SGT_AtlasRows);
	float  instancedFade = _SGT_LightningData[quadIndex].y;

	float sideFactor = v.texcoord0.x * 0.5f;
	float tLinear    = v.texcoord0.y * 0.5f + 0.5f;

	// Calculate the base radius (outerB) and target outer radius (outerR)
	float outerB      = length(instancedPos);
	float outerR      = _SGT_RadiusData.y;
	float3 radialDir  = instancedPos / max(0.0001, outerB);
	float currentRadius = lerp(_SGT_RadiusData.x, _SGT_RadiusData.y, tLinear);

	float3 objectCamPos = SSS_WorldToObject(_WorldSpaceCameraPos);
	float3 viewVector   = normalize(objectCamPos - instancedPos);
	float3 lateralUp    = normalize(cross(instancedPos, viewVector));

	// Expand both vertically (radialDir) and horizontally (lateralUp)
	float3 activePosition = (radialDir * currentRadius) + lateralUp * (sideFactor * _SGT_LightningThickness);

	float isActive = step(0.0001f, instancedFade);
	v.position = activePosition * isActive;

	v.extraV2F0.x = instancedFade;
	v.extraV2F0.y = atlasIndex;

	#if CW_COMPRESS_POSITIONS
		float3 wpos = mul(_SGT_ObjectToWorld, float4(v.position, 1.0f)).xyz;
		v.extraV2F0.z = distance(_WorldSpaceCameraPos, wpos);
		wpos = SSS_CompressWorld(wpos);
		v.position = mul(_SGT_WorldToObject, float4(wpos, 1.0f)).xyz;
	#endif
}

Texture2D _SGT_Volumetrics_ColorTex; // Global
float4    _SGT_Volumetrics_ColorSize;
// Global
Texture2D _SGT_Volumetrics_DepthTex; // Global
float2    _SGT_Volumetrics_DepthSize; // Global

SamplerState my_linear_clamp_sampler;

float CW_GetVolumetricsOpacity(float2 screenUV, float worldDepth)
{
	float4 centerColor = _SGT_Volumetrics_ColorTex.Sample(my_linear_clamp_sampler, screenUV);
	float  centerDepth = _SGT_Volumetrics_DepthTex.Sample(my_linear_clamp_sampler, screenUV).x;
	return centerColor.w * (centerDepth < worldDepth);
}

void SSS_Frag(inout SSS_SurfaceData o, inout SSS_FragmentData d)
{
	#if CW_COMPRESS_POSITIONS
		d.worldSpacePosition = _WorldSpaceCameraPos - d.worldSpaceViewDir * d.extraV2F0.z;
	#endif
	
	float dynamicFade = d.extraV2F0.x;
	float atlasIndex  = d.extraV2F0.y + 0.001;

	float cols = max(1.0, _SGT_AtlasCols);
	float rows = max(1.0, _SGT_AtlasRows);
	
	float col = fmod(atlasIndex, cols);
	float row = floor(atlasIndex / cols);

	float2 textureUV = d.texcoord0.xy * 0.5f + 0.5f;
	textureUV.x = (textureUV.x + col) / cols;
	textureUV.y = (textureUV.y + row) / rows;
	
	float4 lightningTextureSample = tex2D(_SGT_LightningTex, textureUV);

	// Calculate progression timeline (0 = start, 1 = end of life)
	float timeProgress = 1.0 - dynamicFade;
	float isPhaseTwo = step(_SGT_AnimationMidpoint, timeProgress);
	
	float revealProgress = saturate(timeProgress / max(0.0001, _SGT_AnimationMidpoint));
	float returnProgress = saturate((timeProgress - _SGT_AnimationMidpoint) / max(0.0001, 1.0 - _SGT_AnimationMidpoint));
	
	// Phase 1: Reveal mask spreading from top to bottom
	// distance from top (d.texcoord0.y: 1 = top, -1 = bottom)
	float distFromTop = 1.0 - (d.texcoord0.y * 0.5f + 0.5f);
	float spread = revealProgress * 1.2;
	
	// Perturb the reveal mask slightly with the texture's own alpha for organic branching
	float organicMask = saturate((spread - distFromTop) * 10.0 + lightningTextureSample.a * 2.0);
	float finalMask = lerp(organicMask, 1.0, isPhaseTwo);
	
	// Phase 2: Brightness spike and return stroke decay
	float returnFade = 1.0 - returnProgress;
	float currentBrightness = _SGT_LightningBrightness + (isPhaseTwo * _SGT_AnimationBrightness * returnFade);
	
	// Maintain opacity during reveal, then fade out during the return stroke decay
	float currentAlpha = lerp(1.0, returnFade, isPhaseTwo);

	o.Albedo = lightningTextureSample.rgb * currentBrightness * finalMask * currentAlpha * lightningTextureSample.a;
	
	o.Albedo *= 1.0 - CW_GetVolumetricsOpacity(d.screenUV, distance(_WorldSpaceCameraPos, d.worldSpacePosition));

	// Fade with ocean surface
	float  surfaceDistance;
	float3 surfaceNormal;
	float cameraDistance = distance(d.worldSpacePosition, _WorldSpaceCameraPos);
	SGT_GetOceanData(d.screenUV, surfaceDistance, surfaceNormal);

	o.Albedo *= cameraDistance > surfaceDistance && surfaceDistance >= 0.0;
}