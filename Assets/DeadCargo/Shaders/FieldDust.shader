// Dead Cargo's field dust: soft banks of dust hanging through the asteroid fields, so rocks sit in something rather
// than float on the sky, and distance inside a field reads at a glance. Thick where the rock is thick, clumps and gaps
// included, and gone in open space.
// One static mesh holds every bank at a fixed spot in a unit cube. The vertex shader repeats that cube through space,
// draws the copy of each bank nearest the camera as a quad facing it, and reads how thick the field is there from
// the field's density map (Scripts/Sectors/Field/FieldDensityMap.cs). Banks stay where they are in the world, cost
// no CPU work, and those in open space are dropped before they are drawn.
// Soft and un-inked on purpose: dust belongs with the sky, not with the comic look of near objects.
Shader "DeadCargo/Field Dust"
{
    Properties
    {
        _Color ("Lit Colour", Color) = (0.42, 0.385, 0.35, 1)
        _ShadowColor ("Shadow Colour", Color) = (0.042, 0.047, 0.065, 1)
        _Opacity ("Opacity", Range(0, 0.5)) = 0.135
        _Amount ("Amount", Range(0, 1)) = 1
        _Scatter ("Sun Scatter", Range(0.5, 8)) = 3
        _FieldSize ("Field Size (m)", Range(2000, 20000)) = 9000
        _BankSize ("Bank Size (m)", Range(100, 3000)) = 1100
        _DensityFloor ("Density Floor", Range(0, 1)) = 0.2
        _DensityFull ("Density Full", Range(0, 1)) = 0.7
        _NearFade ("Near Fade (bank sizes)", Range(0.2, 3)) = 1.1
        _FarFade ("Far Fade Start", Range(0.1, 1)) = 0.6
        _Wisp ("Wisps", Range(0, 1)) = 0.75
        _WispScale ("Wisp Scale", Range(0.5, 8)) = 2.2
        _Drift ("Drift", Range(0, 0.2)) = 0.012
        [HideInInspector] _SunDirection ("Sun Direction", Vector) = (0, 1, 0, 0)
    }

    SubShader
    {
        // Before other transparent things, such as engine glow and space dust, which sit in front of the haze.
        Tags { "Queue" = "Transparent-10" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        // Premultiplied alpha, so dust both catches the light and veils what is behind it.
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma target 3.5

            #include "UnityCG.cginc"

            float4 _Color;
            float4 _ShadowColor;
            float _Opacity;
            float _Amount;
            float _Scatter;
            float _FieldSize;
            float _BankSize;
            float _DensityFloor;
            float _DensityFull;
            float _NearFade;
            float _FarFade;
            float _Wisp;
            float _WispScale;
            float _Drift;

            // Unit direction towards the sun, set by Field Dust (Scripts/Visuals/FieldDust.cs).
            float4 _SunDirection;

            // The field's density map, set for the whole scene by the asteroid field. Origin: the map's lowest corner
            // in world space, with w 1 while a map is published. Scale: one over its size in metres.
            sampler3D _FieldDensityMap;
            float4 _FieldDensityOrigin;
            float4 _FieldDensityScale;

            // Banks fainter than this are dropped in the vertex shader, so they cost no pixels.
            static const float MinOpacity = 0.002;

            static const float MinWidth = 1e-4;

            struct Attributes
            {
                // The bank's spot in the unit cube.
                float4 vertex : POSITION;

                // The quad's corner, -1 to 1 each way.
                float2 corner : TEXCOORD0;

                // x: size, y: strength, z: rank against Amount, all 0..1.
                float3 random : TEXCOORD1;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;

                // xy: across the bank, -1 to 1. zw: where its wisps are taken from.
                float4 local : TEXCOORD0;
                nointerpolation float4 tone : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                // The copy of this bank nearest the camera, inside a cube of the field size centred on it.
                float3 camera = _WorldSpaceCameraPos;
                float3 centre = camera + (frac(input.vertex.xyz - camera / _FieldSize + 0.5) - 0.5) * _FieldSize;
                float size = _BankSize * lerp(0.6, 1.6, input.random.x);

                // How thick the field is at the bank. Outside the map there is no field.
                float3 mapPoint = (centre - _FieldDensityOrigin.xyz) * _FieldDensityScale.xyz;
                float inside = all(mapPoint == saturate(mapPoint)) ? _FieldDensityOrigin.w : 0;
                float density = tex3Dlod(_FieldDensityMap, float4(mapPoint, 0)).r * inside;
                float thickness = smoothstep(_DensityFloor, max(_DensityFull, _DensityFloor + MinWidth), density);

                // Fades out as the camera enters it, so flying through never shows its edge or fills the screen, and
                // towards the cube's edge, so banks never pop as they wrap.
                float away = length(centre - camera);
                float halfField = _FieldSize * 0.5;
                float fade = smoothstep(size * _NearFade * 0.3, size * _NearFade, away)
                    * (1 - smoothstep(halfField * _FarFade, halfField, away));
                float opacity = _Opacity * thickness * fade * lerp(0.5, 1, input.random.y) * step(input.random.z, _Amount);

                // A quad facing the camera.
                float3 right = UNITY_MATRIX_V[0].xyz;
                float3 up = UNITY_MATRIX_V[1].xyz;
                float3 world = centre + (right * input.corner.x + up * input.corner.y) * size;
                output.position = UnityWorldToClipPos(world);

                // Dust between the camera and the sun glows; dust lit from behind the camera is dim.
                float3 toBank = (centre - camera) / max(away, MinWidth);
                float towardSun = saturate(dot(toBank, _SunDirection.xyz) * 0.5 + 0.5);
                output.tone = float4(lerp(_ShadowColor.rgb, _Color.rgb, pow(towardSun, _Scatter)), opacity);

                // Each bank takes its wisps from its own place in the noise, turned its own way.
                float turn = input.random.y * 6.2831853;
                float2 turned = float2(cos(turn), sin(turn));
                float2 across = float2(input.corner.x * turned.x - input.corner.y * turned.y,
                    input.corner.x * turned.y + input.corner.y * turned.x);
                output.local = float4(input.corner, across * _WispScale + input.vertex.xy * 37.0);

                if (opacity < MinOpacity)
                    output.position = float4(0, 0, 0, 1);

                return output;
            }

            float Hash(float2 cell)
            {
                return frac(sin(dot(cell, float2(127.1, 311.7))) * 43758.5453);
            }

            // Smooth noise from 0 to 1.
            float Noise(float2 place)
            {
                float2 cell = floor(place);
                float2 within = frac(place);
                within = within * within * (3 - 2 * within);
                float low = lerp(Hash(cell), Hash(cell + float2(1, 0)), within.x);
                float high = lerp(Hash(cell + float2(0, 1)), Hash(cell + float2(1, 1)), within.x);
                return lerp(low, high, within.y);
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                // A soft ball, thickest at the centre and gone at the quad's edge.
                float body = saturate(1 - dot(input.local.xy, input.local.xy));
                body *= body;

                // Wisps: two layers of noise drifting slowly past each other, so the dust is never still.
                float2 place = input.local.zw + _Time.y * _Drift;
                float wisps = Noise(place) * 0.65 + Noise(place * 2.3 - _Time.y * _Drift * 1.7) * 0.35;
                float alpha = body * lerp(1, saturate(wisps * 1.8 - 0.3), _Wisp) * input.tone.a;
                return half4(input.tone.rgb * alpha, alpha);
            }
            ENDCG
        }
    }

    Fallback Off
}
