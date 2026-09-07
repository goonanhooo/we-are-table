Shader "Custom/UIWorldOverlay"
{
    // ★2026-09-07 월드 공간 UI(키캡 튜토리얼 표지판)용 언릿 셰이더.
    // uGUI 규약: _MainTex(스프라이트/폰트 아틀라스) × 정점색, 알파 전용 폰트 텍스처는 _TextureSampleAdd(캔버스가 (1,1,1,0)으로 세팅)로 보정.
    // ZTest Always → 벽/지형에 가려지지 않음(원근·빌보드로 3D 표지판처럼 보이되 좁은 복도에서 잘리지 않게).
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Overlay" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }

        Pass
        {
            Name "UIWorldOverlay"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZTest Always
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;
            float4 _TextureSampleAdd;
            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionHCS : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.color = IN.color * _Color;
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) + _TextureSampleAdd;
                half4 c = tex * IN.color;
                clip(c.a - 0.002);
                return c;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
