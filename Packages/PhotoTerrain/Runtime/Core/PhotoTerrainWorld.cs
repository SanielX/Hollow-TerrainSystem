using System.Collections.Generic;
using Hollow.TerrainSystem.Utility;
using UnityEngine;

namespace Hollow.TerrainSystem
{
public class PhotoTerrainWorld
{
    internal class EditingProcess
    {
        public UBounds            bounds;
        public List<PhotoTerrain> affectedTerrains = new();
    }

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
#endif
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        if (instance is not null)
            return;

        instance = new();
    }

    private static PhotoTerrainWorld instance;

    private PhotoTerrainWorld()
    {
    }

    private bool                     activeEditing;
    private List<PhotoTerrain>       activeTerrains    = new();
    private List<TerrainDecal>       activeDecals      = new();
    private EditingProcess           activeEditProcess = new();
    private BoundsTree<TerrainDecal> decalTree = new(4096 * 2); // let's assume this is reasonable

    internal double    timeSetDebugMask;
    internal Matrix4x4 worldToDebugLayerMask;
    internal Texture   worldDebugLayerMask;

    internal static int TerrainListVersion { get; set; }
    internal static int DecalListVersion   { get; set; }

    public static void RefreshDecalBoundsTree()
    {
        var activeDecals = instance.activeDecals;
        var decalTree = instance.decalTree;

        decalTree.Clear();
        for (int i = 0; i < activeDecals.Count; i++)
        {
            var decal = activeDecals[i];
            if (TerrainDecal.IsValid(decal))
            {
                decal.Refresh();
                decalTree.Insert(decal, decal.bounds);
            }
        }
    }

    public static void GetAllDecalsWithinBounds(UBounds bounds, List<TerrainDecal> decals)
    {
        instance.decalTree.FindIntersectionsWith(bounds, decals);
    }

    public static void SetDebugMask(Matrix4x4 worldToMask, Texture mask)
    {
#if UNITY_EDITOR
        instance.timeSetDebugMask = UnityEditor.EditorApplication.timeSinceStartup;
#else
            instance.timeSetDebugMask = Time.realtimeSinceStartupAsDouble;
#endif
        instance.worldToDebugLayerMask = worldToMask;
        instance.worldDebugLayerMask   = mask;
    }

    public static bool GetDebugMask(out Matrix4x4 worldToMask, out Texture maskTexture)
    {
        worldToMask = default;
        maskTexture = default;
#if UNITY_EDITOR
        if (UnityEditor.EditorApplication.timeSinceStartup > (instance.timeSetDebugMask + 0.1f))
            return false;
#else
            if(Time.realtimeSinceStartupAsDouble > (instance.timeSetDebugMask + 0.1f))
                return false;
#endif

        if (!instance.worldDebugLayerMask)
            return false;

        worldToMask = instance.worldToDebugLayerMask;
        maskTexture = instance.worldDebugLayerMask;
        return true;
    }

    internal static void RegisterActiveTerrain(PhotoTerrain terrain)
    {
        instance.activeTerrains.Add(terrain);
        TerrainListVersion++;
    }

    internal static void UnregisterActiveTerrain(PhotoTerrain terrain)
    {
        instance.activeTerrains.Remove(terrain);
        TerrainListVersion++;
    }

    internal static void RegisterDecal(TerrainDecal decal)
    {
        instance.activeDecals.Add(decal);
        DecalListVersion++;
    }

    internal static void UnregisterDecal(TerrainDecal decal)
    {
        instance.activeDecals.Remove(decal);
        DecalListVersion++;
    }

    internal static EditingProcess     ActiveEditProcess => instance.activeEditing ? instance.activeEditProcess : null;
    internal static List<PhotoTerrain> ActiveTerrains    => instance.activeTerrains;
    internal static List<TerrainDecal> ActiveDecals      => instance.activeDecals;

    /// <summary>
    /// Begins terrain editing operation, during which terrain will not recompute its LOD Errors every time it is changed,
    /// instead using LOD0 for "dirty" regions. When EndContinuousEditing is called, all terrains within bounds will recompute LOD errors
    /// </summary>
    /*public static void BeginContinuousEditing(Bounds? bounds, PhotoTerrainLayer layer)
    {
        if(instance.activeEditing)
            throw new System.Exception("Terrain is already being actively edited");

        if(bounds is null && !layer)
            throw new System.Exception("You must provide either layer or valid bounds");

        bounds ??= layer.CalculateBounds();
        instance.activeEditProcess.bounds = bounds.Value;
        instance.activeEditProcess.affectedTerrains.Clear();

        instance.activeEditing = true;

        for (int i = 0; i < ActiveTerrains.Count; i++)
        {
            var terrainBounds = ActiveTerrains[i].ComputeBounds();
            if (terrainBounds.Intersects(bounds.Value))
            {
                instance.activeEditProcess.affectedTerrains.Add(ActiveTerrains[i]);
            }
        }
    }

    public static bool EndContinuousEditing()
    {
        if(!instance.activeEditing)
            return false;

        // We need to calculate LOD errors immediately because otherwise scene view won't get repaint for a long time, which feels like lag
        CommandBuffer cmd = new();
        instance.activeEditing = false;
        for (int i = 0; i < instance.activeEditProcess.affectedTerrains.Count; i++)
        {
            instance.activeEditProcess.affectedTerrains[i].CalculateLODErrors(cmd);
        }
        instance.activeEditProcess.affectedTerrains.Clear();
        Graphics.ExecuteCommandBuffer(cmd);

        return true;
    }*/

    // public static void DisplayDebugData(PhotoTerrain layer)
    // {
    //     var dataValue = layer.GetVisualizationDebugData();
    //     if(dataValue is null)
    //         return;
    //     
    //     var data = dataValue.Value;
    //     SetDebugMask(data.worldToLayer, data.mask);
    // }
}
}