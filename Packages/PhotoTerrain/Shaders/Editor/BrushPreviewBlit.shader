Shader "Hidden/PhotoTerrain/BrushPreviewBlit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "Painting/Brush.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float _BackgroundType;

            float4 frag (v2f i) : SV_Target
            {
                float brush = SampleBrushNoWeight(i.uv.xyy * PT_BrushSize.xyy, i.uv);
                float4 output = brush.xxxx;
                
                if(_BackgroundType == 1)
                    output.a = 1;
                
                return output;
            }
            ENDHLSL
        }
    }
}
