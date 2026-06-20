Shader "Custom/Lava"
{
    // 흐르는 용암(언릿 이미시브). 월드 XZ 기준 2겹 밸류노이즈를 시간에 따라 흘려보내
    // 식은 검은 크러스트(_CrustColor) 사이로 뜨거운 용암(_HotColor, HDR)이 갈라져 흐른다.
    // 이미시브 HDR이라 포스트 블룸에 빛난다.
    Properties
    {
        _HotColor   ("Hot (HDR)", Color)  = (4.0, 1.2, 0.15, 1)
        _MidColor   ("Mid", Color)        = (1.4, 0.35, 0.04, 1)
        _CrustColor ("Crust", Color)      = (0.06, 0.025, 0.02, 1)
        _Scale      ("Noise Scale", Float) = 0.10
        _Speed      ("Flow Speed", Float)  = 0.25
        _FlowDir    ("Flow Dir (x,z)", Vector) = (1, 0.15, 0, 0)
        _CrustWidth ("Crust Amount", Range(0,1)) = 0.55
        _Emission   ("Emission Boost", Range(0.5,6)) = 2.2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "RenderType"="Opaque" }

        Pass
        {
            Name "Lava"
            Tags { "LightMode"="UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _HotColor;
                float4 _MidColor;
                float4 _CrustColor;
                float  _Scale;
                float  _Speed;
                float4 _FlowDir;
                float  _CrustWidth;
                float  _Emission;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
            };

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            // 부드러운 2D 밸류 노이즈
            float vnoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i + float2(0, 0));
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0, amp = 0.5;
                for (int i = 0; i < 4; i++)
                {
                    v += amp * vnoise(p);
                    p *= 2.03;
                    amp *= 0.5;
                }
                return v;
            }

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float2 uv = IN.positionWS.xz * _Scale;
                float2 flow = normalize(_FlowDir.xy + 1e-4) * (_Time.y * _Speed);

                // 두 겹을 서로 다른 속도로 흘려 갈라지는 흐름 표현
                float n1 = fbm(uv - flow);
                float n2 = fbm(uv * 1.7 + flow * 0.6 + 11.3);
                float n = (n1 * 0.65 + n2 * 0.35);

                // 크러스트(차가운 껍질) ~ 균열 사이 뜨거운 용암
                float crust = smoothstep(_CrustWidth, _CrustWidth + 0.12, n);
                float hot = smoothstep(0.62, 0.95, n);

                float3 col = lerp(_CrustColor.rgb, _MidColor.rgb, crust);
                col = lerp(col, _HotColor.rgb, hot);
                col *= _Emission;

                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
