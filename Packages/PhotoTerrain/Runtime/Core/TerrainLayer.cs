using System.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Hollow.TerrainSystem
{
[CreateAssetMenu(menuName = "PhotoTerrain/Terrain Layer")]
public class TerrainLayer : ScriptableObject, ITerrainTextureStorage
{
    public TerrainLayer() : base()
    {
        heightMap = new(this, "HeightMap", 1025, GraphicsFormat.R16_UNorm, Color.black);
        splatMap  = new(this, "SplatMap",  1024, GraphicsFormat.R16_UNorm, defaultValue: TerrainTexture.SPLAT_DEFAULT_VALUE);  // blend set to 1, background & foreground = 0

        holesMap  = new(this, "HolesMap",  1025, GraphicsFormat.R8_UNorm, Color.black);
    }

    public TerrainTexture heightMap;
    public TerrainTexture splatMap;

    public TerrainTexture holesMap;

    public TerrainTexture GetTexture(string name)
    {
        return name switch
        {
            TerrainTexture.HEIGHT => heightMap,
            TerrainTexture.SPLAT  => splatMap,
            TerrainTexture.HOLES  => holesMap,

            _ => throw new System.Exception($"Invalid terrain texture name '{name}'")
        };
    }

    [field: SerializeField] public int Version { get; set; }

    [ContextMenu("Save")]
    internal void SaveToDisk()
    {
        heightMap.SaveToDisk();
        splatMap.SaveToDisk();
        holesMap.SaveToDisk();
    }
}
}