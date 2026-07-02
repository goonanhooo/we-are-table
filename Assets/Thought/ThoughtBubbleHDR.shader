Shader "UI/ThoughtBubbleHDR"
{
    // 말풍선 전용: 흰색(밝고 저채도) 픽셀만 HDR로 띄워, 톤매핑/블룸 후에도 바닥처럼 순백으로 보이게.
    // 의자(갈색)·하트(빨강)·검은 외곽선은 채도/어둠 때문에 부스트 안 됨.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Boost ("White HDR Boost", Float) = 1.9
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata_t { float4 vertex : POSITION; float4 color : COLOR; float2 texcoord : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 texcoord : TEXCOORD0; };
            sampler2D _MainTex;
            fixed4 _Color;
            float _Boost;
            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                o.texcoord = v.texcoord;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.texcoord) * i.color;
                float mn = min(c.r, min(c.g, c.b));
                float mx = max(c.r, max(c.g, c.b));
                float sat = mx - mn;
                // 밝고(저 mn>0.6) 거의 무채색(sat 작음)일수록 1 → 흰 구름만 부스트
                float wf = saturate((mn - 0.6) / 0.4) * saturate(1.0 - sat * 6.0);
                c.rgb *= lerp(1.0, _Boost, wf);
                return c;
            }
            ENDCG
        }
    }
}
