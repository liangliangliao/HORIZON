Shader "HORIZON/LitColor"
{
    Properties { _Color ("Color", Color) = (0.2,0.4,0.5,1) _Emission ("Emission", Color) = (0,0,0,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
            fixed4 _Color, _Emission;
            struct v2f { float4 pos:SV_POSITION; float3 normal:TEXCOORD0; float3 world:TEXCOORD1; SHADOW_COORDS(2) UNITY_FOG_COORDS(3) };
            v2f vert(appdata_base v)
            {
                v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.normal=UnityObjectToWorldNormal(v.normal);
                o.world=mul(unity_ObjectToWorld,v.vertex).xyz; TRANSFER_SHADOW(o); UNITY_TRANSFER_FOG(o,o.pos); return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                half3 n=normalize(i.normal);
                half diffuse=max(0,dot(n,normalize(_WorldSpaceLightPos0.xyz)));
                half3 rim=pow(1-saturate(dot(n,normalize(_WorldSpaceCameraPos-i.world))),3)*half3(0.08,0.22,0.24);
                fixed4 c=fixed4(_Color.rgb*(ShadeSH9(half4(n,1))+_LightColor0.rgb*diffuse*SHADOW_ATTENUATION(i))+_Emission.rgb+rim,1);
                UNITY_APPLY_FOG(i.fogCoord,c); return c;
            }
            ENDCG
        }
    }
    Fallback "Diffuse"
}
