using System.Reflection;
using Hollow.TerrainSystem;
using UnityEditor;
using UnityEngine;

namespace HollowEditor.TerrainSystem
{
[CustomEditor(typeof(PhotoTerrain))]
class PhotoTerrainEditor : Editor
{
    private bool brush_gui_foldout
    {
        get => EditorPrefs.GetBool("PT_BRUSH_HEADER", true);
        set => EditorPrefs.SetBool("PT_BRUSH_HEADER", value);
    }
    
    private bool base_layer_foldout
    {
        get => EditorPrefs.GetBool("PT_BASE_LAYER_HEADER", true);
        set => EditorPrefs.SetBool("PT_BASE_LAYER_HEADER", value);
    }
    
    private bool main_foldout
    {
        get => EditorPrefs.GetBool("PT_MAIN_HEADER", true);
        set => EditorPrefs.SetBool("PT_MAIN_HEADER", value);
    }

    private Editor cachedToolEditor;

    private TerrainTool lastActiveTool;
    private TerrainToolAttribute toolAttribute;
    
    private static int[] potOptions = new[]
    {
        256,
        512,
        1024,
        2048,
        4096
    };
    
    private static GUIContent[] potOptionsContent = new GUIContent[]
    {
        new("256x256"),
        new("512x512"),
        new("1024x1024"),
        new("2048x2048"),
        new("4096x4096"),
    };
    
    private int[] npotOptions = new[]
    {
        257,
        513,
        1025,
        2049,
        4097
    };
    
    private static GUIContent[] npotOptionsContent = new GUIContent[]
    {
        new("257x257"),
        new("513x513"),
        new("1025x1025"),
        new("2049x2049"),
        new("4097x4097"),
    };
    
    private static readonly GUIContent heightmapSizeContent = new("Heightmap & Holes Resolution");
    private static readonly GUIContent splatmapSizeContent  = new("Splat Resolution");

    public override void OnInspectorGUI()
    {
        var target = (PhotoTerrain)this.target;

        main_foldout = TerrainToolsGUI.DrawHeaderFoldout("Main", main_foldout);
        if (main_foldout)
        {
            EditorGUI.indentLevel++;
            base.OnInspectorGUI();
            EditorGUI.indentLevel--;
        }

        if (target.outputData && target.outputData.unityData)
        {   
            base_layer_foldout = TerrainToolsGUI.DrawHeaderFoldout("Terrain Data", base_layer_foldout);
            if (base_layer_foldout)
            {
                EditorGUI.indentLevel++;
                // -- SIZE
                var unityData = target.outputData.unityData;
                EditorGUI.BeginChangeCheck();
                var newSize = EditorGUILayout.Vector3Field("Size", unityData.size);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(unityData, "Change Terrain Size");
                    unityData.size = Vector3.Max(Vector3.zero, newSize);
                }
                
                var hmResolution    = target.baseLayer.heightMap.Size;
                int newHmResolution = EditorGUILayout.IntPopup(heightmapSizeContent, hmResolution, npotOptionsContent, npotOptions);
                if (newHmResolution != hmResolution)
                {
                    target.baseLayer.heightMap.RecordUndo();
                    target.baseLayer.holesMap .RecordUndo();
                    target.outputData.unityData.heightmapResolution = newHmResolution;
                    
                    target.baseLayer.heightMap.Resize(newHmResolution);
                    target.baseLayer.holesMap .Resize(newHmResolution);
                }
                
                var splatResolution = target.baseLayer.splatMap.Size;
                int newSplatResolution = EditorGUILayout.IntPopup(splatmapSizeContent, splatResolution, potOptionsContent, potOptions);

                if (newSplatResolution != splatResolution)
                {
                    target.baseLayer.splatMap.RecordUndo();
                    target.baseLayer.splatMap.Resize(newSplatResolution);
                }
                
                EditorGUI.indentLevel--;
            }
        }

        if (TerrainTools.ActiveTool)
        {
            EditorGUILayout.Space();
            //  if (lastActiveTool != TerrainTools.ActiveTool) // FIXME: Breaks on domain reload, annoying
            {
                toolAttribute = TerrainTools.ActiveTool.GetType().GetCustomAttribute<TerrainToolAttribute>();
                lastActiveTool = TerrainTools.ActiveTool;
            }

            var niceName = "Selected Toool - " + toolAttribute.DisplayName;
            brush_gui_foldout = TerrainToolsGUI.DrawHeaderFoldout(niceName, brush_gui_foldout);

            if (brush_gui_foldout)
            {
                TerrainToolsGUI.DrawActiveToolLayout(ref cachedToolEditor);
            }
        }
    }
}
}