Shader "Hidden/SgtSquareLandscape"
{
	Properties
	{
		_CwAlbedo ("Albedo", 2D) = "white" {}
		_CwOpacity ("Opacity", 2D) = "white" {}
	}
	SubShader
	{
		Cull Off
		ZWrite Off
		ZTest Always
		Blend One Zero

		Pass
		{
			CGPROGRAM
			#pragma vertex   CW_Vert
			#pragma fragment CW_Frag
			#include "SgtFeature.cginc"

			float _SGT_OceanHeight;
			float _SGT_OceanDensity;

			int   _CwDepth;
			float _CwSquareSize;

			float3 _CwPositionA;
			float3 _CwPositionB;
			float3 _CwPositionC;

			float4x4 _CwCoordX;
			float4x4 _CwCoordY;
			float4x4 _CwCoordZ;
			float4x4 _CwCoordW;

			sampler2D _CwAlbedo;
			float2    _CwAlbedoSize;

			sampler2D _SGT_WeightTex;

			sampler2D_float _CwTopology;
			float2          _CwTopologySize;
			float3          _CwTopologyData;

			void CW_Frag(v2f i, out f2g o)
			{
				float3 weights    = tex2Dlod(_SGT_WeightTex, float4(i.coord, 0.0f, 0.0f)).xyz;
				float3 positionH  = i.localPos.xyz + _CwOffset;
				float3 positionL  = _CwPositionA * weights.x + _CwPositionB * weights.y + _CwPositionC * weights.z;
				float2 coord      = positionL.xz / _CwSquareSize + 0.5f;
				float4 albedo     = CW_SampleCubic(_CwAlbedo, coord, _CwAlbedoSize);
				float  occlusion  = 1.0f;
				float  emission   = 0.0f;
				float  smoothness = 0.0f;
				float4 topology   = CW_SampleCubic(_CwTopology, coord, _CwTopologySize);
				float  strata     = _CwTopologyData.z * topology.w;

				topology.xy -= 0.5;
				topology.xy /= _CwTopologyData.xy;

				float2 coordX = mul(_CwCoordX, float4(weights, 0)).xy;
				float2 coordY = mul(_CwCoordY, float4(weights, 0)).xy;
				float2 coordZ = mul(_CwCoordZ, float4(weights, 0)).xy;
				float2 coordW = mul(_CwCoordW, float4(weights, 0)).xy;

				float4x4 coordM = float4x4(
					coordX.x, coordY.x, coordZ.x, coordW.x,
					coordX.y, coordY.y, coordZ.y, coordW.y,
					0,0,0,0,
					0,0,0,0);

				float4 globalCoord = float4(coord, coord);

				float2 globalOffset = float2(0.0f, 0.0f);

				CW_ContributeTopologyFeatures(albedo, occlusion, emission, smoothness, topology, strata, coordM, positionL, globalCoord, globalOffset, float2(1.0f, 1.0f), 0.0f);

				topology.xyz = normalize(float3(topology.xy, 1.0));

				float height = lerp(topology.w, i.localPos.w, _CwDepth > 25);

				CW_ContributeColorFeatures(albedo, occlusion, emission, smoothness, topology, strata, coordM, positionL, globalCoord, globalOffset, float2(1.0f, 1.0f), 0.0f, height);

				albedo *= occlusion;

				float ocean = CW_EncodeWaterDepth(_SGT_OceanHeight - topology.w);

				o.rgbo = float4(albedo.xyz, ocean);
				o.nnes = float4(topology.xy * 0.5f + 0.5f, emission, smoothness);

				CW_ContributeHoles(o.rgbo, o.nnes, positionH);
			}
			ENDCG
		}
	}
}