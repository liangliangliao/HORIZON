Shader "HORIZON/RewardAura"
{
    Properties {
        _Color ("Tint", Color) = (1,0.65,0.16,1)
        _Intensity ("Intensity", Range(0,2)) = 0
        _Clock ("Director clock", Float) = 0
        _Mode ("Halo or rays", Float) = 0
    }
    SubShader {
        Tags { "Queue"="Transparent-5" "RenderType"="Transparent" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            float4 _Color;
            float _Intensity, _Clock, _Mode;
            struct v2f { float4 position:SV_POSITION; float2 uv:TEXCOORD0; };
            v2f vert(appdata_base v) { v2f o; o.position=UnityObjectToClipPos(v.vertex); o.uv=v.texcoord; return o; }
            half4 frag(v2f i):SV_Target {
                float2 p=(i.uv-.5)*2;
                float radius=length(p), angle=atan2(p.y,p.x);
                float edge=1-smoothstep(.72,1,radius);
                float ring=exp(-pow((radius-.52)*22,2));
                float glow=exp(-radius*radius*5)*.34;
                float rays=pow(saturate(.5+.5*sin(angle*18-_Clock*.65)),16);
                rays+=pow(saturate(.5+.5*sin(angle*11+_Clock*.31)),28)*.35;
                float light=lerp(glow+ring*.45,rays*smoothstep(.05,.25,radius)+glow*.7,saturate(_Mode))*edge;
                // Soft additive light remains visible on GLES without a postprocess
                // chain. No wall clock or _Time: pause and hit-stop freeze the rays.
                return half4(_Color.rgb*1.6,light*_Color.a*_Intensity);
            }
            ENDCG
        }
    }
    Fallback Off
}
