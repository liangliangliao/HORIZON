Shader "HORIZON/GlowColor"
{
    Properties { _Color ("Color", Color) = (0.3,1,0.8,0.7) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            half4 _Color;
            struct v2f { float4 vertex:SV_POSITION; fixed4 tint:COLOR; };
            v2f vert(appdata_full v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.tint=v.color; return o; }
            half4 frag(v2f i):SV_Target { return _Color*i.tint; }
            ENDCG
        }
    }
}
