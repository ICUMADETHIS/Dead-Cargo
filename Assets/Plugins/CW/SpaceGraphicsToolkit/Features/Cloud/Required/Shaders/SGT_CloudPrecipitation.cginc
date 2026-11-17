BEGIN_PROPERTIES
	[HideInInspector] _SGT_Brightness("", Float) = 0
	[HideInInspector] _SGT_WrapSize("", Vector) = (0,0,0,0)
	[HideInInspector] _SGT_Offset("", Vector) = (0,0,0)
	[HideInInspector] _SGT_StretchDirection("", Vector) = (0,0,0,0)
	[HideInInspector] _SGT_RadiusData("", Vector) = (0,0,0,0)
	
	[HideInInspector] _SGT_CoverageTex("", 2D) = "white" {}
	[HideInInspector] _SGT_TemperatureTex("", 2D) = "white" {}
	[HideInInspector] _SGT_CoverageChannel("", Vector) = (1,0,0,0)
	[HideInInspector] _SGT_CoverageThreshold("", Float) = 0.5
	
	[HideInInspector] _SGT_RainTex("", 2D) = "white" {}
	[HideInInspector] _SGT_RainData("", Vector) = (0,0,0,0)
	[HideInInspector] _SGT_RainAmount("", Float) = 1

	[HideInInspector] _SGT_SnowTex("", 2D) = "white" {}
	[HideInInspector] _SGT_SnowData("", Vector) = (0,0,0,0)
	[HideInInspector] _SGT_SnowSway("", Vector) = (0,0,0,0)
	
	[Header(CLOUDS)]
	[Toggle(_SGT_CLOUDS)] _SGT_Clouds("	Enable", Float) = 0
	[HideInInspector] [NoScaleOffset] _SGT_CloudTex("", 2D) = "black" {}
	[HideInInspector] _SGT_CloudOpacity("", Vector) = (0.0, 0.0, 0.0, 0.0)
	[HideInInspector] _SGT_CloudWarp("", Float) = 0
	[HideInInspector] _SGT_SnowMinHeight("", Float) = 0
END_PROPERTIES

BEGIN_CBUFFER
END_CBUFFER

BEGIN_DEFINES
	#pragma shader_feature_local _SGT_CLOUDS_OFF _SGT_CLOUDS
END_DEFINES

float    _SGT_Brightness;
float4   _SGT_WrapSize;
float4   _SGT_RadiusData;
float4   _SGT_StretchDirection;
float4x4 _SGT_WorldToLocal;
float4x4 _SGT_WorldToPlanet;

sampler2D _SGT_CoverageTex;
sampler2D _SGT_TemperatureTex;
float     _SGT_TemperatureInvert;
float4    _SGT_CoverageChannel;
float     _SGT_CoverageThreshold;

sampler2D _SGT_RainTex;
float4    _SGT_RainData;   // x: Speed, y: Brightness, z: Length, w: Thickness
float     _SGT_RainAmount;
float3    _SGT_CameraVelocity; // Global
float4    _SGT_CameraVelocityData;

sampler2D _SGT_SnowTex;
float4    _SGT_SnowData;   // x: Speed, y: Brightness, z: Size, w: Amount
float4    _SGT_SnowSway;   // x: Freq, y: Amp
float     _SGT_SnowMinHeight;

// CLOUDS
sampler2D _SGT_CloudTex;
float4    _SGT_CloudOpacity;
float     _SGT_CloudWarp;
float4x4  _SGT_CloudMatrix;

sampler2D _SGT_PrecipitationDepthTex;
float4x4  _SGT_PrecipitationDepthMatrix;
float     _SGT_PrecipitationExists;

float2 SGT_DirectionToEquirectangular(float3 dir)
{
	dir = normalize(dir);
	float u = atan2(dir.z, dir.x) / (2.0 * 3.141592653) + 0.5;
	float v = asin(clamp(dir.y, -1.0, 1.0)) / 3.141592653 + 0.5;
	return float2(u, v);
}

float SGT_SampleCloudDensity(float3 wpos, float bias)
{
	float3 p = normalize(mul(_SGT_CloudMatrix, float4(wpos, 1.0f)).xyz);
	float2 w = float2(-p.x, p.y);
	float2 z = normalize(w) * pow(length(w), 1.0f / (_SGT_CloudWarp + 1.0f));
	return saturate(dot(tex2Dbias(_SGT_CloudTex, float4(z * 0.5f + 0.5f, 0, bias)), _SGT_CloudOpacity));
}

