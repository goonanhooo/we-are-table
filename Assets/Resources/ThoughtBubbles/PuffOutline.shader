Shader "Hidden/ThoughtPuffOutline"
{
    Properties { _MainTex ("Puff", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _Crop;
            float2 _Radius;
            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 uv = _Crop.xy + ((i.uv - .5) * 1.12 + .5) * _Crop.zw;
                fixed4 center = tex2D(_MainTex, uv);
                float a = center.a;
                for (int k=0;k<8;k++)
                {
                    float angle=k*.785398163;
                    a=max(a,tex2D(_MainTex,uv+float2(cos(angle),sin(angle))*_Radius).a);
                }
                return fixed4(center.rgb*center.a/max(a,.00001),a);
            }
            ENDCG
        }
    }
}
