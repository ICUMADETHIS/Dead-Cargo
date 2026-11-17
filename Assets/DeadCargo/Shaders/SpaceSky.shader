// Dead Cargo's procedural space sky: deep space as an endless dark void.
// Layers, farthest first: a near-black backdrop stepping up faintly towards a galactic plane, distant galaxies, a
// faint far nebula, then the near nebula: muted gas lit softly from one side, cut through by dark dust lanes. Sparse
// stars sit on top, and the sun on top of everything: a distant star seen as a boiling sphere with a ragged corona,
// drawn wherever the scene's sun light comes from, so the sky, the ships and the rocks always agree on where the
// light is. Softness blends the steps from crisp cel bands to soft gas; ink and hatching give a comic look when
// wanted. Motion is pure maths on time: cloud edges creep, the far layer drifts, stars shimmer, the sun boils and turns.
// The nebula's shape is baked into a small two-channel map (see SpaceSkyBaker), so the costly noise runs once in
// the editor and the sky costs four reads of that map and a little maths per pixel.
// Hard edges are softened over exactly one pixel, so the sky stays crisp at any resolution.
Shader "DeadCargo/Space Sky"
{
    Properties
    {
        _SkyRotation ("Rotation", Vector) = (-18, 35, 28, 0)
        _LightDirection ("Light Direction", Vector) = (0.45, 0.5, -0.75, 0)
        _Exposure ("Exposure", Range(0, 4)) = 1

        _NebulaFlow ("Flow", Range(0, 0.05)) = 0.012
        _NebulaFlowSpeed ("Flow Speed", Range(0, 0.5)) = 0.03
        _FarDrift ("Far Drift", Range(-2, 2)) = 0.15
        _StarTwinkle ("Twinkle", Range(0, 1)) = 0.15
        _TwinkleSpeed ("Twinkle Speed", Range(0, 4)) = 0.8
        _SunBoil ("Sun Boil", Range(0, 2)) = 0.25
        _SunSpin ("Sun Spin", Range(-5, 5)) = 0.4

        _SunSize ("Sun Size", Range(0.2, 60)) = 28
        _SunCoreColor ("Hot", Color) = (1, 0.92, 0.6, 1)
        _SunSurfaceColor ("Surface", Color) = (0.98, 0.48, 0.12, 1)
        _SunSpotColor ("Cool", Color) = (0.42, 0.08, 0.03, 1)
        _SunBrightness ("Brightness", Range(0, 2)) = 1.1
        [IntRange] _SunSteps ("Tone Steps", Range(2, 8)) = 5
        _SunSoftness ("Softness", Range(0, 1)) = 0.55
        _SunGranules ("Granules", Range(1, 30)) = 9
        _SunVeins ("Veins", Range(0, 1)) = 0.25
        _SunLimbDarkening ("Limb Darkening", Range(0, 1)) = 0.65
        _SunCoronaColor ("Corona", Color) = (1, 0.45, 0.12, 1)
        _SunCorona ("Corona Strength", Range(0, 2)) = 0.4
        _SunCoronaSize ("Corona Size", Range(0.2, 40)) = 5
        _SunFlares ("Flares", Range(0, 1)) = 0.5
        _SunLightTint ("Light Colour", Range(0, 1)) = 0.3
        _SunLightsClouds ("Lights the Clouds", Range(0, 1)) = 1

        _VoidColor ("Void", Color) = (0.006, 0.008, 0.016, 1)
        _PlaneColor ("Galactic Plane", Color) = (0.02, 0.027, 0.045, 1)
        [IntRange] _BackdropBands ("Bands", Range(1, 8)) = 3
        _BackdropWidth ("Band Width", Range(0.05, 1)) = 0.5
        _BackdropWobble ("Band Wobble", Range(0, 0.5)) = 0.2

        _FarNebula ("Far Layer", Range(0, 1)) = 0.6
        _FarNebulaColor ("Far Colour", Color) = (0.032, 0.036, 0.06, 1)
        _FarNebulaThreshold ("Far Step", Range(0, 1)) = 0.42
        _FarNebulaRotation ("Far Rotation", Vector) = (40, 110, -30, 0)
        _GalaxyColor ("Galaxy Colour", Color) = (0.55, 0.52, 0.66, 1)
        _Galaxies ("Galaxies (cells, chance, radius px, brightness)", Vector) = (8, 0.2, 5, 0.35)

        _DustLanes ("Dust Lanes", Range(0, 1)) = 0.75
        _DustThreshold ("Dust Step", Range(0, 1)) = 0.55
        _DustRotation ("Dust Rotation", Vector) = (-60, 200, 45, 0)

        [NoScaleOffset] _NebulaMap ("Nebula Map", 2D) = "black" {}
        _NebulaOpacity ("Opacity", Range(0, 1)) = 0.85
        _NebulaSoftness ("Softness", Range(0, 0.25)) = 0.08
        _NebulaThresholds ("Step Thresholds", Vector) = (0.48, 0.62, 0.78, 0)
        _NebulaHueSplit ("Hue Split", Range(0, 1)) = 0.6
        _NebulaStarDimming ("Star Dimming", Range(0, 1)) = 0.5
        _NebulaColorA0 ("Faint", Color) = (0.05, 0.068, 0.095, 1)
        _NebulaColorA1 ("Mid", Color) = (0.09, 0.125, 0.165, 1)
        _NebulaColorA2 ("Core", Color) = (0.16, 0.21, 0.26, 1)
        _NebulaColorB0 ("Faint", Color) = (0.085, 0.06, 0.06, 1)
        _NebulaColorB1 ("Mid", Color) = (0.16, 0.11, 0.09, 1)
        _NebulaColorB2 ("Core", Color) = (0.34, 0.25, 0.2, 1)

        _InkColor ("Ink", Color) = (0.02, 0.02, 0.035, 1)
        _InkWidth ("Ink Width", Range(0, 4)) = 0
        _CloudLighting ("Cloud Lighting", Range(0, 1)) = 0.7
        _CloudRelief ("Relief", Range(0.002, 0.08)) = 0.05
        _ShadowTint ("Shadow Tint", Color) = (0.45, 0.45, 0.6, 1)
        _HighlightColor ("Highlight", Color) = (0.28, 0.31, 0.38, 1)
        _HighlightLevel ("Highlight Level", Range(0.005, 0.3)) = 0.1
        _Hatching ("Hatching", Range(0, 1)) = 0
        _HatchLines ("Hatch Lines", Range(100, 2000)) = 300
        _HatchWidth ("Hatch Width", Range(0.5, 3)) = 1.4

        _StarSeed ("Seed", Float) = 0
        _StarPlaneBoost ("Plane Boost", Range(0, 4)) = 2
        _StarSparkle ("Sparkle", Range(0, 1)) = 0.3
        _StarCoolColor ("Cool", Color) = (0.76, 0.86, 1, 1)
        _StarWarmColor ("Warm", Color) = (1, 0.9, 0.76, 1)
        _StarTints ("Tint Chances (cool, warm)", Vector) = (0.12, 0.08, 0, 0)
        _StarSmall ("Small Stars (cells, chance, radius px, brightness)", Vector) = (90, 0.3, 0.7, 0.4)
        _StarMedium ("Medium Stars (cells, chance, radius px, brightness)", Vector) = (40, 0.25, 1.1, 0.75)
        _StarLarge ("Large Stars (cells, chance, radius px, brightness)", Vector) = (14, 0.3, 1.8, 1.4)

        _NebulaSeed ("Seed", Float) = 0
        _NebulaScale ("Scale", Range(0.5, 8)) = 3.2
        [IntRange] _NebulaOctaves ("Detail", Range(1, 6)) = 5
        _NebulaSwirl ("Swirl", Range(0, 2)) = 1
        _NebulaBandWidth ("Band Width", Range(0.05, 1)) = 0.25
        _NebulaBandFocus ("Band Focus", Range(0, 1)) = 0.92
        _NebulaHueScale ("Hue Scale", Range(0.25, 4)) = 1.2
        [HideInInspector] _NebulaBakeHash ("Bake Hash", Integer) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma target 3.5

            #include "UnityCG.cginc"
            #include "UnityLightingCommon.cginc"

            sampler2D _NebulaMap;
            float4 _NebulaMap_TexelSize;

            float4 _SkyRotation;
            float4 _LightDirection;
            float _Exposure;

            float _NebulaFlow;
            float _NebulaFlowSpeed;
            float _FarDrift;
            float _StarTwinkle;
            float _TwinkleSpeed;
            float _SunBoil;
            float _SunSpin;

            float _SunSize;
            float4 _SunCoreColor;
            float4 _SunSurfaceColor;
            float4 _SunSpotColor;
            float _SunBrightness;
            float _SunSteps;
            float _SunSoftness;
            float _SunGranules;
            float _SunVeins;
            float _SunLimbDarkening;
            float4 _SunCoronaColor;
            float _SunCorona;
            float _SunCoronaSize;
            float _SunFlares;
            float _SunLightTint;
            float _SunLightsClouds;

            // The darkness of space, set by Space Lighting (Scripts/Visuals/SpaceLighting.cs). y: how much darker the
            // sky is, 0 to 1. Zero, as with no Space Lighting in the scene, leaves the sky unchanged.
            float4 _SpaceShade;

            float4 _VoidColor;
            float4 _PlaneColor;
            float _BackdropBands;
            float _BackdropWidth;
            float _BackdropWobble;

            float _FarNebula;
            float4 _FarNebulaColor;
            float _FarNebulaThreshold;
            float4 _FarNebulaRotation;
            float4 _GalaxyColor;
            float4 _Galaxies;

            float _DustLanes;
            float _DustThreshold;
            float4 _DustRotation;

            float _NebulaOpacity;
            float _NebulaSoftness;
            float4 _NebulaThresholds;
            float _NebulaHueSplit;
            float _NebulaStarDimming;
            float4 _NebulaColorA0;
            float4 _NebulaColorA1;
            float4 _NebulaColorA2;
            float4 _NebulaColorB0;
            float4 _NebulaColorB1;
            float4 _NebulaColorB2;

            float4 _InkColor;
            float _InkWidth;
            float _CloudLighting;
            float _CloudRelief;
            float4 _ShadowTint;
            float4 _HighlightColor;
            float _HighlightLevel;
            float _Hatching;
            float _HatchLines;
            float _HatchWidth;

            float _StarSeed;
            float _StarPlaneBoost;
            float _StarSparkle;
            float4 _StarCoolColor;
            float4 _StarWarmColor;
            float4 _StarTints;
            float4 _StarSmall;
            float4 _StarMedium;
            float4 _StarLarge;

            static const float QuarterPi = 0.78539816;
            static const float TwoPi = 6.28318531;

            // Each star keeps this fraction of its cell clear on every side, so it never crosses into a neighbour
            // and one cell lookup per layer is enough.
            static const float StarMargin = 0.25;

            // Equi-angular cells are narrowest along a face edge, at this fraction of their width at the face centre.
            static const float NarrowestCell = 0.7071;

            // Pixel sizes are given at this screen height and scale with it.
            static const float ReferenceHeight = 1080.0;

            static const float MinWidth = 1e-5;

            // The far layer's second step sits this far above its first, in this brighter shade.
            static const float FarCoreOffset = 0.16;
            static const float FarCoreBoost = 1.4;

            // Ink is heavier on a cloud's shadowed side, and the inner steps get a lighter line.
            static const float InkLitWeight = 0.75;
            static const float InkShadowWeight = 1.35;
            static const float InnerInkWeight = 0.9;

            static const float HighlightMix = 0.5;

            // Hatching fades out where its strokes would be closer together than this, in pixels.
            static const float MinHatchSpacing = 3.0;

            // Crossing strokes start this many highlight levels into the shadow, so only the darkest parts get them.
            static const float CrossHatchDepth = 2.0;

            // A sparkle's arms reach this many star radii at full strength.
            static const float SparkleReach = 3.0;

            // Galaxies: the core's share of the radius, and the halo's brightness relative to the core.
            static const float GalaxyCore = 0.35;
            static const float GalaxyHalo = 0.45;

            // Real galaxies are hidden behind the galactic plane's dust. This share of them is missing along it.
            static const float GalaxyAvoidance = 0.85;

            // Flow waves across the full range of the hue field.
            static const float FlowWaves = 4.0;

            // Lighting softens at this share of the nebula's softness, so light and shadow stay a little firmer.
            static const float ReliefSoftness = 0.5;

            // How much a dust lane darkens what lies behind it.
            static const float DustDarkening = 0.8;

            // Below this brightness the scene has no sun light, and no sun is drawn.
            static const float MinSunLight = 0.001;

            // Narrowest tone step edge on the sun's surface, as a share of a step. Its pixels sit inside a dynamic
            // branch, where screen derivatives are not available, so softness has a floor instead.
            static const float SunMinStepWidth = 0.04;

            // The surface: how far the slow field bends the gas, and how thin the bright veins are.
            static const float SunBend = 0.5;
            static const float SunVeinSharpness = 10.0;
            static const float SunFineGrain = 0.08;

            // A tight halo hugs the limb, out to this share of Corona Size, at this strength.
            static const float SunHaloReach = 0.12;
            static const float SunHaloStrength = 0.8;

            // The corona fades by this rate over its reach, which varies from short to long round the rim in fine
            // streamers, so it is ragged rather than round. It ends at Fade Out times the reach.
            static const float SunCoronaFalloff = 3.5;
            static const float SunStreamerShort = 0.7;
            static const float SunStreamerLong = 1.3;
            static const float SunStreamerDetail = 9.0;
            static const float SunCoronaFadeOut = 1.6;

            // Flame tongues: how many round the rim, the share of noise that becomes flame, and how high they rise as
            // a share of Corona Size.
            static const float SunFlameDetail = 5.0;
            static const float SunFlameCut = 0.35;
            static const float SunFlameHeight = 0.35;

            // Time wraps before it grows large enough to cost the noise its precision.
            static const float SunTimeWrap = 1000.0;

            struct Attributes
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float3 direction : TEXCOORD0;
                float3 farDirection : TEXCOORD1;
                float3 light : TEXCOORD2;
                float3 dustDirection : TEXCOORD3;

                // Towards the sun in sky space, and 1 when the scene has a sun light, else 0.
                float4 sun : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // One cube face of the sky, for star and galaxy placement.
            struct StarFace
            {
                float2 coord;
                float3 axis;
                float3 tangent;
                float3 bitangent;
                uint index;
            };

            // A point in the sky for one cell of a layer, with the random values that describe it.
            struct CellPoint
            {
                float3 direction;
                float chance;
                float pick;
                float tint;
                float shade;
                float spin;
                float cells;
            };

            // The same rotation as Quaternion.Euler: Z, then X, then Y.
            float3x3 EulerMatrix(float3 degrees)
            {
                float3 s;
                float3 c;
                sincos(radians(degrees), s, c);
                float3x3 rx = float3x3(1, 0, 0, 0, c.x, -s.x, 0, s.x, c.x);
                float3x3 ry = float3x3(c.y, 0, s.y, 0, 1, 0, -s.y, 0, c.y);
                float3x3 rz = float3x3(c.z, -s.z, 0, s.z, c.z, 0, 0, 0, 1);
                return mul(ry, mul(rx, rz));
            }

            // PCG3D (Jarzynski and Olano, 2020): three well mixed values from three inputs.
            uint3 Hash3(uint3 v)
            {
                v = v * 1664525u + 1013904223u;
                v.x += v.y * v.z;
                v.y += v.z * v.x;
                v.z += v.x * v.y;
                v ^= v >> 16u;
                v.x += v.y * v.z;
                v.y += v.z * v.x;
                v.z += v.x * v.y;
                return v;
            }

            // A hard step at the threshold, softened over one pixel.
            float CrispStep(float threshold, float value, float width)
            {
                return saturate((value - threshold) / width + 0.5);
            }

            // Coverage of a line of the given width, both in pixels, at a distance in pixels from its centre.
            float LineCoverage(float offset, float width)
            {
                return saturate(width * 0.5 - offset + 0.5) * saturate(width);
            }

            // Quantises 0..1 into flat bands, softening each edge over one pixel. Returns 0..1.
            float Posterize(float t, float bands)
            {
                float x = t * bands;
                float width = max(fwidth(x), MinWidth);
                float whole = floor(x);
                float part = x - whole;
                float level = whole + saturate((part - 1) / width + 0.5) - saturate(0.5 - part / width);
                return saturate(level / max(bands - 1, 1));
            }

            // Octahedral map coordinates in -1..1 for a unit direction, with sky-space Y as the pole.
            // Must match OctahedralMapping.Encode in the baker.
            float2 OctahedralEncode(float3 d)
            {
                float2 p = d.xz / (abs(d.x) + abs(d.y) + abs(d.z));
                if (d.y < 0)
                {
                    float2 side = step(0, p) * 2 - 1;
                    p = (1 - abs(p.yx)) * side;
                }

                return p;
            }

            // Density in r and hue in g. The map has a one-texel border holding the far side of each fold,
            // so filtering across the folds is seamless.
            float2 SampleNebula(float3 d)
            {
                float2 p = OctahedralEncode(d) * 0.5 + 0.5;
                float2 uv = p * (1 - 2 * _NebulaMap_TexelSize.xy) + _NebulaMap_TexelSize.xy;
                return tex2Dlod(_NebulaMap, float4(uv, 0, 0)).rg;
            }

            // How much the cloud thins a short step towards the light: positive on the side facing it, negative on
            // the far side. Read as a height map, this lights each cloud like a solid shape.
            float CloudRelief(float3 d, float3 light, float density)
            {
                float3 towardsLight = light - d * dot(d, light);
                float3 probe = normalize(d + towardsLight / max(length(towardsLight), MinWidth) * _CloudRelief);
                return density - SampleNebula(probe).r;
            }

            // Diagonal strokes anchored to the sky, so they never swim as the view turns. They fill the shadowed side
            // of each cloud and taper to nothing towards the light; the darkest parts get a second, crossing set.
            float Hatching(float3 d, float relief, float pixelScale)
            {
                float longitude = atan2(d.z, d.x) / TwoPi;
                float latitude = asin(clamp(d.y, -1, 1)) / TwoPi;

                // Longitude wraps once behind the sky. A whole number of strokes per turn keeps them continuous there,
                // and its derivatives are unwrapped so the stroke width stays even.
                float lines = round(_HatchLines);
                float2 longitudeSlope = float2(ddx(longitude), ddy(longitude));
                longitudeSlope -= round(longitudeSlope);
                float2 latitudeSlope = float2(ddx(latitude), ddy(latitude));

                float2 strokes = float2(longitude + latitude, longitude - latitude) * lines;
                float2 width = float2(
                    abs(longitudeSlope.x + latitudeSlope.x) + abs(longitudeSlope.y + latitudeSlope.y),
                    abs(longitudeSlope.x - latitudeSlope.x) + abs(longitudeSlope.y - latitudeSlope.y)) * lines;
                width = max(width, MinWidth);

                float level = max(_HighlightLevel, MinWidth);
                float primary = saturate(-relief / level);
                float crossing = saturate((-relief - CrossHatchDepth * level) / level);
                float2 stroke = _HatchWidth * pixelScale * float2(primary, crossing);

                float2 gap = abs(frac(strokes) - 0.5) / width;
                float2 coverage = float2(LineCoverage(gap.x, stroke.x), LineCoverage(gap.y, stroke.y));
                coverage *= saturate((1 / width - MinHatchSpacing) / MinHatchSpacing);
                return max(coverage.x, coverage.y);
            }

            StarFace GetStarFace(float3 d)
            {
                StarFace face;
                float3 a = abs(d);
                float2 uv;
                if (a.x >= a.y && a.x >= a.z)
                {
                    face.index = d.x >= 0 ? 0u : 1u;
                    face.axis = float3(d.x >= 0 ? 1 : -1, 0, 0);
                    face.tangent = float3(0, 0, 1);
                    face.bitangent = float3(0, 1, 0);
                    uv = d.zy / a.x;
                }
                else if (a.y >= a.z)
                {
                    face.index = d.y >= 0 ? 2u : 3u;
                    face.axis = float3(0, d.y >= 0 ? 1 : -1, 0);
                    face.tangent = float3(1, 0, 0);
                    face.bitangent = float3(0, 0, 1);
                    uv = d.xz / a.y;
                }
                else
                {
                    face.index = d.z >= 0 ? 4u : 5u;
                    face.axis = float3(0, 0, d.z >= 0 ? 1 : -1);
                    face.tangent = float3(1, 0, 0);
                    face.bitangent = float3(0, 1, 0);
                    uv = d.xy / a.z;
                }

                // Equi-angular coordinates spread cells far more evenly over the sphere than plain cube coordinates.
                face.coord = atan(uv) / QuarterPi;
                return face;
            }

            // The point a layer places in this pixel's cell. layer.x is cells per face edge and layer.y the chance
            // of a point per cell. Cells away from a face centre cover less sky, so their chance is scaled down.
            CellPoint GetCellPoint(StarFace face, float4 layer, uint stream)
            {
                CellPoint result;
                result.cells = max(layer.x, 1);
                float2 cell = min(floor((face.coord * 0.5 + 0.5) * result.cells), result.cells - 1);
                uint3 hash = Hash3(uint3(uint2(cell), (uint)_StarSeed * 64u + stream * 8u + face.index));
                float4 random = float4(hash.x >> 16, hash.x & 0xFFFFu, hash.y >> 16, hash.y & 0xFFFFu) / 65535.0;
                result.pick = random.x;
                result.tint = random.w;
                result.shade = (hash.z >> 16) / 65535.0;
                result.spin = (hash.z & 0xFFFFu) / 65535.0;

                float2 centre = (cell + StarMargin + (1 - 2 * StarMargin) * random.yz) / result.cells * 2 - 1;
                float2 cubeCoord = tan(centre * QuarterPi);
                result.direction = normalize(face.axis + cubeCoord.x * face.tangent + cubeCoord.y * face.bitangent);

                float2 squared = cubeCoord * cubeCoord;
                float area = (1 + squared.x) * (1 + squared.y) * pow(1 + squared.x + squared.y, -1.5);
                result.chance = layer.y * area;
                return result;
            }

            // Closeness to the galactic plane, 1 on it and 0 beyond the backdrop's band width.
            float PlaneCloseness(float3 d)
            {
                return saturate(1 - abs(d.y) / _BackdropWidth);
            }

            // An offset on the sky in screen pixels, from the sky-space change across one pixel in x and y.
            float2 ScreenOffset(float3 offset, float3 dX, float3 dY)
            {
                return float2(dot(offset, dX) / max(dot(dX, dX), MinWidth * MinWidth),
                              dot(offset, dY) / max(dot(dY, dY), MinWidth * MinWidth));
            }

            // One layer of stars: at most one per cell. layer is (cells per face edge, chance per cell, radius in
            // pixels, brightness). Returns the star colour in rgb and its coverage of this pixel in a.
            float4 StarLayer(StarFace face, float3 d, float3 dX, float3 dY, float pixelAngle, float pixelScale,
                float4 layer, uint stream, float sparkle)
            {
                CellPoint star = GetCellPoint(face, layer, stream);

                // Stars thicken towards the galactic plane.
                float chance = star.chance * lerp(1 / (1 + _StarPlaneBoost), 1, PlaneCloseness(star.direction));
                if (star.pick >= chance)
                    return 0;

                float maxRadius = StarMargin * NarrowestCell * 2 * QuarterPi / star.cells;
                float radius = min(layer.z * pixelAngle * pixelScale, maxRadius);

                // A star smaller than a pixel is drawn a pixel wide and dimmed to match, so it holds steady
                // instead of flickering as the view turns.
                float drawn = max(radius, 0.75 * pixelAngle);
                float energy = (radius * radius) / (drawn * drawn);
                float coverage = saturate((drawn - length(d - star.direction)) / pixelAngle + 0.5);

                // Each star twinkles on its own beat and speed.
                float beat = _Time.y * _TwinkleSpeed * lerp(0.6, 1.4, star.spin) + star.spin * TwoPi;
                float twinkle = lerp(1, 0.5 + 0.5 * sin(beat), _StarTwinkle);

                // Bright stars get four screen-aligned points, like a comic-book glint, pulsing with the twinkle.
                if (sparkle > 0)
                {
                    float2 offset = abs(ScreenOffset(d - star.direction, dX, dY));
                    float radiusPixels = drawn / pixelAngle;
                    float armLength = min(radiusPixels * (1 + SparkleReach * sparkle * twinkle), maxRadius / pixelAngle);
                    float armWidth = max(0.6, radiusPixels * 0.3);
                    float armX = saturate((1 - offset.x / armLength - offset.y / armWidth) * armWidth + 0.5);
                    float armY = saturate((1 - offset.y / armLength - offset.x / armWidth) * armWidth + 0.5);
                    coverage = max(coverage, max(armX, armY));
                }

                float3 tint = star.tint < _StarTints.x ? _StarCoolColor.rgb
                    : star.tint < _StarTints.x + _StarTints.y ? _StarWarmColor.rgb : float3(1, 1, 1);
                return float4(tint * (layer.w * lerp(0.6, 1, star.shade) * energy * twinkle), coverage);
            }

            // Distant galaxies: small tilted ellipses with a bright core, flat and un-inked like everything far away.
            float4 GalaxyLayer(StarFace face, float3 d, float3 dX, float3 dY, float pixelAngle, float pixelScale)
            {
                CellPoint galaxy = GetCellPoint(face, _Galaxies, 3u);
                float chance = galaxy.chance * (1 - GalaxyAvoidance * PlaneCloseness(galaxy.direction));
                if (galaxy.pick >= chance)
                    return 0;

                float maxRadius = StarMargin * NarrowestCell * 2 * QuarterPi / galaxy.cells / pixelAngle;
                float major = min(_Galaxies.z * pixelScale * lerp(0.6, 1.4, galaxy.shade), maxRadius);
                float minor = major * lerp(0.25, 0.75, galaxy.tint);

                float2 offset = ScreenOffset(d - galaxy.direction, dX, dY);
                float s;
                float c;
                sincos(galaxy.spin * UNITY_PI, s, c);
                offset = float2(c * offset.x - s * offset.y, s * offset.x + c * offset.y);
                float reach = length(offset / float2(major, minor));

                float halo = saturate((1 - reach) * minor + 0.5);
                float core = saturate((GalaxyCore - reach) * minor + 0.5);
                return float4(_GalaxyColor.rgb * (_Galaxies.w * lerp(GalaxyHalo, 1, core)), halo);
            }

            float3 Composite(float3 color, float4 layer, float visibility)
            {
                return lerp(color, layer.rgb, layer.a * visibility);
            }

            // Gradient noise for the sun, smooth and about -1 to 1. It only runs on the pixels round the sun.
            float3 SunGradient(float3 cell)
            {
                float3 h = float3(dot(cell, float3(127.1, 311.7, 74.7)), dot(cell, float3(269.5, 183.3, 246.1)),
                    dot(cell, float3(113.5, 271.9, 124.6)));
                return frac(sin(h) * 43758.5453) * 2 - 1;
            }

            float SunNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = p - i;
                float3 u = f * f * f * (f * (f * 6 - 15) + 10);
                float n000 = dot(SunGradient(i), f);
                float n100 = dot(SunGradient(i + float3(1, 0, 0)), f - float3(1, 0, 0));
                float n010 = dot(SunGradient(i + float3(0, 1, 0)), f - float3(0, 1, 0));
                float n110 = dot(SunGradient(i + float3(1, 1, 0)), f - float3(1, 1, 0));
                float n001 = dot(SunGradient(i + float3(0, 0, 1)), f - float3(0, 0, 1));
                float n101 = dot(SunGradient(i + float3(1, 0, 1)), f - float3(1, 0, 1));
                float n011 = dot(SunGradient(i + float3(0, 1, 1)), f - float3(0, 1, 1));
                float n111 = dot(SunGradient(i + float3(1, 1, 1)), f - float3(1, 1, 1));
                float4 alongX = lerp(float4(n000, n010, n001, n011), float4(n100, n110, n101, n111), u.x);
                float2 alongY = lerp(alongX.xz, alongX.yw, u.y);

                // Gradient noise rarely passes about 0.6, so it is scaled to use the whole range.
                return lerp(alongY.x, alongY.y, u.z) * 1.6;
            }

            // Four layers, the last a faint fine grain, so the surface holds detail when the star fills much of the view.
            float SunFractal(float3 p)
            {
                return SunNoise(p) * 0.55 + SunNoise(p * 2.1 + 3.7) * 0.3 + SunNoise(p * 4.3 + 7.1) * 0.15 +
                    SunNoise(p * 8.9 + 1.9) * SunFineGrain;
            }

            // Steps a tone of 0 to 1 into Tone Steps flat tones; Softness widens each edge into a blend, as the
            // nebula's Softness does.
            float SunTone(float tone)
            {
                float steps = max(_SunSteps, 2) - 1;
                float x = saturate(tone) * steps;
                float whole = floor(x);
                float width = lerp(SunMinStepWidth, 1, _SunSoftness);
                return (whole + saturate((x - whole - 0.5) / width + 0.5)) / steps;
            }

            // The sun's surface and corona, for the pixels near it only. Angles are in radians; disc is the share of
            // the pixel the sphere covers.
            float3 SunNear(float3 color, float3 d, float3 towardsSun, float angle, float radius, float coronaSize, float disc)
            {
                // Where on the face of the sun this pixel looks: across the disc, -1 to 1, and towards the viewer.
                float3 side = abs(towardsSun.y) < 0.99 ? float3(0, 1, 0) : float3(1, 0, 0);
                float3 right = normalize(cross(side, towardsSun));
                float3 up = cross(towardsSun, right);
                float2 across = float2(dot(d, right), dot(d, up)) / max(sin(radius), MinWidth);
                float time = fmod(_Time.y, SunTimeWrap);
                float boil = time * _SunBoil;
                float3 light = _LightColor0.rgb;
                float3 tint = lerp(1, light / max(max(light.r, max(light.g, light.b)), MinWidth), _SunLightTint);

                // The surface, only where the disc covers the pixel: most of this region is corona.
                float3 face = 0;
                UNITY_BRANCH
                if (disc > 0)
                {
                    // The sphere turns about its own axis.
                    float facing = sqrt(saturate(1 - dot(across, across)));
                    float spinSin;
                    float spinCos;
                    sincos(radians(time * _SunSpin), spinSin, spinCos);
                    float3 surface = float3(across.x * spinCos + facing * spinSin, across.y, facing * spinCos - across.x * spinSin);

                    // A slow field bends the surface, so the cells churn and stream rather than slide past.
                    float3 p = surface * _SunGranules;
                    float3 bend = float3(SunNoise(p * 0.5 + float3(0, boil, 0)), SunNoise(p * 0.5 + float3(5.2, -boil, 1.3)),
                        SunNoise(p * 0.5 + float3(boil, 2.8, 9.4)));
                    float3 gas = p + bend * SunBend;
                    float cells = SunFractal(gas + float3(0, 0, boil * 0.5));
                    float veins = pow(saturate(1 - abs(SunNoise(gas * 1.7 - boil * 0.3))), SunVeinSharpness) * _SunVeins;
                    float tone = SunTone((0.5 + cells * 0.6 + veins * 0.5) * lerp(1 - _SunLimbDarkening, 1, sqrt(facing)));

                    face = lerp(lerp(_SunSpotColor.rgb, _SunSurfaceColor.rgb, saturate(tone * 2)), _SunCoreColor.rgb, saturate(tone * 2 - 1));
                    face *= tint * _SunBrightness;
                }

                // Corona: a tight halo hugging the limb, a long soft glow whose reach varies in fine streamers round
                // the rim, and a few tongues of flame rising off the limb.
                float height = max(angle - radius, 0) / coronaSize;
                float2 rim = across / max(length(across), MinWidth);
                float halo = pow(saturate(1 - height / SunHaloReach), 2) * SunHaloStrength;
                float streamers = SunNoise(float3(rim * SunStreamerDetail, boil * 0.2)) * 0.5 + 0.5;
                float reach = lerp(SunStreamerShort, SunStreamerLong, streamers);
                float glow = exp(-height / reach * SunCoronaFalloff) * saturate(1 - height / (reach * SunCoronaFadeOut));
                float flame = SunNoise(float3(rim * SunFlameDetail, height * 2.5 - boil * 0.6));
                float flames = saturate((flame - SunFlameCut) * 3) * saturate(1 - height / SunFlameHeight) * _SunFlares;
                float3 corona = (_SunCoronaColor.rgb * (glow + halo) + lerp(_SunCoronaColor.rgb, _SunSurfaceColor.rgb, 0.5) * flames) *
                    tint * _SunCorona;

                return lerp(color + corona, face, disc);
            }

            // The sun: a distant star seen as a sphere, where the scene's light comes from. Its surface boils: cells
            // of hot gas churn and turn, cut by bright veins, painted in a few soft tones like the nebula. It darkens
            // and reddens towards the limb, which makes it read as round. Round it, a ragged corona of streamers and
            // tongues of flame. The limb is softened over one pixel; everything else is soft by nature, so the costly
            // part runs only near the sun, in a branch where screen derivatives are not needed.
            float3 SunLayer(float3 color, float3 d, float4 sun, float pixelAngle)
            {
                float angle = acos(clamp(dot(d, sun.xyz), -1, 1));
                float radius = radians(_SunSize) * 0.5;
                float disc = saturate((radius - angle) / pixelAngle + 0.5) * sun.w;
                float coronaSize = max(radians(_SunCoronaSize), MinWidth);

                UNITY_BRANCH
                if (sun.w > 0 && angle <= radius + coronaSize * SunStreamerLong * SunCoronaFadeOut)
                    color = SunNear(color, d, sun.xyz, angle, radius, coronaSize, disc);

                return color;
            }

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.position = UnityObjectToClipPos(input.vertex);

                // World to sky space is the inverse rotation. A row vector times the matrix applies its transpose.
                float3x3 sky = EulerMatrix(_SkyRotation.xyz);
                output.direction = mul(input.vertex.xyz, sky);

                // The far layer reads the same map turned another way, so it never lines up with the near clouds.
                // It also drifts slowly about the pole. Moving against the near clouds, it reads as far deeper.
                float drift = fmod(_Time.y * _FarDrift, 360);
                output.farDirection = mul(output.direction, EulerMatrix(_FarNebulaRotation.xyz + float3(0, drift, 0)));

                // The sun is where the scene's main directional light comes from: the Sun Source in the Lighting
                // settings, or else the brightest directional light. The clouds are lit from it by Lights the Clouds,
                // or from the authored Light Direction.
                float3 towardsSun = _WorldSpaceLightPos0.xyz;
                float hasSun = length(towardsSun) > 0.5 && max(_LightColor0.r, max(_LightColor0.g, _LightColor0.b)) > MinSunLight;
                towardsSun = hasSun ? normalize(towardsSun) : float3(0, 1, 0);
                output.sun = float4(mul(towardsSun, sky), hasSun);

                float3 authored = _LightDirection.xyz / max(length(_LightDirection.xyz), MinWidth);
                float3 light = lerp(authored, towardsSun, _SunLightsClouds * hasSun);
                output.light = mul(light / max(length(light), MinWidth), sky);

                // Dust lanes read the map turned a third way, so they cut across the clouds rather than follow them.
                output.dustDirection = mul(output.direction, EulerMatrix(_DustRotation.xyz));
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float3 d = normalize(input.direction);
                float3 dX = ddx(d);
                float3 dY = ddy(d);
                float pixelAngle = max(max(length(dX), length(dY)), MinWidth);
                float pixelScale = _ScreenParams.y / ReferenceHeight;
                StarFace face = GetStarFace(d);

                float2 nebula = SampleNebula(d);
                float stillDensity = nebula.r;
                float hueField = nebula.g;

                // Flow: a slow wave across the hue field nudges the density, so cloud edges creep like drifting gas.
                // Lighting reads the still density, so light and shadow hold steady while the edges move.
                float density = stillDensity + sin((_Time.y * _NebulaFlowSpeed + hueField * FlowWaves) * TwoPi) * _NebulaFlow;

                // Backdrop: flat bands stepping up towards the galactic plane, bent by the hue field.
                float planeDistance = abs(d.y) + (hueField - 0.5) * _BackdropWobble;
                float band = Posterize(saturate(1 - planeDistance / _BackdropWidth), _BackdropBands);
                float3 color = lerp(_VoidColor.rgb, _PlaneColor.rgb, band);

                // Distant galaxies, then the far nebula: flat, faint and un-inked, so they read as far away.
                color = Composite(color, GalaxyLayer(face, d, dX, dY, pixelAngle, pixelScale), 1);
                float far = SampleNebula(normalize(input.farDirection)).r;
                float farWidth = max(fwidth(far), _NebulaSoftness);
                float farCore = CrispStep(_FarNebulaThreshold + FarCoreOffset, far, farWidth);
                float3 farColor = _FarNebulaColor.rgb * lerp(1, FarCoreBoost, farCore);
                color = lerp(color, farColor, CrispStep(_FarNebulaThreshold, far, farWidth) * _FarNebula);

                // Near nebula: three steps in one of two palettes. Softness widens each step from one pixel into a
                // gradient, turning crisp cel bands into soft gas.
                float densityWidth = max(fwidth(density), MinWidth);
                float hueWidth = max(fwidth(hueField), MinWidth);
                float hue = CrispStep(_NebulaHueSplit, hueField, max(hueWidth, _NebulaSoftness));
                float3 steps = saturate((density - _NebulaThresholds.xyz) / max(densityWidth, _NebulaSoftness) + 0.5);
                float3 cloud = lerp(lerp(_NebulaColorA0.rgb, _NebulaColorB0.rgb, hue), lerp(_NebulaColorA1.rgb, _NebulaColorB1.rgb, hue), steps.y);
                cloud = lerp(cloud, lerp(_NebulaColorA2.rgb, _NebulaColorB2.rgb, hue), steps.z);

                // Two-tone light: a lit side, a tinted shadow side and a highlight on the edge facing the light.
                float relief = CloudRelief(d, input.light, stillDensity);
                float reliefWidth = max(fwidth(relief), _NebulaSoftness * ReliefSoftness);
                float lit = CrispStep(0, relief, reliefWidth);
                float highlight = CrispStep(_HighlightLevel, relief, reliefWidth);
                float3 shaded = lerp(cloud * _ShadowTint.rgb, cloud, lit);
                shaded = lerp(shaded, _HighlightColor.rgb, highlight * HighlightMix);
                cloud = lerp(cloud, shaded, _CloudLighting);
                float inside = steps.x * _NebulaOpacity;
                color = lerp(color, cloud, inside);

                // Ink: an outline round each cloud, heavier on its shadowed side, and lines between steps and
                // between the two palettes.
                float shadow = (1 - lit) * _CloudLighting;
                float inkWidth = _InkWidth * pixelScale * lerp(InkLitWeight, InkShadowWeight, shadow);
                float3 contour = abs(density - _NebulaThresholds.xyz) / densityWidth;
                float ink = LineCoverage(contour.x, inkWidth);
                ink = max(ink, LineCoverage(contour.y, inkWidth * InnerInkWeight));
                ink = max(ink, LineCoverage(contour.z, inkWidth * InnerInkWeight));
                ink = max(ink, LineCoverage(abs(hueField - _NebulaHueSplit) / hueWidth, inkWidth * InnerInkWeight) * steps.x);
                ink *= _NebulaOpacity;
                float hatch = Hatching(d, relief, pixelScale) * steps.x * _Hatching * _CloudLighting;
                color = lerp(color, _InkColor.rgb, max(ink, hatch));

                // Dust lanes: dark filaments in front of everything along the galactic plane. They hide the gas and
                // stars behind them, which layers the sky in depth.
                float dust = SampleNebula(normalize(input.dustDirection)).r;
                float lane = CrispStep(_DustThreshold, dust, max(fwidth(dust), _NebulaSoftness)) * PlaneCloseness(d) * _DustLanes;
                color *= 1 - lane * DustDarkening;

                // Stars: three layers of fixed sizes, dimmed behind the nebula and the dust. The large ones glint.
                float visibility = (1 - inside * _NebulaStarDimming) * (1 - lane);
                color = Composite(color, StarLayer(face, d, dX, dY, pixelAngle, pixelScale, _StarSmall, 0u, 0), visibility);
                color = Composite(color, StarLayer(face, d, dX, dY, pixelAngle, pixelScale, _StarMedium, 1u, 0), visibility);
                color = Composite(color, StarLayer(face, d, dX, dY, pixelAngle, pixelScale, _StarLarge, 2u, _StarSparkle), visibility);

                // Space Lighting darkens the sky, not the sun, which sits in front of everything and keeps its own
                // brightness whatever the sky's exposure.
                color = SunLayer(color * _Exposure * (1 - _SpaceShade.y), d, input.sun, pixelAngle);
                return half4(color, 1);
            }
            ENDCG
        }
    }

    CustomEditor "DeadCargo.EditorTools.Visuals.SpaceSkyShaderGUI"
    Fallback Off
}
