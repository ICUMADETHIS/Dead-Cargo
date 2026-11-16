#ifndef DEAD_CARGO_STYLIZED_SURFACE_EMISSION_INCLUDED
#define DEAD_CARGO_STYLIZED_SURFACE_EMISSION_INCLUDED

#include "Assets/FlatKit/Shaders/StylizedSurface/DustyroomStylizedLighting.cginc"

sampler2D _EmissionMap;
half4 _EmissionColor;

void surfWithEmission(InputObject input, inout SurfaceOutputDustyroom surface)
{
    surfObject(input, surface);
    surface.Emission = tex2D(_EmissionMap, input.uv_MainTex).rgb * _EmissionColor.rgb;
}

#endif
