#include "UnityCG.cginc"
#include "../../../LightAndShadow/Required/Shaders/Resources/SgtLightAndShadow.cginc"
#include "../../../Sky/Required/Shaders/SgtSky.cginc"
#include "../../../../../Shared/SimpleOriginSystem/Scripts/PositionCompression.cginc"
#define __SGT_CUTOFF 0.05

SamplerState sampler_point_clamp;

Texture2D _SGT_SceneDepthTexture;
float SGT_GetLinearEyeDepth(float2 uv) { return _SGT_SceneDepthTexture.SampleLevel(sampler_point_clamp, uv, 0); }

float remap(float x, float low1, float high1, float low2, float high2)
{
	return low2 + (x - low1) * (high2 - low2) / (high1 - low1);
}

float henyey_greenstein_phase_func(float mu, float g)
{
	return (1. - g*g) / ((4. * UNITY_PI) * pow(1. + g*g - 2.*g*mu, 1.5));
}

void SGT_Ortho(float3 worldPoint, out float3 worldPos, out float3 worldDir)
{
	float3 forward = -normalize(UNITY_MATRIX_V[2].xyz);

	float3 planePoint = _WorldSpaceCameraPos;

	float d = dot(forward, forward);
	float t = dot(planePoint - worldPoint, forward) / d;

	worldPos = worldPoint + forward * t;
	worldDir = forward;
}

float InverseLerpClamped(float a, float b, float value)
{
    return saturate((value - a) / (b - a)); // saturate clamps between 0 and 1
}

float HenyeyGreenstein(float g, float costh)
{
    return (1.0 / (4.0 * 3.14159265)) * ((1.0 - g * g) / pow(1.0 + g * g - 2.0 * g * costh, 1.5));
}

float3 SGT_MultipleOctaves(float extinction, float mu, float stepL, float3 sigmaE)
{
    float3 luminance = 0;
    float a = 1.0;
    float b = 1.0;
    float c = 1.0;

    for (int i = 0; i < 4; i++)
    {
        // Two-lobed Henyey-Greenstein
        float phase = lerp(HenyeyGreenstein(-0.1 * c, mu), HenyeyGreenstein(0.3 * c, mu), 0.7);
        luminance += b * phase * exp(-stepL * extinction * sigmaE * a);
        a *= 0.2;
        b *= 0.5;
        c *= 0.5;
    }
    return luminance;
}