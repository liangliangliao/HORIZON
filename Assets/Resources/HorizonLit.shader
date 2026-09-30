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
            v2f vert(appdata_full v)
            {
                v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.normal=UnityObjectToWorldNormal(v.normal);
                o.world=mul(unity_ObjectToWorld,v.vertex).xyz; TRANSFER_SHADOW(o); UNITY_TRANSFER_FOG(o,o.pos); return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                half3 n=normalize(i.normal);
                half3 light=normalize(_WorldSpaceLightPos0.xyz);
                half3 view=normalize(_WorldSpaceCameraPos-i.world);
                half diffuse=saturate(dot(n,light)*0.65+0.35);
                half shadow=lerp(0.38,1,SHADOW_ATTENUATION(i));
                half spec=pow(saturate(dot(n,normalize(light+view))),24)*0.14;
                half3 rim=pow(1-saturate(dot(n,view)),3)*half3(0.05,0.15,0.17);
                half3 rgb=_Color.rgb*(ShadeSH9(half4(n,1))*0.75+_LightColor0.rgb*diffuse*shadow*0.8)+_Emission.rgb+rim+spec*_LightColor0.rgb;
                rgb=rgb/(1+rgb*0.35);
                fixed4 c=fixed4(rgb,1);
                UNITY_APPLY_FOG(i.fogCoord,c); return c;
            }
            ENDCG
        }
    }
    Fallback "Diffuse"
}
