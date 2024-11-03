Shader "Hidden/PTEditor/BrushRegionBlit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        ZTest always
        ZClip off
        ZWrite off
        Cull off

        Pass
        {
            Name "Copy To Brush Staging"
            
            HLSLPROGRAM
            #pragma enable_d3d11_debug_symbols
            
            #pragma vertex vert
            #pragma fragment frag
            #include "PaintingCommon.hlsl"

            Texture2D TargetTile;
            float4    TargetTile_TexelSize;
            float4    TargetTileBounds; // center, size
            float     UseUnityVPMatrix;
            float4x4  unity_MatrixVP;

            SamplerState point_clamp_sampler;
            
            StructuredBuffer<BrushState> BrushStateBuffer;
            
            struct VertexOutput
            {
                float4 clipPos  : SV_Position;
                float3 worldPos : VAR_WORLDPOS;
                float2 uv       : VAR_UV;
            };

            VertexOutput vert(float4 vertex : POSITION, float2 uv : TEXCOORD0)
            {
                VertexOutput o;

                float3 origin = float3(TargetTileBounds.x, 0, TargetTileBounds.y);
                float3 size   = float3(TargetTileBounds.z, 1, TargetTileBounds.w);

                float4x4 vpMatrix = UseUnityVPMatrix > 0? unity_MatrixVP : BrushStateBuffer[0].viewProjMatrix[0]; 
                o.worldPos = vertex * size + origin;
                o.clipPos  = mul(vpMatrix, float4(o.worldPos, 1));
                o.uv       = uv;
                
                return o;
            }

            float2 unlerp(float2 a, float2 b, float2 x)
            {
                return (x - a) / (b - a);
            }

            float4 frag(VertexOutput i) : SV_Target
            {
                float2 tileMin = TargetTileBounds.xy - TargetTileBounds.zw*0.5;
                float2 tileMax = TargetTileBounds.xy + TargetTileBounds.zw*0.5;
                // float2 texelCoord = unlerp(tileMin, tileMax, i.worldPos.xz) * TargetTile_TexelSize.zw;  //i.uv * TargetTile_TexelSize.zw;
                float2 texelCoord = i.uv * TargetTile_TexelSize.zw;
                uint2  texel = floor(texelCoord);
                
                return TargetTile.Load(int3(texel, 0));
            }
            
            ENDHLSL
        }

        Pass
        {
            Name "Copy Staging To Tiles"
            
            HLSLPROGRAM
            #pragma enable_d3d11_debug_symbols
            
            #pragma vertex vert
            #pragma fragment frag
            #include "PaintingCommon.hlsl"

            float4x4  region_MatrixVP;
            
            Texture2D BrushStaging;
            float4    BrushStaging_TexelSize;

            SamplerState point_clamp_sampler;
            
            StructuredBuffer<BrushState> BrushStateBuffer;
            
            struct VertexOutput
            {
                float4 clipPos  : SV_Position;
                float3 worldPos : VAR_WORLDPOS;
                float2 uv       : VAR_UV;
            };

            VertexOutput vert(float4 vertex : POSITION, float2 uv : TEXCOORD0)
            {
                VertexOutput o;
                BrushState brush = BrushStateBuffer[0];

                float2 brush_origin = (brush.brushBounds.xy + brush.brushBounds.zw) / 2.0;
                float2 brush_size   = (brush.brushBounds.zw - brush.brushBounds.xy);
                float3 origin = float3(brush_origin.x, 0, brush_origin.y);
                float3 size   = float3(brush_size.x,   1, brush_size.y);

                o.worldPos = vertex * size + origin;
                o.clipPos  = mul(region_MatrixVP, float4(o.worldPos, 1));
                o.uv       = uv;
                
                return o;
            }

            float4 frag(VertexOutput i) : SV_Target
            {
                float2 texelCoord = i.uv * BrushStaging_TexelSize.zw;
                uint2  texel = floor(texelCoord);
                
                return BrushStaging.Load(int3(texel, 0));
            }
            
            ENDHLSL
        }
    }
}
