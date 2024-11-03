#pragma once
#include "PackingUtils.hlsl"
#define AVT_FEEDBACK_REGISTER u5

// 0 - default (from call of duty)
// 1 - sign (g - positive, b - negative)
// 2 - g -> value, b -> sign
#define HIGH_QUALITY_VT_NORMAL 1







struct AVTWorldCell
{
   // float4 uvRange;
    float2 virtualUv;
    float  virtualUvSize;
    
    float  derivativeScale;
    float  maxMip;
    //int    imageId;
};

struct VTIndirectionContent
{
    float2 physicalOffset; // In whole pages
    int    mip;
};

RWTexture2D<float4> AVT_FeedbackBuffer : register(AVT_FEEDBACK_REGISTER);

Texture2D AVT_IndirectionTexture;
Texture2D AVT_AlbedoCacheTexture;
Texture2D AVT_GBuffer1CacheTexture;
Texture2D AVT_GBuffer2CacheTexture;

#if TERRAIN_DEBUG_POINT_SAMPLING
    #define AVT_CACHE_SAMPLER avt_sampler_point_clamp
#else
    #define AVT_CACHE_SAMPLER avt_sampler_linear_aniso8_clamp
#endif

SamplerState AVT_CACHE_SAMPLER;


StructuredBuffer<AVTWorldCell> AVT_WorldGrid;

cbuffer AVT_WorldGridCommon_CBuffer
{
    float2 AVT_GridMinWorldPosition;
    float2 AVT_GridWorldSizeRcp;
    int2   AVT_WorldGridResolution;
    float  AVT_WorldGridCellSize;
    float  AVT_WorldGridCellSizeRcp;
};

cbuffer AVT_VirtualTextureFeedbackData
{
    uint2 PT_FeedbackBufferSize;
    uint  PT_FeedbackBufferFrame;

    // If cbuffer it might not just bind itself
    // so add useless stuff
    uint  __pt_avt_pad1;
    uint4 __pt_avt_pad2;
};

cbuffer AVT_Common_CBuffer
{
    float2 IndirectionTextureSize;
    int    IndirectionTextureSizeInt;
    float  PhysicalTextureRcpSize;

    float AVT_CacheTileSize;
    float AVT_CacheBorderSize;
    float AVT_CacheTileCountWide;

    float AVT_CacheTextureVTScale;
    // ushort CacheTileSize;     // 0x0000FFFF - Total tile size WITH border
    // byte   CacheBorderSize;   // 0x00FF0000
    // byte   CacheTileCountWide;// 0xFF000000
};

uint AVT_HashScreenPos(float2 screenPosition, const uint power, const uint powerLog2)
{
    uint2 localTilePos  = uint2(screenPosition.xy) & (power - 1);
    uint  localTileHash = localTilePos.x + (localTilePos.y << powerLog2);

    return localTileHash;
}

void AVT_StoreFeedback(float2 screenPos, float2 virtualUV, int mip)
{
    uint2 absVirtualCoord = virtualUV * IndirectionTextureSize;
    uint  screenHash      = AVT_HashScreenPos(screenPos, 8, 3);

    bool feedback_written = screenHash == PT_FeedbackBufferFrame;
    if (feedback_written)
    {
        mip = clamp(mip + 1, 0, 12);

        uint2 feedBackCoords = uint2(screenPos) >> 3;
        uint  feedback       = 0;
        feedback |= (absVirtualCoord.x & 0xFFF) << 0;
        feedback |= (absVirtualCoord.y & 0xFFF) << 12;
        feedback |= (mip & 0xF) << 24;
        feedback |= 1 << 31; // Mark feedback pixel as used

        AVT_FeedbackBuffer[feedBackCoords] = rUnpackUnorm4x8(feedback);
    }
}

float2 AVT_ClampUV(float2 uv)
{
    const float min_uv = 0; // 1.0 / 8192.0;
    return clamp(uv, 0.0, 1.0 - min_uv); // this is to avoid UV being >= 1.0, leading to jumps in physical texture
}

// Unreal Engine people claim using aniso mip calculation is closer to how hardware does it even if maxAnisoLOG2 is 1 (no aniso)
// Code based on Software Virtual Textures J.M.P. van Waveren
float AVT_CalculateMipLevelAniso(float2 dx, float2 dy, int maxAnisoLog2)
{
    float2 p = float2(dot(dx, dx), dot(dy, dy));

    float maxLOD = 0.5 * log2(max(p.x, p.y));
    float minLOD = 0.5 * log2(min(p.x, p.y));

    float anisoLOD = maxLOD - min(maxLOD - minLOD, maxAnisoLog2);
    return anisoLOD;
}

struct VirtualFetchInfo
{
    float2 sectorUV;
    float2 virtualUV;
    float  mip;
    float  maxMip;

    float2 dx, dy;

    uint gridIndex;
};

VTIndirectionContent AVT_UnpackIndirectionSample(float4 sample)
{
    VTIndirectionContent indirectionData;
    indirectionData.physicalOffset = floor(sample.xy * 255.0);
    indirectionData.mip            = int(sample.z * 255.0);

    return indirectionData;
}

