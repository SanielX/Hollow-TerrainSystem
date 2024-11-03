using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Hollow.TerrainSystem
{
[CreateAssetMenu]
public class AdditionalTerrainData : ScriptableObject
{
    public AdditionalTerrainData()
    {
        splatMap = new(this, "SplatMap", 1024, TerrainTexture.SPLAT_FORMAT, defaultValue: TerrainTexture.SPLAT_DEFAULT_VALUE);
    }

    public TerrainData unityData;

    [SerializeField] TerrainTexture splatMap;

    public Texture Splat => splatMap.GetLastTexture();

    public RenderTexture SplatRT
    {
        get
        {
            splatMap.EnsureRenderTarget();
            return splatMap.GetLastTexture() as RenderTexture;
        }
    }

    internal void SaveToDisk()
    {
        int h = unityData.heightmapResolution;
        unityData.DirtyHeightmapRegion(new(0, 0, h, h), TerrainHeightmapSyncControl.HeightAndLod);
        unityData.DirtyTextureRegion(TerrainData.HolesTextureName, new(0, 0, unityData.holesResolution, unityData.holesResolution), false);
        unityData.SyncHeightmap();
        //unityData.SyncTexture(TerrainData.HolesTextureName);

        splatMap.SaveToDisk();
    }
}
}