Shader "HORIZON/LitColor"
{
    Properties {
        _Color ("Color", Color) = (0.2,0.4,0.5,1)
        _Emission ("Emission", Color) = (0,0,0,1)
        _Smoothness ("Smoothness", Range(0,1)) = 0.32
    }
    SubShader {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _Color, _Emission;
            half _Smoothness;
        CBUFFER_END
        ENDHLSL
        Pass {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; float3 positionWS:TEXCOORD1; half fog:TEXCOORD2; UNITY_VERTEX_INPUT_INSTANCE_ID };
            Varyings vert(Attributes i) {
                Varyings o; UNITY_SETUP_INSTANCE_ID(i); UNITY_TRANSFER_INSTANCE_ID(i,o);
                o.positionWS=TransformObjectToWorld(i.positionOS.xyz); o.positionCS=TransformWorldToHClip(o.positionWS);
                o.normalWS=TransformObjectToWorldNormal(i.normalOS); o.fog=ComputeFogFactor(o.positionCS.z); return o;
            }
            half4 frag(Varyings i):SV_Target {
                UNITY_SETUP_INSTANCE_ID(i);
                half3 normal=normalize(i.normalWS), view=GetWorldSpaceNormalizeViewDir(i.positionWS);
                Light light=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half3 color=_Color.rgb*(SampleSH(normal)+light.color*saturate(dot(normal,light.direction))*light.shadowAttenuation);
                half spec=pow(saturate(dot(normal,normalize(light.direction+view))),lerp(8,64,_Smoothness));
                color+=spec*light.color*.18h+_Emission.rgb;
                #ifdef _ADDITIONAL_LIGHTS
                uint count=GetAdditionalLightsCount();
                for(uint n=0;n<count;n++) { Light extra=GetAdditionalLight(n,i.positionWS); color+=_Color.rgb*extra.color*saturate(dot(normal,extra.direction))*extra.distanceAttenuation; }
                #endif
                return half4(MixFog(color,i.fog),1);
            }
            ENDHLSL
        }
        // Compile these passes in this shader's keyword space. UsePass from
        // URP/Lit triggers incompatible keyword-state assertions in Unity 6.
        Pass {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
        Pass {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
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