VirtualFetchInfo AVT_WorldToVirtualInfo(float3 worldPosition, int aniso = 1)
{
    VirtualFetchInfo o;

    float2 localGridPos    = worldPosition.xz - AVT_GridMinWorldPosition;
    int2   sectorGridCoord = floor(localGridPos * AVT_WorldGridCellSizeRcp);
    sectorGridCoord        = min(sectorGridCoord, AVT_WorldGridResolution);

    uint gridIndex = sectorGridCoord.y * AVT_WorldGridResolution.x + sectorGridCoord.x;

    AVTWorldCell cell = AVT_WorldGrid[gridIndex];

    // Needs to be continuous
    float2 derivativeUV = worldPosition.xz * AVT_WorldGridCellSizeRcp;

    o.dx = ddx_fine(derivativeUV) * cell.derivativeScale;
    o.dy = ddy_fine(derivativeUV) * cell.derivativeScale;
    
    o.mip = AVT_CalculateMipLevelAniso(o.dx, o.dy, log2(aniso));
    // o.mip = clamp(o.mip, 0, cell.maxMip);
    o.maxMip = cell.maxMip;

    float2 worldUV = frac(derivativeUV);
       //  worldUV = AVT_ClampUV(worldUV);

    o.gridIndex = gridIndex;
    o.sectorUV  = worldUV;

    float4 uvRange = float4(cell.virtualUv, cell.virtualUv + cell.virtualUvSize);
    o.virtualUV = lerp(uvRange.xy, uvRange.zw, worldUV);

    return o;
}

float2 AVT_VirtualToPhysicalUV(float4 indirectionSample, float2 virtualCoords)
{
    VTIndirectionContent indirectionContent = AVT_UnpackIndirectionSample(indirectionSample);
    
    float cellSize   = AVT_CacheTileSize;
    float cellBorder = AVT_CacheBorderSize;
    // float cellBorder = _BorderSize; 
    float rcpPhysTexelsWide = PhysicalTextureRcpSize; // rcp(cellCount * cellSize);

    // based on: https://mrelusive.com/publications/papers/Software-Virtual-Textures.pdf
    // Software Virtual Textures - J.M.P. van Waveren

    float indirectionMipSize = float(IndirectionTextureSizeInt >> indirectionContent.mip);

    float2 virtUV = floor(virtualCoords * indirectionMipSize);

    float  scaleST        = indirectionMipSize * AVT_CacheTextureVTScale; //(cellSize - 2.0 * cellBorder) * rcpPhysTexelsWide;
    float2 virtualOffset  = (scaleST * virtUV) / indirectionMipSize;
    float2 physicalOffset = ((indirectionContent.physicalOffset * cellSize) + cellBorder) * rcpPhysTexelsWide;

    float2 bias = physicalOffset - virtualOffset;

    // TODO: Possibly precompute this?
    // But would require bigger indirection texture :P
    // Storing uint offset fits into 24bit, scale bias is whole float3 which is 96bit
    // Even storing half of that is still a lot
    float2 physicalUV = virtualCoords * scaleST.xx + bias;
    return physicalUV;
}

// It's normalized and is always up
// so we can analytically calculate it 
float3 AVT_DecodeVirtualTextureNormal(float4 gbuffer1, float4 gbuffer2)
{
    #if HIGH_QUALITY_VT_NORMAL == 2
        float x = gbuffer1.g * (gbuffer1.b > 0.5? 1 : -1);
        float z = gbuffer2.g * (gbuffer2.b > 0.5? 1 : -1);
    #elif HIGH_QUALITY_VT_NORMAL == 1
        float x = gbuffer1.g - gbuffer1.b;
        float z = gbuffer2.g - gbuffer2.b;
    #else 
        float x = gbuffer1.g * 2 - 1;
        float z = gbuffer2.g * 2 - 1;
    #endif
    
    float3 nrm = float3(x, 0, z);
    nrm.y = 1.0 - nrm.x*nrm.x - nrm.z*nrm.z;
    nrm.y = sqrt(abs(nrm.y));

    return nrm;
}

struct VTSurface
{
    float3 albedo;
    float  alpha;
    
    float3 normal; // world-space
    float smoothness;
    float metallic;
};

void PackSurfaceToVT(VTSurface surf, out float4 vtBuffer0, out float4 vtBuffer1, out float4 vtBuffer2)
{
    vtBuffer0 = vtBuffer1 = vtBuffer2 = 0;
    
    vtBuffer0.rgb = surf.albedo.rgb;

    vtBuffer1.r = surf.smoothness;
    vtBuffer2.r = surf.metallic;
    
    #if HIGH_QUALITY_VT_NORMAL == 2
        vtBuffer1.g = abs(surf.normal.x);
        vtBuffer1.b = sign(surf.normal.x) > 0? 1 : 0;
    
        vtBuffer2.g = abs(surf.normal.z);
        vtBuffer2.b = sign(surf.normal.z) > 0? 1 : 0;
    #elif HIGH_QUALITY_VT_NORMAL == 1
        vtBuffer1.g = max(0.0, surf.normal.x);
        vtBuffer1.b = abs(min(0.0, surf.normal.x));
        
        vtBuffer2.g = max(0.0, surf.normal.z);
        vtBuffer2.b = abs(min(0.0, surf.normal.z));
    #else
        // Use both so compresses better
        vtBuffer2.b = vtBuffer2.g = surf.normal.z * .5 + .5;
        vtBuffer1.b = vtBuffer1.g = surf.normal.x * .5 + .5;
    #endif
    
    vtBuffer0.a   = surf.alpha;
    vtBuffer1.a   = surf.alpha;
    vtBuffer2.a   = surf.alpha;
}