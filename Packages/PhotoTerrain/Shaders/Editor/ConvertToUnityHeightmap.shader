Shader "Hidden/PhotoTerrainEditor/ConvertToUnityHeightmap"
{
    Properties
    {
        _MainTex("Main Texture", 2D) = "white" {}
    }

    HLSLINCLUDE
    #include "UnityCG.cginc"

    Texture2D _MainTex;
    float4 _MainTex_ST;
    SamplerState sampler_linear_clamp;

    struct Varyings
    {
        float2 uv : TEXCOORD0;
        float4 vertex : SV_POSITION;
    };

    struct AttributesDefault
    {
        float4 vertex : Position;
        float2 texcoord : TEXCOORD0;
    };

    Varyings VertBlit(AttributesDefault v)
    {
        Varyings o;
        o.vertex = UnityObjectToClipPos(v.vertex);
        o.uv = UnityStereoScreenSpaceUVAdjust(v.texcoord, _MainTex_ST);
        return o;
    }
    
    #define maxHeightmapHeight (32766.0 / 65535.0)

    half4 FragBlit(Varyings i) : SV_Target
    {
        half4 col = _MainTex.Sample(sampler_linear_clamp, i.uv) * maxHeightmapHeight;
        return col;
    }
    ENDHLSL

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex VertBlit
            #pragma fragment FragBlit
            ENDHLSL
        }
    }
}