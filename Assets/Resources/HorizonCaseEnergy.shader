Shader "HORIZON/Case Energy"
{
    Properties
    {
        _MainTex ("Case flame atlas", 2D) = "black" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Cue ("Frame, intensity, age, seed", Vector) = (0, 0, 0, 0)
        _Surface ("Wave center and half size on the desk", Vector) = (0, 0, 0, 0)
        _Layer ("Layer", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off ZWrite Off ZTest LEqual
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            half4 _Color;
            float4 _Cue;
            float4 _Surface;
            float _Layer;

            struct v2f { float4 position : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata_base input)
            {
                v2f output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.texcoord;
                return output;
            }

            half4 flame(float frame, float2 uv)
            {
                float2 cell = float2(fmod(frame, 8), floor(frame / 8));
                return tex2D(_MainTex, (cell + clamp(uv, .024, .976)) / 8);
            }

            half4 frag(v2f input) : SV_Target
            {
                float2 uv = input.uv;
                float2 p = uv * 2 - 1;
                half density;
                half3 color = _Color.rgb;
                half coverage = 1;
                if (_Layer < .5)
                {
                    float frame = clamp(_Cue.x, 0, 63);
                    half4 a = flame(floor(frame), uv);
                    half4 b = flame(min(63, floor(frame) + 1), uv);
                    half4 plume = lerp(half4(a.rgb * a.a, a.a), half4(b.rgb * b.a, b.a), frac(frame));
                    half opacity = saturate(_Cue.y);
                    return half4(plume.rgb * color * opacity, plume.a * opacity * .84);
                }
                if (_Layer < 1.5)
                {
                    float radius = length(p * float2(.85, 1.13));
                    density = (1 - smoothstep(.04, .48, radius)) * .82 + (1 - smoothstep(.04, .95, radius)) * .18;
                }
                else if (_Layer < 2.5)
                {
                    float angle = atan2(p.y, p.x);
                    float ripple = sin(angle * 5 + _Cue.w * 6) * .047 + sin(angle * 9 - _Cue.z * 5) * .025;
                    float distanceToFront = length(p * float2(.98, 1.07)) - (.69 + ripple);
                    float front = 1 - smoothstep(.011, .048, abs(distanceToFront));
                    float wake = smoothstep(-.31, -.02, distanceToFront) * (1 - smoothstep(-.02, .01, distanceToFront));
                    float2 desk = abs(_Surface.xy + p * _Surface.zw);
                    float onDesk = (1 - smoothstep(.76, .825, desk.x)) * (1 - smoothstep(.37, .415, desk.y));
                    density = (front + wake * .26) * (.67 + .33 * sin(angle * 3 + _Cue.z * 3)) * onDesk;
                    coverage = .36;
                }
                else
                {
                    float taper = lerp(.28, 1, uv.x);
                    density = (1 - smoothstep(.08, 1, abs(p.y) / taper)) *
                        smoothstep(0, .14, uv.x) * (1 - smoothstep(.48, 1, uv.x));
                    coverage = .17;
                }
                half intensity = saturate(density * _Cue.y);
                return half4(color * intensity, intensity * coverage);
            }
            ENDCG
        }
    }
}
