Shader "Hidden/PhotoTerrain/PaintOMPVSplatMap"
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

        HLSLINCLUDE
        #pragma enable_d3d11_debug_symbols
        
        #include "../Painting/Brush.hlsl"
        #include "../../Includes/OMPV.hlsl"

        Texture2D<float> _PreviousSplat;
        
        float _TargetForegroundIndex;
        float _TargetBackgroundIndex;

        float _BlendPaintMode;

        float frag(BrushPixelInput i) : SV_Target
        {
            float alphaNoWeight = SampleBrushNoWeight(i.worldPosition, i.uv0);
            float alpha = alphaNoWeight * PT_Brush_Weight;
            //clip(alpha - 1e-5);

            uint w, h;
            _PreviousSplat.GetDimensions(w, h);
            
            float2 screenUV = i.vertex.xy / float2(w,h);
            float splatValue = _PreviousSplat.SampleLevel(ompv_sampler_point_clamp, screenUV, 0);

            OMPVSplatSample previousSample = DecodeOMPVSplatSample(splatValue);

            if(alphaNoWeight > 0 && _TargetBackgroundIndex >= 0)
                previousSample.backgroundLayer = _TargetBackgroundIndex;
            
            if(alphaNoWeight > 0 && _TargetForegroundIndex >= 0)
                previousSample.foregroundLayer = _TargetForegroundIndex;

            if(_BlendPaintMode == 0) // Exact
            {
                previousSample.blend = lerp(previousSample.blend, PT_Brush_Weight, alphaNoWeight);
            }
            else if(_BlendPaintMode == 1) // Progressive_add
            {
                previousSample.blend += saturate(alpha);
            }
            else if(_BlendPaintMode == 2)// Progressive_reduce
            {
                previousSample.blend -= saturate(alpha);
            }

            previousSample.blend = saturate(previousSample.blend);

            return EncodeOMPVSplatSample(previousSample);
        }
        ENDHLSL

        Pass
        {
            Blend One Zero
            
            HLSLPROGRAM
            #pragma vertex BrushVert
            #pragma fragment frag
            ENDHLSL
        }
    }
}