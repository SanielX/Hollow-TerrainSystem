Shader "Hidden/PhotoTerrainEditor/PaintHeight"
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
        Blend One One

        HLSLINCLUDE

        #pragma enable_d3d11_debug_symbols
        #include "../Painting/Brush.hlsl"

        float frag(BrushPixelInput i) : SV_Target
        {
            float alpha = SampleBrush(i.worldPosition, i.uv0);
            //return 0;
            return alpha;
        }
        ENDHLSL

        Pass
        {
            Name "Additive"
            BlendOp Add
            
            HLSLPROGRAM
            #pragma vertex BrushVert
            #pragma fragment frag
            ENDHLSL
        }
        Pass
        {
            Name "Subtractive"
            Blend One One
            BlendOp RevSub
            
            HLSLPROGRAM
            #pragma vertex BrushVert
            #pragma fragment frag
            ENDHLSL
        }
    }
}