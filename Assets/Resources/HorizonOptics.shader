Shader "HORIZON/Depth Focus and Refraction"
{
    Properties { _MainTex("Scene", 2D) = "black" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize, _Optics;
            struct Attributes { float4 vertex: POSITION; float2 uv: TEXCOORD0; };
            struct Varyings { float4 vertex: SV_POSITION; float2 uv: TEXCOORD0; };
            Varyings vert(Attributes v)
            { Varyings o; o.vertex=TransformObjectToHClip(v.vertex.xyz); o.uv=v.uv; return o; }
            half4 frag(Varyings i):SV_Target
            {
                float2 delta=i.uv-.5;
                float2 uv=clamp(i.uv+delta*dot(delta,delta)*_Optics.z*.09, .002,.998);
                float depth=LinearEyeDepth(SampleSceneDepth(uv),_ZBufferParams);
                // Foreground and background have different circles of confusion;
                // the focus plane stays sharp as the lens settles into place.
                float coc=saturate(abs(depth-_Optics.y)/max(1.0,depth)*2)*_Optics.x;
                if(coc<.001) return half4(SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv).rgb,1);
                float2 radius=abs(_MainTex_TexelSize.xy)*_Optics.w*coc;
                half3 c=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv).rgb*2;
                c+=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv+radius*float2(1,0)).rgb;
                c+=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv+radius*float2(-1,0)).rgb;
                c+=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv+radius*float2(0,1)).rgb;
                c+=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv+radius*float2(0,-1)).rgb;
                c+=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv+radius*.707).rgb;
                c+=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv-radius*.707).rgb;
                return half4(c/8,1);
            }
            ENDHLSL
        }
    }
}
