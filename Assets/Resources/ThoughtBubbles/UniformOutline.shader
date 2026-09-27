Shader "Hidden/ThoughtBubbleUniformOutline"
{
    Properties { _MainTex("Silhouette",2D)="white"{} _Opacity("Opacity",Float)=1 _StrokeUV("Stroke UV",Vector)=(.001,.001,0,0) }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _Opacity; float4 _StrokeUV;
            struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct Vary { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; };
            Vary vert(Input v) { Vary o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o; }
            fixed4 frag(Vary i):SV_Target
            {
                float a=tex2D(_MainTex,i.uv).a;
                float inside=a;
                // Reuse the original silhouette, redraw its ink at one screen-space width.
                // This also removes white seams baked into the old painted black outline.
                [unroll] for(int n=0;n<16;n++)
                {
                    float theta=n*0.3926990817;
                    inside=min(inside,tex2D(_MainTex,i.uv+float2(cos(theta),sin(theta))*_StrokeUV.xy).a);
                }
                float fill=smoothstep(.15,.85,inside);
                return fixed4(lerp(float3(.025,.025,.025),float3(1,1,1),fill),a*_Opacity);
            }
            ENDCG
        }
    }
}
