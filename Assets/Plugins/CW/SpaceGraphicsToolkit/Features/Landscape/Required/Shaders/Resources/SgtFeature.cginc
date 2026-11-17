#include "SgtShared.cginc"

struct a2v
{
	float4 vertex    : POSITION;
	float2 texcoord0 : TEXCOORD0;
};

struct v2f
{
	float4 vertex   : SV_POSITION;
	float4 pixel    : TEXCOORD0;
	float2 coord    : TEXCOORD1;
	float4 localPos : TEXCOORD2;
};

struct f2g
{
	float4 rgbo : SV_TARGET0;
	float4 nnes : SV_TARGET1;
};

#define VERTEX_COUNT 243

#define FEATURE_CAPACITY 256
#define HOLES_CAPACITY 64

float4   _CwFeatureRangeDetail;  // x: global start, y: global end, z: local start, w: local end
float4   _CwFeatureRangeFlatten;
float4   _CwFeatureRangeColor;
float4   _CwFeatureRangeTexture;

float4   _CwFeatureDataA[FEATURE_CAPACITY];
float4   _CwFeatureDataB[FEATURE_CAPACITY];
float4   _CwFeatureDataC[FEATURE_CAPACITY];
float4   _CwFeatureDataD[FEATURE_CAPACITY];
float4   _CwFeatureDataE[FEATURE_CAPACITY];
float4   _CwFeatureLayer[FEATURE_CAPACITY];
float4x4 _CwFeatureMatrix[FEATURE_CAPACITY];

UNITY_DECLARE_TEX2DARRAY(_CwHeightTopologyAtlas);
UNITY_DECLARE_TEX2DARRAY(_CwMaskTopologyAtlas);
UNITY_DECLARE_TEX2DARRAY(_CwGradientAtlas);
UNITY_DECLARE_TEX2DARRAY(_CwDetailAtlas);

Texture2DArray _CwAlbedoAtlas;
SamplerState sampler_CwAlbedoAtlas;
Texture2DArray _CwNormalAtlas;
SamplerState sampler_CwNormalAtlas;

float4 _CwHeightTopologyAtlasSize;
float4 _CwMaskTopologyAtlasSize;
float4 _CwGradientAtlasSize;
float4 _CwDetailAtlasSize;
float4 _CwAlbedoAtlasSize;
float4 _CwNormalAtlasSize;

sampler2D       _CwNoiseTex;
sampler2D       _CwBufferP;
float4          _CwBufferSize;
float           _CwVertexResolution;
float2          _CwPixelSize;
float4x4        _CwMatrix;
float4          _CwCoords[VERTEX_COUNT];
float4          _CwWeights[VERTEX_COUNT];
float3          _CwOffset;

int      _CwHoleCount;
float4   _CwHoleDatas[HOLES_CAPACITY];
float4x4 _CwHoleMatrices[HOLES_CAPACITY];

float2 CW_Hash12(float x)
{
	// Returns pseudo-random float2 in range [0, 1]
	return frac(sin(float2(x, x + 1.618)) * float2(43758.5453, 22578.1459));
}

float4 CW_SampleTextureArrayGrad(Texture2DArray t, SamplerState s, float2 uv, float index, float2 uvx, float2 uvy, float tile, float noise01)
{
	#if 1
		float  v = noise01 * 8.0; 
		float  i = floor(v);
		float  f = frac(v);
		float4 a = t.SampleGrad(s, float3(uv * tile + CW_Hash12(i + 0.0), index), uvx * tile, uvy * tile);
		float4 b = t.SampleGrad(s, float3(uv * tile + CW_Hash12(i + 1.0), index), uvx * tile, uvy * tile);
	
		return lerp(a, b, smoothstep(0.2, 0.8, f));
	#else
		return t.SampleGrad(s, float3(uv * tile, index), uvx * tile, uvy * tile);
	#endif
}

void CW_ContributeTopo(inout float4 cur, float4 add, float replace, float mask)
{
	cur = lerp(cur, 0.0f, replace * mask) + add * mask;
}

void CW_ContributeStrata(inout float cur, float add, float replace, float mask)
{
	cur = lerp(cur, 0.0f, replace * mask) + add * mask;
}

float4 CW_Load(sampler2D s, float2 t, int x, int y)
{
	return tex2Dlod(s, float4((x + 0.5) * t.x, (y + 0.5) * t.y, 0.0, 0.0));
}

