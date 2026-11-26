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
                half3 violet = lerp(half3(.33,.18,.79), half3(.55,.24,.92), _Energy.w);
                half3 copper = half3(1,.34,.061);
                half3 gold = half3(1,.72,.23);
                half3 light = half3(1,.97,.82);
                float edge = length((input.uv - .5) * 2);
                float broad = 1 - smoothstep(.14,.84,r);
                float grain = .73 + .14 * sin(spin * 7 + r * 14) + .13 * sin(spin * 13 - r * 8);
                float spokes = pow(saturate(.5 + .5 * sin(spin * 19 + sin(spin * 6) * 1.7)), 9);
                if (age < 0)
                {
                    float inward = sin(r * 28 + age * 13 + sin(spin * 6) * 2);
                    float gathering = _Cue.y * (.10 * broad + .22 * spokes * broad
                        + .24 * smoothstep(.60,1.38,edge) * grain + .032 * inward * broad);
                    half3 tint = lerp(violet, gold, saturate(_Cue.y * .78 + spokes * .19));
                    return half4(tint * gathering, gathering);
                }

                float flash = _Energy.x * (1 - smoothstep(.045, .34, r)) * .77;
                float wake = _Energy.z;
                float body = _Energy.y;
                float furthest = length(float2(_Cue.w * .52, .61));
                float radius = lerp(.072, furthest * 1.08, smoothstep(0,1.32,age));
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
                float bloom = body * broad * (.22 + spokes * .48) * (.73 + .27 * grain);
                float streak = body * (1 - smoothstep(.005,.037,abs(p.y))) * (1 - smoothstep(.12,furthest,r)) * .29;
                float late = smoothstep(.61,1.30,age);
                float undertone = (wake*.27 + body*.14) * late * smoothstep(.38,1.17,edge) * grain;

                float alpha = saturate(flash + sweep + rim + bloom + streak + undertone);
                half3 rgb = light * flash + lerp(copper,light,saturate(crest*.89+lightning*.49)) * sweep
                    + lerp(copper,violet,late*.58) * rim + gold * bloom + light * streak + violet * undertone;
                return half4(min(rgb, alpha), alpha);
            }
            ENDCG
        }
    }
}
