Shader "HORIZON/Immersion"
{
    Properties {
        _Color ("Resource identity", Color) = (.2,.8,1,1)
        _Clock ("Sampled director time", Float) = 0
        _Charge ("Inward energy", Range(0,1)) = 0
        _Burst ("Release", Range(0,1)) = 0
        _Afterglow ("Afterglow", Range(0,1)) = 0
        _Layer ("Depth layer", Float) = 0
        _Seed ("Event seed", Float) = 0
        _ReleaseAge ("Release age", Float) = 0
        _Flash ("Local white flash", Range(0,1)) = 0
        _Fire ("Irregular energy volume", Range(0,1)) = 0
        _Wave ("Traveling shock front", Range(0,1)) = 0
    }
    SubShader {
        Tags { "Queue"="Transparent-7" "RenderType"="Transparent" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            float4 _Color;
            float _Clock, _Charge, _Burst, _Afterglow, _Layer;
            float _ReleaseAge, _Flash, _Fire, _Wave, _Seed;
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; };
            v2f vert(appdata_base v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.texcoord; return o; }
            float bell(float distanceToBand,float sharpness) {
                float x=distanceToBand*sharpness;
                return exp(-x*x);
            }
            #include "HorizonEnergyRelease.cginc"
            half4 frag(v2f i):SV_Target {
                float2 p=(i.uv-.5)*2;
                float r=length(p), a=atan2(p.y,p.x);
                if(_Layer>2.5) {
                    float3 spill=releaseLight(p,_ReleaseAge,_Flash,_Fire,_Wave,_Color.rgb,_Seed);
                    float opacity=min(1,max(spill.r,max(spill.g,spill.b)));
                    return half4(spill/max(.0001,opacity),opacity*_Color.a);
                }
                // Empty central aperture leaves the resource silhouette readable.
                float mask=smoothstep(.16,.29,r)*(1-smoothstep(.85,1,r));
                float spiral=pow(saturate(.5+.5*sin(a*(9+_Layer*2)+r*16+_Clock*(3+_Layer))),24);
                float inward=pow(saturate(.5+.5*sin(r*27+_Clock*9+_Layer*2)),16);
                float beams=pow(saturate(.5+.5*sin(a*(15+_Layer*3)-_Clock*.32)),34);
                float radius=.28+(1-_Burst)*.58;
                float wave=bell(r-radius,32);
                float seam=bell(r-(.60-_Charge*.18),65);
                float halo=bell(r-.48,4.8)*(_Charge*.055+_Afterglow*.035);
                // Broad amber shafts establish volume first; narrow ivory
                // cores and tiny moving glints arrive as the charge grows.
                // Keep the aperture hollow so the event is never a white wall.
                float growth=smoothstep(.04,.92,_Charge);
                float fan=pow(saturate(.5+.5*sin(a*(19+_Layer*4)+sin(a*3)*.55+_Clock*.19)),12);
                float reach=smoothstep(.28,.4,r)*(1-smoothstep(.42+growth*.53,.94+growth*.18,r));
                float filaments=pow(saturate(.5+.5*sin(a*57-r*13+_Clock*.7)),52);
                float sparkle=pow(saturate(sin(a*39+_Clock*1.7)*sin(r*47-_Clock*2.1)),18);
                float corona=bell(r-(.39+sin(_Clock*.8)*.015),25);
                // Streak heads travel in from different distances. Their
                // longer trailing edges point back toward the periphery.
                float lane=floor((a+3.141593)*4.45634);
                float phase=frac(_Clock*.78+lane*.38197+_Layer*.29);
                float head=.85-phase*.58;
                float trail=r-head;
                float streak=bell(trail,trail<0?76:13)*filaments;
                float arrival=sin(phase*3.141593)*growth;
                float inwardStreak=streak*arrival*(.28+.65*growth);
                float light=mask*(spiral*inward*_Charge*.46+beams*_Burst*.58+
                    wave*_Burst*.50+seam*(_Charge*.22+_Afterglow*.08)+halo+
                    fan*reach*growth*.42+filaments*reach*growth*growth*.22+
                    sparkle*growth*.18+corona*growth*.15+inwardStreak*.4+
                    energyStreams(p,_Clock,_Seed+_Layer*.38,growth,33+_Layer*4)*.76);
                // Thin high-frequency rays sit over a broad low-energy halo.
                // This keeps the subject silhouette and text readable instead
                // of turning every late frame into the same white starburst.
                float3 tint=lerp(_Color.rgb,float3(1,.69,.19),growth*.72);
                tint=lerp(tint,lerp(float3(1,.94,.68),_Color.rgb,smoothstep(.04,.26,_Color.b-_Color.g)),
                    saturate(_Burst*.7+sparkle*.25));
                // Blend SrcAlpha One: premultiply once, keep energy independent of ray mask.
                float3 rgb=tint*1.45*min(.72,saturate(light));
                if(_Layer<.5 && (_Flash+_Fire+_Wave)>.001)
                    rgb+=releaseLight(p,_ReleaseAge,_Flash,_Fire,_Wave,_Color.rgb,_Seed);
                // Preserve zero alpha outside the field and the legacy source-alpha
                // blend. Normalize RGB so the added energy is not multiplied twice.
                float alpha=min(1,max(rgb.r,max(rgb.g,rgb.b)));
                return half4(rgb/max(.0001,alpha),alpha*_Color.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