void SSS_Vert(inout SSS_VertexData v)
{
	float3 wcam = _WorldSpaceCameraPos;
	float  seed = v.texcoord0.w;
	
	// Determine virtual wrap boundaries based strictly on coordinate center to prevent swaying jumps
	float3 localCenterBase = frac(v.position - SSS_WorldToObject(wcam) * _SGT_WrapSize.y + 0.5f) - 0.5f;
	localCenterBase *= _SGT_WrapSize.x;
	
	float3 worldCenterBase = SSS_ObjectToWorld(localCenterBase);
	float3 localObjPosBase = mul(_SGT_WorldToLocal, float4(worldCenterBase, 1.0f)).xyz;
	float2 localCoord      = SGT_DirectionToEquirectangular(localObjPosBase);
	float3 planetPos       = mul(_SGT_WorldToPlanet, float4(worldCenterBase, 1.0f)).xyz;
	float2 planetCoord     = SGT_DirectionToEquirectangular(planetPos);
	
	// Evaluate Textures
	float temperature = tex2Dlod(_SGT_TemperatureTex, float4(planetCoord, 0.0f, 0.0f)).r;
	float coverage    = dot(tex2Dlod(_SGT_CoverageTex, float4(localCoord, 0.0f, 0.0f)), _SGT_CoverageChannel);
	float weatherMask = saturate((coverage - _SGT_CoverageThreshold) / max(0.0001f, 1.0f - _SGT_CoverageThreshold));
	
	// Decide if particle is Snow (1.0) or Rain (0.0) based on seed & temperature
	float isSnow = step(frac(seed * 8.43f), lerp(1.0f - temperature, temperature, _SGT_TemperatureInvert));
	
	// Calculate altitude relative to the InnerRadius and smoothly blend snow to rain if too low
	float heightAboveInner = length(localObjPosBase) - _SGT_RadiusData.x;
	float snowWeight       = saturate(heightAboveInner / max(0.0001f, _SGT_SnowMinHeight));
	isSnow                *= snowWeight * snowWeight; // Squared 0..1 weight
	
	// Probability spawn check
	float spawnChance = weatherMask * lerp(_SGT_RainAmount, _SGT_SnowData.w, isSnow);
	float spawnProb   = step(frac(seed * 7.13f), spawnChance);
	
	// Calculate specific fall speeds and offsets
	float currentSpeed = lerp(_SGT_RainData.x, _SGT_SnowData.x, isSnow);
	float fallSpeed    = lerp(0.8f, 1.2f, seed) * currentSpeed;
	float fallOffset   = _Time.y * fallSpeed;
	
	// Re-evaluate Wrap with Virtual Camera Movement
	float3 virtualWcam = wcam - _SGT_StretchDirection.xyz * fallOffset * _SGT_WrapSize.x;
	float3 wcam_snap   = virtualWcam + frac((virtualWcam - _SGT_Offset) * _SGT_WrapSize.y) * _SGT_WrapSize.x;
	float3 ocam_snap   = SSS_WorldToObject(wcam_snap);
	
	float3 localCenter = frac(v.position - ocam_snap * _SGT_WrapSize.y + 0.5f) - 0.5f;
	localCenter *= _SGT_WrapSize.x;
	
	// --- STABLE SWAY LOGIC ---
	// Create an arbitrary orthogonal axis to the stretch direction that ignores the camera.
	float3 refAxis = abs(_SGT_StretchDirection.y) < 0.999f ? float3(0.0f, 1.0f, 0.0f) : float3(1.0f, 0.0f, 0.0f);
	float3 baseSwayDir = normalize(cross(_SGT_StretchDirection.xyz, refAxis));
	float3 orthoSwayDir = cross(_SGT_StretchDirection.xyz, baseSwayDir);
	
	// Rotate the sway plane using the particle's seed so they don't all sway in unison on the same axis.
	float swayRotSin = sin(seed * 6.2831853f);
	float swayRotCos = cos(seed * 6.2831853f);
	float3 particleSwayDir = baseSwayDir * swayRotCos + orthoSwayDir * swayRotSin;
	
	// Apply Sway for Snow
	float sway = sin(_Time.y * _SGT_SnowSway.x + seed * 6.2831853f) * _SGT_SnowSway.y * isSnow;
	localCenter += particleSwayDir * sway;
	
	// Expand in world space (billboard facing the camera)
    float3 camVel       = _SGT_CameraVelocity * _SGT_CameraVelocityData.x;
    float  camSpeed     = length(camVel);
    float3 cappedCamVel = camSpeed > _SGT_CameraVelocityData.y ? (camVel / camSpeed) * _SGT_CameraVelocityData.y : camVel;

	// Apparent movement = (falling vector) - (camera movement vector)
	float3 relativeVel = _SGT_StretchDirection.xyz * fallSpeed - cappedCamVel;
	float relativeSpeed = length(relativeVel);
	
	float3 finalStretchDir = relativeVel / max(0.0001f, relativeSpeed);
	float speedMultiplier = relativeSpeed / max(0.0001f, fallSpeed);

	float3 ocam = SSS_WorldToObject(wcam);
	float3 view = normalize(ocam - localCenter);
	float3 up   = cross(finalStretchDir, view);
	
	// Radii Fading
	float3 finalWorldCenter = SSS_ObjectToWorld(localCenter);
	float3 finalLocalObjPos = mul(_SGT_WorldToLocal, float4(finalWorldCenter, 1.0f)).xyz;
	float distFromCenter = length(finalLocalObjPos);
	
	float radiusRange = max(0.0001f, _SGT_RadiusData.y - _SGT_RadiusData.x);
	float fadeMargin  = radiusRange * 0.1f;
	
	// Fade from 0 to 1 OUTSIDE the inner radius
	float innerFade   = smoothstep(_SGT_RadiusData.x - fadeMargin, _SGT_RadiusData.x, distFromCenter);
	// Fade from 1 to 0 OUTSIDE the outer radius
	float outerFade   = 1.0f - smoothstep(_SGT_RadiusData.y, _SGT_RadiusData.y + fadeMargin, distFromCenter);
	
	float radialFade  = innerFade * outerFade;
	
	// Wrap Fading
	float edgeDist = max(abs(localCenter.x), max(abs(localCenter.y), abs(localCenter.z))) / _SGT_WrapSize.x;
	float wrapFade = smoothstep(0.5f, 0.3f, edgeDist);
	
	// Determine size and thickness
	float currentBoost     = lerp(0.4f, 2.0f, weatherMask);
	float currentLength    = lerp(_SGT_RainData.z, _SGT_SnowData.z, isSnow) * currentBoost;
	float currentThickness = lerp(_SGT_RainData.w, _SGT_SnowData.z, isSnow) * currentBoost;
	
	// Apply Expansion (Swapped += to -= for the width vector as requested)
	v.position = localCenter;
	v.position += finalStretchDir * (-v.texcoord0.y) * (currentLength * speedMultiplier) * spawnProb;
	v.position -= normalize(up) * v.texcoord0.z * currentThickness * spawnProb;
	
	// Pass brightness multiplier & fading logic to Fragment Data
	v.extraV2F0.x = lerp(_SGT_RainData.y, _SGT_SnowData.y, isSnow);
	//v.extraV2F0.x *= length(up);
	v.extraV2F0.x *= wrapFade;
	v.extraV2F0.x *= radialFade;
	v.extraV2F0.x *= spawnProb;
	
	float stretchFade = 1.0f / max(1.0f, speedMultiplier * _SGT_CameraVelocityData.z);
	v.extraV2F0.x *= stretchFade;
	
	// Pass weather type flag to Fragment Data (1 = Snow, 0 = Rain)
	v.extraV2F0.y = isSnow;
}

