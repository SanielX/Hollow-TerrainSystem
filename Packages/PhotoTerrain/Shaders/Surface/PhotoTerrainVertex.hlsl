#pragma once

#include "PhotoTerrainSurface.hlsl"
#include "../Includes/TerrainInstance.hlsl"
#include "../Includes/TerrainHeightCommon.hlsl"
// #include "Packages/com.hollow.edrp/Shaders/Includes/MaterialShading.hlsl"
// #include "Packages/com.hollow.edrp/Shaders/Includes/CameraNormals.hlsl"

#ifndef UNITY_MATRIX_VP
    #define UNITY_MATRIX_VP unity_MatrixVP
    float4x4 UNITY_MATRIX_VP;
#endif 

StructuredBuffer<TerrainPatch> PhotoTerrainLODList;
StructuredBuffer<uint>         PhotoTerrainVisibleLODList;
StructuredBuffer<uint>         PhotoTerrainSeamList;
Texture2D<uint>                LODLevelMap;

struct VertexOutput
{
    uint instanceID : SV_InstanceID;

    float4 clipPos : SV_POSITION;
    float3 localUV_Height : VAR_UV0;
    uint   lodLevel : VAR_LODLEVEL;
    float3 worldPos : VAR_WORLDPOS;
};

struct VertexInput
{
    uint instanceID : SV_InstanceID;
    uint vertexID : SV_VertexID;
};
#ifndef UNITY_STANDARD_UTILS_INCLUDED
    float4 unity_LightShadowBias;
#endif

VertexOutput vert(VertexInput i)
{
    VertexOutput o;

    uint2 id  = PT_DecodeIndex(i.vertexID);
    uint  xID = id.x;
    uint  yID = id.y;

    /* Decoding vertex ID could also look like this if we were to encode grid position into index buffer
     * I think this should be much better than doing that division
     xID = (vertexID) & 0xFFFF
     yID = (vertexID >> 16) & 0xFFFF
     */

    uint         nodeIndex = PhotoTerrainVisibleLODList[i.instanceID];
    PatchSeams   seams     = UnpackPatchSeams(PhotoTerrainSeamList[nodeIndex]);
    TerrainPatch patchInfo = PhotoTerrainLODList[nodeIndex];

    // Stitch vertices
    if (yID == (PT_GridPatchResolution - 1) && seams.up > 0)
    {
        xID -= xID % (1 << seams.up);
    }
    else if (yID == 0 && seams.down > 0)
    {
        xID -= xID % (1 << seams.down);
    }
    else if (xID == (PT_GridPatchResolution - 1) && seams.right > 0)
    {
        yID -= yID % (1 << seams.right);
    }
    else if (xID == 0 && seams.left > 0)
    {
        yID -= yID % (1 << seams.left);
    }

    float2 coord = float2(xID, yID) / float(PT_GridPatchResolution - 1) * patchInfo.ScaleOffset.xy + patchInfo.ScaleOffset.zw;

    float2 localUV = coord * PT_TerrainInstanceSizeRcp.xz;
    float  height  = NormalizeUnityHeightmap(PT_Heightmap.SampleLevel(sampler_point_clamp_hm, localUV, 0).r);

    bool clipVertex = PT_Holesmap.SampleLevel(sampler_point_clamp_hm, localUV, 0).r < .5;
    if(_PT_NO_HOLES > 0)
        clipVertex = false;
    
    float4 worldPos = float4(coord.x, 0, coord.y, 1);
    worldPos.xyz += PT_TerrainInstancePosition.xyz;
    worldPos.y += height * PT_TerrainInstanceSize.y;

    o.clipPos        = mul(UNITY_MATRIX_VP, float4(worldPos.xyz, 1));
    o.localUV_Height = float3(localUV, height);
    o.lodLevel       = patchInfo.Lod;
    o.worldPos       = worldPos.xyz;
    o.instanceID     = i.instanceID;
    #if SHADOWCASTER 
        #if defined(UNITY_REVERSED_Z)
            float clamped = min(o.clipPos.z, o.clipPos.w*1);
        #else
            float clamped = max(o.clipPos.z, o.clipPos.w*0);
        #endif
        if(unity_LightShadowBias.y > 0)
            o.clipPos.z = min(1.0, o.clipPos.z);
        o.clipPos.z += max(-1, min(unity_LightShadowBias.x / o.clipPos.w, 0));
    #endif
    
    if (clipVertex) o.clipPos /= 0;

    return o;
}
