// 우주 스카이박스: 짙은 심우주 그라데이션 + 절차 별(2겹 셀 해시) + 은은한 네뷸라 색 번짐.
// 텍스처 없이 방향 벡터만으로 그린다(커스텀 언릿 — URP에서도 동작, Custom/Lava 등과 동일 패턴).
Shader "Custom/SpaceSkybox"
{
    Properties
    {
        _BaseColor("Deep Space", Color) = (0.006, 0.007, 0.02, 1)
        _NebulaA("Nebula A (magenta)", Color) = (0.14, 0.03, 0.20, 1)
        _NebulaB("Nebula B (cyan)", Color) = (0.02, 0.10, 0.18, 1)
        _StarDensity("Star Density (higher=fewer)", Range(0.9, 0.999)) = 0.991
        _StarBright("Star Brightness", Float) = 1.7
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

            fixed4 _BaseColor, _NebulaA, _NebulaB;
            float _StarDensity, _StarBright;

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

            // 셀마다 확률적으로 별 하나(무작위 오프셋·밝기)
            float starLayer(float3 d, float scale, float dens)
            {
                float3 p = d * scale;
                float3 cell = floor(p);
                float h = hash13(cell);
                float3 off = float3(hash13(cell + 7.1), hash13(cell + 3.7), hash13(cell + 5.3)) - 0.5;
                float dist = length(frac(p) - 0.5 - off * 0.55);
                float star = smoothstep(0.12, 0.0, dist);
                return star * step(dens, h) * (0.35 + 0.65 * hash13(cell + 11.0));
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float n1 = noise3(d * 2.2);
                float n2 = noise3(d * 4.7 + 13.0);
                float3 col = _BaseColor.rgb
                    + _NebulaA.rgb * saturate(n1 * n1 * 1.5) * saturate(0.55 + 0.45 * d.x)
                    + _NebulaB.rgb * saturate(n2 * n2 * 1.2) * saturate(0.55 - 0.45 * d.y);
                float s = starLayer(d, 80.0, _StarDensity)
                        + starLayer(d, 170.0, _StarDensity) * 0.6
                        + starLayer(d, 40.0, 0.996) * 1.5;   // 드문 큰 별
                col += s * _StarBright;
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
