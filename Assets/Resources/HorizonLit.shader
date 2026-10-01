Shader "HORIZON/LitColor"
{
    Properties {
        _Color ("Color", Color) = (0.2,0.4,0.5,1)
        _Emission ("Emission", Color) = (0,0,0,1)
        _Smoothness ("Smoothness", Range(0,1)) = 0.32
    }
    SubShader {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0
        fixed4 _Color;
        half4 _Emission;
        half _Smoothness;
        struct Input { float3 viewDir; };
        void surf(Input IN, inout SurfaceOutputStandard o) {
            o.Albedo=_Color.rgb;
            o.Metallic=0.05;
            o.Smoothness=_Smoothness;
            half rim=pow(1-saturate(dot(normalize(IN.viewDir),float3(0,0,1))),3);
            o.Emission=_Emission.rgb+rim*half3(0.025,0.06,0.065);
            o.Alpha=1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
