Shader "Imoni/World Boundary"
{
    Properties
    {
        _TileTex ("Supplied Tile", 2D) = "white" {}
        _BoundaryMask ("Cell Mask", 2D) = "black" {}
        _Tint ("Tile Tint", Color) = (1,1,1,1)
        _WorldSize ("World Size", Vector) = (1,1,0,0)
        _Feather ("Feather In Tiles", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _TileTex, _BoundaryMask;
            float4 _WorldSize, _Tint;
            float _Feather;
            struct input { float4 vertex : POSITION; float2 tile : TEXCOORD0; };
            struct output { float4 position : SV_POSITION; float2 tile : TEXCOORD0; };
            output vert(input value)
            {
                output result; result.position = UnityObjectToClipPos(value.vertex); result.tile = value.tile; return result;
            }
            fixed4 frag(output value) : SV_Target
            {
                // Bilinear cell-center values round diagonal corners without changing any walkable cell.
                float mask = tex2D(_BoundaryMask, value.tile / _WorldSize.xy).r;
                float alpha = smoothstep(0.5 - _Feather * 0.5, 0.5 + _Feather * 0.5, mask);
                float2 tileUv = frac(value.tile); tileUv.y = 1 - tileUv.y;
                fixed4 color = tex2D(_TileTex, tileUv) * _Tint; color.a *= alpha; return color;
            }
            ENDCG
        }
    }
}
