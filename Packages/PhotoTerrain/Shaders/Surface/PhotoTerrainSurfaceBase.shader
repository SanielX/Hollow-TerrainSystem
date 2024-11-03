Shader "Hidden/PhotoTerrain/Base"
{
    Properties
    {
    }
    SubShader
    {
        HLSLINCLUDE
//        #pragma enable_d3d11_debug_symbols
        ENDHLSL
        Pass
        {
            Name "Shadowcaster"
            Tags
            {
                "LightMode" = "ShadowCaster"
            }

            HLSLPROGRAM
            #pragma target 5.0
            #define SHADOWCASTER 1
            #include "PhotoTerrainVertex.hlsl"
            #pragma vertex vert
            #pragma fragment frag

            void frag() { }
            ENDHLSL
        }
        Pass
        {
            Tags
            {
                "LightMode" = "ScenePicking"
            }
            Name "ScenePicking"
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 5.0
            #include "PhotoTerrainVertex.hlsl"
            #pragma vertex vert
            #pragma fragment fragPicking
            #include "../Editor/TerrainPickingCommon.hlsl"

            float _TerrainInstanceID;

            float2 fragPicking(VertexOutput i) : SV_Target
            {
                float3 normal = HeightToNormal(PT_Heightmap, sampler_linear_clamp_hm, PT_Heightmap_TexelSize, i.localUV_Height.xy,
                                               PT_TerrainInstanceSize.y);
                uint nrm = EncodePickingNormal(normal);
                return float2(asfloat(nrm), _TerrainInstanceID);
            }
            ENDHLSL
        }
        Pass
        {
            Name "VBufferPass"

            Tags
            {
                "LightMode" = "VBuffer"
            }
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 5.0
            #include "PhotoTerrainVertex.hlsl"
            #pragma vertex vert
            #pragma fragment frag
            #include "../Editor/TerrainPickingCommon.hlsl"

            float _TerrainInstanceID;

            uint2 frag(VertexOutput i, uint primitiveID : SV_PrimitiveID) : SV_Target
            {
                uint nodeIndex = PhotoTerrainVisibleLODList[i.instanceID];
                return uint2(nodeIndex, primitiveID);
            }
            ENDHLSL
        }
        Pass
        {
            Name "VirtualTextureBlit"

            Tags
            {
                "LightMode" = "VirtualTextureBlit"
            }
            
            ZWrite off 
            ZTest always 
            ZClip off
            Cull off
        
            HLSLPROGRAM
            #include "../Includes/TerrainInstance.hlsl"
            #include "../Includes/OMPV.hlsl"
            #include "../Includes/VirtualTexture.hlsl"
            #pragma vertex   vert
            #pragma fragment frag
            #pragma enable_d3d11_debug_symbols
            #pragma target 5.0

            float4 _ProjectionParams;

            float4 _CellWorldPositionRange;
            float  _CellId;
            float _FlipScreenSpace;
            
            static const float3 fullscreenTrianglePositions[3] = 
            {
                float3(-1, -1, 0),
                float3(-1,  3, 0),
                float3( 3, -1, 0),
            };

            struct VertexInput
            {
                uint vertexID : SV_VertexID;
            };
            
            struct VertexOutput
            {
                float4 clipPos        : SV_POSITION;
                float2 screenPosition : VAR_SCREENPOS;
            };

            VertexOutput vert(VertexInput i)
            {
                VertexOutput o;
                o.clipPos = float4(fullscreenTrianglePositions[i.vertexID], 1);
                o.screenPosition = (o.clipPos.xy + 1) * 0.5;
                
                #if UNITY_UV_STARTS_AT_TOP
                    o.screenPosition =  o.screenPosition * float2(1.0, -1.0) + float2(0.0, 1.0);
                #endif
                
                if(_FlipScreenSpace > 0)
                {
                    o.screenPosition.y = 1.0 - o.screenPosition.y;
                }
                
                return o;
            }
            
            struct PixelOutput
            {
                float4 albedo   : SV_Target0;
                float4 gbuffer1 : SV_Target1;
                float4 gbuffer2 : SV_Target2;
            };

            PixelOutput frag(VertexOutput i)
            {
                PixelOutput o = (PixelOutput)0;
                float2 pos = lerp(_CellWorldPositionRange.xy, _CellWorldPositionRange.zw, i.screenPosition);
                float2 localUv = (pos - PT_TerrainInstancePosition.xz) * PT_TerrainInstanceSizeRcp.xz; 
                
                OMPVSample ompv = SampleOMPVMaterialGather(localUv, pos, pos);

                float3 normal = SampleTerrainNormal(localUv);
                normal = normalize(OMPVNormalBlend(ompv.tangentNormal, normal.xzy).xzy);

                VTSurface surf;
                surf.albedo = ompv.albedo.rgb;
                surf.alpha = 1;
                surf.smoothness = ompv.smoothness;
                surf.metallic = ompv.metallic;
                surf.normal = normal;
                PackSurfaceToVT(surf, o.albedo, o.gbuffer1, o.gbuffer2);

                return o;
            }

            ENDHLSL
        }
    }
}