Shader "HORIZON/Contact"
{
    SubShader {
        Tags { "Queue"="Transparent-10" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; };
            v2f vert(appdata_base v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.texcoord; return o; }
            fixed4 frag(v2f i):SV_Target {
                float distance=length((i.uv-0.5)*2);
                return fixed4(0.015,0.025,0.045,pow(saturate(1-distance),1.8)*0.52);
            }
            ENDCG
        }
    }
}
