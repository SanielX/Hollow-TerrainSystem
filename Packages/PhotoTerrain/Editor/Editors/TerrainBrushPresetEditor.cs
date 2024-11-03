using Hollow;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace HollowEditor.TerrainSystem
{
[CustomEditor(typeof(TerrainBrushPreset))]
public class TerrainBrushPresetEditor : Editor
{
    private RenderTexture preview;
    private CommandBuffer cmd;

    private SerializedProperty sizeProp;
    private SerializedProperty opacityProp;
    private SerializedProperty hardnessProp;
    private SerializedProperty rotateProp;
    private SerializedProperty radiusProp;
    private SerializedProperty flipXProp;
    private SerializedProperty flipYProp;
    private SerializedProperty jitterProp;

    private SerializedProperty maskProp;
    private SerializedProperty maskScaleOffsetProp;
    private SerializedProperty maskBrightnessProp;
    private SerializedProperty maskContrastProp;
    private SerializedProperty maskIsWorldSpaceProp;

    void OnEnable()
    {
        preview = new(128, 128, GraphicsFormat.R8G8B8A8_UNorm, GraphicsFormat.None);
        preview.hideFlags |= HideFlags.DontSave;
        preview.Create();

        cmd = new();

        sizeProp     = serializedObject.FindProperty(nameof(TerrainBrushPreset.size));
        opacityProp  = serializedObject.FindProperty(nameof(TerrainBrushPreset.opacity));
        hardnessProp = serializedObject.FindProperty(nameof(TerrainBrushPreset.hardness));
        radiusProp   = serializedObject.FindProperty(nameof(TerrainBrushPreset.radius));
        rotateProp   = serializedObject.FindProperty(nameof(TerrainBrushPreset.rotate));
        flipXProp    = serializedObject.FindProperty(nameof(TerrainBrushPreset.flipX));
        flipYProp    = serializedObject.FindProperty(nameof(TerrainBrushPreset.flipY));
        jitterProp   = serializedObject.FindProperty(nameof(TerrainBrushPreset.jitter));

        jitterProp   = serializedObject.FindProperty(nameof(TerrainBrushPreset.jitter));

        maskProp             = serializedObject.FindProperty(nameof(TerrainBrushPreset.mask));
        maskScaleOffsetProp  = serializedObject.FindProperty(nameof(TerrainBrushPreset.maskScaleOffset));
        maskBrightnessProp   = serializedObject.FindProperty(nameof(TerrainBrushPreset.maskBrightness));
        maskContrastProp     = serializedObject.FindProperty(nameof(TerrainBrushPreset.maskContrast));
        maskIsWorldSpaceProp = serializedObject.FindProperty(nameof(TerrainBrushPreset.maskIsWorldSpace));
    }

    void OnDisable()
    {
        ObjectUtility.SafeDestroy(preview);
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        float kSingleLine      = EditorGUIUtility.singleLineHeight;
        float kVerticalSpacing = EditorGUIUtility.standardVerticalSpacing;

        RenderPreview();

        var mainRect = EditorGUILayout.GetControlRect(false, 96f);
        var previewRect = mainRect;
        previewRect.xMin = previewRect.xMax - 96f;
        previewRect.x -= 5f;

        {
            var labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth -= 96f + 10;

            var r = mainRect;
            r.height = kSingleLine;
            r.xMax -= 96 + 10f;

            sizeProp.floatValue = TerrainToolsGUI.DrawSizeSlider(r, sizeProp.floatValue);

            r.y += kSingleLine + kVerticalSpacing * 0.5f;
            opacityProp.floatValue = TerrainToolsGUI.DrawOpacitySlider(r, opacityProp.floatValue, TerrainTools.ActiveTool);

            r.y += kSingleLine * 2 + kVerticalSpacing;
            EditorGUI.PropertyField(r, hardnessProp);
            r.y += kSingleLine + kVerticalSpacing * 0.5f;
            EditorGUI.PropertyField(r, radiusProp);

            GUI.DrawTexture(previewRect, preview);

            EditorGUIUtility.labelWidth = labelWidth;
        }

        {
            var rotateRect = EditorGUILayout.GetControlRect(false, kSingleLine * 2f);
            rotateRect.height = kSingleLine;

            var labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth -= 96f + 10;

            rotateRect.width -= 96f + 10;
            var rotateRect2 = rotateRect;
            EditorGUI.PropertyField(rotateRect2, rotateProp);

            // GUILayout.FlexibleSpace();
            rotateRect.xMin = rotateRect.xMax + 5f;
            rotateRect.xMax += labelWidth - (96f + 43f);
            rotateRect.width = 32f;
            EditorGUI.LabelField(rotateRect, "Flip");

            rotateRect.x += rotateRect.width;
            rotateRect.width = (96 - rotateRect.width) / 2f;

            flipXProp.boolValue = EditorGUI.ToggleLeft(rotateRect, "X", flipXProp.boolValue);

            rotateRect.x += rotateRect.width;
            flipYProp.boolValue = EditorGUI.ToggleLeft(rotateRect, "Y", flipYProp.boolValue);

            rotateRect2.y += kSingleLine + kVerticalSpacing;
            EditorGUI.PropertyField(rotateRect2, jitterProp);

            EditorGUIUtility.labelWidth = labelWidth;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Mask", EditorStyles.boldLabel);
        {
            var maskMainRect = EditorGUILayout.GetControlRect(false, 80f);

            var maskRect = maskMainRect;
            maskRect.height = maskRect.width = 64f;

            maskProp.objectReferenceValue = EditorGUI.ObjectField(maskRect, maskProp.objectReferenceValue, typeof(Texture2D), false);

            var maskScaleRect = maskMainRect;
            maskScaleRect.xMin   = maskRect.xMax + 5f;
            maskScaleRect.height = kSingleLine * 2f;

            maskScaleOffsetProp.vector4Value = TerrainToolsGUI.DrawScaleOffset(maskScaleRect, maskScaleOffsetProp.vector4Value, 0, false);

            maskScaleRect.y += kSingleLine * 2 + kVerticalSpacing * 5;
            maskScaleRect.height = kSingleLine;

            maskIsWorldSpaceProp.boolValue = EditorGUI.ToggleLeft(maskScaleRect, "World Space", maskIsWorldSpaceProp.boolValue);

            EditorGUILayout.PropertyField(maskBrightnessProp);
            EditorGUILayout.PropertyField(maskContrastProp);
        }

        serializedObject.ApplyModifiedProperties();

        // base.OnInspectorGUI();
    }

    void RenderPreview()
    {
        if (Event.current.type != EventType.Repaint)
            return;
        cmd.Clear();
        var preset = (TerrainBrushPreset)target;

        cmd.SetGlobalFloat("_BackgroundType", 1);
        cmd.SetRenderTarget(preview);
        cmd.ClearRenderTarget(true, true, Color.clear);
        preset.DrawBrush(cmd, preview);

        cmd.SetGlobalFloat("_BackgroundType", 0);
        Graphics.ExecuteCommandBuffer(cmd);
    }
}
}