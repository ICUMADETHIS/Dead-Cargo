Shader "SgtCloud"
{
	Properties
	{
		[HideInInspector] _SGT_Detail("_MinStep", Vector) = (0,0,0,0)
		[HideInInspector] _SGT_Density("Density", Range(0, 100000)) = 1

		[HideInInspector] _SGT_Size("", Vector) = (0,0,0,0)
		[HideInInspector] _SGT_CullAltitude("", Float) = 0
		[HideInInspector] _SGT_Color("", Color) = (0,0,0,0)
		[HideInInspector] _SGT_LightDensity("", Float) = 0

		[Header(ERODE)]
		[KeywordEnum(Off, Layered, Stochastic, FastStochastic)] _SGT_Erode ("	", Float) = 0
		[NoScaleOffset] _SGT_ErodeTex("	Tex", 3D) = "" {}
		[HideInInspector] [NoScaleOffset] _SGT_ErodeNoiseTex("", 3D) = "" {}
		[HideInInspector] _SGT_ErodeData("", Vector) = (0,0,0,0)

		[Header(CARVE)]
		[Toggle(_SGT_CARVE)] _SGT_Carve("	Enable", Float) = 0
		
		[Header(ALBEDO)]
		[Toggle(_SGT_ALBEDO)] _SGT_Albedo("	Enable", Float) = 0
		
		[Header(SHADOW)]
		[Toggle(_SGT_SHADOW)] _SGT_Shadow("	Enable", Float) = 0

		[Header(SKY)]
		[Toggle(_SGT_SKY)] _SGT_Sky("	Enable", Float) = 0
	
		[Header(LIGHTING)]
		[Toggle(_SGT_LIGHTING)] _SGT_Lighting("	Enable", Float) = 0
	}
	SubShader
	{
		HLSLINCLUDE
			#pragma shader_feature_local _SGT_ERODE_OFF _SGT_ERODE_LAYERED _SGT_ERODE_STOCHASTIC _SGT_ERODE_FASTSTOCHASTIC
			#pragma shader_feature_local _SGT_CARVE
			#pragma shader_feature_local _SGT_ALBEDO
			#pragma shader_feature_local _SGT_SHADOW
			#pragma shader_feature_local _SGT_SKY
			#pragma shader_feature_local _SGT_LIGHTING
			#pragma multi_compile __ CW_COMPRESS_POSITIONS

			SSS_INCLUDE

			#include "SgtCloud.cginc"
			#include "../../../Volumetrics/Required/Shaders/SgtErode.cginc"

			sampler2D _SGT_CoverageTex;
			float2    _SGT_CoverageTex_Size;

			float4 _SGT_Jitter; // x, y
			float4 _SGT_Checkerboard; // x, y, cb

			float4 _SGT_Size;
			float2 _SGT_CullStrength;
			float4 _SGT_Color;
			float4 _SGT_Color2;
			float3 _SGT_WindOffset;
			float3 _SGT_ScatteringColor;
			float4 _SGT_CloudAmbientTint;

			float    _SGT_Density; // Auto
			float4   _SGT_Detail;
			float    _SGT_Opacity;

			float4x4 _SGT_WorldToCloud;
			float4x4 _SGT_CloudToWorld;

			float4 _SGT_LayerBiasK;
			float4 _SGT_LayerRise0;
			float4 _SGT_LayerRise1;
			float4 _SGT_LayerFall0;
			float4 _SGT_LayerFall1;
			float4 _SGT_LayerDensities;
			float4 _SGT_LayerBounds0;
			float4 _SGT_LayerBounds1;
			float4 _SGT_LayerBrightnesses0;
			float4 _SGT_LayerBrightnesses1;
			float4 _SGT_LayerSamples0;
			float4 _SGT_LayerSamples1;
			float4x4 _SGT_LayerSampleChannels;
			float4x4 _SGT_LayerErodeChannels;
			float4x4 _SGT_LayerCarveChannels;
			float4   _SGT_LongShadowParams;

			float4 _SGT_RenderSize;
			float4 _SGT_FullSize;

			float4 _SGT_ShadowData;

			float4 _SGT_LightningPositions[16]; 
			float4 _SGT_LightningColors[16];
			float4 _SGT_LightningMask;
			float4 _SGT_LightningData;

			struct a2v
			{
				float4 vertex    : POSITION;
				float3 normal    : NORMAL;
				float2 texcoord0 : TEXCOORD0;
			};

			struct v2f
			{
				float4 pos          : SV_POSITION;
				float2 uv           : TEXCOORD0;
				float3 worldPos     : TEXCOORD1;
				float  ratio        : TEXCOORD2;
				float3 viewPosition : TEXCOORD3;
			};

			struct CloudSample
			{
				float3 color;
				float4 weights;
				float  density;
			};

			float4 SGT_CloudHeightDensity(float h, float off)
			{
				float4 h4 = saturate(h);

				// Apply Schlick Bias Warp per layer
				h4 = h4 / (_SGT_LayerBiasK * (1.0 - h4) + 1.0);

				// Smoothstep rise and fall edges
				float4 rise = smoothstep(_SGT_LayerRise0, _SGT_LayerRise1, h4);
				float4 fall = smoothstep(_SGT_LayerFall0, _SGT_LayerFall1, h4) + off;

				// Return the density for each layer in RGBA format
				return rise * fall * _SGT_LayerDensities;
			}
			
			float SGT_GetDensity(float2 uv, float lod)
			{
				float4 coverage = tex2Dlod(_SGT_CoverageTex, float4(uv, 0, lod));
				float  den      = 0.0f;
				
				#if _SGT_ALBEDO
					den = coverage.w;
				#else
					den = dot(coverage.xyz, 1.0);
				#endif

				return den;
			}

			CloudSample SGT_SampleCloud(float3 p, float off)
			{
				float2 uv        = SGT_DirectionToEquirectangular(p);
				float4 coverage  = tex2Dlod(_SGT_CoverageTex, float4(uv, 0.0, 0.0));
				float  height01  = saturate((length(p) - _SGT_Size.w) * _SGT_Size.y);

				CloudSample o;

				#if _SGT_ALBEDO
					o.color   = _SGT_Color.xyz * coverage.xyz;
					o.density = height01 * coverage.w;
					o.weights = float4(1.0, 0.0, 0.0, 0.0);
				#else
					float4 heightDensities   = SGT_CloudHeightDensity(height01, off);
					float4 pickedCoverage    = mul(_SGT_LayerSampleChannels, coverage);
					float4 remappedCoverage  = saturate((pickedCoverage - _SGT_LayerSamples0) * _SGT_LayerSamples1);
					float4 layerDensities    = remappedCoverage * heightDensities;
					float  totalDensity      = saturate(layerDensities.x + layerDensities.y + layerDensities.z + layerDensities.w);
					float4 layerWeights      = layerDensities / max(0.0001, totalDensity);
					float4 layerT            = 1.0 - saturate((height01 - _SGT_LayerBounds0) / max(0.0001, _SGT_LayerBounds1 - _SGT_LayerBounds0));
					float4 layerBrightnesses = lerp(_SGT_LayerBrightnesses0, _SGT_LayerBrightnesses1, 1.0 - layerT * layerT);
					float  finalBrightness   = dot(layerBrightnesses, layerWeights);
					float  totalCoverage     = saturate(dot(coverage, 1.0));

					o.color   = lerp(_SGT_Color.xyz, _SGT_Color2.xyz, totalCoverage) * finalBrightness;
					o.weights = layerWeights;
					o.density = totalDensity;
				#endif

				return o;
			}

			void Vert(a2v v, out v2f o)
			{
				o.uv           = v.texcoord0;
				o.worldPos     = mul(unity_ObjectToWorld, v.vertex).xyz;
				o.viewPosition = mul(UNITY_MATRIX_V, float4(o.worldPos, 1.0f));

				#if CW_COMPRESS_POSITIONS
					float4 worldPos    = mul(unity_ObjectToWorld, v.vertex);
					float3 worldCenter = unity_ObjectToWorld._m03_m13_m23;
					worldPos.xyz = SSS_CompressWorld(worldPos.xyz, worldCenter, _SGT_Size.z, o.ratio);
					v.vertex = mul(unity_WorldToObject, worldPos);
				#else
					o.ratio = 1.0;
				#endif

				float4 clipPos = UnityObjectToClipPos(v.vertex);
				
				clipPos.xy += _SGT_Jitter.xy * clipPos.w;
				
				o.pos = clipPos;
			}

			void SGT_MarchSegment(float3 rayPos, float3 rayDir, float3 rayDst, inout float4 c, inout float2 d, float dither, float3 lightStep, float cullStrength, inout float stepSize)
			{
				float3 eyePos = rayPos;

				float3 lightDir = normalize(lightStep); 
				float  mu       = dot(rayDir, lightDir);
				float  mv       = 1.0 - abs(dot(normalize(rayPos), lightDir));

				float3 mid    = rayPos + rayDir * (rayDst.x + rayDst.z) * 0.5f;
				float  weight = pow(saturate(length(mid) / _SGT_Size.w), cullStrength);
				
				float off = rayDst.x;
				rayPos += rayDir * rayDst.x;
				rayDst.xy -= off;
				float t = 0;

				[loop]
				for (int s = 0; s < 250 && t < rayDst.y && c.w > __SGT_CUTOFF; s++)
				{
					float  len = min(stepSize, rayDst.y - t);
					float3 pos = rayPos + rayDir * (t + stepSize * dither);

					CloudSample cloud = SGT_SampleCloud(pos, 0.0);
					
					cloud.density *= weight;
		
					float3 noisePos = (pos + _SGT_WindOffset) * _SGT_ErodeTiling;
			
					SGT_Erode(eyePos, pos, noisePos, cloud.density, mul(_SGT_LayerErodeChannels, cloud.weights), 0);
					SGT_Carve(eyePos, pos, noisePos, cloud.density, mul(_SGT_LayerCarveChannels, cloud.weights), 0);
		
					#if _SGT_SHADOW
						float3 hitDistances = SGT_SphereTest(pos, lightDir, _SGT_LongShadowParams.x);
						float t2 = max(0.0, -hitDistances.y);
						float3 lightStep2 = lightDir * hitDistances.y;

						float3 shadowPos = (pos + lightStep2 + _SGT_WindOffset) * _SGT_ErodeTiling;
						float  shadowStr = saturate((_SGT_LongShadowParams.x - length(pos)) * 10000 * _SGT_LongShadowParams.y);
		
						CloudSample shadow = SGT_SampleCloud(pos + lightStep2, _SGT_ShadowData.x);
		
						//SGT_Erode(eyePos, pos, shadowPos, shadow.density, mul(_SGT_LayerErodeChannels, shadow.weights * _SGT_ShadowData.z), _SGT_ShadowData.y);
		
						cloud.color *= exp(-shadow.density * shadowStr);
					#endif

					float  beer        = exp(-cloud.density * len * _SGT_Density);
					float  beersLaw    = 1.0 - beer;
					float  powderTerm  = beersLaw * 2.0 * (1.0 - beer * beer);
					float  blendFactor = 0.5 + 0.5 * mu;
					float3 luminance   = cloud.color * (lerp(beersLaw, powderTerm, blendFactor * _SGT_ShadowData.w));

					d     += float2(off + t, 1.0) * (1.0 - beer) * c.w;
					c.rgb += luminance * c.w;
					c.w   *= beer;

					t += stepSize; stepSize *= _SGT_Detail.y;
				}
			}

			void SGT_March(float3 rayPos, float3 rayDir, float rayMax, out float4 outColor, out float outDepth, float dither, float stepSize, float cullStrength)
			{
				outColor = 0;
				outDepth = 0;

				// March
				float3 lightDir = normalize(mul(_SGT_WorldToCloud, float4(_SGT_LightDirection[0].xyz, 0.0f)).xyz);
				float  costh    = dot(rayDir, lightDir);
				float4 c        = float4(0.0f, 0.0f, 0.0f, 1.0f);
				float2 d        = 0.0f;

				// March one or two segments
				float3 outerHit = SGT_SphereTest(rayPos, rayDir, _SGT_Size.z);
				float3 innerHit = SGT_SphereTest(rayPos, rayDir, _SGT_Size.w);
				
				outerHit.xy = clamp(outerHit.xy, 0.0f, rayMax);
				innerHit.xy = clamp(innerHit.xy, 0.0f, rayMax);
				
				if (outerHit.z > 0.0f)
				{
					float3 lightStep = lightDir * _SGT_ShadowData.x;
					
					if (innerHit.z < 0.0f) // One segment
					{
						float3 segment1 = float3(outerHit.x, outerHit.y, outerHit.x);
						
						SGT_MarchSegment(rayPos, rayDir, segment1, c, d.xy, dither, lightStep, cullStrength, stepSize);
					}
					else // Two segments
					{
						float3 segment1 = float3(outerHit.x, min(innerHit.x, outerHit.y), outerHit.x);
						float3 segment2 = float3(max(innerHit.y, outerHit.x), outerHit.y, outerHit.x);
						float3 segmentM = float3(segment1.y, segment2.x, outerHit.x);

						SGT_MarchSegment(rayPos, rayDir, segment1, c, d, dither, lightStep, cullStrength, stepSize);
						SGT_MarchSegment(rayPos, rayDir, segment2, c, d, dither, lightStep, cullStrength, stepSize);
					}
				}

				if (c.w >= 1.0 - __SGT_CUTOFF)
				{
					discard;
				}

				float3 softPos  = rayPos + rayDir * (d.x / max(0.00001f, d.y));
				float4 softWPos = mul(_SGT_CloudToWorld, float4(softPos, 1.0f));

				outColor.rgb = c.rgb;
				outColor.a   = 1.0 - c.w;
				outDepth     = distance(softWPos, _WorldSpaceCameraPos);
	
				outColor = saturate((outColor - __SGT_CUTOFF) / ((1.0 - __SGT_CUTOFF) - __SGT_CUTOFF));
				//outColor = saturate(outColor - __SGT_CUTOFF);
			}
		ENDHLSL

		Pass // Main 0
		{
			Blend One Zero, One Zero
			Cull Front
			ZWrite Off

			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag

			struct f2a
			{
				float4 color : SV_TARGET0;
				float4 depth : SV_TARGET1;
			};

			float SGT_Scattering(float cosTheta, float density)
			{
				float gBase = lerp(0.88, 0.76, density);
				float4 g = float4(
					-0.12,
					gBase * 0.45,
					gBase * 0.82,
					gBase
				);

				// Heavier weight on the sharpest lobe
				float4 wMin = float4(0.01, 0.05, 0.18, 0.76);
				float4 wMax = float4(0.04, 0.10, 0.22, 0.64);
				float4 w    = lerp(wMin, wMax, density);

				w *= rcp(dot(w, 1.0));
	
				float4 k     = 1.55 * g - 0.55 * g * g * g;
				float4 denom = max(1e-3, 1.0 + k * cosTheta);
				float4 phase = pow((1.0 - k * k) / (denom * denom), 1.15);

				return dot(phase, w) * density;
			}

			void SGT_ContributeLightning(float3 cloudPos, inout float4 color)
			{
				int   lightningCount  = (int)_SGT_LightningData.x;
				float lightningTiling = _SGT_LightningData.y;
				float lightningDetail = _SGT_LightningData.z;
				float3 lightningTotal = 0.0;
	
				for (int i = 0; i < lightningCount; i++)
				{
					// 1. Get the raw and normalized distance
					float dist = distance(cloudPos, _SGT_LightningPositions[i].xyz);
					float distNorm = saturate(dist * _SGT_LightningPositions[i].w);
		
					// 2. Beer-Lambert exponential scattering
					// (Increase the 5.0 to make the core tighter and clouds feel thicker)
					float scatter = exp(-distNorm * 5.0);
		
					// 3. Smooth windowing to ensure it strictly hits 0.0 at the boundary
					float window = saturate(1.0 - (distNorm * distNorm));
		
					float finalFlash = scatter * (window * window);
		
					lightningTotal += _SGT_LightningColors[i].rgb * finalFlash;
				}
	
				float detail = dot(tex3Dlod(_SGT_ErodeTex, float4(cloudPos * lightningTiling, 0.0)), _SGT_LightningMask);
	
				#if _SSS_HDRP && !_SSS_NO_DERIVATIVES
					lightningTotal *= 25000.0;
				#endif
	
				color.xyz += lightningTotal * color.w * lerp(1.0, detail, lightningDetail);
			}

			void Frag(v2f i, out f2a o)
			{
				float3 worldPos    = _WorldSpaceCameraPos;
				float3 worldDir    = normalize(i.worldPos - _WorldSpaceCameraPos);
				float  worldMax    = distance(worldPos, i.worldPos);
				float  linearScale = length(i.viewPosition.xyz / i.viewPosition.z);

				float2 pixelPos   = floor(i.pos.xy);
				float2 pixelPos2  = pixelPos * _SGT_Checkerboard.z + _SGT_Checkerboard.xy + 0.5;
				float2 screenUV   = pixelPos2 * _SGT_FullSize.zw;

				if (unity_OrthoParams.w == 1.0)
				{
					SGT_Ortho(i.worldPos, worldPos, worldDir);
				}

				// Depth clip
				float worldEyeDepth = 0.0;

				for (int y = 0; y < (int)_SGT_Checkerboard.z; y++)
				for (int x = 0; x < (int)_SGT_Checkerboard.z; x++)
				{
					float2 subUV = (pixelPos2 + float2(x, y)) * _SGT_FullSize.zw;

					worldEyeDepth = max(worldEyeDepth, SGT_GetLinearEyeDepth(subUV));
				}

				worldEyeDepth *= linearScale;
	
				worldEyeDepth += (worldEyeDepth < _SGT_Volumetrics_NearClip) * worldMax;

				#if CW_COMPRESS_POSITIONS
					worldEyeDepth = SSS_DecompressWorldDist(worldEyeDepth);
					worldEyeDepth = SSS_ElongateDistance(worldEyeDepth);
				#endif

				worldMax = min(worldMax, worldEyeDepth);
				
				float3 rayPos = mul(_SGT_WorldToCloud, float4(worldPos, 1.0)).xyz;
				float3 rayDst = mul(_SGT_WorldToCloud, float4(worldPos + worldDir * worldMax, 1.0)).xyz;
				float3 rayDir = normalize(rayDst - rayPos);
				float  rayMax = distance(rayPos, rayDst);

				float4 color = 0.0;
				float  depth = 0.0;

				float dither = SGT_DitherBlue(screenUV);

				float eyeAlt01     = saturate((length(rayPos) - _SGT_Size.w) * _SGT_Size.y);
				float cullStrength = lerp(_SGT_CullStrength.x, _SGT_CullStrength.y, saturate(eyeAlt01));

				SGT_March(rayPos, rayDir, rayMax, color, depth, dither, _SGT_Detail.x, cullStrength);

				#if _SGT_LIGHTING
					float3 sunDir     = normalize(mul(_SGT_WorldToSky, float4(_SGT_LightDirection[0].xyz, 0.0)).xyz);
					float4 lighting   = float4(0.0, 0.0, 0.0, 1.0);
					float3 scattering = 0.0;

					for (int j = 0; j < _SGT_LightCount; j++)
					{
						float cosTheta = dot(-worldDir, _SGT_LightDirection[j].xyz);
		
						lighting.xyz += _SGT_LightColor[j].xyz;

						scattering += _SGT_LightColor[j].xyz * SGT_Scattering(cosTheta, color.w);
					}
	
					color.xyz *= lighting.xyz;
	
					#if _SGT_SKY && _SSS_HDRP
						color.xyz *= 0.25;
					#endif

					color.xyz += scattering * color.w * (1.0 - color.w) * _SGT_ScatteringColor;
				#else
					float3 sunDir   = normalize(rayPos);
					float4 lighting = float4(1.0, 1.0, 1.0, 1.0);
				#endif
	
				float3 worldHit = worldPos + worldDir * depth;
				float3 skyHit   = mul(_SGT_WorldToSky, float4(worldHit, 1.0)).xyz;
				float3 cloudHit = mul(_SGT_WorldToCloud, float4(worldHit, 1.0)).xyz;
	
				#if _SSS_HDRP
					float3 ambientLighting = _SGT_AmbientColor.xyz * _SGT_CloudAmbientTint.xyz * 0.25;
				#else
					float3 ambientLighting = _SGT_AmbientColor.xyz * _SGT_CloudAmbientTint.xyz;
				#endif
	
				#if _SGT_SKY
					float4 skyColor = SGT_SimpleAtmosphere(worldPos, worldHit, 1.0, 1.0);

					color.xyz *= SGT_AtmosphereColor(skyHit, sunDir, dither) + ambientLighting;
					color.xyz = lerp(color.xyz, skyColor.xyz * lighting.xyz * color.w, skyColor.w);
				#endif
	
				SGT_ContributeLightning(cloudHit, color);

				o.color = color;
				o.depth = float4(depth, 0.0, 0.0, 1.0);
			}
			ENDHLSL
		} // Main 0
	}
}
