#pragma once

struct TerrainPatch
{
    float4 ScaleOffset;
    float2 MinMaxHeight;
    float  Deviation; // Stored in [0;1] range, meaning its relative to terrain max height!
    uint   Lod;
    int2   TexelCoordinates;
};

struct WorkingTerrainPatch
{
    float4 ScaleOffset;
    uint2  MinMaxHeight;
    uint   Deviation; // Stored in [0;1] range, meaning its relative to terrain max height!
    uint   Lod;
    uint2  TexelCoordinates;
};
#define PT_FLOAT_MAX_SAFE_INTEGER (16777215)

struct PT_Bounds
{
    float3 min;
    float3 max;
};

uint2 PT_DecodeIndex(uint vertexID)
{
    uint xID = vertexID % 17;
    uint yID = vertexID / 17;
    return uint2(xID, yID);
}
