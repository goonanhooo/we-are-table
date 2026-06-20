Shader "Custom/OceanWater"
{
    // 스타일라이즈드 실시간 바다. Gerstner 파도(정점 변위) + 프레넬 색 + 스페큘러 + 크레스트 거품.
    // URP Forward. 정점에서 월드 XZ 기준으로 파도를 합성하고 노멀을 해석적으로 재계산한다.
    Properties
    {
        _DeepColor    ("Deep Color", Color)    = (0.02, 0.18, 0.30, 1)
        _ShallowColor ("Shallow Color", Color) = (0.10, 0.45, 0.55, 1)
        _SkyColor     ("Fresnel Sky Color", Color) = (0.55, 0.75, 0.95, 1)
        _FoamColor    ("Foam Color", Color)    = (0.95, 0.98, 1.0, 1)
        _Smoothness   ("Specular Power", Range(8, 512)) = 200
        _SpecStrength ("Specular Strength", Range(0,4)) = 1.4
        _FresnelPow   ("Fresnel Power", Range(1,8)) = 4
        _FoamStart    ("Foam Crest Height", Range(0,3)) = 0.55
        _FoamSharp    ("Foam Sharpness", Range(0.02,2)) = 0.45
        _WaveSpeed    ("Wave Speed", Range(0,3)) = 1.0
        // 각 파도: xy = 방향, z = steepness(0~1), w = wavelength
        _WaveA ("Wave A (dirx,dirz,steep,len)", Vector) = (1, 0.3, 0.32, 38)
        _WaveB ("Wave B", Vector) = (0.6, 1, 0.28, 22)
        _WaveC ("Wave C", Vector) = (-0.7, 0.5, 0.20, 13)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "RenderType"="Opaque" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _DeepColor;
                float4 _ShallowColor;
                float4 _SkyColor;
                float4 _FoamColor;
                float  _Smoothness;
                float  _SpecStrength;
                float  _FresnelPow;
                float  _FoamStart;
                float  _FoamSharp;
                float  _WaveSpeed;
                float4 _WaveA;
                float4 _WaveB;
                float4 _WaveC;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float  crest       : TEXCOORD2;
            };

            // Gerstner 파도 1개. p(월드)에 변위를 더하고 tangent/binormal 누적.
            float3 GerstnerWave(float4 wave, float3 p, inout float3 tangent, inout float3 binormal)
            {
                float steepness = wave.z;
                float wavelength = max(wave.w, 0.001);
                float k = 6.28318530718 / wavelength;
                float c = sqrt(9.8 / k);
                float2 d = normalize(wave.xy);
                float f = k * (dot(d, p.xz) - c * _Time.y * _WaveSpeed);
                float a = steepness / k;

                float sf = sin(f);
                float cf = cos(f);
                tangent  += float3(-d.x * d.x * (steepness * sf), d.x * (steepness * cf), -d.x * d.y * (steepness * sf));
                binormal += float3(-d.x * d.y * (steepness * sf), d.y * (steepness * cf), -d.y * d.y * (steepness * sf));
                return float3(d.x * (a * cf), a * sf, d.y * (a * cf));
            }

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);

                float3 tangent = float3(1, 0, 0);
                float3 binormal = float3(0, 0, 1);
                float3 disp = 0;
                disp += GerstnerWave(_WaveA, posWS, tangent, binormal);
                disp += GerstnerWave(_WaveB, posWS, tangent, binormal);
                disp += GerstnerWave(_WaveC, posWS, tangent, binormal);
                posWS += disp;

                float3 n = normalize(cross(binormal, tangent));

                OUT.positionWS = posWS;
                OUT.normalWS = n;
                OUT.crest = disp.y;
                OUT.positionHCS = TransformWorldToHClip(posWS);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(GetWorldSpaceViewDir(IN.positionWS));

                Light mainLight = GetMainLight();
                float3 L = normalize(mainLight.direction);
                float  ndotl = saturate(dot(N, L));

                // 프레넬로 얕은색↔깊은색↔하늘반사 블렌딩
                float fres = pow(1.0 - saturate(dot(N, V)), _FresnelPow);
                float3 baseCol = lerp(_DeepColor.rgb, _ShallowColor.rgb, saturate(ndotl * 0.6 + 0.25));
                baseCol = lerp(baseCol, _SkyColor.rgb, fres);

                // 디퓨즈 + 앰비언트(SH)
                float3 ambient = SampleSH(N);
                float3 col = baseCol * (ambient + mainLight.color * ndotl * 0.9);

                // 스페큘러(블린-퐁, 날카로운 햇빛 반짝임)
                float3 H = normalize(L + V);
                float spec = pow(saturate(dot(N, H)), _Smoothness) * _SpecStrength;
                col += mainLight.color * spec;

                // 파도 마루 거품
                float foam = smoothstep(_FoamStart, _FoamStart + _FoamSharp, IN.crest);
                col = lerp(col, _FoamColor.rgb, foam);

                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
