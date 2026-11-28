Shader "HORIZON/Embossed Outcome SDF"
{
    Properties {
        _MainTex("Font atlas",2D)="white" {}
        _FaceColor("Face",Color)=(1,1,1,1)
        _FaceDilate("Face dilate",Range(-1,1))=0
        _OutlineColor("Engraved edge",Color)=(.08,.025,.008,1)
        _OutlineWidth("Outline",Range(0,1))=.16
        _OutlineSoftness("Softness",Range(0,1))=0
        _GlowColor("Local halo",Color)=(1,.6,.13,.6)
        _GlowOffset("Glow offset",Float)=0
        _GlowInner("Glow inner",Float)=.05
        _GlowOuter("Glow outer",Float)=.2
        _GlowPower("Glow power",Float)=.75
        _WeightNormal("Normal weight",Float)=0
        _WeightBold("Bold weight",Float)=.5
        _GradientScale("Gradient scale",Float)=8
        _TextureWidth("Atlas width",Float)=1024
        _TextureHeight("Atlas height",Float)=1024
        _ScaleX("Scale X",Float)=1
        _ScaleY("Scale Y",Float)=1
        _PerspectiveFilter("Perspective filter",Float)=.875
        _Sharpness("Sharpness",Float)=0
        _ScaleRatioA("Ratio A",Float)=1
        _ScaleRatioB("Ratio B",Float)=1
        _ScaleRatioC("Ratio C",Float)=1
        _ShaderFlags("Flags",Float)=0
        _MaskSoftnessX("Mask softness X",Float)=0
        _MaskSoftnessY("Mask softness Y",Float)=0
        _StencilComp("Stencil comparison",Float)=8
        _Stencil("Stencil ID",Float)=0
        _StencilOp("Stencil operation",Float)=0
        _StencilWriteMask("Stencil write",Float)=255
        _StencilReadMask("Stencil read",Float)=255
        _ColorMask("Color mask",Float)=15
        _CullMode("Cull mode",Float)=0
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip("Alpha clip",Float)=0
    }
    SubShader {
        Tags {"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True"}
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull [_CullMode] Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha ColorMask [_ColorMask]
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            sampler2D _MainTex;
            float4 _FaceColor,_OutlineColor,_GlowColor,_ClipRect;
            float _FaceDilate,_OutlineWidth,_GlowOuter;
            struct appdata {float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR;};
            struct v2f {float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float4 local:TEXCOORD1; fixed4 color:COLOR;};
            v2f vert(appdata i) {v2f o; o.local=i.vertex; o.vertex=UnityObjectToClipPos(i.vertex); o.uv=i.uv; o.color=i.color*_FaceColor; return o;}
            half4 frag(v2f i):SV_Target {
                float d=tex2D(_MainTex,i.uv).a+_FaceDilate*.12;
                float aa=max(.002,fwidth(d)*.85);
                float face=smoothstep(.5-aa,.5+aa,d);
                float edge=smoothstep(.5-_OutlineWidth*.3-aa,.5-_OutlineWidth*.3+aa,d);
                // SDF derivatives light the stroke edge, not a flat colour slab.
                // Interior remains legible while the raised upper-left rim glints.
                float2 gradient=float2(ddx(d),ddy(d));
                float slope=dot(normalize(gradient+float2(.0001,.0002)),normalize(float2(-.65,.76)));
                float bevel=(1-smoothstep(.51,.65,d))*face;
                half3 metal=i.color.rgb*(1.02+bevel*slope*.24)+bevel*pow(saturate(slope),6)*half3(1,.93,.72)*.6;
                half3 color=lerp(_OutlineColor.rgb,metal,face);
                float halo=exp(-pow((d-.44)/max(.025,_GlowOuter*.28),2))*(1-edge)*_GlowColor.a*.32;
                float alpha=saturate(edge+halo)*i.color.a;
                color=lerp(color,_GlowColor.rgb,halo);
                #ifdef UNITY_UI_CLIP_RECT
                alpha*=UnityGet2DClipping(i.local.xy,_ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(alpha-.001);
                #endif
                return half4(color,alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
