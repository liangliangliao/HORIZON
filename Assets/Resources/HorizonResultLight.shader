Shader "HORIZON/Resource Reveal Horizon"
{
    Properties {
        _Tint ("Resource light",Color)=(.2,.9,.78,1)
        _Age ("Receipt age",Float)=0
        _Pulse ("Release",Range(0,1))=0
        _Gain ("Positive accent",Range(0,1))=1
        _Opacity ("Entrance",Range(0,1))=1
    }
    SubShader {
        Tags { "Queue"="Transparent-4" "RenderType"="Transparent" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            half4 _Tint;
            float _Age, _Pulse, _Gain, _Opacity;
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
            v2f vert(appdata_base v) {
                v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.uv=v.texcoord; return o;
            }

            // Pixel-footprint smoothing keeps the engraving legible when the
            // Android adaptive renderer lowers its render scale.
            float stroke(float distanceToLine,float width,float pixel) {
                return 1-smoothstep(width,width+pixel,abs(distanceToLine));
            }
            half4 frag(v2f i):SV_Target {
                float2 p=i.uv*2-1;
                float r=length(p);
                float pixel=max(fwidth(r),.001);
                float edge=1-smoothstep(.63,1,r);
                float a=atan2(p.y,p.x+.00001);
                float turn=_Age*.24;
                float unfold=smoothstep(.04,.64,_Age);
                float radius=lerp(.35,.49,unfold);
                // Three angular frequencies break up the fan into broad
                // illuminated fins, narrower gaps and sharp moving edges.
                float blade=.5+.5*cos(11*a-turn);
                float split=.5+.5*cos(19*a+turn*1.7+.7);
                float ridge=pow(blade,6.0)*(.20+.80*split);
                float fan=smoothstep(.23,.45,r)*(1-smoothstep(.52,1.03,r));
                float rays=fan*(ridge+pow(split,20.0)*.22)*_Gain*(.17+1.55*_Pulse);

                float crest=exp(-abs(r-radius)*22);
                float metal=stroke(r-radius,.002,pixel)+.46*stroke(r-radius-.026,.001,pixel);
                float marks=1-smoothstep(.11,.24,abs(frac(a*(48.0/6.2831853)+.5)-.5));
                metal+=marks*smoothstep(radius+.03,radius+.038,r)*(1-smoothstep(radius+.052,radius+.063,r));
                float engraving=(.32+.68*pow(.5+.5*cos(3*a-turn),3.0))*metal;
                float frontRadius=.34+.58*smoothstep(.14,.92,_Age);
                float wave=exp(-abs(r-frontRadius)*61)*_Pulse;
                // A grazing light passes across the rim and the lower horizon.
                float sweep=exp(-abs(p.x+p.y*.24+.85-1.7*smoothstep(.16,.94,_Age))*8);
                float horizon=exp(-abs(p.y+.29)*38)*exp(-abs(p.x)*2.7);
                half3 accent=_Tint.rgb;
                half3 foil=lerp(accent,half3(1,.74,.32),.75*_Gain);
                half3 ivory=lerp(accent,half3(1,.95,.76),.85*_Gain);
                float mask=edge*_Opacity;
                // Premultiplied ink brings the scenery down locally. The 3D
                // object depth-tests in front and preserves its own highlights.
                float alpha=mask*(.25+.42*exp(-r*r*2));
                half3 ink=half3(.008,.012,.026)+accent*.033;
                half3 light=accent*(.16*crest+rays)+foil*(.50*engraving+wave*.72)
                           +ivory*(sweep*engraving*.84+horizon*(.16+.68*_Pulse));
                return half4(ink*alpha+light*mask,alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
