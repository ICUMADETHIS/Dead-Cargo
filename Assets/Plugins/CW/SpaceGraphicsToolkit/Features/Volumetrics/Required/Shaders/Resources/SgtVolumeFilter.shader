Shader "Hidden/SgtVolumeFilter"
{
	SubShader
	{
		HLSLINCLUDE
			#include "UnityCG.cginc"

			float4 _SGT_Volumetrics_ColorSize;
			float4 _SGT_Volumetrics_NewSize;

			Texture2D _SGT_Volumetrics_DepthTex;
			Texture2D _SGT_Volumetrics_ColorTex;

			Texture2D _SGT_Volumetrics_DepthTex_New;
			Texture2D _SGT_Volumetrics_ColorTex_New;

			Texture2D _SGT_Volumetrics_DepthTex_Old;
			Texture2D _SGT_Volumetrics_ColorTex_Old;

			Texture2D _SGT_SceneDepthTexture;

			SamplerState sampler_point_clamp;
			SamplerState sampler_linear_clamp;

			// DITHER
			sampler2D _SGT_BlueNoiseTex; // Global
			float     _SGT_Frame; // Global

			float3 _SGT_WindVelocity;
	
			float SGT_DitherBlue(float2 screenUV)
			{
				float2 pixel = floor(screenUV * _ScreenParams.xy);
				float  noise = tex2D(_SGT_BlueNoiseTex, pixel / 64.0f).r;
				return frac(noise + _SGT_Frame / sqrt(0.5f));
			}

			struct Attributes
			{
				uint vertexID : SV_VertexID;
				float2 texcoord0 : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				float2 texcoord0 : TEXCOORD0;
				UNITY_VERTEX_OUTPUT_STEREO
			};

			struct MRT1
			{
				float4 color : SV_Target0;
			};

			struct MRT2
			{
				float4 color : SV_Target0;
				float  depth : SV_Target1;
			};

			Varyings Vert(Attributes input)
			{
				Varyings output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
				output.positionCS = float4(input.texcoord0.xy * 2.0f - 1.0f, 0.5f, 1.0f);
				output.texcoord0  = input.texcoord0;

				#if UNITY_UV_STARTS_AT_TOP
					output.positionCS.y = -output.positionCS.y;
				#endif
				return output;
			}
		ENDHLSL

		Pass
		{
			Name "TAA"
			ZWrite Off
			ZTest Always
			Blend Off
			Cull Off

			HLSLPROGRAM
				#pragma vertex   Vert
				#pragma fragment Frag

				float4x4 _SGT_InvViewProj;
				float4x4 _SGT_PrevViewProj;
				float    _SGT_TAA;
				float4   _SGT_Checkerboard; // Global

				float2 GetReprojectedUV(float2 uv, float depth)
				{
					float4 clip = float4(uv * 2.0f - 1.0f, 1.0f, 1.0f);
					float4 wp   = mul(_SGT_InvViewProj, clip);
					float3 dir  = normalize(wp.xyz / wp.w - _WorldSpaceCameraPos);
					float3 wpos = _WorldSpaceCameraPos + dir * depth;

					wpos += _SGT_WindVelocity;

					float4 pc = mul(_SGT_PrevViewProj, float4(wpos, 1.0f));
					return (pc.xy / pc.w) * 0.5f + 0.5f;
				}

				// Returns true if the UV is within the visible screen bounds
				bool IsValidHistory(float2 uv)
				{
					return all(uv >= 0.0f && uv <= 1.0f);
				}

				float4 RGBToYCoCg(float4 c)
				{
					float Y  = dot(c, float3( 0.25, 0.50, 0.25));
					float Co = dot(c, float3( 0.50, 0.00,-0.50));
					float Cg = dot(c, float3(-0.25, 0.50,-0.25));
					return float4(Y, Co, Cg, c.a);
				}

				float4 YCoCgToRGB(float4 c)
				{
					float r = c.x + c.y - c.z;
					float g = c.x + c.z;
					float b = c.x - c.y - c.z;
					return float4(r, g, b, c.a);
				}

				float4 ClampNeighborhoodYCoCg(float4 center, float2 uv)
				{
					float4 minC =  1e9;
					float4 maxC = -1e9;

					[unroll]
					for (int y = -1; y <= 1; y++)
					{
						[unroll]
						for (int x = -1; x <= 1; x++)
						{
							float2 o = float2(x, y) * _SGT_Volumetrics_NewSize.zw;
							float4 s = _SGT_Volumetrics_ColorTex_New.SampleLevel(sampler_point_clamp, uv + o, 0);

							//if (s.w > 0.1)
							{
								float4 ycocg = RGBToYCoCg(s);

								minC = min(minC, ycocg);
								maxC = max(maxC, ycocg);
							}
						}
					}

					float4 c = clamp(RGBToYCoCg(center), minC, maxC);
					return YCoCgToRGB(c);
				}

				float4 ClipVarianceYCoCg(float4 historyColor, float2 uv)
				{
					float4 m1 = 0.0;
					float4 m2 = 0.0;
					float sampleCount = 0.0;

					[unroll]
					for (int y = -1; y <= 1; y++)
					{
						[unroll]
						for (int x = -1; x <= 1; x++)
						{
							float2 o = float2(x, y) * _SGT_Volumetrics_NewSize.zw;
							float4 s = _SGT_Volumetrics_ColorTex_New.SampleLevel(sampler_point_clamp, uv + o, 0);
            
							// Only include valid cloud pixels in our statistics
							//if (s.a > 0.0) 
							{
								float4 ycocg = RGBToYCoCg(s);
								m1 += ycocg;
								m2 += ycocg * ycocg;
								sampleCount += 1.0;
							}
						}
					}

					if (sampleCount < 1.0) return historyColor; // Fallback if neighborhood is entirely empty sky

					float4 mu = m1 / sampleCount;
					float4 sigma = sqrt(abs(m2 / sampleCount - mu * mu));
    
					// Gamma dictates the size of the clipping box. 1.0 to 1.5 is standard.
					float gamma = 1.5; 
					float4 minC = mu - gamma * sigma;
					float4 maxC = mu + gamma * sigma;

					float4 historyYCoCg = RGBToYCoCg(historyColor);
					float4 clippedYCoCg = clamp(historyYCoCg, minC, maxC);
    
					return YCoCgToRGB(clippedYCoCg);
				}

				float4 ClampNeighborhoodDepth(float center, float2 uv)
				{
					float minC =  1e9;
					float maxC = -1e9;

					[unroll]
					for (int y = -1; y <= 1; y++)
					{
						[unroll]
						for (int x = -1; x <= 1; x++)
						{
							float2 o = float2(x, y) * _SGT_Volumetrics_NewSize.zw;
							float  d = _SGT_Volumetrics_DepthTex_New.SampleLevel(sampler_point_clamp, uv + o, 0).x;

							minC = min(minC, d);
							maxC = max(maxC, d);
						}
					}

					return clamp(center, minC, maxC);
				}

				float4 ClampNeighborhoodYCoCg2(float4 centerColor, float2 uv)
				{
					float4 minC =  1e9;
					float4 maxC = -1e9;

					[unroll]
					for (int y = -1; y <= 1; y++)
					{
						[unroll]
						for (int x = -1; x <= 1; x++)
						{
							float2 o = float2(x, y) * _SGT_Volumetrics_NewSize.zw;
							float4 s = _SGT_Volumetrics_ColorTex_New.SampleLevel(sampler_point_clamp, uv + o, 0);
							float  d = _SGT_Volumetrics_DepthTex_New.SampleLevel(sampler_point_clamp, uv + o, 0).x;

							if (s.w > 0.01)
							{
								float4 ycocg = RGBToYCoCg(s);

								minC = min(minC, ycocg);
								maxC = max(maxC, ycocg);
							}
						}
					}

					float4 c = clamp(RGBToYCoCg(centerColor), minC, maxC);
					return YCoCgToRGB(c);
				}

				MRT2 Frag(Varyings varyings)
				{
					UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(varyings);
					MRT2 m;

					float2 pixelPos   = floor(varyings.positionCS.xy);
					float2 uvFull     = (pixelPos + 0.5) * _SGT_Volumetrics_ColorSize.zw;
					float2 lowPixel   = floor(pixelPos / _SGT_Checkerboard.z);
					float2 lowUV      = (lowPixel + 0.5f) * _SGT_Volumetrics_NewSize.zw;
					float2 smoothUV   = (pixelPos + 0.5 - _SGT_Checkerboard.xy) * _SGT_Volumetrics_ColorSize.zw;
					float4 newColor   = _SGT_Volumetrics_ColorTex_New.SampleLevel(sampler_point_clamp, lowUV, 0);
					float  newDepth   = _SGT_Volumetrics_DepthTex_New.SampleLevel(sampler_point_clamp, lowUV, 0).x; // World space
					float  sceneDepth = _SGT_SceneDepthTexture.SampleLevel(sampler_point_clamp, uvFull, 0).x; // World space

					float2 prevUV   = GetReprojectedUV(uvFull, newDepth);
					bool   oldValid = IsValidHistory(prevUV);

					if (_SGT_Checkerboard.z > 1)
					{
						if ((uint)pixelPos.x % (uint)_SGT_Checkerboard.z == (uint)_SGT_Checkerboard.x &&
							(uint)pixelPos.y % (uint)_SGT_Checkerboard.z == (uint)_SGT_Checkerboard.y) // This pixel was ray marched this frame, so write it directly
						{
							float4 oldColor = _SGT_Volumetrics_ColorTex_Old.SampleLevel(sampler_linear_clamp, prevUV, 0);
							float  oldDepth = _SGT_Volumetrics_DepthTex_Old.SampleLevel(sampler_linear_clamp, prevUV, 0).x;

							if (oldValid)
							{
								oldColor = ClipVarianceYCoCg(oldColor, lowUV);
								oldDepth = ClampNeighborhoodDepth(oldDepth, lowUV);

								float blend = _SGT_TAA * (newColor.w > 0);

								m.color = lerp(newColor, oldColor, blend);
								m.depth = lerp(newDepth, oldDepth, 0);
							}
							else
							{
								m.color = newColor;
								m.depth = newDepth;
							}
						}
						else
						{
							float4 oldColor = _SGT_Volumetrics_ColorTex_Old.SampleLevel(sampler_linear_clamp, prevUV, 0);
							float  oldDepth = _SGT_Volumetrics_DepthTex_Old.SampleLevel(sampler_linear_clamp, prevUV, 0).x;

							if (oldValid && oldColor.a > 0 && newColor.a > 0)
							{
								oldColor = ClipVarianceYCoCg(oldColor, lowUV);
								oldDepth = ClampNeighborhoodDepth(oldDepth, lowUV);

								m.color = oldColor;
								m.depth = oldDepth;
							}
							else
							{
								m.color = _SGT_Volumetrics_ColorTex_New.SampleLevel(sampler_point_clamp, smoothUV, 0);
								m.depth = _SGT_Volumetrics_DepthTex_New.SampleLevel(sampler_point_clamp, smoothUV, 0).x;
							}
						}
					}
					else // TAA Only
					{
						if (oldValid)
						{
							float4 oldColor = _SGT_Volumetrics_ColorTex_Old.SampleLevel(sampler_linear_clamp, prevUV, 0);
							float  oldDepth = _SGT_Volumetrics_DepthTex_Old.SampleLevel(sampler_linear_clamp, prevUV, 0).x;

							oldColor = ClampNeighborhoodYCoCg2(oldColor, lowUV);

							float blend = _SGT_TAA * (newColor.w > 0);

							m.color = lerp(newColor, oldColor, blend);
							m.depth = lerp(newDepth, oldDepth, 0);
						}
						else
						{
							m.color = newColor;
							m.depth = newDepth;
						}
					}

					return m;
				}
			ENDHLSL
		} // Pass TAA

		Pass
		{
			Name "TAA2"
			ZWrite Off
			ZTest Always
			Blend Off
			Cull Off

			HLSLPROGRAM
				#pragma vertex   Vert
				#pragma fragment Frag

				// write old
				MRT2 Frag(Varyings varyings)
				{
					UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(varyings);

					MRT2 m;

					m.color = _SGT_Volumetrics_ColorTex.SampleLevel(sampler_point_clamp, varyings.texcoord0, 0);
					m.depth = _SGT_Volumetrics_DepthTex.SampleLevel(sampler_point_clamp, varyings.texcoord0, 0);

					return m;
				}
			ENDHLSL
		} // Pass TAA2
	}
}
