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

            // 셀마다 확률적으로 별 하나 — 색온도(파랑↔흰↔노랑) 포함.
            // ⚠️rad: 셀 단위 별 반경 — 고밀도(고스케일) 레이어는 셀이 픽셀급이라 반경을 키워야 화면에 걸린다(서브픽셀 소실 방지).
            float3 starLayer(float3 d, float scale, float dens, float rad)
            {
                float3 p = d * scale;
                float3 cell = floor(p);
                float h = hash13(cell);
                float3 off = float3(hash13(cell + 7.1), hash13(cell + 3.7), hash13(cell + 5.3)) - 0.5;
                float dist = length(frac(p) - 0.5 - off * 0.55);
                float star = smoothstep(rad, 0.0, dist) * step(dens, h) * (0.3 + 0.7 * hash13(cell + 11.0));
                float t = hash13(cell + 23.0);                       // 0=파랑 1=노랑, 중간=흰색
                float3 tint = lerp(float3(0.68, 0.80, 1.0), float3(1.0, 0.88, 0.64), t);
                tint = lerp(float3(1, 1, 1), tint, abs(t - 0.5) * 2.0);   // 대부분 흰색, 양끝만 물듦
                return star * tint;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                // ── v4(2026-07-08, 실사 은하수 사진 레퍼런스): 짙은 남색 하늘 + 하늘 전체 잔별 +
                //    크림-핑크 먼지 구름 은하수(암흑성운 실루엣) + 보라 성운 패치 + 코어 쪽 웜 글로우 ──
                float3 col = _BaseColor.rgb;                          // 짙은 남색(검정 아님)

                float w = _BandWidth;
                float3 bn = normalize(_BandDir.xyz);
                float bd = dot(d, bn);
                float band = exp(-(bd * bd) / (w * w));
                float bandWide = exp(-(bd * bd) / (w * w * 7.0));
                float3 cd = _CoreDir.xyz - bn * dot(_CoreDir.xyz, bn);
                cd = normalize(cd + 1e-4);
                float ca = saturate(dot(d, cd));

                // 밴드 좌표계(along=cd, side=e, across=bn) — 구름/먼지를 '밴드 방향으로 길게' 늘이기 위함
                float3 e = normalize(cross(bn, cd) + 1e-4);
                float3 dc = float3(dot(d, e), dot(d, cd), dot(d, bn));

                // 다옥타브 구름결(이방성: 밴드 가로 방향 주파수↑ = 결이 밴드를 따라 흐름)
                float cl1 = noise3(float3(dc.x * 3.2, dc.y * 3.2, dc.z * 7.0));
                float cl2 = noise3(float3(dc.x * 7.0, dc.y * 7.0, dc.z * 15.0) + 31.0);
                float cl3 = noise3(float3(dc.x * 14.0, dc.y * 14.0, dc.z * 28.0) + 77.0);
                float cl4 = noise3(d * 37.0 + 7.0);
                float cloud = 0.35 * cl1 + 0.30 * cl2 + 0.22 * cl3 + 0.13 * cl4;

                // 암흑 성운 실루엣 — ⚠️등방 노이즈 임계는 '둥근 치즈 구멍'이 됨(실측) →
                //   밴드 좌표계에서 across 주파수를 훨씬 높여 길게 찢어진 균열로
                float dn1 = noise3(float3(dc.x * 2.6, dc.y * 2.6, dc.z * 17.0) + 13.0);
                float dn2 = noise3(float3(dc.x * 5.5, dc.y * 5.5, dc.z * 32.0) + 41.0);
                float dust = saturate((dn1 - 0.46) * 3.4) * band;
                dust = max(dust, saturate((dn2 - 0.53) * 3.2) * band * 0.85);

                float milk = (band * saturate(cloud * 1.35 - 0.18) + bandWide * 0.035 * (0.4 + 0.6 * cl1)) * (1.0 - dust * 0.92);

                // 색: 외곽 청백 → 중심으로 갈수록 크림/핑크(웜코어 — 과하면 커피우유가 되니 절제)
                float3 blueW = float3(0.72, 0.80, 1.0);
                float3 cream = float3(1.0, 0.88, 0.78);
                float3 pink = float3(1.0, 0.74, 0.70);
                float warmth = saturate(pow(ca, 3.0)) * 0.85;
                float3 bandCol = lerp(blueW, lerp(cream, pink, cl2 * 0.7), warmth);
                col += bandCol * milk * _BandIntensity;

                // 코어 방향 웜 글로우(사진 하단의 주황-핑크 노을) — ⚠️밴드 폭 안에만(넓히면 안개벽)
                float glow = pow(ca, 6.0) * band * (1.0 - dust * 0.6);
                col += float3(1.0, 0.62, 0.46) * glow * _BandIntensity * 0.28;

                // 보라 성운 패치(레퍼런스 2) — 큰 스케일 노이즈, 은은하게
                float pur = saturate((noise3(d * 2.3 + 91.0) - 0.60) * 2.6);
                col += float3(0.42, 0.22, 0.75) * pur * 0.16;

                // ── 별: 하늘 '전체'가 잔별로 빽빽(사진 핵심) + 밴드/코어 근처 가중 ──
                float bandStar = saturate(band * 0.8 + pow(ca, 3.0) * 0.3);
                float3 s = starLayer(d, 48.0, 0.994, 0.12) * 1.5;
                s += starLayer(d, 100.0, 0.982, 0.14);
                s += starLayer(d, 190.0, lerp(0.945, 0.885, bandStar), 0.22) * 0.9;
                s += starLayer(d, 330.0, lerp(0.930, 0.890, bandStar), 0.28) * 0.6;
                s += starLayer(d, 520.0, 0.930, 0.33) * 0.35;
                col += s * _StarBright;

                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
