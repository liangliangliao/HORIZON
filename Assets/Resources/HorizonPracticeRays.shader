Shader "HORIZON/Practice prize light"
{
    Properties { _Beat ("Phase, light, opening, charge", Vector) = (0,0,0,0)
                 _Column ("Landing beam", Color) = (0,0,0,0)
                 _Seed ("Event seed", Float) = 0
                 _Layer ("Backlight, airborne sparks or chased enamel", Float) = 0 }
    SubShader
    {
        Tags { "Queue"="Transparent+5" "RenderType"="Transparent" }
        Pass
        {
            Blend One One
            Cull Off ZWrite Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "HorizonEnergyRelease.cginc"
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; };
            float4 _Beat, _Column;
            float _Layer, _Seed;
            float lineMask(float distance, float width)
            {
                return 1.0-smoothstep(width,width+.012,abs(distance));
            }
            float hashSector(float id)
            {
                return frac(sin(id*12.9898+4.17)*4375.5453);
            }
            v2f vert(appdata_base v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.texcoord.xy; return o; }
            half4 frag(v2f i):SV_Target
            {
                float2 p=i.uv*2-1;
                if (_Column.a>.01)
                {
                    float height=i.uv.y;
                    float center=abs(p.x)/(1.0+.30*height);
                    float light=exp2(-center*center*16.0)+.44*exp2(-center*center*3.0);
                    float edge=exp2(-abs(center-.42)*23.0)*.48;
                    float fade=(1.0-smoothstep(.29,1.0,height))*smoothstep(0,.06,height);
                    return half4((_Column.rgb*(light+edge)+half3(.75,.70,.64)*exp2(-center*center*150.0))*fade,0);
                }
                float r=length(p), a=atan2(p.y,p.x+.00001);
                float phase=_Beat.x, energy=_Beat.y, opening=_Beat.z;
                float3 amber=float3(1.0,.43,.085), brass=float3(1.0,.79,.39);
                if (_Layer>1.5)
                {
                    // Repeating chased rosettes with a dark groove, paired rails and corner diamonds.
                    float2 cell=float2(frac((p.x+1)*2.5)*2-1,p.y*1.02);
                    float d=length(cell), theta=atan2(cell.y,cell.x+.0001);
                    float flower=lineMask(d-.48-.13*cos(6*theta),.018);
                    float bezel=lineMask(d-.79,.015)+lineMask(abs(p.y)-.9,.018);
                    float diamond=lineMask(abs(cell.x)+abs(cell.y)-.21,.025);
                    float shade=.52+.36*sin(cell.x*2.9+p.y*3.8);
                    return half4((amber*.4+brass*shade)*(flower*.83+bezel*.63+diamond),0);
                }
                float spin=(a+3.141593)/6.283185;
                float wave=smoothstep(.18,.72,opening);
                if (_Layer>.5)
                {
                    // One polar cell per flight path; each spark has a different launch and depth.
                    float sector=floor(spin*43);
                    float seed=hashSector(sector+_Seed*15);
                    float lane=abs(frac(spin*43)-.5);
                    float flight=frac(phase*(.6+seed*.5)+seed);
                    float radius=lerp(.96,.14,flight);
                    if(opening>0) radius=.10+wave*(.36+seed*.62);
                    float tip=exp2(-abs(r-radius)*112)*exp2(-lane*lane*56);
                    float tail=exp2(-abs(r-radius-.04)*23)*exp2(-lane*lane*140);
                    float shock=lineMask(r-(.10+wave*.76),.008)*sin(3.14159*wave);
                    float fall=(1-smoothstep(.78,1,r))*smoothstep(.065,.2,r);
                    float stream=opening>0 ? 0 : energyStreams(p,phase,_Seed,_Beat.w,37);
                    return half4((brass*(tip*1.25+stream*.72)+amber*tail*.48+float3(.48,.77,1)*shock*.7)*fall*energy,0);
                }
                // Irregular broad shafts behind the relic, with brighter inner spines.
                float angle=a+sin(3*a+phase)*.075;
                float blades=pow(saturate(.5+.5*cos(13*angle-phase*1.8+sin(5*a))),4);
                float fine=pow(saturate(.5+.5*cos(31*a+phase*2.1)),20);
                float reach=lerp(.35,1.0,_Beat.w);
                float air=(1-smoothstep(.32*reach,reach,r))*exp2(-1.7*r);
                float caustic=pow(saturate(.5+.5*cos(6*a+8*r-4*phase)),4);
                float3 dusk=float3(.18,.23,.85);
                float3 light=lerp(dusk,amber,smoothstep(.20,.83,_Beat.w))*(blades*.90+fine*.47)*air;
                light+=dusk*caustic*air*.24+brass*exp2(-r*r*76);
                light+=float3(1,.93,.77)*exp2(-r*r*330)*1.8;
                return half4(light*energy,0);
            }
            ENDCG
        }
    }
}