void CW_Vert(a2v i, out v2f o)
{
	int    vertexIndex = round(i.vertex.x);
	float2 coord       = _CwCoords[vertexIndex].xy;
	float  partIndex   = i.vertex.y;
	
	coord.x = (coord.x + partIndex) / 3.0f;
	
	o.coord    = coord;
	o.pixel    = float4(coord * _CwPixelSize, coord * _CwVertexResolution);
	o.localPos = CW_Load(_CwBufferP, _CwBufferSize.zw, vertexIndex, 0);
	o.vertex   = float4(coord * 2.0f - 1.0f, 0.5f, 1.0f);

#if UNITY_UV_STARTS_AT_TOP
	o.vertex.y = -o.vertex.y;
#endif
}

float4 CW_SampleMaskTopology(float4 globalCoord, float2 globalOffset, float maskIndex, float maskInvert, float maskSharpness, float maskShift, float detailIndex, float2 detailTiling, float maskDetailOffset)
{
	float4 maskTopology = float4(0.0f, 0.0f, 1.0f, 1.0f);

	if (maskIndex >= 0.0f)
	{
		float detail = 1.0f;

		if (detailIndex >= 0.0f)
		{
			detail = UNITY_SAMPLE_TEX2DARRAY(_CwDetailAtlas, float3(globalCoord.zw * detailTiling, detailIndex)).x;

			globalCoord.x += sin(detail * 6.2831853f) * maskDetailOffset;
			globalCoord.y += cos(detail * 6.2831853f) * maskDetailOffset;
		}
		
		maskTopology = UNITY_SAMPLE_TEX2DARRAY_LOD(_CwMaskTopologyAtlas, float3(globalCoord.xy + globalOffset * maskShift, maskIndex), 0);

		maskTopology.w = maskTopology.w * 2.0f - 1.0f;

		maskTopology.w = sign(maskTopology.w) * (1-pow(1-abs(maskTopology.w), max(1, 1.0f + maskSharpness * detail)));

		maskTopology.w = maskTopology.w * 0.5f + 0.5f;

		maskTopology.xy *= maskTopology.w;

		maskTopology.xyw = lerp(maskTopology.xyw, 1.0f - maskTopology.xyw, maskInvert);
	}

	return maskTopology;
}

float3 CW_CombineNormals(float3 a, float3 b)
{
	return normalize(float3(a.xy + b.xy, a.z * b.z));
}

float CW_CalculateRockStrata(float h)
{
	float3 s = (h * 2 + 23) / float3(10.0, 3.0, 0.7);
	float3 i = floor(s);
	float3 f = smoothstep(0.3, 0.7, frac(s));
	float3 a = frac(sin((i + 0.0) * float3(127.1, 269.5, 419.2)) * 53758.5453);
	float3 b = frac(sin((i + 1.0) * float3(127.1, 269.5, 419.2)) * 53758.5453);
	float  v = dot(lerp(a, b, f), float3(0.5, 0.35, 0.15));
	
	return lerp(0.5, 1.5, v);
}

void CW_ContributeHoles(inout float4 rgbo, inout float4 nnes, float3 localPos)
{
	for (int h = 0; h < _CwHoleCount; h++)
	{
		float3 entrancePoint = mul(_CwHoleMatrices[h], float4(localPos, 1.0)).xyz;
		float  circleDist    = dot(entrancePoint.xz, entrancePoint.xz);
		float  boxDist       = max(abs(entrancePoint.x), abs(entrancePoint.z));
		float  finalDist     = lerp(circleDist, boxDist, _CwHoleDatas[h].x);
		
		if (finalDist < 1.0 && abs(entrancePoint).y < 1.0)
		{
			rgbo = 0.0f;
			nnes = 0.0f;
			
			break;
		}
	}
}

