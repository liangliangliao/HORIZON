Shader "HORIZON/Baked Cinematic Flipbook"
{
    Properties { _MainTex("16 baked frames",2D)="black" {} _Frame("Frame",Float)=0 _Color("Tint",Color)=(1,1,1,1) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; float _Frame; half4 _Color;
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; };
            v2f vert(appdata_base v) { v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord;return o; }
            half4 frag(v2f i):SV_Target
            {
                float frame=clamp(floor(_Frame),0,15);
                float2 cell=float2(fmod(frame,4),floor(frame/4));
                float2 uv=(cell+clamp(i.uv,.008,.992))*.25;
                return tex2D(_MainTex,uv)*_Color;
            }
            ENDCG
        }
    }
}
