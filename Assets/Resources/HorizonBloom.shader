Shader "HORIZON/Bloom"
{
    Properties { _MainTex ("Source", 2D) = "white" {} }
    SubShader {
        Cull Off ZWrite Off ZTest Always
        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex, _BloomTex;
        float4 _MainTex_TexelSize;
        float2 _Direction;
        float _Intensity, _Echo;
        half4 prefilter(v2f_img i):SV_Target {
            half3 c=tex2D(_MainTex,i.uv).rgb;
            half brightness=max(c.r,max(c.g,c.b));
            return half4(c*max(0,brightness-0.72)/max(brightness,0.001),1);
        }
        half4 blur(v2f_img i):SV_Target {
            float2 d=_Direction*_MainTex_TexelSize.xy;
            half3 c=tex2D(_MainTex,i.uv).rgb*0.227027;
            c+=(tex2D(_MainTex,i.uv+d*1.384615).rgb+tex2D(_MainTex,i.uv-d*1.384615).rgb)*0.316216;
            c+=(tex2D(_MainTex,i.uv+d*3.230769).rgb+tex2D(_MainTex,i.uv-d*3.230769).rgb)*0.070270;
            return half4(c,1);
        }
        half4 composite(v2f_img i):SV_Target {
            float2 uv=i.uv;
            float2 offset=(uv-0.5)*_Echo*0.003;
            half3 c=tex2D(_MainTex,uv).rgb;
            c.r=tex2D(_MainTex,uv+offset).r; c.b=tex2D(_MainTex,uv-offset).b;
            c+=tex2D(_BloomTex,uv).rgb*_Intensity;
            c=c/(1+c*0.18);
            float vignette=1-dot(uv-0.5,uv-0.5)*0.42;
            return half4(c*vignette,1);
        }
        ENDCG
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment prefilter
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment blur
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment composite
            ENDCG }
    }
    Fallback Off
}
