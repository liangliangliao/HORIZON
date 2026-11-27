Shader "Horizon/RevealField"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Cue ("Age, charge, seed, aspect", Vector) = (0, 0, 0, 1)
        _Energy ("Flash, fire, wave, echo", Vector) = (0, 0, 0, 0)
        _Origin ("Screen origin", Vector) = (.5, .57, 0, 0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct Varyings { float4 position : SV_POSITION; float2 uv : TEXCOORD0; };
            float4 _Cue, _Energy, _Origin;

            Varyings Vert(appdata_base v)
            {
                Varyings o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy;
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float age = _Cue.x, seed = _Cue.z;
                float2 p = input.uv - _Origin.xy;
                p.x *= _Cue.w;
                float r = length(p);
                float angle = atan2(p.y, p.x + .0001);
                float spin = angle + seed * 6.283 + age * .31;
                half3 violet = lerp(half3(.33,.18,.79),half3(.22,.45,.85),_Energy.w);
                half3 copper = half3(1,.34,.061);
                half3 gold = half3(1,.72,.23);
                half3 light = half3(1,.97,.82);
                float edge = length((input.uv - .5) * 2);
                float broad = 1 - smoothstep(.14,.84,r);
                float grain = .73 + .14 * sin(spin * 7 + r * 14) + .13 * sin(spin * 13 - r * 8);
                float spokes = pow(saturate(.5 + .5 * sin(spin * 19 + sin(spin * 6) * 1.7 + r * sin(spin*3))), 9);
                if (age < 0)
                {
                    float hold = step(-.191,age);
                    float inward = pow(saturate(.5+.5*sin(r*43 + age*29 + sin(spin*6)*1.55)), 6);
                    float filament = .82 * spokes * inward * broad * (1 - hold*.88);
                    float gather = _Cue.y * (.040 * broad + filament + .060 * smoothstep(.55,1.27,edge) * grain);
                    float shade = _Cue.y * (.28+.33*hold) * smoothstep(.19,.74,r);
                    half3 tint = lerp(violet,gold,smoothstep(-1.04,-.24,age)*.86);
                    return half4(tint*gather,saturate(gather+shade))*_Origin.w;
                }

                float flash = _Energy.x*(1-smoothstep(.045,lerp(.34,.217,_Energy.w),r))*.77;
                float wake = _Energy.z;
                float body = _Energy.y;
                float furthest = length(float2(_Cue.w * .52, .61));
                float radius = lerp(.061, furthest * 1.03, _Origin.z);
                float jagged = (sin(spin * 5 + age * 1.8) * .024 + sin(spin * 12 - age * 4) * .012
                    + sin(spin * 27 + age * 4.8) * .006) * smoothstep(.08,.35,radius);
                float distance = abs(r - radius - jagged);
                float crest = (1 - smoothstep(.011,.055,distance)) * grain;
                float wakeGlow = (1 - smoothstep(.025,.185,distance)) * .53;
                float lightning = (1 - smoothstep(.002,.012,distance)) *
                    smoothstep(.05,.68,sin(spin * 5 + age * 2.5));
                float trailing = abs(r - radius * .75 + jagged * 1.55);
                float second = (1 - smoothstep(.013,.068,trailing)) * .31 * grain;
                float sweep = wake * (crest * .63 + wakeGlow + lightning * .34 + second);
                float rim = wake * smoothstep(.52,1.30,edge) * (.18 + spokes * .42) * (1 - smoothstep(.77,1.63,age));
                float rest = smoothstep(.38,.79,age);
                float bloom = body * broad * (.19 + spokes * .48) * (.73 + .27 * grain)
                    * (1-rest*.78*(1-smoothstep(.09,.37,r)));
                float streak = body * (1 - smoothstep(.005,.037,abs(p.y))) * (1 - smoothstep(.12,furthest,r)) * .29;
                float late = smoothstep(.61,1.30,age);
                float undertone = (wake*.27 + body*.14) * late * smoothstep(.38,1.17,edge) * grain;
                // The archive convergence throws light across the frame while
                // the persistent result rows remain in front of this layer.
                float fans = pow(saturate(.54 + .46 * sin(spin * 11 + sin(spin*7)*1.13 + r*2)), 4);
                float flood = _Energy.w * (wake*.27 + body*.30) * (1-smoothstep(.35,1.02,age));
                float fanLight = flood * (.22 + fans*.89) * (1-smoothstep(radius + .07,radius + .40,r));
                float crown = flood * (1 - smoothstep(.006,.067,abs(p.y+.18*p.x))) * .62;

                float alpha = saturate(flash + sweep + rim + bloom + streak + undertone + fanLight + crown);
                half3 rgb = light * flash + lerp(copper,light,saturate(crest*.89+lightning*.49)) * sweep
                    + lerp(copper,violet,late*.58) * rim + gold * bloom + light * streak + violet * undertone
                    + lerp(gold,light,fans*.69) * fanLight + gold*crown;
                return half4(min(rgb,alpha),alpha)*_Origin.w;
            }
            ENDCG
        }
    }
}
