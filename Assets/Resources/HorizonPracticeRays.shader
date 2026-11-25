Shader "HORIZON/Practice prize light"
{
    Properties { _Beat ("Phase, light, opening, charge", Vector) = (0,0,0,0)
                 _Column ("Landing beam", Color) = (0,0,0,0) }
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
            float4 _Beat, _Column;
            v2f vert(appdata_base v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.texcoord.xy; return o; }
            half4 frag(v2f i):SV_Target
            {
                float2 p=i.uv*2-1;
                if (_Column.a>.01)
                {
                    float height=i.uv.y;
                    float center=abs(p.x)/(1.0+.30*height);
                    float light=exp2(-center*center*16.0)+.44*exp2(-center*center*3.0);
                    float edge=exp2(-abs(center-.42)*23.0)*.48;
                    float fade=(1.0-smoothstep(.29,1.0,height))*smoothstep(0,.06,height);
                    return half4((_Column.rgb*(light+edge)+half3(.75,.70,.64)*exp2(-center*center*150.0))*fade,0);
                }
                float r=length(p), a=atan2(p.y,p.x+.00001);
                float phase=_Beat.x, energy=_Beat.y, open=_Beat.z;
                float fan=pow(saturate(.5+.5*cos(19*a+phase*.8+sin(7*a))),6);
                float fine=pow(saturate(.5+.5*cos(37*a-phase*1.1)),18);
                float reach=lerp(.33,.98,_Beat.w);
                float beams=(fan*.75+fine*.36)*exp2(-3*r)*(1-smoothstep(reach*.55,reach,r));
                float core=exp2(-r*r*155);
                float ring=exp2(-abs(r-(.07+phase*.78))*54)*open*(1-phase);
                float3 rgb=lerp(float3(.33,.20,.95),float3(1,.56,.08),_Beat.w)*(beams+ring);
                rgb+=float3(1,.93,.68)*core;
                return half4(rgb*energy,0);
            }
            ENDCG
        }
    }
}
