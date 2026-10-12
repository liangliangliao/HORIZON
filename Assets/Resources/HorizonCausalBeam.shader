Shader "HORIZON/CausalBeam"
{
    Properties {
        _Color ("Evidence tint", Color) = (0.2,0.9,0.8,1)
        _Intensity ("Cue intensity", Range(0,2)) = 0
        _Clock ("Director clock", Float) = 0
    }
    SubShader {
        Tags { "Queue"="Transparent-4" "RenderType"="Transparent" }
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
            float _Intensity, _Clock;
            struct v2f { float4 position:SV_POSITION; float2 uv:TEXCOORD0; };
            v2f vert(appdata_base v) { v2f o; o.position=UnityObjectToClipPos(v.vertex); o.uv=v.texcoord; return o; }
            half4 frag(v2f i):SV_Target {
                float x=(i.uv.x-.5)*2;
                float core=exp(-x*x*28), glow=exp(-x*x*4)*.25;
                float height=saturate(i.uv.y);
                float fade=smoothstep(0,.06,height)*(1-smoothstep(.55,1,height));
                float scan=.8+.2*sin(height*31-_Clock*4);
                return half4(_Color.rgb*1.5,(core+glow)*fade*scan*_Color.a*_Intensity);
            }
            ENDCG
        }
    }
    Fallback Off
}
