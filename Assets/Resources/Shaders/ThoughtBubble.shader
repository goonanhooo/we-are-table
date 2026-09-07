Shader "Hidden/Table/ThoughtBubble"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Shape;
            float _UseTex;
            float _HeightPx;
            float _OutlinePx;
            float4 _FillColor;
            float _Fade;
            float _TexBlend;
            float _Alpha;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            static const float3 CLOUD_LOBES[19] =
            {
                float3(-0.665, -0.285, 0.15),
                float3(-0.475, -0.285, 0.15),
                float3(-0.285, -0.285, 0.15),
                float3(-0.095, -0.285, 0.15),
                float3(0.095, -0.285, 0.15),
                float3(0.285, -0.285, 0.15),
                float3(0.475, -0.285, 0.15),
                float3(0.665, -0.285, 0.15),
                float3(-0.63, 0.285, 0.155),
                float3(-0.42, 0.285, 0.155),
                float3(-0.21, 0.285, 0.155),
                float3(0.0, 0.285, 0.155),
                float3(0.21, 0.285, 0.155),
                float3(0.42, 0.285, 0.155),
                float3(0.63, 0.285, 0.155),
                float3(-0.735, -0.115, 0.15),
                float3(-0.735, 0.115, 0.15),
                float3(0.735, -0.115, 0.15),
                float3(0.735, 0.115, 0.15)
            };

            static const float3 PUFF_LOBES[4] =
            {
                float3(-0.18, 0.02, 0.24),
                float3(0.16, -0.06, 0.26),
                float3(0.02, 0.23, 0.22),
                float3(-0.02, -0.25, 0.21)
            };

            float RoundedBox(float2 p, float2 halfSize, float radius)
            {
                float2 q = abs(p) - halfSize + radius;
                return length(max(q, 0.0)) +
                    min(max(q.x, q.y), 0.0) - radius;
            }

            float CloudDistance(float2 p)
            {
                float d = RoundedBox(
                    p, float2(0.70, 0.27), 0.12);
                [unroll]
                for (int i = 0; i < 19; i++)
                {
                    d = min(d, length(p - CLOUD_LOBES[i].xy) -
                        CLOUD_LOBES[i].z);
                }
                return d;
            }

            // square-ish thought bubble: rounded box with scallops on all four sides (design space 1:1)
            static const float3 SQUARE_LOBES[14] =
            {
                float3(-0.34, -0.37, 0.12), float3(-0.115, -0.37, 0.125),
                float3(0.115, -0.37, 0.12), float3(0.34, -0.37, 0.125),
                float3(-0.34, 0.37, 0.125), float3(-0.115, 0.37, 0.12),
                float3(0.115, 0.37, 0.125), float3(0.34, 0.37, 0.12),
                float3(-0.37, -0.20, 0.12), float3(-0.37, 0.0, 0.125), float3(-0.37, 0.20, 0.12),
                float3(0.37, -0.20, 0.125), float3(0.37, 0.0, 0.12), float3(0.37, 0.20, 0.125)
            };

            float SquareCloudDistance(float2 p)
            {
                float d = RoundedBox(p, float2(0.37, 0.37), 0.10);
                [unroll]
                for (int i = 0; i < 14; i++)
                {
                    d = min(d, length(p - SQUARE_LOBES[i].xy) -
                        SQUARE_LOBES[i].z);
                }
                return d;
            }

            float PuffDistance(float2 p)
            {
                float d = 100.0;
                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    d = min(d, length(p - PUFF_LOBES[i].xy) -
                        PUFF_LOBES[i].z);
                }
                return d;
            }

            float4 frag(v2f input) : SV_Target
            {
                bool isCloud = _Shape < 0.5;
                bool isSquare = _Shape > 1.5;
                float designAspect = isCloud ? 1.7778 : (isSquare ? 1.0 : 1.17);
                float2 p = float2(
                    (input.uv.x - 0.5) * designAspect,
                    0.5 - input.uv.y);

                float d = isCloud
                    ? CloudDistance(p)
                    : (isSquare ? SquareCloudDistance(p) : PuffDistance(p));
                float dPx = d * _HeightPx;

                float antialias = 1.25;
                float halfLine = _OutlinePx * 0.5;

                float alpha = 1.0 - smoothstep(
                    halfLine - antialias,
                    halfLine + antialias, dPx);
                alpha = lerp(alpha, 1.0, _Fade);
                float lineBlend = smoothstep(
                    -halfLine - antialias,
                    -halfLine + antialias, dPx) *
                    (1.0 - _Fade);

                float3 texColor = tex2D(_MainTex, input.uv).rgb;
                float3 fill = lerp(_FillColor.rgb, texColor,
                    saturate(_UseTex * _TexBlend));

                float3 lineColor =
                    float3(0.004, 0.005, 0.006);
                float3 color = lerp(
                    fill, lineColor, lineBlend);
                return float4(color, alpha * _Alpha);
            }
            ENDCG
        }
    }
}
