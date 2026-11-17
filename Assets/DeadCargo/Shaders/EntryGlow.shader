// Dead Cargo's entry glow: air heated by a ship's speed, as a shell of plasma round the hull.
// The shell is a sphere fitted round the ship and drawn from the inside (Cull Front), so from behind the ship its bow
// shows round the hull's outline as a glowing bow wave, and from the cockpit it fills the view ahead. Brightest where the
// shell faces the direction of travel, with streaks flowing back along the flanks. Cut into flat tone bands, white-hot to
// red, for the comic look. Added light only, so it brightens what is behind it and never darkens.
Shader "DeadCargo/EntryGlow"
{
    Properties
    {
        _HotColor ("Hot Colour", Color) = (1, 0.93, 0.72, 1)
        _WarmColor ("Warm Colour", Color) = (1, 0.48, 0.14, 1)
        _EdgeColor ("Edge Colour", Color) = (0.85, 0.12, 0.06, 1)
        _Brightness ("Brightness", Range(0, 4)) = 1.6
        _Bands ("Tone Bands", Range(2, 8)) = 4
        _BowPower ("Bow Tightness", Range(0.5, 6)) = 2
        _StreakAmount ("Streak Amount", Range(0, 1)) = 0.6
        _StreakScale ("Streak Scale", Range(1, 30)) = 9
        _FlowSpeed ("Flow Speed", Range(0, 20)) = 5
        _InsideDim ("Inside Brightness", Range(0, 1)) = 0.35
        [HideInInspector] _Glow ("Glow", Range(0, 1)) = 0
        [HideInInspector] _Travel ("Travel", Vector) = (0, 0, 1, 0)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent+10" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Blend One One
        ZWrite Off
        Cull Front

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _HotColor;
            fixed4 _WarmColor;
            fixed4 _EdgeColor;
            half _Brightness;
            half _Bands;
            half _BowPower;
            half _StreakAmount;
            half _StreakScale;
            half _FlowSpeed;
            half _InsideDim;
            half _Glow;
            float4 _Travel;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 objectPos : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.objectPos = v.vertex.xyz;
                return o;
            }

            float Hash(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float ValueNoise(float3 p)
            {
                float3 cell = floor(p);
                float3 f = frac(p);
                float3 u = f * f * (3.0 - 2.0 * f);

                float a = lerp(Hash(cell), Hash(cell + float3(1, 0, 0)), u.x);
                float b = lerp(Hash(cell + float3(0, 1, 0)), Hash(cell + float3(1, 1, 0)), u.x);
                float c = lerp(Hash(cell + float3(0, 0, 1)), Hash(cell + float3(1, 0, 1)), u.x);
                float d = lerp(Hash(cell + float3(0, 1, 1)), Hash(cell + float3(1, 1, 1)), u.x);
                return lerp(lerp(a, b, u.y), lerp(c, d, u.y), u.z);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.worldNormal);
                float3 travel = normalize(_Travel.xyz + float3(0, 0, 1e-5));
                float facing = dot(n, travel);
                float bow = pow(saturate(facing), _BowPower);

                float3 view = normalize(_WorldSpaceCameraPos - i.worldPos);
                float rim = 1.0 - abs(dot(n, view));

                // Streaks in the shell's own space, so they keep their size whatever the ship's, flowing tail-wards.
                float3 travelLocal = normalize(mul((float3x3)unity_WorldToObject, travel) + float3(0, 0, 1e-5));
                float along = dot(i.objectPos, travelLocal);
                float3 across = i.objectPos - along * travelLocal;
                float3 coordinates = across * _StreakScale + travelLocal * (along * 1.5 + _Time.y * _FlowSpeed);
                float streak = smoothstep(0.45, 0.85, ValueNoise(coordinates));
                float flanks = saturate(facing + 0.6) * (1.0 - bow);

                float heat = (bow + flanks * streak * _StreakAmount) * (0.35 + 0.65 * rim) * _Glow;

                // From the cockpit the whole bow fills the view, so it is held back.
                float3 eye = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1.0)).xyz;
                heat *= length(eye) < 0.5 ? _InsideDim : 1.0;

                float banded = floor(saturate(heat) * _Bands + 0.5) / _Bands;
                float3 color = lerp(_EdgeColor.rgb, _WarmColor.rgb, saturate(banded * 1.6));
                color = lerp(color, _HotColor.rgb, saturate((banded - 0.6) * 2.5));
                return fixed4(color * banded * _Brightness, 0);
            }
            ENDCG
        }
    }
}
