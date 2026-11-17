sampler3D _SGT_ErodeTex;
sampler3D _SGT_ErodeNoiseTex;
float4    _SGT_ErodeData;
float     _SGT_ErodeTiling;
float     _SGT_ErodeStrength;

float    _SGT_CarveTiling;
float    _SGT_CarveStrength;
float4   _SGT_CarveData;

float SGT_Hash31(float3 p)
{
	p = frac(p * 0.1031);
	p += dot(p, p.yzx + 33.33);
	return frac((p.x + p.y) * p.z);
}

float3 SGT_Permutate(float3 p, float id)
{
	float2 s, c; sincos(id * float2(2.39996, 4.44288), s, c);
	p.xz = p.xz * c.x + float2(-p.z, p.x) * s.x; 
	p.yz = p.yz * c.y + float2(-p.z, p.y) * s.y; 
	return p;
}

float4 SGT_SampleNoiseStochastic(sampler3D tex, float3 p, float lod)
{
	float phase = tex3Dlod(_SGT_ErodeNoiseTex, float4(p * 0.05, 0)).x * 7.0;
	float id    = floor(phase);
	float w     = smoothstep(0.0, 1.0, frac(phase));

	#if _SGT_ERODE_STOCHASTIC // 2 tap
		float4 tex0 = tex3Dlod(tex, float4(SGT_Permutate(p, id    ), lod));
		float4 tex1 = tex3Dlod(tex, float4(SGT_Permutate(p, id + 1), lod));
		return lerp(tex0, tex1, w);
	#else // 1 tap
		float rnd = frac(SGT_Hash31(floor(p * 1111.0)));
		return tex3Dlod(tex, float4(SGT_Permutate(p, rnd >= w ? id : id + 1), lod));
	#endif
}

float4 SGT_SampleNoiseLayered(sampler3D tex, float3 p, float lod, float distanceWeight)
{
	float  steps = (1.0f - distanceWeight * distanceWeight) * 5;
	float  i     = floor(steps);
	float  f     = steps - i;
	float3 uv    = p * pow(0.5f, i);

	float4 tex0 = tex3Dlod(_SGT_ErodeTex, float4(uv * 1, lod));
	float4 tex1 = tex3Dlod(_SGT_ErodeTex, float4(uv * 2, lod));
	return lerp(tex1, tex0, f);
}

void SGT_ErodeBase(float3 eyePos, float3 pos, float3 noisePos, inout float density, float4 erodeData, float4 erodeStrength, float lod0)
{
	float eyeD       = distance(eyePos, pos);
	float erodeRange = erodeData.z;
	float erodeRecip = erodeData.w;

	if (density > 0.0 && eyeD < erodeRange)
	{
		float distanceWeight = 1.0f - eyeD * erodeRecip;
		float noiseWeight    = saturate(density * 0.5);

		float  lod = lerp(10, lod0, 1-pow(1-distanceWeight,2));
		float4 str = lerp(0, erodeStrength, distanceWeight);

		#if _SGT_ERODE_LAYERED
			float4 noise4 = SGT_SampleNoiseLayered(_SGT_ErodeTex, noisePos, lod, distanceWeight) - 0.5;
		#else
			float4 noise4 = SGT_SampleNoiseStochastic(_SGT_ErodeTex, noisePos, lod) - 0.5;
		#endif
		noise4 = lerp(noise4, 1, density);
		//noise4 = lerp(0.0, noise4, distanceWeight);
		float noise1 = dot(noise4, str);
		
		density += noise1 * distanceWeight * noiseWeight * (1-noiseWeight);

		density = saturate(density);
	}
}

void SGT_Erode(float3 eyePos, float3 pos, float3 noisePos, inout float density, float4 strength, float lod0)
{
	#if !_SGT_ERODE_OFF
		SGT_ErodeBase(eyePos, pos, noisePos, density, _SGT_ErodeData, _SGT_ErodeStrength * strength, lod0);
	#endif
}

void SGT_Carve(float3 eyePos, float3 pos, float3 noisePos, inout float density, float4 strength, float lod0)
{
	#if _SGT_CARVE
		SGT_ErodeBase(eyePos, pos, noisePos * _SGT_CarveTiling, density, _SGT_CarveData, _SGT_CarveStrength * strength, lod0);
	#endif
}