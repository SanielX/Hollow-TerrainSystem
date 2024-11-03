Shader "Hidden/PhotoTerrainEditor/BrushPreviewDecal"
{
    Properties 
    {
        [HDR] _Color("Color", color) = (1,1,1,1)
    }
    SubShader
    {
        Tags
        {
            "RenderQueue"="5000"
        }

        Pass
        {
            Name "Normal"
            Cull off
            ZTest Always 
            ZWrite Off
            
           // BlendOp Sub
    	    Blend SrcAlpha OneMinusSrcAlpha
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0
            #pragma enable_d3d11_debug_symbols
            #define BLEND_OP_MULTIPLY 1
            
            #include "BrushPreview.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "Quantized"
            Cull off
            ZTest Always 
            ZWrite Off
    	    Blend SrcAlpha OneMinusSrcAlpha
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0
            #pragma enable_d3d11_debug_symbols
            #define QUANTIZED_PAINTING 1
            #include "BrushPreview.hlsl"
            ENDHLSL
        }
    }
}