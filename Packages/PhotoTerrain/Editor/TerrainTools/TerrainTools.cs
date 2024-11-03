using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hollow.TerrainSystem;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HollowEditor.TerrainSystem
{
internal class TerrainTools : ScriptableSingleton<TerrainTools>
{
    int[]       _selectedTerrainsInstanceIds = System.Array.Empty<int>();
    private int _lastSelectedTerrainId;
    private int _lastSelectedLayerId;
    private TerrainTool _activeTool;

    private (TerrainTool tool, TerrainToolAttribute attribute)[] _allTools;

    /// <summary> Most relevant terain instance, was selected last or something, use to get palette/max height from </summary>
    public static PhotoTerrain GetMainTerrain()
    {
        return instance._selectedTerrainsInstanceIds.Length > 0 ? EditorUtility.InstanceIDToObject(instance._selectedTerrainsInstanceIds[0]) as PhotoTerrain : null;
    }

    public static PhotoTerrain[] GetSelectedTerrains()
    {
        return instance._selectedTerrainsInstanceIds.Select((id) => EditorUtility.InstanceIDToObject(id) as PhotoTerrain).Where(t => t).ToArray();
    }

    public static bool ShouldShowTerrainTools
    {
        get
        {
            var go = Selection.activeGameObject;
            if (!go)
                return false;

            return go.GetComponent<PhotoTerrain>();
        }
    }

    internal static TerrainTool ActiveTool
    {
        get
        {
            if (instance._activeTool && ToolManager.activeToolType == instance._activeTool.GetType())
                return instance._activeTool;

            return null;
        }

        set => instance._activeTool = value;
    }

    public static int                  ToolCount                     => instance._allTools.Length;
    public static TerrainTool          GetToolAt(int          index) => instance._allTools[index].tool;
    public static TerrainToolAttribute GetToolAttributeAt(int index) => instance._allTools[index].attribute;

    public static bool TrySetTool(TerrainTool tool)
    {
        if (!tool)
            return false;

        var layer = GetMainTerrain();
        if (layer)
        {
            ToolManager.SetActiveTool(tool);
            return true;
        }

        return false;
    }

    public static bool TryGetCustomAttribute<T>(Type t, out T attribute) where T : Attribute
    {
        attribute = t.GetCustomAttribute<T>();
        return attribute is not null;
    }

    void OnEnable()
    {
        ToolManager.activeToolChanged += ToolChanged;
        Selection.selectionChanged    += SelectionChanged;
        EditorApplication.quitting    += OnDisable;

        TypeCache.TypeCollection                  terrainToolTypes = TypeCache.GetTypesDerivedFrom<TerrainTool>();
        List<(TerrainTool, TerrainToolAttribute)> toolInstanceList = new(terrainToolTypes.Count);
        for (int i = 0; i < terrainToolTypes.Count; i++)
        {
            var toolType = terrainToolTypes[i];

            if (toolType.IsAbstract ||
                !TryGetCustomAttribute<TerrainToolAttribute>(toolType, out var toolAttribute))
            {
                continue;
            }

            TerrainTool tool;
            try
            {
                tool = (TerrainTool)ScriptableObject.CreateInstance(toolType);
                tool.hideFlags |= HideFlags.DontSave;
                tool.name = toolType.Name;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                continue;
            }

            toolInstanceList.Add((tool, toolAttribute));
        }

        toolInstanceList.Sort((c0, c1) =>
        {
            int order0 = c0.Item2.SortingOrder;
            int order1 = c1.Item2.SortingOrder;
            if (order0 != order1)
                return order0.CompareTo(order1);

            return String.Compare(c0.Item1.GetType().Name, c1.Item1.GetType().Name, StringComparison.Ordinal);
        });

        _allTools = toolInstanceList.ToArray();
        for (int i = 0; i < _allTools.Length; i++)
        {
            _allTools[i].tool.LoadToolData();
        }
    }

    void OnDisable()
    {
        Selection.selectionChanged -= SelectionChanged;

        for (int i = 0; i < _allTools.Length; i++)
        {
            if (_allTools[i].tool)
                _allTools[i].tool.SaveToolData();
        }
    }

    void ToolChanged()
    {
        // A bit of a clusterfuck
        // What this means if we were painting and changed to move tool while painting
        // We should set _activeTool (backing) to null so that when we click on non terrain layer
        // and back it doesn't select paint tool
        if (ShouldShowTerrainTools && (!ActiveTool))
        {
            if (_activeTool)
                _activeTool.SaveToolData();

            _activeTool = null;
        }
    }

    void SelectionChanged()
    {
        if (_activeTool && !TrySetTool(_activeTool))
            ToolManager.RestorePreviousPersistentTool();

        var go = Selection.activeGameObject;

        _selectedTerrainsInstanceIds = System.Array.Empty<int>();
        if (go)
        {
            if (go.TryGetComponent<PhotoTerrain>(out var terrain))
            {
                _lastSelectedTerrainId = terrain.GetInstanceID();
                _selectedTerrainsInstanceIds = FindAllConnectedTerrains(terrain).Select(t => t.GetInstanceID()).ToArray();
            }
        }
    }

    List<PhotoTerrain> FindAllConnectedTerrains(PhotoTerrain root)
    {
        float3 origin = root.transform.position;
        float3 size   = root.Size;

        var allTerrains = Object.FindObjectsOfType<PhotoTerrain>();

        List<PhotoTerrain> result = new(allTerrains.Length) { root };
        for (int i = 0; i < allTerrains.Length; i++)
        {
            var t = allTerrains[i];
            if (t == root)
            {
                continue;
            }

            float3 quantizedPositon = (float3)t.transform.position - origin;
            quantizedPositon.x /= size.x;
            quantizedPositon.y /= size.y;
            quantizedPositon.z /= size.z;

            var diff = quantizedPositon - math.floor(quantizedPositon);
            if (math.all(diff < 1e-8f))
            {
                result.Add(t);
            }
        }

        return result;
    }
}
}