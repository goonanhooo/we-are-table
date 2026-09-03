Shader "Custom/VertexColorCave"
{
    // ★2026-09-04 동굴용 정점색 셰이더(IceCavern). VertexColorLit + 추가 조명(포인트/스포트) 루프 + URP 안개 + 메인 라이트 배율.
    //   지하 동굴은 태양이 닿지 않아야 하므로 _MainLightScale=0, 앰비언트는 _AmbientBoost로 낮춤. (URP Forward 기준 루프 —
    //   Forward+로 바꾸면 LIGHT_LOOP_BEGIN 매크로 방식으로 갱신 필요)
    Properties
    {
        _Tint ("Tint", Color) = (1,1,1,1)
        _AmbientBoost ("Ambient Boost", Range(0,2)) = 0.3
        _MainLightScale ("Main Light Scale", Range(0,1)) = 0.0
        _LightBoost ("Additional Light Boost", Range(0,4)) = 1.0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "RenderType"="Opaque" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float  _AmbientBoost;
                float  _MainLightScale;
                float  _LightBoost;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
            };
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float  fogFactor   : TEXCOORD2;
                float4 color       : COLOR;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionWS = posWS;
                OUT.positionHCS = TransformWorldToHClip(posWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.fogFactor = ComputeFogFactor(OUT.positionHCS.z);
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float3 N = normalize(IN.normalWS);
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float ndotl = saturate(dot(N, normalize(mainLight.direction)));

                float3 albedo = IN.color.rgb * _Tint.rgb;
                float3 ambient = SampleSH(N) * _AmbientBoost;
                float3 col = albedo * (ambient + mainLight.color * ndotl * mainLight.shadowAttenuation * _MainLightScale);

                #if defined(_ADDITIONAL_LIGHTS)
                uint count = GetAdditionalLightsCount();
                for (uint li = 0u; li < count; ++li)
                {
                    Light l = GetAdditionalLight(li, IN.positionWS);
                    float nl = saturate(dot(N, l.direction));
                    col += albedo * l.color * (l.distanceAttenuation * l.shadowAttenuation * nl * _LightBoost);
                }
                #endif

                col = MixFog(col, IN.fogFactor);
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionHCS : SV_POSITION; };
            float3 _LightDirection;
            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 nWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionHCS = TransformWorldToHClip(ApplyShadowBias(posWS, nWS, _LightDirection));
                return OUT;
            }
            half4 frag (Varyings IN) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
