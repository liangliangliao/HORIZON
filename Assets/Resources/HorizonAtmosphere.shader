Shader "HORIZON/Atmosphere"
{
    Properties {
        _Top ("Night", Color) = (0.015,0.03,0.085,1)
        _Horizon ("Horizon", Color) = (0.15,0.32,0.36,1)
        _Dawn ("Dawn", Color) = (0.55,0.29,0.15,1)
    }
    SubShader {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            half4 _Top, _Horizon, _Dawn;
            struct v2f { float4 vertex:SV_POSITION; float3 direction:TEXCOORD0; };
            v2f vert(appdata_base v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.direction=v.vertex.xyz; return o; }
            half4 frag(v2f i):SV_Target {
                float3 d=normalize(i.direction);
                half3 sky=lerp(_Horizon.rgb,_Top.rgb,smoothstep(-0.05,0.6,d.y));
                float dawn=pow(saturate(dot(d,normalize(float3(-0.65,0.03,1)))),18);
                sky+=_Dawn.rgb*dawn*exp(-abs(d.y)*8)*0.55;
                float2 grid=floor(d.xz/max(0.16,abs(d.y))*110);
                float hash=frac(sin(dot(grid,float2(127.1,311.7)))*43758.5453);
                float2 cell=frac(d.xz/max(0.16,abs(d.y))*110)-0.5;
                float star=step(0.997,hash)*(1-smoothstep(0.01,0.08,length(cell)))*smoothstep(0.1,0.4,d.y);
                sky+=star*half3(0.65,0.78,0.8);
                return half4(sky,1);
            }
            ENDCG
        }
    }
}
