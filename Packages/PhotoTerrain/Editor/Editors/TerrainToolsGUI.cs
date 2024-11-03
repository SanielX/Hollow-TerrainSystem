using System;
using System.Collections.Generic;
using System.Reflection;
using Hollow;
using Hollow.TerrainSystem;
using UnityEditor;
using UnityEngine;

namespace HollowEditor.TerrainSystem
{
public static class TerrainToolsGUI
{
#region Internal API Mess

    internal static readonly Type inspector_type = Type.GetType("UnityEditor.InspectorWindow,UnityEditor.CoreModule");

    public static void RepaintAllInspectors()
    {
        var repaint_method = inspector_type.GetMethod("RepaintAllInspectors", BindingFlags.NonPublic | BindingFlags.Static);
        repaint_method.Invoke(null, null);
    }

#endregion

#region Misc

    public static Vector2 MinMaxSlider(Rect position, GUIContent content, Vector2 value, float min, float max)
    {
        var r = EditorGUI.PrefixLabel(position, content);

        var indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        const float num_field_width = 50f;
        var lr = r;
        lr.width = num_field_width;

        var rr = r;
        rr.xMin = r.xMax - num_field_width;

        value.x = EditorGUI.FloatField(lr, value.x);
        value.y = EditorGUI.FloatField(rr, value.y);

        var mr = position;
        mr.xMin = lr.xMax + 5f;
        mr.xMax = rr.xMin - 5f;

        EditorGUI.MinMaxSlider(mr, ref value.x, ref value.y, min, max);

        EditorGUI.indentLevel = indent;

        return value;
    }

    public static Vector4 DrawScaleOffset(Rect position, Vector4 scaleOffset, int mixedValueMask, bool partOfTexturePropertyControl)
    {
        float kLineHeight = EditorGUIUtility.singleLineHeight;
        Vector2 tiling = new Vector2(scaleOffset.x, scaleOffset.y);
        Vector2 offset = new Vector2(scaleOffset.z, scaleOffset.w);

        float labelWidth = EditorGUIUtility.labelWidth;
        float controlStartX = position.x + labelWidth;
        float labelStartX = position.x + EditorGUI.indentLevel * 5f;

        // Temporarily reset the indent level as it was already used above to compute the positions of the label and control. See issue 946082.
        int oldIndentLevel = EditorGUI.indentLevel;

        EditorGUI.indentLevel = 0;

        if (partOfTexturePropertyControl)
        {
            labelWidth = 65;
            controlStartX = position.x + labelWidth;
            labelStartX = position.x;
            position.y = position.yMax - 2 * kLineHeight; // align with large texture thumb bottom
        }

        // Tiling
        Rect labelRect = new Rect(labelStartX, position.y, labelWidth, kLineHeight);
        Rect valueRect = new Rect(controlStartX, position.y, position.width - labelWidth, kLineHeight);
        EditorGUI.PrefixLabel(labelRect, EditorGUIUtility.TrTextContent("Tiling"));
        tiling = EditorGUI.Vector2Field(valueRect, GUIContent.none, tiling);

        // Offset
        labelRect.y += kLineHeight + 4f;
        valueRect.y += kLineHeight + 4f;
        EditorGUI.PrefixLabel(labelRect, EditorGUIUtility.TrTextContent("Offset"));
        offset = EditorGUI.Vector2Field(valueRect, GUIContent.none, offset);

        // Restore the indent level
        EditorGUI.indentLevel = oldIndentLevel;

        return new Vector4(tiling.x, tiling.y, offset.x, offset.y);
    }

    static Rect ToFullWidth(Rect rect)
    {
        rect.xMin = 0f;
        rect.width += 4f;
        return rect;
    }

    public static bool DrawHeaderFoldout(string title, bool state, bool isBoxed = false)
        => DrawHeaderFoldout(EditorGUIUtility.TrTextContent(title), state, isBoxed);

