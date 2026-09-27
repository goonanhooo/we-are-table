Shader "Hidden/WeAreTable/LobbyTable"
{
    Properties { _Color ("Color",Color)=(1,1,1,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;};
            struct Varyings {float4 positionCS:SV_POSITION;float3 normalWS:TEXCOORD0;};
            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            CBUFFER_END
            Varyings vert(Attributes i){Varyings o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.normalWS=TransformObjectToWorldNormal(i.normalOS);return o;}
            half4 frag(Varyings i):SV_Target{half shade=.64+.36*saturate(dot(normalize(i.normalWS),normalize(float3(-.35,.8,-.55))));return half4(_Color.rgb*shade,1);}
            ENDHLSL
        }
    }
}
