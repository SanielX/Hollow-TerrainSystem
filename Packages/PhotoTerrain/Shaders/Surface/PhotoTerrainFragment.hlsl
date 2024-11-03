#pragma once 
#include "PhotoTerrainVertex.hlsl"
#include "../Includes/OMPV.hlsl"
#include "../Includes/VirtualTexture.hlsl"
#include "../Includes/SurfaceDebug.hlsl"

struct TriplanarSample
{
    float3 albedo;
    float3 normal;
    float4 mask;
};

static VirtualFetchInfo debug_lastFetch;
TriplanarSample SampleVirtualTexture_WithFeedback(float4 clipPos, VirtualFetchInfo vinfo)
{
    TriplanarSample o;
    
    debug_lastFetch = vinfo;
    int wh, wi;
    AVT_IndirectionTexture.GetDimensions(wh, wi);
                
    int2 virtualTexel = vinfo.virtualUV * int2(wh, wi);
    int mip0 = clamp(vinfo.mip    , 0, vinfo.maxMip);
    int mip1 = clamp(vinfo.mip + 1, 0, vinfo.maxMip);
                
    float4 indirectionSample0 = AVT_IndirectionTexture.Load(int3(virtualTexel >> mip0, mip0));
    float2 physicalUV0 = AVT_VirtualToPhysicalUV(indirectionSample0, vinfo.virtualUV);

    float4 vtSample0_0 = AVT_AlbedoCacheTexture  .Sample(AVT_CACHE_SAMPLER, physicalUV0);
    float4 vtSample1_0 = AVT_GBuffer1CacheTexture.Sample(AVT_CACHE_SAMPLER, physicalUV0);
    float4 vtSample2_0 = AVT_GBuffer2CacheTexture.Sample(AVT_CACHE_SAMPLER, physicalUV0);

    float4 indirectionSample1 = AVT_IndirectionTexture.Load(int3(virtualTexel >> mip1, mip1));
    float2 physicalUV1 = AVT_VirtualToPhysicalUV(indirectionSample1, vinfo.virtualUV);

    float4 vtSample0_1 = AVT_AlbedoCacheTexture  .Sample(AVT_CACHE_SAMPLER, physicalUV1);
    float4 vtSample1_1 = AVT_GBuffer1CacheTexture.Sample(AVT_CACHE_SAMPLER, physicalUV1);
    float4 vtSample2_1 = AVT_GBuffer2CacheTexture.Sample(AVT_CACHE_SAMPLER, physicalUV1);

    float  trilinearFrac = frac(vinfo.mip);
    float4 vtSample0 = lerp(vtSample0_0, vtSample0_1, trilinearFrac);
    float4 vtSample1 = lerp(vtSample1_0, vtSample1_1, trilinearFrac);
    float4 vtSample2 = lerp(vtSample2_0, vtSample2_1, trilinearFrac);

    o.albedo = vtSample0.rgb;
    o.normal = AVT_DecodeVirtualTextureNormal(vtSample1, vtSample2);
    o.mask.a = vtSample1.r;
    o.mask.r = vtSample2.r;
    
    AVT_StoreFeedback(clipPos.xy, vinfo.virtualUV, int(vinfo.mip));

    return o;
}

struct TerrainSurfaceInputs
{
    float4 clipPos;
    float4 worldPos;
    float2 uv;
    float  vertexHeight;

    float triBlendOffset;
    float triBlendPower;
};

struct TerrainSurface
{
    float3 albedo;
    float3 heightNormal;
    float3 normal;
    float metallic;
    float smoothness;
};

#define WTF_SAMPLER sampler_wtf_Trilinear_clamp_aniso8
SamplerState WTF_SAMPLER;

