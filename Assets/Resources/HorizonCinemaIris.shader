Shader "HORIZON/CinemaIris"
{
    Properties { _Strength ("Attention iris",Range(0,1))=0 }
    SubShader {
        Tags { "Queue"="Transparent+100" "RenderType"="Transparent" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            float _Strength;
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
            v2f vert(appdata_base v) { v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord;return o; }
            half4 frag(v2f i):SV_Target {
                float2 p=(i.uv-float2(.5,.61))*float2(1.4,1);
                float edge=smoothstep(.12,.58,length(p));
                return half4(.001,.003,.009,edge*_Strength);
            }
            ENDCG
        }
    }
    Fallback Off
}