void CW_ApplyDetail(int i, float4 topoCoord, float4 maskCoord, float2 globalOffset, float2 scale, float2 pole2, float angle1, inout float4 finalTopology, inout float finalStrata)
{
	float  strata           = _CwFeatureDataB[i].x;
	float  heightIndex      = _CwFeatureDataB[i].y;
	float2 heightRange      = _CwFeatureDataB[i].zw;
	float  replace          = _CwFeatureDataE[i].x;
	float  maskIndex        = _CwFeatureDataC[i].x;
	float  maskSharpness    = _CwFeatureDataC[i].y;
	float  maskShift        = _CwFeatureDataC[i].z;
	float  maskInvert       = _CwFeatureDataC[i].w;
	float  maskDetailIndex  = _CwFeatureDataD[i].x;
	float2 maskDetailTiling = _CwFeatureDataD[i].yz;
	float  maskDetailOffset = _CwFeatureDataD[i].w;

	#ifdef CW_TWO_COORDS
		float4 topologyA = UNITY_SAMPLE_TEX2DARRAY(_CwHeightTopologyAtlas, float3(topoCoord.xy, heightIndex)); topologyA.xy -= 0.5;
		float4 topologyB = UNITY_SAMPLE_TEX2DARRAY(_CwHeightTopologyAtlas, float3(topoCoord.zw, heightIndex)); topologyB.xy -= 0.5;
		topologyB.xy = CW_Rotate(topologyB.xy, angle1);
		float4 topology  = topologyA * pole2.x + topologyB * pole2.y;
	#else
		float4 topology = UNITY_SAMPLE_TEX2DARRAY(_CwHeightTopologyAtlas, float3(topoCoord.xy, heightIndex)); topology.xy -= 0.5;
	#endif

	float mask = CW_SampleMaskTopology(maskCoord, globalOffset, maskIndex, maskInvert, maskSharpness, maskShift, maskDetailIndex, maskDetailTiling, maskDetailOffset).w;
	CW_ContributeStrata(finalStrata, strata * topology.w, replace, mask);
	
	topology.xy *= scale * _CwHeightTopologyAtlasSize.xy * 0.5;
	topology.w   = heightRange.x + heightRange.y * topology.w;
	CW_ContributeTopo(finalTopology, topology, replace, mask);
}

void CW_ApplyFlatten(int i, float2 maskCoord, inout float4 finalTopology, inout float finalStrata)
{
	float targetHeight  = _CwFeatureDataA[i].x;
	float targetStrata  = _CwFeatureDataA[i].y;
	float flattenHeight = _CwFeatureDataA[i].z;
	float flattenStrata = _CwFeatureDataA[i].w;
	float maskIndex     = _CwFeatureDataC[i].x;
	float maskInvert    = _CwFeatureDataC[i].w;

	float4 maskTopology = float4(0.0f, 0.0f, 1.0f, 1.0f);
	
	if (maskIndex >= 0.0f)
	{
		maskTopology = UNITY_SAMPLE_TEX2DARRAY(_CwMaskTopologyAtlas, float3(maskCoord, maskIndex));
	}
	
	maskTopology.xyz = normalize(float3(maskTopology.xy * (targetHeight - finalTopology.w), 1.0f));
	maskTopology.xyw = lerp(maskTopology.xyw, 1.0f - maskTopology.xyw, maskInvert);
	finalTopology = lerp(finalTopology, float4(maskTopology.xyz, targetHeight), flattenHeight * maskTopology.w);
	finalStrata   = lerp(finalStrata, targetStrata, flattenStrata * maskTopology.w);
}

void CW_ApplyColor(int i, float4 maskCoord, float2 globalOffset, float n, inout float4 finalAlbedo, inout float finalOcclusion, inout float finalEmission, inout float finalSmoothness, float finalStrata)
{
	float  variation        = _CwFeatureDataA[i].x;
	float  occlusion        = _CwFeatureDataA[i].y;
	float  strata           = _CwFeatureDataA[i].z;
	float  gradientIndex    = _CwFeatureDataA[i].w;
	float  blur             = _CwFeatureDataB[i].x;
	float  offset           = _CwFeatureDataB[i].y;
	float  smoothnessStr    = _CwFeatureDataB[i].z;
	float  emissionStr      = _CwFeatureDataB[i].w;
	float  maskIndex        = _CwFeatureDataC[i].x;
	float  maskSharpness    = _CwFeatureDataC[i].y;
	float  maskShift        = _CwFeatureDataC[i].z;
	float  maskInvert       = _CwFeatureDataC[i].w;
	float  maskDetailIndex  = _CwFeatureDataD[i].x;
	float2 maskDetailTiling = _CwFeatureDataD[i].yz;
	float  maskDetailOffset = _CwFeatureDataD[i].w;
	float  smoothnessMid    = _CwFeatureDataE[i].x;
	float  smoothnessPow    = _CwFeatureDataE[i].y;
	float  emissionMid      = _CwFeatureDataE[i].z;
	float  emissionPow      = _CwFeatureDataE[i].w;

	float  v  = finalStrata * strata * 0.125f;
	float  ao = pow(1.0f - n, occlusion);
	
	float  mask         = CW_SampleMaskTopology(maskCoord, globalOffset, maskIndex, maskInvert, maskSharpness, maskShift, maskDetailIndex, maskDetailTiling, maskDetailOffset).w;
	float4 detailAlbedo = UNITY_SAMPLE_TEX2DARRAY_LOD(_CwGradientAtlas, float3(variation, offset + v, gradientIndex), blur) * ao;

	float luminosity       = dot(detailAlbedo.xyz, float3(0.2126f, 0.7152f, 0.0722f));
	float smoothnessWeight = saturate(pow(1.0f - abs(luminosity - smoothnessMid), smoothnessPow));
	float emissionWeight   = saturate(pow(1.0f - abs(luminosity - emissionMid), emissionPow));

	finalAlbedo.xyz = lerp(finalAlbedo.xyz, detailAlbedo.xyz, mask);
	finalSmoothness = lerp(finalSmoothness, smoothnessStr, mask * smoothnessWeight);
	finalEmission = lerp(finalEmission, emissionStr, mask * emissionWeight);
}

