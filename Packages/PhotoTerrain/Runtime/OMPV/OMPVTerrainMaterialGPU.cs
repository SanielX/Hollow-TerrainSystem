using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hollow.TerrainSystem
{
// See OMPVCommon.hlsl
[StructLayout(LayoutKind.Sequential)]
public struct OMPVTerrainMaterialGPU
{
    public int albedoIndex;
    public int normalIndex;
    public int maskIndex;

    private float pad;

    public float4 scaleOffset;

    public float smoothness;
    public float metallic;
    public float normalStrength;
    public float heightTransition;

    public float2 heightRemap;
}
}