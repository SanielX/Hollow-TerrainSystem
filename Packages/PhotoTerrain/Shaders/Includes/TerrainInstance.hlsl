#pragma once
//#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "TerrainInstanceCommon.hlsl"
#include "TerrainHeightCommon.hlsl"

#define LOD_MAP_SIZE (65535)

// 0 - Normal bilinear sampling
// 1 - Method based on Inigo Quilez hardware interpolation - 2013, much better results
#define HIGH_QUALITY_HEIGHT_SAMPLING 1


SamplerState pthm_linear_clamp_sampler;
Texture2D PT_Heightmap;
float4    PT_Heightmap_TexelSize;

float     _PT_NO_HOLES;
Texture2D PT_Holesmap;
float4    PT_Holesmap_TexelSize;

cbuffer PhotoTerrainInstanceData
{
    float3 PT_TerrainInstancePosition;
    uint   PT_GridPatchResolution;

    float4 PT_TerrainInstanceSize;
    float4 PT_TerrainInstanceSizeRcp;
}

float3 PT_TerrainPatchWorldPosition(TerrainPatch patch)
{
    float  h = patch.MinMaxHeight.x * PT_TerrainInstanceSize.y;
    float3 p = PT_TerrainInstancePosition + float3(patch.ScaleOffset.z, h, patch.ScaleOffset.w);
    return p;
}

PT_Bounds PT_TerrainPatchBounds(TerrainPatch patch)
{
    PT_Bounds o;
    o.min = PT_TerrainPatchWorldPosition(patch);
    o.max = o.min + float3(patch.ScaleOffset.x, 0, patch.ScaleOffset.y);
    o.max.y = PT_TerrainInstancePosition.y + patch.MinMaxHeight.y * PT_TerrainInstanceSize.y;
    return o;
}

float PT_DistanceToBounds(PT_Bounds b, float3 p)
{
    float3 closestPoint = clamp(p, b.min, b.max);
    return distance(closestPoint, p);
}

float PT_WorldSpacePatchDeviation(TerrainPatch patch)
{
    return patch.Deviation * PT_TerrainInstanceSize.y;
}

struct PatchSeams
{
    uint right, left, up, down;
};

PatchSeams UnpackPatchSeams(uint seam)
{
    PatchSeams o;
    o.right = (seam >> 0 ) & 0xFF;
    o.left  = (seam >> 8 ) & 0xFF;
    o.up    = (seam >> 16) & 0xFF;
    o.down  = (seam >> 24) & 0xFF;

    return o;
}

float SampleTerrainHeightmap(float2 uv)
{
    #if HIGH_QUALITY_HEIGHT_SAMPLING
        return NormalizeUnityHeightmap(PT_SampleImproved(PT_Heightmap, pthm_linear_clamp_sampler, uv).r);
    #else
        return NormalizeUnityHeightmap(PT_Heightmap.SampleLevel(pthm_linear_clamp_sampler, uv, 0).r);
    #endif
}

float3 SampleTerrainNormal(float2 uv)
{
    // Helps a lot at the edges of tile because normal is usually smooth so it'll "extrapolate" instead of breaking completely
    uv = clamp(uv, PT_Heightmap_TexelSize.xy, 1 - PT_Heightmap_TexelSize.xy);
    const float d = 0.5;
                
    float2 du = float2(PT_Heightmap_TexelSize.x * d, 0);
    float u1 = SampleTerrainHeightmap(uv - du);
    float u2 = SampleTerrainHeightmap(uv + du);
    //	float3 tu = float3(1, u2 - u1, 0);
                
    float2 dv = float2(0, PT_Heightmap_TexelSize.y * d);
    float v1 = SampleTerrainHeightmap(uv - dv);
    float v2 = SampleTerrainHeightmap(uv + dv);
    //	float3 tv = float3(0, v2 - v1, 1);
                
    //	i.normal = cross(tv, tu);
    float3 normal = float3(u1 - u2, 1, v1 - v2);
    normal.xz *= PT_TerrainInstanceSize.y;
    return normalize(normal);
}