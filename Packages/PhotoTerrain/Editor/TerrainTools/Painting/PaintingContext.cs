using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace HollowEditor.TerrainSystem
{
[StructLayout(LayoutKind.Sequential)]
public unsafe struct BrushGPUState
{
    public float4x4 objectToWorld;
    public float4   brushBounds;

    // public float4x4 projMatrix;
    // public float4x4 viewMatrix;

    public float4 brushRotation;
    public float3 brushPosition;
    public float3 brushSize;
    public float3 brushPickingNormal;

    public uint   isValid;
    public uint   terrainInstanceId;

    public fixed float contextMatrices[16 * 8];
}
}