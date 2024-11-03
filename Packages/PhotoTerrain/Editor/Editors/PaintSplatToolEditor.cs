using Hollow.TerrainSystem;
using UnityEditor;
using UnityEngine;

namespace HollowEditor.TerrainSystem
{
[CustomEditor(typeof(PaintSplatTool))]
public class PaintSplatToolEditor : TerrainPaintToolEditor
{
    private Texture[] toolbarTextures;
    private SerializedObject paletteSerialized;

    private bool material_header
    {
        get => EditorPrefs.GetBool("PT_SPLAT_MAT_HEADER", true);
        set => EditorPrefs.SetBool("PT_SPLAT_MAT_HEADER", value);
    }

    protected override void OnEnable()
    {
        useBaseInspectorGUI = false;
        base.OnEnable();
        toolbarTextures = new Texture[2];
    }

    public override void OnInspectorGUI()
    {
        var target = (PaintSplatTool)this.target;

        // -- DRAW TOOLBAR
        EditorGUILayout.BeginHorizontal();

        GUILayout.FlexibleSpace(); // -- space

        EditorGUI.BeginChangeCheck();

        toolbarTextures[0] = target.GetBlendModeIcon();
        toolbarTextures[1] = target.GetPaintTargetIcon();

        int clickedIndex = GUILayout.Toolbar(-1, toolbarTextures, (GUIStyle)"Command");
        if (EditorGUI.EndChangeCheck())
        {
            if (clickedIndex == 0)
            {
                target.ChangeBlendMode();
            }
            else
            {
                target.ChangePaintTarget();
            }
        }

        GUILayout.FlexibleSpace(); // -- space

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5f);
        TerrainToolsGUI.DrawSplitter();
        EditorGUILayout.Space(5f);

        // -- DRAW PALETTE
        int index = target.paintTarget == PaintSplatTool.PaintTarget.Foreground ? target.foregroundIndex : target.backgroundIndex;
        var palette = TerrainTools.GetMainTerrain().palette;
        int newIndex = TerrainToolsGUI.DrawPalette(palette, index);

        if (newIndex != index)
        {
            Undo.RecordObject(target, "Change paint target");
            if (target.paintTarget == PaintSplatTool.PaintTarget.Foreground)
            {
                target.foregroundIndex = newIndex;
            }
            else
            {
                target.backgroundIndex = newIndex;
            }
        }

        // -- DRAW MATERIAL SETTINGS
        if (index >= 0 && index < palette.materials.Length)
        {
            material_header = TerrainToolsGUI.DrawHeaderFoldout(palette.materials[index].albedo ? palette.materials[index].albedo.name : $"Material {index}", material_header);
            if (material_header)
            {
                EnsureSerializedPalette(palette);
                var materialsArrayProp = paletteSerialized.FindProperty(nameof(TerrainMaterialPalette.materials));
                var selectedMat = materialsArrayProp.GetArrayElementAtIndex(index);
                EditorGUILayout.Space();
                EditorGUILayout.PropertyField(selectedMat);
                paletteSerialized.ApplyModifiedProperties();
            }
        }

        TerrainToolsGUI.DrawSplitter();

        base.OnInspectorGUI();
    }

    void EnsureSerializedPalette(TerrainMaterialPalette palette)
    {
        if (paletteSerialized is null || paletteSerialized.targetObject != palette)
        {
            paletteSerialized = new(palette);
        }

        paletteSerialized.Update();
    }
}
}