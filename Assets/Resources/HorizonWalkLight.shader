Shader "HORIZON/Walk recovery light"
{
    Properties
    {
        _Tint ("Light colour", Color) = (.18,1,.78,1)
        _Travel ("Travel", Range(0,1)) = 0
        _Ribbon ("Ribbon mesh", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent+5" "RenderType"="Transparent" }
        Pass
        {
            Blend One One
            Cull Off ZWrite Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; };
            half4 _Tint;
            float _Travel, _Ribbon;
            v2f vert(appdata_base v)
            {
                v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.texcoord.xy; return o;
            }
            half4 frag(v2f i):SV_Target
            {
                float2 p=i.uv*2-1;
                float glow,core;
                if(_Ribbon>.5)
                {
                    float ends=saturate(i.uv.x*8)*saturate((1-i.uv.x)*12);
                    glow=exp2(-5*p.y*p.y)*ends;
                    core=exp2(-44*p.y*p.y)*ends*(.3+.7*i.uv.x);
                }
                else
                {
                    float r=length(p);
                    float radius=lerp(.04,.91,_Travel);
                    float d=r-radius;
                    float fade=(1-smoothstep(.55,1,_Travel))*(1-smoothstep(.90,1,r));
                    core=exp2(-d*d*3000)*fade;
                    glow=(exp2(-d*d*95)*.62+exp2(-abs(d+.13)*36)*.55+exp2(-r*r*12)*(1-_Travel)) * fade;
                }
                return half4((_Tint.rgb*glow+lerp(_Tint.rgb,1,.58)*core)*_Tint.a,0);
            }
            ENDCG
        }
    }
}
