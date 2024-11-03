using UnityEditor;
using UnityEngine;

namespace HollowEditor
{
public class HollowEditorGUI
{
    public static float PowerSliderLayout(GUIContent               label, float value, float leftValue, float rightValue, float power,
                                          params GUILayoutOption[] options)
    {
        // if(float.IsNaN(value))
        //     value = leftValue;
        var rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight, options);
        var v    = PowerSlider(rect, label, value, leftValue, rightValue, power);
        return v;
    }

    public static float PowerSlider(Rect rect, GUIContent label, float value, float leftValue, float rightValue, float power)
    {
        const float label_offset = 2f;
        rect.y -= label_offset;

        var prefix = EditorGUI.PrefixLabel(rect, label, EditorStyles.label);

        prefix.y += label_offset;

        var sliderRect = prefix;
        sliderRect.width -= 50;

        var fieldRect = prefix;
        fieldRect.x     += sliderRect.width;
        fieldRect.width -= sliderRect.width;

        sliderRect.width -= 10f;

        float sliderValue = Mathf.InverseLerp(leftValue, rightValue, value);

        if (power != 1)
            sliderValue = Mathf.Pow(sliderValue, power);

        sliderValue = GUI.HorizontalSlider(sliderRect, sliderValue, 0, 1);

        if (power != 1)
            sliderValue = Mathf.Pow(sliderValue, 1f / power);

        float v;

        v = Mathf.Lerp(leftValue, rightValue, sliderValue);
        v = EditorGUI.FloatField(fieldRect, v);

        if (float.IsInfinity(v) || float.IsNaN(v))
            v = leftValue;

        return v;
    }
}
}