// 우주 스카이박스 v2(2026-07-08): '실제 우주 한가운데' 느낌 —
// 색 얼룩(네뷸라 워시) 제거, 거의 순검정 심우주 + 색온도 있는 별(파랑↔흰↔노랑) 3겹 +
// 하늘을 가로지르는 '은하수 밴드'(은은한 무채색 젖빛 + 밴드 근처 잔별 밀도 급증).
// 텍스처 없음(방향 벡터 절차) — URP에서 동작(Custom/Lava 등과 동일 패턴).
Shader "Custom/SpaceSkybox"
{
    Properties
    {
        _BaseColor("Deep Space", Color) = (0.004, 0.005, 0.010, 1)
        _StarBright("Star Brightness", Float) = 1.6
        _BandDir("Milky Way Plane Normal", Vector) = (0.25, 0.82, 0.4, 0)
        _BandColor("Milky Way Tint", Color) = (0.62, 0.60, 0.56, 1)
        _BandIntensity("Milky Way Intensity", Float) = 0.30
        _BandWidth("Milky Way Width", Range(0.05, 0.6)) = 0.22
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _BaseColor, _BandColor;
            float4 _BandDir;
            float _StarBright, _BandIntensity, _BandWidth;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float noise3(float3 p)
            {
                float3 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float n000 = hash13(i);
                float n100 = hash13(i + float3(1, 0, 0));
                float n010 = hash13(i + float3(0, 1, 0));
                float n110 = hash13(i + float3(1, 1, 0));
                float n001 = hash13(i + float3(0, 0, 1));
                float n101 = hash13(i + float3(1, 0, 1));
                float n011 = hash13(i + float3(0, 1, 1));
                float n111 = hash13(i + float3(1, 1, 1));
                return lerp(lerp(lerp(n000, n100, f.x), lerp(n010, n110, f.x), f.y),
                            lerp(lerp(n001, n101, f.x), lerp(n011, n111, f.x), f.y), f.z);
            }

            // 셀마다 확률적으로 별 하나 — 색온도(파랑↔흰↔노랑) 포함
            float3 starLayer(float3 d, float scale, float dens)
            {
                float3 p = d * scale;
                float3 cell = floor(p);
                float h = hash13(cell);
                float3 off = float3(hash13(cell + 7.1), hash13(cell + 3.7), hash13(cell + 5.3)) - 0.5;
                float dist = length(frac(p) - 0.5 - off * 0.55);
                float star = smoothstep(0.11, 0.0, dist) * step(dens, h) * (0.3 + 0.7 * hash13(cell + 11.0));
                float t = hash13(cell + 23.0);                       // 0=파랑 1=노랑, 중간=흰색
                float3 tint = lerp(float3(0.68, 0.80, 1.0), float3(1.0, 0.88, 0.64), t);
                tint = lerp(float3(1, 1, 1), tint, abs(t - 0.5) * 2.0);   // 대부분 흰색, 양끝만 물듦
                return star * tint;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float3 col = _BaseColor.rgb;

                // ── 은하수 밴드: 연기 같은 안개가 아니라 '잔별의 강' — 젖빛은 아주 은은하게만 ──
                float3 bn = normalize(_BandDir.xyz);
                float bd = abs(dot(d, bn));
                float band = exp(-(bd * bd) / (_BandWidth * _BandWidth));
                float cloud = noise3(d * 5.0) * 0.55 + noise3(d * 11.0 + 31.0) * 0.45;
                float milk = band * (0.35 + 0.40 * cloud);          // 얼룩 없는 매끈한 젖빛(아주 은은)
                col += _BandColor.rgb * milk * _BandIntensity;

                // ── 별 4겹(성긴 큰 별 / 중간 / 잔별 / 초미세 — 밴드 근처에서 잔별 밀도 급증) ──
                float3 s = starLayer(d, 48.0, 0.995) * 1.6;
                s += starLayer(d, 105.0, 0.990);
                float fineDens = lerp(0.988, 0.935, band);          // 은하수 = 잔별의 강
                s += starLayer(d, 230.0, fineDens) * 0.5;
                s += starLayer(d, 380.0, lerp(0.995, 0.958, band)) * 0.34;
                col += s * _StarBright;

                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