TerrainSurface SampleTerrainSurface(TerrainSurfaceInputs i)
{
    TerrainSurface surf = (TerrainSurface)0;
    
    float3 normal = SampleTerrainNormal(i.uv); // HeightToNormal(PT_Heightmap, WTF_SAMPLER, PT_Heightmap_TexelSize, i.uv, PT_TerrainInstanceSize.y);
    surf.heightNormal = normal;
    // return float4(normal, 1);
    
    TriplanarSample xSurface = (TriplanarSample)0;
    TriplanarSample ySurface = (TriplanarSample)0;
    TriplanarSample zSurface = (TriplanarSample)0;
    
    float3 blendWeights = pow(saturate(abs(normal) - i.triBlendOffset), i.triBlendPower);
    blendWeights = blendWeights / (blendWeights.x + blendWeights.y + blendWeights.z);

   // blendWeights = float3(0, 1, 0);

    if(blendWeights.x > 0)
    {
        float2 uv = i.worldPos.zy * sign(normal.x);
        OMPVSample ompv = SampleOMPVMaterialBackground(i.uv, uv, uv);
        ompv.tangentNormal.xy *= sign(normal.x);
        
        float3 normalOmpv = OMPVNormalBlend(ompv.tangentNormal, normal.zyx).zyx;
        //normalOmpv.z *= -1;

        xSurface.albedo = ompv.albedo;
        xSurface.normal = normalOmpv;
        xSurface.mask   = float4(ompv.metallic, 0, 0, ompv.smoothness);
    }
    
    if(blendWeights.y > 0)
    {
      //  #define PT_AVT_ENABLED 1
        #if PT_AVT_ENABLED
        
            VirtualFetchInfo vinfo = AVT_WorldToVirtualInfo(i.worldPos, 8);
            ySurface = SampleVirtualTexture_WithFeedback(i.clipPos, vinfo);
        
        //    if(vinfo.maxMip > 0)
        //    {
        //    }
        //    else 
        #else
            {
                // Gather surface properties
                OMPVSample ompvY = SampleOMPVMaterialGather(i.uv.xy, i.worldPos.xz, i.worldPos.xz);
                float3 normalY = OMPVNormalBlend(ompvY.tangentNormal, normal.xzy).xzy;
    
                ySurface.albedo = ompvY.albedo.rgb;
                ySurface.normal = normalY.rgb;
                ySurface.mask   = float4(ompvY.metallic, 0, 0, ompvY.smoothness);
            }
        #endif
    }
    
    if(blendWeights.z > 0)
    {
        float2 uv = i.worldPos.xy * -sign(normal.z);
        OMPVSample ompv = SampleOMPVMaterialBackground(i.uv.xy, uv, uv);
        ompv.tangentNormal.x *= -sign(normal.z);
        
        float3 normalOmpv = OMPVNormalBlend(ompv.tangentNormal, normal);
        
        zSurface.albedo = ompv.albedo;
        zSurface.normal = normalOmpv;
        zSurface.mask   = float4(ompv.metallic, 0, 0, ompv.smoothness);
    }

    surf.albedo.rgb = xSurface.albedo * blendWeights.x + ySurface.albedo * blendWeights.y + zSurface.albedo * blendWeights.z;
    surf.normal    = xSurface.normal * blendWeights.x + ySurface.normal * blendWeights.y + zSurface.normal * blendWeights.z;

    float4 msk      = xSurface.mask * blendWeights.x + ySurface.mask * blendWeights.y + zSurface.mask * blendWeights.z;
    surf.metallic   = msk.r;
    surf.smoothness = msk.a;

    #if PT_WORLD_DEBUG_MASK
        float4 debug_mask = PT_SampleDebugMask(i.worldPos);
        float3 albedoGrayscale = 0.21 * surf.albedo.r + 0.72 * surf.albedo.g + 0.07 * surf.albedo.b; // grayscale
        albedoGrayscale *= 0.5;

        surf.albedo.rgb  = lerp(surf.albedo.rgb, albedoGrayscale, 1 - debug_mask.r);
        surf.smoothness = lerp(surf.smoothness, 0, 1 - debug_mask.r);
        surf.metallic   = lerp(surf.metallic,   0, 1 - debug_mask.r);
    #endif

    return surf;
}