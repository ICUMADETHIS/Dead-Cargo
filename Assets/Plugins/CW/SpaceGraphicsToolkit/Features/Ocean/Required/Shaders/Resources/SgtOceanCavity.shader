Shader "Hidden/SgtOceanCavity"
{
    Properties { }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        CGINCLUDE
        #pragma multi_compile __ CW_COMPRESS_POSITIONS
        #include "UnityCG.cginc"
        #include "../../../../../../Shared/SimpleOriginSystem/Scripts/PositionCompression.cginc"

        float3 _SGT_WCam;

        struct a2v { float4 vertex : POSITION; };
        struct v2f {
            float4 position : SV_POSITION;
            float3 worldPos : TEXCOORD0;
        };

        v2f vert(a2v v) {
            v2f o;
            float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
            #if CW_COMPRESS_POSITIONS
                worldPos.xyz = SSS_CompressWorld(worldPos.xyz);
                v.vertex = mul(unity_WorldToObject, worldPos);
            #endif
            o.position = UnityObjectToClipPos(v.vertex);
            o.worldPos = worldPos.xyz;
            return o;
        }

        float4 frag(v2f i) : SV_Target {
            float d = distance(_SGT_WCam, i.worldPos);
            return float4(d, d, 0, 0); // Feeds distance value to both R and G channels
        }
        ENDCG

        // PASS 0: Front Faces (Closest Entry)
        Pass
        {
            Cull Back
            ZWrite Off ZTest Always
            ColorMask R      // Only write to the RED channel
            BlendOp Min
            Blend One One

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDCG
        }

        // PASS 1: Back Faces (Deepest Exit)
        Pass
        {
            Cull Front
            ZWrite Off ZTest Always
            ColorMask G      // Only write to the GREEN channel
            BlendOp Max
            Blend One One

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDCG
        }
    }
}