void SSS_Frag(inout SSS_SurfaceData o, inout SSS_FragmentData d)
{
	float finalColor     = d.extraV2F0.x;
	float isSnow         = d.extraV2F0.y;
	
	// Sample textures
	float2 texUV = d.texcoord0.xy * 0.5f + 0.5f;
	float4 rainTex = tex2D(_SGT_RainTex, texUV);
	float4 snowTex = tex2D(_SGT_SnowTex, texUV);
	float4 weatherTex = lerp(rainTex, snowTex, isSnow);
	
	finalColor *= weatherTex.a;
	
	// Ocean surface data at this screen pixel
	float  surfaceDistance;
	float3 surfaceNormal;
	SGT_GetOceanData(d.screenUV, surfaceDistance, surfaceNormal);

	// True if no ocean exists, OR if both the camera and particle are above water
	float cameraDistance = distance(d.worldSpacePosition, _WorldSpaceCameraPos);
	bool isAboveOrNoWater = (surfaceDistance == 0.0) || (surfaceDistance > 0.0 && surfaceDistance > cameraDistance);

	finalColor *= isAboveOrNoWater;
	
	#if _SGT_CLOUDS
		float shadow2 = SGT_SampleCloudDensity(_WorldSpaceCameraPos - d.worldSpaceViewDir * 10.0f, 10.0f);
		finalColor *= 1.0f - shadow2;
	#endif
	
	#if _SSS_HDRP && !_SSS_NO_DERIVATIVES
		finalColor *= GetCurrentExposureMultiplier();
		finalColor *= 0.25;
	#endif
	
	o.Albedo = weatherTex.rgb * _SGT_Brightness;
	o.Alpha  = weatherTex.a * finalColor;
	
	if (_SGT_PrecipitationExists > 0.0)
    {
        float3 gridPos = mul(_SGT_PrecipitationDepthMatrix, float4(d.worldSpacePosition, 1.0f)).xyz;

        if (gridPos.x >= 0.0f && gridPos.x <= 1.0f && gridPos.y >= 0.0f && gridPos.y <= 1.0f)
        {
            float groundDist = tex2D(_SGT_PrecipitationDepthTex, gridPos.xy).r;
            o.Alpha *= step(gridPos.z, groundDist);
        }
    }
}