    public static bool DrawHeaderFoldout(GUIContent title, bool state, bool isBoxed = false)
    {
        const float height = 17f;
        var backgroundRect = GUILayoutUtility.GetRect(1f, height);
        if (backgroundRect.xMin != 0) // Fix for material editor
            backgroundRect.xMin = 1 + 15f * (EditorGUI.indentLevel + 1);
        float xMin = backgroundRect.xMin;

        var labelRect = backgroundRect;
        labelRect.xMin += 16f;
        labelRect.xMax -= 20f;

        var foldoutRect = backgroundRect;
        foldoutRect.y += 1f;
        foldoutRect.width = 13f;
        foldoutRect.height = 13f;
        foldoutRect.x = labelRect.xMin + 15 * (EditorGUI.indentLevel - 1); //fix for presset

        // Background rect should be full-width
        backgroundRect = ToFullWidth(backgroundRect);

        if (isBoxed)
        {
            labelRect.xMin += 5;
            foldoutRect.xMin += 5;
            backgroundRect.xMin = xMin == 7.0 ? 4.0f : EditorGUIUtility.singleLineHeight;
            backgroundRect.width -= 1;
        }

        float backgroundTint = EditorGUIUtility.isProSkin ? 0.1f : 1f;
        EditorGUI.DrawRect(backgroundRect, new Color(backgroundTint, backgroundTint, backgroundTint, 0.2f));

        // Title
        EditorGUI.LabelField(labelRect, title, EditorStyles.boldLabel);

        // Active checkbox
        state = GUI.Toggle(foldoutRect, state, GUIContent.none, EditorStyles.foldout);

        // Context menu
        var menuRect = new Rect(labelRect.xMax + 3f, labelRect.y + 1f, 16, 16);

        var e = Event.current;

        if (e.type == EventType.MouseDown)
        {
            if (backgroundRect.Contains(e.mousePosition))
            {
                if (e.button == 0)
                {
                    state = !state;
                    e.Use();
                }

                e.Use();
            }
        }

        return state;
    }

    public static void DrawSplitter(bool isBoxed = false)
    {
        var rect = GUILayoutUtility.GetRect(1f, 1f);
        float xMin = rect.xMin;

        // Splitter rect should be full-width
        rect = ToFullWidth(rect);

        if (isBoxed)
        {
            rect.xMin = xMin == 7.0 ? 4.0f : EditorGUIUtility.singleLineHeight;
            rect.width -= 1;
        }

        if (Event.current.type != EventType.Repaint)
            return;

        EditorGUI.DrawRect(rect, !EditorGUIUtility.isProSkin
                               ? new Color(0.6f, 0.6f, 0.6f, 1.333f)
                               : new Color(0.12f, 0.12f, 0.12f, 1.333f));
    }

#endregion

    public static void DrawActiveToolLayout(ref Editor editor)
    {
        Editor.CreateCachedEditor(TerrainTools.ActiveTool, null, ref editor);
        editor.OnInspectorGUI();
    }

    private static GUIStyle centeredLabelStyle;
    private static GUIContent addIconContent;

