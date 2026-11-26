Shader "Hidden/Imoni/EffectGrayscale"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _Gray ("Grayscale amount", Range(0, 1)) = 0
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _Gray;
            float4 frag(v2f_img input) : SV_Target
            {
                float4 color = tex2D(_MainTex, input.uv);
                // Rec.709 luminance; alpha and the supplied image's light halo are preserved.
                float luminance = dot(color.rgb, float3(0.2126, 0.7152, 0.0722));
                color.rgb = lerp(color.rgb, luminance.xxx, _Gray);
                return color;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
