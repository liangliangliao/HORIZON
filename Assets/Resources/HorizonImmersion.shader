Shader "HORIZON/Immersion"
{
    Properties {
        _Color ("Resource identity", Color) = (.2,.8,1,1)
        _Clock ("Sampled director time", Float) = 0
        _Charge ("Inward energy", Range(0,1)) = 0
        _Burst ("Release", Range(0,1)) = 0
        _Afterglow ("Afterglow", Range(0,1)) = 0
        _Layer ("Depth layer", Float) = 0
    }
    SubShader {
        Tags { "Queue"="Transparent-7" "RenderType"="Transparent" }
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
            float _Clock, _Charge, _Burst, _Afterglow, _Layer;
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; };
            v2f vert(appdata_base v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.texcoord; return o; }
            half4 frag(v2f i):SV_Target {
                float2 p=(i.uv-.5)*2;
                float r=length(p), a=atan2(p.y,p.x);
                // Empty central aperture leaves the resource silhouette readable.
                float mask=smoothstep(.16,.29,r)*(1-smoothstep(.85,1,r));
                float spiral=pow(saturate(.5+.5*sin(a*(9+_Layer*2)+r*16+_Clock*(3+_Layer))),24);
                float inward=pow(saturate(.5+.5*sin(r*27+_Clock*9+_Layer*2)),16);
                float beams=pow(saturate(.5+.5*sin(a*(15+_Layer*3)-_Clock*.32)),34);
                float radius=.28+(1-_Burst)*.58;
                float wave=exp(-pow((r-radius)*32,2));
                float seam=exp(-pow((r-(.60-_Charge*.18))*65,2));
                float halo=exp(-pow((r-.48)*4.8,2))*(_Charge*.055+_Afterglow*.035);
                float light=mask*(spiral*inward*_Charge*.46+beams*_Burst*.58+
                    wave*_Burst*.50+seam*(_Charge*.22+_Afterglow*.08)+halo);
                // Thin high-frequency rays sit over a broad low-energy halo.
                // This keeps the subject silhouette and text readable instead
                // of turning every late frame into the same white starburst.
                float3 tint=lerp(_Color.rgb,float3(1,.86,.48),.12+_Burst*.25);
                return half4(tint*1.38,saturate(light*.88)*_Color.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
