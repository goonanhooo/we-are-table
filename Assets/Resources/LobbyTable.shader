Shader "Hidden/WeAreTable/LobbyTable"
{
    Properties { _Color ("Color",Color)=(1,1,1,1) _MainTex ("Texture",2D)="white" {} }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;};
            struct Varyings {float4 positionCS:SV_POSITION;float3 normalWS:TEXCOORD0;float2 uv:TEXCOORD1;};
            TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            half4 _Color;float4 _MainTex_ST;
            CBUFFER_END
            Varyings vert(Attributes i){Varyings o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.normalWS=TransformObjectToWorldNormal(i.normalOS);o.uv=TRANSFORM_TEX(i.uv,_MainTex);return o;}
            half4 frag(Varyings i):SV_Target{half shade=.64+.36*saturate(dot(normalize(i.normalWS),normalize(float3(-.35,.8,-.55))));half3 albedo=_Color.rgb*SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).rgb;return half4(albedo*shade,1);}
            ENDHLSL
        }
    }
}