void CW_ApplyTexture(int i, float4 maskCoord, float2 globalOffset, float height, float slope, float noiseX, float noiseY, float4 textureCoord, float4 textureCoordX, float4 textureCoordY, float2 pole2, float angle1, inout float4 finalAlbedo, inout float finalOcclusion, inout float finalSmoothness, inout float3 textureNormal, inout float textureHeight, float finalStrata)
{
	float  albedoIndex      = _CwFeatureDataA[i].x;
	float  minHeight        = _CwFeatureDataA[i].y;
	float  minHeightFalloff = _CwFeatureDataA[i].z;
	float  maxHeight        = _CwFeatureDataA[i].w;
	float  maxHeightFalloff = _CwFeatureDataB[i].x;
	float  minSlope         = _CwFeatureDataB[i].y;
	float  minSlopeFalloff  = _CwFeatureDataB[i].z;
	float  maxSlope         = _CwFeatureDataB[i].w;
	float  maxSlopeFalloff  = _CwFeatureDataC[i].x;
	float  maskIndex        = _CwFeatureDataC[i].y;
	float  maskSharpness    = _CwFeatureDataC[i].z;
	float  maskInvert       = _CwFeatureDataC[i].w;
	float  maskDetailIndex  = _CwFeatureDataD[i].x;
	float2 maskDetailTiling = _CwFeatureDataD[i].yz;
	float  maskDetailOffset = _CwFeatureDataD[i].w;
	float  maskShift        = _CwFeatureDataE[i].x;
	float  normalIndex      = _CwFeatureDataE[i].y;
	float  strataWeight     = _CwFeatureDataE[i].z;

	float mask = CW_SampleMaskTopology(maskCoord, globalOffset, maskIndex, maskInvert, maskSharpness, maskShift, maskDetailIndex, maskDetailTiling, maskDetailOffset).w;
	
	float minH = saturate((height - minHeight) / minHeightFalloff);
	float maxH = saturate((maxHeight - height) / maxHeightFalloff);
	float minS = saturate((slope - minSlope) / minSlopeFalloff);
	float maxS = saturate((maxSlope - slope) / maxSlopeFalloff);
	
	mask *= lerp(1.0, minH, step(0.0, minHeight)) * lerp(1.0, maxH, step(0.0, maxHeight));
	mask *= lerp(1.0, minS, step(0.0, minSlope )) * lerp(1.0, maxS, step(0.0, maxSlope ));
	
	if (mask < 0.01) return;
	
	float tile = 32.0;
	#ifdef CW_TWO_COORDS
		float4 albedoA = CW_SampleTextureArrayGrad(_CwAlbedoAtlas, sampler_CwAlbedoAtlas, textureCoord.xy, albedoIndex, textureCoordX.xy, textureCoordY.xy, tile, noiseX);
		float4 albedoB = CW_SampleTextureArrayGrad(_CwAlbedoAtlas, sampler_CwAlbedoAtlas, textureCoord.zw, albedoIndex, textureCoordX.zw, textureCoordY.zw, tile, noiseX);
		float4 albedo  = albedoA * pole2.x + albedoB * pole2.y;
		float4 normalA = CW_SampleTextureArrayGrad(_CwNormalAtlas, sampler_CwNormalAtlas, textureCoord.xy, normalIndex, textureCoordX.xy, textureCoordY.xy, tile, noiseX); normalA.xy = normalA.xy * 2.0 - 1.0;
		float4 normalB = CW_SampleTextureArrayGrad(_CwNormalAtlas, sampler_CwNormalAtlas, textureCoord.zw, normalIndex, textureCoordX.zw, textureCoordY.zw, tile, noiseX); normalB.xy = normalB.xy * 2.0 - 1.0; normalB.xy = CW_Rotate(normalB.xy, angle1);
		float4 normal  = normalA * pole2.x + normalB * pole2.y;
	#else
		float4 albedo = CW_SampleTextureArrayGrad(_CwAlbedoAtlas, sampler_CwAlbedoAtlas, textureCoord.xy, albedoIndex, textureCoordX.xy, textureCoordY.xy, tile, noiseX);
		float4 normal = CW_SampleTextureArrayGrad(_CwNormalAtlas, sampler_CwNormalAtlas, textureCoord.xy, normalIndex, textureCoordX.xy, textureCoordY.xy, tile, noiseX); normal.xy = normal.xy * 2.0 - 1.0;
	#endif
	
	float strata = lerp(1.0, CW_CalculateRockStrata(height + noiseY * 0.5), strataWeight);
	albedo.xyz = saturate(albedo.xyz * lerp(0.8, 1.2, noiseX) * strata);
	
	float layerHeight = normal.w;
	float heightBias = (layerHeight - textureHeight) * 0.3; // HeightStrength
	mask = saturate(mask + heightBias);

	if (mask < 0.01) return;
	
	mask = pow(mask, 3);
	
	finalAlbedo.xyz = lerp(finalAlbedo.xyz, albedo.xyz, mask);
	finalOcclusion = lerp(finalOcclusion, albedo.w, mask);
	finalSmoothness = lerp(finalSmoothness, normal.z, mask);
	textureNormal.xy = lerp(textureNormal.xy, normal.xy, mask);
	textureHeight = lerp(textureHeight, layerHeight, mask);
}

