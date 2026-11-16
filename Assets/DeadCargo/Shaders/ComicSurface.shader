// Dead Cargo's comic surface: the look for near objects such as ships, rocks and stations. A painted texture lit in
// three soft tones (shadow, mid and lit), with a highlight band and a flat glint facing the light, a rim, a bold ink
// outline round the silhouette, and ink drawn into the texture's darkest details. Hatching is available for a
// harder comic look. Ink fades out with distance, so far objects read as flat and far, matching the sky.
// GPU instancing is supported, so an asteroid field draws thousands of rocks in a few batches. Property names follow
// Flat Kit's where they mean the same thing, so a Flat Kit material converts by copying it with this shader.
Shader "DeadCargo/Comic Surface"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _TextureImpact ("Texture Strength", Range(0, 1)) = 1
        [Normal] [NoScaleOffset] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Range(0, 2)) = 1
        _Color ("Lit Colour", Color) = (0.8, 0.8, 0.8, 1)
        _ColorDim ("Shadow Colour", Color) = (0.3, 0.3, 0.34, 1)
        _MidBlend ("Mid Tone", Range(0, 1)) = 0.55
        _SelfShadingSize ("Shadow Edge", Range(0, 1)) = 0.45
        _LitEdge ("Lit Edge", Range(0, 1)) = 0.7
        _CelSoftness ("Cel Softness", Range(0, 0.25)) = 0.05
        _LightContribution ("Light Colour Strength", Range(0, 1)) = 0.2
        _InstanceVariation ("Instance Variation", Range(0, 0.5)) = 0

        _HighlightColor ("Highlight Colour", Color) = (1, 0.95, 0.85, 1)
        _HighlightSize ("Highlight Size", Range(0, 1)) = 0.12
        _HighlightStrength ("Highlight Strength", Range(0, 1)) = 0.35
        _SpecularColor ("Glint Colour", Color) = (1, 1, 1, 1)
        _SpecularSize ("Glint Size", Range(0, 0.5)) = 0.03
        _SpecularStrength ("Glint Strength", Range(0, 1)) = 0.5
        _RimColor ("Rim Colour", Color) = (0.55, 0.7, 0.9, 1)
        _RimSize ("Rim Size", Range(0, 1)) = 0.1
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.3

        _InkColor ("Ink", Color) = (0.03, 0.025, 0.04, 1)
        _InkWidth ("Ink Width (px at 1080p)", Range(0, 6)) = 2
        _InkGapFill ("Ink Gap Fill", Range(0, 1)) = 0
        _InkFade ("Ink Fade (start, end, m)", Vector) = (150, 1200, 0, 0)
        _TextureInk ("Texture Ink", Range(0, 1)) = 0
        _Hatching ("Hatching", Range(0, 1)) = 0
        _HatchSpacing ("Hatch Spacing (m)", Range(0.02, 10)) = 0.2
        _HatchWidth ("Hatch Width (px at 1080p)", Range(0.5, 3)) = 1.2

        [Toggle(_TRIPLANAR)] _Triplanar ("Triplanar (voxel rock)", Float) = 0
        _TriplanarTile ("Triplanar Tile (m)", Range(0.5, 64)) = 8
        _TriplanarSharpness ("Triplanar Sharpness", Range(1, 16)) = 4
        _OreShadow ("Ore Shadow", Range(0, 1)) = 0.35
        [HideInInspector] _TriplanarOffset ("Triplanar Offset (m)", Vector) = (0, 0, 0, 0)

        // Cross-fade between two meshes of one object, such as voxel rock changing detail. x: share shown, 0 to 1;
        // y: 1 for the mesh fading in, -1 for the one fading out. Only read with the _DITHER_FADE keyword.
        [HideInInspector] _DitherFade ("Dither Fade", Vector) = (1, 1, 0, 0)
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    float4 _InkColor;
    float _InkWidth;
    float _InkGapFill;
    float4 _InkFade;
    float4 _DitherFade;

    static const float ReferenceHeight = 1080.0;
    static const float MinWidth = 1e-4;

    // How much ink survives at a distance: all of it up close, none beyond the fade's end.
    float InkAtDistance(float distance)
    {
        return 1 - saturate((distance - _InkFade.x) / max(_InkFade.y - _InkFade.x, MinWidth));
    }

    // Coverage of a line of the given width, both in pixels, at a distance in pixels from its centre.
    float LineCoverage(float offset, float width)
    {
        return saturate(width * 0.5 - offset + 0.5) * saturate(width);
    }

    // A hard step at the threshold, softened over one pixel.
    float CrispStep(float threshold, float value, float width)
    {
        return saturate((value - threshold) / width + 0.5);
    }

    // Dithered cross-fade: the mesh fading in keeps the pixels whose threshold is below the share shown, the mesh fading
    // out keeps the rest, so the two together cover every pixel exactly once and nothing pops. Interleaved gradient
    // noise spreads the pattern evenly, too fine to read as a pattern while it lasts.
    void DitherClip(float2 pixel)
    {
    #if defined(_DITHER_FADE)
        float threshold = frac(52.9829189 * frac(dot(floor(pixel), float2(0.06711056, 0.00583715))));
        clip((_DitherFade.x - threshold) * _DitherFade.y);
    #endif
    }
    ENDCG

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }

        Pass
        {
            Name "Comic"
            Tags { "LightMode" = "ForwardBase" }

            CGPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma target 3.5
            #pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight
            #pragma multi_compile_instancing
            #pragma shader_feature_local _TRIPLANAR
            #pragma multi_compile_local _ _DITHER_FADE

            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _TextureImpact;
            sampler2D _BumpMap;
            float _BumpScale;
            float4 _Color;
            float4 _ColorDim;
            float _MidBlend;
            float _SelfShadingSize;
            float _LitEdge;
            float _CelSoftness;
            float _LightContribution;
            float _InstanceVariation;
            float _TextureInk;

            float4 _HighlightColor;
            float _HighlightSize;
            float _HighlightStrength;
            float4 _SpecularColor;
            float _SpecularSize;
            float _SpecularStrength;
            float4 _RimColor;
            float _RimSize;
            float _RimStrength;

            float _Hatching;
            float _HatchSpacing;
            float _HatchWidth;

            float _TriplanarTile;
            float _TriplanarSharpness;
            float _OreShadow;
            float4 _TriplanarOffset;

            // Hatching fades out where its strokes would be closer together than this, in pixels.
            static const float MinHatchSpacing = 3.0;

            // How far below the shadow line hatching reaches full width, and where crossing strokes begin.
            static const float HatchRamp = 0.2;
            static const float CrossHatchDepth = 0.3;

            // Voxel rock has no texture coordinates or tangents: it is textured by projection and carries its ore
            // tint in the vertex colour instead.
            struct Attributes
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            #if defined(_TRIPLANAR)
                float4 color : COLOR;
            #else
                float4 tangent : TANGENT;
                float2 uv : TEXCOORD0;
            #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                // Named pos, as the shadow macros expect.
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldPos : TEXCOORD2;

                // Object-space position in metres, so hatching is anchored to the object and the same size on any mesh.
                float3 metricPos : TEXCOORD3;
                float3 objectNormal : TEXCOORD4;
                nointerpolation float tint : TEXCOORD5;
                SHADOW_COORDS(6)
            #if defined(_TRIPLANAR)
                float4 color : TEXCOORD7;
            #else
                float3 worldTangent : TEXCOORD7;
                float3 worldBitangent : TEXCOORD8;
            #endif
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // The shadow macros read the vertex input as v.
            Varyings Vertex(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;

            #if defined(_TRIPLANAR)
                o.uv = float2(0, 0);
                o.color = v.color;
            #else
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                // Tangent frame for the normal map. A mesh without tangents gets a zero frame, which leaves the
                // default flat normal map with no effect.
                o.worldTangent = UnityObjectToWorldDir(v.tangent.xyz);
                o.worldBitangent = cross(o.worldNormal, o.worldTangent) * v.tangent.w * unity_WorldTransformParams.w;
            #endif

                float3 scale = float3(length(unity_ObjectToWorld._m00_m10_m20), length(unity_ObjectToWorld._m01_m11_m21),
                    length(unity_ObjectToWorld._m02_m12_m22));
                o.metricPos = v.vertex.xyz * scale;
                o.objectNormal = v.normal;

                // Each object, or each instance of an instanced batch, gets its own small shade variation.
                float3 origin = unity_ObjectToWorld._m03_m13_m23;
                o.tint = frac(sin(dot(origin, float3(12.9898, 78.233, 37.719))) * 43758.5453);

                TRANSFER_SHADOW(o);
                return o;
            }

            // Diagonal strokes on the object's surface, on the plane facing its normal most. They fill the shadowed
            // side, taper to nothing towards the light, and cross over in the darkest parts.
            float Hatching(float3 metricPos, float3 objectNormal, float light, float pixelScale)
            {
                float3 facing = abs(objectNormal);
                float2 plane = facing.x > facing.y && facing.x > facing.z ? metricPos.yz
                    : facing.y > facing.z ? metricPos.xz : metricPos.xy;
                float2 strokes = float2(plane.x + plane.y, plane.x - plane.y) / _HatchSpacing;
                float2 width = max(fwidth(strokes), MinWidth);

                float depth = _SelfShadingSize - light;
                float primary = saturate(depth / HatchRamp);
                float crossing = saturate((depth - CrossHatchDepth) / HatchRamp);
                float2 stroke = _HatchWidth * pixelScale * float2(primary, crossing);

                float2 gap = abs(frac(strokes) - 0.5) / width;
                float2 coverage = float2(LineCoverage(gap.x, stroke.x), LineCoverage(gap.y, stroke.y));
                coverage *= saturate((1 / width - MinHatchSpacing) / MinHatchSpacing);
                return max(coverage.x, coverage.y);
            }

        #if defined(_TRIPLANAR)
            // Texture and normal map projected along the object's three axes and blended by the surface's facing, in
            // metres, so they stick to the rock as it turns. The normals use a whiteout blend, so relief keeps its
            // depth on every side.
            void Triplanar(Varyings i, out float3 texel, out float3 normal)
            {
                float3 objectNormal = normalize(i.objectNormal);
                float3 weights = pow(abs(objectNormal), _TriplanarSharpness);
                weights /= max(dot(weights, float3(1, 1, 1)), MinWidth);

                float3 p = (i.metricPos + _TriplanarOffset.xyz) / _TriplanarTile;
                float2 uvX = p.zy;
                float2 uvY = p.xz;
                float2 uvZ = p.xy;

                texel = tex2D(_MainTex, uvX).rgb * weights.x + tex2D(_MainTex, uvY).rgb * weights.y + tex2D(_MainTex, uvZ).rgb * weights.z;

                float3 bumpX = UnpackNormalWithScale(tex2D(_BumpMap, uvX), _BumpScale);
                float3 bumpY = UnpackNormalWithScale(tex2D(_BumpMap, uvY), _BumpScale);
                float3 bumpZ = UnpackNormalWithScale(tex2D(_BumpMap, uvZ), _BumpScale);
                bumpX = float3(bumpX.xy + objectNormal.zy, abs(bumpX.z) * objectNormal.x);
                bumpY = float3(bumpY.xy + objectNormal.xz, abs(bumpY.z) * objectNormal.y);
                bumpZ = float3(bumpZ.xy + objectNormal.xy, abs(bumpZ.z) * objectNormal.z);
                float3 bumped = normalize(bumpX.zyx * weights.x + bumpY.xzy * weights.y + bumpZ.xyz * weights.z);
                normal = normalize(UnityObjectToWorldNormal(bumped));
            }
        #endif

            half4 Fragment(Varyings i) : SV_Target
            {
                DitherClip(i.pos.xy);
            #if defined(_TRIPLANAR)
                float3 texel;
                float3 normal;
                Triplanar(i, texel, normal);
            #else
                float3 detail = UnpackNormalWithScale(tex2D(_BumpMap, i.uv), _BumpScale);
                float3 normal = normalize(detail.x * i.worldTangent + detail.y * i.worldBitangent + detail.z * i.worldNormal);
                float3 texel = tex2D(_MainTex, i.uv).rgb;
            #endif
                float3 toLight = normalize(_WorldSpaceLightPos0.xyz);
                float3 toView = normalize(_WorldSpaceCameraPos - i.worldPos);
                float pixelScale = _ScreenParams.y / ReferenceHeight;

                // Light amount from 0 (facing away) to 1 (facing the light). Cast shadows count as facing away.
                float light = (dot(normal, toLight) * 0.5 + 0.5) * SHADOW_ATTENUATION(i);

                // Three tones, shadow, mid and lit, with edges a little soft: a hint of cel rather than hard cuts.
                float toneWidth = max(fwidth(light), _CelSoftness);
                float lit = CrispStep(_SelfShadingSize, light, toneWidth);
                float bright = CrispStep(_LitEdge, light, toneWidth);
                float highlight = CrispStep(1 - _HighlightSize, light, toneWidth) * _HighlightStrength;

                float3 albedo = lerp(1, texel, _TextureImpact);
                albedo *= 1 + (i.tint - 0.5) * 2 * _InstanceVariation;
                float3 lightTint = lerp(1, _LightColor0.rgb, _LightContribution);
                float3 litColor = albedo * _Color.rgb * lightTint;
                float3 shadowColor = albedo * _ColorDim.rgb;

            #if defined(_TRIPLANAR)
                // Ore veins take over the rock's lit and shadow tones by their strength, keeping the texture's grain.
                litColor = lerp(litColor, albedo * i.color.rgb * lightTint, i.color.a);
                shadowColor = lerp(shadowColor, albedo * i.color.rgb * _OreShadow, i.color.a);
            #endif
                float3 midColor = lerp(shadowColor, litColor, _MidBlend);
                float3 color = lerp(lerp(shadowColor, midColor, lit), litColor, bright);
                color = lerp(color, _HighlightColor.rgb, highlight);

                // Glint: a flat spot where the surface mirrors the light towards the viewer.
                float mirror = dot(normal, normalize(toLight + toView));
                float glint = CrispStep(1 - _SpecularSize, mirror, max(fwidth(mirror), MinWidth)) * lit * _SpecularStrength;
                color = lerp(color, _SpecularColor.rgb, glint);

                // Rim: a band along the edges seen side-on.
                float edge = 1 - saturate(dot(normal, toView));
                float rim = CrispStep(1 - _RimSize, edge, max(fwidth(edge), MinWidth)) * _RimStrength;
                color = lerp(color, _RimColor.rgb, rim);

                // Texture ink: the texture's darkest details, such as cracks and crater rims, are drawn in ink, like
                // line work painted into the texture. Hatching, when used, is ink too. Both fade with distance.
                float ink = InkAtDistance(length(_WorldSpaceCameraPos - i.worldPos));
                float shade = Luminance(texel);
                float lines = CrispStep(-_TextureInk, -shade, max(fwidth(shade), MinWidth)) * saturate(_TextureInk / MinWidth);
                float hatch = Hatching(i.metricPos, i.objectNormal, light, pixelScale) * _Hatching;
                color = lerp(color, _InkColor.rgb, max(lines, hatch) * ink);

                return half4(color, 1);
            }
            ENDCG
        }

        // Ink outline: the back faces pushed out by a fixed pixel width, drawn behind the object.
        Pass
        {
            Name "Ink"
            Tags { "LightMode" = "Always" }
            Cull Front

            CGPROGRAM
            #pragma vertex InkVertex
            #pragma fragment InkFragment
            #pragma target 3.5
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ _DITHER_FADE

            struct InkAttributes
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct InkVaryings
            {
                float4 pos : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            InkVaryings InkVertex(InkAttributes v)
            {
                InkVaryings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                // Meshes with hard edges have split normals, which open gaps in the outline at corners. Gap Fill pushes
                // out from the object's centre instead, which closes them on rounded shapes such as rocks.
                float3 outward = v.vertex.xyz / max(length(v.vertex.xyz), MinWidth);
                float3 direction = normalize(lerp(v.normal, outward, _InkGapFill) + float3(0, 0, MinWidth));

                o.pos = UnityObjectToClipPos(v.vertex);
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 viewNormal = mul((float3x3)UNITY_MATRIX_V, UnityObjectToWorldNormal(direction));
                float2 clipNormal = float2(UNITY_MATRIX_P[0][0] * viewNormal.x, UNITY_MATRIX_P[1][1] * viewNormal.y);
                float clipLength = length(clipNormal);
                clipNormal = clipLength > MinWidth ? clipNormal / clipLength : float2(0, 0);

                float widthPixels = _InkWidth * (_ScreenParams.y / ReferenceHeight) * InkAtDistance(length(_WorldSpaceCameraPos - worldPos));
                o.pos.xy += clipNormal * (widthPixels * 2 / _ScreenParams.xy) * o.pos.w;

                // With no ink to draw, the outline collapses to nothing.
                if (widthPixels <= 0)
                    o.pos = float4(0, 0, 0, 1);

                return o;
            }

            half4 InkFragment(InkVaryings i) : SV_Target
            {
                DitherClip(i.pos.xy);
                return half4(_InkColor.rgb, 1);
            }
            ENDCG
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            CGPROGRAM
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment
            #pragma target 3.5
            #pragma multi_compile_shadowcaster
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ _DITHER_FADE

            // Position and normal only, so meshes without texture coordinates, such as voxel rock, cast shadows too.
            struct ShadowAttributes
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                V2F_SHADOW_CASTER;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // The shadow caster macros read the vertex input as v.
            ShadowVaryings ShadowVertex(ShadowAttributes v)
            {
                ShadowVaryings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                return o;
            }

            float4 ShadowFragment(ShadowVaryings i) : SV_Target
            {
                DitherClip(i.pos.xy);
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }

    Fallback Off
}
