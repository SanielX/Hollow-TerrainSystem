using UnityEngine;

namespace Hollow.TerrainSystem
{
internal class TerrainResources : ScriptableObject
{
    private static TerrainResources _instance;

    public static TerrainResources Instance
    {
        get
        {
            if (_instance)
                return _instance;

            return _instance = Resources.Load<TerrainResources>("PT/RuntimeResources");
        } 
    }

    private Material _brushRegionCopyMaterial;

    public Material BrushRegionCopyMaterial
    {
        get
        {
            if (!_brushRegionCopyMaterial) _brushRegionCopyMaterial = new(BrushRegionCopyShader);
            return _brushRegionCopyMaterial;
        }
    }

    public Shader           BrushRegionCopyShader;
    public ComputeShader    TerrainLODErrorCalculationShader;
    public ComputeShader    TerrainLODCullingShader;
    public ComputeShader    PopulateIndirectionMapShader;
    public PhotoTerrainMesh PatchMesh;
    public Shader           DebugHeightShader;

    public Mesh             LayerCubeMesh;
    public Material         BrushPreviewMaterial;

    public Shader           PhotoToUnityHeightmapBlitter;
#if UNITY_EDITOR
    public ComputeShader    PickingShader;
    public ComputeShader    BrushUpdateShader;
#endif
}
}