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
        _CoreDir("Galactic Core Direction", Vector) = (0.6, 0.0, 0.8, 0)
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
            float4 _BandDir, _CoreDir;
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

                // ── 웅장한 은하수 아치(2026-07-08 v3): 황금 중심부 + 암흑 성운 골 + 파란 외곽 + 잔별의 강 ──
                float w = _BandWidth;
                float3 bn = normalize(_BandDir.xyz);
                float bd = dot(d, bn);                               // 은하면까지 부호 거리
                float band = exp(-(bd * bd) / (w * w));
                float bandWide = exp(-(bd * bd) / (w * w * 9.0));    // 넓은 헤일로

                // 은하 중심(궁수자리) 방향 — 은하면에 투영해 사용
                float3 cd = _CoreDir.xyz - bn * dot(_CoreDir.xyz, bn);
                cd = normalize(cd + 1e-4);
                float ca = saturate(dot(d, cd));                     // 중심 방향 근접도
                float bulge = pow(ca, 6.0) * band;                   // 중심부 광대역 벌지
                float bulge2 = pow(ca, 20.0) * exp(-(bd * bd) / (w * w * 2.6));   // 밝은 심

                // 구름결(다옥타브) + 암흑 성운 골(밴드 심을 따라 노이즈로 굽이치는 어두운 틈)
                float cl1 = noise3(d * 5.0);
                float cl2 = noise3(d * 11.0 + 31.0);
                float cl3 = noise3(d * 23.0 + 77.0);
                float cloud = 0.45 * cl1 + 0.35 * cl2 + 0.20 * cl3;
                float riftBd = bd + (noise3(d * 7.0 + 13.0) - 0.5) * 0.10;
                float rift = exp(-(riftBd * riftBd) / (w * w * 0.14)) * saturate(cl2 * 1.7 - 0.25);
                rift *= saturate(0.35 + ca);                         // 중심부일수록 골 선명

                float milk = (band * (0.35 + 0.60 * cloud) + bandWide * (0.05 + 0.09 * cl1)) * (1.0 - rift * 0.72);
                float3 warmC = float3(1.0, 0.88, 0.70);
                float3 coolC = float3(0.76, 0.83, 1.0);                          // 은청색 별의 강
                float3 bandCol = lerp(coolC, warmC, pow(ca, 12.0));              // 황금빛은 중심 매듭에만 압축
                col += bandCol * milk * _BandIntensity;
                col += warmC * (bulge * 0.10 + bulge2 * 0.85) * _BandIntensity;  // 중심부 광휘(콤팩트)

                // ── 별 4겹(색온도) — 밴드/중심부일수록 잔별 밀도 급증 = 별의 강 ──
                float bandStar = saturate(band + bulge * 0.6);
                float3 s = starLayer(d, 48.0, 0.995) * 1.6;
                s += starLayer(d, 105.0, lerp(0.990, 0.975, bandStar));
                s += starLayer(d, 230.0, lerp(0.988, 0.915, bandStar)) * 0.5;
                s += starLayer(d, 380.0, lerp(0.995, 0.945, bandStar)) * 0.34;
                col += s * _StarBright;

                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
