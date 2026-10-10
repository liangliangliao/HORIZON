Shader "HORIZON/Recorded Memory"
{
    Properties { _MainTex("Recorded frame",2D)="black" {} }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+1" }
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            half4 frag(v2f_img i):SV_Target { return half4(tex2D(_MainTex,i.uv).rgb,1); }
            ENDCG
        }
    }
}
