Shader "Hidden/PhotoTerrainEditor/SetHeight"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        // No culling or depth
        Cull Off
        ZWrite Off
        ZTest Always
        ZClip off
        Blend off

        HLSLINCLUDE
        #pragma enable_d3d11_debug_symbols
        #include "../Painting/Brush.hlsl"
        
        Texture2D    _HeightTex;
        float4       _HeightTex_TexelSize;
        SamplerState sampler_HeightTex;

        float _TargetHeight;
        float _SetHeightMode;

        float frag(BrushPixelInput i) : SV_Target
        {
            uint hw,hh;
            _HeightTex.GetDimensions(hw, hh);

            float2 screenUV = i.vertex.xy / float2(hw,hh);

            float height = _HeightTex.Sample(sampler_HeightTex, screenUV);
            // return height;
            
            float brushStrength = SampleBrush(i.worldPosition, i.uv0);

            float targetHeight = _TargetHeight;

            if(_SetHeightMode == 1 && height > targetHeight)
                return height;

            // This is used by unity terrain for some reason but I honestly don't see any good reason to
            // have to do this check to ensure strength 0 == no change (code below makes a super tiny change even with strength 0)
            if (false && brushStrength > 0)
            {
                float deltaHeight = height - targetHeight;

                // see https://www.desmos.com/calculator/880ka3lfkl
                float p = saturate(brushStrength);
                float w = (1.0f - p) / (p + 0.000001f);
//              float w = (1.0f - p*p) / (p + 0.000001f);       // alternative TODO test and compare
                float fx = clamp(w * deltaHeight, -1.0f, 1.0f);
                float g = fx * (0.5f * fx * sign(fx) - 1.0f);

                deltaHeight = deltaHeight + g / w;

                height = targetHeight + deltaHeight;
            }
            
            return lerp(height, targetHeight, brushStrength);
        }
        ENDHLSL

        Pass
        {   
            HLSLPROGRAM
            #pragma vertex BrushVert
            #pragma fragment frag
            ENDHLSL
        }
    }
}