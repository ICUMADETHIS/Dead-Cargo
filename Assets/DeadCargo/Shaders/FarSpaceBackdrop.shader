// The far camera's picture of space, sky and planets, shown as the main camera's sky, pixel for pixel. Used by FarSpace
// on a Skybox of the main camera's own, so the near world is drawn over it and the picture keeps its HDR brightness.
Shader "DeadCargo/Far Space Backdrop"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Far Space", 2D) = "black" {}
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 screen : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.screen = ComputeScreenPos(o.pos);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                return half4(tex2Dproj(_MainTex, i.screen).rgb, 1.0);
            }
            ENDCG
        }
    }

    Fallback Off
}