void CW_ContributeTopologyFeatures(inout float4 finalAlbedo, inout float finalOcclusion, inout float finalEmission, inout float finalSmoothness, inout float4 finalTopology, inout float finalStrata, float4x4 coordM, float3 localPos, float4 globalCoord, float2 globalOffset, float2 pole2, float angle1)
{
	int i;
	
	UNITY_LOOP
	for (i = _CwFeatureRangeDetail.x; i < _CwFeatureRangeDetail.y; i++) // Global Detail
	{
		float scale = _CwFeatureDataA[i].x;
		float tile  = _CwFeatureDataA[i].y;
		float4 topoCoord = mul(coordM, _CwFeatureLayer[i]) * tile + _CwHeightTopologyAtlasSize.zwzw * 0.5f;
		CW_ApplyDetail(i, topoCoord, globalCoord, globalOffset, float2(scale, scale), pole2, angle1, finalTopology, finalStrata);
	}

	UNITY_LOOP
	for (i = _CwFeatureRangeDetail.z; i < _CwFeatureRangeDetail.w; i++) // Local Detail
	{
		float3 featurePoint = mul(_CwFeatureMatrix[i], float4(localPos, 1.0)).xyz;
		float3 featureBound = abs(featurePoint - 0.5f);
		if (max(max(featureBound.x, featureBound.y), featureBound.z) < 0.5f)
		{
			float2 tiling = _CwFeatureDataA[i].xy;
			float2 scale  = _CwFeatureDataA[i].zw;
			float4 topoCoord = float4(featurePoint.xy * tiling, 0, 0);
			CW_ApplyDetail(i, topoCoord, featurePoint.xyxy, globalOffset, scale, pole2, angle1, finalTopology, finalStrata);
		}
	}

	UNITY_LOOP
	for (i = _CwFeatureRangeFlatten.x; i < _CwFeatureRangeFlatten.y; i++) // Global Flatten
	{
		CW_ApplyFlatten(i, globalCoord.xy, finalTopology, finalStrata);
	}

	UNITY_LOOP
	for (i = _CwFeatureRangeFlatten.z; i < _CwFeatureRangeFlatten.w; i++) // Local Flatten
	{
		float3 featurePoint = mul(_CwFeatureMatrix[i], float4(localPos, 1.0)).xyz;
		float3 featureBound = abs(featurePoint - 0.5f);
		if (max(max(featureBound.x, featureBound.y), featureBound.z) < 0.5f)
		{
			CW_ApplyFlatten(i, featurePoint.xy, finalTopology, finalStrata);
		}
	}
}

