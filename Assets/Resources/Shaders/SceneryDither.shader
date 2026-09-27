Shader "Custom/SceneryDither"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1,1,1,1)
        _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Strength", Float) = 1
        _EmissionMap("Emission", 2D) = "white" {}
        _EmissionColor("Emission Color", Color) = (0,0,0,1)
        _Metallic("Metallic", Range(0,1)) = 0
        _Smoothness("Smoothness", Range(0,1)) = 0.5
        _Cutoff("Cutoff", Range(0,1)) = 0.5
        _AlphaClip("Alpha Clip", Float) = 0
        _Cull("Cull", Float) = 2
        _Tint("Vertex Tint", Color) = (1,1,1,1)
        _VertexColors("Vertex Colors", Float) = 0
        _Unlit("Unlit", Float) = 0
        _UseNormal("Normal Enabled", Float) = 0
        _UseEmission("Emission Enabled", Float) = 0
        _AmbientBoost("Ambient Boost", Float) = 1
        _MainLightScale("Main Light Scale", Float) = 1
        _LightBoost("Additional Light Scale", Float) = 1
        _MaxBright("Max Vertex Brightness", Float) = 100
        _Coverage("Visible Coverage", Range(0,1)) = 1
        _DotPixels("Dot Size", Float) = 3
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Cull [_Cull]
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST, _BaseColor, _EmissionColor, _Tint;
            float _BumpScale, _Metallic, _Smoothness, _Cutoff, _AlphaClip;
            float _VertexColors, _Unlit, _UseNormal, _UseEmission;
            float _AmbientBoost, _MainLightScale, _LightBoost, _MaxBright, _Coverage, _DotPixels;
        CBUFFER_END
        struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 tangentOS:TANGENT; float2 uv:TEXCOORD0; float4 color:COLOR; };
        struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float4 tangentWS:TEXCOORD2; float2 uv:TEXCOORD3; float4 color:COLOR; float fog:TEXCOORD4; };
        Varyings Vert(Attributes input)
        {
            Varyings output;
            output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.tangentWS = float4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w * GetOddNegativeScale());
            output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            output.color = input.color; output.fog = ComputeFogFactor(output.positionCS.z);
            return output;
        }
        half4 Base(Varyings input)
        {
            half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
            if (_VertexColors > 0.5) color *= input.color * _Tint;
            if (_AlphaClip > 0.5) clip(color.a - _Cutoff);
            return color;
        }
        void Dither(float2 position)
        {
            static const float bayer[16] = {0,8,2,10,12,4,14,6,3,11,1,9,15,7,13,5};
            uint2 cell = (uint2)floor(position / max(1.0, _DotPixels)) % 4;
            clip(_Coverage - (bayer[cell.y * 4 + cell.x] + 0.5) / 16.0);
        }
        float3 Normal(Varyings input, bool front)
        {
            float3 normal = normalize(input.normalWS);
            if (_UseNormal > 0.5)
            {
                float3 tangent = normalize(input.tangentWS.xyz);
                float3 bitangent = cross(normal, tangent) * input.tangentWS.w;
                normal = normalize(TransformTangentToWorld(UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale), half3x3(tangent, bitangent, normal)));
            }
            return front ? normal : -normal;
        }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            half4 Frag(Varyings input, bool front:SV_IsFrontFace):SV_Target
            {
                Dither(input.positionCS.xy); half4 base = Base(input);
                float3 normal = Normal(input, front);
                half3 color;
                if (_Unlit > 0.5) color = base.rgb;
                else if (_VertexColors > 0.5)
                {
                    Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                    color = base.rgb * (SampleSH(normal) * _AmbientBoost + light.color * saturate(dot(normal,light.direction)) * light.shadowAttenuation * _MainLightScale);
                    #if defined(_ADDITIONAL_LIGHTS)
                    uint count = GetAdditionalLightsCount();
                    for(uint i=0u;i<count;i++){Light l=GetAdditionalLight(i,input.positionWS);color += base.rgb*l.color*saturate(dot(normal,l.direction))*l.distanceAttenuation*l.shadowAttenuation*_LightBoost;}
                    #endif
                    color = min(color, base.rgb * _MaxBright + 0.02);
                }
                else
                {
                    InputData data = (InputData)0;
                    data.positionWS=input.positionWS; data.normalWS=normal; data.viewDirectionWS=GetWorldSpaceNormalizeViewDir(input.positionWS);
                    data.shadowCoord=TransformWorldToShadowCoord(input.positionWS); data.bakedGI=SampleSH(normal);
                    data.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(input.positionCS); data.shadowMask=half4(1,1,1,1);
                    SurfaceData surface=(SurfaceData)0;
                    surface.albedo=base.rgb; surface.alpha=1; surface.metallic=_Metallic; surface.smoothness=_Smoothness; surface.occlusion=1;
                    surface.normalTS=half3(0,0,1);
                    if(_UseEmission>0.5) surface.emission=SAMPLE_TEXTURE2D(_EmissionMap,sampler_EmissionMap,input.uv).rgb*_EmissionColor.rgb;
                    color=UniversalFragmentPBR(data,surface).rgb;
                }
                return half4(MixFog(color,input.fog),1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDepth
            half4 FragDepth(Varyings input):SV_Target { Dither(input.positionCS.xy); Base(input); return input.positionCS.z; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragNormals
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            half4 FragNormals(Varyings input, bool front:SV_IsFrontFace):SV_Target
            {
                Dither(input.positionCS.xy); Base(input); float3 normal=Normal(input,front);
                #if defined(_GBUFFER_NORMALS_OCT)
                float2 oct=PackNormalOctQuadEncode(normal); return half4(PackFloat2To888(saturate(oct*0.5+0.5)),0);
                #else
                return half4(normal,0);
                #endif
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 _LightDirection, _LightPosition;
            Varyings ShadowVert(Attributes input)
            {
                Varyings output=Vert(input);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirection=normalize(_LightPosition-output.positionWS);
                #else
                float3 lightDirection=_LightDirection;
                #endif
                output.positionCS=TransformWorldToHClip(ApplyShadowBias(output.positionWS,output.normalWS,lightDirection));
                #if UNITY_REVERSED_Z
                output.positionCS.z=min(output.positionCS.z,output.positionCS.w*UNITY_NEAR_CLIP_VALUE);
                #else
                output.positionCS.z=max(output.positionCS.z,output.positionCS.w*UNITY_NEAR_CLIP_VALUE);
                #endif
                return output;
            }
            half4 ShadowFrag(Varyings input):SV_Target {Base(input);return 0;}
            ENDHLSL
        }
    }
    Fallback Off
}
