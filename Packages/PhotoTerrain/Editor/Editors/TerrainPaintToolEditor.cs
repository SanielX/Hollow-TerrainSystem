using System;
using UnityEditor;
using UnityEngine;

namespace HollowEditor.TerrainSystem
{
[CustomEditor(typeof(TerrainPaintTool), editorForChildClasses: true)]
public class TerrainPaintToolEditor : TerrainToolEditor
{
    private Editor brushPresetGUI;
    
    private bool preset_header
    {
        get => EditorPrefs.GetBool("PT_BRUSH_PRESET_EXPANDED", true);
        set => EditorPrefs.SetBool("PT_BRUSH_PRESET_EXPANDED", value);
    }

    protected virtual void OnEnable()
    {
        var inspector = TerrainToolsGUI.inspector_type;
        var allInspectors = Resources.FindObjectsOfTypeAll(inspector);
        for (int i = 0; i < allInspectors.Length; i++)
        {
            var window = allInspectors[i] as EditorWindow;
            window.wantsMouseMove = true;
        }
    }
    protected virtual void OnDisable() {}
    
    public override void OnInspectorGUI()
    {
        var target = (TerrainPaintTool)this.target;
        
        if(Event.current.isMouse) Repaint();
        
        base.OnInspectorGUI();
        
        preset_header = TerrainToolsGUI.DrawHeaderFoldout("Brush", preset_header);
        if (preset_header)
        {
            Editor.CreateCachedEditor(target.BrushPreset, null, ref brushPresetGUI);

            EditorGUILayout.Space();
            brushPresetGUI.OnInspectorGUI();
        }
    }
}
}