Shader "HORIZON/CinemaBackdrop"
{
    Properties {
        _Color ("Optical accent",Color)=(.2,.8,.7,1)
        _Clock ("Director clock",Float)=0
    }
    SubShader {
        Tags { "Queue"="Background+10" "RenderType"="Opaque" }
        ZWrite Off
        Cull Off
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            half4 _Color; float _Clock;
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
            v2f vert(appdata_base v) { v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord;return o; }
            half4 frag(v2f i):SV_Target {
                float2 p=(i.uv-.5)*float2(2.4,1.4);
                float halo=exp(-dot(p,p)*4);
                float mist=exp(-pow((p.y+.2)*7,2))*.12;
                float arc=exp(-pow((length(p*float2(1,.82))-.4)*70,2))*.03;
                float sweep=.85+.15*sin(p.x*8+p.y*5-_Clock*.25);
                half3 dark=half3(.002,.006,.016);
                return half4(dark+_Color.rgb*(halo*.055+mist+arc)*sweep,1);
            }
            ENDCG
        }
    }
    Fallback Off
}