void CW_ContributeColorFeatures(inout float4 finalAlbedo, inout float finalOcclusion, inout float finalEmission, inout float finalSmoothness, inout float4 finalTopology, inout float finalStrata, float4x4 coordM, float3 localPos, float4 globalCoord, float2 globalOffset, float2 pole2, float angle1, float height)
{
	int i;
	float  n     = length(finalTopology.xy);
	float  slope = degrees(atan(length(finalTopology.xy)));
	
	UNITY_LOOP
	for (i = _CwFeatureRangeColor.x; i < _CwFeatureRangeColor.y; i++) // Global Color
	{
		CW_ApplyColor(i, globalCoord, globalOffset, n, finalAlbedo, finalOcclusion, finalEmission, finalSmoothness, finalStrata);
	}
	
	UNITY_LOOP
	for (i = _CwFeatureRangeColor.z; i < _CwFeatureRangeColor.w; i++) // Local Color
	{
		float3 featurePoint = mul(_CwFeatureMatrix[i], float4(localPos, 1.0)).xyz;
		float3 featureBound = abs(featurePoint - 0.5f);
		if (max(max(featureBound.x, featureBound.y), featureBound.z) < 0.5f)
		{
			CW_ApplyColor(i, featurePoint.xyxy, globalOffset, n, finalAlbedo, finalOcclusion, finalEmission, finalSmoothness, finalStrata);
		}
	}
	
	float3 textureNormal = float3(0, 0, 1);
	float4 textureCoord  = mul(coordM, float4(0,1,0,0)) + _CwAlbedoAtlasSize.zwzw * 0.5f;
	float4 textureCoordX = ddx(textureCoord);
	float4 textureCoordY = ddy(textureCoord);
	
	#ifdef CW_TWO_COORDS
		float4 noise01A = tex2Dgrad(_CwNoiseTex, textureCoord.xy, textureCoordX.xy, textureCoordY.xy);
		float4 noise01B = tex2Dgrad(_CwNoiseTex, textureCoord.zw, textureCoordX.zw, textureCoordY.zw);
		float4 noise01  = noise01A * pole2.x + noise01B * pole2.y;
	#else
		float4 noise01 = tex2Dgrad(_CwNoiseTex, textureCoord.xy, textureCoordX.xy, textureCoordY.xy);
	#endif
	
	float textureHeight = -1.0;
	float height2 = height + finalStrata;
	
	UNITY_LOOP
	for (i = _CwFeatureRangeTexture.x; i < _CwFeatureRangeTexture.y; i++) // Global Texture
	{
		CW_ApplyTexture(i, globalCoord, globalOffset, height2, slope, noise01.x, noise01.y, textureCoord, textureCoordX, textureCoordY, pole2, angle1, finalAlbedo, finalOcclusion, finalSmoothness, textureNormal, textureHeight, finalStrata);
	}

	UNITY_LOOP
	for (i = _CwFeatureRangeTexture.z; i < _CwFeatureRangeTexture.w; i++) // Local Texture
	{
		float3 featurePoint = mul(_CwFeatureMatrix[i], float4(localPos, 1.0)).xyz;
		float3 featureBound = abs(featurePoint - 0.5f);
		if (max(max(featureBound.x, featureBound.y), featureBound.z) < 0.5f)
		{
			CW_ApplyTexture(i, featurePoint.xyxy, globalOffset, height2, slope, noise01.x, noise01.y, textureCoord, textureCoordX, textureCoordY, pole2, angle1, finalAlbedo, finalOcclusion, finalSmoothness, textureNormal, textureHeight, finalStrata);
		}
	}
	
	// Blend normals
	textureNormal.z = sqrt(1.0 - textureNormal.x * textureNormal.x - textureNormal.y * textureNormal.y);
	finalTopology.xyz = CW_CombineNormals(finalTopology.xyz, textureNormal);
}

static const float WATER_DEPTH_SHALLOW = 30.0f;
static const float WATER_DEPTH_MAX     = 3000.0f;

float CW_EncodeWaterDepth(float depthInMeters)
{
	float shallow = depthInMeters / (WATER_DEPTH_SHALLOW * 2.0f);
	float deep = 0.5f + (depthInMeters - WATER_DEPTH_SHALLOW) / ((WATER_DEPTH_MAX - WATER_DEPTH_SHALLOW) * 2.0f);
	float isDeep = step(WATER_DEPTH_SHALLOW, depthInMeters);
	return saturate(lerp(shallow, deep, isDeep));
}