Shader "HORIZON/Resource Reveal Horizon"
{
    Properties {
        _Tint ("Resource light",Color)=(.2,.9,.78,1)
        _Age ("Receipt age",Float)=0
        _Pulse ("Release",Range(0,1))=0
        _Gain ("Positive accent",Range(0,1))=1
        _Opacity ("Entrance",Range(0,1))=1
        _Fire ("Irregular receipt energy",Range(0,1))=0
        _Wave ("Receipt propagation",Range(0,1))=0
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
            float _Age, _Pulse, _Gain, _Opacity, _Fire, _Wave;
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
            v2f vert(appdata_base v) {
                v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.uv=v.texcoord; return o;
            }

            // Pixel-footprint smoothing keeps the engraving legible when the
            // Android adaptive renderer lowers its render scale.
            float stroke(float distanceToLine,float width,float pixel) {
                return 1-smoothstep(width,width+pixel,abs(distanceToLine));
            }
            // Bounded three-octave value noise, sampled only during a release.
            // Distorted shell: not a smooth radial sprite scaled over time.
            float releaseHash(float2 p) {
                p=frac(p*float2(.1031,.11369));
                p+=dot(p,p.yx+19.19);
                return frac((p.x+p.y)*p.x);
            }
            float releaseNoise(float2 p) {
                float2 cell=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(releaseHash(cell),releaseHash(cell+float2(1,0)),f.x),
                            lerp(releaseHash(cell+float2(0,1)),releaseHash(cell+1),f.x),f.y);
            }
            float3 releaseLight(float2 p,float age,float flash,float fire,float wave,float3 tint) {
                float r=length(p);
                float3 outLight=float3(0,0,0);
                if(flash>.001) {
                    float glow=exp(-r*r*9.5)*flash*.65;
                    outLight+=float3(1,.97,.89)*glow;
                }
                if(fire>.001) {
                    float spread=smoothstep(0,.8,age);
                    float2 q=p*(5.0-spread*2.5)-float2(age*.45,age*.28);
                    float n=releaseNoise(q)*.57+releaseNoise(q*2.08+7)*.28+releaseNoise(q*4.15-3)*.15;
                    float edge=.23+spread*.56+(n-.5)*.32;
                    float body=(1-smoothstep(edge-.065,edge+.085,r))*smoothstep(.12,.27,r);
                    float detail=smoothstep(.23,.78,n);
                    float hot=body*(.12+detail*.95)*fire;
                    float rim=exp(-abs(r-edge)*18)*fire*(.2+.8*detail);
                    float3 ember=lerp(tint,float3(1,.25,.035),.64);
                    outLight+=ember*rim*.85+lerp(ember,float3(1,.87,.43),detail)*hot;
                }
                if(wave>.001) {
                    float radius=.22+.69*smoothstep(.08,1.15,age);
                    float ring=exp(-abs(r-radius)*43);
                    float tail=exp(-abs(r-radius+.07)*19)*.23;
                    outLight+=lerp(tint,float3(1,.85,.46),.72)*(ring+tail)*wave*.68;
                }
                return outLight*(1-smoothstep(.84,1,r));
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
                if(_Fire+_Wave>.001) light+=releaseLight(p,_Age,0,_Fire,_Wave,accent);
                return half4(ink*alpha+light*mask,alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
