using Hollow.TerrainSystem;
using UnityEditor;
using UnityEngine;

namespace HollowEditor.TerrainSystem
{
[CustomPropertyDrawer(typeof(TerrainMaterial))]
public class TerrainPaletteMaterialDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return 80f + (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * 5f + EditorGUIUtility.standardVerticalSpacing * 9f;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var albedoProp      = property.FindPropertyRelative(nameof(TerrainMaterial.albedo));
        var maskProp        = property.FindPropertyRelative(nameof(TerrainMaterial.mask));
        var normalProp      = property.FindPropertyRelative(nameof(TerrainMaterial.normal));
        var scaleOffsetProp = property.FindPropertyRelative(nameof(TerrainMaterial.scaleOffset));
        var smoothnessProp  = property.FindPropertyRelative(nameof(TerrainMaterial.smoothness));
        var metallicProp    = property.FindPropertyRelative(nameof(TerrainMaterial.metallic));
        var heightRemapProp = property.FindPropertyRelative(nameof(TerrainMaterial.heightRemap));
        var heightTransProp = property.FindPropertyRelative(nameof(TerrainMaterial.heightTransition));
        var normalSterngthProp = property.FindPropertyRelative(nameof(TerrainMaterial.normalStrength));
        var physicsMatProp = property.FindPropertyRelative(nameof(TerrainMaterial.physicsMaterial));

        // -- TEXTURE FIELDS
        const float tex_width = 64f;
        Rect texRect = position;
        Rect labelRect = texRect;
        texRect.width  = tex_width;
        texRect.height = tex_width;

        labelRect.y += texRect.height;
        labelRect.height = EditorGUIUtility.singleLineHeight;
        albedoProp.objectReferenceValue = EditorGUI.ObjectField(texRect, albedoProp.objectReferenceValue, typeof(Texture2D), false);
        EditorGUI.LabelField(labelRect, "Albedo");

        texRect.x += tex_width + 5f;
        labelRect.x += tex_width + 5f;
        normalProp.objectReferenceValue = EditorGUI.ObjectField(texRect, normalProp.objectReferenceValue, typeof(Texture2D), false);
        EditorGUI.LabelField(labelRect, "Normal");

        texRect.x += tex_width + 5f;
        labelRect.x += tex_width + 5f;
        maskProp.objectReferenceValue = EditorGUI.ObjectField(texRect, maskProp.objectReferenceValue, typeof(Texture2D), false);
        EditorGUI.LabelField(labelRect, "Mask");

        texRect.x += tex_width + 5f;
        texRect.xMax = position.xMax;

        // -- TILING & OFFSET
        {
            var labelWidth = EditorGUIUtility.labelWidth;

            EditorGUIUtility.labelWidth = 50f;
            scaleOffsetProp.vector4Value = TerrainToolsGUI.DrawScaleOffset(texRect, scaleOffsetProp.vector4Value, 0, false);

            EditorGUIUtility.labelWidth = labelWidth;
            texRect.y += EditorGUIUtility.singleLineHeight * 2.5f;
            texRect.height = EditorGUIUtility.singleLineHeight;
            EditorGUI.PropertyField(texRect, normalSterngthProp);
        }
        // -- SMOOTHNESS & METALLIC
        var pr = position;
        pr.height = EditorGUIUtility.singleLineHeight;
        pr.y += 80f + EditorGUIUtility.standardVerticalSpacing * 4;
        EditorGUI.PropertyField(pr, smoothnessProp);

        pr.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        EditorGUI.PropertyField(pr, metallicProp);

        // -- HEIGHT PROPS
        pr.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing * 3;
        EditorGUI.PropertyField(pr, heightTransProp);

        pr.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        heightRemapProp.vector2Value = TerrainToolsGUI.MinMaxSlider(pr, EditorGUIUtility.TrTextContent("Height Remap"), heightRemapProp.vector2Value, -1, 2f);

        pr.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing * 2;
        EditorGUI.PropertyField(pr, physicsMatProp);

        property.serializedObject.ApplyModifiedProperties();
    }
}
}