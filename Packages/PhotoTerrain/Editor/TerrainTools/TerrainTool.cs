using System.Reflection;
using Hollow.Extensions;
using Hollow.TerrainSystem;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UIElements;

namespace HollowEditor.TerrainSystem
{
public enum TerrainToolSupport
{
    NotAvailable,
    Available,
    Disabled,
}

[System.Serializable]
public abstract class TerrainTool : EditorTool
{
    public const int HeightBrushToolOrder = 5_500;
    public const int SplatBrushToolOrder  = 8_500;
    public const int MaskBrushToolOrder   = 10500;

    public override bool IsAvailable()
    {
        return TerrainTools.ShouldShowTerrainTools;
    }

    public override void OnActivated()
    {
        base.OnActivated();
        TerrainTools.ActiveTool = this;
        Selection.selectionChanged += SelectionChanged;
        SelectionChanged();
    }

    public override void OnWillBeDeactivated()
    {
        base.OnWillBeDeactivated();
        Selection.selectionChanged -= SelectionChanged;
    }

    void SelectionChanged()
    {
        if (!TerrainTools.ShouldShowTerrainTools)
        {
            ToolManager.RestorePreviousPersistentTool();
        }
    }

    public virtual Texture2D GetIcon() => EditorGUIUtility.IconContent("CustomTool").image as Texture2D;

    protected internal virtual void SaveToolData()
    {
        if (!name.Contains("(Terrain tool)"))
            return;

        var attribute = GetType().GetCustomAttribute<TerrainToolAttribute>();
        Assert.IsTrue(attribute != null, "terrain tool attribute != null (when saving tool data)");
        string json = EditorJsonUtility.ToJson(this);

        Assert.IsFalse(json.IsNullOrEmpty(), "json.IsNullOrEmpty()");

        EditorPrefs.SetString(attribute.ID + "_TerrainToolData", json);
    }

    protected internal virtual void LoadToolData()
    {
        if (!name.Contains("(Terrain tool)"))
            return;

        var attribute = GetType().GetCustomAttribute<TerrainToolAttribute>();
        var json      = EditorPrefs.GetString(attribute.ID + "_TerrainToolData");
        if (!json.IsNullOrEmpty())
        {
            EditorJsonUtility.FromJsonOverwrite(json, this);
        }
    }

    protected internal virtual VisualElement CreateHelpPopupContent()
    {
        return null;
    }

    /// <summary>
    /// Automagically convert world height to [0;1] range
    /// </summary>
    public float ConvertToLocalHeight(float worldHeight)
    {
        var terrain = TerrainTools.GetMainTerrain();
        if (terrain && terrain.IsValidInstance())
        {
            float localHeight = worldHeight - terrain.transform.position.y;
            return Mathf.Clamp01(localHeight / terrain.MaxHeight);
        }
        else
        {
            return Mathf.Clamp01(worldHeight / 1024.0f);
        }
    }

    public Vector2 GetActiveWorldHeightRange()
    {
        var terrain = UnityEngine.Object.FindObjectOfType<PhotoTerrain>();
        if (terrain && terrain.IsValidInstance())
        {
            var pos = terrain.transform.position;
            return new(pos.y, pos.y + terrain.MaxHeight);
        }
        else
        {
            return new(0, 100f);
        }
    }

    protected static Material CreateEditorMaterial(Shader shader)
    {
        if (!shader)
            return null;

        Material mat = new(shader) { hideFlags = HideFlags.DontSave };
        return mat;
    }
}
}