    public static int DrawPalette(TerrainMaterialPalette palette, int selectedMaterial)
    {
        if (centeredLabelStyle is null)
        {
            centeredLabelStyle = new(EditorStyles.miniLabel);
            centeredLabelStyle.alignment = TextAnchor.MiddleCenter;
            centeredLabelStyle.wordWrap = true;
        }

        const float elementWidth = 72f;
        const float elementHeight = 100f;
        int elementCount = palette.materials.Length + 1;
        var viewWidth = EditorGUIUtility.currentViewWidth - EditorGUI.indentLevel * 5f - 20f;
        int elementsPerRow = Mathf.Min(elementCount, Mathf.FloorToInt(viewWidth / elementWidth));
        int rows = Mathf.CeilToInt(elementCount / (float)elementsPerRow);

        float padding = (viewWidth - elementsPerRow * elementWidth) / elementsPerRow;

        var rect = EditorGUILayout.GetControlRect(false, rows * elementHeight);

        var evt = Event.current;
        var elementRect = rect;
        elementRect.width  = elementWidth;
        elementRect.height = elementWidth;
        int x = 0;
        int y = 0;
        for (int i = 0; i < elementCount; i++)
        {
            TerrainMaterial mat = i < palette.materials.Length ? palette.materials[i] : null;
            Rect r = elementRect;
            r.x += (elementWidth + padding) * x;
            r.y += elementHeight * y;

            Rect mr = r;
            mr.height = elementHeight;

            if (mr.Contains(evt.mousePosition))
            {
                EditorGUI.DrawRect(r, Color.gray.WithAlpha(0.4f));

                if (evt.type == EventType.MouseDown)
                {
                    if (evt.button == 0 && mat is not null)
                    {
                        GUI.changed = true;
                        evt.Use();
                        return i;
                    }
                    else if (evt.button == 0)
                    {
                        Undo.RecordObject(palette, "Add new material to palette");
                        System.Array.Resize(ref palette.materials, palette.materials.Length + 1);
                        palette.materials[^1] = new();
                        return i;
                    }
                    else if (evt.button == 1 && mat is not null)
                    {
                        int index = i;

                        GenericMenu menu = new();
                        menu.AddItem(new GUIContent("Delete"), false, () =>
                        {
                            SerializedObject s = new(palette);
                            var mats = s.FindProperty(nameof(TerrainMaterialPalette.materials));
                            mats.DeleteArrayElementAtIndex(index);
                            s.ApplyModifiedProperties();
                        });

                        menu.ShowAsContext();
                    }
                }
            }

            if (i == selectedMaterial)
            {
                EditorGUI.DrawRect(r, ColorUtils.FromHEX(0x4F657F));
            }

            var pr = r;
            pr.xMin += 5;
            pr.xMax -= 5;
            pr.yMin += 5;
            pr.yMax -= 5;
            if (mat is not null)
            {
                if (mat.albedo)
                    EditorGUI.DrawPreviewTexture(pr, mat.albedo);
            }
            else
            {
                if (addIconContent is null) addIconContent = EditorGUIUtility.IconContent("CreateAddNew@2x");
                var icon = addIconContent.image;
                EditorGUI.DrawRect(pr, ColorUtils.FromHEX(0x222222));

                var plus_rect = pr;
                plus_rect.xMin += 19;
                plus_rect.xMax -= 19;
                plus_rect.yMin += 19;
                plus_rect.yMax -= 19;
                GUI.DrawTexture(plus_rect, icon);
                //EditorGUI.DrawTextureAlpha
            }

            var lr = pr;
            lr.height = EditorGUIUtility.singleLineHeight * 2;
            lr.y += pr.height + EditorGUIUtility.standardVerticalSpacing;

            if (mat is not null)
            {
                if (mat.albedo)
                {
                    string matName;
                    if (!cachedMaterialNames.TryGetValue(mat.albedo, out matName))
                    {
                        matName = mat.albedo.name;
                        matName = ObjectNames.NicifyVariableName(matName.Replace("_", " "));

                        if (matName.Length > 20)
                            matName = matName[..20];

                        cachedMaterialNames[mat.albedo] = matName;
                    }

                    EditorGUI.LabelField(lr, matName, centeredLabelStyle);
                }
            }
            else
            {
                EditorGUI.LabelField(lr, "New", centeredLabelStyle);
            }

            x++;
            if (x >= elementsPerRow)
            {
                x = 0;
                y++;
            }
        }

        return selectedMaterial;
    }

    private static Dictionary<Texture2D, string> cachedMaterialNames = new();

    private static readonly GUIContent sizeContent    = new("Size");
    private static readonly GUIContent opacityContent = new("Opacity");

    public static float DrawSizeSliderLayout(float value, params GUILayoutOption[] layout)
    {
        return HollowEditorGUI.PowerSliderLayout(sizeContent, value, 0, 1000, 1f / 4f, layout);
    }

    public static float DrawSizeSlider(Rect r, float value)
    {
        return HollowEditorGUI.PowerSlider(r, sizeContent, value, 0, 1000, 1f / 4f);
    }

    public static float DrawOpacitySliderLayout(float value, TerrainTool selectedTool, params GUILayoutOption[] options)
    {
        return EditorGUILayout.Slider(opacityContent, value, 0, 1f, options);
    }

    public static float DrawOpacitySlider(Rect r, float value, TerrainTool selectedTool)
    {
        return EditorGUI.Slider(r, opacityContent, value, 0, 1f);
    }
}
}