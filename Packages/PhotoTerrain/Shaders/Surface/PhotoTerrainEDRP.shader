Shader "PhotoTerrain/PTerrain Surface (EDRP)"
{
    Properties
    {
        _BlendOffset("Tri Blend Offset", float) = 0
        _BlendPower ("Tri Blend Power", float) = 1
    }
    SubShader
    {
        HLSLINCLUDE

        #pragma target 5.0
        #include "PhotoTerrainVertex.hlsl"
        
        ENDHLSL
        Pass
        {
            Tags
            {
                "LightMode" = "EDRPPrepass"
                "RenderPipeline" = "EDRP"
            }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            [earlydepthstencil]
            float4 frag(VertexOutput i) : SV_Target
            {
                float3 normal = HeightToNormal(PT_Heightmap, sampler_linear_clamp_hm, PT_Heightmap_TexelSize, i.localUV_Height.xy,
                                               PT_TerrainInstanceSize.y);
                return EncodeNormalsGBuffer(normal, 0);
            }
            ENDHLSL
        }
        Pass
        {
            Tags
            {
                "LightMode" = "EDRPForward"
                "RenderPipeline" = "EDRP"
            }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ PT_AVT_ENABLED
            #pragma multi_compile _ SCREEN_SPACE_SHADOWS
            #pragma multi_compile _ PT_WORLD_DEBUG_MASK
            
            #define SHADER_PASS_FORWARD 1
            #include "PhotoTerrainFragment.hlsl"
            #include "../Includes/SurfaceDebug.hlsl"

            float _BlendOffset;
            float _BlendPower;

            [earlydepthstencil]
            float4 frag(VertexOutput i) : SV_Target
            {
                Surface surf = DefaultSurface();

                float3 normal = HeightToNormal(PT_Heightmap, LINEAR_CLAMP_SAMPLER, PT_Heightmap_TexelSize, i.localUV_Height.xy, PT_TerrainInstanceSize.y);
                // return float4(normal, 1);
                
                TriplanarSample xSurface = (TriplanarSample)0;
                TriplanarSample ySurface = (TriplanarSample)0;
                TriplanarSample zSurface = (TriplanarSample)0;
                
                float3 blendWeights = pow(saturate(abs(normal) - _BlendOffset), _BlendPower);
                blendWeights = blendWeights / (blendWeights.x + blendWeights.y + blendWeights.z);

                if(blendWeights.x > 0)
                {
                    float2 uv = i.worldPos.zy * sign(normal.x);
                    OMPVSample ompv = SampleOMPVMaterialBackground(i.localUV_Height.xy, uv, uv);
                    ompv.tangentNormal.xy *= sign(normal.x);
                    
                    float3 normalOmpv = OMPVNormalBlend(ompv.tangentNormal, normal.zyx).zyx;
                    //normalOmpv.z *= -1;

                    xSurface.albedo = ompv.albedo;
                    xSurface.normal = normalOmpv;
                    xSurface.mask   = float4(ompv.metallic, 0, 0, ompv.smoothness);
                }
                
                if(blendWeights.y > 0)
                {
                    #if PT_AVT_ENABLED
                        ySurface = SampleVirtualTexture_WithFeedback(i.clipPos, i.worldPos);
                    #else
                        // Gather surface properties
                        OMPVSample ompvY = SampleOMPVMaterialGather(i.localUV_Height.xy, i.worldPos.xz, i.worldPos.xz);
                        float3 normalY = OMPVNormalBlend(ompvY.tangentNormal, normal.xzy).xzy;
    
                        ySurface.albedo = ompvY.albedo.rgb;
                        ySurface.normal = normalY.rgb;
                        ySurface.mask   = float4(ompvY.metallic, 0, 0, ompvY.smoothness);
                    #endif
                }
                
                if(blendWeights.z > 0)
                {
                    float2 uv = i.worldPos.xy * -sign(normal.z);
                    OMPVSample ompv = SampleOMPVMaterialBackground(i.localUV_Height.xy, uv, uv);
                    ompv.tangentNormal.x *= -sign(normal.z);
                    
                    float3 normalOmpv = OMPVNormalBlend(ompv.tangentNormal, normal);
                    
                    zSurface.albedo = ompv.albedo;
                    zSurface.normal = normalOmpv;
                    zSurface.mask   = float4(ompv.metallic, 0, 0, ompv.smoothness);
                }

                surf.color.rgb = xSurface.albedo * blendWeights.x + ySurface.albedo * blendWeights.y + zSurface.albedo * blendWeights.z;
                surf.normal    = xSurface.normal * blendWeights.x + ySurface.normal * blendWeights.y + zSurface.normal * blendWeights.z;

                float4 msk      = xSurface.mask * blendWeights.x + ySurface.mask * blendWeights.y + zSurface.mask * blendWeights.z;
                surf.metallic   = msk.r;
                surf.smoothness = msk.a;

                #if PT_WORLD_DEBUG_MASK
                    float4 debug_mask = PT_SampleDebugMask(i.worldPos);
                    float3 albedoGrayscale = 0.21 * surf.color.r + 0.72 * surf.color.g + 0.07 * surf.color.b;
                    albedoGrayscale *= 0.5;

                    surf.color.rgb  = lerp(surf.color.rgb, albedoGrayscale, 1 - debug_mask.r);
                    surf.smoothness = lerp(surf.smoothness, 0, 1 - debug_mask.r);
                    surf.metallic   = lerp(surf.metallic,   0, 1 - debug_mask.r);
                #endif 

              //  surf.normal = normal;
                
                ShadingInputs si;
                si.clipPos         = i.clipPos;
                si.vertexNormal    = normal;
                si.worldPos        = float4(i.worldPos, 1);
                float3 shadedPixel = ShadeSimple(si, surf, false); // Apply lighting and everything else

               // return unlerp(PT_Heightmap.SampleLevel(POINT_CLAMP_SAMPLER, i.localUV_Height.xy, 0).xxxx, -0.01, 0.01);
                return float4(shadedPixel, 1);
            }
            ENDHLSL
        }

        UsePass "Hidden/PhotoTerrain/Base/VBUFFERPASS"
        UsePass "Hidden/PhotoTerrain/Base/SCENEPICKING"
        UsePass "Hidden/PhotoTerrain/Base/SHADOWCASTER"
    }
}