//<sss_checksum>B792C88F</sss_checksum>
Shader "Space Graphics Toolkit/Landscape"
{
Properties
{



	[KeywordEnum(Square, Sphere)] _SGT_Shape ("Shape", Float) = 0
	_SGT_Occlusion ("Occlusion", Range(0, 1)) = 0

	[Header(SKY)]
	[Toggle(_SGT_SKY)] _SGT_Sky("	Enable", Float) = 0

	[Header(CLOUDS)]
	[Toggle(_SGT_CLOUDS)] _SGT_Clouds("	Enable", Float) = 0
	[HideInInspector] [NoScaleOffset] _SGT_CloudShadowTex("", 2D) = "black" {}
	[HideInInspector] _SGT_CloudShadowDirection("", Vector) = (0,0,0,0)
	[HideInInspector] _SGT_CloudShadowOpacity("", Vector) = (0,0,0,0)
	
	[Header(OCEAN FADE)]
	[Toggle(_SGT_OCEAN_FADE)] _SGT_OceanFade("	Enable", Float) = 0
	[HideInInspector] _SGT_OceanDistance("", Vector) = (0,0,0,0)
	[HideInInspector] _SGT_OceanColor("", Color) = (0,0,0)
	[HideInInspector] _SGT_OceanSmoothness("", Float) = 0
	[HideInInspector] _SGT_OceanLightDirection("", Vector) = (0,0,0,0)


[HideInInspector]_BUILTIN_QueueOffset("Float", Float) = 0
[HideInInspector]_BUILTIN_QueueControl("Float", Float) = -1
}
SubShader
{
Tags
{
// RenderPipeline: <None>
"RenderType"="Opaque"
"BuiltInMaterialType" = "Lit"
"Queue"="AlphaTest"
// DisableBatching: <None>
"ShaderGraphShader"="true"
"ShaderGraphTargetId"="BuiltInLitSubTarget"
}





Pass
{
    Name "BuiltIn Forward"
    Tags
    {
        "LightMode" = "ForwardBase"
    }

// Render State
Cull Back
Blend One Zero
ZTest LEqual
ZWrite On

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM
#define _SSS_PASS_BUILTIN_FORWARD 1

#define _SSS_BIRP 1


// Pragmas
#pragma target 3.0
#pragma multi_compile_instancing
#pragma multi_compile_fog
#pragma multi_compile_fwdbase
#pragma vertex vert
#pragma fragment frag

// Keywords
#pragma multi_compile _ _SCREEN_SPACE_OCCLUSION
#pragma multi_compile _ LIGHTMAP_ON
#pragma multi_compile _ DIRLIGHTMAP_COMBINED
#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
#pragma multi_compile _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS _ADDITIONAL_OFF
#pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
#pragma multi_compile _ _SHADOWS_SOFT
#pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
#pragma multi_compile _ SHADOWS_SHADOWMASK
// GraphKeywords: <None>

// Defines
#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_TEXCOORD0
#define ATTRIBUTES_NEED_TEXCOORD1
#define ATTRIBUTES_NEED_TEXCOORD2
#define ATTRIBUTES_NEED_TEXCOORD3
#define ATTRIBUTES_NEED_COLOR
#define ATTRIBUTES_NEED_INSTANCEID
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_NORMAL_WS
#define VARYINGS_NEED_TANGENT_WS
#define VARYINGS_NEED_TEXCOORD0
#define VARYINGS_NEED_TEXCOORD1
#define VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
#define VARYINGS_NEED_CULLFACE
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_FORWARD
#define BUILTIN_TARGET_API 1
#define _BUILTIN_AlphaClip 1
#define _BUILTIN_ALPHATEST_ON 1
#ifdef _BUILTIN_SURFACE_TYPE_TRANSPARENT
#define _SURFACE_TYPE_TRANSPARENT _BUILTIN_SURFACE_TYPE_TRANSPARENT
#endif
#ifdef _BUILTIN_ALPHATEST_ON
#define _ALPHATEST_ON _BUILTIN_ALPHATEST_ON
#endif
#ifdef _BUILTIN_AlphaClip
#define _AlphaClip _BUILTIN_AlphaClip
#endif
#ifdef _BUILTIN_ALPHAPREMULTIPLY_ON
#define _ALPHAPREMULTIPLY_ON _BUILTIN_ALPHAPREMULTIPLY_ON
#endif


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Shim/Shims.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/LegacySurfaceVertex.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/ShaderGraphFunctions.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
 float4 uv0 : TEXCOORD0;
 float4 uv1 : TEXCOORD1;
 float4 uv2 : TEXCOORD2;
 float4 uv3 : TEXCOORD3;
 float4 color : COLOR;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float3 positionWS;
 float3 normalWS;
 float4 tangentWS;
 float4 texCoord0;
 float4 texCoord1;
#if defined(LIGHTMAP_ON)
 float2 lightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
 float3 sh;
#endif
 float4 fogFactorAndVertexLight;
 float4 shadowCoord;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
 float4 extraV2F0;
};
struct SurfaceDescriptionInputs
{
 float3 WorldSpaceNormal;
 float3 TangentSpaceNormal;
 float3 WorldSpaceTangent;
 float3 WorldSpacePosition;
 float4 uv0;
 float4 uv1;
 float FaceSign;
 float4 extraV2F0;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
 float4 uv0;
 float4 uv1;
 float4 uv2;
 float4 uv3;
 float4 VertexColor;
 uint InstanceID;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
#if defined(LIGHTMAP_ON)
 float2 lightmapUV : INTERP0;
#endif
#if !defined(LIGHTMAP_ON)
 float3 sh : INTERP1;
#endif
 float4 tangentWS : INTERP2;
 float4 texCoord0 : INTERP3;
 float4 texCoord1 : INTERP4;
 float4 fogFactorAndVertexLight : INTERP5;
 float4 shadowCoord : INTERP6;
 float4 extraV2F0 : INTERP7;
 float3 positionWS : INTERP8;
 float3 normalWS : INTERP9;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
#if defined(LIGHTMAP_ON)
output.lightmapUV = input.lightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
output.sh = input.sh;
#endif
output.tangentWS.xyzw = input.tangentWS;
output.texCoord0.xyzw = input.texCoord0;
output.texCoord1.xyzw = input.texCoord1;
output.fogFactorAndVertexLight.xyzw = input.fogFactorAndVertexLight;
output.shadowCoord.xyzw = input.shadowCoord;
output.extraV2F0.xyzw = input.extraV2F0;
output.positionWS.xyz = input.positionWS;
output.normalWS.xyz = input.normalWS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
#if defined(LIGHTMAP_ON)
output.lightmapUV = input.lightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
output.sh = input.sh;
#endif
output.tangentWS = input.tangentWS.xyzw;
output.texCoord0 = input.texCoord0.xyzw;
output.texCoord1 = input.texCoord1.xyzw;
output.fogFactorAndVertexLight = input.fogFactorAndVertexLight.xyzw;
output.shadowCoord = input.shadowCoord.xyzw;
output.extraV2F0 = input.extraV2F0.xyzw;
output.positionWS = input.positionWS.xyz;
output.normalWS = input.normalWS.xyz;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties
CBUFFER_START(UnityPerMaterial)





CBUFFER_END


// Object and Global properties

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Includes
// UNITY_SHADER_NO_UPGRADE
float3 SSS_HClipToScreen(float4 v)
{
	float3 uv = v.xyz / v.w;
	#if UNITY_UV_STARTS_AT_TOP
		uv.y = -uv.y;
	#endif
	uv.xy = uv.xy * 0.5 + 0.5;
	return uv;
}

#if _SSS_HDRP
	float3 SSS_WorldToAbsolute(float3 v) { return GetAbsolutePositionWS(v); }
	float3 SSS_AbsoluteToWorld(float3 v) { return GetCameraRelativePositionWS(v); }
#else
	float3 SSS_WorldToAbsolute(float3 v) { return v; }
	float3 SSS_AbsoluteToWorld(float3 v) { return v; }
#endif

float3 SSS_WorldToView(float3 v) { return TransformWorldToView(SSS_AbsoluteToWorld(v)); }
float3 SSS_WorldToObject(float3 v) { return TransformWorldToObject(SSS_AbsoluteToWorld(v)); }
float3 SSS_WorldToScreen(float3 v) { return SSS_HClipToScreen(TransformWorldToHClip(SSS_AbsoluteToWorld(v))); }
float3 SSS_ObjectToScreen(float3 v) { return SSS_HClipToScreen(TransformObjectToHClip(v)); }
float3 SSS_ObjectToWorld(float3 v) { return SSS_WorldToAbsolute(TransformObjectToWorld(v)); }
float3 SSS_ObjectToView(float3 v) { return TransformWorldToView(TransformObjectToWorld(v)); }
float3 SSS_ScreenToWorld(float3 v) { return SSS_WorldToAbsolute(ComputeWorldSpacePosition(v.xy, v.z, UNITY_MATRIX_I_VP)); }
float3 SSS_ScreenToObject(float3 v) { return SSS_WorldToObject(SSS_ScreenToWorld(v)); }
float3 SSS_ScreenToView(float3 v) { return SSS_WorldToView(SSS_ScreenToWorld(v)); }
float3 SSS_ViewToWorld(float3 v) { return mul(UNITY_MATRIX_I_V, float4(v, 1.0)).xyz; }
float3 SSS_ViewToObject(float3 v) { return TransformWorldToObject(SSS_ViewToWorld(v)); }
float3 SSS_ViewToScreen(float3 v) { return SSS_HClipToScreen(TransformWViewToHClip(v)); }
float3 SSS_ObjectToWorldDir(float3 v)
{
	#if _SSS_BIRP
		return TransformObjectToWorldDir(v);
	#else
		return TransformObjectToWorldDir(v, true);
	#endif
}
float3 SSS_ObjectToViewDir(float3 v)
{
	#if _SSS_BIRP
		return TransformWorldToViewDir(TransformObjectToWorldDir(v));
	#else
		return TransformWorldToViewDir(TransformObjectToWorldDir(v, false), true);
	#endif
}
float3 SSS_WorldToObjectDir(float3 v)
{
	#if _SSS_BIRP
		return TransformWorldToObjectDir(v);
	#else
		return TransformWorldToObjectDir(v, true);
	#endif
}
float3 SSS_WorldToViewDir(float3 v)
{
	#if _SSS_BIRP
		return TransformWorldToViewDir(v);
	#else
		return TransformWorldToViewDir(v, true);
	#endif
}
float3 SSS_ViewToObjectDir(float3 v)
{
	#if _SSS_URP || _SSS_HDRP
		return SSS_WorldToObjectDir(mul(UNITY_MATRIX_I_V, float4(v, 0.0)).xyz);
	#else
		return SSS_WorldToObjectDir(mul((float3x3)UNITY_MATRIX_I_V, v));
	#endif
}
float3 SSS_ViewToWorldDir(float3 v)
{
	return mul(UNITY_MATRIX_I_V, float4(v, 0.0)).xyz;
}

#if _SSS_NO_DERIVATIVES
	float3 SSS_GetSceneColor(float2 uv) { return float3(0.0, 0.0, 0.0); }
	float3 SSS_GetSceneColorHD(float2 uv) { return SSS_GetSceneColor(uv); }
	float  SSS_GetSceneDepth(float2 uv) { return 0.0; }
#else
	#if _SSS_URP
		float3 SSS_GetSceneColor(float2 uv) { return SHADERGRAPH_SAMPLE_SCENE_COLOR(uv).xyz; }
		float3 SSS_GetSceneColorHD(float2 uv) { return SSS_GetSceneColor(uv); }
	#elif _SSS_HDRP
		float3 SSS_GetSceneColor(float2 uv) { return SHADERGRAPH_SAMPLE_SCENE_COLOR(uv).xyz; }
		float3 SSS_GetSceneColorHD(float2 uv)
		{
			#if defined(REQUIRE_OPAQUE_TEXTURE) && defined(_SURFACE_TYPE_TRANSPARENT) && defined(SHADERPASS) && (SHADERPASS != SHADERPASS_LIGHT_TRANSPORT) && (SHADERPASS != SHADERPASS_PATH_TRACING) && (SHADERPASS != SHADERPASS_RAYTRACING_VISIBILITY) && (SHADERPASS != SHADERPASS_RAYTRACING_FORWARD)
			return SampleCameraColor(uv, 0);
			#endif
			#if defined(REQUIRE_OPAQUE_TEXTURE) && defined(CUSTOM_PASS_SAMPLING_HLSL) && defined(SHADERPASS) && (SHADERPASS == SHADERPASS_DRAWPROCEDURAL || SHADERPASS == SHADERPASS_BLIT)
			return CustomPassSampleCameraColor(uv, 0);
			#endif
			return float3(0.0, 0.0, 0.0);
		}
	#else
		#if defined(UNITY_DECLARE_OPAQUE_TEXTURE_INCLUDED)
			float3 SSS_GetSceneColor(float2 uv) { return SampleSceneColor(uv); }
		#else
			sampler2D _CameraOpaqueTexture; float3 SSS_GetSceneColor(float2 uv) { return tex2D(_CameraOpaqueTexture, uv).xyz; }
		#endif
		float3 SSS_GetSceneColorHD(float2 uv) { return SSS_GetSceneColor(uv); }
	#endif

	float SSS_GetSceneDepth(float2 uv) { return SHADERGRAPH_SAMPLE_SCENE_DEPTH(uv); }
#endif

float3 SSS_GetSceneWorldPosition(float2 screenUV, float sceneDepth)
{
	#if _SSS_BIRP
		float4 clipPos  = float4(screenUV * 2.0f - 1.0f, 0.0f, 1.0f);
		float4 viewPos  = mul(unity_CameraInvProjection, clipPos);
		float3 worldDir = mul((float3x3)UNITY_MATRIX_I_V, viewPos.xyz);
					
		return _WorldSpaceCameraPos + worldDir * LinearEyeDepth(sceneDepth);
	#else
		float4 clipPos = float4(screenUV * 2.0 - 1.0, sceneDepth, 1.0);
					
		#if UNITY_UV_STARTS_AT_TOP
			clipPos.y = -clipPos.y;
		#endif
					
		float4 worldPos = mul(UNITY_MATRIX_I_VP, clipPos);
					
		worldPos.xyz /= worldPos.w;
					
		#if _SSS_HDRP
			worldPos.xyz = GetAbsolutePositionWS(worldPos.xyz);
		#endif
					
		return worldPos.xyz;
	#endif
}

float SSS_GetSceneWorldDistance(float2 screenUV, float sceneDepth)
{
	return distance(_WorldSpaceCameraPos, SSS_GetSceneWorldPosition(screenUV, sceneDepth));
}

float3 SSS_UnpackNormalScale(float4 c, float s)
{
	#if _SSS_BIRP
		return UnpackScaleNormal(c, s);
	#else
		return UnpackNormalScale(c, s);
	#endif
}

struct SSS_VertexData
{
	float  instanceID;
	float3 position;
	float3 normal;
	float3 tangent;
	float4 color;
	float4 texcoord0;
	float4 texcoord1;
	float4 texcoord2;
	float4 texcoord3;
	float4 extraV2F0;
	float4 extraV2F1;
	float4 extraV2F2;
	float4 extraV2F3;
	float4 extraV2F4;
	float4 extraV2F5;
	float4 extraV2F6;
	float4 extraV2F7;
	



};

struct SSS_FragmentData
{
	float3 localSpacePosition;
	float3 localSpaceNormal;
	float3 localSpaceTangent;
	
	float3 worldSpacePosition;
	float3 worldSpaceNormal;
	float3 worldSpaceTangent;
	//float tangentSign;

	float3 worldSpaceViewDir;
	//float3 tangentSpaceViewDir;
	
	float4 texcoord0;
	float4 texcoord1;
	float4 texcoord2;
	float4 texcoord3;
	
	float2 screenUV;
	float4 screenPos;

	float4 vertexColor;
	bool isFrontFace;
	
	float4 extraV2F0;
	float4 extraV2F1;
	float4 extraV2F2;
	float4 extraV2F3;
	float4 extraV2F4;
	float4 extraV2F5;
	float4 extraV2F6;
	float4 extraV2F7;

	float3x3 TBNMatrix;
	



};

struct SSS_SurfaceData
{
	float3 Albedo;
	float  Smoothness;
	float3 Normal;
	float3 Emission;
	float  Occlusion;
	float  Metallic;
	float  Alpha;
};

float2 _CW_PositionCompressionData; // x = start distance, y = max distance limit

#define DEBUG_SCALE 0.001
#define SSS_LOG_K          0.0001
#define SSS_MAX_WORLD_DIST 1e27

float SSS_GetCompressionRatio(float worldDist)
{
    float startDist = _CW_PositionCompressionData.x;
    float maxDist   = _CW_PositionCompressionData.y;

    // no compression nearby
    if (worldDist <= startDist)
        return 1.0;

    float excess = worldDist - startDist;

    float compressedDist =
        startDist +
        (maxDist - startDist) *
        log(1.0 + excess * SSS_LOG_K) /
        log(1.0 + SSS_MAX_WORLD_DIST * SSS_LOG_K);

    return compressedDist / worldDist;
}

float SSS_DecompressWorldDist(float compressedDist)
{
    float startDist = _CW_PositionCompressionData.x;
    float maxDist   = _CW_PositionCompressionData.y;

    if (compressedDist <= startDist)
        return compressedDist;

    float t =
        saturate(
            (compressedDist - startDist) /
            (maxDist - startDist)
        );

    return startDist +
        (
            exp(
                t *
                log(1.0 + SSS_MAX_WORLD_DIST * SSS_LOG_K)
            ) - 1.0
        ) / SSS_LOG_K;
}

float3 SSS_CompressWorld(float3 worldPos, float3 worldCenter, float worldRadius, out float ratio) 
{
    float3 worldDelta = worldPos - _WorldSpaceCameraPos;
    float  farDist    = distance(_WorldSpaceCameraPos, worldCenter) + worldRadius;
    
    ratio = SSS_GetCompressionRatio(farDist);
    
    return _WorldSpaceCameraPos + worldDelta * ratio;
}

float3 SSS_CompressWorld(float3 worldPos) 
{
    float3 worldDelta = worldPos - _WorldSpaceCameraPos;
    float  worldDist  = length(worldDelta);
    
    return _WorldSpaceCameraPos + worldDelta * SSS_GetCompressionRatio(worldDist);
}

float3 SSS_DecompressWorld(float3 worldPos)
{
    float3 worldDelta = worldPos - _WorldSpaceCameraPos;
    float  worldDist  = length(worldDelta);
    
    return worldDist > 0.001 ? _WorldSpaceCameraPos + (worldDelta / worldDist) * SSS_DecompressWorldDist(worldDist) : _WorldSpaceCameraPos;
}

float3 SSS_DecompressWorld(float3 worldPos, float ratio) 
{
    float3 worldDelta = worldPos - _WorldSpaceCameraPos;
    return _WorldSpaceCameraPos + worldDelta / ratio;
}

float SSS_ElongateDistance(float worldDist)
{
	float minDist = 10000.0;
	float maxDist = 100000.0;
	float scale   = saturate((worldDist - minDist) / (maxDist - minDist));

	return worldDist * lerp(1.0, 1.2, scale);
}
#define SGT_OPACITY_STEPS  5

float4x4        _SGT_SkyToWorld;
float4x4        _SGT_WorldToSky;
float4          _SGT_SkyColor;
float           _SGT_SkyBrightness;
float4          _SGT_SkyAmbientTint;
float           _SGT_SkyExposure;
float4          _SGT_SkyRadius;
float4          _SGT_SkyAltitudeData;
float           _SGT_SkyDensity;
float4          _SGT_SkyMieWeight;
float4          _SGT_SkyMieData;
sampler3D       _SGT_SkyRadianceTex;
float           _SGT_SkyRadianceLod;
sampler2D       _SGT_SkyLightingTex;
sampler2D       _SGT_SkyAlbedoTex;
float           _SGT_SkyRevealStars;
float           _SGT_SkyDepthOpaque;

float     _SGT_Volumetrics_Downscale; // Global
float     _SGT_Volumetrics_NearClip; // Global
Texture2D _SGT_Volumetrics_ColorTex; // Global
float4    _SGT_Volumetrics_ColorSize; // Global
Texture2D _SGT_Volumetrics_DepthTex; // Global
float2    _SGT_Volumetrics_DepthSize; // Global

SamplerState my_linear_clamp_sampler;
SamplerState my_point_clamp_sampler;

sampler2D _SGT_BlueNoiseTex; // Global
float     _SGT_Frame; // Global
float3    _SGT_AmbientColor; // Global
	
float SGT_DitherBlue(float2 screenUV)
{
	float2 pixel = floor(screenUV * _ScreenParams.xy);
	float  noise = tex2D(_SGT_BlueNoiseTex, pixel / 64.0f).r;
	return frac(noise + _SGT_Frame / sqrt(0.5f));
}

float SGT_DitherFast(float2 screenUV)
{
    float2 R2 = float2(0.7548776662, 0.5698402909);
    float2 p  = screenUV * _ScreenParams.xy + frac(_Time.y * R2) * 256.0;
    return frac(52.9829189 * frac(dot(p, float2(0.06711056, 0.00583715))));
}
	
float2 SGT_DirectionToEquirectangular(float3 dir)
{
	dir = normalize(dir);
	float u = atan2(dir.z, dir.x) / (2.0 * 3.141592653) + 0.5;
	float v = asin(clamp(dir.y, -1.0, 1.0)) / 3.141592653 + 0.5;
	return float2(u, v);
}

float3 SGT_SphereTest(float3 ray, float3 rayD, float radius)
{
	float B = -dot(ray, rayD);
	float C = dot(ray, ray) - radius * radius;
	float D = B * B - C;
	float E = sqrt(max(D, 0.0f));
	return float3(B - E, B + E, D);
}

float SGT_InverseLerp(float a, float b, float v)
{
	return (v - a) / (b - a);
}

float3 SGT_CalculateMie(float3 ray, float mie)
{
	return normalize(ray * _SGT_SkyMieWeight.xyz + 1e-6) * mie;
}

float SGT_GetPitch01(float3 rayDir, float3 up, float altitude01)
{
	float power = 2.0f;
	float eyeHeight = _SGT_SkyRadius.x + altitude01 * (_SGT_SkyRadius.y - _SGT_SkyRadius.x);
	float horizonAngleFromDown = -acos(clamp(_SGT_SkyRadius.x / max(eyeHeight, 0.00001f), -1.0f, 1.0f));
	float rayAng = asin(clamp(dot(up, rayDir), -1.0f, 1.0f));
	float sun01;

	if (rayAng < horizonAngleFromDown)
	{
		sun01 = SGT_InverseLerp(-3.141592653 * 0.5f, horizonAngleFromDown, rayAng);
		sun01 = 1.0f - pow(1.0f - sun01, 1.0f / power);
		sun01 = sun01 * 0.5f;
	}
	else
	{
		sun01 = SGT_InverseLerp(horizonAngleFromDown, 3.141592653 * 0.5f, rayAng);
		sun01 = pow(sun01, 1.0f / power);
		sun01 = sun01 * 0.5f + 0.5f;
	}

	return sun01;
}

float3 GetLutCoord(float3 rayPos, float3 rayDir, float3 sunDir, float dither)
{
	float3 up         = normalize(rayPos);
	float  eyeHeight  = length(rayPos);
	float  altitude01 = saturate(SGT_InverseLerp(_SGT_SkyRadius.z, 1.0f, eyeHeight));
	float  pitch01    = SGT_GetPitch01(rayDir, up, altitude01);
	float  sun01      = SGT_GetPitch01(sunDir, up, altitude01);
	
	pitch01 += (dither - 0.5f) / 128.0f;
	sun01   += (dither - 0.5f) /  64.0f;
	
	return float3(pitch01, altitude01, sun01);
}

float2 SGT_GetDensity2(float3 pos, float weightMul)
{
	float alt01 = saturate((1.0f - length(pos)) * _SGT_SkyAltitudeData.z);

	return pow(alt01, _SGT_SkyAltitudeData.xy * weightMul) * _SGT_SkyDensity;
}

float SGT_GetOpacity(float3 rayPos, float3 rayDir, float rayMax, float weightMul)
{
	float2 opticalDepth = 0.0f;
	float  rayStep      = rayMax / SGT_OPACITY_STEPS;
	float  t            = rayStep * 0.5f;
	
	for (int i = 0; i < SGT_OPACITY_STEPS; i++)
	{
		float3 pos = rayPos + rayDir * t;

		opticalDepth += SGT_GetDensity2(pos, weightMul) * rayStep;
		
		t += rayStep;
	}
	
	return 1.0f - exp((opticalDepth.x + opticalDepth.y) * -20.0f);
}

float4 SGT_SampleLUT(float3 coord)
{
	return tex3Dlod(_SGT_SkyRadianceTex, float4(coord, _SGT_SkyRadianceLod));
}

float3 SGT_AtmosphereColor(float3 rayPos, float3 sunDir, float dither)
{
	float3 up         = normalize(rayPos);
	float  eyeHeight  = length(rayPos);
	float  altitude01 = saturate(SGT_InverseLerp(_SGT_SkyRadius.z, 1.0f, eyeHeight));
	float  sun01      = 0.5f + 0.5f * dot(sunDir, up);
		
	return tex2Dlod(_SGT_SkyLightingTex, float4(sun01, altitude01, 0.0f, 0.0f)).xyz;
}

float3 SGT_UnderwaterAtmosphere(float3 rayPos, float3 rayDir, float3 sunDir, float exposure, float dither)
{
	float3 coord = GetLutCoord(rayPos, rayDir, sunDir, dither); coord.x = 1.0f;
	float4 ray   = SGT_SampleLUT(coord);
	float3 mie   = SGT_CalculateMie(ray.xyz, ray.w);
	float3 color = ray.xyz + mie;
	
	#if _SSS_HDRP
		color *= exposure * 0.25f;
	#else
		color = 1.0 - exp(-color * exposure);
	#endif
	
	return saturate(_SGT_SkyColor.xyz * color * _SGT_SkyBrightness);
}

float4 SGT_Atmosphere(float3 rayPos, float3 rayDir, float rayFar, float rayMax, float3 sunDir, float exposure, float dither, float mieMul, float weightMul)
{
	// 1. Calculate opacity FIRST so we can use it for blending
	float opacity = SGT_GetOpacity(rayPos, rayDir, rayMax, weightMul);

	float3 coord  = GetLutCoord(rayPos, rayDir, sunDir, dither);
	float2 mieOff = 0.0f;
	
	if (rayMax * 1.02f <= rayFar) // If ground
	{
		// 2. Store the true, un-clamped sky coordinate
		float trueCoordX = coord.x;
		
		// 3. Calculate the clamped "below horizon" coordinate (original behavior)
		float clampedCoordX = min(coord.x, 0.5f - (1.0f / 128.0f));
		clampedCoordX = lerp(0.5f - (1.0f / 128.0f), clampedCoordX, coord.y * coord.y);
		
		// 4. Smoothly blend towards the true sky color as fog density/opacity increases
		coord.x = lerp(clampedCoordX, trueCoordX, opacity);
		mieOff = lerp(0.2f, 0.0f, opacity);
	}
	else
	{
		//coord.x = max(coord.x, 0.5f + (1.0f / 128.0f));
	}
	
	float4 ray    = SGT_SampleLUT(coord);
	float3 mie    = SGT_CalculateMie(ray.xyz, ray.w);
	float2 g      = lerp(_SGT_SkyMieData.xy, 0.0f, mieOff);
	float  c      = dot(rayDir, sunDir);
	float  phaseR = 3.0f / (16.0f * 3.141592653f) * (1.0f + c * c);
	float2 phaseM = 3.0f / (8.0f * 3.141592653f) * ((1.0f - g * g) * (c * c + 1.0f)) / (pow(abs(1.0f + g * g - 2.0f * c * g), 1.5f) * (2.0f + g * g));
	float3 color  = ray.xyz * phaseR + mie * dot(phaseM, 1) * mieMul;
	
	#if _SSS_HDRP
		color *= exposure * 0.25f;
	#else
		color = 1.0 - exp(-color * exposure);
	#endif
	
	#if _SSS_HDRP
		color += _SGT_AmbientColor.xyz * _SGT_SkyAmbientTint.xyz * 0.25;
	#else
		color += _SGT_AmbientColor.xyz * _SGT_SkyAmbientTint.xyz;
	#endif

	#if _SGT_SOLID_CORE_OFF // If the atmosphere below InnerMeshRadius isn't solid, then continue to darken because the LUT doesn't extend any farther
		float eyeHeight     = length(rayPos);
		float inner         = _SGT_SkyRadius.z;
		float interiorDepth = saturate((_SGT_SkyRadius.z - eyeHeight) / _SGT_SkyRadius.z);

		if (interiorDepth > 0.0)
		{
			float extinction = exp(-interiorDepth * 20.0 * _SGT_SkyDensity);

			color *= extinction;
			opacity = lerp(opacity, 1.0f, saturate(interiorDepth * 100.0f));
		}
	#endif
	
	return float4(saturate(_SGT_SkyColor.xyz * color * _SGT_SkyBrightness), opacity);
}

// Global cached light properties
float3 _SGT_CachedLightDirection;
float3 _SGT_CachedLightColor;

float3 SGT_SampleSkyReflection(float3 ocam, float3 odir)
{
    // 2. Intersect with the Atmosphere Shell (Outer radius is 1.0 in sky space)
    float3 odst = SGT_SphereTest(ocam, odir, 1.0f);
    odst.xy = max(odst.xy, 0.0f); // Ensure distances are ahead of ray
    
    float3 rayPos = ocam + odir * odst.x;
    float3 rayDir = odir;
    
    // 3. Determine Atmosphere Limits
    float rayFar = SGT_SphereTest(rayPos, rayDir, 1.0f).y; // Distance to sky exit
    float rayMax = odst.y - odst.x;                       // Max scattering distance
    
    // Optional: Intersect with the planet core so reflections don't "see through" the world
    #if _SGT_SOLID_CORE
        float3 ihit = SGT_SphereTest(ocam, odir, _SGT_SkyRadius.w);
        if (ihit.z > 0.0f && ihit.x > 0.0f) {
            rayMax = min(ihit.x - odst.x, rayMax);
        }
    #endif

    // 4. Calculate Sun Direction in Sky Space
    float3 sunDir = normalize(mul(_SGT_WorldToSky, float4(_SGT_CachedLightDirection, 0.0f)).xyz);
    
    // 5. Sample the Atmosphere
    // dither is set to 0.0 to keep reflections smooth; mieMul is 1.0
    float4 sky = SGT_Atmosphere(rayPos, rayDir, rayFar, rayMax, sunDir, _SGT_SkyExposure, 0.0, 1.0, 1.0);
    
    // 6. Apply cached light intensity
    return sky.rgb * _SGT_CachedLightColor;
}

float4 SGT_SimpleAtmosphere(float3 worldPos, float3 worldHit, float mieMul, float weightMul)
{
	float dither   = 0.5f;
	
	float3 ocam = mul(_SGT_WorldToSky, float4(worldPos, 1.0f)).xyz;
	float3 ofar = mul(_SGT_WorldToSky, float4(worldHit, 1.0f)).xyz;
	float  omax = distance(ocam, ofar);
	float3 odir = normalize(ofar - ocam);
	float3 odst = SGT_SphereTest(ocam, odir, 1.0f);
	
	odst.xy = max(odst.xy, 0.0f);
	
	float3 rayPos = ocam + odir * odst.x;
	float3 rayDir = odir;
	float  rayFar = SGT_SphereTest(rayPos, rayDir, 1.0f).y;
	float  rayMax = min(odst.y, omax) - odst.x;
	
	#if _SGT_LIGHTING
		float3 sunDir = normalize(mul(_SGT_WorldToSky, float4(_SGT_LightDirection[0].xyz, 0.0f)).xyz);
	#else
		float3 sunDir = normalize(rayPos);
	#endif
	
	return SGT_Atmosphere(rayPos, rayDir, rayFar, rayMax, sunDir, _SGT_SkyExposure, dither, mieMul, weightMul);
}

float3 CW_Depth2World(float viewDepth, float3 viewDir)
{
	return _WorldSpaceCameraPos + viewDir * viewDepth;
}
	
bool CW_DepthInBounds(float viewDepth, float3 viewDir)
{
	float3 wPos = CW_Depth2World(viewDepth, viewDir);
	float3 oPos = mul(_SGT_WorldToSky, float4(wPos, 1.0f)).xyz;
		
	return length(oPos) < 1.2f;
}
	
void CW_SampleVolumetricsDefault(float2 screenUV, float3 viewDir, out float4 outColor, out float outDepth)
{
	float4 centerColor = _SGT_Volumetrics_ColorTex.Sample(my_linear_clamp_sampler, screenUV);
	float  centerDepth = _SGT_Volumetrics_DepthTex.Sample(my_linear_clamp_sampler, screenUV).x;
		
	if (CW_DepthInBounds(centerDepth, viewDir) == true)
	{
		outColor = centerColor;
		outDepth = centerDepth;
	}
	else
	{
		outColor = 0;
		outDepth = 0;
	}
}

void CW_SampleVolumetricsDefault2(float2 screenUV, float3 viewDir, out float4 outColor, out float outDepth)
{
	outColor = _SGT_Volumetrics_ColorTex.Sample(my_linear_clamp_sampler, screenUV);
	outDepth = _SGT_Volumetrics_DepthTex.Sample(my_linear_clamp_sampler, screenUV).x;
}
	
void CW_SampleVolumetrics(float2 screenUV, float3 viewDir, float sceneDepth, out float4 outColor, out float outDepth)
{
	outColor = 0;
	outDepth = 0;
	//CW_SampleVolumetricsDefault(screenUV, viewDir, outColor, outDepth);return;
		
	if (_SGT_Volumetrics_Downscale > 1)
	{
		float2 pixelF   = screenUV * _SGT_Volumetrics_ColorSize.xy - 0.5f;
		float2 pixelI   = floor(pixelF);
		float2 snappedF = pixelF - pixelI;
			
		float2 coord0 = (pixelI + float2(0.5f, 0.5f)) * _SGT_Volumetrics_ColorSize.zw;
		float2 coord1 = (pixelI + float2(1.5f, 0.5f)) * _SGT_Volumetrics_ColorSize.zw;
		float2 coord2 = (pixelI + float2(0.5f, 1.5f)) * _SGT_Volumetrics_ColorSize.zw;
		float2 coord3 = (pixelI + float2(1.5f, 1.5f)) * _SGT_Volumetrics_ColorSize.zw;
			
		float depth0 = _SGT_Volumetrics_DepthTex.Sample(my_point_clamp_sampler, coord0).x;
		float depth1 = _SGT_Volumetrics_DepthTex.Sample(my_point_clamp_sampler, coord1).x;
		float depth2 = _SGT_Volumetrics_DepthTex.Sample(my_point_clamp_sampler, coord2).x;
		float depth3 = _SGT_Volumetrics_DepthTex.Sample(my_point_clamp_sampler, coord3).x;
			
		float4 color0 = _SGT_Volumetrics_ColorTex.Sample(my_point_clamp_sampler, coord0);
		float4 color1 = _SGT_Volumetrics_ColorTex.Sample(my_point_clamp_sampler, coord1);
		float4 color2 = _SGT_Volumetrics_ColorTex.Sample(my_point_clamp_sampler, coord2);
		float4 color3 = _SGT_Volumetrics_ColorTex.Sample(my_point_clamp_sampler, coord3);
			
		float w0 = CW_DepthInBounds(depth0, viewDir) * (depth0 < sceneDepth) * color0.w * (1.0 - snappedF.x) * (1.0 - snappedF.y);
		float w1 = CW_DepthInBounds(depth1, viewDir) * (depth1 < sceneDepth) * color1.w * snappedF.x * (1.0 - snappedF.y);
		float w2 = CW_DepthInBounds(depth2, viewDir) * (depth2 < sceneDepth) * color2.w * (1.0 - snappedF.x) * snappedF.y;
		float w3 = CW_DepthInBounds(depth3, viewDir) * (depth3 < sceneDepth) * color3.w * snappedF.x * snappedF.y;
			
		float wt = w0 + w1 + w2 + w3;
			
		if (wt > 0.0f)
		{
			outColor = (color0 * w0 + color1 * w1 + color2 * w2 + color3 * w3) / wt;
			outDepth = (depth0 * w0 + depth1 * w1 + depth2 * w2 + depth3 * w3) / wt;
		}
		else
		{
			return;
		}
	}
	else
	{
		float2 coord0 = screenUV;
		float  depth0 = _SGT_Volumetrics_DepthTex.Sample(my_point_clamp_sampler, coord0).x;
		float4 color0 = _SGT_Volumetrics_ColorTex.Sample(my_point_clamp_sampler, coord0);
		float  w0     = CW_DepthInBounds(depth0, viewDir) * (depth0 < sceneDepth);
		
		if (w0 > 0.0f)
		{
			outColor = color0;
			outDepth = depth0;
		}
		else
		{
			return;
		}
	}
}






#pragma instancing_options procedural:SetupInstancing
#if _SSS_PASS_SHADOWCASTER || _SSS_PASS_META
	#pragma multi_compile_instancing // For some reason the ShadowCaster and Meta pass in BIRP doesn't have this line
#endif


#define BATCH_CAPACITY 35
#define VERTEX_COUNT 243

float4 _CwSize;
float4 _CwAtlas;
float4 _CwWeights[VERTEX_COUNT];
float4 _CwCoords[VERTEX_COUNT];

float4 _CwOrigins[BATCH_CAPACITY];
float4 _CwPositionsA[BATCH_CAPACITY];
float4 _CwPositionsB[BATCH_CAPACITY];
float4 _CwPositionsC[BATCH_CAPACITY];

sampler2D DataP;
sampler2D DataA;
sampler2D DataN;

float3 _CwOffset;

float4x4 _CwObjectToWorld;
float4x4 _CwWorldToObject;

float _SGT_Occlusion;

// CLOUDS
sampler2D _SGT_CloudShadowTex;
float3    _SGT_CloudShadowDirection;
float4    _SGT_CloudShadowOpacity;
float4x4  _SGT_CloudShadowMatrix;

// OCEAN FADE
float  _SGT_OceanFade;
float2 _SGT_OceanDensity;
float3 _SGT_OceanColor;
float  _SGT_OceanSmoothness;
float4 _SGT_OceanLightDirection;

float _SGT_OceanRadius;

void SetupInstancing()
{
	#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
		#ifdef unity_ObjectToWorld
			#undef unity_ObjectToWorld
		#endif

		#ifdef unity_WorldToObject
			#undef unity_WorldToObject
		#endif
		
		unity_ObjectToWorld = _CwObjectToWorld;
		unity_WorldToObject = _CwWorldToObject;
	#endif
}

float SGT_SampleCloudDensity(float3 wpos)
{
	float3 cpos  = mul(_SGT_CloudShadowMatrix, float4(wpos, 1.0)).xyz;
	float3 chit  = SGT_SphereTest(cpos, _SGT_CloudShadowDirection, 1.0);
	float2 uv    = SGT_DirectionToEquirectangular(normalize(cpos + chit.y * _SGT_CloudShadowDirection));
	
	return saturate(dot(tex2D(_SGT_CloudShadowTex, uv), _SGT_CloudShadowOpacity));
}

void SSS_Vert(inout SSS_VertexData v)
{
	float vertexIndex = v.position.x;
	float squareIndex = v.position.y;
	float batchIndex  = v.instanceID;
	
	float  batchCol = batchIndex % _CwAtlas.z;
	float  batchRow = floor(batchIndex / _CwAtlas.z);
	float3 origin   = _CwOrigins[batchIndex].xyz;
	float3 weights  = _CwWeights[vertexIndex].xyz;
	float2 coord    = _CwCoords[vertexIndex].xy; coord.x /= 3.0;
	float3 position = _CwPositionsA[batchIndex].xyz * weights.x + _CwPositionsB[batchIndex].xyz * weights.y + _CwPositionsC[batchIndex].xyz * weights.z;
	
	v.position.xyz = _CwOffset + origin + tex2Dlod(DataP, float4(vertexIndex * _CwSize.x, batchIndex * _CwSize.y, 0.0, 0.0)).xyz;
	
	float3 ocam = mul(_CwWorldToObject, float4(_WorldSpaceCameraPos - _CwOffset, 1.0)).xyz;
	
	#if _SGT_SHAPE_SQUARE
		v.normal  = float3(0,1,0);
		v.tangent = float4(1,0,0,-1).xyz;
		
		v.extraV2F0.w = ocam.y;
	#elif _SGT_SHAPE_SPHERE
		v.normal = normalize(position);
		
		if (position.x == 0.0 && position.z == 0.0)
		{
			position.xz += 0.00000001;
		}
		
		v.tangent = float4(normalize(cross(float3(0,1,0), normalize(position))), -1.0).xyz;
		
		v.extraV2F0.w = length(ocam) - _SGT_OceanRadius;
	#endif
	
	#if _SGT_OCEAN_FADE
		if (_SGT_OceanFade > 0.0 && length(v.position.xyz - _CwOffset) < _SGT_OceanRadius)
		{
			float3 N = normalize(position); // sphere normal
			float3 V = normalize(_WorldSpaceCameraPos - v.position.xyz);
			float  F = pow(abs(1.0 - saturate(dot(N, V))), 2.0);

			float3 oceanPos = _CwOffset + N * _SGT_OceanRadius;
			v.position.xyz = lerp(v.position.xyz, oceanPos, F);
		}

	#endif
	
	// Calc UV
	float2 pixelS = _CwAtlas.xy * _CwAtlas.zw;
	float  pixelX = batchCol * _CwAtlas.x + coord.x * (_CwAtlas.x - 3.0) + 0.5 + squareIndex * (_CwAtlas.x / 3.0);
	float  pixelY = batchRow * _CwAtlas.y + coord.y * (_CwAtlas.y - 1.0) + 0.5;
	
	v.extraV2F0.x = pixelX / pixelS.x;
	v.extraV2F0.y = pixelY / pixelS.y;
	
	#if CW_COMPRESS_POSITIONS
		float3 wpos = mul(_CwObjectToWorld, float4(v.position, 1.0)).xyz;
		v.extraV2F0.z = distance(_WorldSpaceCameraPos, wpos);
		wpos = SSS_CompressWorld(wpos);
		v.position = mul(_CwWorldToObject, float4(wpos, 1.0)).xyz;
	#endif
}

float3 SGT_GetColor(float3 wnormal, float3 wlight)
{
	return saturate(dot(wnormal, wlight));
}

static const float WATER_DEPTH_SHALLOW = 30.0;
static const float WATER_DEPTH_MAX = 3000.0;

float SGT_DecodeWaterDepth(float encoded)
{
	float shallow = encoded * (WATER_DEPTH_SHALLOW * 2.0);
    float deep = WATER_DEPTH_SHALLOW + (encoded - 0.5) * ((WATER_DEPTH_MAX - WATER_DEPTH_SHALLOW) * 2.0);
    float isDeep = step(0.5, encoded);
    return lerp(shallow, deep, isDeep);
}

float3 SGT_ApplyOceanColor(
    float3 terrainColor,
    float  waterDepth01,
    float3 waterColor,
    float2 waterDensity,
    float  waterBlend,
    float3 ambientColor)
{
    float depth = SGT_DecodeWaterDepth(waterDepth01);
    float3 w = float3(1.0, 0.2, 0.1);

	float3 e0 = w * waterDensity.x;
	float3 e1 = w * waterDensity.y;

	float3 floorT0 = exp(-e0 * depth);
	float3 floorT1 = exp(-e1 * depth);
	
	float3 volumeT0 = floorT0 * floorT0 * floorT0;
	float3 volumeT1 = floorT1 * floorT1 * floorT1;

	float3 scatter = waterColor * ambientColor;

	float3 col0 = terrainColor * floorT0 + scatter * (1.0 - volumeT0);
	float3 col1 = terrainColor * floorT1 + scatter * (1.0 - volumeT1);

	return lerp(col0, col1, waterBlend);
}

float SGT_Bayer4x4(float2 screenPos)
{
	uint2 p = uint2(screenPos * _ScreenParams.xy) % 4;
	float bayer[16] = { 0,  8,  2, 10, 12,  4, 14,  6, 3, 11,  1,  9, 15,  7, 13,  5 };
	return (bayer[p.x + p.y * 4] + 0.5) / 16.0;
}

float SGT_Fresnel(float3 dir, float3 normal, float airIOR, float waterIOR)
{
	float cosTheta = clamp(dot(dir, normal), 0.0, 1.0);
	float F0       = pow(abs((waterIOR / airIOR - 1.0) / (waterIOR / airIOR + 1.0)), 2.0);

	return F0 + (1.0 - F0) * pow(abs(1.0 - cosTheta), 5.0);
}

void SSS_Frag(inout SSS_SurfaceData o, inout SSS_FragmentData d)
{
	#if CW_COMPRESS_POSITIONS
		d.worldSpacePosition = _WorldSpaceCameraPos - d.worldSpaceViewDir * d.extraV2F0.z;
	#endif
	
	float4 dataA = tex2Dlod(DataA, float4(d.extraV2F0.xy,0,0));
	float4 dataN = tex2Dlod(DataN, float4(d.extraV2F0.xy,0,0)); 
	float2 tnorm = dataN.xy * 2.0 - 1.0;
	
	tnorm.x = -tnorm.x; // Don't set negative tangent sign, so do it manually
	
	o.Albedo     = dataA.xyz;
	o.Occlusion  = _SGT_Occlusion;
	o.Emission   = dataA.xyz * dataN.z;
	o.Smoothness = dataN.w;
	o.Metallic   = 0.0;
	o.Normal     = float3(tnorm, sqrt(1.0 - saturate(dot(tnorm, tnorm))));
	o.Alpha      = dataN.x != 0.0 && dataN.y != 0.0;
	
	float localEyeHeight = d.extraV2F0.w;
	
	#if _SGT_OCEAN_FADE
		float  fadeTransition = step(SGT_Bayer4x4(d.screenUV), _SGT_OceanFade);
		float  cameraDistance = distance(d.worldSpacePosition, _WorldSpaceCameraPos);
		float  cameraDist01   = 1.0 - pow(abs(saturate(1.0 - cameraDistance / _SGT_OceanRadius)), 8.0);
		float3 oceanColor     = SGT_ApplyOceanColor(o.Albedo, dataA.w, _SGT_OceanColor, _SGT_OceanDensity, cameraDist01, 1.0);
		float  fade           = saturate(dataA.w > 0) * fadeTransition;

		o.Albedo     = lerp(o.Albedo, oceanColor, fade);
		o.Smoothness = lerp(o.Smoothness, _SGT_OceanSmoothness, fade);
		o.Normal     = lerp(o.Normal, float3(0.0, 0.0, 1.0), fade);
		o.Occlusion  = lerp(o.Occlusion, 0.15, fade);

		#if _SGT_SKY
			float  airIOR   = 1.0;
			float  waterIOR = 1.33;
			float3 opos     = mul(_SGT_WorldToSky, float4(d.worldSpacePosition, 1.0)).xyz;
			float3 onrm     = normalize(opos);
			float3 oview    = normalize(mul(_SGT_WorldToSky, float4(d.worldSpaceViewDir, 0.0)).xyz);
			float3 odir     = reflect(oview, onrm);
	
			o.Emission = SGT_SampleSkyReflection(opos, -odir) * fade * SGT_Fresnel(oview, onrm, airIOR, waterIOR);
			#if _SSS_HDRP
				o.Emission *= 120000;
			#endif
		#endif
		//o.Emission +=  _SGT_AmbientColor.xyz * 0.02;
	#endif
	
	#if _SGT_CLOUDS
		float shadow = SGT_SampleCloudDensity(d.worldSpacePosition);
		
		o.Albedo    = lerp(o.Albedo   , 0.0, shadow);
		o.Occlusion = lerp(o.Occlusion, 0.0, shadow);
	#endif
}


void Vert_float
	(
	float  iInstanceID,
	float3 iPosition,
	float3 iNormal,
	float3 iTangent,
	float4 iColor,
	float4 iTexcoord0,
	float4 iTexcoord1,
	float4 iTexcoord2,
	float4 iTexcoord3,

	out float3 oPosition,
	out float3 oNormal,
	out float3 oTangent,
	out float4 oExtraV2F0,
	out float4 oExtraV2F1,
	out float4 oExtraV2F2,
	out float4 oExtraV2F3,
	out float4 oExtraV2F4,
	out float4 oExtraV2F5,
	out float4 oExtraV2F6,
	out float4 oExtraV2F7
	)
{
	SSS_VertexData v = (SSS_VertexData)0;
	
	v.instanceID = iInstanceID;
	v.position   = iPosition;
	v.normal     = iNormal;
	v.tangent    = iTangent;
	v.color      = iColor;
	v.texcoord0  = iTexcoord0;
	v.texcoord1  = iTexcoord1;
	v.texcoord2  = iTexcoord2;
	v.texcoord3  = iTexcoord3;
	v.extraV2F0  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F1  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F2  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F3  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F4  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F5  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F6  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F7  = float4(0.0, 0.0, 0.0, 0.0);
	
	SSS_Vert(v);
	
	oPosition  = v.position;
	oNormal    = v.normal;
	oTangent   = v.tangent;
	oExtraV2F0 = v.extraV2F0;
	oExtraV2F1 = v.extraV2F1;
	oExtraV2F2 = v.extraV2F2;
	oExtraV2F3 = v.extraV2F3;
	oExtraV2F4 = v.extraV2F4;
	oExtraV2F5 = v.extraV2F5;
	oExtraV2F6 = v.extraV2F6;
	oExtraV2F7 = v.extraV2F7;
}

void Frag_float
	(
	inout float3 iPosition,
	inout float3 iNormal,
	inout float3 iTangent,
	bool   iIsFrontFace,
	float4 iColor,
	float4 iTexcoord0,
	float4 iTexcoord1,
	float4 iTexcoord2,
	float4 iTexcoord3,
	float4 iExtraV2F0,
	float4 iExtraV2F1,
	float4 iExtraV2F2,
	float4 iExtraV2F3,
	float4 iExtraV2F4,
	float4 iExtraV2F5,
	float4 iExtraV2F6,
	float4 iExtraV2F7,

	out float4x4 oExtra,
	out float3   oAlbedo,
	out float    oSmoothness,
	out float3   oNormal,
	out float3   oEmission,
	out float    oOcclusion,
	out float    oMetallic,
	out float    oAlpha
	)
{
	SSS_SurfaceData  s = (SSS_SurfaceData)0;
	SSS_FragmentData d = (SSS_FragmentData)0;
	
	s.Albedo = 1.0;
	s.Smoothness = 0.5;
	s.Normal = float3(0.0, 0.0, 1.0);
	s.Emission = float3(0.0, 0.0, 0.0);
	s.Occlusion = 0.0;
	s.Metallic = 0.0;
	s.Alpha = 1.0;
	
	iPosition = SSS_WorldToAbsolute(iPosition);
	
	d.localSpacePosition = SSS_WorldToObject(iPosition);
	d.localSpaceNormal   = normalize(SSS_WorldToObjectDir(iNormal));
	d.localSpaceTangent  = normalize(SSS_WorldToObjectDir(iTangent));
	
	d.worldSpacePosition = iPosition;
	d.worldSpaceNormal   = iNormal;
	d.worldSpaceTangent  = iTangent;
	//d.tangentSign;
	
	d.worldSpaceViewDir  = normalize(_WorldSpaceCameraPos - d.worldSpacePosition);
	//d.tangentSpaceViewDir;
	
	d.texcoord0 = iTexcoord0;
	d.texcoord1 = iTexcoord1;
	d.texcoord2 = iTexcoord2;
	d.texcoord3 = iTexcoord3;
	
	d.screenPos = float4(SSS_WorldToScreen(iPosition), 1.0);
	d.screenUV  = d.screenPos.xy;

	d.vertexColor = iColor;
	d.isFrontFace = iIsFrontFace;
	
	d.extraV2F0 = iExtraV2F0;
	d.extraV2F1 = iExtraV2F1;
	d.extraV2F2 = iExtraV2F2;
	d.extraV2F3 = iExtraV2F3;
	d.extraV2F4 = iExtraV2F4;
	d.extraV2F5 = iExtraV2F5;
	d.extraV2F6 = iExtraV2F6;
	d.extraV2F7 = iExtraV2F7;

	d.TBNMatrix = float3x3(d.worldSpaceTangent, normalize(cross(d.worldSpaceNormal, d.worldSpaceTangent)), d.worldSpaceNormal);
	
	SSS_Frag(s, d);
	
	iPosition = SSS_AbsoluteToWorld(d.worldSpacePosition); iNormal = d.worldSpaceNormal; iTangent = d.worldSpaceTangent; // Write back
	
	oExtra      = float4x4(d.extraV2F0, d.extraV2F1, d.extraV2F2, d.extraV2F3);
	oAlbedo     = s.Albedo;
	oSmoothness = s.Smoothness;
	oNormal     = s.Normal;
	oEmission   = s.Emission;
	oOcclusion  = s.Occlusion;
	oMetallic   = s.Metallic;
	oAlpha      = s.Alpha;
}



	#pragma shader_feature_local _SGT_SHAPE_SQUARE _SGT_SHAPE_SPHERE
	#pragma shader_feature_local _SGT_CLOUDS_OFF _SGT_CLOUDS
	#pragma shader_feature_local _SGT_OCEAN_FADE_OFF _SGT_OCEAN_FADE
	#pragma shader_feature_local _SGT_SKY_OFF _SGT_SKY
	#pragma multi_compile __ CW_COMPRESS_POSITIONS



// Graph Functions
// GraphFunctions: <None>

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
float4 extraV2F0;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
float4 _UV_ccc703dc3bdd420e8a8a4aa0607d4ec4_Out_0_Vector4 = IN.uv0;
float4 _UV_46571950324b44e8bf8895f08abd191f_Out_0_Vector4 = IN.uv1;
float4 _UV_ba4ef72275334f08b9b01fedf9da0065_Out_0_Vector4 = IN.uv2;
float4 _UV_255ab3a515d14af686e70c40d04416e6_Out_0_Vector4 = IN.uv3;
float3 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oPosition_0_Vector3;
float3 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oNormal_5_Vector3;
float3 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oTangent_6_Vector3;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F0_7_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F1_8_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F2_13_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F3_14_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F4_15_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F5_16_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F6_17_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F7_18_Vector4;
Vert_float(IN.InstanceID, IN.ObjectSpacePosition, IN.ObjectSpaceNormal, IN.ObjectSpaceTangent, IN.VertexColor, _UV_ccc703dc3bdd420e8a8a4aa0607d4ec4_Out_0_Vector4, _UV_46571950324b44e8bf8895f08abd191f_Out_0_Vector4, _UV_ba4ef72275334f08b9b01fedf9da0065_Out_0_Vector4, _UV_255ab3a515d14af686e70c40d04416e6_Out_0_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oPosition_0_Vector3, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oNormal_5_Vector3, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oTangent_6_Vector3, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F0_7_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F1_8_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F2_13_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F3_14_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F4_15_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F5_16_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F6_17_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F7_18_Vector4);
description.Position = _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oPosition_0_Vector3;
description.Normal = _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oNormal_5_Vector3;
description.Tangent = _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oTangent_6_Vector3;
description.extraV2F0 = _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F0_7_Vector4;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
output.extraV2F0 = input.extraV2F0;
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
float3 BaseColor;
float3 NormalTS;
float3 Emission;
float Metallic;
float Smoothness;
float Occlusion;
float Alpha;
float AlphaClipThreshold;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
float _IsFrontFace_5b03f99e2e7841c3a7d2ae306590b576_Out_0_Boolean = max(0, IN.FaceSign.x);
float4 _UV_c1e39353e82e434da821b9bdc4338e4b_Out_0_Vector4 = IN.uv0;
float4 _UV_64d51b5974bc4ecf849e7307e6de2727_Out_0_Vector4 = IN.uv1;
float4x4 _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oExtra_23_Matrix4;
float3 _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oAlbedo_0_Vector3;
float _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oSmoothness_5_Float;
float3 _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oNormal_6_Vector3;
float3 _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oEmission_7_Vector3;
float _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oOcclusion_8_Float;
float _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oMetallic_9_Float;
float _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oAlpha_10_Float;
Frag_float(IN.WorldSpacePosition, IN.WorldSpaceNormal, IN.WorldSpaceTangent, _IsFrontFace_5b03f99e2e7841c3a7d2ae306590b576_Out_0_Boolean, float4 (0, 0, 0, 0), _UV_c1e39353e82e434da821b9bdc4338e4b_Out_0_Vector4, _UV_64d51b5974bc4ecf849e7307e6de2727_Out_0_Vector4, float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), IN.extraV2F0, float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oExtra_23_Matrix4, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oAlbedo_0_Vector3, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oSmoothness_5_Float, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oNormal_6_Vector3, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oEmission_7_Vector3, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oOcclusion_8_Float, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oMetallic_9_Float, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oAlpha_10_Float);
surface.BaseColor = _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oAlbedo_0_Vector3;
surface.NormalTS = _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oNormal_6_Vector3;
surface.Emission = _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oEmission_7_Vector3;
surface.Metallic = _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oMetallic_9_Float;
surface.Smoothness = _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oSmoothness_5_Float;
surface.Occlusion = _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oOcclusion_8_Float;
surface.Alpha = _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oAlpha_10_Float;
surface.AlphaClipThreshold = float(0.5);
return surface;
}

// --------------------------------------------------
// Build Graph Inputs

VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;
    output.uv0 =                                        input.uv0;
    output.uv1 =                                        input.uv1;
    output.uv2 =                                        input.uv2;
    output.uv3 =                                        input.uv3;
    output.VertexColor =                                input.color;
#if UNITY_ANY_INSTANCING_ENABLED
    output.InstanceID =                                 unity_InstanceID;
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
    output.InstanceID =                                 input.instanceID;
#endif

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

    output.extraV2F0 = input.extraV2F0;

    // must use interpolated tangent, bitangent and normal before they are normalized in the pixel shader.
    float3 unnormalizedNormalWS = input.normalWS;
    const float renormFactor = 1.0 / length(unnormalizedNormalWS);


    output.WorldSpaceNormal = renormFactor * input.normalWS.xyz;      // we want a unit length Normal Vector node in shader graph
    output.TangentSpaceNormal = float3(0.0f, 0.0f, 1.0f);

    // to preserve mikktspace compliance we use same scale renormFactor as was used on the normal.
    // This is explained in section 2.2 in "surface gradient based bump mapping framework"
    output.WorldSpaceTangent = renormFactor * input.tangentWS.xyz;

    output.WorldSpacePosition = input.positionWS;

    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


    output.uv0 = input.texCoord0;
    output.uv1 = input.texCoord1;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
    BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

void BuildAppDataFull(Attributes attributes, VertexDescription vertexDescription, inout appdata_full result)
{
    result.vertex     = float4(attributes.positionOS, 1);
    result.tangent    = attributes.tangentOS;
    result.normal     = attributes.normalOS;
    result.texcoord   = attributes.uv0;
    result.texcoord1  = attributes.uv1;
    result.texcoord2  = attributes.uv2;
    result.texcoord3  = attributes.uv3;
    result.color      = attributes.color;
    result.vertex     = float4(vertexDescription.Position, 1);
    result.normal     = vertexDescription.Normal;
    result.tangent    = float4(vertexDescription.Tangent, 0);
    #if UNITY_ANY_INSTANCING_ENABLED
    result.instanceID = attributes.instanceID;
    #endif
}

void VaryingsToSurfaceVertex(Varyings varyings, inout v2f_surf result)
{
    result.pos = varyings.positionCS;
    result.worldPos = varyings.positionWS;
    result.worldNormal = varyings.normalWS;
    // World Tangent isn't an available input on v2f_surf

    result._ShadowCoord = varyings.shadowCoord;

    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
    #if UNITY_SHOULD_SAMPLE_SH
    #if !defined(LIGHTMAP_ON)
    result.sh = varyings.sh;
    #endif
    #endif
    #if defined(LIGHTMAP_ON)
    result.lmap.xy = varyings.lightmapUV;
    #endif
    #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        result.fogCoord = varyings.fogFactorAndVertexLight.x;
        COPY_TO_LIGHT_COORDS(result, varyings.fogFactorAndVertexLight.yzw);
    #endif

    DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(varyings, result);
}

void SurfaceVertexToVaryings(v2f_surf surfVertex, inout Varyings result)
{
    result.positionCS = surfVertex.pos;
    result.positionWS = surfVertex.worldPos;
    result.normalWS = surfVertex.worldNormal;
    // viewDirectionWS is never filled out in the legacy pass' function. Always use the value computed by SRP
    // World Tangent isn't an available input on v2f_surf
    result.shadowCoord = surfVertex._ShadowCoord;

    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
    #if UNITY_SHOULD_SAMPLE_SH
    #if !defined(LIGHTMAP_ON)
    result.sh = surfVertex.sh;
    #endif
    #endif
    #if defined(LIGHTMAP_ON)
    result.lightmapUV = surfVertex.lmap.xy;
    #endif
    #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        result.fogFactorAndVertexLight.x = surfVertex.fogCoord;
        COPY_FROM_LIGHT_COORDS(result.fogFactorAndVertexLight.yzw, surfVertex);
    #endif

    DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(surfVertex, result);
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/PBRForwardPass.hlsl"

ENDHLSL
}
Pass
{
    Name "BuiltIn ForwardAdd"
    Tags
    {
        "LightMode" = "ForwardAdd"
    }

// Render State
Blend SrcAlpha One, One One
ZWrite Off

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM
#define _SSS_PASS_BUILTIN_FORWARDADD 1

#define _SSS_BIRP 1


// Pragmas
#pragma target 3.0
#pragma multi_compile_instancing
#pragma multi_compile_fog
#pragma multi_compile_fwdadd_fullshadows
#pragma vertex vert
#pragma fragment frag

// Keywords
#pragma multi_compile _ _SCREEN_SPACE_OCCLUSION
#pragma multi_compile _ LIGHTMAP_ON
#pragma multi_compile _ DIRLIGHTMAP_COMBINED
#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
#pragma multi_compile _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS _ADDITIONAL_OFF
#pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
#pragma multi_compile _ _SHADOWS_SOFT
#pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
#pragma multi_compile _ SHADOWS_SHADOWMASK
// GraphKeywords: <None>

// Defines
#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_TEXCOORD0
#define ATTRIBUTES_NEED_TEXCOORD1
#define ATTRIBUTES_NEED_TEXCOORD2
#define ATTRIBUTES_NEED_TEXCOORD3
#define ATTRIBUTES_NEED_COLOR
#define ATTRIBUTES_NEED_INSTANCEID
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_NORMAL_WS
#define VARYINGS_NEED_TANGENT_WS
#define VARYINGS_NEED_TEXCOORD0
#define VARYINGS_NEED_TEXCOORD1
#define VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
#define VARYINGS_NEED_CULLFACE
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_FORWARD_ADD
#define BUILTIN_TARGET_API 1
#define _BUILTIN_AlphaClip 1
#define _BUILTIN_ALPHATEST_ON 1
#ifdef _BUILTIN_SURFACE_TYPE_TRANSPARENT
#define _SURFACE_TYPE_TRANSPARENT _BUILTIN_SURFACE_TYPE_TRANSPARENT
#endif
#ifdef _BUILTIN_ALPHATEST_ON
#define _ALPHATEST_ON _BUILTIN_ALPHATEST_ON
#endif
#ifdef _BUILTIN_AlphaClip
#define _AlphaClip _BUILTIN_AlphaClip
#endif
#ifdef _BUILTIN_ALPHAPREMULTIPLY_ON
#define _ALPHAPREMULTIPLY_ON _BUILTIN_ALPHAPREMULTIPLY_ON
#endif


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Shim/Shims.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/LegacySurfaceVertex.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/ShaderGraphFunctions.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
 float4 uv0 : TEXCOORD0;
 float4 uv1 : TEXCOORD1;
 float4 uv2 : TEXCOORD2;
 float4 uv3 : TEXCOORD3;
 float4 color : COLOR;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float3 positionWS;
 float3 normalWS;
 float4 tangentWS;
 float4 texCoord0;
 float4 texCoord1;
#if defined(LIGHTMAP_ON)
 float2 lightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
 float3 sh;
#endif
 float4 fogFactorAndVertexLight;
 float4 shadowCoord;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
 float4 extraV2F0;
};
struct SurfaceDescriptionInputs
{
 float3 WorldSpaceNormal;
 float3 TangentSpaceNormal;
 float3 WorldSpaceTangent;
 float3 WorldSpacePosition;
 float4 uv0;
 float4 uv1;
 float FaceSign;
 float4 extraV2F0;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
 float4 uv0;
 float4 uv1;
 float4 uv2;
 float4 uv3;
 float4 VertexColor;
 uint InstanceID;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
#if defined(LIGHTMAP_ON)
 float2 lightmapUV : INTERP0;
#endif
#if !defined(LIGHTMAP_ON)
 float3 sh : INTERP1;
#endif
 float4 tangentWS : INTERP2;
 float4 texCoord0 : INTERP3;
 float4 texCoord1 : INTERP4;
 float4 fogFactorAndVertexLight : INTERP5;
 float4 shadowCoord : INTERP6;
 float4 extraV2F0 : INTERP7;
 float3 positionWS : INTERP8;
 float3 normalWS : INTERP9;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
#if defined(LIGHTMAP_ON)
output.lightmapUV = input.lightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
output.sh = input.sh;
#endif
output.tangentWS.xyzw = input.tangentWS;
output.texCoord0.xyzw = input.texCoord0;
output.texCoord1.xyzw = input.texCoord1;
output.fogFactorAndVertexLight.xyzw = input.fogFactorAndVertexLight;
output.shadowCoord.xyzw = input.shadowCoord;
output.extraV2F0.xyzw = input.extraV2F0;
output.positionWS.xyz = input.positionWS;
output.normalWS.xyz = input.normalWS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
#if defined(LIGHTMAP_ON)
output.lightmapUV = input.lightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
output.sh = input.sh;
#endif
output.tangentWS = input.tangentWS.xyzw;
output.texCoord0 = input.texCoord0.xyzw;
output.texCoord1 = input.texCoord1.xyzw;
output.fogFactorAndVertexLight = input.fogFactorAndVertexLight.xyzw;
output.shadowCoord = input.shadowCoord.xyzw;
output.extraV2F0 = input.extraV2F0.xyzw;
output.positionWS = input.positionWS.xyz;
output.normalWS = input.normalWS.xyz;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties
CBUFFER_START(UnityPerMaterial)





CBUFFER_END


// Object and Global properties

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Includes
// UNITY_SHADER_NO_UPGRADE
float3 SSS_HClipToScreen(float4 v)
{
	float3 uv = v.xyz / v.w;
	#if UNITY_UV_STARTS_AT_TOP
		uv.y = -uv.y;
	#endif
	uv.xy = uv.xy * 0.5 + 0.5;
	return uv;
}

#if _SSS_HDRP
	float3 SSS_WorldToAbsolute(float3 v) { return GetAbsolutePositionWS(v); }
	float3 SSS_AbsoluteToWorld(float3 v) { return GetCameraRelativePositionWS(v); }
#else
	float3 SSS_WorldToAbsolute(float3 v) { return v; }
	float3 SSS_AbsoluteToWorld(float3 v) { return v; }
#endif

float3 SSS_WorldToView(float3 v) { return TransformWorldToView(SSS_AbsoluteToWorld(v)); }
float3 SSS_WorldToObject(float3 v) { return TransformWorldToObject(SSS_AbsoluteToWorld(v)); }
float3 SSS_WorldToScreen(float3 v) { return SSS_HClipToScreen(TransformWorldToHClip(SSS_AbsoluteToWorld(v))); }
float3 SSS_ObjectToScreen(float3 v) { return SSS_HClipToScreen(TransformObjectToHClip(v)); }
float3 SSS_ObjectToWorld(float3 v) { return SSS_WorldToAbsolute(TransformObjectToWorld(v)); }
float3 SSS_ObjectToView(float3 v) { return TransformWorldToView(TransformObjectToWorld(v)); }
float3 SSS_ScreenToWorld(float3 v) { return SSS_WorldToAbsolute(ComputeWorldSpacePosition(v.xy, v.z, UNITY_MATRIX_I_VP)); }
float3 SSS_ScreenToObject(float3 v) { return SSS_WorldToObject(SSS_ScreenToWorld(v)); }
float3 SSS_ScreenToView(float3 v) { return SSS_WorldToView(SSS_ScreenToWorld(v)); }
float3 SSS_ViewToWorld(float3 v) { return mul(UNITY_MATRIX_I_V, float4(v, 1.0)).xyz; }
float3 SSS_ViewToObject(float3 v) { return TransformWorldToObject(SSS_ViewToWorld(v)); }
float3 SSS_ViewToScreen(float3 v) { return SSS_HClipToScreen(TransformWViewToHClip(v)); }
float3 SSS_ObjectToWorldDir(float3 v)
{
	#if _SSS_BIRP
		return TransformObjectToWorldDir(v);
	#else
		return TransformObjectToWorldDir(v, true);
	#endif
}
float3 SSS_ObjectToViewDir(float3 v)
{
	#if _SSS_BIRP
		return TransformWorldToViewDir(TransformObjectToWorldDir(v));
	#else
		return TransformWorldToViewDir(TransformObjectToWorldDir(v, false), true);
	#endif
}
float3 SSS_WorldToObjectDir(float3 v)
{
	#if _SSS_BIRP
		return TransformWorldToObjectDir(v);
	#else
		return TransformWorldToObjectDir(v, true);
	#endif
}
float3 SSS_WorldToViewDir(float3 v)
{
	#if _SSS_BIRP
		return TransformWorldToViewDir(v);
	#else
		return TransformWorldToViewDir(v, true);
	#endif
}
float3 SSS_ViewToObjectDir(float3 v)
{
	#if _SSS_URP || _SSS_HDRP
		return SSS_WorldToObjectDir(mul(UNITY_MATRIX_I_V, float4(v, 0.0)).xyz);
	#else
		return SSS_WorldToObjectDir(mul((float3x3)UNITY_MATRIX_I_V, v));
	#endif
}
float3 SSS_ViewToWorldDir(float3 v)
{
	return mul(UNITY_MATRIX_I_V, float4(v, 0.0)).xyz;
}

#if _SSS_NO_DERIVATIVES
	float3 SSS_GetSceneColor(float2 uv) { return float3(0.0, 0.0, 0.0); }
	float3 SSS_GetSceneColorHD(float2 uv) { return SSS_GetSceneColor(uv); }
	float  SSS_GetSceneDepth(float2 uv) { return 0.0; }
#else
	#if _SSS_URP
		float3 SSS_GetSceneColor(float2 uv) { return SHADERGRAPH_SAMPLE_SCENE_COLOR(uv).xyz; }
		float3 SSS_GetSceneColorHD(float2 uv) { return SSS_GetSceneColor(uv); }
	#elif _SSS_HDRP
		float3 SSS_GetSceneColor(float2 uv) { return SHADERGRAPH_SAMPLE_SCENE_COLOR(uv).xyz; }
		float3 SSS_GetSceneColorHD(float2 uv)
		{
			#if defined(REQUIRE_OPAQUE_TEXTURE) && defined(_SURFACE_TYPE_TRANSPARENT) && defined(SHADERPASS) && (SHADERPASS != SHADERPASS_LIGHT_TRANSPORT) && (SHADERPASS != SHADERPASS_PATH_TRACING) && (SHADERPASS != SHADERPASS_RAYTRACING_VISIBILITY) && (SHADERPASS != SHADERPASS_RAYTRACING_FORWARD)
			return SampleCameraColor(uv, 0);
			#endif
			#if defined(REQUIRE_OPAQUE_TEXTURE) && defined(CUSTOM_PASS_SAMPLING_HLSL) && defined(SHADERPASS) && (SHADERPASS == SHADERPASS_DRAWPROCEDURAL || SHADERPASS == SHADERPASS_BLIT)
			return CustomPassSampleCameraColor(uv, 0);
			#endif
			return float3(0.0, 0.0, 0.0);
		}
	#else
		#if defined(UNITY_DECLARE_OPAQUE_TEXTURE_INCLUDED)
			float3 SSS_GetSceneColor(float2 uv) { return SampleSceneColor(uv); }
		#else
			sampler2D _CameraOpaqueTexture; float3 SSS_GetSceneColor(float2 uv) { return tex2D(_CameraOpaqueTexture, uv).xyz; }
		#endif
		float3 SSS_GetSceneColorHD(float2 uv) { return SSS_GetSceneColor(uv); }
	#endif

	float SSS_GetSceneDepth(float2 uv) { return SHADERGRAPH_SAMPLE_SCENE_DEPTH(uv); }
#endif

float3 SSS_GetSceneWorldPosition(float2 screenUV, float sceneDepth)
{
	#if _SSS_BIRP
		float4 clipPos  = float4(screenUV * 2.0f - 1.0f, 0.0f, 1.0f);
		float4 viewPos  = mul(unity_CameraInvProjection, clipPos);
		float3 worldDir = mul((float3x3)UNITY_MATRIX_I_V, viewPos.xyz);
					
		return _WorldSpaceCameraPos + worldDir * LinearEyeDepth(sceneDepth);
	#else
		float4 clipPos = float4(screenUV * 2.0 - 1.0, sceneDepth, 1.0);
					
		#if UNITY_UV_STARTS_AT_TOP
			clipPos.y = -clipPos.y;
		#endif
					
		float4 worldPos = mul(UNITY_MATRIX_I_VP, clipPos);
					
		worldPos.xyz /= worldPos.w;
					
		#if _SSS_HDRP
			worldPos.xyz = GetAbsolutePositionWS(worldPos.xyz);
		#endif
					
		return worldPos.xyz;
	#endif
}

float SSS_GetSceneWorldDistance(float2 screenUV, float sceneDepth)
{
	return distance(_WorldSpaceCameraPos, SSS_GetSceneWorldPosition(screenUV, sceneDepth));
}

float3 SSS_UnpackNormalScale(float4 c, float s)
{
	#if _SSS_BIRP
		return UnpackScaleNormal(c, s);
	#else
		return UnpackNormalScale(c, s);
	#endif
}

struct SSS_VertexData
{
	float  instanceID;
	float3 position;
	float3 normal;
	float3 tangent;
	float4 color;
	float4 texcoord0;
	float4 texcoord1;
	float4 texcoord2;
	float4 texcoord3;
	float4 extraV2F0;
	float4 extraV2F1;
	float4 extraV2F2;
	float4 extraV2F3;
	float4 extraV2F4;
	float4 extraV2F5;
	float4 extraV2F6;
	float4 extraV2F7;
	



};

struct SSS_FragmentData
{
	float3 localSpacePosition;
	float3 localSpaceNormal;
	float3 localSpaceTangent;
	
	float3 worldSpacePosition;
	float3 worldSpaceNormal;
	float3 worldSpaceTangent;
	//float tangentSign;

	float3 worldSpaceViewDir;
	//float3 tangentSpaceViewDir;
	
	float4 texcoord0;
	float4 texcoord1;
	float4 texcoord2;
	float4 texcoord3;
	
	float2 screenUV;
	float4 screenPos;

	float4 vertexColor;
	bool isFrontFace;
	
	float4 extraV2F0;
	float4 extraV2F1;
	float4 extraV2F2;
	float4 extraV2F3;
	float4 extraV2F4;
	float4 extraV2F5;
	float4 extraV2F6;
	float4 extraV2F7;

	float3x3 TBNMatrix;
	



};

struct SSS_SurfaceData
{
	float3 Albedo;
	float  Smoothness;
	float3 Normal;
	float3 Emission;
	float  Occlusion;
	float  Metallic;
	float  Alpha;
};

float2 _CW_PositionCompressionData; // x = start distance, y = max distance limit

#define DEBUG_SCALE 0.001
#define SSS_LOG_K          0.0001
#define SSS_MAX_WORLD_DIST 1e27

float SSS_GetCompressionRatio(float worldDist)
{
    float startDist = _CW_PositionCompressionData.x;
    float maxDist   = _CW_PositionCompressionData.y;

    // no compression nearby
    if (worldDist <= startDist)
        return 1.0;

    float excess = worldDist - startDist;

    float compressedDist =
        startDist +
        (maxDist - startDist) *
        log(1.0 + excess * SSS_LOG_K) /
        log(1.0 + SSS_MAX_WORLD_DIST * SSS_LOG_K);

    return compressedDist / worldDist;
}

float SSS_DecompressWorldDist(float compressedDist)
{
    float startDist = _CW_PositionCompressionData.x;
    float maxDist   = _CW_PositionCompressionData.y;

    if (compressedDist <= startDist)
        return compressedDist;

    float t =
        saturate(
            (compressedDist - startDist) /
            (maxDist - startDist)
        );

    return startDist +
        (
            exp(
                t *
                log(1.0 + SSS_MAX_WORLD_DIST * SSS_LOG_K)
            ) - 1.0
        ) / SSS_LOG_K;
}

float3 SSS_CompressWorld(float3 worldPos, float3 worldCenter, float worldRadius, out float ratio) 
{
    float3 worldDelta = worldPos - _WorldSpaceCameraPos;
    float  farDist    = distance(_WorldSpaceCameraPos, worldCenter) + worldRadius;
    
    ratio = SSS_GetCompressionRatio(farDist);
    
    return _WorldSpaceCameraPos + worldDelta * ratio;
}

float3 SSS_CompressWorld(float3 worldPos) 
{
    float3 worldDelta = worldPos - _WorldSpaceCameraPos;
    float  worldDist  = length(worldDelta);
    
    return _WorldSpaceCameraPos + worldDelta * SSS_GetCompressionRatio(worldDist);
}

float3 SSS_DecompressWorld(float3 worldPos)
{
    float3 worldDelta = worldPos - _WorldSpaceCameraPos;
    float  worldDist  = length(worldDelta);
    
    return worldDist > 0.001 ? _WorldSpaceCameraPos + (worldDelta / worldDist) * SSS_DecompressWorldDist(worldDist) : _WorldSpaceCameraPos;
}

float3 SSS_DecompressWorld(float3 worldPos, float ratio) 
{
    float3 worldDelta = worldPos - _WorldSpaceCameraPos;
    return _WorldSpaceCameraPos + worldDelta / ratio;
}

float SSS_ElongateDistance(float worldDist)
{
	float minDist = 10000.0;
	float maxDist = 100000.0;
	float scale   = saturate((worldDist - minDist) / (maxDist - minDist));

	return worldDist * lerp(1.0, 1.2, scale);
}
#define SGT_OPACITY_STEPS  5

float4x4        _SGT_SkyToWorld;
float4x4        _SGT_WorldToSky;
float4          _SGT_SkyColor;
float           _SGT_SkyBrightness;
float4          _SGT_SkyAmbientTint;
float           _SGT_SkyExposure;
float4          _SGT_SkyRadius;
float4          _SGT_SkyAltitudeData;
float           _SGT_SkyDensity;
float4          _SGT_SkyMieWeight;
float4          _SGT_SkyMieData;
sampler3D       _SGT_SkyRadianceTex;
float           _SGT_SkyRadianceLod;
sampler2D       _SGT_SkyLightingTex;
sampler2D       _SGT_SkyAlbedoTex;
float           _SGT_SkyRevealStars;
float           _SGT_SkyDepthOpaque;

float     _SGT_Volumetrics_Downscale; // Global
float     _SGT_Volumetrics_NearClip; // Global
Texture2D _SGT_Volumetrics_ColorTex; // Global
float4    _SGT_Volumetrics_ColorSize; // Global
Texture2D _SGT_Volumetrics_DepthTex; // Global
float2    _SGT_Volumetrics_DepthSize; // Global

SamplerState my_linear_clamp_sampler;
SamplerState my_point_clamp_sampler;

sampler2D _SGT_BlueNoiseTex; // Global
float     _SGT_Frame; // Global
float3    _SGT_AmbientColor; // Global
	
float SGT_DitherBlue(float2 screenUV)
{
	float2 pixel = floor(screenUV * _ScreenParams.xy);
	float  noise = tex2D(_SGT_BlueNoiseTex, pixel / 64.0f).r;
	return frac(noise + _SGT_Frame / sqrt(0.5f));
}

float SGT_DitherFast(float2 screenUV)
{
    float2 R2 = float2(0.7548776662, 0.5698402909);
    float2 p  = screenUV * _ScreenParams.xy + frac(_Time.y * R2) * 256.0;
    return frac(52.9829189 * frac(dot(p, float2(0.06711056, 0.00583715))));
}
	
float2 SGT_DirectionToEquirectangular(float3 dir)
{
	dir = normalize(dir);
	float u = atan2(dir.z, dir.x) / (2.0 * 3.141592653) + 0.5;
	float v = asin(clamp(dir.y, -1.0, 1.0)) / 3.141592653 + 0.5;
	return float2(u, v);
}

float3 SGT_SphereTest(float3 ray, float3 rayD, float radius)
{
	float B = -dot(ray, rayD);
	float C = dot(ray, ray) - radius * radius;
	float D = B * B - C;
	float E = sqrt(max(D, 0.0f));
	return float3(B - E, B + E, D);
}

float SGT_InverseLerp(float a, float b, float v)
{
	return (v - a) / (b - a);
}

float3 SGT_CalculateMie(float3 ray, float mie)
{
	return normalize(ray * _SGT_SkyMieWeight.xyz + 1e-6) * mie;
}

float SGT_GetPitch01(float3 rayDir, float3 up, float altitude01)
{
	float power = 2.0f;
	float eyeHeight = _SGT_SkyRadius.x + altitude01 * (_SGT_SkyRadius.y - _SGT_SkyRadius.x);
	float horizonAngleFromDown = -acos(clamp(_SGT_SkyRadius.x / max(eyeHeight, 0.00001f), -1.0f, 1.0f));
	float rayAng = asin(clamp(dot(up, rayDir), -1.0f, 1.0f));
	float sun01;

	if (rayAng < horizonAngleFromDown)
	{
		sun01 = SGT_InverseLerp(-3.141592653 * 0.5f, horizonAngleFromDown, rayAng);
		sun01 = 1.0f - pow(1.0f - sun01, 1.0f / power);
		sun01 = sun01 * 0.5f;
	}
	else
	{
		sun01 = SGT_InverseLerp(horizonAngleFromDown, 3.141592653 * 0.5f, rayAng);
		sun01 = pow(sun01, 1.0f / power);
		sun01 = sun01 * 0.5f + 0.5f;
	}

	return sun01;
}

float3 GetLutCoord(float3 rayPos, float3 rayDir, float3 sunDir, float dither)
{
	float3 up         = normalize(rayPos);
	float  eyeHeight  = length(rayPos);
	float  altitude01 = saturate(SGT_InverseLerp(_SGT_SkyRadius.z, 1.0f, eyeHeight));
	float  pitch01    = SGT_GetPitch01(rayDir, up, altitude01);
	float  sun01      = SGT_GetPitch01(sunDir, up, altitude01);
	
	pitch01 += (dither - 0.5f) / 128.0f;
	sun01   += (dither - 0.5f) /  64.0f;
	
	return float3(pitch01, altitude01, sun01);
}

float2 SGT_GetDensity2(float3 pos, float weightMul)
{
	float alt01 = saturate((1.0f - length(pos)) * _SGT_SkyAltitudeData.z);

	return pow(alt01, _SGT_SkyAltitudeData.xy * weightMul) * _SGT_SkyDensity;
}

float SGT_GetOpacity(float3 rayPos, float3 rayDir, float rayMax, float weightMul)
{
	float2 opticalDepth = 0.0f;
	float  rayStep      = rayMax / SGT_OPACITY_STEPS;
	float  t            = rayStep * 0.5f;
	
	for (int i = 0; i < SGT_OPACITY_STEPS; i++)
	{
		float3 pos = rayPos + rayDir * t;

		opticalDepth += SGT_GetDensity2(pos, weightMul) * rayStep;
		
		t += rayStep;
	}
	
	return 1.0f - exp((opticalDepth.x + opticalDepth.y) * -20.0f);
}

float4 SGT_SampleLUT(float3 coord)
{
	return tex3Dlod(_SGT_SkyRadianceTex, float4(coord, _SGT_SkyRadianceLod));
}

float3 SGT_AtmosphereColor(float3 rayPos, float3 sunDir, float dither)
{
	float3 up         = normalize(rayPos);
	float  eyeHeight  = length(rayPos);
	float  altitude01 = saturate(SGT_InverseLerp(_SGT_SkyRadius.z, 1.0f, eyeHeight));
	float  sun01      = 0.5f + 0.5f * dot(sunDir, up);
		
	return tex2Dlod(_SGT_SkyLightingTex, float4(sun01, altitude01, 0.0f, 0.0f)).xyz;
}

float3 SGT_UnderwaterAtmosphere(float3 rayPos, float3 rayDir, float3 sunDir, float exposure, float dither)
{
	float3 coord = GetLutCoord(rayPos, rayDir, sunDir, dither); coord.x = 1.0f;
	float4 ray   = SGT_SampleLUT(coord);
	float3 mie   = SGT_CalculateMie(ray.xyz, ray.w);
	float3 color = ray.xyz + mie;
	
	#if _SSS_HDRP
		color *= exposure * 0.25f;
	#else
		color = 1.0 - exp(-color * exposure);
	#endif
	
	return saturate(_SGT_SkyColor.xyz * color * _SGT_SkyBrightness);
}

float4 SGT_Atmosphere(float3 rayPos, float3 rayDir, float rayFar, float rayMax, float3 sunDir, float exposure, float dither, float mieMul, float weightMul)
{
	// 1. Calculate opacity FIRST so we can use it for blending
	float opacity = SGT_GetOpacity(rayPos, rayDir, rayMax, weightMul);

	float3 coord  = GetLutCoord(rayPos, rayDir, sunDir, dither);
	float2 mieOff = 0.0f;
	
	if (rayMax * 1.02f <= rayFar) // If ground
	{
		// 2. Store the true, un-clamped sky coordinate
		float trueCoordX = coord.x;
		
		// 3. Calculate the clamped "below horizon" coordinate (original behavior)
		float clampedCoordX = min(coord.x, 0.5f - (1.0f / 128.0f));
		clampedCoordX = lerp(0.5f - (1.0f / 128.0f), clampedCoordX, coord.y * coord.y);
		
		// 4. Smoothly blend towards the true sky color as fog density/opacity increases
		coord.x = lerp(clampedCoordX, trueCoordX, opacity);
		mieOff = lerp(0.2f, 0.0f, opacity);
	}
	else
	{
		//coord.x = max(coord.x, 0.5f + (1.0f / 128.0f));
	}
	
	float4 ray    = SGT_SampleLUT(coord);
	float3 mie    = SGT_CalculateMie(ray.xyz, ray.w);
	float2 g      = lerp(_SGT_SkyMieData.xy, 0.0f, mieOff);
	float  c      = dot(rayDir, sunDir);
	float  phaseR = 3.0f / (16.0f * 3.141592653f) * (1.0f + c * c);
	float2 phaseM = 3.0f / (8.0f * 3.141592653f) * ((1.0f - g * g) * (c * c + 1.0f)) / (pow(abs(1.0f + g * g - 2.0f * c * g), 1.5f) * (2.0f + g * g));
	float3 color  = ray.xyz * phaseR + mie * dot(phaseM, 1) * mieMul;
	
	#if _SSS_HDRP
		color *= exposure * 0.25f;
	#else
		color = 1.0 - exp(-color * exposure);
	#endif
	
	#if _SSS_HDRP
		color += _SGT_AmbientColor.xyz * _SGT_SkyAmbientTint.xyz * 0.25;
	#else
		color += _SGT_AmbientColor.xyz * _SGT_SkyAmbientTint.xyz;
	#endif

	#if _SGT_SOLID_CORE_OFF // If the atmosphere below InnerMeshRadius isn't solid, then continue to darken because the LUT doesn't extend any farther
		float eyeHeight     = length(rayPos);
		float inner         = _SGT_SkyRadius.z;
		float interiorDepth = saturate((_SGT_SkyRadius.z - eyeHeight) / _SGT_SkyRadius.z);

		if (interiorDepth > 0.0)
		{
			float extinction = exp(-interiorDepth * 20.0 * _SGT_SkyDensity);

			color *= extinction;
			opacity = lerp(opacity, 1.0f, saturate(interiorDepth * 100.0f));
		}
	#endif
	
	return float4(saturate(_SGT_SkyColor.xyz * color * _SGT_SkyBrightness), opacity);
}

// Global cached light properties
float3 _SGT_CachedLightDirection;
float3 _SGT_CachedLightColor;

float3 SGT_SampleSkyReflection(float3 ocam, float3 odir)
{
    // 2. Intersect with the Atmosphere Shell (Outer radius is 1.0 in sky space)
    float3 odst = SGT_SphereTest(ocam, odir, 1.0f);
    odst.xy = max(odst.xy, 0.0f); // Ensure distances are ahead of ray
    
    float3 rayPos = ocam + odir * odst.x;
    float3 rayDir = odir;
    
    // 3. Determine Atmosphere Limits
    float rayFar = SGT_SphereTest(rayPos, rayDir, 1.0f).y; // Distance to sky exit
    float rayMax = odst.y - odst.x;                       // Max scattering distance
    
    // Optional: Intersect with the planet core so reflections don't "see through" the world
    #if _SGT_SOLID_CORE
        float3 ihit = SGT_SphereTest(ocam, odir, _SGT_SkyRadius.w);
        if (ihit.z > 0.0f && ihit.x > 0.0f) {
            rayMax = min(ihit.x - odst.x, rayMax);
        }
    #endif

    // 4. Calculate Sun Direction in Sky Space
    float3 sunDir = normalize(mul(_SGT_WorldToSky, float4(_SGT_CachedLightDirection, 0.0f)).xyz);
    
    // 5. Sample the Atmosphere
    // dither is set to 0.0 to keep reflections smooth; mieMul is 1.0
    float4 sky = SGT_Atmosphere(rayPos, rayDir, rayFar, rayMax, sunDir, _SGT_SkyExposure, 0.0, 1.0, 1.0);
    
    // 6. Apply cached light intensity
    return sky.rgb * _SGT_CachedLightColor;
}

float4 SGT_SimpleAtmosphere(float3 worldPos, float3 worldHit, float mieMul, float weightMul)
{
	float dither   = 0.5f;
	
	float3 ocam = mul(_SGT_WorldToSky, float4(worldPos, 1.0f)).xyz;
	float3 ofar = mul(_SGT_WorldToSky, float4(worldHit, 1.0f)).xyz;
	float  omax = distance(ocam, ofar);
	float3 odir = normalize(ofar - ocam);
	float3 odst = SGT_SphereTest(ocam, odir, 1.0f);
	
	odst.xy = max(odst.xy, 0.0f);
	
	float3 rayPos = ocam + odir * odst.x;
	float3 rayDir = odir;
	float  rayFar = SGT_SphereTest(rayPos, rayDir, 1.0f).y;
	float  rayMax = min(odst.y, omax) - odst.x;
	
	#if _SGT_LIGHTING
		float3 sunDir = normalize(mul(_SGT_WorldToSky, float4(_SGT_LightDirection[0].xyz, 0.0f)).xyz);
	#else
		float3 sunDir = normalize(rayPos);
	#endif
	
	return SGT_Atmosphere(rayPos, rayDir, rayFar, rayMax, sunDir, _SGT_SkyExposure, dither, mieMul, weightMul);
}

float3 CW_Depth2World(float viewDepth, float3 viewDir)
{
	return _WorldSpaceCameraPos + viewDir * viewDepth;
}
	
bool CW_DepthInBounds(float viewDepth, float3 viewDir)
{
	float3 wPos = CW_Depth2World(viewDepth, viewDir);
	float3 oPos = mul(_SGT_WorldToSky, float4(wPos, 1.0f)).xyz;
		
	return length(oPos) < 1.2f;
}
	
void CW_SampleVolumetricsDefault(float2 screenUV, float3 viewDir, out float4 outColor, out float outDepth)
{
	float4 centerColor = _SGT_Volumetrics_ColorTex.Sample(my_linear_clamp_sampler, screenUV);
	float  centerDepth = _SGT_Volumetrics_DepthTex.Sample(my_linear_clamp_sampler, screenUV).x;
		
	if (CW_DepthInBounds(centerDepth, viewDir) == true)
	{
		outColor = centerColor;
		outDepth = centerDepth;
	}
	else
	{
		outColor = 0;
		outDepth = 0;
	}
}

void CW_SampleVolumetricsDefault2(float2 screenUV, float3 viewDir, out float4 outColor, out float outDepth)
{
	outColor = _SGT_Volumetrics_ColorTex.Sample(my_linear_clamp_sampler, screenUV);
	outDepth = _SGT_Volumetrics_DepthTex.Sample(my_linear_clamp_sampler, screenUV).x;
}
	
void CW_SampleVolumetrics(float2 screenUV, float3 viewDir, float sceneDepth, out float4 outColor, out float outDepth)
{
	outColor = 0;
	outDepth = 0;
	//CW_SampleVolumetricsDefault(screenUV, viewDir, outColor, outDepth);return;
		
	if (_SGT_Volumetrics_Downscale > 1)
	{
		float2 pixelF   = screenUV * _SGT_Volumetrics_ColorSize.xy - 0.5f;
		float2 pixelI   = floor(pixelF);
		float2 snappedF = pixelF - pixelI;
			
		float2 coord0 = (pixelI + float2(0.5f, 0.5f)) * _SGT_Volumetrics_ColorSize.zw;
		float2 coord1 = (pixelI + float2(1.5f, 0.5f)) * _SGT_Volumetrics_ColorSize.zw;
		float2 coord2 = (pixelI + float2(0.5f, 1.5f)) * _SGT_Volumetrics_ColorSize.zw;
		float2 coord3 = (pixelI + float2(1.5f, 1.5f)) * _SGT_Volumetrics_ColorSize.zw;
			
		float depth0 = _SGT_Volumetrics_DepthTex.Sample(my_point_clamp_sampler, coord0).x;
		float depth1 = _SGT_Volumetrics_DepthTex.Sample(my_point_clamp_sampler, coord1).x;
		float depth2 = _SGT_Volumetrics_DepthTex.Sample(my_point_clamp_sampler, coord2).x;
		float depth3 = _SGT_Volumetrics_DepthTex.Sample(my_point_clamp_sampler, coord3).x;
			
		float4 color0 = _SGT_Volumetrics_ColorTex.Sample(my_point_clamp_sampler, coord0);
		float4 color1 = _SGT_Volumetrics_ColorTex.Sample(my_point_clamp_sampler, coord1);
		float4 color2 = _SGT_Volumetrics_ColorTex.Sample(my_point_clamp_sampler, coord2);
		float4 color3 = _SGT_Volumetrics_ColorTex.Sample(my_point_clamp_sampler, coord3);
			
		float w0 = CW_DepthInBounds(depth0, viewDir) * (depth0 < sceneDepth) * color0.w * (1.0 - snappedF.x) * (1.0 - snappedF.y);
		float w1 = CW_DepthInBounds(depth1, viewDir) * (depth1 < sceneDepth) * color1.w * snappedF.x * (1.0 - snappedF.y);
		float w2 = CW_DepthInBounds(depth2, viewDir) * (depth2 < sceneDepth) * color2.w * (1.0 - snappedF.x) * snappedF.y;
		float w3 = CW_DepthInBounds(depth3, viewDir) * (depth3 < sceneDepth) * color3.w * snappedF.x * snappedF.y;
			
		float wt = w0 + w1 + w2 + w3;
			
		if (wt > 0.0f)
		{
			outColor = (color0 * w0 + color1 * w1 + color2 * w2 + color3 * w3) / wt;
			outDepth = (depth0 * w0 + depth1 * w1 + depth2 * w2 + depth3 * w3) / wt;
		}
		else
		{
			return;
		}
	}
	else
	{
		float2 coord0 = screenUV;
		float  depth0 = _SGT_Volumetrics_DepthTex.Sample(my_point_clamp_sampler, coord0).x;
		float4 color0 = _SGT_Volumetrics_ColorTex.Sample(my_point_clamp_sampler, coord0);
		float  w0     = CW_DepthInBounds(depth0, viewDir) * (depth0 < sceneDepth);
		
		if (w0 > 0.0f)
		{
			outColor = color0;
			outDepth = depth0;
		}
		else
		{
			return;
		}
	}
}






#pragma instancing_options procedural:SetupInstancing
#if _SSS_PASS_SHADOWCASTER || _SSS_PASS_META
	#pragma multi_compile_instancing // For some reason the ShadowCaster and Meta pass in BIRP doesn't have this line
#endif


#define BATCH_CAPACITY 35
#define VERTEX_COUNT 243

float4 _CwSize;
float4 _CwAtlas;
float4 _CwWeights[VERTEX_COUNT];
float4 _CwCoords[VERTEX_COUNT];

float4 _CwOrigins[BATCH_CAPACITY];
float4 _CwPositionsA[BATCH_CAPACITY];
float4 _CwPositionsB[BATCH_CAPACITY];
float4 _CwPositionsC[BATCH_CAPACITY];

sampler2D DataP;
sampler2D DataA;
sampler2D DataN;

float3 _CwOffset;

float4x4 _CwObjectToWorld;
float4x4 _CwWorldToObject;

float _SGT_Occlusion;

// CLOUDS
sampler2D _SGT_CloudShadowTex;
float3    _SGT_CloudShadowDirection;
float4    _SGT_CloudShadowOpacity;
float4x4  _SGT_CloudShadowMatrix;

// OCEAN FADE
float  _SGT_OceanFade;
float2 _SGT_OceanDensity;
float3 _SGT_OceanColor;
float  _SGT_OceanSmoothness;
float4 _SGT_OceanLightDirection;

float _SGT_OceanRadius;

void SetupInstancing()
{
	#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
		#ifdef unity_ObjectToWorld
			#undef unity_ObjectToWorld
		#endif

		#ifdef unity_WorldToObject
			#undef unity_WorldToObject
		#endif
		
		unity_ObjectToWorld = _CwObjectToWorld;
		unity_WorldToObject = _CwWorldToObject;
	#endif
}

float SGT_SampleCloudDensity(float3 wpos)
{
	float3 cpos  = mul(_SGT_CloudShadowMatrix, float4(wpos, 1.0)).xyz;
	float3 chit  = SGT_SphereTest(cpos, _SGT_CloudShadowDirection, 1.0);
	float2 uv    = SGT_DirectionToEquirectangular(normalize(cpos + chit.y * _SGT_CloudShadowDirection));
	
	return saturate(dot(tex2D(_SGT_CloudShadowTex, uv), _SGT_CloudShadowOpacity));
}

void SSS_Vert(inout SSS_VertexData v)
{
	float vertexIndex = v.position.x;
	float squareIndex = v.position.y;
	float batchIndex  = v.instanceID;
	
	float  batchCol = batchIndex % _CwAtlas.z;
	float  batchRow = floor(batchIndex / _CwAtlas.z);
	float3 origin   = _CwOrigins[batchIndex].xyz;
	float3 weights  = _CwWeights[vertexIndex].xyz;
	float2 coord    = _CwCoords[vertexIndex].xy; coord.x /= 3.0;
	float3 position = _CwPositionsA[batchIndex].xyz * weights.x + _CwPositionsB[batchIndex].xyz * weights.y + _CwPositionsC[batchIndex].xyz * weights.z;
	
	v.position.xyz = _CwOffset + origin + tex2Dlod(DataP, float4(vertexIndex * _CwSize.x, batchIndex * _CwSize.y, 0.0, 0.0)).xyz;
	
	float3 ocam = mul(_CwWorldToObject, float4(_WorldSpaceCameraPos - _CwOffset, 1.0)).xyz;
	
	#if _SGT_SHAPE_SQUARE
		v.normal  = float3(0,1,0);
		v.tangent = float4(1,0,0,-1).xyz;
		
		v.extraV2F0.w = ocam.y;
	#elif _SGT_SHAPE_SPHERE
		v.normal = normalize(position);
		
		if (position.x == 0.0 && position.z == 0.0)
		{
			position.xz += 0.00000001;
		}
		
		v.tangent = float4(normalize(cross(float3(0,1,0), normalize(position))), -1.0).xyz;
		
		v.extraV2F0.w = length(ocam) - _SGT_OceanRadius;
	#endif
	
	#if _SGT_OCEAN_FADE
		if (_SGT_OceanFade > 0.0 && length(v.position.xyz - _CwOffset) < _SGT_OceanRadius)
		{
			float3 N = normalize(position); // sphere normal
			float3 V = normalize(_WorldSpaceCameraPos - v.position.xyz);
			float  F = pow(abs(1.0 - saturate(dot(N, V))), 2.0);

			float3 oceanPos = _CwOffset + N * _SGT_OceanRadius;
			v.position.xyz = lerp(v.position.xyz, oceanPos, F);
		}

	#endif
	
	// Calc UV
	float2 pixelS = _CwAtlas.xy * _CwAtlas.zw;
	float  pixelX = batchCol * _CwAtlas.x + coord.x * (_CwAtlas.x - 3.0) + 0.5 + squareIndex * (_CwAtlas.x / 3.0);
	float  pixelY = batchRow * _CwAtlas.y + coord.y * (_CwAtlas.y - 1.0) + 0.5;
	
	v.extraV2F0.x = pixelX / pixelS.x;
	v.extraV2F0.y = pixelY / pixelS.y;
	
	#if CW_COMPRESS_POSITIONS
		float3 wpos = mul(_CwObjectToWorld, float4(v.position, 1.0)).xyz;
		v.extraV2F0.z = distance(_WorldSpaceCameraPos, wpos);
		wpos = SSS_CompressWorld(wpos);
		v.position = mul(_CwWorldToObject, float4(wpos, 1.0)).xyz;
	#endif
}

float3 SGT_GetColor(float3 wnormal, float3 wlight)
{
	return saturate(dot(wnormal, wlight));
}

static const float WATER_DEPTH_SHALLOW = 30.0;
static const float WATER_DEPTH_MAX = 3000.0;

float SGT_DecodeWaterDepth(float encoded)
{
	float shallow = encoded * (WATER_DEPTH_SHALLOW * 2.0);
    float deep = WATER_DEPTH_SHALLOW + (encoded - 0.5) * ((WATER_DEPTH_MAX - WATER_DEPTH_SHALLOW) * 2.0);
    float isDeep = step(0.5, encoded);
    return lerp(shallow, deep, isDeep);
}

float3 SGT_ApplyOceanColor(
    float3 terrainColor,
    float  waterDepth01,
    float3 waterColor,
    float2 waterDensity,
    float  waterBlend,
    float3 ambientColor)
{
    float depth = SGT_DecodeWaterDepth(waterDepth01);
    float3 w = float3(1.0, 0.2, 0.1);

	float3 e0 = w * waterDensity.x;
	float3 e1 = w * waterDensity.y;

	float3 floorT0 = exp(-e0 * depth);
	float3 floorT1 = exp(-e1 * depth);
	
	float3 volumeT0 = floorT0 * floorT0 * floorT0;
	float3 volumeT1 = floorT1 * floorT1 * floorT1;

	float3 scatter = waterColor * ambientColor;

	float3 col0 = terrainColor * floorT0 + scatter * (1.0 - volumeT0);
	float3 col1 = terrainColor * floorT1 + scatter * (1.0 - volumeT1);

	return lerp(col0, col1, waterBlend);
}

float SGT_Bayer4x4(float2 screenPos)
{
	uint2 p = uint2(screenPos * _ScreenParams.xy) % 4;
	float bayer[16] = { 0,  8,  2, 10, 12,  4, 14,  6, 3, 11,  1,  9, 15,  7, 13,  5 };
	return (bayer[p.x + p.y * 4] + 0.5) / 16.0;
}

float SGT_Fresnel(float3 dir, float3 normal, float airIOR, float waterIOR)
{
	float cosTheta = clamp(dot(dir, normal), 0.0, 1.0);
	float F0       = pow(abs((waterIOR / airIOR - 1.0) / (waterIOR / airIOR + 1.0)), 2.0);

	return F0 + (1.0 - F0) * pow(abs(1.0 - cosTheta), 5.0);
}

void SSS_Frag(inout SSS_SurfaceData o, inout SSS_FragmentData d)
{
	#if CW_COMPRESS_POSITIONS
		d.worldSpacePosition = _WorldSpaceCameraPos - d.worldSpaceViewDir * d.extraV2F0.z;
	#endif
	
	float4 dataA = tex2Dlod(DataA, float4(d.extraV2F0.xy,0,0));
	float4 dataN = tex2Dlod(DataN, float4(d.extraV2F0.xy,0,0)); 
	float2 tnorm = dataN.xy * 2.0 - 1.0;
	
	tnorm.x = -tnorm.x; // Don't set negative tangent sign, so do it manually
	
	o.Albedo     = dataA.xyz;
	o.Occlusion  = _SGT_Occlusion;
	o.Emission   = dataA.xyz * dataN.z;
	o.Smoothness = dataN.w;
	o.Metallic   = 0.0;
	o.Normal     = float3(tnorm, sqrt(1.0 - saturate(dot(tnorm, tnorm))));
	o.Alpha      = dataN.x != 0.0 && dataN.y != 0.0;
	
	float localEyeHeight = d.extraV2F0.w;
	
	#if _SGT_OCEAN_FADE
		float  fadeTransition = step(SGT_Bayer4x4(d.screenUV), _SGT_OceanFade);
		float  cameraDistance = distance(d.worldSpacePosition, _WorldSpaceCameraPos);
		float  cameraDist01   = 1.0 - pow(abs(saturate(1.0 - cameraDistance / _SGT_OceanRadius)), 8.0);
		float3 oceanColor     = SGT_ApplyOceanColor(o.Albedo, dataA.w, _SGT_OceanColor, _SGT_OceanDensity, cameraDist01, 1.0);
		float  fade           = saturate(dataA.w > 0) * fadeTransition;

		o.Albedo     = lerp(o.Albedo, oceanColor, fade);
		o.Smoothness = lerp(o.Smoothness, _SGT_OceanSmoothness, fade);
		o.Normal     = lerp(o.Normal, float3(0.0, 0.0, 1.0), fade);
		o.Occlusion  = lerp(o.Occlusion, 0.15, fade);

		#if _SGT_SKY
			float  airIOR   = 1.0;
			float  waterIOR = 1.33;
			float3 opos     = mul(_SGT_WorldToSky, float4(d.worldSpacePosition, 1.0)).xyz;
			float3 onrm     = normalize(opos);
			float3 oview    = normalize(mul(_SGT_WorldToSky, float4(d.worldSpaceViewDir, 0.0)).xyz);
			float3 odir     = reflect(oview, onrm);
	
			o.Emission = SGT_SampleSkyReflection(opos, -odir) * fade * SGT_Fresnel(oview, onrm, airIOR, waterIOR);
			#if _SSS_HDRP
				o.Emission *= 120000;
			#endif
		#endif
		//o.Emission +=  _SGT_AmbientColor.xyz * 0.02;
	#endif
	
	#if _SGT_CLOUDS
		float shadow = SGT_SampleCloudDensity(d.worldSpacePosition);
		
		o.Albedo    = lerp(o.Albedo   , 0.0, shadow);
		o.Occlusion = lerp(o.Occlusion, 0.0, shadow);
	#endif
}


void Vert_float
	(
	float  iInstanceID,
	float3 iPosition,
	float3 iNormal,
	float3 iTangent,
	float4 iColor,
	float4 iTexcoord0,
	float4 iTexcoord1,
	float4 iTexcoord2,
	float4 iTexcoord3,

	out float3 oPosition,
	out float3 oNormal,
	out float3 oTangent,
	out float4 oExtraV2F0,
	out float4 oExtraV2F1,
	out float4 oExtraV2F2,
	out float4 oExtraV2F3,
	out float4 oExtraV2F4,
	out float4 oExtraV2F5,
	out float4 oExtraV2F6,
	out float4 oExtraV2F7
	)
{
	SSS_VertexData v = (SSS_VertexData)0;
	
	v.instanceID = iInstanceID;
	v.position   = iPosition;
	v.normal     = iNormal;
	v.tangent    = iTangent;
	v.color      = iColor;
	v.texcoord0  = iTexcoord0;
	v.texcoord1  = iTexcoord1;
	v.texcoord2  = iTexcoord2;
	v.texcoord3  = iTexcoord3;
	v.extraV2F0  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F1  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F2  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F3  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F4  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F5  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F6  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F7  = float4(0.0, 0.0, 0.0, 0.0);
	
	SSS_Vert(v);
	
	oPosition  = v.position;
	oNormal    = v.normal;
	oTangent   = v.tangent;
	oExtraV2F0 = v.extraV2F0;
	oExtraV2F1 = v.extraV2F1;
	oExtraV2F2 = v.extraV2F2;
	oExtraV2F3 = v.extraV2F3;
	oExtraV2F4 = v.extraV2F4;
	oExtraV2F5 = v.extraV2F5;
	oExtraV2F6 = v.extraV2F6;
	oExtraV2F7 = v.extraV2F7;
}

void Frag_float
	(
	inout float3 iPosition,
	inout float3 iNormal,
	inout float3 iTangent,
	bool   iIsFrontFace,
	float4 iColor,
	float4 iTexcoord0,
	float4 iTexcoord1,
	float4 iTexcoord2,
	float4 iTexcoord3,
	float4 iExtraV2F0,
	float4 iExtraV2F1,
	float4 iExtraV2F2,
	float4 iExtraV2F3,
	float4 iExtraV2F4,
	float4 iExtraV2F5,
	float4 iExtraV2F6,
	float4 iExtraV2F7,

	out float4x4 oExtra,
	out float3   oAlbedo,
	out float    oSmoothness,
	out float3   oNormal,
	out float3   oEmission,
	out float    oOcclusion,
	out float    oMetallic,
	out float    oAlpha
	)
{
	SSS_SurfaceData  s = (SSS_SurfaceData)0;
	SSS_FragmentData d = (SSS_FragmentData)0;
	
	s.Albedo = 1.0;
	s.Smoothness = 0.5;
	s.Normal = float3(0.0, 0.0, 1.0);
	s.Emission = float3(0.0, 0.0, 0.0);
	s.Occlusion = 0.0;
	s.Metallic = 0.0;
	s.Alpha = 1.0;
	
	iPosition = SSS_WorldToAbsolute(iPosition);
	
	d.localSpacePosition = SSS_WorldToObject(iPosition);
	d.localSpaceNormal   = normalize(SSS_WorldToObjectDir(iNormal));
	d.localSpaceTangent  = normalize(SSS_WorldToObjectDir(iTangent));
	
	d.worldSpacePosition = iPosition;
	d.worldSpaceNormal   = iNormal;
	d.worldSpaceTangent  = iTangent;
	//d.tangentSign;
	
	d.worldSpaceViewDir  = normalize(_WorldSpaceCameraPos - d.worldSpacePosition);
	//d.tangentSpaceViewDir;
	
	d.texcoord0 = iTexcoord0;
	d.texcoord1 = iTexcoord1;
	d.texcoord2 = iTexcoord2;
	d.texcoord3 = iTexcoord3;
	
	d.screenPos = float4(SSS_WorldToScreen(iPosition), 1.0);
	d.screenUV  = d.screenPos.xy;

	d.vertexColor = iColor;
	d.isFrontFace = iIsFrontFace;
	
	d.extraV2F0 = iExtraV2F0;
	d.extraV2F1 = iExtraV2F1;
	d.extraV2F2 = iExtraV2F2;
	d.extraV2F3 = iExtraV2F3;
	d.extraV2F4 = iExtraV2F4;
	d.extraV2F5 = iExtraV2F5;
	d.extraV2F6 = iExtraV2F6;
	d.extraV2F7 = iExtraV2F7;

	d.TBNMatrix = float3x3(d.worldSpaceTangent, normalize(cross(d.worldSpaceNormal, d.worldSpaceTangent)), d.worldSpaceNormal);
	
	SSS_Frag(s, d);
	
	iPosition = SSS_AbsoluteToWorld(d.worldSpacePosition); iNormal = d.worldSpaceNormal; iTangent = d.worldSpaceTangent; // Write back
	
	oExtra      = float4x4(d.extraV2F0, d.extraV2F1, d.extraV2F2, d.extraV2F3);
	oAlbedo     = s.Albedo;
	oSmoothness = s.Smoothness;
	oNormal     = s.Normal;
	oEmission   = s.Emission;
	oOcclusion  = s.Occlusion;
	oMetallic   = s.Metallic;
	oAlpha      = s.Alpha;
}



	#pragma shader_feature_local _SGT_SHAPE_SQUARE _SGT_SHAPE_SPHERE
	#pragma shader_feature_local _SGT_CLOUDS_OFF _SGT_CLOUDS
	#pragma shader_feature_local _SGT_OCEAN_FADE_OFF _SGT_OCEAN_FADE
	#pragma shader_feature_local _SGT_SKY_OFF _SGT_SKY
	#pragma multi_compile __ CW_COMPRESS_POSITIONS



// Graph Functions
// GraphFunctions: <None>

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
float4 extraV2F0;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
float4 _UV_ccc703dc3bdd420e8a8a4aa0607d4ec4_Out_0_Vector4 = IN.uv0;
float4 _UV_46571950324b44e8bf8895f08abd191f_Out_0_Vector4 = IN.uv1;
float4 _UV_ba4ef72275334f08b9b01fedf9da0065_Out_0_Vector4 = IN.uv2;
float4 _UV_255ab3a515d14af686e70c40d04416e6_Out_0_Vector4 = IN.uv3;
float3 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oPosition_0_Vector3;
float3 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oNormal_5_Vector3;
float3 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oTangent_6_Vector3;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F0_7_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F1_8_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F2_13_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F3_14_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F4_15_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F5_16_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F6_17_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F7_18_Vector4;
Vert_float(IN.InstanceID, IN.ObjectSpacePosition, IN.ObjectSpaceNormal, IN.ObjectSpaceTangent, IN.VertexColor, _UV_ccc703dc3bdd420e8a8a4aa0607d4ec4_Out_0_Vector4, _UV_46571950324b44e8bf8895f08abd191f_Out_0_Vector4, _UV_ba4ef72275334f08b9b01fedf9da0065_Out_0_Vector4, _UV_255ab3a515d14af686e70c40d04416e6_Out_0_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oPosition_0_Vector3, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oNormal_5_Vector3, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oTangent_6_Vector3, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F0_7_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F1_8_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F2_13_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F3_14_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F4_15_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F5_16_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F6_17_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F7_18_Vector4);
description.Position = _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oPosition_0_Vector3;
description.Normal = _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oNormal_5_Vector3;
description.Tangent = _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oTangent_6_Vector3;
description.extraV2F0 = _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F0_7_Vector4;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
output.extraV2F0 = input.extraV2F0;
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
float3 BaseColor;
float3 NormalTS;
float3 Emission;
float Metallic;
float Smoothness;
float Occlusion;
float Alpha;
float AlphaClipThreshold;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
float _IsFrontFace_5b03f99e2e7841c3a7d2ae306590b576_Out_0_Boolean = max(0, IN.FaceSign.x);
float4 _UV_c1e39353e82e434da821b9bdc4338e4b_Out_0_Vector4 = IN.uv0;
float4 _UV_64d51b5974bc4ecf849e7307e6de2727_Out_0_Vector4 = IN.uv1;
float4x4 _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oExtra_23_Matrix4;
float3 _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oAlbedo_0_Vector3;
float _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oSmoothness_5_Float;
float3 _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oNormal_6_Vector3;
float3 _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oEmission_7_Vector3;
float _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oOcclusion_8_Float;
float _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oMetallic_9_Float;
float _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oAlpha_10_Float;
Frag_float(IN.WorldSpacePosition, IN.WorldSpaceNormal, IN.WorldSpaceTangent, _IsFrontFace_5b03f99e2e7841c3a7d2ae306590b576_Out_0_Boolean, float4 (0, 0, 0, 0), _UV_c1e39353e82e434da821b9bdc4338e4b_Out_0_Vector4, _UV_64d51b5974bc4ecf849e7307e6de2727_Out_0_Vector4, float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), IN.extraV2F0, float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oExtra_23_Matrix4, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oAlbedo_0_Vector3, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oSmoothness_5_Float, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oNormal_6_Vector3, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oEmission_7_Vector3, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oOcclusion_8_Float, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oMetallic_9_Float, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oAlpha_10_Float);
surface.BaseColor = _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oAlbedo_0_Vector3;
surface.NormalTS = _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oNormal_6_Vector3;
surface.Emission = _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oEmission_7_Vector3;
surface.Metallic = _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oMetallic_9_Float;
surface.Smoothness = _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oSmoothness_5_Float;
surface.Occlusion = _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oOcclusion_8_Float;
surface.Alpha = _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oAlpha_10_Float;
surface.AlphaClipThreshold = float(0.5);
return surface;
}

// --------------------------------------------------
// Build Graph Inputs

VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;
    output.uv0 =                                        input.uv0;
    output.uv1 =                                        input.uv1;
    output.uv2 =                                        input.uv2;
    output.uv3 =                                        input.uv3;
    output.VertexColor =                                input.color;
#if UNITY_ANY_INSTANCING_ENABLED
    output.InstanceID =                                 unity_InstanceID;
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
    output.InstanceID =                                 input.instanceID;
#endif

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

    output.extraV2F0 = input.extraV2F0;

    // must use interpolated tangent, bitangent and normal before they are normalized in the pixel shader.
    float3 unnormalizedNormalWS = input.normalWS;
    const float renormFactor = 1.0 / length(unnormalizedNormalWS);


    output.WorldSpaceNormal = renormFactor * input.normalWS.xyz;      // we want a unit length Normal Vector node in shader graph
    output.TangentSpaceNormal = float3(0.0f, 0.0f, 1.0f);

    // to preserve mikktspace compliance we use same scale renormFactor as was used on the normal.
    // This is explained in section 2.2 in "surface gradient based bump mapping framework"
    output.WorldSpaceTangent = renormFactor * input.tangentWS.xyz;

    output.WorldSpacePosition = input.positionWS;

    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


    output.uv0 = input.texCoord0;
    output.uv1 = input.texCoord1;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
    BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

void BuildAppDataFull(Attributes attributes, VertexDescription vertexDescription, inout appdata_full result)
{
    result.vertex     = float4(attributes.positionOS, 1);
    result.tangent    = attributes.tangentOS;
    result.normal     = attributes.normalOS;
    result.texcoord   = attributes.uv0;
    result.texcoord1  = attributes.uv1;
    result.texcoord2  = attributes.uv2;
    result.texcoord3  = attributes.uv3;
    result.color      = attributes.color;
    result.vertex     = float4(vertexDescription.Position, 1);
    result.normal     = vertexDescription.Normal;
    result.tangent    = float4(vertexDescription.Tangent, 0);
    #if UNITY_ANY_INSTANCING_ENABLED
    result.instanceID = attributes.instanceID;
    #endif
}

void VaryingsToSurfaceVertex(Varyings varyings, inout v2f_surf result)
{
    result.pos = varyings.positionCS;
    result.worldPos = varyings.positionWS;
    result.worldNormal = varyings.normalWS;
    // World Tangent isn't an available input on v2f_surf

    result._ShadowCoord = varyings.shadowCoord;

    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
    #if UNITY_SHOULD_SAMPLE_SH
    #if !defined(LIGHTMAP_ON)
    result.sh = varyings.sh;
    #endif
    #endif
    #if defined(LIGHTMAP_ON)
    result.lmap.xy = varyings.lightmapUV;
    #endif
    #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        result.fogCoord = varyings.fogFactorAndVertexLight.x;
        COPY_TO_LIGHT_COORDS(result, varyings.fogFactorAndVertexLight.yzw);
    #endif

    DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(varyings, result);
}

void SurfaceVertexToVaryings(v2f_surf surfVertex, inout Varyings result)
{
    result.positionCS = surfVertex.pos;
    result.positionWS = surfVertex.worldPos;
    result.normalWS = surfVertex.worldNormal;
    // viewDirectionWS is never filled out in the legacy pass' function. Always use the value computed by SRP
    // World Tangent isn't an available input on v2f_surf
    result.shadowCoord = surfVertex._ShadowCoord;

    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
    #if UNITY_SHOULD_SAMPLE_SH
    #if !defined(LIGHTMAP_ON)
    result.sh = surfVertex.sh;
    #endif
    #endif
    #if defined(LIGHTMAP_ON)
    result.lightmapUV = surfVertex.lmap.xy;
    #endif
    #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        result.fogFactorAndVertexLight.x = surfVertex.fogCoord;
        COPY_FROM_LIGHT_COORDS(result.fogFactorAndVertexLight.yzw, surfVertex);
    #endif

    DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(surfVertex, result);
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/PBRForwardAddPass.hlsl"

ENDHLSL
}
Pass
{
    Name "BuiltIn Deferred"
    Tags
    {
        "LightMode" = "Deferred"
    }

// Render State
Cull Back
Blend One Zero
ZTest LEqual
ZWrite On

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM
#define _SSS_PASS_BUILTIN_DEFERRED 1

#define _SSS_BIRP 1


// Pragmas
#pragma target 4.5
#pragma multi_compile_instancing
#pragma exclude_renderers nomrt
#pragma multi_compile_prepassfinal
#pragma skip_variants FOG_LINEAR FOG_EXP FOG_EXP2
#pragma vertex vert
#pragma fragment frag

// Keywords
#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
#pragma multi_compile _ _SHADOWS_SOFT
#pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
#pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE
#pragma multi_compile _ _GBUFFER_NORMALS_OCT
// GraphKeywords: <None>

// Defines
#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_TEXCOORD0
#define ATTRIBUTES_NEED_TEXCOORD1
#define ATTRIBUTES_NEED_TEXCOORD2
#define ATTRIBUTES_NEED_TEXCOORD3
#define ATTRIBUTES_NEED_COLOR
#define ATTRIBUTES_NEED_INSTANCEID
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_NORMAL_WS
#define VARYINGS_NEED_TANGENT_WS
#define VARYINGS_NEED_TEXCOORD0
#define VARYINGS_NEED_TEXCOORD1
#define VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
#define VARYINGS_NEED_CULLFACE
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_DEFERRED
#define BUILTIN_TARGET_API 1
#define _BUILTIN_AlphaClip 1
#define _BUILTIN_ALPHATEST_ON 1
#ifdef _BUILTIN_SURFACE_TYPE_TRANSPARENT
#define _SURFACE_TYPE_TRANSPARENT _BUILTIN_SURFACE_TYPE_TRANSPARENT
#endif
#ifdef _BUILTIN_ALPHATEST_ON
#define _ALPHATEST_ON _BUILTIN_ALPHATEST_ON
#endif
#ifdef _BUILTIN_AlphaClip
#define _AlphaClip _BUILTIN_AlphaClip
#endif
#ifdef _BUILTIN_ALPHAPREMULTIPLY_ON
#define _ALPHAPREMULTIPLY_ON _BUILTIN_ALPHAPREMULTIPLY_ON
#endif


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Shim/Shims.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/LegacySurfaceVertex.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/ShaderGraphFunctions.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
 float4 uv0 : TEXCOORD0;
 float4 uv1 : TEXCOORD1;
 float4 uv2 : TEXCOORD2;
 float4 uv3 : TEXCOORD3;
 float4 color : COLOR;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float3 positionWS;
 float3 normalWS;
 float4 tangentWS;
 float4 texCoord0;
 float4 texCoord1;
#if defined(LIGHTMAP_ON)
 float2 lightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
 float3 sh;
#endif
 float4 fogFactorAndVertexLight;
 float4 shadowCoord;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
 float4 extraV2F0;
};
struct SurfaceDescriptionInputs
{
 float3 WorldSpaceNormal;
 float3 TangentSpaceNormal;
 float3 WorldSpaceTangent;
 float3 WorldSpacePosition;
 float4 uv0;
 float4 uv1;
 float FaceSign;
 float4 extraV2F0;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
 float4 uv0;
 float4 uv1;
 float4 uv2;
 float4 uv3;
 float4 VertexColor;
 uint InstanceID;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
#if defined(LIGHTMAP_ON)
 float2 lightmapUV : INTERP0;
#endif
#if !defined(LIGHTMAP_ON)
 float3 sh : INTERP1;
#endif
 float4 tangentWS : INTERP2;
 float4 texCoord0 : INTERP3;
 float4 texCoord1 : INTERP4;
 float4 fogFactorAndVertexLight : INTERP5;
 float4 shadowCoord : INTERP6;
 float4 extraV2F0 : INTERP7;
 float3 positionWS : INTERP8;
 float3 normalWS : INTERP9;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
#if defined(LIGHTMAP_ON)
output.lightmapUV = input.lightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
output.sh = input.sh;
#endif
output.tangentWS.xyzw = input.tangentWS;
output.texCoord0.xyzw = input.texCoord0;
output.texCoord1.xyzw = input.texCoord1;
output.fogFactorAndVertexLight.xyzw = input.fogFactorAndVertexLight;
output.shadowCoord.xyzw = input.shadowCoord;
output.extraV2F0.xyzw = input.extraV2F0;
output.positionWS.xyz = input.positionWS;
output.normalWS.xyz = input.normalWS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
#if defined(LIGHTMAP_ON)
output.lightmapUV = input.lightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
output.sh = input.sh;
#endif
output.tangentWS = input.tangentWS.xyzw;
output.texCoord0 = input.texCoord0.xyzw;
output.texCoord1 = input.texCoord1.xyzw;
output.fogFactorAndVertexLight = input.fogFactorAndVertexLight.xyzw;
output.shadowCoord = input.shadowCoord.xyzw;
output.extraV2F0 = input.extraV2F0.xyzw;
output.positionWS = input.positionWS.xyz;
output.normalWS = input.normalWS.xyz;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties
CBUFFER_START(UnityPerMaterial)





CBUFFER_END


// Object and Global properties

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Includes
// UNITY_SHADER_NO_UPGRADE
float3 SSS_HClipToScreen(float4 v)
{
	float3 uv = v.xyz / v.w;
	#if UNITY_UV_STARTS_AT_TOP
		uv.y = -uv.y;
	#endif
	uv.xy = uv.xy * 0.5 + 0.5;
	return uv;
}

#if _SSS_HDRP
	float3 SSS_WorldToAbsolute(float3 v) { return GetAbsolutePositionWS(v); }
	float3 SSS_AbsoluteToWorld(float3 v) { return GetCameraRelativePositionWS(v); }
#else
	float3 SSS_WorldToAbsolute(float3 v) { return v; }
	float3 SSS_AbsoluteToWorld(float3 v) { return v; }
#endif

float3 SSS_WorldToView(float3 v) { return TransformWorldToView(SSS_AbsoluteToWorld(v)); }
float3 SSS_WorldToObject(float3 v) { return TransformWorldToObject(SSS_AbsoluteToWorld(v)); }
float3 SSS_WorldToScreen(float3 v) { return SSS_HClipToScreen(TransformWorldToHClip(SSS_AbsoluteToWorld(v))); }
float3 SSS_ObjectToScreen(float3 v) { return SSS_HClipToScreen(TransformObjectToHClip(v)); }
float3 SSS_ObjectToWorld(float3 v) { return SSS_WorldToAbsolute(TransformObjectToWorld(v)); }
float3 SSS_ObjectToView(float3 v) { return TransformWorldToView(TransformObjectToWorld(v)); }
float3 SSS_ScreenToWorld(float3 v) { return SSS_WorldToAbsolute(ComputeWorldSpacePosition(v.xy, v.z, UNITY_MATRIX_I_VP)); }
float3 SSS_ScreenToObject(float3 v) { return SSS_WorldToObject(SSS_ScreenToWorld(v)); }
float3 SSS_ScreenToView(float3 v) { return SSS_WorldToView(SSS_ScreenToWorld(v)); }
float3 SSS_ViewToWorld(float3 v) { return mul(UNITY_MATRIX_I_V, float4(v, 1.0)).xyz; }
float3 SSS_ViewToObject(float3 v) { return TransformWorldToObject(SSS_ViewToWorld(v)); }
float3 SSS_ViewToScreen(float3 v) { return SSS_HClipToScreen(TransformWViewToHClip(v)); }
float3 SSS_ObjectToWorldDir(float3 v)
{
	#if _SSS_BIRP
		return TransformObjectToWorldDir(v);
	#else
		return TransformObjectToWorldDir(v, true);
	#endif
}
float3 SSS_ObjectToViewDir(float3 v)
{
	#if _SSS_BIRP
		return TransformWorldToViewDir(TransformObjectToWorldDir(v));
	#else
		return TransformWorldToViewDir(TransformObjectToWorldDir(v, false), true);
	#endif
}
float3 SSS_WorldToObjectDir(float3 v)
{
	#if _SSS_BIRP
		return TransformWorldToObjectDir(v);
	#else
		return TransformWorldToObjectDir(v, true);
	#endif
}
float3 SSS_WorldToViewDir(float3 v)
{
	#if _SSS_BIRP
		return TransformWorldToViewDir(v);
	#else
		return TransformWorldToViewDir(v, true);
	#endif
}
float3 SSS_ViewToObjectDir(float3 v)
{
	#if _SSS_URP || _SSS_HDRP
		return SSS_WorldToObjectDir(mul(UNITY_MATRIX_I_V, float4(v, 0.0)).xyz);
	#else
		return SSS_WorldToObjectDir(mul((float3x3)UNITY_MATRIX_I_V, v));
	#endif
}
float3 SSS_ViewToWorldDir(float3 v)
{
	return mul(UNITY_MATRIX_I_V, float4(v, 0.0)).xyz;
}

#if _SSS_NO_DERIVATIVES
	float3 SSS_GetSceneColor(float2 uv) { return float3(0.0, 0.0, 0.0); }
	float3 SSS_GetSceneColorHD(float2 uv) { return SSS_GetSceneColor(uv); }
	float  SSS_GetSceneDepth(float2 uv) { return 0.0; }
#else
	#if _SSS_URP
		float3 SSS_GetSceneColor(float2 uv) { return SHADERGRAPH_SAMPLE_SCENE_COLOR(uv).xyz; }
		float3 SSS_GetSceneColorHD(float2 uv) { return SSS_GetSceneColor(uv); }
	#elif _SSS_HDRP
		float3 SSS_GetSceneColor(float2 uv) { return SHADERGRAPH_SAMPLE_SCENE_COLOR(uv).xyz; }
		float3 SSS_GetSceneColorHD(float2 uv)
		{
			#if defined(REQUIRE_OPAQUE_TEXTURE) && defined(_SURFACE_TYPE_TRANSPARENT) && defined(SHADERPASS) && (SHADERPASS != SHADERPASS_LIGHT_TRANSPORT) && (SHADERPASS != SHADERPASS_PATH_TRACING) && (SHADERPASS != SHADERPASS_RAYTRACING_VISIBILITY) && (SHADERPASS != SHADERPASS_RAYTRACING_FORWARD)
			return SampleCameraColor(uv, 0);
			#endif
			#if defined(REQUIRE_OPAQUE_TEXTURE) && defined(CUSTOM_PASS_SAMPLING_HLSL) && defined(SHADERPASS) && (SHADERPASS == SHADERPASS_DRAWPROCEDURAL || SHADERPASS == SHADERPASS_BLIT)
			return CustomPassSampleCameraColor(uv, 0);
			#endif
			return float3(0.0, 0.0, 0.0);
		}
	#else
		#if defined(UNITY_DECLARE_OPAQUE_TEXTURE_INCLUDED)
			float3 SSS_GetSceneColor(float2 uv) { return SampleSceneColor(uv); }
		#else
			sampler2D _CameraOpaqueTexture; float3 SSS_GetSceneColor(float2 uv) { return tex2D(_CameraOpaqueTexture, uv).xyz; }
		#endif
		float3 SSS_GetSceneColorHD(float2 uv) { return SSS_GetSceneColor(uv); }
	#endif

	float SSS_GetSceneDepth(float2 uv) { return SHADERGRAPH_SAMPLE_SCENE_DEPTH(uv); }
#endif

float3 SSS_GetSceneWorldPosition(float2 screenUV, float sceneDepth)
{
	#if _SSS_BIRP
		float4 clipPos  = float4(screenUV * 2.0f - 1.0f, 0.0f, 1.0f);
		float4 viewPos  = mul(unity_CameraInvProjection, clipPos);
		float3 worldDir = mul((float3x3)UNITY_MATRIX_I_V, viewPos.xyz);
					
		return _WorldSpaceCameraPos + worldDir * LinearEyeDepth(sceneDepth);
	#else
		float4 clipPos = float4(screenUV * 2.0 - 1.0, sceneDepth, 1.0);
					
		#if UNITY_UV_STARTS_AT_TOP
			clipPos.y = -clipPos.y;
		#endif
					
		float4 worldPos = mul(UNITY_MATRIX_I_VP, clipPos);
					
		worldPos.xyz /= worldPos.w;
					
		#if _SSS_HDRP
			worldPos.xyz = GetAbsolutePositionWS(worldPos.xyz);
		#endif
					
		return worldPos.xyz;
	#endif
}

float SSS_GetSceneWorldDistance(float2 screenUV, float sceneDepth)
{
	return distance(_WorldSpaceCameraPos, SSS_GetSceneWorldPosition(screenUV, sceneDepth));
}

float3 SSS_UnpackNormalScale(float4 c, float s)
{
	#if _SSS_BIRP
		return UnpackScaleNormal(c, s);
	#else
		return UnpackNormalScale(c, s);
	#endif
}

struct SSS_VertexData
{
	float  instanceID;
	float3 position;
	float3 normal;
	float3 tangent;
	float4 color;
	float4 texcoord0;
	float4 texcoord1;
	float4 texcoord2;
	float4 texcoord3;
	float4 extraV2F0;
	float4 extraV2F1;
	float4 extraV2F2;
	float4 extraV2F3;
	float4 extraV2F4;
	float4 extraV2F5;
	float4 extraV2F6;
	float4 extraV2F7;
	



};

struct SSS_FragmentData
{
	float3 localSpacePosition;
	float3 localSpaceNormal;
	float3 localSpaceTangent;
	
	float3 worldSpacePosition;
	float3 worldSpaceNormal;
	float3 worldSpaceTangent;
	//float tangentSign;

	float3 worldSpaceViewDir;
	//float3 tangentSpaceViewDir;
	
	float4 texcoord0;
	float4 texcoord1;
	float4 texcoord2;
	float4 texcoord3;
	
	float2 screenUV;
	float4 screenPos;

	float4 vertexColor;
	bool isFrontFace;
	
	float4 extraV2F0;
	float4 extraV2F1;
	float4 extraV2F2;
	float4 extraV2F3;
	float4 extraV2F4;
	float4 extraV2F5;
	float4 extraV2F6;
	float4 extraV2F7;

	float3x3 TBNMatrix;
	



};

struct SSS_SurfaceData
{
	float3 Albedo;
	float  Smoothness;
	float3 Normal;
	float3 Emission;
	float  Occlusion;
	float  Metallic;
	float  Alpha;
};

float2 _CW_PositionCompressionData; // x = start distance, y = max distance limit

#define DEBUG_SCALE 0.001
#define SSS_LOG_K          0.0001
#define SSS_MAX_WORLD_DIST 1e27

float SSS_GetCompressionRatio(float worldDist)
{
    float startDist = _CW_PositionCompressionData.x;
    float maxDist   = _CW_PositionCompressionData.y;

    // no compression nearby
    if (worldDist <= startDist)
        return 1.0;

    float excess = worldDist - startDist;

    float compressedDist =
        startDist +
        (maxDist - startDist) *
        log(1.0 + excess * SSS_LOG_K) /
        log(1.0 + SSS_MAX_WORLD_DIST * SSS_LOG_K);

    return compressedDist / worldDist;
}

float SSS_DecompressWorldDist(float compressedDist)
{
    float startDist = _CW_PositionCompressionData.x;
    float maxDist   = _CW_PositionCompressionData.y;

    if (compressedDist <= startDist)
        return compressedDist;

    float t =
        saturate(
            (compressedDist - startDist) /
            (maxDist - startDist)
        );

    return startDist +
        (
            exp(
                t *
                log(1.0 + SSS_MAX_WORLD_DIST * SSS_LOG_K)
            ) - 1.0
        ) / SSS_LOG_K;
}

float3 SSS_CompressWorld(float3 worldPos, float3 worldCenter, float worldRadius, out float ratio) 
{
    float3 worldDelta = worldPos - _WorldSpaceCameraPos;
    float  farDist    = distance(_WorldSpaceCameraPos, worldCenter) + worldRadius;
    
    ratio = SSS_GetCompressionRatio(farDist);
    
    return _WorldSpaceCameraPos + worldDelta * ratio;
}

float3 SSS_CompressWorld(float3 worldPos) 
{
    float3 worldDelta = worldPos - _WorldSpaceCameraPos;
    float  worldDist  = length(worldDelta);
    
    return _WorldSpaceCameraPos + worldDelta * SSS_GetCompressionRatio(worldDist);
}

float3 SSS_DecompressWorld(float3 worldPos)
{
    float3 worldDelta = worldPos - _WorldSpaceCameraPos;
    float  worldDist  = length(worldDelta);
    
    return worldDist > 0.001 ? _WorldSpaceCameraPos + (worldDelta / worldDist) * SSS_DecompressWorldDist(worldDist) : _WorldSpaceCameraPos;
}

float3 SSS_DecompressWorld(float3 worldPos, float ratio) 
{
    float3 worldDelta = worldPos - _WorldSpaceCameraPos;
    return _WorldSpaceCameraPos + worldDelta / ratio;
}

float SSS_ElongateDistance(float worldDist)
{
	float minDist = 10000.0;
	float maxDist = 100000.0;
	float scale   = saturate((worldDist - minDist) / (maxDist - minDist));

	return worldDist * lerp(1.0, 1.2, scale);
}
#define SGT_OPACITY_STEPS  5

float4x4        _SGT_SkyToWorld;
float4x4        _SGT_WorldToSky;
float4          _SGT_SkyColor;
float           _SGT_SkyBrightness;
float4          _SGT_SkyAmbientTint;
float           _SGT_SkyExposure;
float4          _SGT_SkyRadius;
float4          _SGT_SkyAltitudeData;
float           _SGT_SkyDensity;
float4          _SGT_SkyMieWeight;
float4          _SGT_SkyMieData;
sampler3D       _SGT_SkyRadianceTex;
float           _SGT_SkyRadianceLod;
sampler2D       _SGT_SkyLightingTex;
sampler2D       _SGT_SkyAlbedoTex;
float           _SGT_SkyRevealStars;
float           _SGT_SkyDepthOpaque;

float     _SGT_Volumetrics_Downscale; // Global
float     _SGT_Volumetrics_NearClip; // Global
Texture2D _SGT_Volumetrics_ColorTex; // Global
float4    _SGT_Volumetrics_ColorSize; // Global
Texture2D _SGT_Volumetrics_DepthTex; // Global
float2    _SGT_Volumetrics_DepthSize; // Global

SamplerState my_linear_clamp_sampler;
SamplerState my_point_clamp_sampler;

sampler2D _SGT_BlueNoiseTex; // Global
float     _SGT_Frame; // Global
float3    _SGT_AmbientColor; // Global
	
float SGT_DitherBlue(float2 screenUV)
{
	float2 pixel = floor(screenUV * _ScreenParams.xy);
	float  noise = tex2D(_SGT_BlueNoiseTex, pixel / 64.0f).r;
	return frac(noise + _SGT_Frame / sqrt(0.5f));
}

float SGT_DitherFast(float2 screenUV)
{
    float2 R2 = float2(0.7548776662, 0.5698402909);
    float2 p  = screenUV * _ScreenParams.xy + frac(_Time.y * R2) * 256.0;
    return frac(52.9829189 * frac(dot(p, float2(0.06711056, 0.00583715))));
}
	
float2 SGT_DirectionToEquirectangular(float3 dir)
{
	dir = normalize(dir);
	float u = atan2(dir.z, dir.x) / (2.0 * 3.141592653) + 0.5;
	float v = asin(clamp(dir.y, -1.0, 1.0)) / 3.141592653 + 0.5;
	return float2(u, v);
}

float3 SGT_SphereTest(float3 ray, float3 rayD, float radius)
{
	float B = -dot(ray, rayD);
	float C = dot(ray, ray) - radius * radius;
	float D = B * B - C;
	float E = sqrt(max(D, 0.0f));
	return float3(B - E, B + E, D);
}

float SGT_InverseLerp(float a, float b, float v)
{
	return (v - a) / (b - a);
}

float3 SGT_CalculateMie(float3 ray, float mie)
{
	return normalize(ray * _SGT_SkyMieWeight.xyz + 1e-6) * mie;
}

float SGT_GetPitch01(float3 rayDir, float3 up, float altitude01)
{
	float power = 2.0f;
	float eyeHeight = _SGT_SkyRadius.x + altitude01 * (_SGT_SkyRadius.y - _SGT_SkyRadius.x);
	float horizonAngleFromDown = -acos(clamp(_SGT_SkyRadius.x / max(eyeHeight, 0.00001f), -1.0f, 1.0f));
	float rayAng = asin(clamp(dot(up, rayDir), -1.0f, 1.0f));
	float sun01;

	if (rayAng < horizonAngleFromDown)
	{
		sun01 = SGT_InverseLerp(-3.141592653 * 0.5f, horizonAngleFromDown, rayAng);
		sun01 = 1.0f - pow(1.0f - sun01, 1.0f / power);
		sun01 = sun01 * 0.5f;
	}
	else
	{
		sun01 = SGT_InverseLerp(horizonAngleFromDown, 3.141592653 * 0.5f, rayAng);
		sun01 = pow(sun01, 1.0f / power);
		sun01 = sun01 * 0.5f + 0.5f;
	}

	return sun01;
}

float3 GetLutCoord(float3 rayPos, float3 rayDir, float3 sunDir, float dither)
{
	float3 up         = normalize(rayPos);
	float  eyeHeight  = length(rayPos);
	float  altitude01 = saturate(SGT_InverseLerp(_SGT_SkyRadius.z, 1.0f, eyeHeight));
	float  pitch01    = SGT_GetPitch01(rayDir, up, altitude01);
	float  sun01      = SGT_GetPitch01(sunDir, up, altitude01);
	
	pitch01 += (dither - 0.5f) / 128.0f;
	sun01   += (dither - 0.5f) /  64.0f;
	
	return float3(pitch01, altitude01, sun01);
}

float2 SGT_GetDensity2(float3 pos, float weightMul)
{
	float alt01 = saturate((1.0f - length(pos)) * _SGT_SkyAltitudeData.z);

	return pow(alt01, _SGT_SkyAltitudeData.xy * weightMul) * _SGT_SkyDensity;
}

float SGT_GetOpacity(float3 rayPos, float3 rayDir, float rayMax, float weightMul)
{
	float2 opticalDepth = 0.0f;
	float  rayStep      = rayMax / SGT_OPACITY_STEPS;
	float  t            = rayStep * 0.5f;
	
	for (int i = 0; i < SGT_OPACITY_STEPS; i++)
	{
		float3 pos = rayPos + rayDir * t;

		opticalDepth += SGT_GetDensity2(pos, weightMul) * rayStep;
		
		t += rayStep;
	}
	
	return 1.0f - exp((opticalDepth.x + opticalDepth.y) * -20.0f);
}

float4 SGT_SampleLUT(float3 coord)
{
	return tex3Dlod(_SGT_SkyRadianceTex, float4(coord, _SGT_SkyRadianceLod));
}

float3 SGT_AtmosphereColor(float3 rayPos, float3 sunDir, float dither)
{
	float3 up         = normalize(rayPos);
	float  eyeHeight  = length(rayPos);
	float  altitude01 = saturate(SGT_InverseLerp(_SGT_SkyRadius.z, 1.0f, eyeHeight));
	float  sun01      = 0.5f + 0.5f * dot(sunDir, up);
		
	return tex2Dlod(_SGT_SkyLightingTex, float4(sun01, altitude01, 0.0f, 0.0f)).xyz;
}

float3 SGT_UnderwaterAtmosphere(float3 rayPos, float3 rayDir, float3 sunDir, float exposure, float dither)
{
	float3 coord = GetLutCoord(rayPos, rayDir, sunDir, dither); coord.x = 1.0f;
	float4 ray   = SGT_SampleLUT(coord);
	float3 mie   = SGT_CalculateMie(ray.xyz, ray.w);
	float3 color = ray.xyz + mie;
	
	#if _SSS_HDRP
		color *= exposure * 0.25f;
	#else
		color = 1.0 - exp(-color * exposure);
	#endif
	
	return saturate(_SGT_SkyColor.xyz * color * _SGT_SkyBrightness);
}

float4 SGT_Atmosphere(float3 rayPos, float3 rayDir, float rayFar, float rayMax, float3 sunDir, float exposure, float dither, float mieMul, float weightMul)
{
	// 1. Calculate opacity FIRST so we can use it for blending
	float opacity = SGT_GetOpacity(rayPos, rayDir, rayMax, weightMul);

	float3 coord  = GetLutCoord(rayPos, rayDir, sunDir, dither);
	float2 mieOff = 0.0f;
	
	if (rayMax * 1.02f <= rayFar) // If ground
	{
		// 2. Store the true, un-clamped sky coordinate
		float trueCoordX = coord.x;
		
		// 3. Calculate the clamped "below horizon" coordinate (original behavior)
		float clampedCoordX = min(coord.x, 0.5f - (1.0f / 128.0f));
		clampedCoordX = lerp(0.5f - (1.0f / 128.0f), clampedCoordX, coord.y * coord.y);
		
		// 4. Smoothly blend towards the true sky color as fog density/opacity increases
		coord.x = lerp(clampedCoordX, trueCoordX, opacity);
		mieOff = lerp(0.2f, 0.0f, opacity);
	}
	else
	{
		//coord.x = max(coord.x, 0.5f + (1.0f / 128.0f));
	}
	
	float4 ray    = SGT_SampleLUT(coord);
	float3 mie    = SGT_CalculateMie(ray.xyz, ray.w);
	float2 g      = lerp(_SGT_SkyMieData.xy, 0.0f, mieOff);
	float  c      = dot(rayDir, sunDir);
	float  phaseR = 3.0f / (16.0f * 3.141592653f) * (1.0f + c * c);
	float2 phaseM = 3.0f / (8.0f * 3.141592653f) * ((1.0f - g * g) * (c * c + 1.0f)) / (pow(abs(1.0f + g * g - 2.0f * c * g), 1.5f) * (2.0f + g * g));
	float3 color  = ray.xyz * phaseR + mie * dot(phaseM, 1) * mieMul;
	
	#if _SSS_HDRP
		color *= exposure * 0.25f;
	#else
		color = 1.0 - exp(-color * exposure);
	#endif
	
	#if _SSS_HDRP
		color += _SGT_AmbientColor.xyz * _SGT_SkyAmbientTint.xyz * 0.25;
	#else
		color += _SGT_AmbientColor.xyz * _SGT_SkyAmbientTint.xyz;
	#endif

	#if _SGT_SOLID_CORE_OFF // If the atmosphere below InnerMeshRadius isn't solid, then continue to darken because the LUT doesn't extend any farther
		float eyeHeight     = length(rayPos);
		float inner         = _SGT_SkyRadius.z;
		float interiorDepth = saturate((_SGT_SkyRadius.z - eyeHeight) / _SGT_SkyRadius.z);

		if (interiorDepth > 0.0)
		{
			float extinction = exp(-interiorDepth * 20.0 * _SGT_SkyDensity);

			color *= extinction;
			opacity = lerp(opacity, 1.0f, saturate(interiorDepth * 100.0f));
		}
	#endif
	
	return float4(saturate(_SGT_SkyColor.xyz * color * _SGT_SkyBrightness), opacity);
}

// Global cached light properties
float3 _SGT_CachedLightDirection;
float3 _SGT_CachedLightColor;

float3 SGT_SampleSkyReflection(float3 ocam, float3 odir)
{
    // 2. Intersect with the Atmosphere Shell (Outer radius is 1.0 in sky space)
    float3 odst = SGT_SphereTest(ocam, odir, 1.0f);
    odst.xy = max(odst.xy, 0.0f); // Ensure distances are ahead of ray
    
    float3 rayPos = ocam + odir * odst.x;
    float3 rayDir = odir;
    
    // 3. Determine Atmosphere Limits
    float rayFar = SGT_SphereTest(rayPos, rayDir, 1.0f).y; // Distance to sky exit
    float rayMax = odst.y - odst.x;                       // Max scattering distance
    
    // Optional: Intersect with the planet core so reflections don't "see through" the world
    #if _SGT_SOLID_CORE
        float3 ihit = SGT_SphereTest(ocam, odir, _SGT_SkyRadius.w);
        if (ihit.z > 0.0f && ihit.x > 0.0f) {
            rayMax = min(ihit.x - odst.x, rayMax);
        }
    #endif

    // 4. Calculate Sun Direction in Sky Space
    float3 sunDir = normalize(mul(_SGT_WorldToSky, float4(_SGT_CachedLightDirection, 0.0f)).xyz);
    
    // 5. Sample the Atmosphere
    // dither is set to 0.0 to keep reflections smooth; mieMul is 1.0
    float4 sky = SGT_Atmosphere(rayPos, rayDir, rayFar, rayMax, sunDir, _SGT_SkyExposure, 0.0, 1.0, 1.0);
    
    // 6. Apply cached light intensity
    return sky.rgb * _SGT_CachedLightColor;
}

float4 SGT_SimpleAtmosphere(float3 worldPos, float3 worldHit, float mieMul, float weightMul)
{
	float dither   = 0.5f;
	
	float3 ocam = mul(_SGT_WorldToSky, float4(worldPos, 1.0f)).xyz;
	float3 ofar = mul(_SGT_WorldToSky, float4(worldHit, 1.0f)).xyz;
	float  omax = distance(ocam, ofar);
	float3 odir = normalize(ofar - ocam);
	float3 odst = SGT_SphereTest(ocam, odir, 1.0f);
	
	odst.xy = max(odst.xy, 0.0f);
	
	float3 rayPos = ocam + odir * odst.x;
	float3 rayDir = odir;
	float  rayFar = SGT_SphereTest(rayPos, rayDir, 1.0f).y;
	float  rayMax = min(odst.y, omax) - odst.x;
	
	#if _SGT_LIGHTING
		float3 sunDir = normalize(mul(_SGT_WorldToSky, float4(_SGT_LightDirection[0].xyz, 0.0f)).xyz);
	#else
		float3 sunDir = normalize(rayPos);
	#endif
	
	return SGT_Atmosphere(rayPos, rayDir, rayFar, rayMax, sunDir, _SGT_SkyExposure, dither, mieMul, weightMul);
}

float3 CW_Depth2World(float viewDepth, float3 viewDir)
{
	return _WorldSpaceCameraPos + viewDir * viewDepth;
}
	
bool CW_DepthInBounds(float viewDepth, float3 viewDir)
{
	float3 wPos = CW_Depth2World(viewDepth, viewDir);
	float3 oPos = mul(_SGT_WorldToSky, float4(wPos, 1.0f)).xyz;
		
	return length(oPos) < 1.2f;
}
	
void CW_SampleVolumetricsDefault(float2 screenUV, float3 viewDir, out float4 outColor, out float outDepth)
{
	float4 centerColor = _SGT_Volumetrics_ColorTex.Sample(my_linear_clamp_sampler, screenUV);
	float  centerDepth = _SGT_Volumetrics_DepthTex.Sample(my_linear_clamp_sampler, screenUV).x;
		
	if (CW_DepthInBounds(centerDepth, viewDir) == true)
	{
		outColor = centerColor;
		outDepth = centerDepth;
	}
	else
	{
		outColor = 0;
		outDepth = 0;
	}
}

void CW_SampleVolumetricsDefault2(float2 screenUV, float3 viewDir, out float4 outColor, out float outDepth)
{
	outColor = _SGT_Volumetrics_ColorTex.Sample(my_linear_clamp_sampler, screenUV);
	outDepth = _SGT_Volumetrics_DepthTex.Sample(my_linear_clamp_sampler, screenUV).x;
}
	
void CW_SampleVolumetrics(float2 screenUV, float3 viewDir, float sceneDepth, out float4 outColor, out float outDepth)
{
	outColor = 0;
	outDepth = 0;
	//CW_SampleVolumetricsDefault(screenUV, viewDir, outColor, outDepth);return;
		
	if (_SGT_Volumetrics_Downscale > 1)
	{
		float2 pixelF   = screenUV * _SGT_Volumetrics_ColorSize.xy - 0.5f;
		float2 pixelI   = floor(pixelF);
		float2 snappedF = pixelF - pixelI;
			
		float2 coord0 = (pixelI + float2(0.5f, 0.5f)) * _SGT_Volumetrics_ColorSize.zw;
		float2 coord1 = (pixelI + float2(1.5f, 0.5f)) * _SGT_Volumetrics_ColorSize.zw;
		float2 coord2 = (pixelI + float2(0.5f, 1.5f)) * _SGT_Volumetrics_ColorSize.zw;
		float2 coord3 = (pixelI + float2(1.5f, 1.5f)) * _SGT_Volumetrics_ColorSize.zw;
			
		float depth0 = _SGT_Volumetrics_DepthTex.Sample(my_point_clamp_sampler, coord0).x;
		float depth1 = _SGT_Volumetrics_DepthTex.Sample(my_point_clamp_sampler, coord1).x;
		float depth2 = _SGT_Volumetrics_DepthTex.Sample(my_point_clamp_sampler, coord2).x;
		float depth3 = _SGT_Volumetrics_DepthTex.Sample(my_point_clamp_sampler, coord3).x;
			
		float4 color0 = _SGT_Volumetrics_ColorTex.Sample(my_point_clamp_sampler, coord0);
		float4 color1 = _SGT_Volumetrics_ColorTex.Sample(my_point_clamp_sampler, coord1);
		float4 color2 = _SGT_Volumetrics_ColorTex.Sample(my_point_clamp_sampler, coord2);
		float4 color3 = _SGT_Volumetrics_ColorTex.Sample(my_point_clamp_sampler, coord3);
			
		float w0 = CW_DepthInBounds(depth0, viewDir) * (depth0 < sceneDepth) * color0.w * (1.0 - snappedF.x) * (1.0 - snappedF.y);
		float w1 = CW_DepthInBounds(depth1, viewDir) * (depth1 < sceneDepth) * color1.w * snappedF.x * (1.0 - snappedF.y);
		float w2 = CW_DepthInBounds(depth2, viewDir) * (depth2 < sceneDepth) * color2.w * (1.0 - snappedF.x) * snappedF.y;
		float w3 = CW_DepthInBounds(depth3, viewDir) * (depth3 < sceneDepth) * color3.w * snappedF.x * snappedF.y;
			
		float wt = w0 + w1 + w2 + w3;
			
		if (wt > 0.0f)
		{
			outColor = (color0 * w0 + color1 * w1 + color2 * w2 + color3 * w3) / wt;
			outDepth = (depth0 * w0 + depth1 * w1 + depth2 * w2 + depth3 * w3) / wt;
		}
		else
		{
			return;
		}
	}
	else
	{
		float2 coord0 = screenUV;
		float  depth0 = _SGT_Volumetrics_DepthTex.Sample(my_point_clamp_sampler, coord0).x;
		float4 color0 = _SGT_Volumetrics_ColorTex.Sample(my_point_clamp_sampler, coord0);
		float  w0     = CW_DepthInBounds(depth0, viewDir) * (depth0 < sceneDepth);
		
		if (w0 > 0.0f)
		{
			outColor = color0;
			outDepth = depth0;
		}
		else
		{
			return;
		}
	}
}






#pragma instancing_options procedural:SetupInstancing
#if _SSS_PASS_SHADOWCASTER || _SSS_PASS_META
	#pragma multi_compile_instancing // For some reason the ShadowCaster and Meta pass in BIRP doesn't have this line
#endif


#define BATCH_CAPACITY 35
#define VERTEX_COUNT 243

float4 _CwSize;
float4 _CwAtlas;
float4 _CwWeights[VERTEX_COUNT];
float4 _CwCoords[VERTEX_COUNT];

float4 _CwOrigins[BATCH_CAPACITY];
float4 _CwPositionsA[BATCH_CAPACITY];
float4 _CwPositionsB[BATCH_CAPACITY];
float4 _CwPositionsC[BATCH_CAPACITY];

sampler2D DataP;
sampler2D DataA;
sampler2D DataN;

float3 _CwOffset;

float4x4 _CwObjectToWorld;
float4x4 _CwWorldToObject;

float _SGT_Occlusion;

// CLOUDS
sampler2D _SGT_CloudShadowTex;
float3    _SGT_CloudShadowDirection;
float4    _SGT_CloudShadowOpacity;
float4x4  _SGT_CloudShadowMatrix;

// OCEAN FADE
float  _SGT_OceanFade;
float2 _SGT_OceanDensity;
float3 _SGT_OceanColor;
float  _SGT_OceanSmoothness;
float4 _SGT_OceanLightDirection;

float _SGT_OceanRadius;

void SetupInstancing()
{
	#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
		#ifdef unity_ObjectToWorld
			#undef unity_ObjectToWorld
		#endif

		#ifdef unity_WorldToObject
			#undef unity_WorldToObject
		#endif
		
		unity_ObjectToWorld = _CwObjectToWorld;
		unity_WorldToObject = _CwWorldToObject;
	#endif
}

float SGT_SampleCloudDensity(float3 wpos)
{
	float3 cpos  = mul(_SGT_CloudShadowMatrix, float4(wpos, 1.0)).xyz;
	float3 chit  = SGT_SphereTest(cpos, _SGT_CloudShadowDirection, 1.0);
	float2 uv    = SGT_DirectionToEquirectangular(normalize(cpos + chit.y * _SGT_CloudShadowDirection));
	
	return saturate(dot(tex2D(_SGT_CloudShadowTex, uv), _SGT_CloudShadowOpacity));
}

void SSS_Vert(inout SSS_VertexData v)
{
	float vertexIndex = v.position.x;
	float squareIndex = v.position.y;
	float batchIndex  = v.instanceID;
	
	float  batchCol = batchIndex % _CwAtlas.z;
	float  batchRow = floor(batchIndex / _CwAtlas.z);
	float3 origin   = _CwOrigins[batchIndex].xyz;
	float3 weights  = _CwWeights[vertexIndex].xyz;
	float2 coord    = _CwCoords[vertexIndex].xy; coord.x /= 3.0;
	float3 position = _CwPositionsA[batchIndex].xyz * weights.x + _CwPositionsB[batchIndex].xyz * weights.y + _CwPositionsC[batchIndex].xyz * weights.z;
	
	v.position.xyz = _CwOffset + origin + tex2Dlod(DataP, float4(vertexIndex * _CwSize.x, batchIndex * _CwSize.y, 0.0, 0.0)).xyz;
	
	float3 ocam = mul(_CwWorldToObject, float4(_WorldSpaceCameraPos - _CwOffset, 1.0)).xyz;
	
	#if _SGT_SHAPE_SQUARE
		v.normal  = float3(0,1,0);
		v.tangent = float4(1,0,0,-1).xyz;
		
		v.extraV2F0.w = ocam.y;
	#elif _SGT_SHAPE_SPHERE
		v.normal = normalize(position);
		
		if (position.x == 0.0 && position.z == 0.0)
		{
			position.xz += 0.00000001;
		}
		
		v.tangent = float4(normalize(cross(float3(0,1,0), normalize(position))), -1.0).xyz;
		
		v.extraV2F0.w = length(ocam) - _SGT_OceanRadius;
	#endif
	
	#if _SGT_OCEAN_FADE
		if (_SGT_OceanFade > 0.0 && length(v.position.xyz - _CwOffset) < _SGT_OceanRadius)
		{
			float3 N = normalize(position); // sphere normal
			float3 V = normalize(_WorldSpaceCameraPos - v.position.xyz);
			float  F = pow(abs(1.0 - saturate(dot(N, V))), 2.0);

			float3 oceanPos = _CwOffset + N * _SGT_OceanRadius;
			v.position.xyz = lerp(v.position.xyz, oceanPos, F);
		}

	#endif
	
	// Calc UV
	float2 pixelS = _CwAtlas.xy * _CwAtlas.zw;
	float  pixelX = batchCol * _CwAtlas.x + coord.x * (_CwAtlas.x - 3.0) + 0.5 + squareIndex * (_CwAtlas.x / 3.0);
	float  pixelY = batchRow * _CwAtlas.y + coord.y * (_CwAtlas.y - 1.0) + 0.5;
	
	v.extraV2F0.x = pixelX / pixelS.x;
	v.extraV2F0.y = pixelY / pixelS.y;
	
	#if CW_COMPRESS_POSITIONS
		float3 wpos = mul(_CwObjectToWorld, float4(v.position, 1.0)).xyz;
		v.extraV2F0.z = distance(_WorldSpaceCameraPos, wpos);
		wpos = SSS_CompressWorld(wpos);
		v.position = mul(_CwWorldToObject, float4(wpos, 1.0)).xyz;
	#endif
}

float3 SGT_GetColor(float3 wnormal, float3 wlight)
{
	return saturate(dot(wnormal, wlight));
}

static const float WATER_DEPTH_SHALLOW = 30.0;
static const float WATER_DEPTH_MAX = 3000.0;

float SGT_DecodeWaterDepth(float encoded)
{
	float shallow = encoded * (WATER_DEPTH_SHALLOW * 2.0);
    float deep = WATER_DEPTH_SHALLOW + (encoded - 0.5) * ((WATER_DEPTH_MAX - WATER_DEPTH_SHALLOW) * 2.0);
    float isDeep = step(0.5, encoded);
    return lerp(shallow, deep, isDeep);
}

float3 SGT_ApplyOceanColor(
    float3 terrainColor,
    float  waterDepth01,
    float3 waterColor,
    float2 waterDensity,
    float  waterBlend,
    float3 ambientColor)
{
    float depth = SGT_DecodeWaterDepth(waterDepth01);
    float3 w = float3(1.0, 0.2, 0.1);

	float3 e0 = w * waterDensity.x;
	float3 e1 = w * waterDensity.y;

	float3 floorT0 = exp(-e0 * depth);
	float3 floorT1 = exp(-e1 * depth);
	
	float3 volumeT0 = floorT0 * floorT0 * floorT0;
	float3 volumeT1 = floorT1 * floorT1 * floorT1;

	float3 scatter = waterColor * ambientColor;

	float3 col0 = terrainColor * floorT0 + scatter * (1.0 - volumeT0);
	float3 col1 = terrainColor * floorT1 + scatter * (1.0 - volumeT1);

	return lerp(col0, col1, waterBlend);
}

float SGT_Bayer4x4(float2 screenPos)
{
	uint2 p = uint2(screenPos * _ScreenParams.xy) % 4;
	float bayer[16] = { 0,  8,  2, 10, 12,  4, 14,  6, 3, 11,  1,  9, 15,  7, 13,  5 };
	return (bayer[p.x + p.y * 4] + 0.5) / 16.0;
}

float SGT_Fresnel(float3 dir, float3 normal, float airIOR, float waterIOR)
{
	float cosTheta = clamp(dot(dir, normal), 0.0, 1.0);
	float F0       = pow(abs((waterIOR / airIOR - 1.0) / (waterIOR / airIOR + 1.0)), 2.0);

	return F0 + (1.0 - F0) * pow(abs(1.0 - cosTheta), 5.0);
}

void SSS_Frag(inout SSS_SurfaceData o, inout SSS_FragmentData d)
{
	#if CW_COMPRESS_POSITIONS
		d.worldSpacePosition = _WorldSpaceCameraPos - d.worldSpaceViewDir * d.extraV2F0.z;
	#endif
	
	float4 dataA = tex2Dlod(DataA, float4(d.extraV2F0.xy,0,0));
	float4 dataN = tex2Dlod(DataN, float4(d.extraV2F0.xy,0,0)); 
	float2 tnorm = dataN.xy * 2.0 - 1.0;
	
	tnorm.x = -tnorm.x; // Don't set negative tangent sign, so do it manually
	
	o.Albedo     = dataA.xyz;
	o.Occlusion  = _SGT_Occlusion;
	o.Emission   = dataA.xyz * dataN.z;
	o.Smoothness = dataN.w;
	o.Metallic   = 0.0;
	o.Normal     = float3(tnorm, sqrt(1.0 - saturate(dot(tnorm, tnorm))));
	o.Alpha      = dataN.x != 0.0 && dataN.y != 0.0;
	
	float localEyeHeight = d.extraV2F0.w;
	
	#if _SGT_OCEAN_FADE
		float  fadeTransition = step(SGT_Bayer4x4(d.screenUV), _SGT_OceanFade);
		float  cameraDistance = distance(d.worldSpacePosition, _WorldSpaceCameraPos);
		float  cameraDist01   = 1.0 - pow(abs(saturate(1.0 - cameraDistance / _SGT_OceanRadius)), 8.0);
		float3 oceanColor     = SGT_ApplyOceanColor(o.Albedo, dataA.w, _SGT_OceanColor, _SGT_OceanDensity, cameraDist01, 1.0);
		float  fade           = saturate(dataA.w > 0) * fadeTransition;

		o.Albedo     = lerp(o.Albedo, oceanColor, fade);
		o.Smoothness = lerp(o.Smoothness, _SGT_OceanSmoothness, fade);
		o.Normal     = lerp(o.Normal, float3(0.0, 0.0, 1.0), fade);
		o.Occlusion  = lerp(o.Occlusion, 0.15, fade);

		#if _SGT_SKY
			float  airIOR   = 1.0;
			float  waterIOR = 1.33;
			float3 opos     = mul(_SGT_WorldToSky, float4(d.worldSpacePosition, 1.0)).xyz;
			float3 onrm     = normalize(opos);
			float3 oview    = normalize(mul(_SGT_WorldToSky, float4(d.worldSpaceViewDir, 0.0)).xyz);
			float3 odir     = reflect(oview, onrm);
	
			o.Emission = SGT_SampleSkyReflection(opos, -odir) * fade * SGT_Fresnel(oview, onrm, airIOR, waterIOR);
			#if _SSS_HDRP
				o.Emission *= 120000;
			#endif
		#endif
		//o.Emission +=  _SGT_AmbientColor.xyz * 0.02;
	#endif
	
	#if _SGT_CLOUDS
		float shadow = SGT_SampleCloudDensity(d.worldSpacePosition);
		
		o.Albedo    = lerp(o.Albedo   , 0.0, shadow);
		o.Occlusion = lerp(o.Occlusion, 0.0, shadow);
	#endif
}


void Vert_float
	(
	float  iInstanceID,
	float3 iPosition,
	float3 iNormal,
	float3 iTangent,
	float4 iColor,
	float4 iTexcoord0,
	float4 iTexcoord1,
	float4 iTexcoord2,
	float4 iTexcoord3,

	out float3 oPosition,
	out float3 oNormal,
	out float3 oTangent,
	out float4 oExtraV2F0,
	out float4 oExtraV2F1,
	out float4 oExtraV2F2,
	out float4 oExtraV2F3,
	out float4 oExtraV2F4,
	out float4 oExtraV2F5,
	out float4 oExtraV2F6,
	out float4 oExtraV2F7
	)
{
	SSS_VertexData v = (SSS_VertexData)0;
	
	v.instanceID = iInstanceID;
	v.position   = iPosition;
	v.normal     = iNormal;
	v.tangent    = iTangent;
	v.color      = iColor;
	v.texcoord0  = iTexcoord0;
	v.texcoord1  = iTexcoord1;
	v.texcoord2  = iTexcoord2;
	v.texcoord3  = iTexcoord3;
	v.extraV2F0  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F1  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F2  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F3  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F4  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F5  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F6  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F7  = float4(0.0, 0.0, 0.0, 0.0);
	
	SSS_Vert(v);
	
	oPosition  = v.position;
	oNormal    = v.normal;
	oTangent   = v.tangent;
	oExtraV2F0 = v.extraV2F0;
	oExtraV2F1 = v.extraV2F1;
	oExtraV2F2 = v.extraV2F2;
	oExtraV2F3 = v.extraV2F3;
	oExtraV2F4 = v.extraV2F4;
	oExtraV2F5 = v.extraV2F5;
	oExtraV2F6 = v.extraV2F6;
	oExtraV2F7 = v.extraV2F7;
}

void Frag_float
	(
	inout float3 iPosition,
	inout float3 iNormal,
	inout float3 iTangent,
	bool   iIsFrontFace,
	float4 iColor,
	float4 iTexcoord0,
	float4 iTexcoord1,
	float4 iTexcoord2,
	float4 iTexcoord3,
	float4 iExtraV2F0,
	float4 iExtraV2F1,
	float4 iExtraV2F2,
	float4 iExtraV2F3,
	float4 iExtraV2F4,
	float4 iExtraV2F5,
	float4 iExtraV2F6,
	float4 iExtraV2F7,

	out float4x4 oExtra,
	out float3   oAlbedo,
	out float    oSmoothness,
	out float3   oNormal,
	out float3   oEmission,
	out float    oOcclusion,
	out float    oMetallic,
	out float    oAlpha
	)
{
	SSS_SurfaceData  s = (SSS_SurfaceData)0;
	SSS_FragmentData d = (SSS_FragmentData)0;
	
	s.Albedo = 1.0;
	s.Smoothness = 0.5;
	s.Normal = float3(0.0, 0.0, 1.0);
	s.Emission = float3(0.0, 0.0, 0.0);
	s.Occlusion = 0.0;
	s.Metallic = 0.0;
	s.Alpha = 1.0;
	
	iPosition = SSS_WorldToAbsolute(iPosition);
	
	d.localSpacePosition = SSS_WorldToObject(iPosition);
	d.localSpaceNormal   = normalize(SSS_WorldToObjectDir(iNormal));
	d.localSpaceTangent  = normalize(SSS_WorldToObjectDir(iTangent));
	
	d.worldSpacePosition = iPosition;
	d.worldSpaceNormal   = iNormal;
	d.worldSpaceTangent  = iTangent;
	//d.tangentSign;
	
	d.worldSpaceViewDir  = normalize(_WorldSpaceCameraPos - d.worldSpacePosition);
	//d.tangentSpaceViewDir;
	
	d.texcoord0 = iTexcoord0;
	d.texcoord1 = iTexcoord1;
	d.texcoord2 = iTexcoord2;
	d.texcoord3 = iTexcoord3;
	
	d.screenPos = float4(SSS_WorldToScreen(iPosition), 1.0);
	d.screenUV  = d.screenPos.xy;

	d.vertexColor = iColor;
	d.isFrontFace = iIsFrontFace;
	
	d.extraV2F0 = iExtraV2F0;
	d.extraV2F1 = iExtraV2F1;
	d.extraV2F2 = iExtraV2F2;
	d.extraV2F3 = iExtraV2F3;
	d.extraV2F4 = iExtraV2F4;
	d.extraV2F5 = iExtraV2F5;
	d.extraV2F6 = iExtraV2F6;
	d.extraV2F7 = iExtraV2F7;

	d.TBNMatrix = float3x3(d.worldSpaceTangent, normalize(cross(d.worldSpaceNormal, d.worldSpaceTangent)), d.worldSpaceNormal);
	
	SSS_Frag(s, d);
	
	iPosition = SSS_AbsoluteToWorld(d.worldSpacePosition); iNormal = d.worldSpaceNormal; iTangent = d.worldSpaceTangent; // Write back
	
	oExtra      = float4x4(d.extraV2F0, d.extraV2F1, d.extraV2F2, d.extraV2F3);
	oAlbedo     = s.Albedo;
	oSmoothness = s.Smoothness;
	oNormal     = s.Normal;
	oEmission   = s.Emission;
	oOcclusion  = s.Occlusion;
	oMetallic   = s.Metallic;
	oAlpha      = s.Alpha;
}



	#pragma shader_feature_local _SGT_SHAPE_SQUARE _SGT_SHAPE_SPHERE
	#pragma shader_feature_local _SGT_CLOUDS_OFF _SGT_CLOUDS
	#pragma shader_feature_local _SGT_OCEAN_FADE_OFF _SGT_OCEAN_FADE
	#pragma shader_feature_local _SGT_SKY_OFF _SGT_SKY
	#pragma multi_compile __ CW_COMPRESS_POSITIONS



// Graph Functions
// GraphFunctions: <None>

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
float4 extraV2F0;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
float4 _UV_ccc703dc3bdd420e8a8a4aa0607d4ec4_Out_0_Vector4 = IN.uv0;
float4 _UV_46571950324b44e8bf8895f08abd191f_Out_0_Vector4 = IN.uv1;
float4 _UV_ba4ef72275334f08b9b01fedf9da0065_Out_0_Vector4 = IN.uv2;
float4 _UV_255ab3a515d14af686e70c40d04416e6_Out_0_Vector4 = IN.uv3;
float3 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oPosition_0_Vector3;
float3 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oNormal_5_Vector3;
float3 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oTangent_6_Vector3;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F0_7_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F1_8_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F2_13_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F3_14_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F4_15_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F5_16_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F6_17_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F7_18_Vector4;
Vert_float(IN.InstanceID, IN.ObjectSpacePosition, IN.ObjectSpaceNormal, IN.ObjectSpaceTangent, IN.VertexColor, _UV_ccc703dc3bdd420e8a8a4aa0607d4ec4_Out_0_Vector4, _UV_46571950324b44e8bf8895f08abd191f_Out_0_Vector4, _UV_ba4ef72275334f08b9b01fedf9da0065_Out_0_Vector4, _UV_255ab3a515d14af686e70c40d04416e6_Out_0_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oPosition_0_Vector3, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oNormal_5_Vector3, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oTangent_6_Vector3, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F0_7_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F1_8_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F2_13_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F3_14_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F4_15_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F5_16_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F6_17_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F7_18_Vector4);
description.Position = _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oPosition_0_Vector3;
description.Normal = _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oNormal_5_Vector3;
description.Tangent = _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oTangent_6_Vector3;
description.extraV2F0 = _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F0_7_Vector4;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
output.extraV2F0 = input.extraV2F0;
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
float3 BaseColor;
float3 NormalTS;
float3 Emission;
float Metallic;
float Smoothness;
float Occlusion;
float Alpha;
float AlphaClipThreshold;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
float _IsFrontFace_5b03f99e2e7841c3a7d2ae306590b576_Out_0_Boolean = max(0, IN.FaceSign.x);
float4 _UV_c1e39353e82e434da821b9bdc4338e4b_Out_0_Vector4 = IN.uv0;
float4 _UV_64d51b5974bc4ecf849e7307e6de2727_Out_0_Vector4 = IN.uv1;
float4x4 _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oExtra_23_Matrix4;
float3 _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oAlbedo_0_Vector3;
float _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oSmoothness_5_Float;
float3 _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oNormal_6_Vector3;
float3 _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oEmission_7_Vector3;
float _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oOcclusion_8_Float;
float _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oMetallic_9_Float;
float _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oAlpha_10_Float;
Frag_float(IN.WorldSpacePosition, IN.WorldSpaceNormal, IN.WorldSpaceTangent, _IsFrontFace_5b03f99e2e7841c3a7d2ae306590b576_Out_0_Boolean, float4 (0, 0, 0, 0), _UV_c1e39353e82e434da821b9bdc4338e4b_Out_0_Vector4, _UV_64d51b5974bc4ecf849e7307e6de2727_Out_0_Vector4, float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), IN.extraV2F0, float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oExtra_23_Matrix4, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oAlbedo_0_Vector3, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oSmoothness_5_Float, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oNormal_6_Vector3, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oEmission_7_Vector3, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oOcclusion_8_Float, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oMetallic_9_Float, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oAlpha_10_Float);
surface.BaseColor = _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oAlbedo_0_Vector3;
surface.NormalTS = _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oNormal_6_Vector3;
surface.Emission = _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oEmission_7_Vector3;
surface.Metallic = _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oMetallic_9_Float;
surface.Smoothness = _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oSmoothness_5_Float;
surface.Occlusion = _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oOcclusion_8_Float;
surface.Alpha = _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oAlpha_10_Float;
surface.AlphaClipThreshold = float(0.5);
return surface;
}

// --------------------------------------------------
// Build Graph Inputs

VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;
    output.uv0 =                                        input.uv0;
    output.uv1 =                                        input.uv1;
    output.uv2 =                                        input.uv2;
    output.uv3 =                                        input.uv3;
    output.VertexColor =                                input.color;
#if UNITY_ANY_INSTANCING_ENABLED
    output.InstanceID =                                 unity_InstanceID;
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
    output.InstanceID =                                 input.instanceID;
#endif

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

    output.extraV2F0 = input.extraV2F0;

    // must use interpolated tangent, bitangent and normal before they are normalized in the pixel shader.
    float3 unnormalizedNormalWS = input.normalWS;
    const float renormFactor = 1.0 / length(unnormalizedNormalWS);


    output.WorldSpaceNormal = renormFactor * input.normalWS.xyz;      // we want a unit length Normal Vector node in shader graph
    output.TangentSpaceNormal = float3(0.0f, 0.0f, 1.0f);

    // to preserve mikktspace compliance we use same scale renormFactor as was used on the normal.
    // This is explained in section 2.2 in "surface gradient based bump mapping framework"
    output.WorldSpaceTangent = renormFactor * input.tangentWS.xyz;

    output.WorldSpacePosition = input.positionWS;

    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


    output.uv0 = input.texCoord0;
    output.uv1 = input.texCoord1;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
    BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

void BuildAppDataFull(Attributes attributes, VertexDescription vertexDescription, inout appdata_full result)
{
    result.vertex     = float4(attributes.positionOS, 1);
    result.tangent    = attributes.tangentOS;
    result.normal     = attributes.normalOS;
    result.texcoord   = attributes.uv0;
    result.texcoord1  = attributes.uv1;
    result.texcoord2  = attributes.uv2;
    result.texcoord3  = attributes.uv3;
    result.color      = attributes.color;
    result.vertex     = float4(vertexDescription.Position, 1);
    result.normal     = vertexDescription.Normal;
    result.tangent    = float4(vertexDescription.Tangent, 0);
    #if UNITY_ANY_INSTANCING_ENABLED
    result.instanceID = attributes.instanceID;
    #endif
}

void VaryingsToSurfaceVertex(Varyings varyings, inout v2f_surf result)
{
    result.pos = varyings.positionCS;
    result.worldPos = varyings.positionWS;
    result.worldNormal = varyings.normalWS;
    // World Tangent isn't an available input on v2f_surf

    result._ShadowCoord = varyings.shadowCoord;

    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
    #if UNITY_SHOULD_SAMPLE_SH
    #if !defined(LIGHTMAP_ON)
    result.sh = varyings.sh;
    #endif
    #endif
    #if defined(LIGHTMAP_ON)
    result.lmap.xy = varyings.lightmapUV;
    #endif
    #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        result.fogCoord = varyings.fogFactorAndVertexLight.x;
        COPY_TO_LIGHT_COORDS(result, varyings.fogFactorAndVertexLight.yzw);
    #endif

    DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(varyings, result);
}

void SurfaceVertexToVaryings(v2f_surf surfVertex, inout Varyings result)
{
    result.positionCS = surfVertex.pos;
    result.positionWS = surfVertex.worldPos;
    result.normalWS = surfVertex.worldNormal;
    // viewDirectionWS is never filled out in the legacy pass' function. Always use the value computed by SRP
    // World Tangent isn't an available input on v2f_surf
    result.shadowCoord = surfVertex._ShadowCoord;

    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
    #if UNITY_SHOULD_SAMPLE_SH
    #if !defined(LIGHTMAP_ON)
    result.sh = surfVertex.sh;
    #endif
    #endif
    #if defined(LIGHTMAP_ON)
    result.lightmapUV = surfVertex.lmap.xy;
    #endif
    #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        result.fogFactorAndVertexLight.x = surfVertex.fogCoord;
        COPY_FROM_LIGHT_COORDS(result.fogFactorAndVertexLight.yzw, surfVertex);
    #endif

    DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(surfVertex, result);
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/PBRDeferredPass.hlsl"

ENDHLSL
}
Pass
{
    Name "ShadowCaster"
    Tags
    {
        "LightMode" = "ShadowCaster"
    }

// Render State
Cull Back
Blend One Zero
ZTest LEqual
ZWrite On
ColorMask 0

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM
#define _SSS_PASS_SHADOWCASTER 1

#define _SSS_BIRP 1


// Pragmas
#pragma target 3.0
#pragma multi_compile_shadowcaster
#pragma vertex vert
#pragma fragment frag

// Keywords
#pragma multi_compile _ _CASTING_PUNCTUAL_LIGHT_SHADOW
// GraphKeywords: <None>

// Defines
#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_TEXCOORD0
#define ATTRIBUTES_NEED_TEXCOORD1
#define ATTRIBUTES_NEED_TEXCOORD2
#define ATTRIBUTES_NEED_TEXCOORD3
#define ATTRIBUTES_NEED_COLOR
#define ATTRIBUTES_NEED_INSTANCEID
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_NORMAL_WS
#define VARYINGS_NEED_TANGENT_WS
#define VARYINGS_NEED_TEXCOORD0
#define VARYINGS_NEED_TEXCOORD1
#define VARYINGS_NEED_CULLFACE
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_SHADOWCASTER
#define BUILTIN_TARGET_API 1
#define _BUILTIN_AlphaClip 1
#define _BUILTIN_ALPHATEST_ON 1
#ifdef _BUILTIN_SURFACE_TYPE_TRANSPARENT
#define _SURFACE_TYPE_TRANSPARENT _BUILTIN_SURFACE_TYPE_TRANSPARENT
#endif
#ifdef _BUILTIN_ALPHATEST_ON
#define _ALPHATEST_ON _BUILTIN_ALPHATEST_ON
#endif
#ifdef _BUILTIN_AlphaClip
#define _AlphaClip _BUILTIN_AlphaClip
#endif
#ifdef _BUILTIN_ALPHAPREMULTIPLY_ON
#define _ALPHAPREMULTIPLY_ON _BUILTIN_ALPHAPREMULTIPLY_ON
#endif


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Shim/Shims.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/LegacySurfaceVertex.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/ShaderGraphFunctions.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
 float4 uv0 : TEXCOORD0;
 float4 uv1 : TEXCOORD1;
 float4 uv2 : TEXCOORD2;
 float4 uv3 : TEXCOORD3;
 float4 color : COLOR;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float3 positionWS;
 float3 normalWS;
 float4 tangentWS;
 float4 texCoord0;
 float4 texCoord1;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
 float4 extraV2F0;
};
struct SurfaceDescriptionInputs
{
 float3 WorldSpaceNormal;
 float3 WorldSpaceTangent;
 float3 WorldSpacePosition;
 float4 uv0;
 float4 uv1;
 float FaceSign;
 float4 extraV2F0;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
 float4 uv0;
 float4 uv1;
 float4 uv2;
 float4 uv3;
 float4 VertexColor;
 uint InstanceID;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
 float4 tangentWS : INTERP0;
 float4 texCoord0 : INTERP1;
 float4 texCoord1 : INTERP2;
 float4 extraV2F0 : INTERP3;
 float3 positionWS : INTERP4;
 float3 normalWS : INTERP5;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
output.tangentWS.xyzw = input.tangentWS;
output.texCoord0.xyzw = input.texCoord0;
output.texCoord1.xyzw = input.texCoord1;
output.extraV2F0.xyzw = input.extraV2F0;
output.positionWS.xyz = input.positionWS;
output.normalWS.xyz = input.normalWS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
output.tangentWS = input.tangentWS.xyzw;
output.texCoord0 = input.texCoord0.xyzw;
output.texCoord1 = input.texCoord1.xyzw;
output.extraV2F0 = input.extraV2F0.xyzw;
output.positionWS = input.positionWS.xyz;
output.normalWS = input.normalWS.xyz;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties
CBUFFER_START(UnityPerMaterial)





CBUFFER_END


// Object and Global properties

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Includes
// UNITY_SHADER_NO_UPGRADE
float3 SSS_HClipToScreen(float4 v)
{
	float3 uv = v.xyz / v.w;
	#if UNITY_UV_STARTS_AT_TOP
		uv.y = -uv.y;
	#endif
	uv.xy = uv.xy * 0.5 + 0.5;
	return uv;
}

#if _SSS_HDRP
	float3 SSS_WorldToAbsolute(float3 v) { return GetAbsolutePositionWS(v); }
	float3 SSS_AbsoluteToWorld(float3 v) { return GetCameraRelativePositionWS(v); }
#else
	float3 SSS_WorldToAbsolute(float3 v) { return v; }
	float3 SSS_AbsoluteToWorld(float3 v) { return v; }
#endif

float3 SSS_WorldToView(float3 v) { return TransformWorldToView(SSS_AbsoluteToWorld(v)); }
float3 SSS_WorldToObject(float3 v) { return TransformWorldToObject(SSS_AbsoluteToWorld(v)); }
float3 SSS_WorldToScreen(float3 v) { return SSS_HClipToScreen(TransformWorldToHClip(SSS_AbsoluteToWorld(v))); }
float3 SSS_ObjectToScreen(float3 v) { return SSS_HClipToScreen(TransformObjectToHClip(v)); }
float3 SSS_ObjectToWorld(float3 v) { return SSS_WorldToAbsolute(TransformObjectToWorld(v)); }
float3 SSS_ObjectToView(float3 v) { return TransformWorldToView(TransformObjectToWorld(v)); }
float3 SSS_ScreenToWorld(float3 v) { return SSS_WorldToAbsolute(ComputeWorldSpacePosition(v.xy, v.z, UNITY_MATRIX_I_VP)); }
float3 SSS_ScreenToObject(float3 v) { return SSS_WorldToObject(SSS_ScreenToWorld(v)); }
float3 SSS_ScreenToView(float3 v) { return SSS_WorldToView(SSS_ScreenToWorld(v)); }
float3 SSS_ViewToWorld(float3 v) { return mul(UNITY_MATRIX_I_V, float4(v, 1.0)).xyz; }
float3 SSS_ViewToObject(float3 v) { return TransformWorldToObject(SSS_ViewToWorld(v)); }
float3 SSS_ViewToScreen(float3 v) { return SSS_HClipToScreen(TransformWViewToHClip(v)); }
float3 SSS_ObjectToWorldDir(float3 v)
{
	#if _SSS_BIRP
		return TransformObjectToWorldDir(v);
	#else
		return TransformObjectToWorldDir(v, true);
	#endif
}
float3 SSS_ObjectToViewDir(float3 v)
{
	#if _SSS_BIRP
		return TransformWorldToViewDir(TransformObjectToWorldDir(v));
	#else
		return TransformWorldToViewDir(TransformObjectToWorldDir(v, false), true);
	#endif
}
float3 SSS_WorldToObjectDir(float3 v)
{
	#if _SSS_BIRP
		return TransformWorldToObjectDir(v);
	#else
		return TransformWorldToObjectDir(v, true);
	#endif
}
float3 SSS_WorldToViewDir(float3 v)
{
	#if _SSS_BIRP
		return TransformWorldToViewDir(v);
	#else
		return TransformWorldToViewDir(v, true);
	#endif
}
float3 SSS_ViewToObjectDir(float3 v)
{
	#if _SSS_URP || _SSS_HDRP
		return SSS_WorldToObjectDir(mul(UNITY_MATRIX_I_V, float4(v, 0.0)).xyz);
	#else
		return SSS_WorldToObjectDir(mul((float3x3)UNITY_MATRIX_I_V, v));
	#endif
}
float3 SSS_ViewToWorldDir(float3 v)
{
	return mul(UNITY_MATRIX_I_V, float4(v, 0.0)).xyz;
}

#if _SSS_NO_DERIVATIVES
	float3 SSS_GetSceneColor(float2 uv) { return float3(0.0, 0.0, 0.0); }
	float3 SSS_GetSceneColorHD(float2 uv) { return SSS_GetSceneColor(uv); }
	float  SSS_GetSceneDepth(float2 uv) { return 0.0; }
#else
	#if _SSS_URP
		float3 SSS_GetSceneColor(float2 uv) { return SHADERGRAPH_SAMPLE_SCENE_COLOR(uv).xyz; }
		float3 SSS_GetSceneColorHD(float2 uv) { return SSS_GetSceneColor(uv); }
	#elif _SSS_HDRP
		float3 SSS_GetSceneColor(float2 uv) { return SHADERGRAPH_SAMPLE_SCENE_COLOR(uv).xyz; }
		float3 SSS_GetSceneColorHD(float2 uv)
		{
			#if defined(REQUIRE_OPAQUE_TEXTURE) && defined(_SURFACE_TYPE_TRANSPARENT) && defined(SHADERPASS) && (SHADERPASS != SHADERPASS_LIGHT_TRANSPORT) && (SHADERPASS != SHADERPASS_PATH_TRACING) && (SHADERPASS != SHADERPASS_RAYTRACING_VISIBILITY) && (SHADERPASS != SHADERPASS_RAYTRACING_FORWARD)
			return SampleCameraColor(uv, 0);
			#endif
			#if defined(REQUIRE_OPAQUE_TEXTURE) && defined(CUSTOM_PASS_SAMPLING_HLSL) && defined(SHADERPASS) && (SHADERPASS == SHADERPASS_DRAWPROCEDURAL || SHADERPASS == SHADERPASS_BLIT)
			return CustomPassSampleCameraColor(uv, 0);
			#endif
			return float3(0.0, 0.0, 0.0);
		}
	#else
		#if defined(UNITY_DECLARE_OPAQUE_TEXTURE_INCLUDED)
			float3 SSS_GetSceneColor(float2 uv) { return SampleSceneColor(uv); }
		#else
			sampler2D _CameraOpaqueTexture; float3 SSS_GetSceneColor(float2 uv) { return tex2D(_CameraOpaqueTexture, uv).xyz; }
		#endif
		float3 SSS_GetSceneColorHD(float2 uv) { return SSS_GetSceneColor(uv); }
	#endif

	float SSS_GetSceneDepth(float2 uv) { return SHADERGRAPH_SAMPLE_SCENE_DEPTH(uv); }
#endif

float3 SSS_GetSceneWorldPosition(float2 screenUV, float sceneDepth)
{
	#if _SSS_BIRP
		float4 clipPos  = float4(screenUV * 2.0f - 1.0f, 0.0f, 1.0f);
		float4 viewPos  = mul(unity_CameraInvProjection, clipPos);
		float3 worldDir = mul((float3x3)UNITY_MATRIX_I_V, viewPos.xyz);
					
		return _WorldSpaceCameraPos + worldDir * LinearEyeDepth(sceneDepth);
	#else
		float4 clipPos = float4(screenUV * 2.0 - 1.0, sceneDepth, 1.0);
					
		#if UNITY_UV_STARTS_AT_TOP
			clipPos.y = -clipPos.y;
		#endif
					
		float4 worldPos = mul(UNITY_MATRIX_I_VP, clipPos);
					
		worldPos.xyz /= worldPos.w;
					
		#if _SSS_HDRP
			worldPos.xyz = GetAbsolutePositionWS(worldPos.xyz);
		#endif
					
		return worldPos.xyz;
	#endif
}

float SSS_GetSceneWorldDistance(float2 screenUV, float sceneDepth)
{
	return distance(_WorldSpaceCameraPos, SSS_GetSceneWorldPosition(screenUV, sceneDepth));
}

float3 SSS_UnpackNormalScale(float4 c, float s)
{
	#if _SSS_BIRP
		return UnpackScaleNormal(c, s);
	#else
		return UnpackNormalScale(c, s);
	#endif
}

struct SSS_VertexData
{
	float  instanceID;
	float3 position;
	float3 normal;
	float3 tangent;
	float4 color;
	float4 texcoord0;
	float4 texcoord1;
	float4 texcoord2;
	float4 texcoord3;
	float4 extraV2F0;
	float4 extraV2F1;
	float4 extraV2F2;
	float4 extraV2F3;
	float4 extraV2F4;
	float4 extraV2F5;
	float4 extraV2F6;
	float4 extraV2F7;
	



};

struct SSS_FragmentData
{
	float3 localSpacePosition;
	float3 localSpaceNormal;
	float3 localSpaceTangent;
	
	float3 worldSpacePosition;
	float3 worldSpaceNormal;
	float3 worldSpaceTangent;
	//float tangentSign;

	float3 worldSpaceViewDir;
	//float3 tangentSpaceViewDir;
	
	float4 texcoord0;
	float4 texcoord1;
	float4 texcoord2;
	float4 texcoord3;
	
	float2 screenUV;
	float4 screenPos;

	float4 vertexColor;
	bool isFrontFace;
	
	float4 extraV2F0;
	float4 extraV2F1;
	float4 extraV2F2;
	float4 extraV2F3;
	float4 extraV2F4;
	float4 extraV2F5;
	float4 extraV2F6;
	float4 extraV2F7;

	float3x3 TBNMatrix;
	



};

struct SSS_SurfaceData
{
	float3 Albedo;
	float  Smoothness;
	float3 Normal;
	float3 Emission;
	float  Occlusion;
	float  Metallic;
	float  Alpha;
};

float2 _CW_PositionCompressionData; // x = start distance, y = max distance limit

#define DEBUG_SCALE 0.001
#define SSS_LOG_K          0.0001
#define SSS_MAX_WORLD_DIST 1e27

float SSS_GetCompressionRatio(float worldDist)
{
    float startDist = _CW_PositionCompressionData.x;
    float maxDist   = _CW_PositionCompressionData.y;

    // no compression nearby
    if (worldDist <= startDist)
        return 1.0;

    float excess = worldDist - startDist;

    float compressedDist =
        startDist +
        (maxDist - startDist) *
        log(1.0 + excess * SSS_LOG_K) /
        log(1.0 + SSS_MAX_WORLD_DIST * SSS_LOG_K);

    return compressedDist / worldDist;
}

float SSS_DecompressWorldDist(float compressedDist)
{
    float startDist = _CW_PositionCompressionData.x;
    float maxDist   = _CW_PositionCompressionData.y;

    if (compressedDist <= startDist)
        return compressedDist;

    float t =
        saturate(
            (compressedDist - startDist) /
            (maxDist - startDist)
        );

    return startDist +
        (
            exp(
                t *
                log(1.0 + SSS_MAX_WORLD_DIST * SSS_LOG_K)
            ) - 1.0
        ) / SSS_LOG_K;
}

float3 SSS_CompressWorld(float3 worldPos, float3 worldCenter, float worldRadius, out float ratio) 
{
    float3 worldDelta = worldPos - _WorldSpaceCameraPos;
    float  farDist    = distance(_WorldSpaceCameraPos, worldCenter) + worldRadius;
    
    ratio = SSS_GetCompressionRatio(farDist);
    
    return _WorldSpaceCameraPos + worldDelta * ratio;
}

float3 SSS_CompressWorld(float3 worldPos) 
{
    float3 worldDelta = worldPos - _WorldSpaceCameraPos;
    float  worldDist  = length(worldDelta);
    
    return _WorldSpaceCameraPos + worldDelta * SSS_GetCompressionRatio(worldDist);
}

float3 SSS_DecompressWorld(float3 worldPos)
{
    float3 worldDelta = worldPos - _WorldSpaceCameraPos;
    float  worldDist  = length(worldDelta);
    
    return worldDist > 0.001 ? _WorldSpaceCameraPos + (worldDelta / worldDist) * SSS_DecompressWorldDist(worldDist) : _WorldSpaceCameraPos;
}

float3 SSS_DecompressWorld(float3 worldPos, float ratio) 
{
    float3 worldDelta = worldPos - _WorldSpaceCameraPos;
    return _WorldSpaceCameraPos + worldDelta / ratio;
}

float SSS_ElongateDistance(float worldDist)
{
	float minDist = 10000.0;
	float maxDist = 100000.0;
	float scale   = saturate((worldDist - minDist) / (maxDist - minDist));

	return worldDist * lerp(1.0, 1.2, scale);
}
#define SGT_OPACITY_STEPS  5

float4x4        _SGT_SkyToWorld;
float4x4        _SGT_WorldToSky;
float4          _SGT_SkyColor;
float           _SGT_SkyBrightness;
float4          _SGT_SkyAmbientTint;
float           _SGT_SkyExposure;
float4          _SGT_SkyRadius;
float4          _SGT_SkyAltitudeData;
float           _SGT_SkyDensity;
float4          _SGT_SkyMieWeight;
float4          _SGT_SkyMieData;
sampler3D       _SGT_SkyRadianceTex;
float           _SGT_SkyRadianceLod;
sampler2D       _SGT_SkyLightingTex;
sampler2D       _SGT_SkyAlbedoTex;
float           _SGT_SkyRevealStars;
float           _SGT_SkyDepthOpaque;

float     _SGT_Volumetrics_Downscale; // Global
float     _SGT_Volumetrics_NearClip; // Global
Texture2D _SGT_Volumetrics_ColorTex; // Global
float4    _SGT_Volumetrics_ColorSize; // Global
Texture2D _SGT_Volumetrics_DepthTex; // Global
float2    _SGT_Volumetrics_DepthSize; // Global

SamplerState my_linear_clamp_sampler;
SamplerState my_point_clamp_sampler;

sampler2D _SGT_BlueNoiseTex; // Global
float     _SGT_Frame; // Global
float3    _SGT_AmbientColor; // Global
	
float SGT_DitherBlue(float2 screenUV)
{
	float2 pixel = floor(screenUV * _ScreenParams.xy);
	float  noise = tex2D(_SGT_BlueNoiseTex, pixel / 64.0f).r;
	return frac(noise + _SGT_Frame / sqrt(0.5f));
}

float SGT_DitherFast(float2 screenUV)
{
    float2 R2 = float2(0.7548776662, 0.5698402909);
    float2 p  = screenUV * _ScreenParams.xy + frac(_Time.y * R2) * 256.0;
    return frac(52.9829189 * frac(dot(p, float2(0.06711056, 0.00583715))));
}
	
float2 SGT_DirectionToEquirectangular(float3 dir)
{
	dir = normalize(dir);
	float u = atan2(dir.z, dir.x) / (2.0 * 3.141592653) + 0.5;
	float v = asin(clamp(dir.y, -1.0, 1.0)) / 3.141592653 + 0.5;
	return float2(u, v);
}

float3 SGT_SphereTest(float3 ray, float3 rayD, float radius)
{
	float B = -dot(ray, rayD);
	float C = dot(ray, ray) - radius * radius;
	float D = B * B - C;
	float E = sqrt(max(D, 0.0f));
	return float3(B - E, B + E, D);
}

float SGT_InverseLerp(float a, float b, float v)
{
	return (v - a) / (b - a);
}

float3 SGT_CalculateMie(float3 ray, float mie)
{
	return normalize(ray * _SGT_SkyMieWeight.xyz + 1e-6) * mie;
}

float SGT_GetPitch01(float3 rayDir, float3 up, float altitude01)
{
	float power = 2.0f;
	float eyeHeight = _SGT_SkyRadius.x + altitude01 * (_SGT_SkyRadius.y - _SGT_SkyRadius.x);
	float horizonAngleFromDown = -acos(clamp(_SGT_SkyRadius.x / max(eyeHeight, 0.00001f), -1.0f, 1.0f));
	float rayAng = asin(clamp(dot(up, rayDir), -1.0f, 1.0f));
	float sun01;

	if (rayAng < horizonAngleFromDown)
	{
		sun01 = SGT_InverseLerp(-3.141592653 * 0.5f, horizonAngleFromDown, rayAng);
		sun01 = 1.0f - pow(1.0f - sun01, 1.0f / power);
		sun01 = sun01 * 0.5f;
	}
	else
	{
		sun01 = SGT_InverseLerp(horizonAngleFromDown, 3.141592653 * 0.5f, rayAng);
		sun01 = pow(sun01, 1.0f / power);
		sun01 = sun01 * 0.5f + 0.5f;
	}

	return sun01;
}

float3 GetLutCoord(float3 rayPos, float3 rayDir, float3 sunDir, float dither)
{
	float3 up         = normalize(rayPos);
	float  eyeHeight  = length(rayPos);
	float  altitude01 = saturate(SGT_InverseLerp(_SGT_SkyRadius.z, 1.0f, eyeHeight));
	float  pitch01    = SGT_GetPitch01(rayDir, up, altitude01);
	float  sun01      = SGT_GetPitch01(sunDir, up, altitude01);
	
	pitch01 += (dither - 0.5f) / 128.0f;
	sun01   += (dither - 0.5f) /  64.0f;
	
	return float3(pitch01, altitude01, sun01);
}

float2 SGT_GetDensity2(float3 pos, float weightMul)
{
	float alt01 = saturate((1.0f - length(pos)) * _SGT_SkyAltitudeData.z);

	return pow(alt01, _SGT_SkyAltitudeData.xy * weightMul) * _SGT_SkyDensity;
}

float SGT_GetOpacity(float3 rayPos, float3 rayDir, float rayMax, float weightMul)
{
	float2 opticalDepth = 0.0f;
	float  rayStep      = rayMax / SGT_OPACITY_STEPS;
	float  t            = rayStep * 0.5f;
	
	for (int i = 0; i < SGT_OPACITY_STEPS; i++)
	{
		float3 pos = rayPos + rayDir * t;

		opticalDepth += SGT_GetDensity2(pos, weightMul) * rayStep;
		
		t += rayStep;
	}
	
	return 1.0f - exp((opticalDepth.x + opticalDepth.y) * -20.0f);
}

float4 SGT_SampleLUT(float3 coord)
{
	return tex3Dlod(_SGT_SkyRadianceTex, float4(coord, _SGT_SkyRadianceLod));
}

float3 SGT_AtmosphereColor(float3 rayPos, float3 sunDir, float dither)
{
	float3 up         = normalize(rayPos);
	float  eyeHeight  = length(rayPos);
	float  altitude01 = saturate(SGT_InverseLerp(_SGT_SkyRadius.z, 1.0f, eyeHeight));
	float  sun01      = 0.5f + 0.5f * dot(sunDir, up);
		
	return tex2Dlod(_SGT_SkyLightingTex, float4(sun01, altitude01, 0.0f, 0.0f)).xyz;
}

float3 SGT_UnderwaterAtmosphere(float3 rayPos, float3 rayDir, float3 sunDir, float exposure, float dither)
{
	float3 coord = GetLutCoord(rayPos, rayDir, sunDir, dither); coord.x = 1.0f;
	float4 ray   = SGT_SampleLUT(coord);
	float3 mie   = SGT_CalculateMie(ray.xyz, ray.w);
	float3 color = ray.xyz + mie;
	
	#if _SSS_HDRP
		color *= exposure * 0.25f;
	#else
		color = 1.0 - exp(-color * exposure);
	#endif
	
	return saturate(_SGT_SkyColor.xyz * color * _SGT_SkyBrightness);
}

float4 SGT_Atmosphere(float3 rayPos, float3 rayDir, float rayFar, float rayMax, float3 sunDir, float exposure, float dither, float mieMul, float weightMul)
{
	// 1. Calculate opacity FIRST so we can use it for blending
	float opacity = SGT_GetOpacity(rayPos, rayDir, rayMax, weightMul);

	float3 coord  = GetLutCoord(rayPos, rayDir, sunDir, dither);
	float2 mieOff = 0.0f;
	
	if (rayMax * 1.02f <= rayFar) // If ground
	{
		// 2. Store the true, un-clamped sky coordinate
		float trueCoordX = coord.x;
		
		// 3. Calculate the clamped "below horizon" coordinate (original behavior)
		float clampedCoordX = min(coord.x, 0.5f - (1.0f / 128.0f));
		clampedCoordX = lerp(0.5f - (1.0f / 128.0f), clampedCoordX, coord.y * coord.y);
		
		// 4. Smoothly blend towards the true sky color as fog density/opacity increases
		coord.x = lerp(clampedCoordX, trueCoordX, opacity);
		mieOff = lerp(0.2f, 0.0f, opacity);
	}
	else
	{
		//coord.x = max(coord.x, 0.5f + (1.0f / 128.0f));
	}
	
	float4 ray    = SGT_SampleLUT(coord);
	float3 mie    = SGT_CalculateMie(ray.xyz, ray.w);
	float2 g      = lerp(_SGT_SkyMieData.xy, 0.0f, mieOff);
	float  c      = dot(rayDir, sunDir);
	float  phaseR = 3.0f / (16.0f * 3.141592653f) * (1.0f + c * c);
	float2 phaseM = 3.0f / (8.0f * 3.141592653f) * ((1.0f - g * g) * (c * c + 1.0f)) / (pow(abs(1.0f + g * g - 2.0f * c * g), 1.5f) * (2.0f + g * g));
	float3 color  = ray.xyz * phaseR + mie * dot(phaseM, 1) * mieMul;
	
	#if _SSS_HDRP
		color *= exposure * 0.25f;
	#else
		color = 1.0 - exp(-color * exposure);
	#endif
	
	#if _SSS_HDRP
		color += _SGT_AmbientColor.xyz * _SGT_SkyAmbientTint.xyz * 0.25;
	#else
		color += _SGT_AmbientColor.xyz * _SGT_SkyAmbientTint.xyz;
	#endif

	#if _SGT_SOLID_CORE_OFF // If the atmosphere below InnerMeshRadius isn't solid, then continue to darken because the LUT doesn't extend any farther
		float eyeHeight     = length(rayPos);
		float inner         = _SGT_SkyRadius.z;
		float interiorDepth = saturate((_SGT_SkyRadius.z - eyeHeight) / _SGT_SkyRadius.z);

		if (interiorDepth > 0.0)
		{
			float extinction = exp(-interiorDepth * 20.0 * _SGT_SkyDensity);

			color *= extinction;
			opacity = lerp(opacity, 1.0f, saturate(interiorDepth * 100.0f));
		}
	#endif
	
	return float4(saturate(_SGT_SkyColor.xyz * color * _SGT_SkyBrightness), opacity);
}

// Global cached light properties
float3 _SGT_CachedLightDirection;
float3 _SGT_CachedLightColor;

float3 SGT_SampleSkyReflection(float3 ocam, float3 odir)
{
    // 2. Intersect with the Atmosphere Shell (Outer radius is 1.0 in sky space)
    float3 odst = SGT_SphereTest(ocam, odir, 1.0f);
    odst.xy = max(odst.xy, 0.0f); // Ensure distances are ahead of ray
    
    float3 rayPos = ocam + odir * odst.x;
    float3 rayDir = odir;
    
    // 3. Determine Atmosphere Limits
    float rayFar = SGT_SphereTest(rayPos, rayDir, 1.0f).y; // Distance to sky exit
    float rayMax = odst.y - odst.x;                       // Max scattering distance
    
    // Optional: Intersect with the planet core so reflections don't "see through" the world
    #if _SGT_SOLID_CORE
        float3 ihit = SGT_SphereTest(ocam, odir, _SGT_SkyRadius.w);
        if (ihit.z > 0.0f && ihit.x > 0.0f) {
            rayMax = min(ihit.x - odst.x, rayMax);
        }
    #endif

    // 4. Calculate Sun Direction in Sky Space
    float3 sunDir = normalize(mul(_SGT_WorldToSky, float4(_SGT_CachedLightDirection, 0.0f)).xyz);
    
    // 5. Sample the Atmosphere
    // dither is set to 0.0 to keep reflections smooth; mieMul is 1.0
    float4 sky = SGT_Atmosphere(rayPos, rayDir, rayFar, rayMax, sunDir, _SGT_SkyExposure, 0.0, 1.0, 1.0);
    
    // 6. Apply cached light intensity
    return sky.rgb * _SGT_CachedLightColor;
}

float4 SGT_SimpleAtmosphere(float3 worldPos, float3 worldHit, float mieMul, float weightMul)
{
	float dither   = 0.5f;
	
	float3 ocam = mul(_SGT_WorldToSky, float4(worldPos, 1.0f)).xyz;
	float3 ofar = mul(_SGT_WorldToSky, float4(worldHit, 1.0f)).xyz;
	float  omax = distance(ocam, ofar);
	float3 odir = normalize(ofar - ocam);
	float3 odst = SGT_SphereTest(ocam, odir, 1.0f);
	
	odst.xy = max(odst.xy, 0.0f);
	
	float3 rayPos = ocam + odir * odst.x;
	float3 rayDir = odir;
	float  rayFar = SGT_SphereTest(rayPos, rayDir, 1.0f).y;
	float  rayMax = min(odst.y, omax) - odst.x;
	
	#if _SGT_LIGHTING
		float3 sunDir = normalize(mul(_SGT_WorldToSky, float4(_SGT_LightDirection[0].xyz, 0.0f)).xyz);
	#else
		float3 sunDir = normalize(rayPos);
	#endif
	
	return SGT_Atmosphere(rayPos, rayDir, rayFar, rayMax, sunDir, _SGT_SkyExposure, dither, mieMul, weightMul);
}

float3 CW_Depth2World(float viewDepth, float3 viewDir)
{
	return _WorldSpaceCameraPos + viewDir * viewDepth;
}
	
bool CW_DepthInBounds(float viewDepth, float3 viewDir)
{
	float3 wPos = CW_Depth2World(viewDepth, viewDir);
	float3 oPos = mul(_SGT_WorldToSky, float4(wPos, 1.0f)).xyz;
		
	return length(oPos) < 1.2f;
}
	
void CW_SampleVolumetricsDefault(float2 screenUV, float3 viewDir, out float4 outColor, out float outDepth)
{
	float4 centerColor = _SGT_Volumetrics_ColorTex.Sample(my_linear_clamp_sampler, screenUV);
	float  centerDepth = _SGT_Volumetrics_DepthTex.Sample(my_linear_clamp_sampler, screenUV).x;
		
	if (CW_DepthInBounds(centerDepth, viewDir) == true)
	{
		outColor = centerColor;
		outDepth = centerDepth;
	}
	else
	{
		outColor = 0;
		outDepth = 0;
	}
}

void CW_SampleVolumetricsDefault2(float2 screenUV, float3 viewDir, out float4 outColor, out float outDepth)
{
	outColor = _SGT_Volumetrics_ColorTex.Sample(my_linear_clamp_sampler, screenUV);
	outDepth = _SGT_Volumetrics_DepthTex.Sample(my_linear_clamp_sampler, screenUV).x;
}
	
void CW_SampleVolumetrics(float2 screenUV, float3 viewDir, float sceneDepth, out float4 outColor, out float outDepth)
{
	outColor = 0;
	outDepth = 0;
	//CW_SampleVolumetricsDefault(screenUV, viewDir, outColor, outDepth);return;
		
	if (_SGT_Volumetrics_Downscale > 1)
	{
		float2 pixelF   = screenUV * _SGT_Volumetrics_ColorSize.xy - 0.5f;
		float2 pixelI   = floor(pixelF);
		float2 snappedF = pixelF - pixelI;
			
		float2 coord0 = (pixelI + float2(0.5f, 0.5f)) * _SGT_Volumetrics_ColorSize.zw;
		float2 coord1 = (pixelI + float2(1.5f, 0.5f)) * _SGT_Volumetrics_ColorSize.zw;
		float2 coord2 = (pixelI + float2(0.5f, 1.5f)) * _SGT_Volumetrics_ColorSize.zw;
		float2 coord3 = (pixelI + float2(1.5f, 1.5f)) * _SGT_Volumetrics_ColorSize.zw;
			
		float depth0 = _SGT_Volumetrics_DepthTex.Sample(my_point_clamp_sampler, coord0).x;
		float depth1 = _SGT_Volumetrics_DepthTex.Sample(my_point_clamp_sampler, coord1).x;
		float depth2 = _SGT_Volumetrics_DepthTex.Sample(my_point_clamp_sampler, coord2).x;
		float depth3 = _SGT_Volumetrics_DepthTex.Sample(my_point_clamp_sampler, coord3).x;
			
		float4 color0 = _SGT_Volumetrics_ColorTex.Sample(my_point_clamp_sampler, coord0);
		float4 color1 = _SGT_Volumetrics_ColorTex.Sample(my_point_clamp_sampler, coord1);
		float4 color2 = _SGT_Volumetrics_ColorTex.Sample(my_point_clamp_sampler, coord2);
		float4 color3 = _SGT_Volumetrics_ColorTex.Sample(my_point_clamp_sampler, coord3);
			
		float w0 = CW_DepthInBounds(depth0, viewDir) * (depth0 < sceneDepth) * color0.w * (1.0 - snappedF.x) * (1.0 - snappedF.y);
		float w1 = CW_DepthInBounds(depth1, viewDir) * (depth1 < sceneDepth) * color1.w * snappedF.x * (1.0 - snappedF.y);
		float w2 = CW_DepthInBounds(depth2, viewDir) * (depth2 < sceneDepth) * color2.w * (1.0 - snappedF.x) * snappedF.y;
		float w3 = CW_DepthInBounds(depth3, viewDir) * (depth3 < sceneDepth) * color3.w * snappedF.x * snappedF.y;
			
		float wt = w0 + w1 + w2 + w3;
			
		if (wt > 0.0f)
		{
			outColor = (color0 * w0 + color1 * w1 + color2 * w2 + color3 * w3) / wt;
			outDepth = (depth0 * w0 + depth1 * w1 + depth2 * w2 + depth3 * w3) / wt;
		}
		else
		{
			return;
		}
	}
	else
	{
		float2 coord0 = screenUV;
		float  depth0 = _SGT_Volumetrics_DepthTex.Sample(my_point_clamp_sampler, coord0).x;
		float4 color0 = _SGT_Volumetrics_ColorTex.Sample(my_point_clamp_sampler, coord0);
		float  w0     = CW_DepthInBounds(depth0, viewDir) * (depth0 < sceneDepth);
		
		if (w0 > 0.0f)
		{
			outColor = color0;
			outDepth = depth0;
		}
		else
		{
			return;
		}
	}
}






#pragma instancing_options procedural:SetupInstancing
#if _SSS_PASS_SHADOWCASTER || _SSS_PASS_META
	#pragma multi_compile_instancing // For some reason the ShadowCaster and Meta pass in BIRP doesn't have this line
#endif


#define BATCH_CAPACITY 35
#define VERTEX_COUNT 243

float4 _CwSize;
float4 _CwAtlas;
float4 _CwWeights[VERTEX_COUNT];
float4 _CwCoords[VERTEX_COUNT];

float4 _CwOrigins[BATCH_CAPACITY];
float4 _CwPositionsA[BATCH_CAPACITY];
float4 _CwPositionsB[BATCH_CAPACITY];
float4 _CwPositionsC[BATCH_CAPACITY];

sampler2D DataP;
sampler2D DataA;
sampler2D DataN;

float3 _CwOffset;

float4x4 _CwObjectToWorld;
float4x4 _CwWorldToObject;

float _SGT_Occlusion;

// CLOUDS
sampler2D _SGT_CloudShadowTex;
float3    _SGT_CloudShadowDirection;
float4    _SGT_CloudShadowOpacity;
float4x4  _SGT_CloudShadowMatrix;

// OCEAN FADE
float  _SGT_OceanFade;
float2 _SGT_OceanDensity;
float3 _SGT_OceanColor;
float  _SGT_OceanSmoothness;
float4 _SGT_OceanLightDirection;

float _SGT_OceanRadius;

void SetupInstancing()
{
	#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
		#ifdef unity_ObjectToWorld
			#undef unity_ObjectToWorld
		#endif

		#ifdef unity_WorldToObject
			#undef unity_WorldToObject
		#endif
		
		unity_ObjectToWorld = _CwObjectToWorld;
		unity_WorldToObject = _CwWorldToObject;
	#endif
}

float SGT_SampleCloudDensity(float3 wpos)
{
	float3 cpos  = mul(_SGT_CloudShadowMatrix, float4(wpos, 1.0)).xyz;
	float3 chit  = SGT_SphereTest(cpos, _SGT_CloudShadowDirection, 1.0);
	float2 uv    = SGT_DirectionToEquirectangular(normalize(cpos + chit.y * _SGT_CloudShadowDirection));
	
	return saturate(dot(tex2D(_SGT_CloudShadowTex, uv), _SGT_CloudShadowOpacity));
}

void SSS_Vert(inout SSS_VertexData v)
{
	float vertexIndex = v.position.x;
	float squareIndex = v.position.y;
	float batchIndex  = v.instanceID;
	
	float  batchCol = batchIndex % _CwAtlas.z;
	float  batchRow = floor(batchIndex / _CwAtlas.z);
	float3 origin   = _CwOrigins[batchIndex].xyz;
	float3 weights  = _CwWeights[vertexIndex].xyz;
	float2 coord    = _CwCoords[vertexIndex].xy; coord.x /= 3.0;
	float3 position = _CwPositionsA[batchIndex].xyz * weights.x + _CwPositionsB[batchIndex].xyz * weights.y + _CwPositionsC[batchIndex].xyz * weights.z;
	
	v.position.xyz = _CwOffset + origin + tex2Dlod(DataP, float4(vertexIndex * _CwSize.x, batchIndex * _CwSize.y, 0.0, 0.0)).xyz;
	
	float3 ocam = mul(_CwWorldToObject, float4(_WorldSpaceCameraPos - _CwOffset, 1.0)).xyz;
	
	#if _SGT_SHAPE_SQUARE
		v.normal  = float3(0,1,0);
		v.tangent = float4(1,0,0,-1).xyz;
		
		v.extraV2F0.w = ocam.y;
	#elif _SGT_SHAPE_SPHERE
		v.normal = normalize(position);
		
		if (position.x == 0.0 && position.z == 0.0)
		{
			position.xz += 0.00000001;
		}
		
		v.tangent = float4(normalize(cross(float3(0,1,0), normalize(position))), -1.0).xyz;
		
		v.extraV2F0.w = length(ocam) - _SGT_OceanRadius;
	#endif
	
	#if _SGT_OCEAN_FADE
		if (_SGT_OceanFade > 0.0 && length(v.position.xyz - _CwOffset) < _SGT_OceanRadius)
		{
			float3 N = normalize(position); // sphere normal
			float3 V = normalize(_WorldSpaceCameraPos - v.position.xyz);
			float  F = pow(abs(1.0 - saturate(dot(N, V))), 2.0);

			float3 oceanPos = _CwOffset + N * _SGT_OceanRadius;
			v.position.xyz = lerp(v.position.xyz, oceanPos, F);
		}

	#endif
	
	// Calc UV
	float2 pixelS = _CwAtlas.xy * _CwAtlas.zw;
	float  pixelX = batchCol * _CwAtlas.x + coord.x * (_CwAtlas.x - 3.0) + 0.5 + squareIndex * (_CwAtlas.x / 3.0);
	float  pixelY = batchRow * _CwAtlas.y + coord.y * (_CwAtlas.y - 1.0) + 0.5;
	
	v.extraV2F0.x = pixelX / pixelS.x;
	v.extraV2F0.y = pixelY / pixelS.y;
	
	#if CW_COMPRESS_POSITIONS
		float3 wpos = mul(_CwObjectToWorld, float4(v.position, 1.0)).xyz;
		v.extraV2F0.z = distance(_WorldSpaceCameraPos, wpos);
		wpos = SSS_CompressWorld(wpos);
		v.position = mul(_CwWorldToObject, float4(wpos, 1.0)).xyz;
	#endif
}

float3 SGT_GetColor(float3 wnormal, float3 wlight)
{
	return saturate(dot(wnormal, wlight));
}

static const float WATER_DEPTH_SHALLOW = 30.0;
static const float WATER_DEPTH_MAX = 3000.0;

float SGT_DecodeWaterDepth(float encoded)
{
	float shallow = encoded * (WATER_DEPTH_SHALLOW * 2.0);
    float deep = WATER_DEPTH_SHALLOW + (encoded - 0.5) * ((WATER_DEPTH_MAX - WATER_DEPTH_SHALLOW) * 2.0);
    float isDeep = step(0.5, encoded);
    return lerp(shallow, deep, isDeep);
}

float3 SGT_ApplyOceanColor(
    float3 terrainColor,
    float  waterDepth01,
    float3 waterColor,
    float2 waterDensity,
    float  waterBlend,
    float3 ambientColor)
{
    float depth = SGT_DecodeWaterDepth(waterDepth01);
    float3 w = float3(1.0, 0.2, 0.1);

	float3 e0 = w * waterDensity.x;
	float3 e1 = w * waterDensity.y;

	float3 floorT0 = exp(-e0 * depth);
	float3 floorT1 = exp(-e1 * depth);
	
	float3 volumeT0 = floorT0 * floorT0 * floorT0;
	float3 volumeT1 = floorT1 * floorT1 * floorT1;

	float3 scatter = waterColor * ambientColor;

	float3 col0 = terrainColor * floorT0 + scatter * (1.0 - volumeT0);
	float3 col1 = terrainColor * floorT1 + scatter * (1.0 - volumeT1);

	return lerp(col0, col1, waterBlend);
}

float SGT_Bayer4x4(float2 screenPos)
{
	uint2 p = uint2(screenPos * _ScreenParams.xy) % 4;
	float bayer[16] = { 0,  8,  2, 10, 12,  4, 14,  6, 3, 11,  1,  9, 15,  7, 13,  5 };
	return (bayer[p.x + p.y * 4] + 0.5) / 16.0;
}

float SGT_Fresnel(float3 dir, float3 normal, float airIOR, float waterIOR)
{
	float cosTheta = clamp(dot(dir, normal), 0.0, 1.0);
	float F0       = pow(abs((waterIOR / airIOR - 1.0) / (waterIOR / airIOR + 1.0)), 2.0);

	return F0 + (1.0 - F0) * pow(abs(1.0 - cosTheta), 5.0);
}

void SSS_Frag(inout SSS_SurfaceData o, inout SSS_FragmentData d)
{
	#if CW_COMPRESS_POSITIONS
		d.worldSpacePosition = _WorldSpaceCameraPos - d.worldSpaceViewDir * d.extraV2F0.z;
	#endif
	
	float4 dataA = tex2Dlod(DataA, float4(d.extraV2F0.xy,0,0));
	float4 dataN = tex2Dlod(DataN, float4(d.extraV2F0.xy,0,0)); 
	float2 tnorm = dataN.xy * 2.0 - 1.0;
	
	tnorm.x = -tnorm.x; // Don't set negative tangent sign, so do it manually
	
	o.Albedo     = dataA.xyz;
	o.Occlusion  = _SGT_Occlusion;
	o.Emission   = dataA.xyz * dataN.z;
	o.Smoothness = dataN.w;
	o.Metallic   = 0.0;
	o.Normal     = float3(tnorm, sqrt(1.0 - saturate(dot(tnorm, tnorm))));
	o.Alpha      = dataN.x != 0.0 && dataN.y != 0.0;
	
	float localEyeHeight = d.extraV2F0.w;
	
	#if _SGT_OCEAN_FADE
		float  fadeTransition = step(SGT_Bayer4x4(d.screenUV), _SGT_OceanFade);
		float  cameraDistance = distance(d.worldSpacePosition, _WorldSpaceCameraPos);
		float  cameraDist01   = 1.0 - pow(abs(saturate(1.0 - cameraDistance / _SGT_OceanRadius)), 8.0);
		float3 oceanColor     = SGT_ApplyOceanColor(o.Albedo, dataA.w, _SGT_OceanColor, _SGT_OceanDensity, cameraDist01, 1.0);
		float  fade           = saturate(dataA.w > 0) * fadeTransition;

		o.Albedo     = lerp(o.Albedo, oceanColor, fade);
		o.Smoothness = lerp(o.Smoothness, _SGT_OceanSmoothness, fade);
		o.Normal     = lerp(o.Normal, float3(0.0, 0.0, 1.0), fade);
		o.Occlusion  = lerp(o.Occlusion, 0.15, fade);

		#if _SGT_SKY
			float  airIOR   = 1.0;
			float  waterIOR = 1.33;
			float3 opos     = mul(_SGT_WorldToSky, float4(d.worldSpacePosition, 1.0)).xyz;
			float3 onrm     = normalize(opos);
			float3 oview    = normalize(mul(_SGT_WorldToSky, float4(d.worldSpaceViewDir, 0.0)).xyz);
			float3 odir     = reflect(oview, onrm);
	
			o.Emission = SGT_SampleSkyReflection(opos, -odir) * fade * SGT_Fresnel(oview, onrm, airIOR, waterIOR);
			#if _SSS_HDRP
				o.Emission *= 120000;
			#endif
		#endif
		//o.Emission +=  _SGT_AmbientColor.xyz * 0.02;
	#endif
	
	#if _SGT_CLOUDS
		float shadow = SGT_SampleCloudDensity(d.worldSpacePosition);
		
		o.Albedo    = lerp(o.Albedo   , 0.0, shadow);
		o.Occlusion = lerp(o.Occlusion, 0.0, shadow);
	#endif
}


void Vert_float
	(
	float  iInstanceID,
	float3 iPosition,
	float3 iNormal,
	float3 iTangent,
	float4 iColor,
	float4 iTexcoord0,
	float4 iTexcoord1,
	float4 iTexcoord2,
	float4 iTexcoord3,

	out float3 oPosition,
	out float3 oNormal,
	out float3 oTangent,
	out float4 oExtraV2F0,
	out float4 oExtraV2F1,
	out float4 oExtraV2F2,
	out float4 oExtraV2F3,
	out float4 oExtraV2F4,
	out float4 oExtraV2F5,
	out float4 oExtraV2F6,
	out float4 oExtraV2F7
	)
{
	SSS_VertexData v = (SSS_VertexData)0;
	
	v.instanceID = iInstanceID;
	v.position   = iPosition;
	v.normal     = iNormal;
	v.tangent    = iTangent;
	v.color      = iColor;
	v.texcoord0  = iTexcoord0;
	v.texcoord1  = iTexcoord1;
	v.texcoord2  = iTexcoord2;
	v.texcoord3  = iTexcoord3;
	v.extraV2F0  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F1  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F2  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F3  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F4  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F5  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F6  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F7  = float4(0.0, 0.0, 0.0, 0.0);
	
	SSS_Vert(v);
	
	oPosition  = v.position;
	oNormal    = v.normal;
	oTangent   = v.tangent;
	oExtraV2F0 = v.extraV2F0;
	oExtraV2F1 = v.extraV2F1;
	oExtraV2F2 = v.extraV2F2;
	oExtraV2F3 = v.extraV2F3;
	oExtraV2F4 = v.extraV2F4;
	oExtraV2F5 = v.extraV2F5;
	oExtraV2F6 = v.extraV2F6;
	oExtraV2F7 = v.extraV2F7;
}

void Frag_float
	(
	inout float3 iPosition,
	inout float3 iNormal,
	inout float3 iTangent,
	bool   iIsFrontFace,
	float4 iColor,
	float4 iTexcoord0,
	float4 iTexcoord1,
	float4 iTexcoord2,
	float4 iTexcoord3,
	float4 iExtraV2F0,
	float4 iExtraV2F1,
	float4 iExtraV2F2,
	float4 iExtraV2F3,
	float4 iExtraV2F4,
	float4 iExtraV2F5,
	float4 iExtraV2F6,
	float4 iExtraV2F7,

	out float4x4 oExtra,
	out float3   oAlbedo,
	out float    oSmoothness,
	out float3   oNormal,
	out float3   oEmission,
	out float    oOcclusion,
	out float    oMetallic,
	out float    oAlpha
	)
{
	SSS_SurfaceData  s = (SSS_SurfaceData)0;
	SSS_FragmentData d = (SSS_FragmentData)0;
	
	s.Albedo = 1.0;
	s.Smoothness = 0.5;
	s.Normal = float3(0.0, 0.0, 1.0);
	s.Emission = float3(0.0, 0.0, 0.0);
	s.Occlusion = 0.0;
	s.Metallic = 0.0;
	s.Alpha = 1.0;
	
	iPosition = SSS_WorldToAbsolute(iPosition);
	
	d.localSpacePosition = SSS_WorldToObject(iPosition);
	d.localSpaceNormal   = normalize(SSS_WorldToObjectDir(iNormal));
	d.localSpaceTangent  = normalize(SSS_WorldToObjectDir(iTangent));
	
	d.worldSpacePosition = iPosition;
	d.worldSpaceNormal   = iNormal;
	d.worldSpaceTangent  = iTangent;
	//d.tangentSign;
	
	d.worldSpaceViewDir  = normalize(_WorldSpaceCameraPos - d.worldSpacePosition);
	//d.tangentSpaceViewDir;
	
	d.texcoord0 = iTexcoord0;
	d.texcoord1 = iTexcoord1;
	d.texcoord2 = iTexcoord2;
	d.texcoord3 = iTexcoord3;
	
	d.screenPos = float4(SSS_WorldToScreen(iPosition), 1.0);
	d.screenUV  = d.screenPos.xy;

	d.vertexColor = iColor;
	d.isFrontFace = iIsFrontFace;
	
	d.extraV2F0 = iExtraV2F0;
	d.extraV2F1 = iExtraV2F1;
	d.extraV2F2 = iExtraV2F2;
	d.extraV2F3 = iExtraV2F3;
	d.extraV2F4 = iExtraV2F4;
	d.extraV2F5 = iExtraV2F5;
	d.extraV2F6 = iExtraV2F6;
	d.extraV2F7 = iExtraV2F7;

	d.TBNMatrix = float3x3(d.worldSpaceTangent, normalize(cross(d.worldSpaceNormal, d.worldSpaceTangent)), d.worldSpaceNormal);
	
	SSS_Frag(s, d);
	
	iPosition = SSS_AbsoluteToWorld(d.worldSpacePosition); iNormal = d.worldSpaceNormal; iTangent = d.worldSpaceTangent; // Write back
	
	oExtra      = float4x4(d.extraV2F0, d.extraV2F1, d.extraV2F2, d.extraV2F3);
	oAlbedo     = s.Albedo;
	oSmoothness = s.Smoothness;
	oNormal     = s.Normal;
	oEmission   = s.Emission;
	oOcclusion  = s.Occlusion;
	oMetallic   = s.Metallic;
	oAlpha      = s.Alpha;
}



	#pragma shader_feature_local _SGT_SHAPE_SQUARE _SGT_SHAPE_SPHERE
	#pragma shader_feature_local _SGT_CLOUDS_OFF _SGT_CLOUDS
	#pragma shader_feature_local _SGT_OCEAN_FADE_OFF _SGT_OCEAN_FADE
	#pragma shader_feature_local _SGT_SKY_OFF _SGT_SKY
	#pragma multi_compile __ CW_COMPRESS_POSITIONS



// Graph Functions
// GraphFunctions: <None>

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
float4 extraV2F0;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
float4 _UV_ccc703dc3bdd420e8a8a4aa0607d4ec4_Out_0_Vector4 = IN.uv0;
float4 _UV_46571950324b44e8bf8895f08abd191f_Out_0_Vector4 = IN.uv1;
float4 _UV_ba4ef72275334f08b9b01fedf9da0065_Out_0_Vector4 = IN.uv2;
float4 _UV_255ab3a515d14af686e70c40d04416e6_Out_0_Vector4 = IN.uv3;
float3 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oPosition_0_Vector3;
float3 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oNormal_5_Vector3;
float3 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oTangent_6_Vector3;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F0_7_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F1_8_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F2_13_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F3_14_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F4_15_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F5_16_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F6_17_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F7_18_Vector4;
Vert_float(IN.InstanceID, IN.ObjectSpacePosition, IN.ObjectSpaceNormal, IN.ObjectSpaceTangent, IN.VertexColor, _UV_ccc703dc3bdd420e8a8a4aa0607d4ec4_Out_0_Vector4, _UV_46571950324b44e8bf8895f08abd191f_Out_0_Vector4, _UV_ba4ef72275334f08b9b01fedf9da0065_Out_0_Vector4, _UV_255ab3a515d14af686e70c40d04416e6_Out_0_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oPosition_0_Vector3, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oNormal_5_Vector3, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oTangent_6_Vector3, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F0_7_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F1_8_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F2_13_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F3_14_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F4_15_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F5_16_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F6_17_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F7_18_Vector4);
description.Position = _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oPosition_0_Vector3;
description.Normal = _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oNormal_5_Vector3;
description.Tangent = _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oTangent_6_Vector3;
description.extraV2F0 = _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F0_7_Vector4;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
output.extraV2F0 = input.extraV2F0;
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
float Alpha;
float AlphaClipThreshold;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
float _IsFrontFace_5b03f99e2e7841c3a7d2ae306590b576_Out_0_Boolean = max(0, IN.FaceSign.x);
float4 _UV_c1e39353e82e434da821b9bdc4338e4b_Out_0_Vector4 = IN.uv0;
float4 _UV_64d51b5974bc4ecf849e7307e6de2727_Out_0_Vector4 = IN.uv1;
float4x4 _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oExtra_23_Matrix4;
float3 _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oAlbedo_0_Vector3;
float _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oSmoothness_5_Float;
float3 _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oNormal_6_Vector3;
float3 _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oEmission_7_Vector3;
float _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oOcclusion_8_Float;
float _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oMetallic_9_Float;
float _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oAlpha_10_Float;
Frag_float(IN.WorldSpacePosition, IN.WorldSpaceNormal, IN.WorldSpaceTangent, _IsFrontFace_5b03f99e2e7841c3a7d2ae306590b576_Out_0_Boolean, float4 (0, 0, 0, 0), _UV_c1e39353e82e434da821b9bdc4338e4b_Out_0_Vector4, _UV_64d51b5974bc4ecf849e7307e6de2727_Out_0_Vector4, float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), IN.extraV2F0, float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oExtra_23_Matrix4, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oAlbedo_0_Vector3, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oSmoothness_5_Float, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oNormal_6_Vector3, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oEmission_7_Vector3, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oOcclusion_8_Float, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oMetallic_9_Float, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oAlpha_10_Float);
surface.Alpha = _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oAlpha_10_Float;
surface.AlphaClipThreshold = float(0.5);
return surface;
}

// --------------------------------------------------
// Build Graph Inputs

VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;
    output.uv0 =                                        input.uv0;
    output.uv1 =                                        input.uv1;
    output.uv2 =                                        input.uv2;
    output.uv3 =                                        input.uv3;
    output.VertexColor =                                input.color;
#if UNITY_ANY_INSTANCING_ENABLED
    output.InstanceID =                                 unity_InstanceID;
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
    output.InstanceID =                                 input.instanceID;
#endif

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

    output.extraV2F0 = input.extraV2F0;

    // must use interpolated tangent, bitangent and normal before they are normalized in the pixel shader.
    float3 unnormalizedNormalWS = input.normalWS;
    const float renormFactor = 1.0 / length(unnormalizedNormalWS);


    output.WorldSpaceNormal = renormFactor * input.normalWS.xyz;      // we want a unit length Normal Vector node in shader graph

    // to preserve mikktspace compliance we use same scale renormFactor as was used on the normal.
    // This is explained in section 2.2 in "surface gradient based bump mapping framework"
    output.WorldSpaceTangent = renormFactor * input.tangentWS.xyz;

    output.WorldSpacePosition = input.positionWS;

    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


    output.uv0 = input.texCoord0;
    output.uv1 = input.texCoord1;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
    BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

void BuildAppDataFull(Attributes attributes, VertexDescription vertexDescription, inout appdata_full result)
{
    result.vertex     = float4(attributes.positionOS, 1);
    result.tangent    = attributes.tangentOS;
    result.normal     = attributes.normalOS;
    result.texcoord   = attributes.uv0;
    result.texcoord1  = attributes.uv1;
    result.texcoord2  = attributes.uv2;
    result.texcoord3  = attributes.uv3;
    result.color      = attributes.color;
    result.vertex     = float4(vertexDescription.Position, 1);
    result.normal     = vertexDescription.Normal;
    result.tangent    = float4(vertexDescription.Tangent, 0);
    #if UNITY_ANY_INSTANCING_ENABLED
    result.instanceID = attributes.instanceID;
    #endif
}

void VaryingsToSurfaceVertex(Varyings varyings, inout v2f_surf result)
{
    result.pos = varyings.positionCS;
    result.worldPos = varyings.positionWS;
    result.worldNormal = varyings.normalWS;
    // World Tangent isn't an available input on v2f_surf


    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
    #if UNITY_SHOULD_SAMPLE_SH
    #if !defined(LIGHTMAP_ON)
    #endif
    #endif
    #if defined(LIGHTMAP_ON)
    #endif
    #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        result.fogCoord = varyings.fogFactorAndVertexLight.x;
        COPY_TO_LIGHT_COORDS(result, varyings.fogFactorAndVertexLight.yzw);
    #endif

    DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(varyings, result);
}

void SurfaceVertexToVaryings(v2f_surf surfVertex, inout Varyings result)
{
    result.positionCS = surfVertex.pos;
    result.positionWS = surfVertex.worldPos;
    result.normalWS = surfVertex.worldNormal;
    // viewDirectionWS is never filled out in the legacy pass' function. Always use the value computed by SRP
    // World Tangent isn't an available input on v2f_surf

    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
    #if UNITY_SHOULD_SAMPLE_SH
    #if !defined(LIGHTMAP_ON)
    #endif
    #endif
    #if defined(LIGHTMAP_ON)
    #endif
    #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        result.fogFactorAndVertexLight.x = surfVertex.fogCoord;
        COPY_FROM_LIGHT_COORDS(result.fogFactorAndVertexLight.yzw, surfVertex);
    #endif

    DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(surfVertex, result);
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/ShadowCasterPass.hlsl"

ENDHLSL
}
Pass
{
    Name "DepthOnly"
    Tags
    {
        "LightMode" = "DepthOnly"
    }

// Render State
Cull Back
Blend One Zero
ZTest LEqual
ZWrite On
ColorMask 0

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM
#define _SSS_PASS_DEPTHONLY 1

#define _SSS_BIRP 1


// Pragmas
#pragma target 3.0
#pragma multi_compile_instancing
#pragma vertex vert
#pragma fragment frag

// Keywords
// PassKeywords: <None>
// GraphKeywords: <None>

// Defines
#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_TEXCOORD0
#define ATTRIBUTES_NEED_TEXCOORD1
#define ATTRIBUTES_NEED_TEXCOORD2
#define ATTRIBUTES_NEED_TEXCOORD3
#define ATTRIBUTES_NEED_COLOR
#define ATTRIBUTES_NEED_INSTANCEID
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_NORMAL_WS
#define VARYINGS_NEED_TANGENT_WS
#define VARYINGS_NEED_TEXCOORD0
#define VARYINGS_NEED_TEXCOORD1
#define VARYINGS_NEED_CULLFACE
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_DEPTHONLY
#define BUILTIN_TARGET_API 1
#define _BUILTIN_AlphaClip 1
#define _BUILTIN_ALPHATEST_ON 1
#ifdef _BUILTIN_SURFACE_TYPE_TRANSPARENT
#define _SURFACE_TYPE_TRANSPARENT _BUILTIN_SURFACE_TYPE_TRANSPARENT
#endif
#ifdef _BUILTIN_ALPHATEST_ON
#define _ALPHATEST_ON _BUILTIN_ALPHATEST_ON
#endif
#ifdef _BUILTIN_AlphaClip
#define _AlphaClip _BUILTIN_AlphaClip
#endif
#ifdef _BUILTIN_ALPHAPREMULTIPLY_ON
#define _ALPHAPREMULTIPLY_ON _BUILTIN_ALPHAPREMULTIPLY_ON
#endif


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Shim/Shims.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/LegacySurfaceVertex.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/ShaderGraphFunctions.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
 float4 uv0 : TEXCOORD0;
 float4 uv1 : TEXCOORD1;
 float4 uv2 : TEXCOORD2;
 float4 uv3 : TEXCOORD3;
 float4 color : COLOR;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float3 positionWS;
 float3 normalWS;
 float4 tangentWS;
 float4 texCoord0;
 float4 texCoord1;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
 float4 extraV2F0;
};
struct SurfaceDescriptionInputs
{
 float3 WorldSpaceNormal;
 float3 WorldSpaceTangent;
 float3 WorldSpacePosition;
 float4 uv0;
 float4 uv1;
 float FaceSign;
 float4 extraV2F0;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
 float4 uv0;
 float4 uv1;
 float4 uv2;
 float4 uv3;
 float4 VertexColor;
 uint InstanceID;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
 float4 tangentWS : INTERP0;
 float4 texCoord0 : INTERP1;
 float4 texCoord1 : INTERP2;
 float4 extraV2F0 : INTERP3;
 float3 positionWS : INTERP4;
 float3 normalWS : INTERP5;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
output.tangentWS.xyzw = input.tangentWS;
output.texCoord0.xyzw = input.texCoord0;
output.texCoord1.xyzw = input.texCoord1;
output.extraV2F0.xyzw = input.extraV2F0;
output.positionWS.xyz = input.positionWS;
output.normalWS.xyz = input.normalWS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
output.tangentWS = input.tangentWS.xyzw;
output.texCoord0 = input.texCoord0.xyzw;
output.texCoord1 = input.texCoord1.xyzw;
output.extraV2F0 = input.extraV2F0.xyzw;
output.positionWS = input.positionWS.xyz;
output.normalWS = input.normalWS.xyz;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties
CBUFFER_START(UnityPerMaterial)





CBUFFER_END


// Object and Global properties

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Includes
// UNITY_SHADER_NO_UPGRADE
float3 SSS_HClipToScreen(float4 v)
{
	float3 uv = v.xyz / v.w;
	#if UNITY_UV_STARTS_AT_TOP
		uv.y = -uv.y;
	#endif
	uv.xy = uv.xy * 0.5 + 0.5;
	return uv;
}

#if _SSS_HDRP
	float3 SSS_WorldToAbsolute(float3 v) { return GetAbsolutePositionWS(v); }
	float3 SSS_AbsoluteToWorld(float3 v) { return GetCameraRelativePositionWS(v); }
#else
	float3 SSS_WorldToAbsolute(float3 v) { return v; }
	float3 SSS_AbsoluteToWorld(float3 v) { return v; }
#endif

float3 SSS_WorldToView(float3 v) { return TransformWorldToView(SSS_AbsoluteToWorld(v)); }
float3 SSS_WorldToObject(float3 v) { return TransformWorldToObject(SSS_AbsoluteToWorld(v)); }
float3 SSS_WorldToScreen(float3 v) { return SSS_HClipToScreen(TransformWorldToHClip(SSS_AbsoluteToWorld(v))); }
float3 SSS_ObjectToScreen(float3 v) { return SSS_HClipToScreen(TransformObjectToHClip(v)); }
float3 SSS_ObjectToWorld(float3 v) { return SSS_WorldToAbsolute(TransformObjectToWorld(v)); }
float3 SSS_ObjectToView(float3 v) { return TransformWorldToView(TransformObjectToWorld(v)); }
float3 SSS_ScreenToWorld(float3 v) { return SSS_WorldToAbsolute(ComputeWorldSpacePosition(v.xy, v.z, UNITY_MATRIX_I_VP)); }
float3 SSS_ScreenToObject(float3 v) { return SSS_WorldToObject(SSS_ScreenToWorld(v)); }
float3 SSS_ScreenToView(float3 v) { return SSS_WorldToView(SSS_ScreenToWorld(v)); }
float3 SSS_ViewToWorld(float3 v) { return mul(UNITY_MATRIX_I_V, float4(v, 1.0)).xyz; }
float3 SSS_ViewToObject(float3 v) { return TransformWorldToObject(SSS_ViewToWorld(v)); }
float3 SSS_ViewToScreen(float3 v) { return SSS_HClipToScreen(TransformWViewToHClip(v)); }
float3 SSS_ObjectToWorldDir(float3 v)
{
	#if _SSS_BIRP
		return TransformObjectToWorldDir(v);
	#else
		return TransformObjectToWorldDir(v, true);
	#endif
}
float3 SSS_ObjectToViewDir(float3 v)
{
	#if _SSS_BIRP
		return TransformWorldToViewDir(TransformObjectToWorldDir(v));
	#else
		return TransformWorldToViewDir(TransformObjectToWorldDir(v, false), true);
	#endif
}
float3 SSS_WorldToObjectDir(float3 v)
{
	#if _SSS_BIRP
		return TransformWorldToObjectDir(v);
	#else
		return TransformWorldToObjectDir(v, true);
	#endif
}
float3 SSS_WorldToViewDir(float3 v)
{
	#if _SSS_BIRP
		return TransformWorldToViewDir(v);
	#else
		return TransformWorldToViewDir(v, true);
	#endif
}
float3 SSS_ViewToObjectDir(float3 v)
{
	#if _SSS_URP || _SSS_HDRP
		return SSS_WorldToObjectDir(mul(UNITY_MATRIX_I_V, float4(v, 0.0)).xyz);
	#else
		return SSS_WorldToObjectDir(mul((float3x3)UNITY_MATRIX_I_V, v));
	#endif
}
float3 SSS_ViewToWorldDir(float3 v)
{
	return mul(UNITY_MATRIX_I_V, float4(v, 0.0)).xyz;
}

#if _SSS_NO_DERIVATIVES
	float3 SSS_GetSceneColor(float2 uv) { return float3(0.0, 0.0, 0.0); }
	float3 SSS_GetSceneColorHD(float2 uv) { return SSS_GetSceneColor(uv); }
	float  SSS_GetSceneDepth(float2 uv) { return 0.0; }
#else
	#if _SSS_URP
		float3 SSS_GetSceneColor(float2 uv) { return SHADERGRAPH_SAMPLE_SCENE_COLOR(uv).xyz; }
		float3 SSS_GetSceneColorHD(float2 uv) { return SSS_GetSceneColor(uv); }
	#elif _SSS_HDRP
		float3 SSS_GetSceneColor(float2 uv) { return SHADERGRAPH_SAMPLE_SCENE_COLOR(uv).xyz; }
		float3 SSS_GetSceneColorHD(float2 uv)
		{
			#if defined(REQUIRE_OPAQUE_TEXTURE) && defined(_SURFACE_TYPE_TRANSPARENT) && defined(SHADERPASS) && (SHADERPASS != SHADERPASS_LIGHT_TRANSPORT) && (SHADERPASS != SHADERPASS_PATH_TRACING) && (SHADERPASS != SHADERPASS_RAYTRACING_VISIBILITY) && (SHADERPASS != SHADERPASS_RAYTRACING_FORWARD)
			return SampleCameraColor(uv, 0);
			#endif
			#if defined(REQUIRE_OPAQUE_TEXTURE) && defined(CUSTOM_PASS_SAMPLING_HLSL) && defined(SHADERPASS) && (SHADERPASS == SHADERPASS_DRAWPROCEDURAL || SHADERPASS == SHADERPASS_BLIT)
			return CustomPassSampleCameraColor(uv, 0);
			#endif
			return float3(0.0, 0.0, 0.0);
		}
	#else
		#if defined(UNITY_DECLARE_OPAQUE_TEXTURE_INCLUDED)
			float3 SSS_GetSceneColor(float2 uv) { return SampleSceneColor(uv); }
		#else
			sampler2D _CameraOpaqueTexture; float3 SSS_GetSceneColor(float2 uv) { return tex2D(_CameraOpaqueTexture, uv).xyz; }
		#endif
		float3 SSS_GetSceneColorHD(float2 uv) { return SSS_GetSceneColor(uv); }
	#endif

	float SSS_GetSceneDepth(float2 uv) { return SHADERGRAPH_SAMPLE_SCENE_DEPTH(uv); }
#endif

float3 SSS_GetSceneWorldPosition(float2 screenUV, float sceneDepth)
{
	#if _SSS_BIRP
		float4 clipPos  = float4(screenUV * 2.0f - 1.0f, 0.0f, 1.0f);
		float4 viewPos  = mul(unity_CameraInvProjection, clipPos);
		float3 worldDir = mul((float3x3)UNITY_MATRIX_I_V, viewPos.xyz);
					
		return _WorldSpaceCameraPos + worldDir * LinearEyeDepth(sceneDepth);
	#else
		float4 clipPos = float4(screenUV * 2.0 - 1.0, sceneDepth, 1.0);
					
		#if UNITY_UV_STARTS_AT_TOP
			clipPos.y = -clipPos.y;
		#endif
					
		float4 worldPos = mul(UNITY_MATRIX_I_VP, clipPos);
					
		worldPos.xyz /= worldPos.w;
					
		#if _SSS_HDRP
			worldPos.xyz = GetAbsolutePositionWS(worldPos.xyz);
		#endif
					
		return worldPos.xyz;
	#endif
}

float SSS_GetSceneWorldDistance(float2 screenUV, float sceneDepth)
{
	return distance(_WorldSpaceCameraPos, SSS_GetSceneWorldPosition(screenUV, sceneDepth));
}

float3 SSS_UnpackNormalScale(float4 c, float s)
{
	#if _SSS_BIRP
		return UnpackScaleNormal(c, s);
	#else
		return UnpackNormalScale(c, s);
	#endif
}

struct SSS_VertexData
{
	float  instanceID;
	float3 position;
	float3 normal;
	float3 tangent;
	float4 color;
	float4 texcoord0;
	float4 texcoord1;
	float4 texcoord2;
	float4 texcoord3;
	float4 extraV2F0;
	float4 extraV2F1;
	float4 extraV2F2;
	float4 extraV2F3;
	float4 extraV2F4;
	float4 extraV2F5;
	float4 extraV2F6;
	float4 extraV2F7;
	



};

struct SSS_FragmentData
{
	float3 localSpacePosition;
	float3 localSpaceNormal;
	float3 localSpaceTangent;
	
	float3 worldSpacePosition;
	float3 worldSpaceNormal;
	float3 worldSpaceTangent;
	//float tangentSign;

	float3 worldSpaceViewDir;
	//float3 tangentSpaceViewDir;
	
	float4 texcoord0;
	float4 texcoord1;
	float4 texcoord2;
	float4 texcoord3;
	
	float2 screenUV;
	float4 screenPos;

	float4 vertexColor;
	bool isFrontFace;
	
	float4 extraV2F0;
	float4 extraV2F1;
	float4 extraV2F2;
	float4 extraV2F3;
	float4 extraV2F4;
	float4 extraV2F5;
	float4 extraV2F6;
	float4 extraV2F7;

	float3x3 TBNMatrix;
	



};

struct SSS_SurfaceData
{
	float3 Albedo;
	float  Smoothness;
	float3 Normal;
	float3 Emission;
	float  Occlusion;
	float  Metallic;
	float  Alpha;
};

float2 _CW_PositionCompressionData; // x = start distance, y = max distance limit

#define DEBUG_SCALE 0.001
#define SSS_LOG_K          0.0001
#define SSS_MAX_WORLD_DIST 1e27

float SSS_GetCompressionRatio(float worldDist)
{
    float startDist = _CW_PositionCompressionData.x;
    float maxDist   = _CW_PositionCompressionData.y;

    // no compression nearby
    if (worldDist <= startDist)
        return 1.0;

    float excess = worldDist - startDist;

    float compressedDist =
        startDist +
        (maxDist - startDist) *
        log(1.0 + excess * SSS_LOG_K) /
        log(1.0 + SSS_MAX_WORLD_DIST * SSS_LOG_K);

    return compressedDist / worldDist;
}

float SSS_DecompressWorldDist(float compressedDist)
{
    float startDist = _CW_PositionCompressionData.x;
    float maxDist   = _CW_PositionCompressionData.y;

    if (compressedDist <= startDist)
        return compressedDist;

    float t =
        saturate(
            (compressedDist - startDist) /
            (maxDist - startDist)
        );

    return startDist +
        (
            exp(
                t *
                log(1.0 + SSS_MAX_WORLD_DIST * SSS_LOG_K)
            ) - 1.0
        ) / SSS_LOG_K;
}

float3 SSS_CompressWorld(float3 worldPos, float3 worldCenter, float worldRadius, out float ratio) 
{
    float3 worldDelta = worldPos - _WorldSpaceCameraPos;
    float  farDist    = distance(_WorldSpaceCameraPos, worldCenter) + worldRadius;
    
    ratio = SSS_GetCompressionRatio(farDist);
    
    return _WorldSpaceCameraPos + worldDelta * ratio;
}

float3 SSS_CompressWorld(float3 worldPos) 
{
    float3 worldDelta = worldPos - _WorldSpaceCameraPos;
    float  worldDist  = length(worldDelta);
    
    return _WorldSpaceCameraPos + worldDelta * SSS_GetCompressionRatio(worldDist);
}

float3 SSS_DecompressWorld(float3 worldPos)
{
    float3 worldDelta = worldPos - _WorldSpaceCameraPos;
    float  worldDist  = length(worldDelta);
    
    return worldDist > 0.001 ? _WorldSpaceCameraPos + (worldDelta / worldDist) * SSS_DecompressWorldDist(worldDist) : _WorldSpaceCameraPos;
}

float3 SSS_DecompressWorld(float3 worldPos, float ratio) 
{
    float3 worldDelta = worldPos - _WorldSpaceCameraPos;
    return _WorldSpaceCameraPos + worldDelta / ratio;
}

float SSS_ElongateDistance(float worldDist)
{
	float minDist = 10000.0;
	float maxDist = 100000.0;
	float scale   = saturate((worldDist - minDist) / (maxDist - minDist));

	return worldDist * lerp(1.0, 1.2, scale);
}
#define SGT_OPACITY_STEPS  5

float4x4        _SGT_SkyToWorld;
float4x4        _SGT_WorldToSky;
float4          _SGT_SkyColor;
float           _SGT_SkyBrightness;
float4          _SGT_SkyAmbientTint;
float           _SGT_SkyExposure;
float4          _SGT_SkyRadius;
float4          _SGT_SkyAltitudeData;
float           _SGT_SkyDensity;
float4          _SGT_SkyMieWeight;
float4          _SGT_SkyMieData;
sampler3D       _SGT_SkyRadianceTex;
float           _SGT_SkyRadianceLod;
sampler2D       _SGT_SkyLightingTex;
sampler2D       _SGT_SkyAlbedoTex;
float           _SGT_SkyRevealStars;
float           _SGT_SkyDepthOpaque;

float     _SGT_Volumetrics_Downscale; // Global
float     _SGT_Volumetrics_NearClip; // Global
Texture2D _SGT_Volumetrics_ColorTex; // Global
float4    _SGT_Volumetrics_ColorSize; // Global
Texture2D _SGT_Volumetrics_DepthTex; // Global
float2    _SGT_Volumetrics_DepthSize; // Global

SamplerState my_linear_clamp_sampler;
SamplerState my_point_clamp_sampler;

sampler2D _SGT_BlueNoiseTex; // Global
float     _SGT_Frame; // Global
float3    _SGT_AmbientColor; // Global
	
float SGT_DitherBlue(float2 screenUV)
{
	float2 pixel = floor(screenUV * _ScreenParams.xy);
	float  noise = tex2D(_SGT_BlueNoiseTex, pixel / 64.0f).r;
	return frac(noise + _SGT_Frame / sqrt(0.5f));
}

float SGT_DitherFast(float2 screenUV)
{
    float2 R2 = float2(0.7548776662, 0.5698402909);
    float2 p  = screenUV * _ScreenParams.xy + frac(_Time.y * R2) * 256.0;
    return frac(52.9829189 * frac(dot(p, float2(0.06711056, 0.00583715))));
}
	
float2 SGT_DirectionToEquirectangular(float3 dir)
{
	dir = normalize(dir);
	float u = atan2(dir.z, dir.x) / (2.0 * 3.141592653) + 0.5;
	float v = asin(clamp(dir.y, -1.0, 1.0)) / 3.141592653 + 0.5;
	return float2(u, v);
}

float3 SGT_SphereTest(float3 ray, float3 rayD, float radius)
{
	float B = -dot(ray, rayD);
	float C = dot(ray, ray) - radius * radius;
	float D = B * B - C;
	float E = sqrt(max(D, 0.0f));
	return float3(B - E, B + E, D);
}

float SGT_InverseLerp(float a, float b, float v)
{
	return (v - a) / (b - a);
}

float3 SGT_CalculateMie(float3 ray, float mie)
{
	return normalize(ray * _SGT_SkyMieWeight.xyz + 1e-6) * mie;
}

float SGT_GetPitch01(float3 rayDir, float3 up, float altitude01)
{
	float power = 2.0f;
	float eyeHeight = _SGT_SkyRadius.x + altitude01 * (_SGT_SkyRadius.y - _SGT_SkyRadius.x);
	float horizonAngleFromDown = -acos(clamp(_SGT_SkyRadius.x / max(eyeHeight, 0.00001f), -1.0f, 1.0f));
	float rayAng = asin(clamp(dot(up, rayDir), -1.0f, 1.0f));
	float sun01;

	if (rayAng < horizonAngleFromDown)
	{
		sun01 = SGT_InverseLerp(-3.141592653 * 0.5f, horizonAngleFromDown, rayAng);
		sun01 = 1.0f - pow(1.0f - sun01, 1.0f / power);
		sun01 = sun01 * 0.5f;
	}
	else
	{
		sun01 = SGT_InverseLerp(horizonAngleFromDown, 3.141592653 * 0.5f, rayAng);
		sun01 = pow(sun01, 1.0f / power);
		sun01 = sun01 * 0.5f + 0.5f;
	}

	return sun01;
}

float3 GetLutCoord(float3 rayPos, float3 rayDir, float3 sunDir, float dither)
{
	float3 up         = normalize(rayPos);
	float  eyeHeight  = length(rayPos);
	float  altitude01 = saturate(SGT_InverseLerp(_SGT_SkyRadius.z, 1.0f, eyeHeight));
	float  pitch01    = SGT_GetPitch01(rayDir, up, altitude01);
	float  sun01      = SGT_GetPitch01(sunDir, up, altitude01);
	
	pitch01 += (dither - 0.5f) / 128.0f;
	sun01   += (dither - 0.5f) /  64.0f;
	
	return float3(pitch01, altitude01, sun01);
}

float2 SGT_GetDensity2(float3 pos, float weightMul)
{
	float alt01 = saturate((1.0f - length(pos)) * _SGT_SkyAltitudeData.z);

	return pow(alt01, _SGT_SkyAltitudeData.xy * weightMul) * _SGT_SkyDensity;
}

float SGT_GetOpacity(float3 rayPos, float3 rayDir, float rayMax, float weightMul)
{
	float2 opticalDepth = 0.0f;
	float  rayStep      = rayMax / SGT_OPACITY_STEPS;
	float  t            = rayStep * 0.5f;
	
	for (int i = 0; i < SGT_OPACITY_STEPS; i++)
	{
		float3 pos = rayPos + rayDir * t;

		opticalDepth += SGT_GetDensity2(pos, weightMul) * rayStep;
		
		t += rayStep;
	}
	
	return 1.0f - exp((opticalDepth.x + opticalDepth.y) * -20.0f);
}

float4 SGT_SampleLUT(float3 coord)
{
	return tex3Dlod(_SGT_SkyRadianceTex, float4(coord, _SGT_SkyRadianceLod));
}

float3 SGT_AtmosphereColor(float3 rayPos, float3 sunDir, float dither)
{
	float3 up         = normalize(rayPos);
	float  eyeHeight  = length(rayPos);
	float  altitude01 = saturate(SGT_InverseLerp(_SGT_SkyRadius.z, 1.0f, eyeHeight));
	float  sun01      = 0.5f + 0.5f * dot(sunDir, up);
		
	return tex2Dlod(_SGT_SkyLightingTex, float4(sun01, altitude01, 0.0f, 0.0f)).xyz;
}

float3 SGT_UnderwaterAtmosphere(float3 rayPos, float3 rayDir, float3 sunDir, float exposure, float dither)
{
	float3 coord = GetLutCoord(rayPos, rayDir, sunDir, dither); coord.x = 1.0f;
	float4 ray   = SGT_SampleLUT(coord);
	float3 mie   = SGT_CalculateMie(ray.xyz, ray.w);
	float3 color = ray.xyz + mie;
	
	#if _SSS_HDRP
		color *= exposure * 0.25f;
	#else
		color = 1.0 - exp(-color * exposure);
	#endif
	
	return saturate(_SGT_SkyColor.xyz * color * _SGT_SkyBrightness);
}

float4 SGT_Atmosphere(float3 rayPos, float3 rayDir, float rayFar, float rayMax, float3 sunDir, float exposure, float dither, float mieMul, float weightMul)
{
	// 1. Calculate opacity FIRST so we can use it for blending
	float opacity = SGT_GetOpacity(rayPos, rayDir, rayMax, weightMul);

	float3 coord  = GetLutCoord(rayPos, rayDir, sunDir, dither);
	float2 mieOff = 0.0f;
	
	if (rayMax * 1.02f <= rayFar) // If ground
	{
		// 2. Store the true, un-clamped sky coordinate
		float trueCoordX = coord.x;
		
		// 3. Calculate the clamped "below horizon" coordinate (original behavior)
		float clampedCoordX = min(coord.x, 0.5f - (1.0f / 128.0f));
		clampedCoordX = lerp(0.5f - (1.0f / 128.0f), clampedCoordX, coord.y * coord.y);
		
		// 4. Smoothly blend towards the true sky color as fog density/opacity increases
		coord.x = lerp(clampedCoordX, trueCoordX, opacity);
		mieOff = lerp(0.2f, 0.0f, opacity);
	}
	else
	{
		//coord.x = max(coord.x, 0.5f + (1.0f / 128.0f));
	}
	
	float4 ray    = SGT_SampleLUT(coord);
	float3 mie    = SGT_CalculateMie(ray.xyz, ray.w);
	float2 g      = lerp(_SGT_SkyMieData.xy, 0.0f, mieOff);
	float  c      = dot(rayDir, sunDir);
	float  phaseR = 3.0f / (16.0f * 3.141592653f) * (1.0f + c * c);
	float2 phaseM = 3.0f / (8.0f * 3.141592653f) * ((1.0f - g * g) * (c * c + 1.0f)) / (pow(abs(1.0f + g * g - 2.0f * c * g), 1.5f) * (2.0f + g * g));
	float3 color  = ray.xyz * phaseR + mie * dot(phaseM, 1) * mieMul;
	
	#if _SSS_HDRP
		color *= exposure * 0.25f;
	#else
		color = 1.0 - exp(-color * exposure);
	#endif
	
	#if _SSS_HDRP
		color += _SGT_AmbientColor.xyz * _SGT_SkyAmbientTint.xyz * 0.25;
	#else
		color += _SGT_AmbientColor.xyz * _SGT_SkyAmbientTint.xyz;
	#endif

	#if _SGT_SOLID_CORE_OFF // If the atmosphere below InnerMeshRadius isn't solid, then continue to darken because the LUT doesn't extend any farther
		float eyeHeight     = length(rayPos);
		float inner         = _SGT_SkyRadius.z;
		float interiorDepth = saturate((_SGT_SkyRadius.z - eyeHeight) / _SGT_SkyRadius.z);

		if (interiorDepth > 0.0)
		{
			float extinction = exp(-interiorDepth * 20.0 * _SGT_SkyDensity);

			color *= extinction;
			opacity = lerp(opacity, 1.0f, saturate(interiorDepth * 100.0f));
		}
	#endif
	
	return float4(saturate(_SGT_SkyColor.xyz * color * _SGT_SkyBrightness), opacity);
}

// Global cached light properties
float3 _SGT_CachedLightDirection;
float3 _SGT_CachedLightColor;

float3 SGT_SampleSkyReflection(float3 ocam, float3 odir)
{
    // 2. Intersect with the Atmosphere Shell (Outer radius is 1.0 in sky space)
    float3 odst = SGT_SphereTest(ocam, odir, 1.0f);
    odst.xy = max(odst.xy, 0.0f); // Ensure distances are ahead of ray
    
    float3 rayPos = ocam + odir * odst.x;
    float3 rayDir = odir;
    
    // 3. Determine Atmosphere Limits
    float rayFar = SGT_SphereTest(rayPos, rayDir, 1.0f).y; // Distance to sky exit
    float rayMax = odst.y - odst.x;                       // Max scattering distance
    
    // Optional: Intersect with the planet core so reflections don't "see through" the world
    #if _SGT_SOLID_CORE
        float3 ihit = SGT_SphereTest(ocam, odir, _SGT_SkyRadius.w);
        if (ihit.z > 0.0f && ihit.x > 0.0f) {
            rayMax = min(ihit.x - odst.x, rayMax);
        }
    #endif

    // 4. Calculate Sun Direction in Sky Space
    float3 sunDir = normalize(mul(_SGT_WorldToSky, float4(_SGT_CachedLightDirection, 0.0f)).xyz);
    
    // 5. Sample the Atmosphere
    // dither is set to 0.0 to keep reflections smooth; mieMul is 1.0
    float4 sky = SGT_Atmosphere(rayPos, rayDir, rayFar, rayMax, sunDir, _SGT_SkyExposure, 0.0, 1.0, 1.0);
    
    // 6. Apply cached light intensity
    return sky.rgb * _SGT_CachedLightColor;
}

float4 SGT_SimpleAtmosphere(float3 worldPos, float3 worldHit, float mieMul, float weightMul)
{
	float dither   = 0.5f;
	
	float3 ocam = mul(_SGT_WorldToSky, float4(worldPos, 1.0f)).xyz;
	float3 ofar = mul(_SGT_WorldToSky, float4(worldHit, 1.0f)).xyz;
	float  omax = distance(ocam, ofar);
	float3 odir = normalize(ofar - ocam);
	float3 odst = SGT_SphereTest(ocam, odir, 1.0f);
	
	odst.xy = max(odst.xy, 0.0f);
	
	float3 rayPos = ocam + odir * odst.x;
	float3 rayDir = odir;
	float  rayFar = SGT_SphereTest(rayPos, rayDir, 1.0f).y;
	float  rayMax = min(odst.y, omax) - odst.x;
	
	#if _SGT_LIGHTING
		float3 sunDir = normalize(mul(_SGT_WorldToSky, float4(_SGT_LightDirection[0].xyz, 0.0f)).xyz);
	#else
		float3 sunDir = normalize(rayPos);
	#endif
	
	return SGT_Atmosphere(rayPos, rayDir, rayFar, rayMax, sunDir, _SGT_SkyExposure, dither, mieMul, weightMul);
}

float3 CW_Depth2World(float viewDepth, float3 viewDir)
{
	return _WorldSpaceCameraPos + viewDir * viewDepth;
}
	
bool CW_DepthInBounds(float viewDepth, float3 viewDir)
{
	float3 wPos = CW_Depth2World(viewDepth, viewDir);
	float3 oPos = mul(_SGT_WorldToSky, float4(wPos, 1.0f)).xyz;
		
	return length(oPos) < 1.2f;
}
	
void CW_SampleVolumetricsDefault(float2 screenUV, float3 viewDir, out float4 outColor, out float outDepth)
{
	float4 centerColor = _SGT_Volumetrics_ColorTex.Sample(my_linear_clamp_sampler, screenUV);
	float  centerDepth = _SGT_Volumetrics_DepthTex.Sample(my_linear_clamp_sampler, screenUV).x;
		
	if (CW_DepthInBounds(centerDepth, viewDir) == true)
	{
		outColor = centerColor;
		outDepth = centerDepth;
	}
	else
	{
		outColor = 0;
		outDepth = 0;
	}
}

void CW_SampleVolumetricsDefault2(float2 screenUV, float3 viewDir, out float4 outColor, out float outDepth)
{
	outColor = _SGT_Volumetrics_ColorTex.Sample(my_linear_clamp_sampler, screenUV);
	outDepth = _SGT_Volumetrics_DepthTex.Sample(my_linear_clamp_sampler, screenUV).x;
}
	
void CW_SampleVolumetrics(float2 screenUV, float3 viewDir, float sceneDepth, out float4 outColor, out float outDepth)
{
	outColor = 0;
	outDepth = 0;
	//CW_SampleVolumetricsDefault(screenUV, viewDir, outColor, outDepth);return;
		
	if (_SGT_Volumetrics_Downscale > 1)
	{
		float2 pixelF   = screenUV * _SGT_Volumetrics_ColorSize.xy - 0.5f;
		float2 pixelI   = floor(pixelF);
		float2 snappedF = pixelF - pixelI;
			
		float2 coord0 = (pixelI + float2(0.5f, 0.5f)) * _SGT_Volumetrics_ColorSize.zw;
		float2 coord1 = (pixelI + float2(1.5f, 0.5f)) * _SGT_Volumetrics_ColorSize.zw;
		float2 coord2 = (pixelI + float2(0.5f, 1.5f)) * _SGT_Volumetrics_ColorSize.zw;
		float2 coord3 = (pixelI + float2(1.5f, 1.5f)) * _SGT_Volumetrics_ColorSize.zw;
			
		float depth0 = _SGT_Volumetrics_DepthTex.Sample(my_point_clamp_sampler, coord0).x;
		float depth1 = _SGT_Volumetrics_DepthTex.Sample(my_point_clamp_sampler, coord1).x;
		float depth2 = _SGT_Volumetrics_DepthTex.Sample(my_point_clamp_sampler, coord2).x;
		float depth3 = _SGT_Volumetrics_DepthTex.Sample(my_point_clamp_sampler, coord3).x;
			
		float4 color0 = _SGT_Volumetrics_ColorTex.Sample(my_point_clamp_sampler, coord0);
		float4 color1 = _SGT_Volumetrics_ColorTex.Sample(my_point_clamp_sampler, coord1);
		float4 color2 = _SGT_Volumetrics_ColorTex.Sample(my_point_clamp_sampler, coord2);
		float4 color3 = _SGT_Volumetrics_ColorTex.Sample(my_point_clamp_sampler, coord3);
			
		float w0 = CW_DepthInBounds(depth0, viewDir) * (depth0 < sceneDepth) * color0.w * (1.0 - snappedF.x) * (1.0 - snappedF.y);
		float w1 = CW_DepthInBounds(depth1, viewDir) * (depth1 < sceneDepth) * color1.w * snappedF.x * (1.0 - snappedF.y);
		float w2 = CW_DepthInBounds(depth2, viewDir) * (depth2 < sceneDepth) * color2.w * (1.0 - snappedF.x) * snappedF.y;
		float w3 = CW_DepthInBounds(depth3, viewDir) * (depth3 < sceneDepth) * color3.w * snappedF.x * snappedF.y;
			
		float wt = w0 + w1 + w2 + w3;
			
		if (wt > 0.0f)
		{
			outColor = (color0 * w0 + color1 * w1 + color2 * w2 + color3 * w3) / wt;
			outDepth = (depth0 * w0 + depth1 * w1 + depth2 * w2 + depth3 * w3) / wt;
		}
		else
		{
			return;
		}
	}
	else
	{
		float2 coord0 = screenUV;
		float  depth0 = _SGT_Volumetrics_DepthTex.Sample(my_point_clamp_sampler, coord0).x;
		float4 color0 = _SGT_Volumetrics_ColorTex.Sample(my_point_clamp_sampler, coord0);
		float  w0     = CW_DepthInBounds(depth0, viewDir) * (depth0 < sceneDepth);
		
		if (w0 > 0.0f)
		{
			outColor = color0;
			outDepth = depth0;
		}
		else
		{
			return;
		}
	}
}






#pragma instancing_options procedural:SetupInstancing
#if _SSS_PASS_SHADOWCASTER || _SSS_PASS_META
	#pragma multi_compile_instancing // For some reason the ShadowCaster and Meta pass in BIRP doesn't have this line
#endif


#define BATCH_CAPACITY 35
#define VERTEX_COUNT 243

float4 _CwSize;
float4 _CwAtlas;
float4 _CwWeights[VERTEX_COUNT];
float4 _CwCoords[VERTEX_COUNT];

float4 _CwOrigins[BATCH_CAPACITY];
float4 _CwPositionsA[BATCH_CAPACITY];
float4 _CwPositionsB[BATCH_CAPACITY];
float4 _CwPositionsC[BATCH_CAPACITY];

sampler2D DataP;
sampler2D DataA;
sampler2D DataN;

float3 _CwOffset;

float4x4 _CwObjectToWorld;
float4x4 _CwWorldToObject;

float _SGT_Occlusion;

// CLOUDS
sampler2D _SGT_CloudShadowTex;
float3    _SGT_CloudShadowDirection;
float4    _SGT_CloudShadowOpacity;
float4x4  _SGT_CloudShadowMatrix;

// OCEAN FADE
float  _SGT_OceanFade;
float2 _SGT_OceanDensity;
float3 _SGT_OceanColor;
float  _SGT_OceanSmoothness;
float4 _SGT_OceanLightDirection;

float _SGT_OceanRadius;

void SetupInstancing()
{
	#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
		#ifdef unity_ObjectToWorld
			#undef unity_ObjectToWorld
		#endif

		#ifdef unity_WorldToObject
			#undef unity_WorldToObject
		#endif
		
		unity_ObjectToWorld = _CwObjectToWorld;
		unity_WorldToObject = _CwWorldToObject;
	#endif
}

float SGT_SampleCloudDensity(float3 wpos)
{
	float3 cpos  = mul(_SGT_CloudShadowMatrix, float4(wpos, 1.0)).xyz;
	float3 chit  = SGT_SphereTest(cpos, _SGT_CloudShadowDirection, 1.0);
	float2 uv    = SGT_DirectionToEquirectangular(normalize(cpos + chit.y * _SGT_CloudShadowDirection));
	
	return saturate(dot(tex2D(_SGT_CloudShadowTex, uv), _SGT_CloudShadowOpacity));
}

void SSS_Vert(inout SSS_VertexData v)
{
	float vertexIndex = v.position.x;
	float squareIndex = v.position.y;
	float batchIndex  = v.instanceID;
	
	float  batchCol = batchIndex % _CwAtlas.z;
	float  batchRow = floor(batchIndex / _CwAtlas.z);
	float3 origin   = _CwOrigins[batchIndex].xyz;
	float3 weights  = _CwWeights[vertexIndex].xyz;
	float2 coord    = _CwCoords[vertexIndex].xy; coord.x /= 3.0;
	float3 position = _CwPositionsA[batchIndex].xyz * weights.x + _CwPositionsB[batchIndex].xyz * weights.y + _CwPositionsC[batchIndex].xyz * weights.z;
	
	v.position.xyz = _CwOffset + origin + tex2Dlod(DataP, float4(vertexIndex * _CwSize.x, batchIndex * _CwSize.y, 0.0, 0.0)).xyz;
	
	float3 ocam = mul(_CwWorldToObject, float4(_WorldSpaceCameraPos - _CwOffset, 1.0)).xyz;
	
	#if _SGT_SHAPE_SQUARE
		v.normal  = float3(0,1,0);
		v.tangent = float4(1,0,0,-1).xyz;
		
		v.extraV2F0.w = ocam.y;
	#elif _SGT_SHAPE_SPHERE
		v.normal = normalize(position);
		
		if (position.x == 0.0 && position.z == 0.0)
		{
			position.xz += 0.00000001;
		}
		
		v.tangent = float4(normalize(cross(float3(0,1,0), normalize(position))), -1.0).xyz;
		
		v.extraV2F0.w = length(ocam) - _SGT_OceanRadius;
	#endif
	
	#if _SGT_OCEAN_FADE
		if (_SGT_OceanFade > 0.0 && length(v.position.xyz - _CwOffset) < _SGT_OceanRadius)
		{
			float3 N = normalize(position); // sphere normal
			float3 V = normalize(_WorldSpaceCameraPos - v.position.xyz);
			float  F = pow(abs(1.0 - saturate(dot(N, V))), 2.0);

			float3 oceanPos = _CwOffset + N * _SGT_OceanRadius;
			v.position.xyz = lerp(v.position.xyz, oceanPos, F);
		}

	#endif
	
	// Calc UV
	float2 pixelS = _CwAtlas.xy * _CwAtlas.zw;
	float  pixelX = batchCol * _CwAtlas.x + coord.x * (_CwAtlas.x - 3.0) + 0.5 + squareIndex * (_CwAtlas.x / 3.0);
	float  pixelY = batchRow * _CwAtlas.y + coord.y * (_CwAtlas.y - 1.0) + 0.5;
	
	v.extraV2F0.x = pixelX / pixelS.x;
	v.extraV2F0.y = pixelY / pixelS.y;
	
	#if CW_COMPRESS_POSITIONS
		float3 wpos = mul(_CwObjectToWorld, float4(v.position, 1.0)).xyz;
		v.extraV2F0.z = distance(_WorldSpaceCameraPos, wpos);
		wpos = SSS_CompressWorld(wpos);
		v.position = mul(_CwWorldToObject, float4(wpos, 1.0)).xyz;
	#endif
}

float3 SGT_GetColor(float3 wnormal, float3 wlight)
{
	return saturate(dot(wnormal, wlight));
}

static const float WATER_DEPTH_SHALLOW = 30.0;
static const float WATER_DEPTH_MAX = 3000.0;

float SGT_DecodeWaterDepth(float encoded)
{
	float shallow = encoded * (WATER_DEPTH_SHALLOW * 2.0);
    float deep = WATER_DEPTH_SHALLOW + (encoded - 0.5) * ((WATER_DEPTH_MAX - WATER_DEPTH_SHALLOW) * 2.0);
    float isDeep = step(0.5, encoded);
    return lerp(shallow, deep, isDeep);
}

float3 SGT_ApplyOceanColor(
    float3 terrainColor,
    float  waterDepth01,
    float3 waterColor,
    float2 waterDensity,
    float  waterBlend,
    float3 ambientColor)
{
    float depth = SGT_DecodeWaterDepth(waterDepth01);
    float3 w = float3(1.0, 0.2, 0.1);

	float3 e0 = w * waterDensity.x;
	float3 e1 = w * waterDensity.y;

	float3 floorT0 = exp(-e0 * depth);
	float3 floorT1 = exp(-e1 * depth);
	
	float3 volumeT0 = floorT0 * floorT0 * floorT0;
	float3 volumeT1 = floorT1 * floorT1 * floorT1;

	float3 scatter = waterColor * ambientColor;

	float3 col0 = terrainColor * floorT0 + scatter * (1.0 - volumeT0);
	float3 col1 = terrainColor * floorT1 + scatter * (1.0 - volumeT1);

	return lerp(col0, col1, waterBlend);
}

float SGT_Bayer4x4(float2 screenPos)
{
	uint2 p = uint2(screenPos * _ScreenParams.xy) % 4;
	float bayer[16] = { 0,  8,  2, 10, 12,  4, 14,  6, 3, 11,  1,  9, 15,  7, 13,  5 };
	return (bayer[p.x + p.y * 4] + 0.5) / 16.0;
}

float SGT_Fresnel(float3 dir, float3 normal, float airIOR, float waterIOR)
{
	float cosTheta = clamp(dot(dir, normal), 0.0, 1.0);
	float F0       = pow(abs((waterIOR / airIOR - 1.0) / (waterIOR / airIOR + 1.0)), 2.0);

	return F0 + (1.0 - F0) * pow(abs(1.0 - cosTheta), 5.0);
}

void SSS_Frag(inout SSS_SurfaceData o, inout SSS_FragmentData d)
{
	#if CW_COMPRESS_POSITIONS
		d.worldSpacePosition = _WorldSpaceCameraPos - d.worldSpaceViewDir * d.extraV2F0.z;
	#endif
	
	float4 dataA = tex2Dlod(DataA, float4(d.extraV2F0.xy,0,0));
	float4 dataN = tex2Dlod(DataN, float4(d.extraV2F0.xy,0,0)); 
	float2 tnorm = dataN.xy * 2.0 - 1.0;
	
	tnorm.x = -tnorm.x; // Don't set negative tangent sign, so do it manually
	
	o.Albedo     = dataA.xyz;
	o.Occlusion  = _SGT_Occlusion;
	o.Emission   = dataA.xyz * dataN.z;
	o.Smoothness = dataN.w;
	o.Metallic   = 0.0;
	o.Normal     = float3(tnorm, sqrt(1.0 - saturate(dot(tnorm, tnorm))));
	o.Alpha      = dataN.x != 0.0 && dataN.y != 0.0;
	
	float localEyeHeight = d.extraV2F0.w;
	
	#if _SGT_OCEAN_FADE
		float  fadeTransition = step(SGT_Bayer4x4(d.screenUV), _SGT_OceanFade);
		float  cameraDistance = distance(d.worldSpacePosition, _WorldSpaceCameraPos);
		float  cameraDist01   = 1.0 - pow(abs(saturate(1.0 - cameraDistance / _SGT_OceanRadius)), 8.0);
		float3 oceanColor     = SGT_ApplyOceanColor(o.Albedo, dataA.w, _SGT_OceanColor, _SGT_OceanDensity, cameraDist01, 1.0);
		float  fade           = saturate(dataA.w > 0) * fadeTransition;

		o.Albedo     = lerp(o.Albedo, oceanColor, fade);
		o.Smoothness = lerp(o.Smoothness, _SGT_OceanSmoothness, fade);
		o.Normal     = lerp(o.Normal, float3(0.0, 0.0, 1.0), fade);
		o.Occlusion  = lerp(o.Occlusion, 0.15, fade);

		#if _SGT_SKY
			float  airIOR   = 1.0;
			float  waterIOR = 1.33;
			float3 opos     = mul(_SGT_WorldToSky, float4(d.worldSpacePosition, 1.0)).xyz;
			float3 onrm     = normalize(opos);
			float3 oview    = normalize(mul(_SGT_WorldToSky, float4(d.worldSpaceViewDir, 0.0)).xyz);
			float3 odir     = reflect(oview, onrm);
	
			o.Emission = SGT_SampleSkyReflection(opos, -odir) * fade * SGT_Fresnel(oview, onrm, airIOR, waterIOR);
			#if _SSS_HDRP
				o.Emission *= 120000;
			#endif
		#endif
		//o.Emission +=  _SGT_AmbientColor.xyz * 0.02;
	#endif
	
	#if _SGT_CLOUDS
		float shadow = SGT_SampleCloudDensity(d.worldSpacePosition);
		
		o.Albedo    = lerp(o.Albedo   , 0.0, shadow);
		o.Occlusion = lerp(o.Occlusion, 0.0, shadow);
	#endif
}


void Vert_float
	(
	float  iInstanceID,
	float3 iPosition,
	float3 iNormal,
	float3 iTangent,
	float4 iColor,
	float4 iTexcoord0,
	float4 iTexcoord1,
	float4 iTexcoord2,
	float4 iTexcoord3,

	out float3 oPosition,
	out float3 oNormal,
	out float3 oTangent,
	out float4 oExtraV2F0,
	out float4 oExtraV2F1,
	out float4 oExtraV2F2,
	out float4 oExtraV2F3,
	out float4 oExtraV2F4,
	out float4 oExtraV2F5,
	out float4 oExtraV2F6,
	out float4 oExtraV2F7
	)
{
	SSS_VertexData v = (SSS_VertexData)0;
	
	v.instanceID = iInstanceID;
	v.position   = iPosition;
	v.normal     = iNormal;
	v.tangent    = iTangent;
	v.color      = iColor;
	v.texcoord0  = iTexcoord0;
	v.texcoord1  = iTexcoord1;
	v.texcoord2  = iTexcoord2;
	v.texcoord3  = iTexcoord3;
	v.extraV2F0  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F1  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F2  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F3  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F4  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F5  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F6  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F7  = float4(0.0, 0.0, 0.0, 0.0);
	
	SSS_Vert(v);
	
	oPosition  = v.position;
	oNormal    = v.normal;
	oTangent   = v.tangent;
	oExtraV2F0 = v.extraV2F0;
	oExtraV2F1 = v.extraV2F1;
	oExtraV2F2 = v.extraV2F2;
	oExtraV2F3 = v.extraV2F3;
	oExtraV2F4 = v.extraV2F4;
	oExtraV2F5 = v.extraV2F5;
	oExtraV2F6 = v.extraV2F6;
	oExtraV2F7 = v.extraV2F7;
}

void Frag_float
	(
	inout float3 iPosition,
	inout float3 iNormal,
	inout float3 iTangent,
	bool   iIsFrontFace,
	float4 iColor,
	float4 iTexcoord0,
	float4 iTexcoord1,
	float4 iTexcoord2,
	float4 iTexcoord3,
	float4 iExtraV2F0,
	float4 iExtraV2F1,
	float4 iExtraV2F2,
	float4 iExtraV2F3,
	float4 iExtraV2F4,
	float4 iExtraV2F5,
	float4 iExtraV2F6,
	float4 iExtraV2F7,

	out float4x4 oExtra,
	out float3   oAlbedo,
	out float    oSmoothness,
	out float3   oNormal,
	out float3   oEmission,
	out float    oOcclusion,
	out float    oMetallic,
	out float    oAlpha
	)
{
	SSS_SurfaceData  s = (SSS_SurfaceData)0;
	SSS_FragmentData d = (SSS_FragmentData)0;
	
	s.Albedo = 1.0;
	s.Smoothness = 0.5;
	s.Normal = float3(0.0, 0.0, 1.0);
	s.Emission = float3(0.0, 0.0, 0.0);
	s.Occlusion = 0.0;
	s.Metallic = 0.0;
	s.Alpha = 1.0;
	
	iPosition = SSS_WorldToAbsolute(iPosition);
	
	d.localSpacePosition = SSS_WorldToObject(iPosition);
	d.localSpaceNormal   = normalize(SSS_WorldToObjectDir(iNormal));
	d.localSpaceTangent  = normalize(SSS_WorldToObjectDir(iTangent));
	
	d.worldSpacePosition = iPosition;
	d.worldSpaceNormal   = iNormal;
	d.worldSpaceTangent  = iTangent;
	//d.tangentSign;
	
	d.worldSpaceViewDir  = normalize(_WorldSpaceCameraPos - d.worldSpacePosition);
	//d.tangentSpaceViewDir;
	
	d.texcoord0 = iTexcoord0;
	d.texcoord1 = iTexcoord1;
	d.texcoord2 = iTexcoord2;
	d.texcoord3 = iTexcoord3;
	
	d.screenPos = float4(SSS_WorldToScreen(iPosition), 1.0);
	d.screenUV  = d.screenPos.xy;

	d.vertexColor = iColor;
	d.isFrontFace = iIsFrontFace;
	
	d.extraV2F0 = iExtraV2F0;
	d.extraV2F1 = iExtraV2F1;
	d.extraV2F2 = iExtraV2F2;
	d.extraV2F3 = iExtraV2F3;
	d.extraV2F4 = iExtraV2F4;
	d.extraV2F5 = iExtraV2F5;
	d.extraV2F6 = iExtraV2F6;
	d.extraV2F7 = iExtraV2F7;

	d.TBNMatrix = float3x3(d.worldSpaceTangent, normalize(cross(d.worldSpaceNormal, d.worldSpaceTangent)), d.worldSpaceNormal);
	
	SSS_Frag(s, d);
	
	iPosition = SSS_AbsoluteToWorld(d.worldSpacePosition); iNormal = d.worldSpaceNormal; iTangent = d.worldSpaceTangent; // Write back
	
	oExtra      = float4x4(d.extraV2F0, d.extraV2F1, d.extraV2F2, d.extraV2F3);
	oAlbedo     = s.Albedo;
	oSmoothness = s.Smoothness;
	oNormal     = s.Normal;
	oEmission   = s.Emission;
	oOcclusion  = s.Occlusion;
	oMetallic   = s.Metallic;
	oAlpha      = s.Alpha;
}



	#pragma shader_feature_local _SGT_SHAPE_SQUARE _SGT_SHAPE_SPHERE
	#pragma shader_feature_local _SGT_CLOUDS_OFF _SGT_CLOUDS
	#pragma shader_feature_local _SGT_OCEAN_FADE_OFF _SGT_OCEAN_FADE
	#pragma shader_feature_local _SGT_SKY_OFF _SGT_SKY
	#pragma multi_compile __ CW_COMPRESS_POSITIONS



// Graph Functions
// GraphFunctions: <None>

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
float4 extraV2F0;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
float4 _UV_ccc703dc3bdd420e8a8a4aa0607d4ec4_Out_0_Vector4 = IN.uv0;
float4 _UV_46571950324b44e8bf8895f08abd191f_Out_0_Vector4 = IN.uv1;
float4 _UV_ba4ef72275334f08b9b01fedf9da0065_Out_0_Vector4 = IN.uv2;
float4 _UV_255ab3a515d14af686e70c40d04416e6_Out_0_Vector4 = IN.uv3;
float3 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oPosition_0_Vector3;
float3 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oNormal_5_Vector3;
float3 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oTangent_6_Vector3;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F0_7_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F1_8_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F2_13_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F3_14_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F4_15_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F5_16_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F6_17_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F7_18_Vector4;
Vert_float(IN.InstanceID, IN.ObjectSpacePosition, IN.ObjectSpaceNormal, IN.ObjectSpaceTangent, IN.VertexColor, _UV_ccc703dc3bdd420e8a8a4aa0607d4ec4_Out_0_Vector4, _UV_46571950324b44e8bf8895f08abd191f_Out_0_Vector4, _UV_ba4ef72275334f08b9b01fedf9da0065_Out_0_Vector4, _UV_255ab3a515d14af686e70c40d04416e6_Out_0_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oPosition_0_Vector3, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oNormal_5_Vector3, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oTangent_6_Vector3, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F0_7_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F1_8_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F2_13_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F3_14_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F4_15_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F5_16_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F6_17_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F7_18_Vector4);
description.Position = _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oPosition_0_Vector3;
description.Normal = _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oNormal_5_Vector3;
description.Tangent = _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oTangent_6_Vector3;
description.extraV2F0 = _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F0_7_Vector4;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
output.extraV2F0 = input.extraV2F0;
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
float Alpha;
float AlphaClipThreshold;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
float _IsFrontFace_5b03f99e2e7841c3a7d2ae306590b576_Out_0_Boolean = max(0, IN.FaceSign.x);
float4 _UV_c1e39353e82e434da821b9bdc4338e4b_Out_0_Vector4 = IN.uv0;
float4 _UV_64d51b5974bc4ecf849e7307e6de2727_Out_0_Vector4 = IN.uv1;
float4x4 _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oExtra_23_Matrix4;
float3 _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oAlbedo_0_Vector3;
float _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oSmoothness_5_Float;
float3 _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oNormal_6_Vector3;
float3 _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oEmission_7_Vector3;
float _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oOcclusion_8_Float;
float _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oMetallic_9_Float;
float _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oAlpha_10_Float;
Frag_float(IN.WorldSpacePosition, IN.WorldSpaceNormal, IN.WorldSpaceTangent, _IsFrontFace_5b03f99e2e7841c3a7d2ae306590b576_Out_0_Boolean, float4 (0, 0, 0, 0), _UV_c1e39353e82e434da821b9bdc4338e4b_Out_0_Vector4, _UV_64d51b5974bc4ecf849e7307e6de2727_Out_0_Vector4, float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), IN.extraV2F0, float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oExtra_23_Matrix4, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oAlbedo_0_Vector3, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oSmoothness_5_Float, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oNormal_6_Vector3, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oEmission_7_Vector3, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oOcclusion_8_Float, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oMetallic_9_Float, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oAlpha_10_Float);
surface.Alpha = _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oAlpha_10_Float;
surface.AlphaClipThreshold = float(0.5);
return surface;
}

// --------------------------------------------------
// Build Graph Inputs

VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;
    output.uv0 =                                        input.uv0;
    output.uv1 =                                        input.uv1;
    output.uv2 =                                        input.uv2;
    output.uv3 =                                        input.uv3;
    output.VertexColor =                                input.color;
#if UNITY_ANY_INSTANCING_ENABLED
    output.InstanceID =                                 unity_InstanceID;
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
    output.InstanceID =                                 input.instanceID;
#endif

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

    output.extraV2F0 = input.extraV2F0;

    // must use interpolated tangent, bitangent and normal before they are normalized in the pixel shader.
    float3 unnormalizedNormalWS = input.normalWS;
    const float renormFactor = 1.0 / length(unnormalizedNormalWS);


    output.WorldSpaceNormal = renormFactor * input.normalWS.xyz;      // we want a unit length Normal Vector node in shader graph

    // to preserve mikktspace compliance we use same scale renormFactor as was used on the normal.
    // This is explained in section 2.2 in "surface gradient based bump mapping framework"
    output.WorldSpaceTangent = renormFactor * input.tangentWS.xyz;

    output.WorldSpacePosition = input.positionWS;

    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


    output.uv0 = input.texCoord0;
    output.uv1 = input.texCoord1;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
    BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

void BuildAppDataFull(Attributes attributes, VertexDescription vertexDescription, inout appdata_full result)
{
    result.vertex     = float4(attributes.positionOS, 1);
    result.tangent    = attributes.tangentOS;
    result.normal     = attributes.normalOS;
    result.texcoord   = attributes.uv0;
    result.texcoord1  = attributes.uv1;
    result.texcoord2  = attributes.uv2;
    result.texcoord3  = attributes.uv3;
    result.color      = attributes.color;
    result.vertex     = float4(vertexDescription.Position, 1);
    result.normal     = vertexDescription.Normal;
    result.tangent    = float4(vertexDescription.Tangent, 0);
    #if UNITY_ANY_INSTANCING_ENABLED
    result.instanceID = attributes.instanceID;
    #endif
}

void VaryingsToSurfaceVertex(Varyings varyings, inout v2f_surf result)
{
    result.pos = varyings.positionCS;
    result.worldPos = varyings.positionWS;
    result.worldNormal = varyings.normalWS;
    // World Tangent isn't an available input on v2f_surf


    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
    #if UNITY_SHOULD_SAMPLE_SH
    #if !defined(LIGHTMAP_ON)
    #endif
    #endif
    #if defined(LIGHTMAP_ON)
    #endif
    #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        result.fogCoord = varyings.fogFactorAndVertexLight.x;
        COPY_TO_LIGHT_COORDS(result, varyings.fogFactorAndVertexLight.yzw);
    #endif

    DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(varyings, result);
}

void SurfaceVertexToVaryings(v2f_surf surfVertex, inout Varyings result)
{
    result.positionCS = surfVertex.pos;
    result.positionWS = surfVertex.worldPos;
    result.normalWS = surfVertex.worldNormal;
    // viewDirectionWS is never filled out in the legacy pass' function. Always use the value computed by SRP
    // World Tangent isn't an available input on v2f_surf

    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
    #if UNITY_SHOULD_SAMPLE_SH
    #if !defined(LIGHTMAP_ON)
    #endif
    #endif
    #if defined(LIGHTMAP_ON)
    #endif
    #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        result.fogFactorAndVertexLight.x = surfVertex.fogCoord;
        COPY_FROM_LIGHT_COORDS(result.fogFactorAndVertexLight.yzw, surfVertex);
    #endif

    DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(surfVertex, result);
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/DepthOnlyPass.hlsl"

ENDHLSL
}
Pass
{
    Name "Meta"
    Tags
    {
        "LightMode" = "Meta"
    }

// Render State
Cull Off

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM
#define _SSS_PASS_META 1

#define _SSS_BIRP 1


// Pragmas
#pragma target 3.0
#pragma vertex vert
#pragma fragment frag

// Keywords
#pragma shader_feature _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
// GraphKeywords: <None>

// Defines
#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_TEXCOORD0
#define ATTRIBUTES_NEED_TEXCOORD1
#define ATTRIBUTES_NEED_TEXCOORD2
#define ATTRIBUTES_NEED_TEXCOORD3
#define ATTRIBUTES_NEED_COLOR
#define ATTRIBUTES_NEED_INSTANCEID
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_NORMAL_WS
#define VARYINGS_NEED_TANGENT_WS
#define VARYINGS_NEED_TEXCOORD0
#define VARYINGS_NEED_TEXCOORD1
#define VARYINGS_NEED_CULLFACE
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_META
#define BUILTIN_TARGET_API 1
#define _BUILTIN_AlphaClip 1
#define _BUILTIN_ALPHATEST_ON 1
#ifdef _BUILTIN_SURFACE_TYPE_TRANSPARENT
#define _SURFACE_TYPE_TRANSPARENT _BUILTIN_SURFACE_TYPE_TRANSPARENT
#endif
#ifdef _BUILTIN_ALPHATEST_ON
#define _ALPHATEST_ON _BUILTIN_ALPHATEST_ON
#endif
#ifdef _BUILTIN_AlphaClip
#define _AlphaClip _BUILTIN_AlphaClip
#endif
#ifdef _BUILTIN_ALPHAPREMULTIPLY_ON
#define _ALPHAPREMULTIPLY_ON _BUILTIN_ALPHAPREMULTIPLY_ON
#endif


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Shim/Shims.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/LegacySurfaceVertex.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/ShaderGraphFunctions.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
 float4 uv0 : TEXCOORD0;
 float4 uv1 : TEXCOORD1;
 float4 uv2 : TEXCOORD2;
 float4 uv3 : TEXCOORD3;
 float4 color : COLOR;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float3 positionWS;
 float3 normalWS;
 float4 tangentWS;
 float4 texCoord0;
 float4 texCoord1;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
 float4 extraV2F0;
};
struct SurfaceDescriptionInputs
{
 float3 WorldSpaceNormal;
 float3 WorldSpaceTangent;
 float3 WorldSpacePosition;
 float4 uv0;
 float4 uv1;
 float FaceSign;
 float4 extraV2F0;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
 float4 uv0;
 float4 uv1;
 float4 uv2;
 float4 uv3;
 float4 VertexColor;
 uint InstanceID;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
 float4 tangentWS : INTERP0;
 float4 texCoord0 : INTERP1;
 float4 texCoord1 : INTERP2;
 float4 extraV2F0 : INTERP3;
 float3 positionWS : INTERP4;
 float3 normalWS : INTERP5;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
output.tangentWS.xyzw = input.tangentWS;
output.texCoord0.xyzw = input.texCoord0;
output.texCoord1.xyzw = input.texCoord1;
output.extraV2F0.xyzw = input.extraV2F0;
output.positionWS.xyz = input.positionWS;
output.normalWS.xyz = input.normalWS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
output.tangentWS = input.tangentWS.xyzw;
output.texCoord0 = input.texCoord0.xyzw;
output.texCoord1 = input.texCoord1.xyzw;
output.extraV2F0 = input.extraV2F0.xyzw;
output.positionWS = input.positionWS.xyz;
output.normalWS = input.normalWS.xyz;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties
CBUFFER_START(UnityPerMaterial)





CBUFFER_END


// Object and Global properties

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Includes
// UNITY_SHADER_NO_UPGRADE
float3 SSS_HClipToScreen(float4 v)
{
	float3 uv = v.xyz / v.w;
	#if UNITY_UV_STARTS_AT_TOP
		uv.y = -uv.y;
	#endif
	uv.xy = uv.xy * 0.5 + 0.5;
	return uv;
}

#if _SSS_HDRP
	float3 SSS_WorldToAbsolute(float3 v) { return GetAbsolutePositionWS(v); }
	float3 SSS_AbsoluteToWorld(float3 v) { return GetCameraRelativePositionWS(v); }
#else
	float3 SSS_WorldToAbsolute(float3 v) { return v; }
	float3 SSS_AbsoluteToWorld(float3 v) { return v; }
#endif

float3 SSS_WorldToView(float3 v) { return TransformWorldToView(SSS_AbsoluteToWorld(v)); }
float3 SSS_WorldToObject(float3 v) { return TransformWorldToObject(SSS_AbsoluteToWorld(v)); }
float3 SSS_WorldToScreen(float3 v) { return SSS_HClipToScreen(TransformWorldToHClip(SSS_AbsoluteToWorld(v))); }
float3 SSS_ObjectToScreen(float3 v) { return SSS_HClipToScreen(TransformObjectToHClip(v)); }
float3 SSS_ObjectToWorld(float3 v) { return SSS_WorldToAbsolute(TransformObjectToWorld(v)); }
float3 SSS_ObjectToView(float3 v) { return TransformWorldToView(TransformObjectToWorld(v)); }
float3 SSS_ScreenToWorld(float3 v) { return SSS_WorldToAbsolute(ComputeWorldSpacePosition(v.xy, v.z, UNITY_MATRIX_I_VP)); }
float3 SSS_ScreenToObject(float3 v) { return SSS_WorldToObject(SSS_ScreenToWorld(v)); }
float3 SSS_ScreenToView(float3 v) { return SSS_WorldToView(SSS_ScreenToWorld(v)); }
float3 SSS_ViewToWorld(float3 v) { return mul(UNITY_MATRIX_I_V, float4(v, 1.0)).xyz; }
float3 SSS_ViewToObject(float3 v) { return TransformWorldToObject(SSS_ViewToWorld(v)); }
float3 SSS_ViewToScreen(float3 v) { return SSS_HClipToScreen(TransformWViewToHClip(v)); }
float3 SSS_ObjectToWorldDir(float3 v)
{
	#if _SSS_BIRP
		return TransformObjectToWorldDir(v);
	#else
		return TransformObjectToWorldDir(v, true);
	#endif
}
float3 SSS_ObjectToViewDir(float3 v)
{
	#if _SSS_BIRP
		return TransformWorldToViewDir(TransformObjectToWorldDir(v));
	#else
		return TransformWorldToViewDir(TransformObjectToWorldDir(v, false), true);
	#endif
}
float3 SSS_WorldToObjectDir(float3 v)
{
	#if _SSS_BIRP
		return TransformWorldToObjectDir(v);
	#else
		return TransformWorldToObjectDir(v, true);
	#endif
}
float3 SSS_WorldToViewDir(float3 v)
{
	#if _SSS_BIRP
		return TransformWorldToViewDir(v);
	#else
		return TransformWorldToViewDir(v, true);
	#endif
}
float3 SSS_ViewToObjectDir(float3 v)
{
	#if _SSS_URP || _SSS_HDRP
		return SSS_WorldToObjectDir(mul(UNITY_MATRIX_I_V, float4(v, 0.0)).xyz);
	#else
		return SSS_WorldToObjectDir(mul((float3x3)UNITY_MATRIX_I_V, v));
	#endif
}
float3 SSS_ViewToWorldDir(float3 v)
{
	return mul(UNITY_MATRIX_I_V, float4(v, 0.0)).xyz;
}

#if _SSS_NO_DERIVATIVES
	float3 SSS_GetSceneColor(float2 uv) { return float3(0.0, 0.0, 0.0); }
	float3 SSS_GetSceneColorHD(float2 uv) { return SSS_GetSceneColor(uv); }
	float  SSS_GetSceneDepth(float2 uv) { return 0.0; }
#else
	#if _SSS_URP
		float3 SSS_GetSceneColor(float2 uv) { return SHADERGRAPH_SAMPLE_SCENE_COLOR(uv).xyz; }
		float3 SSS_GetSceneColorHD(float2 uv) { return SSS_GetSceneColor(uv); }
	#elif _SSS_HDRP
		float3 SSS_GetSceneColor(float2 uv) { return SHADERGRAPH_SAMPLE_SCENE_COLOR(uv).xyz; }
		float3 SSS_GetSceneColorHD(float2 uv)
		{
			#if defined(REQUIRE_OPAQUE_TEXTURE) && defined(_SURFACE_TYPE_TRANSPARENT) && defined(SHADERPASS) && (SHADERPASS != SHADERPASS_LIGHT_TRANSPORT) && (SHADERPASS != SHADERPASS_PATH_TRACING) && (SHADERPASS != SHADERPASS_RAYTRACING_VISIBILITY) && (SHADERPASS != SHADERPASS_RAYTRACING_FORWARD)
			return SampleCameraColor(uv, 0);
			#endif
			#if defined(REQUIRE_OPAQUE_TEXTURE) && defined(CUSTOM_PASS_SAMPLING_HLSL) && defined(SHADERPASS) && (SHADERPASS == SHADERPASS_DRAWPROCEDURAL || SHADERPASS == SHADERPASS_BLIT)
			return CustomPassSampleCameraColor(uv, 0);
			#endif
			return float3(0.0, 0.0, 0.0);
		}
	#else
		#if defined(UNITY_DECLARE_OPAQUE_TEXTURE_INCLUDED)
			float3 SSS_GetSceneColor(float2 uv) { return SampleSceneColor(uv); }
		#else
			sampler2D _CameraOpaqueTexture; float3 SSS_GetSceneColor(float2 uv) { return tex2D(_CameraOpaqueTexture, uv).xyz; }
		#endif
		float3 SSS_GetSceneColorHD(float2 uv) { return SSS_GetSceneColor(uv); }
	#endif

	float SSS_GetSceneDepth(float2 uv) { return SHADERGRAPH_SAMPLE_SCENE_DEPTH(uv); }
#endif

float3 SSS_GetSceneWorldPosition(float2 screenUV, float sceneDepth)
{
	#if _SSS_BIRP
		float4 clipPos  = float4(screenUV * 2.0f - 1.0f, 0.0f, 1.0f);
		float4 viewPos  = mul(unity_CameraInvProjection, clipPos);
		float3 worldDir = mul((float3x3)UNITY_MATRIX_I_V, viewPos.xyz);
					
		return _WorldSpaceCameraPos + worldDir * LinearEyeDepth(sceneDepth);
	#else
		float4 clipPos = float4(screenUV * 2.0 - 1.0, sceneDepth, 1.0);
					
		#if UNITY_UV_STARTS_AT_TOP
			clipPos.y = -clipPos.y;
		#endif
					
		float4 worldPos = mul(UNITY_MATRIX_I_VP, clipPos);
					
		worldPos.xyz /= worldPos.w;
					
		#if _SSS_HDRP
			worldPos.xyz = GetAbsolutePositionWS(worldPos.xyz);
		#endif
					
		return worldPos.xyz;
	#endif
}

float SSS_GetSceneWorldDistance(float2 screenUV, float sceneDepth)
{
	return distance(_WorldSpaceCameraPos, SSS_GetSceneWorldPosition(screenUV, sceneDepth));
}

float3 SSS_UnpackNormalScale(float4 c, float s)
{
	#if _SSS_BIRP
		return UnpackScaleNormal(c, s);
	#else
		return UnpackNormalScale(c, s);
	#endif
}

struct SSS_VertexData
{
	float  instanceID;
	float3 position;
	float3 normal;
	float3 tangent;
	float4 color;
	float4 texcoord0;
	float4 texcoord1;
	float4 texcoord2;
	float4 texcoord3;
	float4 extraV2F0;
	float4 extraV2F1;
	float4 extraV2F2;
	float4 extraV2F3;
	float4 extraV2F4;
	float4 extraV2F5;
	float4 extraV2F6;
	float4 extraV2F7;
	



};

struct SSS_FragmentData
{
	float3 localSpacePosition;
	float3 localSpaceNormal;
	float3 localSpaceTangent;
	
	float3 worldSpacePosition;
	float3 worldSpaceNormal;
	float3 worldSpaceTangent;
	//float tangentSign;

	float3 worldSpaceViewDir;
	//float3 tangentSpaceViewDir;
	
	float4 texcoord0;
	float4 texcoord1;
	float4 texcoord2;
	float4 texcoord3;
	
	float2 screenUV;
	float4 screenPos;

	float4 vertexColor;
	bool isFrontFace;
	
	float4 extraV2F0;
	float4 extraV2F1;
	float4 extraV2F2;
	float4 extraV2F3;
	float4 extraV2F4;
	float4 extraV2F5;
	float4 extraV2F6;
	float4 extraV2F7;

	float3x3 TBNMatrix;
	



};

struct SSS_SurfaceData
{
	float3 Albedo;
	float  Smoothness;
	float3 Normal;
	float3 Emission;
	float  Occlusion;
	float  Metallic;
	float  Alpha;
};

float2 _CW_PositionCompressionData; // x = start distance, y = max distance limit

#define DEBUG_SCALE 0.001
#define SSS_LOG_K          0.0001
#define SSS_MAX_WORLD_DIST 1e27

float SSS_GetCompressionRatio(float worldDist)
{
    float startDist = _CW_PositionCompressionData.x;
    float maxDist   = _CW_PositionCompressionData.y;

    // no compression nearby
    if (worldDist <= startDist)
        return 1.0;

    float excess = worldDist - startDist;

    float compressedDist =
        startDist +
        (maxDist - startDist) *
        log(1.0 + excess * SSS_LOG_K) /
        log(1.0 + SSS_MAX_WORLD_DIST * SSS_LOG_K);

    return compressedDist / worldDist;
}

float SSS_DecompressWorldDist(float compressedDist)
{
    float startDist = _CW_PositionCompressionData.x;
    float maxDist   = _CW_PositionCompressionData.y;

    if (compressedDist <= startDist)
        return compressedDist;

    float t =
        saturate(
            (compressedDist - startDist) /
            (maxDist - startDist)
        );

    return startDist +
        (
            exp(
                t *
                log(1.0 + SSS_MAX_WORLD_DIST * SSS_LOG_K)
            ) - 1.0
        ) / SSS_LOG_K;
}

float3 SSS_CompressWorld(float3 worldPos, float3 worldCenter, float worldRadius, out float ratio) 
{
    float3 worldDelta = worldPos - _WorldSpaceCameraPos;
    float  farDist    = distance(_WorldSpaceCameraPos, worldCenter) + worldRadius;
    
    ratio = SSS_GetCompressionRatio(farDist);
    
    return _WorldSpaceCameraPos + worldDelta * ratio;
}

float3 SSS_CompressWorld(float3 worldPos) 
{
    float3 worldDelta = worldPos - _WorldSpaceCameraPos;
    float  worldDist  = length(worldDelta);
    
    return _WorldSpaceCameraPos + worldDelta * SSS_GetCompressionRatio(worldDist);
}

float3 SSS_DecompressWorld(float3 worldPos)
{
    float3 worldDelta = worldPos - _WorldSpaceCameraPos;
    float  worldDist  = length(worldDelta);
    
    return worldDist > 0.001 ? _WorldSpaceCameraPos + (worldDelta / worldDist) * SSS_DecompressWorldDist(worldDist) : _WorldSpaceCameraPos;
}

float3 SSS_DecompressWorld(float3 worldPos, float ratio) 
{
    float3 worldDelta = worldPos - _WorldSpaceCameraPos;
    return _WorldSpaceCameraPos + worldDelta / ratio;
}

float SSS_ElongateDistance(float worldDist)
{
	float minDist = 10000.0;
	float maxDist = 100000.0;
	float scale   = saturate((worldDist - minDist) / (maxDist - minDist));

	return worldDist * lerp(1.0, 1.2, scale);
}
#define SGT_OPACITY_STEPS  5

float4x4        _SGT_SkyToWorld;
float4x4        _SGT_WorldToSky;
float4          _SGT_SkyColor;
float           _SGT_SkyBrightness;
float4          _SGT_SkyAmbientTint;
float           _SGT_SkyExposure;
float4          _SGT_SkyRadius;
float4          _SGT_SkyAltitudeData;
float           _SGT_SkyDensity;
float4          _SGT_SkyMieWeight;
float4          _SGT_SkyMieData;
sampler3D       _SGT_SkyRadianceTex;
float           _SGT_SkyRadianceLod;
sampler2D       _SGT_SkyLightingTex;
sampler2D       _SGT_SkyAlbedoTex;
float           _SGT_SkyRevealStars;
float           _SGT_SkyDepthOpaque;

float     _SGT_Volumetrics_Downscale; // Global
float     _SGT_Volumetrics_NearClip; // Global
Texture2D _SGT_Volumetrics_ColorTex; // Global
float4    _SGT_Volumetrics_ColorSize; // Global
Texture2D _SGT_Volumetrics_DepthTex; // Global
float2    _SGT_Volumetrics_DepthSize; // Global

SamplerState my_linear_clamp_sampler;
SamplerState my_point_clamp_sampler;

sampler2D _SGT_BlueNoiseTex; // Global
float     _SGT_Frame; // Global
float3    _SGT_AmbientColor; // Global
	
float SGT_DitherBlue(float2 screenUV)
{
	float2 pixel = floor(screenUV * _ScreenParams.xy);
	float  noise = tex2D(_SGT_BlueNoiseTex, pixel / 64.0f).r;
	return frac(noise + _SGT_Frame / sqrt(0.5f));
}

float SGT_DitherFast(float2 screenUV)
{
    float2 R2 = float2(0.7548776662, 0.5698402909);
    float2 p  = screenUV * _ScreenParams.xy + frac(_Time.y * R2) * 256.0;
    return frac(52.9829189 * frac(dot(p, float2(0.06711056, 0.00583715))));
}
	
float2 SGT_DirectionToEquirectangular(float3 dir)
{
	dir = normalize(dir);
	float u = atan2(dir.z, dir.x) / (2.0 * 3.141592653) + 0.5;
	float v = asin(clamp(dir.y, -1.0, 1.0)) / 3.141592653 + 0.5;
	return float2(u, v);
}

float3 SGT_SphereTest(float3 ray, float3 rayD, float radius)
{
	float B = -dot(ray, rayD);
	float C = dot(ray, ray) - radius * radius;
	float D = B * B - C;
	float E = sqrt(max(D, 0.0f));
	return float3(B - E, B + E, D);
}

float SGT_InverseLerp(float a, float b, float v)
{
	return (v - a) / (b - a);
}

float3 SGT_CalculateMie(float3 ray, float mie)
{
	return normalize(ray * _SGT_SkyMieWeight.xyz + 1e-6) * mie;
}

float SGT_GetPitch01(float3 rayDir, float3 up, float altitude01)
{
	float power = 2.0f;
	float eyeHeight = _SGT_SkyRadius.x + altitude01 * (_SGT_SkyRadius.y - _SGT_SkyRadius.x);
	float horizonAngleFromDown = -acos(clamp(_SGT_SkyRadius.x / max(eyeHeight, 0.00001f), -1.0f, 1.0f));
	float rayAng = asin(clamp(dot(up, rayDir), -1.0f, 1.0f));
	float sun01;

	if (rayAng < horizonAngleFromDown)
	{
		sun01 = SGT_InverseLerp(-3.141592653 * 0.5f, horizonAngleFromDown, rayAng);
		sun01 = 1.0f - pow(1.0f - sun01, 1.0f / power);
		sun01 = sun01 * 0.5f;
	}
	else
	{
		sun01 = SGT_InverseLerp(horizonAngleFromDown, 3.141592653 * 0.5f, rayAng);
		sun01 = pow(sun01, 1.0f / power);
		sun01 = sun01 * 0.5f + 0.5f;
	}

	return sun01;
}

float3 GetLutCoord(float3 rayPos, float3 rayDir, float3 sunDir, float dither)
{
	float3 up         = normalize(rayPos);
	float  eyeHeight  = length(rayPos);
	float  altitude01 = saturate(SGT_InverseLerp(_SGT_SkyRadius.z, 1.0f, eyeHeight));
	float  pitch01    = SGT_GetPitch01(rayDir, up, altitude01);
	float  sun01      = SGT_GetPitch01(sunDir, up, altitude01);
	
	pitch01 += (dither - 0.5f) / 128.0f;
	sun01   += (dither - 0.5f) /  64.0f;
	
	return float3(pitch01, altitude01, sun01);
}

float2 SGT_GetDensity2(float3 pos, float weightMul)
{
	float alt01 = saturate((1.0f - length(pos)) * _SGT_SkyAltitudeData.z);

	return pow(alt01, _SGT_SkyAltitudeData.xy * weightMul) * _SGT_SkyDensity;
}

float SGT_GetOpacity(float3 rayPos, float3 rayDir, float rayMax, float weightMul)
{
	float2 opticalDepth = 0.0f;
	float  rayStep      = rayMax / SGT_OPACITY_STEPS;
	float  t            = rayStep * 0.5f;
	
	for (int i = 0; i < SGT_OPACITY_STEPS; i++)
	{
		float3 pos = rayPos + rayDir * t;

		opticalDepth += SGT_GetDensity2(pos, weightMul) * rayStep;
		
		t += rayStep;
	}
	
	return 1.0f - exp((opticalDepth.x + opticalDepth.y) * -20.0f);
}

float4 SGT_SampleLUT(float3 coord)
{
	return tex3Dlod(_SGT_SkyRadianceTex, float4(coord, _SGT_SkyRadianceLod));
}

float3 SGT_AtmosphereColor(float3 rayPos, float3 sunDir, float dither)
{
	float3 up         = normalize(rayPos);
	float  eyeHeight  = length(rayPos);
	float  altitude01 = saturate(SGT_InverseLerp(_SGT_SkyRadius.z, 1.0f, eyeHeight));
	float  sun01      = 0.5f + 0.5f * dot(sunDir, up);
		
	return tex2Dlod(_SGT_SkyLightingTex, float4(sun01, altitude01, 0.0f, 0.0f)).xyz;
}

float3 SGT_UnderwaterAtmosphere(float3 rayPos, float3 rayDir, float3 sunDir, float exposure, float dither)
{
	float3 coord = GetLutCoord(rayPos, rayDir, sunDir, dither); coord.x = 1.0f;
	float4 ray   = SGT_SampleLUT(coord);
	float3 mie   = SGT_CalculateMie(ray.xyz, ray.w);
	float3 color = ray.xyz + mie;
	
	#if _SSS_HDRP
		color *= exposure * 0.25f;
	#else
		color = 1.0 - exp(-color * exposure);
	#endif
	
	return saturate(_SGT_SkyColor.xyz * color * _SGT_SkyBrightness);
}

float4 SGT_Atmosphere(float3 rayPos, float3 rayDir, float rayFar, float rayMax, float3 sunDir, float exposure, float dither, float mieMul, float weightMul)
{
	// 1. Calculate opacity FIRST so we can use it for blending
	float opacity = SGT_GetOpacity(rayPos, rayDir, rayMax, weightMul);

	float3 coord  = GetLutCoord(rayPos, rayDir, sunDir, dither);
	float2 mieOff = 0.0f;
	
	if (rayMax * 1.02f <= rayFar) // If ground
	{
		// 2. Store the true, un-clamped sky coordinate
		float trueCoordX = coord.x;
		
		// 3. Calculate the clamped "below horizon" coordinate (original behavior)
		float clampedCoordX = min(coord.x, 0.5f - (1.0f / 128.0f));
		clampedCoordX = lerp(0.5f - (1.0f / 128.0f), clampedCoordX, coord.y * coord.y);
		
		// 4. Smoothly blend towards the true sky color as fog density/opacity increases
		coord.x = lerp(clampedCoordX, trueCoordX, opacity);
		mieOff = lerp(0.2f, 0.0f, opacity);
	}
	else
	{
		//coord.x = max(coord.x, 0.5f + (1.0f / 128.0f));
	}
	
	float4 ray    = SGT_SampleLUT(coord);
	float3 mie    = SGT_CalculateMie(ray.xyz, ray.w);
	float2 g      = lerp(_SGT_SkyMieData.xy, 0.0f, mieOff);
	float  c      = dot(rayDir, sunDir);
	float  phaseR = 3.0f / (16.0f * 3.141592653f) * (1.0f + c * c);
	float2 phaseM = 3.0f / (8.0f * 3.141592653f) * ((1.0f - g * g) * (c * c + 1.0f)) / (pow(abs(1.0f + g * g - 2.0f * c * g), 1.5f) * (2.0f + g * g));
	float3 color  = ray.xyz * phaseR + mie * dot(phaseM, 1) * mieMul;
	
	#if _SSS_HDRP
		color *= exposure * 0.25f;
	#else
		color = 1.0 - exp(-color * exposure);
	#endif
	
	#if _SSS_HDRP
		color += _SGT_AmbientColor.xyz * _SGT_SkyAmbientTint.xyz * 0.25;
	#else
		color += _SGT_AmbientColor.xyz * _SGT_SkyAmbientTint.xyz;
	#endif

	#if _SGT_SOLID_CORE_OFF // If the atmosphere below InnerMeshRadius isn't solid, then continue to darken because the LUT doesn't extend any farther
		float eyeHeight     = length(rayPos);
		float inner         = _SGT_SkyRadius.z;
		float interiorDepth = saturate((_SGT_SkyRadius.z - eyeHeight) / _SGT_SkyRadius.z);

		if (interiorDepth > 0.0)
		{
			float extinction = exp(-interiorDepth * 20.0 * _SGT_SkyDensity);

			color *= extinction;
			opacity = lerp(opacity, 1.0f, saturate(interiorDepth * 100.0f));
		}
	#endif
	
	return float4(saturate(_SGT_SkyColor.xyz * color * _SGT_SkyBrightness), opacity);
}

// Global cached light properties
float3 _SGT_CachedLightDirection;
float3 _SGT_CachedLightColor;

float3 SGT_SampleSkyReflection(float3 ocam, float3 odir)
{
    // 2. Intersect with the Atmosphere Shell (Outer radius is 1.0 in sky space)
    float3 odst = SGT_SphereTest(ocam, odir, 1.0f);
    odst.xy = max(odst.xy, 0.0f); // Ensure distances are ahead of ray
    
    float3 rayPos = ocam + odir * odst.x;
    float3 rayDir = odir;
    
    // 3. Determine Atmosphere Limits
    float rayFar = SGT_SphereTest(rayPos, rayDir, 1.0f).y; // Distance to sky exit
    float rayMax = odst.y - odst.x;                       // Max scattering distance
    
    // Optional: Intersect with the planet core so reflections don't "see through" the world
    #if _SGT_SOLID_CORE
        float3 ihit = SGT_SphereTest(ocam, odir, _SGT_SkyRadius.w);
        if (ihit.z > 0.0f && ihit.x > 0.0f) {
            rayMax = min(ihit.x - odst.x, rayMax);
        }
    #endif

    // 4. Calculate Sun Direction in Sky Space
    float3 sunDir = normalize(mul(_SGT_WorldToSky, float4(_SGT_CachedLightDirection, 0.0f)).xyz);
    
    // 5. Sample the Atmosphere
    // dither is set to 0.0 to keep reflections smooth; mieMul is 1.0
    float4 sky = SGT_Atmosphere(rayPos, rayDir, rayFar, rayMax, sunDir, _SGT_SkyExposure, 0.0, 1.0, 1.0);
    
    // 6. Apply cached light intensity
    return sky.rgb * _SGT_CachedLightColor;
}

float4 SGT_SimpleAtmosphere(float3 worldPos, float3 worldHit, float mieMul, float weightMul)
{
	float dither   = 0.5f;
	
	float3 ocam = mul(_SGT_WorldToSky, float4(worldPos, 1.0f)).xyz;
	float3 ofar = mul(_SGT_WorldToSky, float4(worldHit, 1.0f)).xyz;
	float  omax = distance(ocam, ofar);
	float3 odir = normalize(ofar - ocam);
	float3 odst = SGT_SphereTest(ocam, odir, 1.0f);
	
	odst.xy = max(odst.xy, 0.0f);
	
	float3 rayPos = ocam + odir * odst.x;
	float3 rayDir = odir;
	float  rayFar = SGT_SphereTest(rayPos, rayDir, 1.0f).y;
	float  rayMax = min(odst.y, omax) - odst.x;
	
	#if _SGT_LIGHTING
		float3 sunDir = normalize(mul(_SGT_WorldToSky, float4(_SGT_LightDirection[0].xyz, 0.0f)).xyz);
	#else
		float3 sunDir = normalize(rayPos);
	#endif
	
	return SGT_Atmosphere(rayPos, rayDir, rayFar, rayMax, sunDir, _SGT_SkyExposure, dither, mieMul, weightMul);
}

float3 CW_Depth2World(float viewDepth, float3 viewDir)
{
	return _WorldSpaceCameraPos + viewDir * viewDepth;
}
	
bool CW_DepthInBounds(float viewDepth, float3 viewDir)
{
	float3 wPos = CW_Depth2World(viewDepth, viewDir);
	float3 oPos = mul(_SGT_WorldToSky, float4(wPos, 1.0f)).xyz;
		
	return length(oPos) < 1.2f;
}
	
void CW_SampleVolumetricsDefault(float2 screenUV, float3 viewDir, out float4 outColor, out float outDepth)
{
	float4 centerColor = _SGT_Volumetrics_ColorTex.Sample(my_linear_clamp_sampler, screenUV);
	float  centerDepth = _SGT_Volumetrics_DepthTex.Sample(my_linear_clamp_sampler, screenUV).x;
		
	if (CW_DepthInBounds(centerDepth, viewDir) == true)
	{
		outColor = centerColor;
		outDepth = centerDepth;
	}
	else
	{
		outColor = 0;
		outDepth = 0;
	}
}

void CW_SampleVolumetricsDefault2(float2 screenUV, float3 viewDir, out float4 outColor, out float outDepth)
{
	outColor = _SGT_Volumetrics_ColorTex.Sample(my_linear_clamp_sampler, screenUV);
	outDepth = _SGT_Volumetrics_DepthTex.Sample(my_linear_clamp_sampler, screenUV).x;
}
	
void CW_SampleVolumetrics(float2 screenUV, float3 viewDir, float sceneDepth, out float4 outColor, out float outDepth)
{
	outColor = 0;
	outDepth = 0;
	//CW_SampleVolumetricsDefault(screenUV, viewDir, outColor, outDepth);return;
		
	if (_SGT_Volumetrics_Downscale > 1)
	{
		float2 pixelF   = screenUV * _SGT_Volumetrics_ColorSize.xy - 0.5f;
		float2 pixelI   = floor(pixelF);
		float2 snappedF = pixelF - pixelI;
			
		float2 coord0 = (pixelI + float2(0.5f, 0.5f)) * _SGT_Volumetrics_ColorSize.zw;
		float2 coord1 = (pixelI + float2(1.5f, 0.5f)) * _SGT_Volumetrics_ColorSize.zw;
		float2 coord2 = (pixelI + float2(0.5f, 1.5f)) * _SGT_Volumetrics_ColorSize.zw;
		float2 coord3 = (pixelI + float2(1.5f, 1.5f)) * _SGT_Volumetrics_ColorSize.zw;
			
		float depth0 = _SGT_Volumetrics_DepthTex.Sample(my_point_clamp_sampler, coord0).x;
		float depth1 = _SGT_Volumetrics_DepthTex.Sample(my_point_clamp_sampler, coord1).x;
		float depth2 = _SGT_Volumetrics_DepthTex.Sample(my_point_clamp_sampler, coord2).x;
		float depth3 = _SGT_Volumetrics_DepthTex.Sample(my_point_clamp_sampler, coord3).x;
			
		float4 color0 = _SGT_Volumetrics_ColorTex.Sample(my_point_clamp_sampler, coord0);
		float4 color1 = _SGT_Volumetrics_ColorTex.Sample(my_point_clamp_sampler, coord1);
		float4 color2 = _SGT_Volumetrics_ColorTex.Sample(my_point_clamp_sampler, coord2);
		float4 color3 = _SGT_Volumetrics_ColorTex.Sample(my_point_clamp_sampler, coord3);
			
		float w0 = CW_DepthInBounds(depth0, viewDir) * (depth0 < sceneDepth) * color0.w * (1.0 - snappedF.x) * (1.0 - snappedF.y);
		float w1 = CW_DepthInBounds(depth1, viewDir) * (depth1 < sceneDepth) * color1.w * snappedF.x * (1.0 - snappedF.y);
		float w2 = CW_DepthInBounds(depth2, viewDir) * (depth2 < sceneDepth) * color2.w * (1.0 - snappedF.x) * snappedF.y;
		float w3 = CW_DepthInBounds(depth3, viewDir) * (depth3 < sceneDepth) * color3.w * snappedF.x * snappedF.y;
			
		float wt = w0 + w1 + w2 + w3;
			
		if (wt > 0.0f)
		{
			outColor = (color0 * w0 + color1 * w1 + color2 * w2 + color3 * w3) / wt;
			outDepth = (depth0 * w0 + depth1 * w1 + depth2 * w2 + depth3 * w3) / wt;
		}
		else
		{
			return;
		}
	}
	else
	{
		float2 coord0 = screenUV;
		float  depth0 = _SGT_Volumetrics_DepthTex.Sample(my_point_clamp_sampler, coord0).x;
		float4 color0 = _SGT_Volumetrics_ColorTex.Sample(my_point_clamp_sampler, coord0);
		float  w0     = CW_DepthInBounds(depth0, viewDir) * (depth0 < sceneDepth);
		
		if (w0 > 0.0f)
		{
			outColor = color0;
			outDepth = depth0;
		}
		else
		{
			return;
		}
	}
}






#pragma instancing_options procedural:SetupInstancing
#if _SSS_PASS_SHADOWCASTER || _SSS_PASS_META
	#pragma multi_compile_instancing // For some reason the ShadowCaster and Meta pass in BIRP doesn't have this line
#endif


#define BATCH_CAPACITY 35
#define VERTEX_COUNT 243

float4 _CwSize;
float4 _CwAtlas;
float4 _CwWeights[VERTEX_COUNT];
float4 _CwCoords[VERTEX_COUNT];

float4 _CwOrigins[BATCH_CAPACITY];
float4 _CwPositionsA[BATCH_CAPACITY];
float4 _CwPositionsB[BATCH_CAPACITY];
float4 _CwPositionsC[BATCH_CAPACITY];

sampler2D DataP;
sampler2D DataA;
sampler2D DataN;

float3 _CwOffset;

float4x4 _CwObjectToWorld;
float4x4 _CwWorldToObject;

float _SGT_Occlusion;

// CLOUDS
sampler2D _SGT_CloudShadowTex;
float3    _SGT_CloudShadowDirection;
float4    _SGT_CloudShadowOpacity;
float4x4  _SGT_CloudShadowMatrix;

// OCEAN FADE
float  _SGT_OceanFade;
float2 _SGT_OceanDensity;
float3 _SGT_OceanColor;
float  _SGT_OceanSmoothness;
float4 _SGT_OceanLightDirection;

float _SGT_OceanRadius;

void SetupInstancing()
{
	#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
		#ifdef unity_ObjectToWorld
			#undef unity_ObjectToWorld
		#endif

		#ifdef unity_WorldToObject
			#undef unity_WorldToObject
		#endif
		
		unity_ObjectToWorld = _CwObjectToWorld;
		unity_WorldToObject = _CwWorldToObject;
	#endif
}

float SGT_SampleCloudDensity(float3 wpos)
{
	float3 cpos  = mul(_SGT_CloudShadowMatrix, float4(wpos, 1.0)).xyz;
	float3 chit  = SGT_SphereTest(cpos, _SGT_CloudShadowDirection, 1.0);
	float2 uv    = SGT_DirectionToEquirectangular(normalize(cpos + chit.y * _SGT_CloudShadowDirection));
	
	return saturate(dot(tex2D(_SGT_CloudShadowTex, uv), _SGT_CloudShadowOpacity));
}

void SSS_Vert(inout SSS_VertexData v)
{
	float vertexIndex = v.position.x;
	float squareIndex = v.position.y;
	float batchIndex  = v.instanceID;
	
	float  batchCol = batchIndex % _CwAtlas.z;
	float  batchRow = floor(batchIndex / _CwAtlas.z);
	float3 origin   = _CwOrigins[batchIndex].xyz;
	float3 weights  = _CwWeights[vertexIndex].xyz;
	float2 coord    = _CwCoords[vertexIndex].xy; coord.x /= 3.0;
	float3 position = _CwPositionsA[batchIndex].xyz * weights.x + _CwPositionsB[batchIndex].xyz * weights.y + _CwPositionsC[batchIndex].xyz * weights.z;
	
	v.position.xyz = _CwOffset + origin + tex2Dlod(DataP, float4(vertexIndex * _CwSize.x, batchIndex * _CwSize.y, 0.0, 0.0)).xyz;
	
	float3 ocam = mul(_CwWorldToObject, float4(_WorldSpaceCameraPos - _CwOffset, 1.0)).xyz;
	
	#if _SGT_SHAPE_SQUARE
		v.normal  = float3(0,1,0);
		v.tangent = float4(1,0,0,-1).xyz;
		
		v.extraV2F0.w = ocam.y;
	#elif _SGT_SHAPE_SPHERE
		v.normal = normalize(position);
		
		if (position.x == 0.0 && position.z == 0.0)
		{
			position.xz += 0.00000001;
		}
		
		v.tangent = float4(normalize(cross(float3(0,1,0), normalize(position))), -1.0).xyz;
		
		v.extraV2F0.w = length(ocam) - _SGT_OceanRadius;
	#endif
	
	#if _SGT_OCEAN_FADE
		if (_SGT_OceanFade > 0.0 && length(v.position.xyz - _CwOffset) < _SGT_OceanRadius)
		{
			float3 N = normalize(position); // sphere normal
			float3 V = normalize(_WorldSpaceCameraPos - v.position.xyz);
			float  F = pow(abs(1.0 - saturate(dot(N, V))), 2.0);

			float3 oceanPos = _CwOffset + N * _SGT_OceanRadius;
			v.position.xyz = lerp(v.position.xyz, oceanPos, F);
		}

	#endif
	
	// Calc UV
	float2 pixelS = _CwAtlas.xy * _CwAtlas.zw;
	float  pixelX = batchCol * _CwAtlas.x + coord.x * (_CwAtlas.x - 3.0) + 0.5 + squareIndex * (_CwAtlas.x / 3.0);
	float  pixelY = batchRow * _CwAtlas.y + coord.y * (_CwAtlas.y - 1.0) + 0.5;
	
	v.extraV2F0.x = pixelX / pixelS.x;
	v.extraV2F0.y = pixelY / pixelS.y;
	
	#if CW_COMPRESS_POSITIONS
		float3 wpos = mul(_CwObjectToWorld, float4(v.position, 1.0)).xyz;
		v.extraV2F0.z = distance(_WorldSpaceCameraPos, wpos);
		wpos = SSS_CompressWorld(wpos);
		v.position = mul(_CwWorldToObject, float4(wpos, 1.0)).xyz;
	#endif
}

float3 SGT_GetColor(float3 wnormal, float3 wlight)
{
	return saturate(dot(wnormal, wlight));
}

static const float WATER_DEPTH_SHALLOW = 30.0;
static const float WATER_DEPTH_MAX = 3000.0;

float SGT_DecodeWaterDepth(float encoded)
{
	float shallow = encoded * (WATER_DEPTH_SHALLOW * 2.0);
    float deep = WATER_DEPTH_SHALLOW + (encoded - 0.5) * ((WATER_DEPTH_MAX - WATER_DEPTH_SHALLOW) * 2.0);
    float isDeep = step(0.5, encoded);
    return lerp(shallow, deep, isDeep);
}

float3 SGT_ApplyOceanColor(
    float3 terrainColor,
    float  waterDepth01,
    float3 waterColor,
    float2 waterDensity,
    float  waterBlend,
    float3 ambientColor)
{
    float depth = SGT_DecodeWaterDepth(waterDepth01);
    float3 w = float3(1.0, 0.2, 0.1);

	float3 e0 = w * waterDensity.x;
	float3 e1 = w * waterDensity.y;

	float3 floorT0 = exp(-e0 * depth);
	float3 floorT1 = exp(-e1 * depth);
	
	float3 volumeT0 = floorT0 * floorT0 * floorT0;
	float3 volumeT1 = floorT1 * floorT1 * floorT1;

	float3 scatter = waterColor * ambientColor;

	float3 col0 = terrainColor * floorT0 + scatter * (1.0 - volumeT0);
	float3 col1 = terrainColor * floorT1 + scatter * (1.0 - volumeT1);

	return lerp(col0, col1, waterBlend);
}

float SGT_Bayer4x4(float2 screenPos)
{
	uint2 p = uint2(screenPos * _ScreenParams.xy) % 4;
	float bayer[16] = { 0,  8,  2, 10, 12,  4, 14,  6, 3, 11,  1,  9, 15,  7, 13,  5 };
	return (bayer[p.x + p.y * 4] + 0.5) / 16.0;
}

float SGT_Fresnel(float3 dir, float3 normal, float airIOR, float waterIOR)
{
	float cosTheta = clamp(dot(dir, normal), 0.0, 1.0);
	float F0       = pow(abs((waterIOR / airIOR - 1.0) / (waterIOR / airIOR + 1.0)), 2.0);

	return F0 + (1.0 - F0) * pow(abs(1.0 - cosTheta), 5.0);
}

void SSS_Frag(inout SSS_SurfaceData o, inout SSS_FragmentData d)
{
	#if CW_COMPRESS_POSITIONS
		d.worldSpacePosition = _WorldSpaceCameraPos - d.worldSpaceViewDir * d.extraV2F0.z;
	#endif
	
	float4 dataA = tex2Dlod(DataA, float4(d.extraV2F0.xy,0,0));
	float4 dataN = tex2Dlod(DataN, float4(d.extraV2F0.xy,0,0)); 
	float2 tnorm = dataN.xy * 2.0 - 1.0;
	
	tnorm.x = -tnorm.x; // Don't set negative tangent sign, so do it manually
	
	o.Albedo     = dataA.xyz;
	o.Occlusion  = _SGT_Occlusion;
	o.Emission   = dataA.xyz * dataN.z;
	o.Smoothness = dataN.w;
	o.Metallic   = 0.0;
	o.Normal     = float3(tnorm, sqrt(1.0 - saturate(dot(tnorm, tnorm))));
	o.Alpha      = dataN.x != 0.0 && dataN.y != 0.0;
	
	float localEyeHeight = d.extraV2F0.w;
	
	#if _SGT_OCEAN_FADE
		float  fadeTransition = step(SGT_Bayer4x4(d.screenUV), _SGT_OceanFade);
		float  cameraDistance = distance(d.worldSpacePosition, _WorldSpaceCameraPos);
		float  cameraDist01   = 1.0 - pow(abs(saturate(1.0 - cameraDistance / _SGT_OceanRadius)), 8.0);
		float3 oceanColor     = SGT_ApplyOceanColor(o.Albedo, dataA.w, _SGT_OceanColor, _SGT_OceanDensity, cameraDist01, 1.0);
		float  fade           = saturate(dataA.w > 0) * fadeTransition;

		o.Albedo     = lerp(o.Albedo, oceanColor, fade);
		o.Smoothness = lerp(o.Smoothness, _SGT_OceanSmoothness, fade);
		o.Normal     = lerp(o.Normal, float3(0.0, 0.0, 1.0), fade);
		o.Occlusion  = lerp(o.Occlusion, 0.15, fade);

		#if _SGT_SKY
			float  airIOR   = 1.0;
			float  waterIOR = 1.33;
			float3 opos     = mul(_SGT_WorldToSky, float4(d.worldSpacePosition, 1.0)).xyz;
			float3 onrm     = normalize(opos);
			float3 oview    = normalize(mul(_SGT_WorldToSky, float4(d.worldSpaceViewDir, 0.0)).xyz);
			float3 odir     = reflect(oview, onrm);
	
			o.Emission = SGT_SampleSkyReflection(opos, -odir) * fade * SGT_Fresnel(oview, onrm, airIOR, waterIOR);
			#if _SSS_HDRP
				o.Emission *= 120000;
			#endif
		#endif
		//o.Emission +=  _SGT_AmbientColor.xyz * 0.02;
	#endif
	
	#if _SGT_CLOUDS
		float shadow = SGT_SampleCloudDensity(d.worldSpacePosition);
		
		o.Albedo    = lerp(o.Albedo   , 0.0, shadow);
		o.Occlusion = lerp(o.Occlusion, 0.0, shadow);
	#endif
}


void Vert_float
	(
	float  iInstanceID,
	float3 iPosition,
	float3 iNormal,
	float3 iTangent,
	float4 iColor,
	float4 iTexcoord0,
	float4 iTexcoord1,
	float4 iTexcoord2,
	float4 iTexcoord3,

	out float3 oPosition,
	out float3 oNormal,
	out float3 oTangent,
	out float4 oExtraV2F0,
	out float4 oExtraV2F1,
	out float4 oExtraV2F2,
	out float4 oExtraV2F3,
	out float4 oExtraV2F4,
	out float4 oExtraV2F5,
	out float4 oExtraV2F6,
	out float4 oExtraV2F7
	)
{
	SSS_VertexData v = (SSS_VertexData)0;
	
	v.instanceID = iInstanceID;
	v.position   = iPosition;
	v.normal     = iNormal;
	v.tangent    = iTangent;
	v.color      = iColor;
	v.texcoord0  = iTexcoord0;
	v.texcoord1  = iTexcoord1;
	v.texcoord2  = iTexcoord2;
	v.texcoord3  = iTexcoord3;
	v.extraV2F0  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F1  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F2  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F3  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F4  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F5  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F6  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F7  = float4(0.0, 0.0, 0.0, 0.0);
	
	SSS_Vert(v);
	
	oPosition  = v.position;
	oNormal    = v.normal;
	oTangent   = v.tangent;
	oExtraV2F0 = v.extraV2F0;
	oExtraV2F1 = v.extraV2F1;
	oExtraV2F2 = v.extraV2F2;
	oExtraV2F3 = v.extraV2F3;
	oExtraV2F4 = v.extraV2F4;
	oExtraV2F5 = v.extraV2F5;
	oExtraV2F6 = v.extraV2F6;
	oExtraV2F7 = v.extraV2F7;
}

void Frag_float
	(
	inout float3 iPosition,
	inout float3 iNormal,
	inout float3 iTangent,
	bool   iIsFrontFace,
	float4 iColor,
	float4 iTexcoord0,
	float4 iTexcoord1,
	float4 iTexcoord2,
	float4 iTexcoord3,
	float4 iExtraV2F0,
	float4 iExtraV2F1,
	float4 iExtraV2F2,
	float4 iExtraV2F3,
	float4 iExtraV2F4,
	float4 iExtraV2F5,
	float4 iExtraV2F6,
	float4 iExtraV2F7,

	out float4x4 oExtra,
	out float3   oAlbedo,
	out float    oSmoothness,
	out float3   oNormal,
	out float3   oEmission,
	out float    oOcclusion,
	out float    oMetallic,
	out float    oAlpha
	)
{
	SSS_SurfaceData  s = (SSS_SurfaceData)0;
	SSS_FragmentData d = (SSS_FragmentData)0;
	
	s.Albedo = 1.0;
	s.Smoothness = 0.5;
	s.Normal = float3(0.0, 0.0, 1.0);
	s.Emission = float3(0.0, 0.0, 0.0);
	s.Occlusion = 0.0;
	s.Metallic = 0.0;
	s.Alpha = 1.0;
	
	iPosition = SSS_WorldToAbsolute(iPosition);
	
	d.localSpacePosition = SSS_WorldToObject(iPosition);
	d.localSpaceNormal   = normalize(SSS_WorldToObjectDir(iNormal));
	d.localSpaceTangent  = normalize(SSS_WorldToObjectDir(iTangent));
	
	d.worldSpacePosition = iPosition;
	d.worldSpaceNormal   = iNormal;
	d.worldSpaceTangent  = iTangent;
	//d.tangentSign;
	
	d.worldSpaceViewDir  = normalize(_WorldSpaceCameraPos - d.worldSpacePosition);
	//d.tangentSpaceViewDir;
	
	d.texcoord0 = iTexcoord0;
	d.texcoord1 = iTexcoord1;
	d.texcoord2 = iTexcoord2;
	d.texcoord3 = iTexcoord3;
	
	d.screenPos = float4(SSS_WorldToScreen(iPosition), 1.0);
	d.screenUV  = d.screenPos.xy;

	d.vertexColor = iColor;
	d.isFrontFace = iIsFrontFace;
	
	d.extraV2F0 = iExtraV2F0;
	d.extraV2F1 = iExtraV2F1;
	d.extraV2F2 = iExtraV2F2;
	d.extraV2F3 = iExtraV2F3;
	d.extraV2F4 = iExtraV2F4;
	d.extraV2F5 = iExtraV2F5;
	d.extraV2F6 = iExtraV2F6;
	d.extraV2F7 = iExtraV2F7;

	d.TBNMatrix = float3x3(d.worldSpaceTangent, normalize(cross(d.worldSpaceNormal, d.worldSpaceTangent)), d.worldSpaceNormal);
	
	SSS_Frag(s, d);
	
	iPosition = SSS_AbsoluteToWorld(d.worldSpacePosition); iNormal = d.worldSpaceNormal; iTangent = d.worldSpaceTangent; // Write back
	
	oExtra      = float4x4(d.extraV2F0, d.extraV2F1, d.extraV2F2, d.extraV2F3);
	oAlbedo     = s.Albedo;
	oSmoothness = s.Smoothness;
	oNormal     = s.Normal;
	oEmission   = s.Emission;
	oOcclusion  = s.Occlusion;
	oMetallic   = s.Metallic;
	oAlpha      = s.Alpha;
}



	#pragma shader_feature_local _SGT_SHAPE_SQUARE _SGT_SHAPE_SPHERE
	#pragma shader_feature_local _SGT_CLOUDS_OFF _SGT_CLOUDS
	#pragma shader_feature_local _SGT_OCEAN_FADE_OFF _SGT_OCEAN_FADE
	#pragma shader_feature_local _SGT_SKY_OFF _SGT_SKY
	#pragma multi_compile __ CW_COMPRESS_POSITIONS



// Graph Functions
// GraphFunctions: <None>

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
float4 extraV2F0;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
float4 _UV_ccc703dc3bdd420e8a8a4aa0607d4ec4_Out_0_Vector4 = IN.uv0;
float4 _UV_46571950324b44e8bf8895f08abd191f_Out_0_Vector4 = IN.uv1;
float4 _UV_ba4ef72275334f08b9b01fedf9da0065_Out_0_Vector4 = IN.uv2;
float4 _UV_255ab3a515d14af686e70c40d04416e6_Out_0_Vector4 = IN.uv3;
float3 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oPosition_0_Vector3;
float3 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oNormal_5_Vector3;
float3 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oTangent_6_Vector3;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F0_7_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F1_8_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F2_13_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F3_14_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F4_15_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F5_16_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F6_17_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F7_18_Vector4;
Vert_float(IN.InstanceID, IN.ObjectSpacePosition, IN.ObjectSpaceNormal, IN.ObjectSpaceTangent, IN.VertexColor, _UV_ccc703dc3bdd420e8a8a4aa0607d4ec4_Out_0_Vector4, _UV_46571950324b44e8bf8895f08abd191f_Out_0_Vector4, _UV_ba4ef72275334f08b9b01fedf9da0065_Out_0_Vector4, _UV_255ab3a515d14af686e70c40d04416e6_Out_0_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oPosition_0_Vector3, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oNormal_5_Vector3, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oTangent_6_Vector3, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F0_7_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F1_8_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F2_13_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F3_14_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F4_15_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F5_16_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F6_17_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F7_18_Vector4);
description.Position = _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oPosition_0_Vector3;
description.Normal = _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oNormal_5_Vector3;
description.Tangent = _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oTangent_6_Vector3;
description.extraV2F0 = _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F0_7_Vector4;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
output.extraV2F0 = input.extraV2F0;
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
float3 BaseColor;
float3 Emission;
float Alpha;
float AlphaClipThreshold;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
float _IsFrontFace_5b03f99e2e7841c3a7d2ae306590b576_Out_0_Boolean = max(0, IN.FaceSign.x);
float4 _UV_c1e39353e82e434da821b9bdc4338e4b_Out_0_Vector4 = IN.uv0;
float4 _UV_64d51b5974bc4ecf849e7307e6de2727_Out_0_Vector4 = IN.uv1;
float4x4 _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oExtra_23_Matrix4;
float3 _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oAlbedo_0_Vector3;
float _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oSmoothness_5_Float;
float3 _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oNormal_6_Vector3;
float3 _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oEmission_7_Vector3;
float _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oOcclusion_8_Float;
float _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oMetallic_9_Float;
float _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oAlpha_10_Float;
Frag_float(IN.WorldSpacePosition, IN.WorldSpaceNormal, IN.WorldSpaceTangent, _IsFrontFace_5b03f99e2e7841c3a7d2ae306590b576_Out_0_Boolean, float4 (0, 0, 0, 0), _UV_c1e39353e82e434da821b9bdc4338e4b_Out_0_Vector4, _UV_64d51b5974bc4ecf849e7307e6de2727_Out_0_Vector4, float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), IN.extraV2F0, float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oExtra_23_Matrix4, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oAlbedo_0_Vector3, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oSmoothness_5_Float, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oNormal_6_Vector3, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oEmission_7_Vector3, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oOcclusion_8_Float, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oMetallic_9_Float, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oAlpha_10_Float);
surface.BaseColor = _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oAlbedo_0_Vector3;
surface.Emission = _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oEmission_7_Vector3;
surface.Alpha = _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oAlpha_10_Float;
surface.AlphaClipThreshold = float(0.5);
return surface;
}

// --------------------------------------------------
// Build Graph Inputs

VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;
    output.uv0 =                                        input.uv0;
    output.uv1 =                                        input.uv1;
    output.uv2 =                                        input.uv2;
    output.uv3 =                                        input.uv3;
    output.VertexColor =                                input.color;
#if UNITY_ANY_INSTANCING_ENABLED
    output.InstanceID =                                 unity_InstanceID;
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
    output.InstanceID =                                 input.instanceID;
#endif

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

    output.extraV2F0 = input.extraV2F0;

    // must use interpolated tangent, bitangent and normal before they are normalized in the pixel shader.
    float3 unnormalizedNormalWS = input.normalWS;
    const float renormFactor = 1.0 / length(unnormalizedNormalWS);


    output.WorldSpaceNormal = renormFactor * input.normalWS.xyz;      // we want a unit length Normal Vector node in shader graph

    // to preserve mikktspace compliance we use same scale renormFactor as was used on the normal.
    // This is explained in section 2.2 in "surface gradient based bump mapping framework"
    output.WorldSpaceTangent = renormFactor * input.tangentWS.xyz;

    output.WorldSpacePosition = input.positionWS;

    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


    output.uv0 = input.texCoord0;
    output.uv1 = input.texCoord1;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
    BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

void BuildAppDataFull(Attributes attributes, VertexDescription vertexDescription, inout appdata_full result)
{
    result.vertex     = float4(attributes.positionOS, 1);
    result.tangent    = attributes.tangentOS;
    result.normal     = attributes.normalOS;
    result.texcoord   = attributes.uv0;
    result.texcoord1  = attributes.uv1;
    result.texcoord2  = attributes.uv2;
    result.texcoord3  = attributes.uv3;
    result.color      = attributes.color;
    result.vertex     = float4(vertexDescription.Position, 1);
    result.normal     = vertexDescription.Normal;
    result.tangent    = float4(vertexDescription.Tangent, 0);
    #if UNITY_ANY_INSTANCING_ENABLED
    result.instanceID = attributes.instanceID;
    #endif
}

void VaryingsToSurfaceVertex(Varyings varyings, inout v2f_surf result)
{
    result.pos = varyings.positionCS;
    result.worldPos = varyings.positionWS;
    result.worldNormal = varyings.normalWS;
    // World Tangent isn't an available input on v2f_surf


    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
    #if UNITY_SHOULD_SAMPLE_SH
    #if !defined(LIGHTMAP_ON)
    #endif
    #endif
    #if defined(LIGHTMAP_ON)
    #endif
    #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        result.fogCoord = varyings.fogFactorAndVertexLight.x;
        COPY_TO_LIGHT_COORDS(result, varyings.fogFactorAndVertexLight.yzw);
    #endif

    DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(varyings, result);
}

void SurfaceVertexToVaryings(v2f_surf surfVertex, inout Varyings result)
{
    result.positionCS = surfVertex.pos;
    result.positionWS = surfVertex.worldPos;
    result.normalWS = surfVertex.worldNormal;
    // viewDirectionWS is never filled out in the legacy pass' function. Always use the value computed by SRP
    // World Tangent isn't an available input on v2f_surf

    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
    #if UNITY_SHOULD_SAMPLE_SH
    #if !defined(LIGHTMAP_ON)
    #endif
    #endif
    #if defined(LIGHTMAP_ON)
    #endif
    #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        result.fogFactorAndVertexLight.x = surfVertex.fogCoord;
        COPY_FROM_LIGHT_COORDS(result.fogFactorAndVertexLight.yzw, surfVertex);
    #endif

    DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(surfVertex, result);
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/LightingMetaPass.hlsl"

ENDHLSL
}
Pass
{
    Name "SceneSelectionPass"
    Tags
    {
        "LightMode" = "SceneSelectionPass"
    }

// Render State
Cull Off

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM
#define _SSS_PASS_SCENESELECTIONPASS 1

#define _SSS_BIRP 1


// Pragmas
#pragma target 3.0
#pragma multi_compile_instancing
#pragma vertex vert
#pragma fragment frag

// Keywords
// PassKeywords: <None>
// GraphKeywords: <None>

// Defines
#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_TEXCOORD0
#define ATTRIBUTES_NEED_TEXCOORD1
#define ATTRIBUTES_NEED_TEXCOORD2
#define ATTRIBUTES_NEED_TEXCOORD3
#define ATTRIBUTES_NEED_COLOR
#define ATTRIBUTES_NEED_INSTANCEID
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_NORMAL_WS
#define VARYINGS_NEED_TANGENT_WS
#define VARYINGS_NEED_TEXCOORD0
#define VARYINGS_NEED_TEXCOORD1
#define VARYINGS_NEED_CULLFACE
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SceneSelectionPass
#define BUILTIN_TARGET_API 1
#define SCENESELECTIONPASS 1
#define _BUILTIN_AlphaClip 1
#define _BUILTIN_ALPHATEST_ON 1
#ifdef _BUILTIN_SURFACE_TYPE_TRANSPARENT
#define _SURFACE_TYPE_TRANSPARENT _BUILTIN_SURFACE_TYPE_TRANSPARENT
#endif
#ifdef _BUILTIN_ALPHATEST_ON
#define _ALPHATEST_ON _BUILTIN_ALPHATEST_ON
#endif
#ifdef _BUILTIN_AlphaClip
#define _AlphaClip _BUILTIN_AlphaClip
#endif
#ifdef _BUILTIN_ALPHAPREMULTIPLY_ON
#define _ALPHAPREMULTIPLY_ON _BUILTIN_ALPHAPREMULTIPLY_ON
#endif


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Shim/Shims.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/LegacySurfaceVertex.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/ShaderGraphFunctions.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
 float4 uv0 : TEXCOORD0;
 float4 uv1 : TEXCOORD1;
 float4 uv2 : TEXCOORD2;
 float4 uv3 : TEXCOORD3;
 float4 color : COLOR;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float3 positionWS;
 float3 normalWS;
 float4 tangentWS;
 float4 texCoord0;
 float4 texCoord1;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
 float4 extraV2F0;
};
struct SurfaceDescriptionInputs
{
 float3 WorldSpaceNormal;
 float3 WorldSpaceTangent;
 float3 WorldSpacePosition;
 float4 uv0;
 float4 uv1;
 float FaceSign;
 float4 extraV2F0;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
 float4 uv0;
 float4 uv1;
 float4 uv2;
 float4 uv3;
 float4 VertexColor;
 uint InstanceID;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
 float4 tangentWS : INTERP0;
 float4 texCoord0 : INTERP1;
 float4 texCoord1 : INTERP2;
 float4 extraV2F0 : INTERP3;
 float3 positionWS : INTERP4;
 float3 normalWS : INTERP5;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
output.tangentWS.xyzw = input.tangentWS;
output.texCoord0.xyzw = input.texCoord0;
output.texCoord1.xyzw = input.texCoord1;
output.extraV2F0.xyzw = input.extraV2F0;
output.positionWS.xyz = input.positionWS;
output.normalWS.xyz = input.normalWS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
output.tangentWS = input.tangentWS.xyzw;
output.texCoord0 = input.texCoord0.xyzw;
output.texCoord1 = input.texCoord1.xyzw;
output.extraV2F0 = input.extraV2F0.xyzw;
output.positionWS = input.positionWS.xyz;
output.normalWS = input.normalWS.xyz;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties
CBUFFER_START(UnityPerMaterial)





CBUFFER_END


// Object and Global properties

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Includes
// UNITY_SHADER_NO_UPGRADE
float3 SSS_HClipToScreen(float4 v)
{
	float3 uv = v.xyz / v.w;
	#if UNITY_UV_STARTS_AT_TOP
		uv.y = -uv.y;
	#endif
	uv.xy = uv.xy * 0.5 + 0.5;
	return uv;
}

#if _SSS_HDRP
	float3 SSS_WorldToAbsolute(float3 v) { return GetAbsolutePositionWS(v); }
	float3 SSS_AbsoluteToWorld(float3 v) { return GetCameraRelativePositionWS(v); }
#else
	float3 SSS_WorldToAbsolute(float3 v) { return v; }
	float3 SSS_AbsoluteToWorld(float3 v) { return v; }
#endif

float3 SSS_WorldToView(float3 v) { return TransformWorldToView(SSS_AbsoluteToWorld(v)); }
float3 SSS_WorldToObject(float3 v) { return TransformWorldToObject(SSS_AbsoluteToWorld(v)); }
float3 SSS_WorldToScreen(float3 v) { return SSS_HClipToScreen(TransformWorldToHClip(SSS_AbsoluteToWorld(v))); }
float3 SSS_ObjectToScreen(float3 v) { return SSS_HClipToScreen(TransformObjectToHClip(v)); }
float3 SSS_ObjectToWorld(float3 v) { return SSS_WorldToAbsolute(TransformObjectToWorld(v)); }
float3 SSS_ObjectToView(float3 v) { return TransformWorldToView(TransformObjectToWorld(v)); }
float3 SSS_ScreenToWorld(float3 v) { return SSS_WorldToAbsolute(ComputeWorldSpacePosition(v.xy, v.z, UNITY_MATRIX_I_VP)); }
float3 SSS_ScreenToObject(float3 v) { return SSS_WorldToObject(SSS_ScreenToWorld(v)); }
float3 SSS_ScreenToView(float3 v) { return SSS_WorldToView(SSS_ScreenToWorld(v)); }
float3 SSS_ViewToWorld(float3 v) { return mul(UNITY_MATRIX_I_V, float4(v, 1.0)).xyz; }
float3 SSS_ViewToObject(float3 v) { return TransformWorldToObject(SSS_ViewToWorld(v)); }
float3 SSS_ViewToScreen(float3 v) { return SSS_HClipToScreen(TransformWViewToHClip(v)); }
float3 SSS_ObjectToWorldDir(float3 v)
{
	#if _SSS_BIRP
		return TransformObjectToWorldDir(v);
	#else
		return TransformObjectToWorldDir(v, true);
	#endif
}
float3 SSS_ObjectToViewDir(float3 v)
{
	#if _SSS_BIRP
		return TransformWorldToViewDir(TransformObjectToWorldDir(v));
	#else
		return TransformWorldToViewDir(TransformObjectToWorldDir(v, false), true);
	#endif
}
float3 SSS_WorldToObjectDir(float3 v)
{
	#if _SSS_BIRP
		return TransformWorldToObjectDir(v);
	#else
		return TransformWorldToObjectDir(v, true);
	#endif
}
float3 SSS_WorldToViewDir(float3 v)
{
	#if _SSS_BIRP
		return TransformWorldToViewDir(v);
	#else
		return TransformWorldToViewDir(v, true);
	#endif
}
float3 SSS_ViewToObjectDir(float3 v)
{
	#if _SSS_URP || _SSS_HDRP
		return SSS_WorldToObjectDir(mul(UNITY_MATRIX_I_V, float4(v, 0.0)).xyz);
	#else
		return SSS_WorldToObjectDir(mul((float3x3)UNITY_MATRIX_I_V, v));
	#endif
}
float3 SSS_ViewToWorldDir(float3 v)
{
	return mul(UNITY_MATRIX_I_V, float4(v, 0.0)).xyz;
}

#if _SSS_NO_DERIVATIVES
	float3 SSS_GetSceneColor(float2 uv) { return float3(0.0, 0.0, 0.0); }
	float3 SSS_GetSceneColorHD(float2 uv) { return SSS_GetSceneColor(uv); }
	float  SSS_GetSceneDepth(float2 uv) { return 0.0; }
#else
	#if _SSS_URP
		float3 SSS_GetSceneColor(float2 uv) { return SHADERGRAPH_SAMPLE_SCENE_COLOR(uv).xyz; }
		float3 SSS_GetSceneColorHD(float2 uv) { return SSS_GetSceneColor(uv); }
	#elif _SSS_HDRP
		float3 SSS_GetSceneColor(float2 uv) { return SHADERGRAPH_SAMPLE_SCENE_COLOR(uv).xyz; }
		float3 SSS_GetSceneColorHD(float2 uv)
		{
			#if defined(REQUIRE_OPAQUE_TEXTURE) && defined(_SURFACE_TYPE_TRANSPARENT) && defined(SHADERPASS) && (SHADERPASS != SHADERPASS_LIGHT_TRANSPORT) && (SHADERPASS != SHADERPASS_PATH_TRACING) && (SHADERPASS != SHADERPASS_RAYTRACING_VISIBILITY) && (SHADERPASS != SHADERPASS_RAYTRACING_FORWARD)
			return SampleCameraColor(uv, 0);
			#endif
			#if defined(REQUIRE_OPAQUE_TEXTURE) && defined(CUSTOM_PASS_SAMPLING_HLSL) && defined(SHADERPASS) && (SHADERPASS == SHADERPASS_DRAWPROCEDURAL || SHADERPASS == SHADERPASS_BLIT)
			return CustomPassSampleCameraColor(uv, 0);
			#endif
			return float3(0.0, 0.0, 0.0);
		}
	#else
		#if defined(UNITY_DECLARE_OPAQUE_TEXTURE_INCLUDED)
			float3 SSS_GetSceneColor(float2 uv) { return SampleSceneColor(uv); }
		#else
			sampler2D _CameraOpaqueTexture; float3 SSS_GetSceneColor(float2 uv) { return tex2D(_CameraOpaqueTexture, uv).xyz; }
		#endif
		float3 SSS_GetSceneColorHD(float2 uv) { return SSS_GetSceneColor(uv); }
	#endif

	float SSS_GetSceneDepth(float2 uv) { return SHADERGRAPH_SAMPLE_SCENE_DEPTH(uv); }
#endif

float3 SSS_GetSceneWorldPosition(float2 screenUV, float sceneDepth)
{
	#if _SSS_BIRP
		float4 clipPos  = float4(screenUV * 2.0f - 1.0f, 0.0f, 1.0f);
		float4 viewPos  = mul(unity_CameraInvProjection, clipPos);
		float3 worldDir = mul((float3x3)UNITY_MATRIX_I_V, viewPos.xyz);
					
		return _WorldSpaceCameraPos + worldDir * LinearEyeDepth(sceneDepth);
	#else
		float4 clipPos = float4(screenUV * 2.0 - 1.0, sceneDepth, 1.0);
					
		#if UNITY_UV_STARTS_AT_TOP
			clipPos.y = -clipPos.y;
		#endif
					
		float4 worldPos = mul(UNITY_MATRIX_I_VP, clipPos);
					
		worldPos.xyz /= worldPos.w;
					
		#if _SSS_HDRP
			worldPos.xyz = GetAbsolutePositionWS(worldPos.xyz);
		#endif
					
		return worldPos.xyz;
	#endif
}

float SSS_GetSceneWorldDistance(float2 screenUV, float sceneDepth)
{
	return distance(_WorldSpaceCameraPos, SSS_GetSceneWorldPosition(screenUV, sceneDepth));
}

float3 SSS_UnpackNormalScale(float4 c, float s)
{
	#if _SSS_BIRP
		return UnpackScaleNormal(c, s);
	#else
		return UnpackNormalScale(c, s);
	#endif
}

struct SSS_VertexData
{
	float  instanceID;
	float3 position;
	float3 normal;
	float3 tangent;
	float4 color;
	float4 texcoord0;
	float4 texcoord1;
	float4 texcoord2;
	float4 texcoord3;
	float4 extraV2F0;
	float4 extraV2F1;
	float4 extraV2F2;
	float4 extraV2F3;
	float4 extraV2F4;
	float4 extraV2F5;
	float4 extraV2F6;
	float4 extraV2F7;
	



};

struct SSS_FragmentData
{
	float3 localSpacePosition;
	float3 localSpaceNormal;
	float3 localSpaceTangent;
	
	float3 worldSpacePosition;
	float3 worldSpaceNormal;
	float3 worldSpaceTangent;
	//float tangentSign;

	float3 worldSpaceViewDir;
	//float3 tangentSpaceViewDir;
	
	float4 texcoord0;
	float4 texcoord1;
	float4 texcoord2;
	float4 texcoord3;
	
	float2 screenUV;
	float4 screenPos;

	float4 vertexColor;
	bool isFrontFace;
	
	float4 extraV2F0;
	float4 extraV2F1;
	float4 extraV2F2;
	float4 extraV2F3;
	float4 extraV2F4;
	float4 extraV2F5;
	float4 extraV2F6;
	float4 extraV2F7;

	float3x3 TBNMatrix;
	



};

struct SSS_SurfaceData
{
	float3 Albedo;
	float  Smoothness;
	float3 Normal;
	float3 Emission;
	float  Occlusion;
	float  Metallic;
	float  Alpha;
};

float2 _CW_PositionCompressionData; // x = start distance, y = max distance limit

#define DEBUG_SCALE 0.001
#define SSS_LOG_K          0.0001
#define SSS_MAX_WORLD_DIST 1e27

float SSS_GetCompressionRatio(float worldDist)
{
    float startDist = _CW_PositionCompressionData.x;
    float maxDist   = _CW_PositionCompressionData.y;

    // no compression nearby
    if (worldDist <= startDist)
        return 1.0;

    float excess = worldDist - startDist;

    float compressedDist =
        startDist +
        (maxDist - startDist) *
        log(1.0 + excess * SSS_LOG_K) /
        log(1.0 + SSS_MAX_WORLD_DIST * SSS_LOG_K);

    return compressedDist / worldDist;
}

float SSS_DecompressWorldDist(float compressedDist)
{
    float startDist = _CW_PositionCompressionData.x;
    float maxDist   = _CW_PositionCompressionData.y;

    if (compressedDist <= startDist)
        return compressedDist;

    float t =
        saturate(
            (compressedDist - startDist) /
            (maxDist - startDist)
        );

    return startDist +
        (
            exp(
                t *
                log(1.0 + SSS_MAX_WORLD_DIST * SSS_LOG_K)
            ) - 1.0
        ) / SSS_LOG_K;
}

float3 SSS_CompressWorld(float3 worldPos, float3 worldCenter, float worldRadius, out float ratio) 
{
    float3 worldDelta = worldPos - _WorldSpaceCameraPos;
    float  farDist    = distance(_WorldSpaceCameraPos, worldCenter) + worldRadius;
    
    ratio = SSS_GetCompressionRatio(farDist);
    
    return _WorldSpaceCameraPos + worldDelta * ratio;
}

float3 SSS_CompressWorld(float3 worldPos) 
{
    float3 worldDelta = worldPos - _WorldSpaceCameraPos;
    float  worldDist  = length(worldDelta);
    
    return _WorldSpaceCameraPos + worldDelta * SSS_GetCompressionRatio(worldDist);
}

float3 SSS_DecompressWorld(float3 worldPos)
{
    float3 worldDelta = worldPos - _WorldSpaceCameraPos;
    float  worldDist  = length(worldDelta);
    
    return worldDist > 0.001 ? _WorldSpaceCameraPos + (worldDelta / worldDist) * SSS_DecompressWorldDist(worldDist) : _WorldSpaceCameraPos;
}

float3 SSS_DecompressWorld(float3 worldPos, float ratio) 
{
    float3 worldDelta = worldPos - _WorldSpaceCameraPos;
    return _WorldSpaceCameraPos + worldDelta / ratio;
}

float SSS_ElongateDistance(float worldDist)
{
	float minDist = 10000.0;
	float maxDist = 100000.0;
	float scale   = saturate((worldDist - minDist) / (maxDist - minDist));

	return worldDist * lerp(1.0, 1.2, scale);
}
#define SGT_OPACITY_STEPS  5

float4x4        _SGT_SkyToWorld;
float4x4        _SGT_WorldToSky;
float4          _SGT_SkyColor;
float           _SGT_SkyBrightness;
float4          _SGT_SkyAmbientTint;
float           _SGT_SkyExposure;
float4          _SGT_SkyRadius;
float4          _SGT_SkyAltitudeData;
float           _SGT_SkyDensity;
float4          _SGT_SkyMieWeight;
float4          _SGT_SkyMieData;
sampler3D       _SGT_SkyRadianceTex;
float           _SGT_SkyRadianceLod;
sampler2D       _SGT_SkyLightingTex;
sampler2D       _SGT_SkyAlbedoTex;
float           _SGT_SkyRevealStars;
float           _SGT_SkyDepthOpaque;

float     _SGT_Volumetrics_Downscale; // Global
float     _SGT_Volumetrics_NearClip; // Global
Texture2D _SGT_Volumetrics_ColorTex; // Global
float4    _SGT_Volumetrics_ColorSize; // Global
Texture2D _SGT_Volumetrics_DepthTex; // Global
float2    _SGT_Volumetrics_DepthSize; // Global

SamplerState my_linear_clamp_sampler;
SamplerState my_point_clamp_sampler;

sampler2D _SGT_BlueNoiseTex; // Global
float     _SGT_Frame; // Global
float3    _SGT_AmbientColor; // Global
	
float SGT_DitherBlue(float2 screenUV)
{
	float2 pixel = floor(screenUV * _ScreenParams.xy);
	float  noise = tex2D(_SGT_BlueNoiseTex, pixel / 64.0f).r;
	return frac(noise + _SGT_Frame / sqrt(0.5f));
}

float SGT_DitherFast(float2 screenUV)
{
    float2 R2 = float2(0.7548776662, 0.5698402909);
    float2 p  = screenUV * _ScreenParams.xy + frac(_Time.y * R2) * 256.0;
    return frac(52.9829189 * frac(dot(p, float2(0.06711056, 0.00583715))));
}
	
float2 SGT_DirectionToEquirectangular(float3 dir)
{
	dir = normalize(dir);
	float u = atan2(dir.z, dir.x) / (2.0 * 3.141592653) + 0.5;
	float v = asin(clamp(dir.y, -1.0, 1.0)) / 3.141592653 + 0.5;
	return float2(u, v);
}

float3 SGT_SphereTest(float3 ray, float3 rayD, float radius)
{
	float B = -dot(ray, rayD);
	float C = dot(ray, ray) - radius * radius;
	float D = B * B - C;
	float E = sqrt(max(D, 0.0f));
	return float3(B - E, B + E, D);
}

float SGT_InverseLerp(float a, float b, float v)
{
	return (v - a) / (b - a);
}

float3 SGT_CalculateMie(float3 ray, float mie)
{
	return normalize(ray * _SGT_SkyMieWeight.xyz + 1e-6) * mie;
}

float SGT_GetPitch01(float3 rayDir, float3 up, float altitude01)
{
	float power = 2.0f;
	float eyeHeight = _SGT_SkyRadius.x + altitude01 * (_SGT_SkyRadius.y - _SGT_SkyRadius.x);
	float horizonAngleFromDown = -acos(clamp(_SGT_SkyRadius.x / max(eyeHeight, 0.00001f), -1.0f, 1.0f));
	float rayAng = asin(clamp(dot(up, rayDir), -1.0f, 1.0f));
	float sun01;

	if (rayAng < horizonAngleFromDown)
	{
		sun01 = SGT_InverseLerp(-3.141592653 * 0.5f, horizonAngleFromDown, rayAng);
		sun01 = 1.0f - pow(1.0f - sun01, 1.0f / power);
		sun01 = sun01 * 0.5f;
	}
	else
	{
		sun01 = SGT_InverseLerp(horizonAngleFromDown, 3.141592653 * 0.5f, rayAng);
		sun01 = pow(sun01, 1.0f / power);
		sun01 = sun01 * 0.5f + 0.5f;
	}

	return sun01;
}

float3 GetLutCoord(float3 rayPos, float3 rayDir, float3 sunDir, float dither)
{
	float3 up         = normalize(rayPos);
	float  eyeHeight  = length(rayPos);
	float  altitude01 = saturate(SGT_InverseLerp(_SGT_SkyRadius.z, 1.0f, eyeHeight));
	float  pitch01    = SGT_GetPitch01(rayDir, up, altitude01);
	float  sun01      = SGT_GetPitch01(sunDir, up, altitude01);
	
	pitch01 += (dither - 0.5f) / 128.0f;
	sun01   += (dither - 0.5f) /  64.0f;
	
	return float3(pitch01, altitude01, sun01);
}

float2 SGT_GetDensity2(float3 pos, float weightMul)
{
	float alt01 = saturate((1.0f - length(pos)) * _SGT_SkyAltitudeData.z);

	return pow(alt01, _SGT_SkyAltitudeData.xy * weightMul) * _SGT_SkyDensity;
}

float SGT_GetOpacity(float3 rayPos, float3 rayDir, float rayMax, float weightMul)
{
	float2 opticalDepth = 0.0f;
	float  rayStep      = rayMax / SGT_OPACITY_STEPS;
	float  t            = rayStep * 0.5f;
	
	for (int i = 0; i < SGT_OPACITY_STEPS; i++)
	{
		float3 pos = rayPos + rayDir * t;

		opticalDepth += SGT_GetDensity2(pos, weightMul) * rayStep;
		
		t += rayStep;
	}
	
	return 1.0f - exp((opticalDepth.x + opticalDepth.y) * -20.0f);
}

float4 SGT_SampleLUT(float3 coord)
{
	return tex3Dlod(_SGT_SkyRadianceTex, float4(coord, _SGT_SkyRadianceLod));
}

float3 SGT_AtmosphereColor(float3 rayPos, float3 sunDir, float dither)
{
	float3 up         = normalize(rayPos);
	float  eyeHeight  = length(rayPos);
	float  altitude01 = saturate(SGT_InverseLerp(_SGT_SkyRadius.z, 1.0f, eyeHeight));
	float  sun01      = 0.5f + 0.5f * dot(sunDir, up);
		
	return tex2Dlod(_SGT_SkyLightingTex, float4(sun01, altitude01, 0.0f, 0.0f)).xyz;
}

float3 SGT_UnderwaterAtmosphere(float3 rayPos, float3 rayDir, float3 sunDir, float exposure, float dither)
{
	float3 coord = GetLutCoord(rayPos, rayDir, sunDir, dither); coord.x = 1.0f;
	float4 ray   = SGT_SampleLUT(coord);
	float3 mie   = SGT_CalculateMie(ray.xyz, ray.w);
	float3 color = ray.xyz + mie;
	
	#if _SSS_HDRP
		color *= exposure * 0.25f;
	#else
		color = 1.0 - exp(-color * exposure);
	#endif
	
	return saturate(_SGT_SkyColor.xyz * color * _SGT_SkyBrightness);
}

float4 SGT_Atmosphere(float3 rayPos, float3 rayDir, float rayFar, float rayMax, float3 sunDir, float exposure, float dither, float mieMul, float weightMul)
{
	// 1. Calculate opacity FIRST so we can use it for blending
	float opacity = SGT_GetOpacity(rayPos, rayDir, rayMax, weightMul);

	float3 coord  = GetLutCoord(rayPos, rayDir, sunDir, dither);
	float2 mieOff = 0.0f;
	
	if (rayMax * 1.02f <= rayFar) // If ground
	{
		// 2. Store the true, un-clamped sky coordinate
		float trueCoordX = coord.x;
		
		// 3. Calculate the clamped "below horizon" coordinate (original behavior)
		float clampedCoordX = min(coord.x, 0.5f - (1.0f / 128.0f));
		clampedCoordX = lerp(0.5f - (1.0f / 128.0f), clampedCoordX, coord.y * coord.y);
		
		// 4. Smoothly blend towards the true sky color as fog density/opacity increases
		coord.x = lerp(clampedCoordX, trueCoordX, opacity);
		mieOff = lerp(0.2f, 0.0f, opacity);
	}
	else
	{
		//coord.x = max(coord.x, 0.5f + (1.0f / 128.0f));
	}
	
	float4 ray    = SGT_SampleLUT(coord);
	float3 mie    = SGT_CalculateMie(ray.xyz, ray.w);
	float2 g      = lerp(_SGT_SkyMieData.xy, 0.0f, mieOff);
	float  c      = dot(rayDir, sunDir);
	float  phaseR = 3.0f / (16.0f * 3.141592653f) * (1.0f + c * c);
	float2 phaseM = 3.0f / (8.0f * 3.141592653f) * ((1.0f - g * g) * (c * c + 1.0f)) / (pow(abs(1.0f + g * g - 2.0f * c * g), 1.5f) * (2.0f + g * g));
	float3 color  = ray.xyz * phaseR + mie * dot(phaseM, 1) * mieMul;
	
	#if _SSS_HDRP
		color *= exposure * 0.25f;
	#else
		color = 1.0 - exp(-color * exposure);
	#endif
	
	#if _SSS_HDRP
		color += _SGT_AmbientColor.xyz * _SGT_SkyAmbientTint.xyz * 0.25;
	#else
		color += _SGT_AmbientColor.xyz * _SGT_SkyAmbientTint.xyz;
	#endif

	#if _SGT_SOLID_CORE_OFF // If the atmosphere below InnerMeshRadius isn't solid, then continue to darken because the LUT doesn't extend any farther
		float eyeHeight     = length(rayPos);
		float inner         = _SGT_SkyRadius.z;
		float interiorDepth = saturate((_SGT_SkyRadius.z - eyeHeight) / _SGT_SkyRadius.z);

		if (interiorDepth > 0.0)
		{
			float extinction = exp(-interiorDepth * 20.0 * _SGT_SkyDensity);

			color *= extinction;
			opacity = lerp(opacity, 1.0f, saturate(interiorDepth * 100.0f));
		}
	#endif
	
	return float4(saturate(_SGT_SkyColor.xyz * color * _SGT_SkyBrightness), opacity);
}

// Global cached light properties
float3 _SGT_CachedLightDirection;
float3 _SGT_CachedLightColor;

float3 SGT_SampleSkyReflection(float3 ocam, float3 odir)
{
    // 2. Intersect with the Atmosphere Shell (Outer radius is 1.0 in sky space)
    float3 odst = SGT_SphereTest(ocam, odir, 1.0f);
    odst.xy = max(odst.xy, 0.0f); // Ensure distances are ahead of ray
    
    float3 rayPos = ocam + odir * odst.x;
    float3 rayDir = odir;
    
    // 3. Determine Atmosphere Limits
    float rayFar = SGT_SphereTest(rayPos, rayDir, 1.0f).y; // Distance to sky exit
    float rayMax = odst.y - odst.x;                       // Max scattering distance
    
    // Optional: Intersect with the planet core so reflections don't "see through" the world
    #if _SGT_SOLID_CORE
        float3 ihit = SGT_SphereTest(ocam, odir, _SGT_SkyRadius.w);
        if (ihit.z > 0.0f && ihit.x > 0.0f) {
            rayMax = min(ihit.x - odst.x, rayMax);
        }
    #endif

    // 4. Calculate Sun Direction in Sky Space
    float3 sunDir = normalize(mul(_SGT_WorldToSky, float4(_SGT_CachedLightDirection, 0.0f)).xyz);
    
    // 5. Sample the Atmosphere
    // dither is set to 0.0 to keep reflections smooth; mieMul is 1.0
    float4 sky = SGT_Atmosphere(rayPos, rayDir, rayFar, rayMax, sunDir, _SGT_SkyExposure, 0.0, 1.0, 1.0);
    
    // 6. Apply cached light intensity
    return sky.rgb * _SGT_CachedLightColor;
}

float4 SGT_SimpleAtmosphere(float3 worldPos, float3 worldHit, float mieMul, float weightMul)
{
	float dither   = 0.5f;
	
	float3 ocam = mul(_SGT_WorldToSky, float4(worldPos, 1.0f)).xyz;
	float3 ofar = mul(_SGT_WorldToSky, float4(worldHit, 1.0f)).xyz;
	float  omax = distance(ocam, ofar);
	float3 odir = normalize(ofar - ocam);
	float3 odst = SGT_SphereTest(ocam, odir, 1.0f);
	
	odst.xy = max(odst.xy, 0.0f);
	
	float3 rayPos = ocam + odir * odst.x;
	float3 rayDir = odir;
	float  rayFar = SGT_SphereTest(rayPos, rayDir, 1.0f).y;
	float  rayMax = min(odst.y, omax) - odst.x;
	
	#if _SGT_LIGHTING
		float3 sunDir = normalize(mul(_SGT_WorldToSky, float4(_SGT_LightDirection[0].xyz, 0.0f)).xyz);
	#else
		float3 sunDir = normalize(rayPos);
	#endif
	
	return SGT_Atmosphere(rayPos, rayDir, rayFar, rayMax, sunDir, _SGT_SkyExposure, dither, mieMul, weightMul);
}

float3 CW_Depth2World(float viewDepth, float3 viewDir)
{
	return _WorldSpaceCameraPos + viewDir * viewDepth;
}
	
bool CW_DepthInBounds(float viewDepth, float3 viewDir)
{
	float3 wPos = CW_Depth2World(viewDepth, viewDir);
	float3 oPos = mul(_SGT_WorldToSky, float4(wPos, 1.0f)).xyz;
		
	return length(oPos) < 1.2f;
}
	
void CW_SampleVolumetricsDefault(float2 screenUV, float3 viewDir, out float4 outColor, out float outDepth)
{
	float4 centerColor = _SGT_Volumetrics_ColorTex.Sample(my_linear_clamp_sampler, screenUV);
	float  centerDepth = _SGT_Volumetrics_DepthTex.Sample(my_linear_clamp_sampler, screenUV).x;
		
	if (CW_DepthInBounds(centerDepth, viewDir) == true)
	{
		outColor = centerColor;
		outDepth = centerDepth;
	}
	else
	{
		outColor = 0;
		outDepth = 0;
	}
}

void CW_SampleVolumetricsDefault2(float2 screenUV, float3 viewDir, out float4 outColor, out float outDepth)
{
	outColor = _SGT_Volumetrics_ColorTex.Sample(my_linear_clamp_sampler, screenUV);
	outDepth = _SGT_Volumetrics_DepthTex.Sample(my_linear_clamp_sampler, screenUV).x;
}
	
void CW_SampleVolumetrics(float2 screenUV, float3 viewDir, float sceneDepth, out float4 outColor, out float outDepth)
{
	outColor = 0;
	outDepth = 0;
	//CW_SampleVolumetricsDefault(screenUV, viewDir, outColor, outDepth);return;
		
	if (_SGT_Volumetrics_Downscale > 1)
	{
		float2 pixelF   = screenUV * _SGT_Volumetrics_ColorSize.xy - 0.5f;
		float2 pixelI   = floor(pixelF);
		float2 snappedF = pixelF - pixelI;
			
		float2 coord0 = (pixelI + float2(0.5f, 0.5f)) * _SGT_Volumetrics_ColorSize.zw;
		float2 coord1 = (pixelI + float2(1.5f, 0.5f)) * _SGT_Volumetrics_ColorSize.zw;
		float2 coord2 = (pixelI + float2(0.5f, 1.5f)) * _SGT_Volumetrics_ColorSize.zw;
		float2 coord3 = (pixelI + float2(1.5f, 1.5f)) * _SGT_Volumetrics_ColorSize.zw;
			
		float depth0 = _SGT_Volumetrics_DepthTex.Sample(my_point_clamp_sampler, coord0).x;
		float depth1 = _SGT_Volumetrics_DepthTex.Sample(my_point_clamp_sampler, coord1).x;
		float depth2 = _SGT_Volumetrics_DepthTex.Sample(my_point_clamp_sampler, coord2).x;
		float depth3 = _SGT_Volumetrics_DepthTex.Sample(my_point_clamp_sampler, coord3).x;
			
		float4 color0 = _SGT_Volumetrics_ColorTex.Sample(my_point_clamp_sampler, coord0);
		float4 color1 = _SGT_Volumetrics_ColorTex.Sample(my_point_clamp_sampler, coord1);
		float4 color2 = _SGT_Volumetrics_ColorTex.Sample(my_point_clamp_sampler, coord2);
		float4 color3 = _SGT_Volumetrics_ColorTex.Sample(my_point_clamp_sampler, coord3);
			
		float w0 = CW_DepthInBounds(depth0, viewDir) * (depth0 < sceneDepth) * color0.w * (1.0 - snappedF.x) * (1.0 - snappedF.y);
		float w1 = CW_DepthInBounds(depth1, viewDir) * (depth1 < sceneDepth) * color1.w * snappedF.x * (1.0 - snappedF.y);
		float w2 = CW_DepthInBounds(depth2, viewDir) * (depth2 < sceneDepth) * color2.w * (1.0 - snappedF.x) * snappedF.y;
		float w3 = CW_DepthInBounds(depth3, viewDir) * (depth3 < sceneDepth) * color3.w * snappedF.x * snappedF.y;
			
		float wt = w0 + w1 + w2 + w3;
			
		if (wt > 0.0f)
		{
			outColor = (color0 * w0 + color1 * w1 + color2 * w2 + color3 * w3) / wt;
			outDepth = (depth0 * w0 + depth1 * w1 + depth2 * w2 + depth3 * w3) / wt;
		}
		else
		{
			return;
		}
	}
	else
	{
		float2 coord0 = screenUV;
		float  depth0 = _SGT_Volumetrics_DepthTex.Sample(my_point_clamp_sampler, coord0).x;
		float4 color0 = _SGT_Volumetrics_ColorTex.Sample(my_point_clamp_sampler, coord0);
		float  w0     = CW_DepthInBounds(depth0, viewDir) * (depth0 < sceneDepth);
		
		if (w0 > 0.0f)
		{
			outColor = color0;
			outDepth = depth0;
		}
		else
		{
			return;
		}
	}
}






#pragma instancing_options procedural:SetupInstancing
#if _SSS_PASS_SHADOWCASTER || _SSS_PASS_META
	#pragma multi_compile_instancing // For some reason the ShadowCaster and Meta pass in BIRP doesn't have this line
#endif


#define BATCH_CAPACITY 35
#define VERTEX_COUNT 243

float4 _CwSize;
float4 _CwAtlas;
float4 _CwWeights[VERTEX_COUNT];
float4 _CwCoords[VERTEX_COUNT];

float4 _CwOrigins[BATCH_CAPACITY];
float4 _CwPositionsA[BATCH_CAPACITY];
float4 _CwPositionsB[BATCH_CAPACITY];
float4 _CwPositionsC[BATCH_CAPACITY];

sampler2D DataP;
sampler2D DataA;
sampler2D DataN;

float3 _CwOffset;

float4x4 _CwObjectToWorld;
float4x4 _CwWorldToObject;

float _SGT_Occlusion;

// CLOUDS
sampler2D _SGT_CloudShadowTex;
float3    _SGT_CloudShadowDirection;
float4    _SGT_CloudShadowOpacity;
float4x4  _SGT_CloudShadowMatrix;

// OCEAN FADE
float  _SGT_OceanFade;
float2 _SGT_OceanDensity;
float3 _SGT_OceanColor;
float  _SGT_OceanSmoothness;
float4 _SGT_OceanLightDirection;

float _SGT_OceanRadius;

void SetupInstancing()
{
	#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
		#ifdef unity_ObjectToWorld
			#undef unity_ObjectToWorld
		#endif

		#ifdef unity_WorldToObject
			#undef unity_WorldToObject
		#endif
		
		unity_ObjectToWorld = _CwObjectToWorld;
		unity_WorldToObject = _CwWorldToObject;
	#endif
}

float SGT_SampleCloudDensity(float3 wpos)
{
	float3 cpos  = mul(_SGT_CloudShadowMatrix, float4(wpos, 1.0)).xyz;
	float3 chit  = SGT_SphereTest(cpos, _SGT_CloudShadowDirection, 1.0);
	float2 uv    = SGT_DirectionToEquirectangular(normalize(cpos + chit.y * _SGT_CloudShadowDirection));
	
	return saturate(dot(tex2D(_SGT_CloudShadowTex, uv), _SGT_CloudShadowOpacity));
}

void SSS_Vert(inout SSS_VertexData v)
{
	float vertexIndex = v.position.x;
	float squareIndex = v.position.y;
	float batchIndex  = v.instanceID;
	
	float  batchCol = batchIndex % _CwAtlas.z;
	float  batchRow = floor(batchIndex / _CwAtlas.z);
	float3 origin   = _CwOrigins[batchIndex].xyz;
	float3 weights  = _CwWeights[vertexIndex].xyz;
	float2 coord    = _CwCoords[vertexIndex].xy; coord.x /= 3.0;
	float3 position = _CwPositionsA[batchIndex].xyz * weights.x + _CwPositionsB[batchIndex].xyz * weights.y + _CwPositionsC[batchIndex].xyz * weights.z;
	
	v.position.xyz = _CwOffset + origin + tex2Dlod(DataP, float4(vertexIndex * _CwSize.x, batchIndex * _CwSize.y, 0.0, 0.0)).xyz;
	
	float3 ocam = mul(_CwWorldToObject, float4(_WorldSpaceCameraPos - _CwOffset, 1.0)).xyz;
	
	#if _SGT_SHAPE_SQUARE
		v.normal  = float3(0,1,0);
		v.tangent = float4(1,0,0,-1).xyz;
		
		v.extraV2F0.w = ocam.y;
	#elif _SGT_SHAPE_SPHERE
		v.normal = normalize(position);
		
		if (position.x == 0.0 && position.z == 0.0)
		{
			position.xz += 0.00000001;
		}
		
		v.tangent = float4(normalize(cross(float3(0,1,0), normalize(position))), -1.0).xyz;
		
		v.extraV2F0.w = length(ocam) - _SGT_OceanRadius;
	#endif
	
	#if _SGT_OCEAN_FADE
		if (_SGT_OceanFade > 0.0 && length(v.position.xyz - _CwOffset) < _SGT_OceanRadius)
		{
			float3 N = normalize(position); // sphere normal
			float3 V = normalize(_WorldSpaceCameraPos - v.position.xyz);
			float  F = pow(abs(1.0 - saturate(dot(N, V))), 2.0);

			float3 oceanPos = _CwOffset + N * _SGT_OceanRadius;
			v.position.xyz = lerp(v.position.xyz, oceanPos, F);
		}

	#endif
	
	// Calc UV
	float2 pixelS = _CwAtlas.xy * _CwAtlas.zw;
	float  pixelX = batchCol * _CwAtlas.x + coord.x * (_CwAtlas.x - 3.0) + 0.5 + squareIndex * (_CwAtlas.x / 3.0);
	float  pixelY = batchRow * _CwAtlas.y + coord.y * (_CwAtlas.y - 1.0) + 0.5;
	
	v.extraV2F0.x = pixelX / pixelS.x;
	v.extraV2F0.y = pixelY / pixelS.y;
	
	#if CW_COMPRESS_POSITIONS
		float3 wpos = mul(_CwObjectToWorld, float4(v.position, 1.0)).xyz;
		v.extraV2F0.z = distance(_WorldSpaceCameraPos, wpos);
		wpos = SSS_CompressWorld(wpos);
		v.position = mul(_CwWorldToObject, float4(wpos, 1.0)).xyz;
	#endif
}

float3 SGT_GetColor(float3 wnormal, float3 wlight)
{
	return saturate(dot(wnormal, wlight));
}

static const float WATER_DEPTH_SHALLOW = 30.0;
static const float WATER_DEPTH_MAX = 3000.0;

float SGT_DecodeWaterDepth(float encoded)
{
	float shallow = encoded * (WATER_DEPTH_SHALLOW * 2.0);
    float deep = WATER_DEPTH_SHALLOW + (encoded - 0.5) * ((WATER_DEPTH_MAX - WATER_DEPTH_SHALLOW) * 2.0);
    float isDeep = step(0.5, encoded);
    return lerp(shallow, deep, isDeep);
}

float3 SGT_ApplyOceanColor(
    float3 terrainColor,
    float  waterDepth01,
    float3 waterColor,
    float2 waterDensity,
    float  waterBlend,
    float3 ambientColor)
{
    float depth = SGT_DecodeWaterDepth(waterDepth01);
    float3 w = float3(1.0, 0.2, 0.1);

	float3 e0 = w * waterDensity.x;
	float3 e1 = w * waterDensity.y;

	float3 floorT0 = exp(-e0 * depth);
	float3 floorT1 = exp(-e1 * depth);
	
	float3 volumeT0 = floorT0 * floorT0 * floorT0;
	float3 volumeT1 = floorT1 * floorT1 * floorT1;

	float3 scatter = waterColor * ambientColor;

	float3 col0 = terrainColor * floorT0 + scatter * (1.0 - volumeT0);
	float3 col1 = terrainColor * floorT1 + scatter * (1.0 - volumeT1);

	return lerp(col0, col1, waterBlend);
}

float SGT_Bayer4x4(float2 screenPos)
{
	uint2 p = uint2(screenPos * _ScreenParams.xy) % 4;
	float bayer[16] = { 0,  8,  2, 10, 12,  4, 14,  6, 3, 11,  1,  9, 15,  7, 13,  5 };
	return (bayer[p.x + p.y * 4] + 0.5) / 16.0;
}

float SGT_Fresnel(float3 dir, float3 normal, float airIOR, float waterIOR)
{
	float cosTheta = clamp(dot(dir, normal), 0.0, 1.0);
	float F0       = pow(abs((waterIOR / airIOR - 1.0) / (waterIOR / airIOR + 1.0)), 2.0);

	return F0 + (1.0 - F0) * pow(abs(1.0 - cosTheta), 5.0);
}

void SSS_Frag(inout SSS_SurfaceData o, inout SSS_FragmentData d)
{
	#if CW_COMPRESS_POSITIONS
		d.worldSpacePosition = _WorldSpaceCameraPos - d.worldSpaceViewDir * d.extraV2F0.z;
	#endif
	
	float4 dataA = tex2Dlod(DataA, float4(d.extraV2F0.xy,0,0));
	float4 dataN = tex2Dlod(DataN, float4(d.extraV2F0.xy,0,0)); 
	float2 tnorm = dataN.xy * 2.0 - 1.0;
	
	tnorm.x = -tnorm.x; // Don't set negative tangent sign, so do it manually
	
	o.Albedo     = dataA.xyz;
	o.Occlusion  = _SGT_Occlusion;
	o.Emission   = dataA.xyz * dataN.z;
	o.Smoothness = dataN.w;
	o.Metallic   = 0.0;
	o.Normal     = float3(tnorm, sqrt(1.0 - saturate(dot(tnorm, tnorm))));
	o.Alpha      = dataN.x != 0.0 && dataN.y != 0.0;
	
	float localEyeHeight = d.extraV2F0.w;
	
	#if _SGT_OCEAN_FADE
		float  fadeTransition = step(SGT_Bayer4x4(d.screenUV), _SGT_OceanFade);
		float  cameraDistance = distance(d.worldSpacePosition, _WorldSpaceCameraPos);
		float  cameraDist01   = 1.0 - pow(abs(saturate(1.0 - cameraDistance / _SGT_OceanRadius)), 8.0);
		float3 oceanColor     = SGT_ApplyOceanColor(o.Albedo, dataA.w, _SGT_OceanColor, _SGT_OceanDensity, cameraDist01, 1.0);
		float  fade           = saturate(dataA.w > 0) * fadeTransition;

		o.Albedo     = lerp(o.Albedo, oceanColor, fade);
		o.Smoothness = lerp(o.Smoothness, _SGT_OceanSmoothness, fade);
		o.Normal     = lerp(o.Normal, float3(0.0, 0.0, 1.0), fade);
		o.Occlusion  = lerp(o.Occlusion, 0.15, fade);

		#if _SGT_SKY
			float  airIOR   = 1.0;
			float  waterIOR = 1.33;
			float3 opos     = mul(_SGT_WorldToSky, float4(d.worldSpacePosition, 1.0)).xyz;
			float3 onrm     = normalize(opos);
			float3 oview    = normalize(mul(_SGT_WorldToSky, float4(d.worldSpaceViewDir, 0.0)).xyz);
			float3 odir     = reflect(oview, onrm);
	
			o.Emission = SGT_SampleSkyReflection(opos, -odir) * fade * SGT_Fresnel(oview, onrm, airIOR, waterIOR);
			#if _SSS_HDRP
				o.Emission *= 120000;
			#endif
		#endif
		//o.Emission +=  _SGT_AmbientColor.xyz * 0.02;
	#endif
	
	#if _SGT_CLOUDS
		float shadow = SGT_SampleCloudDensity(d.worldSpacePosition);
		
		o.Albedo    = lerp(o.Albedo   , 0.0, shadow);
		o.Occlusion = lerp(o.Occlusion, 0.0, shadow);
	#endif
}


void Vert_float
	(
	float  iInstanceID,
	float3 iPosition,
	float3 iNormal,
	float3 iTangent,
	float4 iColor,
	float4 iTexcoord0,
	float4 iTexcoord1,
	float4 iTexcoord2,
	float4 iTexcoord3,

	out float3 oPosition,
	out float3 oNormal,
	out float3 oTangent,
	out float4 oExtraV2F0,
	out float4 oExtraV2F1,
	out float4 oExtraV2F2,
	out float4 oExtraV2F3,
	out float4 oExtraV2F4,
	out float4 oExtraV2F5,
	out float4 oExtraV2F6,
	out float4 oExtraV2F7
	)
{
	SSS_VertexData v = (SSS_VertexData)0;
	
	v.instanceID = iInstanceID;
	v.position   = iPosition;
	v.normal     = iNormal;
	v.tangent    = iTangent;
	v.color      = iColor;
	v.texcoord0  = iTexcoord0;
	v.texcoord1  = iTexcoord1;
	v.texcoord2  = iTexcoord2;
	v.texcoord3  = iTexcoord3;
	v.extraV2F0  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F1  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F2  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F3  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F4  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F5  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F6  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F7  = float4(0.0, 0.0, 0.0, 0.0);
	
	SSS_Vert(v);
	
	oPosition  = v.position;
	oNormal    = v.normal;
	oTangent   = v.tangent;
	oExtraV2F0 = v.extraV2F0;
	oExtraV2F1 = v.extraV2F1;
	oExtraV2F2 = v.extraV2F2;
	oExtraV2F3 = v.extraV2F3;
	oExtraV2F4 = v.extraV2F4;
	oExtraV2F5 = v.extraV2F5;
	oExtraV2F6 = v.extraV2F6;
	oExtraV2F7 = v.extraV2F7;
}

void Frag_float
	(
	inout float3 iPosition,
	inout float3 iNormal,
	inout float3 iTangent,
	bool   iIsFrontFace,
	float4 iColor,
	float4 iTexcoord0,
	float4 iTexcoord1,
	float4 iTexcoord2,
	float4 iTexcoord3,
	float4 iExtraV2F0,
	float4 iExtraV2F1,
	float4 iExtraV2F2,
	float4 iExtraV2F3,
	float4 iExtraV2F4,
	float4 iExtraV2F5,
	float4 iExtraV2F6,
	float4 iExtraV2F7,

	out float4x4 oExtra,
	out float3   oAlbedo,
	out float    oSmoothness,
	out float3   oNormal,
	out float3   oEmission,
	out float    oOcclusion,
	out float    oMetallic,
	out float    oAlpha
	)
{
	SSS_SurfaceData  s = (SSS_SurfaceData)0;
	SSS_FragmentData d = (SSS_FragmentData)0;
	
	s.Albedo = 1.0;
	s.Smoothness = 0.5;
	s.Normal = float3(0.0, 0.0, 1.0);
	s.Emission = float3(0.0, 0.0, 0.0);
	s.Occlusion = 0.0;
	s.Metallic = 0.0;
	s.Alpha = 1.0;
	
	iPosition = SSS_WorldToAbsolute(iPosition);
	
	d.localSpacePosition = SSS_WorldToObject(iPosition);
	d.localSpaceNormal   = normalize(SSS_WorldToObjectDir(iNormal));
	d.localSpaceTangent  = normalize(SSS_WorldToObjectDir(iTangent));
	
	d.worldSpacePosition = iPosition;
	d.worldSpaceNormal   = iNormal;
	d.worldSpaceTangent  = iTangent;
	//d.tangentSign;
	
	d.worldSpaceViewDir  = normalize(_WorldSpaceCameraPos - d.worldSpacePosition);
	//d.tangentSpaceViewDir;
	
	d.texcoord0 = iTexcoord0;
	d.texcoord1 = iTexcoord1;
	d.texcoord2 = iTexcoord2;
	d.texcoord3 = iTexcoord3;
	
	d.screenPos = float4(SSS_WorldToScreen(iPosition), 1.0);
	d.screenUV  = d.screenPos.xy;

	d.vertexColor = iColor;
	d.isFrontFace = iIsFrontFace;
	
	d.extraV2F0 = iExtraV2F0;
	d.extraV2F1 = iExtraV2F1;
	d.extraV2F2 = iExtraV2F2;
	d.extraV2F3 = iExtraV2F3;
	d.extraV2F4 = iExtraV2F4;
	d.extraV2F5 = iExtraV2F5;
	d.extraV2F6 = iExtraV2F6;
	d.extraV2F7 = iExtraV2F7;

	d.TBNMatrix = float3x3(d.worldSpaceTangent, normalize(cross(d.worldSpaceNormal, d.worldSpaceTangent)), d.worldSpaceNormal);
	
	SSS_Frag(s, d);
	
	iPosition = SSS_AbsoluteToWorld(d.worldSpacePosition); iNormal = d.worldSpaceNormal; iTangent = d.worldSpaceTangent; // Write back
	
	oExtra      = float4x4(d.extraV2F0, d.extraV2F1, d.extraV2F2, d.extraV2F3);
	oAlbedo     = s.Albedo;
	oSmoothness = s.Smoothness;
	oNormal     = s.Normal;
	oEmission   = s.Emission;
	oOcclusion  = s.Occlusion;
	oMetallic   = s.Metallic;
	oAlpha      = s.Alpha;
}



	#pragma shader_feature_local _SGT_SHAPE_SQUARE _SGT_SHAPE_SPHERE
	#pragma shader_feature_local _SGT_CLOUDS_OFF _SGT_CLOUDS
	#pragma shader_feature_local _SGT_OCEAN_FADE_OFF _SGT_OCEAN_FADE
	#pragma shader_feature_local _SGT_SKY_OFF _SGT_SKY
	#pragma multi_compile __ CW_COMPRESS_POSITIONS



// Graph Functions
// GraphFunctions: <None>

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
float4 extraV2F0;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
float4 _UV_ccc703dc3bdd420e8a8a4aa0607d4ec4_Out_0_Vector4 = IN.uv0;
float4 _UV_46571950324b44e8bf8895f08abd191f_Out_0_Vector4 = IN.uv1;
float4 _UV_ba4ef72275334f08b9b01fedf9da0065_Out_0_Vector4 = IN.uv2;
float4 _UV_255ab3a515d14af686e70c40d04416e6_Out_0_Vector4 = IN.uv3;
float3 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oPosition_0_Vector3;
float3 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oNormal_5_Vector3;
float3 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oTangent_6_Vector3;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F0_7_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F1_8_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F2_13_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F3_14_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F4_15_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F5_16_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F6_17_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F7_18_Vector4;
Vert_float(IN.InstanceID, IN.ObjectSpacePosition, IN.ObjectSpaceNormal, IN.ObjectSpaceTangent, IN.VertexColor, _UV_ccc703dc3bdd420e8a8a4aa0607d4ec4_Out_0_Vector4, _UV_46571950324b44e8bf8895f08abd191f_Out_0_Vector4, _UV_ba4ef72275334f08b9b01fedf9da0065_Out_0_Vector4, _UV_255ab3a515d14af686e70c40d04416e6_Out_0_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oPosition_0_Vector3, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oNormal_5_Vector3, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oTangent_6_Vector3, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F0_7_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F1_8_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F2_13_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F3_14_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F4_15_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F5_16_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F6_17_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F7_18_Vector4);
description.Position = _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oPosition_0_Vector3;
description.Normal = _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oNormal_5_Vector3;
description.Tangent = _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oTangent_6_Vector3;
description.extraV2F0 = _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F0_7_Vector4;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
output.extraV2F0 = input.extraV2F0;
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
float Alpha;
float AlphaClipThreshold;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
float _IsFrontFace_5b03f99e2e7841c3a7d2ae306590b576_Out_0_Boolean = max(0, IN.FaceSign.x);
float4 _UV_c1e39353e82e434da821b9bdc4338e4b_Out_0_Vector4 = IN.uv0;
float4 _UV_64d51b5974bc4ecf849e7307e6de2727_Out_0_Vector4 = IN.uv1;
float4x4 _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oExtra_23_Matrix4;
float3 _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oAlbedo_0_Vector3;
float _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oSmoothness_5_Float;
float3 _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oNormal_6_Vector3;
float3 _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oEmission_7_Vector3;
float _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oOcclusion_8_Float;
float _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oMetallic_9_Float;
float _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oAlpha_10_Float;
Frag_float(IN.WorldSpacePosition, IN.WorldSpaceNormal, IN.WorldSpaceTangent, _IsFrontFace_5b03f99e2e7841c3a7d2ae306590b576_Out_0_Boolean, float4 (0, 0, 0, 0), _UV_c1e39353e82e434da821b9bdc4338e4b_Out_0_Vector4, _UV_64d51b5974bc4ecf849e7307e6de2727_Out_0_Vector4, float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), IN.extraV2F0, float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oExtra_23_Matrix4, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oAlbedo_0_Vector3, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oSmoothness_5_Float, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oNormal_6_Vector3, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oEmission_7_Vector3, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oOcclusion_8_Float, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oMetallic_9_Float, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oAlpha_10_Float);
surface.Alpha = _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oAlpha_10_Float;
surface.AlphaClipThreshold = float(0.5);
return surface;
}

// --------------------------------------------------
// Build Graph Inputs

VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;
    output.uv0 =                                        input.uv0;
    output.uv1 =                                        input.uv1;
    output.uv2 =                                        input.uv2;
    output.uv3 =                                        input.uv3;
    output.VertexColor =                                input.color;
#if UNITY_ANY_INSTANCING_ENABLED
    output.InstanceID =                                 unity_InstanceID;
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
    output.InstanceID =                                 input.instanceID;
#endif

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

    output.extraV2F0 = input.extraV2F0;

    // must use interpolated tangent, bitangent and normal before they are normalized in the pixel shader.
    float3 unnormalizedNormalWS = input.normalWS;
    const float renormFactor = 1.0 / length(unnormalizedNormalWS);


    output.WorldSpaceNormal = renormFactor * input.normalWS.xyz;      // we want a unit length Normal Vector node in shader graph

    // to preserve mikktspace compliance we use same scale renormFactor as was used on the normal.
    // This is explained in section 2.2 in "surface gradient based bump mapping framework"
    output.WorldSpaceTangent = renormFactor * input.tangentWS.xyz;

    output.WorldSpacePosition = input.positionWS;

    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


    output.uv0 = input.texCoord0;
    output.uv1 = input.texCoord1;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
    BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

void BuildAppDataFull(Attributes attributes, VertexDescription vertexDescription, inout appdata_full result)
{
    result.vertex     = float4(attributes.positionOS, 1);
    result.tangent    = attributes.tangentOS;
    result.normal     = attributes.normalOS;
    result.texcoord   = attributes.uv0;
    result.texcoord1  = attributes.uv1;
    result.texcoord2  = attributes.uv2;
    result.texcoord3  = attributes.uv3;
    result.color      = attributes.color;
    result.vertex     = float4(vertexDescription.Position, 1);
    result.normal     = vertexDescription.Normal;
    result.tangent    = float4(vertexDescription.Tangent, 0);
    #if UNITY_ANY_INSTANCING_ENABLED
    result.instanceID = attributes.instanceID;
    #endif
}

void VaryingsToSurfaceVertex(Varyings varyings, inout v2f_surf result)
{
    result.pos = varyings.positionCS;
    result.worldPos = varyings.positionWS;
    result.worldNormal = varyings.normalWS;
    // World Tangent isn't an available input on v2f_surf


    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
    #if UNITY_SHOULD_SAMPLE_SH
    #if !defined(LIGHTMAP_ON)
    #endif
    #endif
    #if defined(LIGHTMAP_ON)
    #endif
    #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        result.fogCoord = varyings.fogFactorAndVertexLight.x;
        COPY_TO_LIGHT_COORDS(result, varyings.fogFactorAndVertexLight.yzw);
    #endif

    DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(varyings, result);
}

void SurfaceVertexToVaryings(v2f_surf surfVertex, inout Varyings result)
{
    result.positionCS = surfVertex.pos;
    result.positionWS = surfVertex.worldPos;
    result.normalWS = surfVertex.worldNormal;
    // viewDirectionWS is never filled out in the legacy pass' function. Always use the value computed by SRP
    // World Tangent isn't an available input on v2f_surf

    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
    #if UNITY_SHOULD_SAMPLE_SH
    #if !defined(LIGHTMAP_ON)
    #endif
    #endif
    #if defined(LIGHTMAP_ON)
    #endif
    #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        result.fogFactorAndVertexLight.x = surfVertex.fogCoord;
        COPY_FROM_LIGHT_COORDS(result.fogFactorAndVertexLight.yzw, surfVertex);
    #endif

    DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(surfVertex, result);
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/DepthOnlyPass.hlsl"

ENDHLSL
}
Pass
{
    Name "ScenePickingPass"
    Tags
    {
        "LightMode" = "Picking"
    }

// Render State
Cull Back

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM
#define _SSS_PASS_SCENEPICKINGPASS 1

#define _SSS_BIRP 1


// Pragmas
#pragma target 3.0
#pragma multi_compile_instancing
#pragma vertex vert
#pragma fragment frag

// Keywords
// PassKeywords: <None>
// GraphKeywords: <None>

// Defines
#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_TEXCOORD0
#define ATTRIBUTES_NEED_TEXCOORD1
#define ATTRIBUTES_NEED_TEXCOORD2
#define ATTRIBUTES_NEED_TEXCOORD3
#define ATTRIBUTES_NEED_COLOR
#define ATTRIBUTES_NEED_INSTANCEID
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_NORMAL_WS
#define VARYINGS_NEED_TANGENT_WS
#define VARYINGS_NEED_TEXCOORD0
#define VARYINGS_NEED_TEXCOORD1
#define VARYINGS_NEED_CULLFACE
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS ScenePickingPass
#define BUILTIN_TARGET_API 1
#define SCENEPICKINGPASS 1
#define _BUILTIN_AlphaClip 1
#define _BUILTIN_ALPHATEST_ON 1
#ifdef _BUILTIN_SURFACE_TYPE_TRANSPARENT
#define _SURFACE_TYPE_TRANSPARENT _BUILTIN_SURFACE_TYPE_TRANSPARENT
#endif
#ifdef _BUILTIN_ALPHATEST_ON
#define _ALPHATEST_ON _BUILTIN_ALPHATEST_ON
#endif
#ifdef _BUILTIN_AlphaClip
#define _AlphaClip _BUILTIN_AlphaClip
#endif
#ifdef _BUILTIN_ALPHAPREMULTIPLY_ON
#define _ALPHAPREMULTIPLY_ON _BUILTIN_ALPHAPREMULTIPLY_ON
#endif


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Shim/Shims.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/LegacySurfaceVertex.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/ShaderGraphFunctions.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
 float4 uv0 : TEXCOORD0;
 float4 uv1 : TEXCOORD1;
 float4 uv2 : TEXCOORD2;
 float4 uv3 : TEXCOORD3;
 float4 color : COLOR;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float3 positionWS;
 float3 normalWS;
 float4 tangentWS;
 float4 texCoord0;
 float4 texCoord1;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
 float4 extraV2F0;
};
struct SurfaceDescriptionInputs
{
 float3 WorldSpaceNormal;
 float3 WorldSpaceTangent;
 float3 WorldSpacePosition;
 float4 uv0;
 float4 uv1;
 float FaceSign;
 float4 extraV2F0;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
 float4 uv0;
 float4 uv1;
 float4 uv2;
 float4 uv3;
 float4 VertexColor;
 uint InstanceID;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
 float4 tangentWS : INTERP0;
 float4 texCoord0 : INTERP1;
 float4 texCoord1 : INTERP2;
 float4 extraV2F0 : INTERP3;
 float3 positionWS : INTERP4;
 float3 normalWS : INTERP5;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
output.tangentWS.xyzw = input.tangentWS;
output.texCoord0.xyzw = input.texCoord0;
output.texCoord1.xyzw = input.texCoord1;
output.extraV2F0.xyzw = input.extraV2F0;
output.positionWS.xyz = input.positionWS;
output.normalWS.xyz = input.normalWS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
output.tangentWS = input.tangentWS.xyzw;
output.texCoord0 = input.texCoord0.xyzw;
output.texCoord1 = input.texCoord1.xyzw;
output.extraV2F0 = input.extraV2F0.xyzw;
output.positionWS = input.positionWS.xyz;
output.normalWS = input.normalWS.xyz;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties
CBUFFER_START(UnityPerMaterial)





CBUFFER_END


// Object and Global properties

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Includes
// UNITY_SHADER_NO_UPGRADE
float3 SSS_HClipToScreen(float4 v)
{
	float3 uv = v.xyz / v.w;
	#if UNITY_UV_STARTS_AT_TOP
		uv.y = -uv.y;
	#endif
	uv.xy = uv.xy * 0.5 + 0.5;
	return uv;
}

#if _SSS_HDRP
	float3 SSS_WorldToAbsolute(float3 v) { return GetAbsolutePositionWS(v); }
	float3 SSS_AbsoluteToWorld(float3 v) { return GetCameraRelativePositionWS(v); }
#else
	float3 SSS_WorldToAbsolute(float3 v) { return v; }
	float3 SSS_AbsoluteToWorld(float3 v) { return v; }
#endif

float3 SSS_WorldToView(float3 v) { return TransformWorldToView(SSS_AbsoluteToWorld(v)); }
float3 SSS_WorldToObject(float3 v) { return TransformWorldToObject(SSS_AbsoluteToWorld(v)); }
float3 SSS_WorldToScreen(float3 v) { return SSS_HClipToScreen(TransformWorldToHClip(SSS_AbsoluteToWorld(v))); }
float3 SSS_ObjectToScreen(float3 v) { return SSS_HClipToScreen(TransformObjectToHClip(v)); }
float3 SSS_ObjectToWorld(float3 v) { return SSS_WorldToAbsolute(TransformObjectToWorld(v)); }
float3 SSS_ObjectToView(float3 v) { return TransformWorldToView(TransformObjectToWorld(v)); }
float3 SSS_ScreenToWorld(float3 v) { return SSS_WorldToAbsolute(ComputeWorldSpacePosition(v.xy, v.z, UNITY_MATRIX_I_VP)); }
float3 SSS_ScreenToObject(float3 v) { return SSS_WorldToObject(SSS_ScreenToWorld(v)); }
float3 SSS_ScreenToView(float3 v) { return SSS_WorldToView(SSS_ScreenToWorld(v)); }
float3 SSS_ViewToWorld(float3 v) { return mul(UNITY_MATRIX_I_V, float4(v, 1.0)).xyz; }
float3 SSS_ViewToObject(float3 v) { return TransformWorldToObject(SSS_ViewToWorld(v)); }
float3 SSS_ViewToScreen(float3 v) { return SSS_HClipToScreen(TransformWViewToHClip(v)); }
float3 SSS_ObjectToWorldDir(float3 v)
{
	#if _SSS_BIRP
		return TransformObjectToWorldDir(v);
	#else
		return TransformObjectToWorldDir(v, true);
	#endif
}
float3 SSS_ObjectToViewDir(float3 v)
{
	#if _SSS_BIRP
		return TransformWorldToViewDir(TransformObjectToWorldDir(v));
	#else
		return TransformWorldToViewDir(TransformObjectToWorldDir(v, false), true);
	#endif
}
float3 SSS_WorldToObjectDir(float3 v)
{
	#if _SSS_BIRP
		return TransformWorldToObjectDir(v);
	#else
		return TransformWorldToObjectDir(v, true);
	#endif
}
float3 SSS_WorldToViewDir(float3 v)
{
	#if _SSS_BIRP
		return TransformWorldToViewDir(v);
	#else
		return TransformWorldToViewDir(v, true);
	#endif
}
float3 SSS_ViewToObjectDir(float3 v)
{
	#if _SSS_URP || _SSS_HDRP
		return SSS_WorldToObjectDir(mul(UNITY_MATRIX_I_V, float4(v, 0.0)).xyz);
	#else
		return SSS_WorldToObjectDir(mul((float3x3)UNITY_MATRIX_I_V, v));
	#endif
}
float3 SSS_ViewToWorldDir(float3 v)
{
	return mul(UNITY_MATRIX_I_V, float4(v, 0.0)).xyz;
}

#if _SSS_NO_DERIVATIVES
	float3 SSS_GetSceneColor(float2 uv) { return float3(0.0, 0.0, 0.0); }
	float3 SSS_GetSceneColorHD(float2 uv) { return SSS_GetSceneColor(uv); }
	float  SSS_GetSceneDepth(float2 uv) { return 0.0; }
#else
	#if _SSS_URP
		float3 SSS_GetSceneColor(float2 uv) { return SHADERGRAPH_SAMPLE_SCENE_COLOR(uv).xyz; }
		float3 SSS_GetSceneColorHD(float2 uv) { return SSS_GetSceneColor(uv); }
	#elif _SSS_HDRP
		float3 SSS_GetSceneColor(float2 uv) { return SHADERGRAPH_SAMPLE_SCENE_COLOR(uv).xyz; }
		float3 SSS_GetSceneColorHD(float2 uv)
		{
			#if defined(REQUIRE_OPAQUE_TEXTURE) && defined(_SURFACE_TYPE_TRANSPARENT) && defined(SHADERPASS) && (SHADERPASS != SHADERPASS_LIGHT_TRANSPORT) && (SHADERPASS != SHADERPASS_PATH_TRACING) && (SHADERPASS != SHADERPASS_RAYTRACING_VISIBILITY) && (SHADERPASS != SHADERPASS_RAYTRACING_FORWARD)
			return SampleCameraColor(uv, 0);
			#endif
			#if defined(REQUIRE_OPAQUE_TEXTURE) && defined(CUSTOM_PASS_SAMPLING_HLSL) && defined(SHADERPASS) && (SHADERPASS == SHADERPASS_DRAWPROCEDURAL || SHADERPASS == SHADERPASS_BLIT)
			return CustomPassSampleCameraColor(uv, 0);
			#endif
			return float3(0.0, 0.0, 0.0);
		}
	#else
		#if defined(UNITY_DECLARE_OPAQUE_TEXTURE_INCLUDED)
			float3 SSS_GetSceneColor(float2 uv) { return SampleSceneColor(uv); }
		#else
			sampler2D _CameraOpaqueTexture; float3 SSS_GetSceneColor(float2 uv) { return tex2D(_CameraOpaqueTexture, uv).xyz; }
		#endif
		float3 SSS_GetSceneColorHD(float2 uv) { return SSS_GetSceneColor(uv); }
	#endif

	float SSS_GetSceneDepth(float2 uv) { return SHADERGRAPH_SAMPLE_SCENE_DEPTH(uv); }
#endif

float3 SSS_GetSceneWorldPosition(float2 screenUV, float sceneDepth)
{
	#if _SSS_BIRP
		float4 clipPos  = float4(screenUV * 2.0f - 1.0f, 0.0f, 1.0f);
		float4 viewPos  = mul(unity_CameraInvProjection, clipPos);
		float3 worldDir = mul((float3x3)UNITY_MATRIX_I_V, viewPos.xyz);
					
		return _WorldSpaceCameraPos + worldDir * LinearEyeDepth(sceneDepth);
	#else
		float4 clipPos = float4(screenUV * 2.0 - 1.0, sceneDepth, 1.0);
					
		#if UNITY_UV_STARTS_AT_TOP
			clipPos.y = -clipPos.y;
		#endif
					
		float4 worldPos = mul(UNITY_MATRIX_I_VP, clipPos);
					
		worldPos.xyz /= worldPos.w;
					
		#if _SSS_HDRP
			worldPos.xyz = GetAbsolutePositionWS(worldPos.xyz);
		#endif
					
		return worldPos.xyz;
	#endif
}

float SSS_GetSceneWorldDistance(float2 screenUV, float sceneDepth)
{
	return distance(_WorldSpaceCameraPos, SSS_GetSceneWorldPosition(screenUV, sceneDepth));
}

float3 SSS_UnpackNormalScale(float4 c, float s)
{
	#if _SSS_BIRP
		return UnpackScaleNormal(c, s);
	#else
		return UnpackNormalScale(c, s);
	#endif
}

struct SSS_VertexData
{
	float  instanceID;
	float3 position;
	float3 normal;
	float3 tangent;
	float4 color;
	float4 texcoord0;
	float4 texcoord1;
	float4 texcoord2;
	float4 texcoord3;
	float4 extraV2F0;
	float4 extraV2F1;
	float4 extraV2F2;
	float4 extraV2F3;
	float4 extraV2F4;
	float4 extraV2F5;
	float4 extraV2F6;
	float4 extraV2F7;
	



};

struct SSS_FragmentData
{
	float3 localSpacePosition;
	float3 localSpaceNormal;
	float3 localSpaceTangent;
	
	float3 worldSpacePosition;
	float3 worldSpaceNormal;
	float3 worldSpaceTangent;
	//float tangentSign;

	float3 worldSpaceViewDir;
	//float3 tangentSpaceViewDir;
	
	float4 texcoord0;
	float4 texcoord1;
	float4 texcoord2;
	float4 texcoord3;
	
	float2 screenUV;
	float4 screenPos;

	float4 vertexColor;
	bool isFrontFace;
	
	float4 extraV2F0;
	float4 extraV2F1;
	float4 extraV2F2;
	float4 extraV2F3;
	float4 extraV2F4;
	float4 extraV2F5;
	float4 extraV2F6;
	float4 extraV2F7;

	float3x3 TBNMatrix;
	



};

struct SSS_SurfaceData
{
	float3 Albedo;
	float  Smoothness;
	float3 Normal;
	float3 Emission;
	float  Occlusion;
	float  Metallic;
	float  Alpha;
};

float2 _CW_PositionCompressionData; // x = start distance, y = max distance limit

#define DEBUG_SCALE 0.001
#define SSS_LOG_K          0.0001
#define SSS_MAX_WORLD_DIST 1e27

float SSS_GetCompressionRatio(float worldDist)
{
    float startDist = _CW_PositionCompressionData.x;
    float maxDist   = _CW_PositionCompressionData.y;

    // no compression nearby
    if (worldDist <= startDist)
        return 1.0;

    float excess = worldDist - startDist;

    float compressedDist =
        startDist +
        (maxDist - startDist) *
        log(1.0 + excess * SSS_LOG_K) /
        log(1.0 + SSS_MAX_WORLD_DIST * SSS_LOG_K);

    return compressedDist / worldDist;
}

float SSS_DecompressWorldDist(float compressedDist)
{
    float startDist = _CW_PositionCompressionData.x;
    float maxDist   = _CW_PositionCompressionData.y;

    if (compressedDist <= startDist)
        return compressedDist;

    float t =
        saturate(
            (compressedDist - startDist) /
            (maxDist - startDist)
        );

    return startDist +
        (
            exp(
                t *
                log(1.0 + SSS_MAX_WORLD_DIST * SSS_LOG_K)
            ) - 1.0
        ) / SSS_LOG_K;
}

float3 SSS_CompressWorld(float3 worldPos, float3 worldCenter, float worldRadius, out float ratio) 
{
    float3 worldDelta = worldPos - _WorldSpaceCameraPos;
    float  farDist    = distance(_WorldSpaceCameraPos, worldCenter) + worldRadius;
    
    ratio = SSS_GetCompressionRatio(farDist);
    
    return _WorldSpaceCameraPos + worldDelta * ratio;
}

float3 SSS_CompressWorld(float3 worldPos) 
{
    float3 worldDelta = worldPos - _WorldSpaceCameraPos;
    float  worldDist  = length(worldDelta);
    
    return _WorldSpaceCameraPos + worldDelta * SSS_GetCompressionRatio(worldDist);
}

float3 SSS_DecompressWorld(float3 worldPos)
{
    float3 worldDelta = worldPos - _WorldSpaceCameraPos;
    float  worldDist  = length(worldDelta);
    
    return worldDist > 0.001 ? _WorldSpaceCameraPos + (worldDelta / worldDist) * SSS_DecompressWorldDist(worldDist) : _WorldSpaceCameraPos;
}

float3 SSS_DecompressWorld(float3 worldPos, float ratio) 
{
    float3 worldDelta = worldPos - _WorldSpaceCameraPos;
    return _WorldSpaceCameraPos + worldDelta / ratio;
}

float SSS_ElongateDistance(float worldDist)
{
	float minDist = 10000.0;
	float maxDist = 100000.0;
	float scale   = saturate((worldDist - minDist) / (maxDist - minDist));

	return worldDist * lerp(1.0, 1.2, scale);
}
#define SGT_OPACITY_STEPS  5

float4x4        _SGT_SkyToWorld;
float4x4        _SGT_WorldToSky;
float4          _SGT_SkyColor;
float           _SGT_SkyBrightness;
float4          _SGT_SkyAmbientTint;
float           _SGT_SkyExposure;
float4          _SGT_SkyRadius;
float4          _SGT_SkyAltitudeData;
float           _SGT_SkyDensity;
float4          _SGT_SkyMieWeight;
float4          _SGT_SkyMieData;
sampler3D       _SGT_SkyRadianceTex;
float           _SGT_SkyRadianceLod;
sampler2D       _SGT_SkyLightingTex;
sampler2D       _SGT_SkyAlbedoTex;
float           _SGT_SkyRevealStars;
float           _SGT_SkyDepthOpaque;

float     _SGT_Volumetrics_Downscale; // Global
float     _SGT_Volumetrics_NearClip; // Global
Texture2D _SGT_Volumetrics_ColorTex; // Global
float4    _SGT_Volumetrics_ColorSize; // Global
Texture2D _SGT_Volumetrics_DepthTex; // Global
float2    _SGT_Volumetrics_DepthSize; // Global

SamplerState my_linear_clamp_sampler;
SamplerState my_point_clamp_sampler;

sampler2D _SGT_BlueNoiseTex; // Global
float     _SGT_Frame; // Global
float3    _SGT_AmbientColor; // Global
	
float SGT_DitherBlue(float2 screenUV)
{
	float2 pixel = floor(screenUV * _ScreenParams.xy);
	float  noise = tex2D(_SGT_BlueNoiseTex, pixel / 64.0f).r;
	return frac(noise + _SGT_Frame / sqrt(0.5f));
}

float SGT_DitherFast(float2 screenUV)
{
    float2 R2 = float2(0.7548776662, 0.5698402909);
    float2 p  = screenUV * _ScreenParams.xy + frac(_Time.y * R2) * 256.0;
    return frac(52.9829189 * frac(dot(p, float2(0.06711056, 0.00583715))));
}
	
float2 SGT_DirectionToEquirectangular(float3 dir)
{
	dir = normalize(dir);
	float u = atan2(dir.z, dir.x) / (2.0 * 3.141592653) + 0.5;
	float v = asin(clamp(dir.y, -1.0, 1.0)) / 3.141592653 + 0.5;
	return float2(u, v);
}

float3 SGT_SphereTest(float3 ray, float3 rayD, float radius)
{
	float B = -dot(ray, rayD);
	float C = dot(ray, ray) - radius * radius;
	float D = B * B - C;
	float E = sqrt(max(D, 0.0f));
	return float3(B - E, B + E, D);
}

float SGT_InverseLerp(float a, float b, float v)
{
	return (v - a) / (b - a);
}

float3 SGT_CalculateMie(float3 ray, float mie)
{
	return normalize(ray * _SGT_SkyMieWeight.xyz + 1e-6) * mie;
}

float SGT_GetPitch01(float3 rayDir, float3 up, float altitude01)
{
	float power = 2.0f;
	float eyeHeight = _SGT_SkyRadius.x + altitude01 * (_SGT_SkyRadius.y - _SGT_SkyRadius.x);
	float horizonAngleFromDown = -acos(clamp(_SGT_SkyRadius.x / max(eyeHeight, 0.00001f), -1.0f, 1.0f));
	float rayAng = asin(clamp(dot(up, rayDir), -1.0f, 1.0f));
	float sun01;

	if (rayAng < horizonAngleFromDown)
	{
		sun01 = SGT_InverseLerp(-3.141592653 * 0.5f, horizonAngleFromDown, rayAng);
		sun01 = 1.0f - pow(1.0f - sun01, 1.0f / power);
		sun01 = sun01 * 0.5f;
	}
	else
	{
		sun01 = SGT_InverseLerp(horizonAngleFromDown, 3.141592653 * 0.5f, rayAng);
		sun01 = pow(sun01, 1.0f / power);
		sun01 = sun01 * 0.5f + 0.5f;
	}

	return sun01;
}

float3 GetLutCoord(float3 rayPos, float3 rayDir, float3 sunDir, float dither)
{
	float3 up         = normalize(rayPos);
	float  eyeHeight  = length(rayPos);
	float  altitude01 = saturate(SGT_InverseLerp(_SGT_SkyRadius.z, 1.0f, eyeHeight));
	float  pitch01    = SGT_GetPitch01(rayDir, up, altitude01);
	float  sun01      = SGT_GetPitch01(sunDir, up, altitude01);
	
	pitch01 += (dither - 0.5f) / 128.0f;
	sun01   += (dither - 0.5f) /  64.0f;
	
	return float3(pitch01, altitude01, sun01);
}

float2 SGT_GetDensity2(float3 pos, float weightMul)
{
	float alt01 = saturate((1.0f - length(pos)) * _SGT_SkyAltitudeData.z);

	return pow(alt01, _SGT_SkyAltitudeData.xy * weightMul) * _SGT_SkyDensity;
}

float SGT_GetOpacity(float3 rayPos, float3 rayDir, float rayMax, float weightMul)
{
	float2 opticalDepth = 0.0f;
	float  rayStep      = rayMax / SGT_OPACITY_STEPS;
	float  t            = rayStep * 0.5f;
	
	for (int i = 0; i < SGT_OPACITY_STEPS; i++)
	{
		float3 pos = rayPos + rayDir * t;

		opticalDepth += SGT_GetDensity2(pos, weightMul) * rayStep;
		
		t += rayStep;
	}
	
	return 1.0f - exp((opticalDepth.x + opticalDepth.y) * -20.0f);
}

float4 SGT_SampleLUT(float3 coord)
{
	return tex3Dlod(_SGT_SkyRadianceTex, float4(coord, _SGT_SkyRadianceLod));
}

float3 SGT_AtmosphereColor(float3 rayPos, float3 sunDir, float dither)
{
	float3 up         = normalize(rayPos);
	float  eyeHeight  = length(rayPos);
	float  altitude01 = saturate(SGT_InverseLerp(_SGT_SkyRadius.z, 1.0f, eyeHeight));
	float  sun01      = 0.5f + 0.5f * dot(sunDir, up);
		
	return tex2Dlod(_SGT_SkyLightingTex, float4(sun01, altitude01, 0.0f, 0.0f)).xyz;
}

float3 SGT_UnderwaterAtmosphere(float3 rayPos, float3 rayDir, float3 sunDir, float exposure, float dither)
{
	float3 coord = GetLutCoord(rayPos, rayDir, sunDir, dither); coord.x = 1.0f;
	float4 ray   = SGT_SampleLUT(coord);
	float3 mie   = SGT_CalculateMie(ray.xyz, ray.w);
	float3 color = ray.xyz + mie;
	
	#if _SSS_HDRP
		color *= exposure * 0.25f;
	#else
		color = 1.0 - exp(-color * exposure);
	#endif
	
	return saturate(_SGT_SkyColor.xyz * color * _SGT_SkyBrightness);
}

float4 SGT_Atmosphere(float3 rayPos, float3 rayDir, float rayFar, float rayMax, float3 sunDir, float exposure, float dither, float mieMul, float weightMul)
{
	// 1. Calculate opacity FIRST so we can use it for blending
	float opacity = SGT_GetOpacity(rayPos, rayDir, rayMax, weightMul);

	float3 coord  = GetLutCoord(rayPos, rayDir, sunDir, dither);
	float2 mieOff = 0.0f;
	
	if (rayMax * 1.02f <= rayFar) // If ground
	{
		// 2. Store the true, un-clamped sky coordinate
		float trueCoordX = coord.x;
		
		// 3. Calculate the clamped "below horizon" coordinate (original behavior)
		float clampedCoordX = min(coord.x, 0.5f - (1.0f / 128.0f));
		clampedCoordX = lerp(0.5f - (1.0f / 128.0f), clampedCoordX, coord.y * coord.y);
		
		// 4. Smoothly blend towards the true sky color as fog density/opacity increases
		coord.x = lerp(clampedCoordX, trueCoordX, opacity);
		mieOff = lerp(0.2f, 0.0f, opacity);
	}
	else
	{
		//coord.x = max(coord.x, 0.5f + (1.0f / 128.0f));
	}
	
	float4 ray    = SGT_SampleLUT(coord);
	float3 mie    = SGT_CalculateMie(ray.xyz, ray.w);
	float2 g      = lerp(_SGT_SkyMieData.xy, 0.0f, mieOff);
	float  c      = dot(rayDir, sunDir);
	float  phaseR = 3.0f / (16.0f * 3.141592653f) * (1.0f + c * c);
	float2 phaseM = 3.0f / (8.0f * 3.141592653f) * ((1.0f - g * g) * (c * c + 1.0f)) / (pow(abs(1.0f + g * g - 2.0f * c * g), 1.5f) * (2.0f + g * g));
	float3 color  = ray.xyz * phaseR + mie * dot(phaseM, 1) * mieMul;
	
	#if _SSS_HDRP
		color *= exposure * 0.25f;
	#else
		color = 1.0 - exp(-color * exposure);
	#endif
	
	#if _SSS_HDRP
		color += _SGT_AmbientColor.xyz * _SGT_SkyAmbientTint.xyz * 0.25;
	#else
		color += _SGT_AmbientColor.xyz * _SGT_SkyAmbientTint.xyz;
	#endif

	#if _SGT_SOLID_CORE_OFF // If the atmosphere below InnerMeshRadius isn't solid, then continue to darken because the LUT doesn't extend any farther
		float eyeHeight     = length(rayPos);
		float inner         = _SGT_SkyRadius.z;
		float interiorDepth = saturate((_SGT_SkyRadius.z - eyeHeight) / _SGT_SkyRadius.z);

		if (interiorDepth > 0.0)
		{
			float extinction = exp(-interiorDepth * 20.0 * _SGT_SkyDensity);

			color *= extinction;
			opacity = lerp(opacity, 1.0f, saturate(interiorDepth * 100.0f));
		}
	#endif
	
	return float4(saturate(_SGT_SkyColor.xyz * color * _SGT_SkyBrightness), opacity);
}

// Global cached light properties
float3 _SGT_CachedLightDirection;
float3 _SGT_CachedLightColor;

float3 SGT_SampleSkyReflection(float3 ocam, float3 odir)
{
    // 2. Intersect with the Atmosphere Shell (Outer radius is 1.0 in sky space)
    float3 odst = SGT_SphereTest(ocam, odir, 1.0f);
    odst.xy = max(odst.xy, 0.0f); // Ensure distances are ahead of ray
    
    float3 rayPos = ocam + odir * odst.x;
    float3 rayDir = odir;
    
    // 3. Determine Atmosphere Limits
    float rayFar = SGT_SphereTest(rayPos, rayDir, 1.0f).y; // Distance to sky exit
    float rayMax = odst.y - odst.x;                       // Max scattering distance
    
    // Optional: Intersect with the planet core so reflections don't "see through" the world
    #if _SGT_SOLID_CORE
        float3 ihit = SGT_SphereTest(ocam, odir, _SGT_SkyRadius.w);
        if (ihit.z > 0.0f && ihit.x > 0.0f) {
            rayMax = min(ihit.x - odst.x, rayMax);
        }
    #endif

    // 4. Calculate Sun Direction in Sky Space
    float3 sunDir = normalize(mul(_SGT_WorldToSky, float4(_SGT_CachedLightDirection, 0.0f)).xyz);
    
    // 5. Sample the Atmosphere
    // dither is set to 0.0 to keep reflections smooth; mieMul is 1.0
    float4 sky = SGT_Atmosphere(rayPos, rayDir, rayFar, rayMax, sunDir, _SGT_SkyExposure, 0.0, 1.0, 1.0);
    
    // 6. Apply cached light intensity
    return sky.rgb * _SGT_CachedLightColor;
}

float4 SGT_SimpleAtmosphere(float3 worldPos, float3 worldHit, float mieMul, float weightMul)
{
	float dither   = 0.5f;
	
	float3 ocam = mul(_SGT_WorldToSky, float4(worldPos, 1.0f)).xyz;
	float3 ofar = mul(_SGT_WorldToSky, float4(worldHit, 1.0f)).xyz;
	float  omax = distance(ocam, ofar);
	float3 odir = normalize(ofar - ocam);
	float3 odst = SGT_SphereTest(ocam, odir, 1.0f);
	
	odst.xy = max(odst.xy, 0.0f);
	
	float3 rayPos = ocam + odir * odst.x;
	float3 rayDir = odir;
	float  rayFar = SGT_SphereTest(rayPos, rayDir, 1.0f).y;
	float  rayMax = min(odst.y, omax) - odst.x;
	
	#if _SGT_LIGHTING
		float3 sunDir = normalize(mul(_SGT_WorldToSky, float4(_SGT_LightDirection[0].xyz, 0.0f)).xyz);
	#else
		float3 sunDir = normalize(rayPos);
	#endif
	
	return SGT_Atmosphere(rayPos, rayDir, rayFar, rayMax, sunDir, _SGT_SkyExposure, dither, mieMul, weightMul);
}

float3 CW_Depth2World(float viewDepth, float3 viewDir)
{
	return _WorldSpaceCameraPos + viewDir * viewDepth;
}
	
bool CW_DepthInBounds(float viewDepth, float3 viewDir)
{
	float3 wPos = CW_Depth2World(viewDepth, viewDir);
	float3 oPos = mul(_SGT_WorldToSky, float4(wPos, 1.0f)).xyz;
		
	return length(oPos) < 1.2f;
}
	
void CW_SampleVolumetricsDefault(float2 screenUV, float3 viewDir, out float4 outColor, out float outDepth)
{
	float4 centerColor = _SGT_Volumetrics_ColorTex.Sample(my_linear_clamp_sampler, screenUV);
	float  centerDepth = _SGT_Volumetrics_DepthTex.Sample(my_linear_clamp_sampler, screenUV).x;
		
	if (CW_DepthInBounds(centerDepth, viewDir) == true)
	{
		outColor = centerColor;
		outDepth = centerDepth;
	}
	else
	{
		outColor = 0;
		outDepth = 0;
	}
}

void CW_SampleVolumetricsDefault2(float2 screenUV, float3 viewDir, out float4 outColor, out float outDepth)
{
	outColor = _SGT_Volumetrics_ColorTex.Sample(my_linear_clamp_sampler, screenUV);
	outDepth = _SGT_Volumetrics_DepthTex.Sample(my_linear_clamp_sampler, screenUV).x;
}
	
void CW_SampleVolumetrics(float2 screenUV, float3 viewDir, float sceneDepth, out float4 outColor, out float outDepth)
{
	outColor = 0;
	outDepth = 0;
	//CW_SampleVolumetricsDefault(screenUV, viewDir, outColor, outDepth);return;
		
	if (_SGT_Volumetrics_Downscale > 1)
	{
		float2 pixelF   = screenUV * _SGT_Volumetrics_ColorSize.xy - 0.5f;
		float2 pixelI   = floor(pixelF);
		float2 snappedF = pixelF - pixelI;
			
		float2 coord0 = (pixelI + float2(0.5f, 0.5f)) * _SGT_Volumetrics_ColorSize.zw;
		float2 coord1 = (pixelI + float2(1.5f, 0.5f)) * _SGT_Volumetrics_ColorSize.zw;
		float2 coord2 = (pixelI + float2(0.5f, 1.5f)) * _SGT_Volumetrics_ColorSize.zw;
		float2 coord3 = (pixelI + float2(1.5f, 1.5f)) * _SGT_Volumetrics_ColorSize.zw;
			
		float depth0 = _SGT_Volumetrics_DepthTex.Sample(my_point_clamp_sampler, coord0).x;
		float depth1 = _SGT_Volumetrics_DepthTex.Sample(my_point_clamp_sampler, coord1).x;
		float depth2 = _SGT_Volumetrics_DepthTex.Sample(my_point_clamp_sampler, coord2).x;
		float depth3 = _SGT_Volumetrics_DepthTex.Sample(my_point_clamp_sampler, coord3).x;
			
		float4 color0 = _SGT_Volumetrics_ColorTex.Sample(my_point_clamp_sampler, coord0);
		float4 color1 = _SGT_Volumetrics_ColorTex.Sample(my_point_clamp_sampler, coord1);
		float4 color2 = _SGT_Volumetrics_ColorTex.Sample(my_point_clamp_sampler, coord2);
		float4 color3 = _SGT_Volumetrics_ColorTex.Sample(my_point_clamp_sampler, coord3);
			
		float w0 = CW_DepthInBounds(depth0, viewDir) * (depth0 < sceneDepth) * color0.w * (1.0 - snappedF.x) * (1.0 - snappedF.y);
		float w1 = CW_DepthInBounds(depth1, viewDir) * (depth1 < sceneDepth) * color1.w * snappedF.x * (1.0 - snappedF.y);
		float w2 = CW_DepthInBounds(depth2, viewDir) * (depth2 < sceneDepth) * color2.w * (1.0 - snappedF.x) * snappedF.y;
		float w3 = CW_DepthInBounds(depth3, viewDir) * (depth3 < sceneDepth) * color3.w * snappedF.x * snappedF.y;
			
		float wt = w0 + w1 + w2 + w3;
			
		if (wt > 0.0f)
		{
			outColor = (color0 * w0 + color1 * w1 + color2 * w2 + color3 * w3) / wt;
			outDepth = (depth0 * w0 + depth1 * w1 + depth2 * w2 + depth3 * w3) / wt;
		}
		else
		{
			return;
		}
	}
	else
	{
		float2 coord0 = screenUV;
		float  depth0 = _SGT_Volumetrics_DepthTex.Sample(my_point_clamp_sampler, coord0).x;
		float4 color0 = _SGT_Volumetrics_ColorTex.Sample(my_point_clamp_sampler, coord0);
		float  w0     = CW_DepthInBounds(depth0, viewDir) * (depth0 < sceneDepth);
		
		if (w0 > 0.0f)
		{
			outColor = color0;
			outDepth = depth0;
		}
		else
		{
			return;
		}
	}
}






#pragma instancing_options procedural:SetupInstancing
#if _SSS_PASS_SHADOWCASTER || _SSS_PASS_META
	#pragma multi_compile_instancing // For some reason the ShadowCaster and Meta pass in BIRP doesn't have this line
#endif


#define BATCH_CAPACITY 35
#define VERTEX_COUNT 243

float4 _CwSize;
float4 _CwAtlas;
float4 _CwWeights[VERTEX_COUNT];
float4 _CwCoords[VERTEX_COUNT];

float4 _CwOrigins[BATCH_CAPACITY];
float4 _CwPositionsA[BATCH_CAPACITY];
float4 _CwPositionsB[BATCH_CAPACITY];
float4 _CwPositionsC[BATCH_CAPACITY];

sampler2D DataP;
sampler2D DataA;
sampler2D DataN;

float3 _CwOffset;

float4x4 _CwObjectToWorld;
float4x4 _CwWorldToObject;

float _SGT_Occlusion;

// CLOUDS
sampler2D _SGT_CloudShadowTex;
float3    _SGT_CloudShadowDirection;
float4    _SGT_CloudShadowOpacity;
float4x4  _SGT_CloudShadowMatrix;

// OCEAN FADE
float  _SGT_OceanFade;
float2 _SGT_OceanDensity;
float3 _SGT_OceanColor;
float  _SGT_OceanSmoothness;
float4 _SGT_OceanLightDirection;

float _SGT_OceanRadius;

void SetupInstancing()
{
	#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
		#ifdef unity_ObjectToWorld
			#undef unity_ObjectToWorld
		#endif

		#ifdef unity_WorldToObject
			#undef unity_WorldToObject
		#endif
		
		unity_ObjectToWorld = _CwObjectToWorld;
		unity_WorldToObject = _CwWorldToObject;
	#endif
}

float SGT_SampleCloudDensity(float3 wpos)
{
	float3 cpos  = mul(_SGT_CloudShadowMatrix, float4(wpos, 1.0)).xyz;
	float3 chit  = SGT_SphereTest(cpos, _SGT_CloudShadowDirection, 1.0);
	float2 uv    = SGT_DirectionToEquirectangular(normalize(cpos + chit.y * _SGT_CloudShadowDirection));
	
	return saturate(dot(tex2D(_SGT_CloudShadowTex, uv), _SGT_CloudShadowOpacity));
}

void SSS_Vert(inout SSS_VertexData v)
{
	float vertexIndex = v.position.x;
	float squareIndex = v.position.y;
	float batchIndex  = v.instanceID;
	
	float  batchCol = batchIndex % _CwAtlas.z;
	float  batchRow = floor(batchIndex / _CwAtlas.z);
	float3 origin   = _CwOrigins[batchIndex].xyz;
	float3 weights  = _CwWeights[vertexIndex].xyz;
	float2 coord    = _CwCoords[vertexIndex].xy; coord.x /= 3.0;
	float3 position = _CwPositionsA[batchIndex].xyz * weights.x + _CwPositionsB[batchIndex].xyz * weights.y + _CwPositionsC[batchIndex].xyz * weights.z;
	
	v.position.xyz = _CwOffset + origin + tex2Dlod(DataP, float4(vertexIndex * _CwSize.x, batchIndex * _CwSize.y, 0.0, 0.0)).xyz;
	
	float3 ocam = mul(_CwWorldToObject, float4(_WorldSpaceCameraPos - _CwOffset, 1.0)).xyz;
	
	#if _SGT_SHAPE_SQUARE
		v.normal  = float3(0,1,0);
		v.tangent = float4(1,0,0,-1).xyz;
		
		v.extraV2F0.w = ocam.y;
	#elif _SGT_SHAPE_SPHERE
		v.normal = normalize(position);
		
		if (position.x == 0.0 && position.z == 0.0)
		{
			position.xz += 0.00000001;
		}
		
		v.tangent = float4(normalize(cross(float3(0,1,0), normalize(position))), -1.0).xyz;
		
		v.extraV2F0.w = length(ocam) - _SGT_OceanRadius;
	#endif
	
	#if _SGT_OCEAN_FADE
		if (_SGT_OceanFade > 0.0 && length(v.position.xyz - _CwOffset) < _SGT_OceanRadius)
		{
			float3 N = normalize(position); // sphere normal
			float3 V = normalize(_WorldSpaceCameraPos - v.position.xyz);
			float  F = pow(abs(1.0 - saturate(dot(N, V))), 2.0);

			float3 oceanPos = _CwOffset + N * _SGT_OceanRadius;
			v.position.xyz = lerp(v.position.xyz, oceanPos, F);
		}

	#endif
	
	// Calc UV
	float2 pixelS = _CwAtlas.xy * _CwAtlas.zw;
	float  pixelX = batchCol * _CwAtlas.x + coord.x * (_CwAtlas.x - 3.0) + 0.5 + squareIndex * (_CwAtlas.x / 3.0);
	float  pixelY = batchRow * _CwAtlas.y + coord.y * (_CwAtlas.y - 1.0) + 0.5;
	
	v.extraV2F0.x = pixelX / pixelS.x;
	v.extraV2F0.y = pixelY / pixelS.y;
	
	#if CW_COMPRESS_POSITIONS
		float3 wpos = mul(_CwObjectToWorld, float4(v.position, 1.0)).xyz;
		v.extraV2F0.z = distance(_WorldSpaceCameraPos, wpos);
		wpos = SSS_CompressWorld(wpos);
		v.position = mul(_CwWorldToObject, float4(wpos, 1.0)).xyz;
	#endif
}

float3 SGT_GetColor(float3 wnormal, float3 wlight)
{
	return saturate(dot(wnormal, wlight));
}

static const float WATER_DEPTH_SHALLOW = 30.0;
static const float WATER_DEPTH_MAX = 3000.0;

float SGT_DecodeWaterDepth(float encoded)
{
	float shallow = encoded * (WATER_DEPTH_SHALLOW * 2.0);
    float deep = WATER_DEPTH_SHALLOW + (encoded - 0.5) * ((WATER_DEPTH_MAX - WATER_DEPTH_SHALLOW) * 2.0);
    float isDeep = step(0.5, encoded);
    return lerp(shallow, deep, isDeep);
}

float3 SGT_ApplyOceanColor(
    float3 terrainColor,
    float  waterDepth01,
    float3 waterColor,
    float2 waterDensity,
    float  waterBlend,
    float3 ambientColor)
{
    float depth = SGT_DecodeWaterDepth(waterDepth01);
    float3 w = float3(1.0, 0.2, 0.1);

	float3 e0 = w * waterDensity.x;
	float3 e1 = w * waterDensity.y;

	float3 floorT0 = exp(-e0 * depth);
	float3 floorT1 = exp(-e1 * depth);
	
	float3 volumeT0 = floorT0 * floorT0 * floorT0;
	float3 volumeT1 = floorT1 * floorT1 * floorT1;

	float3 scatter = waterColor * ambientColor;

	float3 col0 = terrainColor * floorT0 + scatter * (1.0 - volumeT0);
	float3 col1 = terrainColor * floorT1 + scatter * (1.0 - volumeT1);

	return lerp(col0, col1, waterBlend);
}

float SGT_Bayer4x4(float2 screenPos)
{
	uint2 p = uint2(screenPos * _ScreenParams.xy) % 4;
	float bayer[16] = { 0,  8,  2, 10, 12,  4, 14,  6, 3, 11,  1,  9, 15,  7, 13,  5 };
	return (bayer[p.x + p.y * 4] + 0.5) / 16.0;
}

float SGT_Fresnel(float3 dir, float3 normal, float airIOR, float waterIOR)
{
	float cosTheta = clamp(dot(dir, normal), 0.0, 1.0);
	float F0       = pow(abs((waterIOR / airIOR - 1.0) / (waterIOR / airIOR + 1.0)), 2.0);

	return F0 + (1.0 - F0) * pow(abs(1.0 - cosTheta), 5.0);
}

void SSS_Frag(inout SSS_SurfaceData o, inout SSS_FragmentData d)
{
	#if CW_COMPRESS_POSITIONS
		d.worldSpacePosition = _WorldSpaceCameraPos - d.worldSpaceViewDir * d.extraV2F0.z;
	#endif
	
	float4 dataA = tex2Dlod(DataA, float4(d.extraV2F0.xy,0,0));
	float4 dataN = tex2Dlod(DataN, float4(d.extraV2F0.xy,0,0)); 
	float2 tnorm = dataN.xy * 2.0 - 1.0;
	
	tnorm.x = -tnorm.x; // Don't set negative tangent sign, so do it manually
	
	o.Albedo     = dataA.xyz;
	o.Occlusion  = _SGT_Occlusion;
	o.Emission   = dataA.xyz * dataN.z;
	o.Smoothness = dataN.w;
	o.Metallic   = 0.0;
	o.Normal     = float3(tnorm, sqrt(1.0 - saturate(dot(tnorm, tnorm))));
	o.Alpha      = dataN.x != 0.0 && dataN.y != 0.0;
	
	float localEyeHeight = d.extraV2F0.w;
	
	#if _SGT_OCEAN_FADE
		float  fadeTransition = step(SGT_Bayer4x4(d.screenUV), _SGT_OceanFade);
		float  cameraDistance = distance(d.worldSpacePosition, _WorldSpaceCameraPos);
		float  cameraDist01   = 1.0 - pow(abs(saturate(1.0 - cameraDistance / _SGT_OceanRadius)), 8.0);
		float3 oceanColor     = SGT_ApplyOceanColor(o.Albedo, dataA.w, _SGT_OceanColor, _SGT_OceanDensity, cameraDist01, 1.0);
		float  fade           = saturate(dataA.w > 0) * fadeTransition;

		o.Albedo     = lerp(o.Albedo, oceanColor, fade);
		o.Smoothness = lerp(o.Smoothness, _SGT_OceanSmoothness, fade);
		o.Normal     = lerp(o.Normal, float3(0.0, 0.0, 1.0), fade);
		o.Occlusion  = lerp(o.Occlusion, 0.15, fade);

		#if _SGT_SKY
			float  airIOR   = 1.0;
			float  waterIOR = 1.33;
			float3 opos     = mul(_SGT_WorldToSky, float4(d.worldSpacePosition, 1.0)).xyz;
			float3 onrm     = normalize(opos);
			float3 oview    = normalize(mul(_SGT_WorldToSky, float4(d.worldSpaceViewDir, 0.0)).xyz);
			float3 odir     = reflect(oview, onrm);
	
			o.Emission = SGT_SampleSkyReflection(opos, -odir) * fade * SGT_Fresnel(oview, onrm, airIOR, waterIOR);
			#if _SSS_HDRP
				o.Emission *= 120000;
			#endif
		#endif
		//o.Emission +=  _SGT_AmbientColor.xyz * 0.02;
	#endif
	
	#if _SGT_CLOUDS
		float shadow = SGT_SampleCloudDensity(d.worldSpacePosition);
		
		o.Albedo    = lerp(o.Albedo   , 0.0, shadow);
		o.Occlusion = lerp(o.Occlusion, 0.0, shadow);
	#endif
}


void Vert_float
	(
	float  iInstanceID,
	float3 iPosition,
	float3 iNormal,
	float3 iTangent,
	float4 iColor,
	float4 iTexcoord0,
	float4 iTexcoord1,
	float4 iTexcoord2,
	float4 iTexcoord3,

	out float3 oPosition,
	out float3 oNormal,
	out float3 oTangent,
	out float4 oExtraV2F0,
	out float4 oExtraV2F1,
	out float4 oExtraV2F2,
	out float4 oExtraV2F3,
	out float4 oExtraV2F4,
	out float4 oExtraV2F5,
	out float4 oExtraV2F6,
	out float4 oExtraV2F7
	)
{
	SSS_VertexData v = (SSS_VertexData)0;
	
	v.instanceID = iInstanceID;
	v.position   = iPosition;
	v.normal     = iNormal;
	v.tangent    = iTangent;
	v.color      = iColor;
	v.texcoord0  = iTexcoord0;
	v.texcoord1  = iTexcoord1;
	v.texcoord2  = iTexcoord2;
	v.texcoord3  = iTexcoord3;
	v.extraV2F0  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F1  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F2  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F3  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F4  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F5  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F6  = float4(0.0, 0.0, 0.0, 0.0);
	v.extraV2F7  = float4(0.0, 0.0, 0.0, 0.0);
	
	SSS_Vert(v);
	
	oPosition  = v.position;
	oNormal    = v.normal;
	oTangent   = v.tangent;
	oExtraV2F0 = v.extraV2F0;
	oExtraV2F1 = v.extraV2F1;
	oExtraV2F2 = v.extraV2F2;
	oExtraV2F3 = v.extraV2F3;
	oExtraV2F4 = v.extraV2F4;
	oExtraV2F5 = v.extraV2F5;
	oExtraV2F6 = v.extraV2F6;
	oExtraV2F7 = v.extraV2F7;
}

void Frag_float
	(
	inout float3 iPosition,
	inout float3 iNormal,
	inout float3 iTangent,
	bool   iIsFrontFace,
	float4 iColor,
	float4 iTexcoord0,
	float4 iTexcoord1,
	float4 iTexcoord2,
	float4 iTexcoord3,
	float4 iExtraV2F0,
	float4 iExtraV2F1,
	float4 iExtraV2F2,
	float4 iExtraV2F3,
	float4 iExtraV2F4,
	float4 iExtraV2F5,
	float4 iExtraV2F6,
	float4 iExtraV2F7,

	out float4x4 oExtra,
	out float3   oAlbedo,
	out float    oSmoothness,
	out float3   oNormal,
	out float3   oEmission,
	out float    oOcclusion,
	out float    oMetallic,
	out float    oAlpha
	)
{
	SSS_SurfaceData  s = (SSS_SurfaceData)0;
	SSS_FragmentData d = (SSS_FragmentData)0;
	
	s.Albedo = 1.0;
	s.Smoothness = 0.5;
	s.Normal = float3(0.0, 0.0, 1.0);
	s.Emission = float3(0.0, 0.0, 0.0);
	s.Occlusion = 0.0;
	s.Metallic = 0.0;
	s.Alpha = 1.0;
	
	iPosition = SSS_WorldToAbsolute(iPosition);
	
	d.localSpacePosition = SSS_WorldToObject(iPosition);
	d.localSpaceNormal   = normalize(SSS_WorldToObjectDir(iNormal));
	d.localSpaceTangent  = normalize(SSS_WorldToObjectDir(iTangent));
	
	d.worldSpacePosition = iPosition;
	d.worldSpaceNormal   = iNormal;
	d.worldSpaceTangent  = iTangent;
	//d.tangentSign;
	
	d.worldSpaceViewDir  = normalize(_WorldSpaceCameraPos - d.worldSpacePosition);
	//d.tangentSpaceViewDir;
	
	d.texcoord0 = iTexcoord0;
	d.texcoord1 = iTexcoord1;
	d.texcoord2 = iTexcoord2;
	d.texcoord3 = iTexcoord3;
	
	d.screenPos = float4(SSS_WorldToScreen(iPosition), 1.0);
	d.screenUV  = d.screenPos.xy;

	d.vertexColor = iColor;
	d.isFrontFace = iIsFrontFace;
	
	d.extraV2F0 = iExtraV2F0;
	d.extraV2F1 = iExtraV2F1;
	d.extraV2F2 = iExtraV2F2;
	d.extraV2F3 = iExtraV2F3;
	d.extraV2F4 = iExtraV2F4;
	d.extraV2F5 = iExtraV2F5;
	d.extraV2F6 = iExtraV2F6;
	d.extraV2F7 = iExtraV2F7;

	d.TBNMatrix = float3x3(d.worldSpaceTangent, normalize(cross(d.worldSpaceNormal, d.worldSpaceTangent)), d.worldSpaceNormal);
	
	SSS_Frag(s, d);
	
	iPosition = SSS_AbsoluteToWorld(d.worldSpacePosition); iNormal = d.worldSpaceNormal; iTangent = d.worldSpaceTangent; // Write back
	
	oExtra      = float4x4(d.extraV2F0, d.extraV2F1, d.extraV2F2, d.extraV2F3);
	oAlbedo     = s.Albedo;
	oSmoothness = s.Smoothness;
	oNormal     = s.Normal;
	oEmission   = s.Emission;
	oOcclusion  = s.Occlusion;
	oMetallic   = s.Metallic;
	oAlpha      = s.Alpha;
}



	#pragma shader_feature_local _SGT_SHAPE_SQUARE _SGT_SHAPE_SPHERE
	#pragma shader_feature_local _SGT_CLOUDS_OFF _SGT_CLOUDS
	#pragma shader_feature_local _SGT_OCEAN_FADE_OFF _SGT_OCEAN_FADE
	#pragma shader_feature_local _SGT_SKY_OFF _SGT_SKY
	#pragma multi_compile __ CW_COMPRESS_POSITIONS



// Graph Functions
// GraphFunctions: <None>

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
float4 extraV2F0;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
float4 _UV_ccc703dc3bdd420e8a8a4aa0607d4ec4_Out_0_Vector4 = IN.uv0;
float4 _UV_46571950324b44e8bf8895f08abd191f_Out_0_Vector4 = IN.uv1;
float4 _UV_ba4ef72275334f08b9b01fedf9da0065_Out_0_Vector4 = IN.uv2;
float4 _UV_255ab3a515d14af686e70c40d04416e6_Out_0_Vector4 = IN.uv3;
float3 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oPosition_0_Vector3;
float3 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oNormal_5_Vector3;
float3 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oTangent_6_Vector3;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F0_7_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F1_8_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F2_13_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F3_14_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F4_15_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F5_16_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F6_17_Vector4;
float4 _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F7_18_Vector4;
Vert_float(IN.InstanceID, IN.ObjectSpacePosition, IN.ObjectSpaceNormal, IN.ObjectSpaceTangent, IN.VertexColor, _UV_ccc703dc3bdd420e8a8a4aa0607d4ec4_Out_0_Vector4, _UV_46571950324b44e8bf8895f08abd191f_Out_0_Vector4, _UV_ba4ef72275334f08b9b01fedf9da0065_Out_0_Vector4, _UV_255ab3a515d14af686e70c40d04416e6_Out_0_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oPosition_0_Vector3, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oNormal_5_Vector3, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oTangent_6_Vector3, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F0_7_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F1_8_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F2_13_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F3_14_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F4_15_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F5_16_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F6_17_Vector4, _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F7_18_Vector4);
description.Position = _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oPosition_0_Vector3;
description.Normal = _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oNormal_5_Vector3;
description.Tangent = _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oTangent_6_Vector3;
description.extraV2F0 = _VertCustomFunction_c06714ff508d45fab2f28a5dcfca5564_oExtraV2F0_7_Vector4;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
output.extraV2F0 = input.extraV2F0;
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
float Alpha;
float AlphaClipThreshold;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
float _IsFrontFace_5b03f99e2e7841c3a7d2ae306590b576_Out_0_Boolean = max(0, IN.FaceSign.x);
float4 _UV_c1e39353e82e434da821b9bdc4338e4b_Out_0_Vector4 = IN.uv0;
float4 _UV_64d51b5974bc4ecf849e7307e6de2727_Out_0_Vector4 = IN.uv1;
float4x4 _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oExtra_23_Matrix4;
float3 _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oAlbedo_0_Vector3;
float _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oSmoothness_5_Float;
float3 _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oNormal_6_Vector3;
float3 _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oEmission_7_Vector3;
float _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oOcclusion_8_Float;
float _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oMetallic_9_Float;
float _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oAlpha_10_Float;
Frag_float(IN.WorldSpacePosition, IN.WorldSpaceNormal, IN.WorldSpaceTangent, _IsFrontFace_5b03f99e2e7841c3a7d2ae306590b576_Out_0_Boolean, float4 (0, 0, 0, 0), _UV_c1e39353e82e434da821b9bdc4338e4b_Out_0_Vector4, _UV_64d51b5974bc4ecf849e7307e6de2727_Out_0_Vector4, float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), IN.extraV2F0, float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), float4 (0, 0, 0, 0), _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oExtra_23_Matrix4, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oAlbedo_0_Vector3, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oSmoothness_5_Float, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oNormal_6_Vector3, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oEmission_7_Vector3, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oOcclusion_8_Float, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oMetallic_9_Float, _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oAlpha_10_Float);
surface.Alpha = _FragCustomFunction_a9f9336e3bc541cd8ae52be701d51fdc_oAlpha_10_Float;
surface.AlphaClipThreshold = float(0.5);
return surface;
}

// --------------------------------------------------
// Build Graph Inputs

VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;
    output.uv0 =                                        input.uv0;
    output.uv1 =                                        input.uv1;
    output.uv2 =                                        input.uv2;
    output.uv3 =                                        input.uv3;
    output.VertexColor =                                input.color;
#if UNITY_ANY_INSTANCING_ENABLED
    output.InstanceID =                                 unity_InstanceID;
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
    output.InstanceID =                                 input.instanceID;
#endif

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

    output.extraV2F0 = input.extraV2F0;

    // must use interpolated tangent, bitangent and normal before they are normalized in the pixel shader.
    float3 unnormalizedNormalWS = input.normalWS;
    const float renormFactor = 1.0 / length(unnormalizedNormalWS);


    output.WorldSpaceNormal = renormFactor * input.normalWS.xyz;      // we want a unit length Normal Vector node in shader graph

    // to preserve mikktspace compliance we use same scale renormFactor as was used on the normal.
    // This is explained in section 2.2 in "surface gradient based bump mapping framework"
    output.WorldSpaceTangent = renormFactor * input.tangentWS.xyz;

    output.WorldSpacePosition = input.positionWS;

    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


    output.uv0 = input.texCoord0;
    output.uv1 = input.texCoord1;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
    BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

void BuildAppDataFull(Attributes attributes, VertexDescription vertexDescription, inout appdata_full result)
{
    result.vertex     = float4(attributes.positionOS, 1);
    result.tangent    = attributes.tangentOS;
    result.normal     = attributes.normalOS;
    result.texcoord   = attributes.uv0;
    result.texcoord1  = attributes.uv1;
    result.texcoord2  = attributes.uv2;
    result.texcoord3  = attributes.uv3;
    result.color      = attributes.color;
    result.vertex     = float4(vertexDescription.Position, 1);
    result.normal     = vertexDescription.Normal;
    result.tangent    = float4(vertexDescription.Tangent, 0);
    #if UNITY_ANY_INSTANCING_ENABLED
    result.instanceID = attributes.instanceID;
    #endif
}

void VaryingsToSurfaceVertex(Varyings varyings, inout v2f_surf result)
{
    result.pos = varyings.positionCS;
    result.worldPos = varyings.positionWS;
    result.worldNormal = varyings.normalWS;
    // World Tangent isn't an available input on v2f_surf


    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
    #if UNITY_SHOULD_SAMPLE_SH
    #if !defined(LIGHTMAP_ON)
    #endif
    #endif
    #if defined(LIGHTMAP_ON)
    #endif
    #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        result.fogCoord = varyings.fogFactorAndVertexLight.x;
        COPY_TO_LIGHT_COORDS(result, varyings.fogFactorAndVertexLight.yzw);
    #endif

    DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(varyings, result);
}

void SurfaceVertexToVaryings(v2f_surf surfVertex, inout Varyings result)
{
    result.positionCS = surfVertex.pos;
    result.positionWS = surfVertex.worldPos;
    result.normalWS = surfVertex.worldNormal;
    // viewDirectionWS is never filled out in the legacy pass' function. Always use the value computed by SRP
    // World Tangent isn't an available input on v2f_surf

    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
    #if UNITY_SHOULD_SAMPLE_SH
    #if !defined(LIGHTMAP_ON)
    #endif
    #endif
    #if defined(LIGHTMAP_ON)
    #endif
    #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        result.fogFactorAndVertexLight.x = surfVertex.fogCoord;
        COPY_FROM_LIGHT_COORDS(result.fogFactorAndVertexLight.yzw, surfVertex);
    #endif

    DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(surfVertex, result);
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/DepthOnlyPass.hlsl"

ENDHLSL
}
}
CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
CustomEditorForRenderPipeline "UnityEditor.Rendering.BuiltIn.ShaderGraph.BuiltInLitGUI" ""
FallBack "Hidden/Shader Graph/FallbackError"
}