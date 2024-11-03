Shader "PhotoTerrain/Editor/MasterLayerBlit"
{
    Properties
    {
        _BlendOp("Blend", float) = 0
    }
    SubShader
    {
        ZWrite Off
        ZTest Always
        Cull off
        ZClip off
        
        HLSLINCLUDE
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv     : TEXCOORD0;
            };

            Texture2D    _MainTex;
            SamplerState sampler_MainTex;
            
            Texture2D    _MaskTex;
            SamplerState sampler_MaskTex;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv     = v.uv;
                
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float4 color = _MainTex.Sample(sampler_MainTex, i.uv);
                float  mask  = _MaskTex.Sample(sampler_MainTex, i.uv).r;
                return float4(color.rgb, mask);
            }
            
            float4 frag_max (v2f i) : SV_Target
            {
                float4 color = _MainTex.Sample(sampler_MainTex, i.uv);
                float  mask  = _MaskTex.Sample(sampler_MainTex, i.uv).r;
                return color * mask;
            }
        ENDHLSL
        
        Pass
        {
            BlendOp Add
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
        }

        Pass
        {
            BlendOp Max
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag_max
            ENDHLSL
        }
    }
}
