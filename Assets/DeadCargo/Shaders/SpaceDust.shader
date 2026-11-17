// Dead Cargo's space dust: faint flecks of rock filling the space around the camera, smeared into streaks by the
// camera's motion. Seeing things stream past is what makes space feel vast and the ship feel fast.
// One static mesh holds every mote at a fixed spot in a unit cube. The vertex shader repeats that cube through space
// and draws the copy of each mote nearest the camera, so the field never runs out and costs no CPU work.
// A hint of the comic look the rocks and ships have: each fleck is in one of two flat tones, catching the sun or in
// shadow, with a thin ink rim. Streaks keep a fixed pixel width, with edges softened over one pixel.
Shader "DeadCargo/Space Dust"
{
    Properties
    {
        _Color ("Lit Colour", Color) = (0.82, 0.76, 0.66, 1)
        _ShadowColor ("Shadow Colour", Color) = (0.34, 0.35, 0.42, 1)
        _LitShare ("Lit Share", Range(0, 1)) = 0.65
        _Brightness ("Brightness", Range(0, 4)) = 0.7
        _Amount ("Amount", Range(0, 1)) = 0.2
        _FieldAmount ("Amount In Asteroid Fields", Range(0, 1)) = 0.7
        _InkColor ("Ink", Color) = (0.03, 0.025, 0.04, 1)
        _InkWidth ("Ink Width (px at 1080p)", Range(0, 2)) = 0.8
        _InkStrength ("Ink Strength", Range(0, 1)) = 0.6
        _FieldSize ("Field Size (m)", Range(20, 1000)) = 160
        _Width ("Width (px at 1080p)", Range(0.5, 4)) = 1.4
        _StreakTime ("Streak Time (s)", Range(0, 0.3)) = 0.06
        _MaxStreak ("Max Streak (m)", Range(1, 200)) = 40
        _MaxStreakPixels ("Max Streak (px at 1080p)", Range(10, 1000)) = 260
        _NearFade ("Near Fade (m)", Range(0.5, 50)) = 6
        _FarFade ("Far Fade Start", Range(0.1, 1)) = 0.55
        _RestVisibility ("Rest Visibility", Range(0, 1)) = 0.15
        _FullSpeed ("Full Speed (m/s)", Range(1, 500)) = 80
        [HideInInspector] _Velocity ("Velocity", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Travel ("Travel", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        // Premultiplied alpha, so a fleck can both brighten the view and lay a dark rim over it.
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
            float _LitShare;
            float _Brightness;
            float _Amount;
            float _FieldAmount;

            // How thick the asteroid field is at the camera, 0 to 1, set for the whole scene by the field. Zero in open
            // space and in scenes without one.
            float _FieldDensityHere;
            float4 _InkColor;
            float _InkWidth;
            float _InkStrength;
            float _FieldSize;
            float _Width;
            float _StreakTime;
            float _MaxStreak;
            float _MaxStreakPixels;
            float _NearFade;
            float _FarFade;
            float _RestVisibility;
            float _FullSpeed;
            float4 _Velocity;
            float4 _Travel;

            // Pixel sizes are given at this screen height and scale with it.
            static const float ReferenceHeight = 1080.0;

            // Pixels added round each streak so its softened edge is never cut off.
            static const float EdgePad = 1.0;

            // Nearest clip-space depth a streak end may sit at. Ends behind it are moved forward along the streak.
            static const float NearestDepth = 1e-3;

            static const float MinLength = 1e-4;

            struct Attributes
            {
                // The mote's spot in the unit cube.
                float4 vertex : POSITION;

                // x: 0 at the streak's head, 1 at its tail. y: -1 or 1, the side.
                float2 corner : TEXCOORD0;

                // x: size, y: brightness, z: rank against Amount, all 0..1.
                float3 random : TEXCOORD1;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 position : SV_POSITION;

                // Pixels along the streak from its head, and across it from its centre line.
                noperspective float2 local : TEXCOORD0;

                // x: streak length in pixels, y: half width in pixels, z: opacity, w: ink width in pixels.
                nointerpolation float4 shape : TEXCOORD1;

                // The fleck's tone: lit or in shadow.
                nointerpolation float3 tone : TEXCOORD2;

                UNITY_VERTEX_OUTPUT_STEREO
            };

            // Moves a streak end that is behind the camera forward along the streak until it is in front.
            float4 ClipToNear(float4 end, float4 other)
            {
                if (end.w >= NearestDepth || other.w <= end.w)
                    return end;

                return lerp(end, other, saturate((NearestDepth - end.w) / (other.w - end.w)));
            }

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                // The copy of this mote nearest the camera, inside a cube of the field size centred on it. Travel moves the
                // field past a camera that holds still, as in cruise, where the ship stays put and the star system moves.
                float3 camera = _WorldSpaceCameraPos;
                float3 anchor = camera + _Travel.xyz;
                float3 head = camera + (frac(input.vertex.xyz - anchor / _FieldSize + 0.5) - 0.5) * _FieldSize;

                // Seen from a moving camera, a still mote smears forward along the camera's motion: that is where
                // it sat, relative to the camera, a moment ago.
                float3 trail = _Velocity.xyz * _StreakTime;
                trail *= min(1, _MaxStreak / max(length(trail), MinLength));
                float3 tail = head + trail;

                float4 clipHead = UnityWorldToClipPos(head);
                float4 clipTail = UnityWorldToClipPos(tail);

                // Fade in away from the camera, judged by the nearer end so no streak sweeps across the screen
                // as it passes, and out towards the field's edge so motes never pop as they wrap.
                float nearest = min(clipHead.w, clipTail.w);
                float halfField = _FieldSize * 0.5;
                float fade = smoothstep(_NearFade * 0.5, _NearFade, nearest)
                    * (1 - smoothstep(halfField * _FarFade, halfField, length(head - camera)));

                // Motes show more with speed, so moving fast feels fast. Amount shows only the motes ranked below it.
                // (A mote ranked between the two amounts appears as the camera goes deeper into a field.)
                float speed = saturate(length(_Velocity.xyz) / _FullSpeed);
                float opacity = lerp(0.35, 1, input.random.y) * lerp(_RestVisibility, 1, speed) * fade;

                // Inside an asteroid field the air is full of grit: more motes show the deeper in the camera is.
                opacity *= step(input.random.z, lerp(_Amount, max(_Amount, _FieldAmount), saturate(_FieldDensityHere)));

                // A fixed share of the flecks catch the sun; the rest are in shadow. The spot in the cube is uniform,
                // so it doubles as the roll.
                float roll = frac(dot(input.vertex.xyz, float3(4093.7, 3571.3, 2617.9)));
                output.tone = (roll < _LitShare ? _Color.rgb : _ShadowColor.rgb) * _Brightness;

                clipHead = ClipToNear(clipHead, clipTail);
                clipTail = ClipToNear(clipTail, clipHead);

                // The streak is a quad of fixed pixel width, built in screen space, with room for the ink rim.
                float pixelScale = _ScreenParams.y / ReferenceHeight;
                float2 halfScreen = _ScreenParams.xy * 0.5;
                float2 headPixels = clipHead.xy / clipHead.w * halfScreen;
                float2 tailPixels = clipTail.xy / clipTail.w * halfScreen;
                float2 along = tailPixels - headPixels;
                float lengthPixels = length(along);
                float2 axis = lengthPixels > MinLength ? along / lengthPixels : float2(1, 0);
                float2 across = float2(-axis.y, axis.x);
                lengthPixels = min(lengthPixels, _MaxStreakPixels * pixelScale);
                tailPixels = headPixels + axis * lengthPixels;
                float halfWidth = _Width * pixelScale * lerp(0.6, 1.4, input.random.x) * 0.5;
                float inkWidth = _InkWidth * pixelScale;

                float isTail = input.corner.x;
                float side = input.corner.y;
                float reach = halfWidth + inkWidth + EdgePad;
                float4 clip = isTail > 0.5 ? clipTail : clipHead;
                float2 endPixels = isTail > 0.5 ? tailPixels : headPixels;
                float2 cornerPixels = endPixels + across * side * reach + axis * (isTail * 2 - 1) * reach;
                clip.xy = cornerPixels / halfScreen * clip.w;

                output.position = clip;
                output.local = float2(isTail * lengthPixels + (isTail * 2 - 1) * reach, side * reach);
                output.shape = float4(lengthPixels, halfWidth, opacity, inkWidth);

                // A faded mote, or one wholly behind the camera, collapses to nothing.
                if (opacity <= 0 || clipHead.w < NearestDepth)
                    output.position = float4(0, 0, 0, 1);

                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                // Distance from the streak's edge, with round caps past each end: negative inside the fleck.
                float lengthPixels = input.shape.x;
                float beyondEnd = max(max(-input.local.x, input.local.x - lengthPixels), 0);
                float edge = length(float2(beyondEnd, input.local.y)) - input.shape.y;

                // The fleck, and the thin ring of ink around it.
                float body = saturate(0.5 - edge);
                float outer = saturate(0.5 - (edge - input.shape.w));
                float rim = saturate(outer - body) * saturate(input.shape.w);
                float opacity = input.shape.z;

                float ink = rim * _InkStrength;
                float3 color = (input.tone * body + _InkColor.rgb * ink) * opacity;
                return half4(color, (body + ink) * opacity);
            }
            ENDCG
        }
    }

    Fallback Off
}
