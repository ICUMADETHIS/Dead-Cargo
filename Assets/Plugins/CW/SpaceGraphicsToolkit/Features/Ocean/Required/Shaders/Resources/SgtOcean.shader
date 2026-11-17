Shader "Hidden/SgtOcean"
{
	Properties
	{
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100

		Pass
		{
			Cull Off
			ZWrite On

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing
			#pragma instancing_options procedural:SetupInstancing
			#pragma multi_compile_local _SGT_SHAPE_BOX _SGT_SHAPE_SPHERE
			#pragma multi_compile_local _SGT_DISPLACEMENT_OFF _SGT_DISPLACEMENT_ON
			#pragma multi_compile_local _SGT_MICRO_OFF _SGT_MICRO_ON
			#pragma multi_compile_local _SGT_MACRO_OFF _SGT_MACRO_ON
			#pragma multi_compile_local _SGT_SNAP_OFF _SGT_SNAP_ON
			#pragma multi_compile_local _SGT_WAVES_1 _SGT_WAVES_2 _SGT_WAVES_3
			#pragma multi_compile __ CW_COMPRESS_POSITIONS

			#if _SGT_WAVES_3
				#define WAVE_SKIP 1
			#elif _SGT_WAVES_2
				#define WAVE_SKIP 2
			#else
				#define WAVE_SKIP 3
			#endif

			#include "UnityCG.cginc"
			#include "../OceanShared.cginc"
			#include "../../../../../../Shared/SimpleOriginSystem/Scripts/PositionCompression.cginc"

			float3 _SGT_WCam;

			sampler2D _SGT_MicroTexture;
			float4x4  _SGT_MicroMatrixA;
			float4x4  _SGT_MicroMatrixB;
			float     _SGT_MicroBlend;
			float4    _SGT_MicroData;

			sampler2D _SGT_MacroTexture;
			float4x4  _SGT_MacroMatrixA;
			float4x4  _SGT_MacroMatrixB;
			float     _SGT_MacroBlend;
			float4    _SGT_MacroData;

			struct a2v
			{
				float4 vertex    : POSITION;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f
			{
				float4 position            : SV_POSITION;
				float3 objectSpacePosition : TEXCOORD0;
				float3 objectSpaceNormal   : TEXCOORD1;
				float3 objectSpaceTangent  : TEXCOORD2;
				float3 worldSpacePosition  : TEXCOORD3;
				float4 screenPos           : TEXCOORD4;
			};

			struct f2g
			{
				float4 distance : SV_Target0;
			};

			float SGT_Bayer4x4(float2 screenPos)
			{
				uint2 p = uint2(screenPos * _ScreenParams.xy) % 4;
				float bayer[16] = { 0,  8,  2, 10, 12,  4, 14,  6, 3, 11,  1,  9, 15,  7, 13,  5 };
				return (bayer[p.x + p.y * 4] + 0.5) / 16.0;
			}

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

			float2 SGT_GetMicroNormal(float3 wp)
			{
				float2 positionA   = mul(_SGT_MicroMatrixA, float4(wp, 1.0f)).xy;
				float2 positionB   = mul(_SGT_MicroMatrixB, float4(wp, 1.0f)).xy;
				float4 dualNormalA = tex2D(_SGT_MicroTexture, positionA) * 2.0 - 1.0; // xy = normal A, zw = normal B
				float4 dualNormalB = tex2D(_SGT_MicroTexture, positionB) * 2.0 - 1.0; // xy = normal A, zw = normal B
				float  worldNoise  = tex3Dlod(_SGT_NoiseTex, float4(wp.xy * 1, 0, 0)).x * 2.0;
				float  pingPong    = sin((worldNoise + _SGT_MicroData.y) * 3.1415) * 0.5 + 0.5;
				float2 normalA     = lerp(dualNormalA.xy, dualNormalA.zw, pingPong);
				float2 normalB     = lerp(dualNormalB.xy, dualNormalB.zw, pingPong);
	
				return lerp(normalA, normalB, _SGT_MicroBlend) * _SGT_MicroData.x * _SGT_MicroData.x;
			}

			float2 SGT_GetMacroNormal(float3 wp, float pixelSize)
			{
				float2 positionA   = mul(_SGT_MacroMatrixA, float4(wp, 1.0f)).xy;
				float2 positionB   = mul(_SGT_MacroMatrixB, float4(wp, 1.0f)).xy;
				float4 dualNormalA = tex2D(_SGT_MacroTexture, positionA) * 2.0 - 1.0;
				float4 dualNormalB = tex2D(_SGT_MacroTexture, positionB) * 2.0 - 1.0;
				float  worldNoise  = tex3Dlod(_SGT_NoiseTex, float4(wp.xy * 1, 0, 0)).x * 2.0;
				float  pingPong    = sin((worldNoise + _SGT_MacroData.y) * 3.1415) * 0.5 + 0.5;
				float2 normalA     = lerp(dualNormalA.xy, dualNormalA.zw, pingPong);
				float2 normalB     = lerp(dualNormalB.xy, dualNormalB.zw, pingPong);

				return lerp(normalA, normalB, _SGT_MacroBlend) * smoothstep(0.005, 0.05, pixelSize) * _SGT_MacroData.x * _SGT_MacroData.x;
			}

			void vert(a2v v, out v2f o)
			{
				UNITY_SETUP_INSTANCE_ID(v);

				float  weight     = 1.0f;
				float3 weights    = v.vertex.xyz;
				float  batchIndex = 0;

				#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
					batchIndex = unity_InstanceID;
				#endif

				float3 origin = _SGT_Origins[batchIndex].xyz;
				float  depth  = _SGT_Origins[batchIndex].w;

				float4 position = _SGT_PositionsA[batchIndex] * weights.x + _SGT_PositionsB[batchIndex] * weights.y + _SGT_PositionsC[batchIndex] * weights.z;
				float3 normalPos = position.xyz + origin;

				position.xyz += _SGT_Offset + origin;

				float3 direction = normalize(normalPos.xyz);
				
				#if _SGT_SHAPE_SPHERE && _SGT_SNAP_ON
					position.xyz = direction * _SGT_Radius + _SGT_Offset;
				#endif

				v.vertex.xyz = position.xyz;
				float3 normal   = float3(0.0, 1.0,  0.0);
				float3 tangent  = float3(1.0, 0.0,  0.0);
				float3 binormal = float3(0.0, 0.0, -1.0);

				#if _SGT_SHAPE_SPHERE
					normal = direction;
					SGT_ComputeTangentFrame(normal, tangent, binormal);
				#endif
				
				o.objectSpacePosition = v.vertex;
				o.objectSpaceNormal   = normal;
				o.objectSpaceTangent  = tangent;

				SGT_ApplyWaves(v.vertex.xyz, normal, tangent, binormal, 0.0, 0.01);
				
				o.worldSpacePosition = mul(_SGT_ObjectToWorld, v.vertex).xyz;
				
				#if CW_COMPRESS_POSITIONS
					float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
					worldPos.xyz = SSS_CompressWorld(worldPos.xyz);
					v.vertex = mul(unity_WorldToObject, worldPos);
				#endif

				o.position  = UnityObjectToClipPos(v.vertex);
				o.screenPos = ComputeScreenPos(o.position);
			}

			void frag(v2f i, out f2g o, in bool isFrontFace : SV_IsFrontFace)
			{
				float3 worldSpacePositionD = mul(_SGT_ObjectToWorld, float4(i.objectSpacePosition, 1.0)).xyz;
				float  pixelSize           = length(fwidth(i.objectSpacePosition));
				float3 objectSpaceBinormal = cross(i.objectSpaceNormal, i.objectSpaceTangent);
				float2 detail              = 0.0;
				
				#if _SGT_MICRO_ON
					detail += SGT_GetMicroNormal(worldSpacePositionD);
				#endif

				#if _SGT_MACRO_ON
					detail += SGT_GetMacroNormal(worldSpacePositionD, pixelSize);
				#endif

				// The detail vector is injected here.
				// Even when the primary waves fade, this macro-detail persists.
				SGT_ApplyWaves(i.objectSpacePosition, i.objectSpaceNormal, i.objectSpaceTangent, objectSpaceBinormal, detail, pixelSize);
				
				float2 screenUV            = (i.screenPos.xy / i.screenPos.w);
				float3 worldSpacePosition  = mul(_SGT_ObjectToWorld, float4(i.objectSpacePosition, 1.0)).xyz;
				float3 worldSpaceNormal    = normalize(mul((float3x3)_SGT_ObjectToWorld, i.objectSpaceNormal));
				float  dist                = distance(_SGT_WCam, worldSpacePosition);
				
				// Top surface
				if (isFrontFace == true)
				{
					o.distance = float4(worldSpaceNormal, dist);
				}
				// Under surface
				else
				{
					o.distance = float4(worldSpaceNormal, -dist);
				}

				o.distance *= step(_SGT_FadeOpacity.y, SGT_Bayer4x4(screenUV));
			}
			ENDCG
		} // Pass
	}
}