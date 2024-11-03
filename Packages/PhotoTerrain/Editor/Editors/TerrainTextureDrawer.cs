using System;
using System.Reflection;
using Hollow.TerrainSystem;
using UnityEditor;
using UnityEngine;

namespace HollowEditor.Editors
{
[CustomPropertyDrawer(typeof(TerrainTexture))]
public class TerrainTextureDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return 64 + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        // return base.GetPropertyHeight(property, label);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var labelWidth = EditorGUIUtility.labelWidth;

        var r = position;
        r.height = EditorGUIUtility.singleLineHeight;
        var tex = GetObjectFromProperty<TerrainTexture>(property, out _);

        var lastTexture = tex.GetLastTexture();
        EditorGUI.LabelField(r, label);
        r.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        {
            var prevRect = r;
            prevRect.width = 64;
            prevRect.height = 64;

            r.xMin += 64 + 15;

            EditorGUIUtility.labelWidth -= 64 + 15;

            EditorGUI.ObjectField(prevRect, lastTexture, typeof(Texture), false);
        }

        EditorGUI.BeginChangeCheck();
        int newWidth = EditorGUI.DelayedIntField(r, "Size", lastTexture.width);
        if (EditorGUI.EndChangeCheck())
        {
            tex.RecordUndo();
            tex.Resize(newWidth);
        }

        EditorGUIUtility.labelWidth = labelWidth;
    }

    public static T GetObjectFromProperty<T>(SerializedProperty prop, out FieldInfo fieldInfo)
    {
        fieldInfo = null;
        if (prop == null) return default(T);

        var path = prop.propertyPath.Replace(".Array.data[", "[");
        return GetObjectFromPath<T>(prop.serializedObject.targetObject, path, out fieldInfo);
    }

    public static T GetObjectFromPath<T>(UnityEngine.Object targetObject, string path, out FieldInfo fieldInfo)
    {
        fieldInfo = null;
        object obj = targetObject;
        var elements = path.Split('.');
        foreach (var element in elements)
        {
            if (element.Contains("["))
            {
                var elementName = element.Substring(0, element.IndexOf("["));
                var index = System.Convert.ToInt32(element.Substring(element.IndexOf("[")).Replace("[", "").Replace("]", ""));
                obj = GetValue_Imp(obj, elementName, index, out fieldInfo);
            }
            else
            {
                obj = GetValue_Imp(obj, element, out fieldInfo);
            }
        }

        Type t = typeof(T);

        return (T)obj;
    }

    private static object GetValue_Imp(object source, string name, out FieldInfo fieldInfo)
    {
        fieldInfo = null;
        if (source == null)
            return null;
        var type = source.GetType();

        while (type != null)
        {
            var f = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (f != null)
            {
                fieldInfo = f;
                return f.GetValue(source);
            }

            var p = type.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (p != null)
            {
                return p.GetValue(source, null);
            }

            type = type.BaseType;
        }

        return null;
    }

    private static object GetValue_Imp(object source, string name, int index, out FieldInfo info)
    {
        var enumerable = GetValue_Imp(source, name, out info) as System.Collections.IEnumerable;
        if (enumerable == null) return null;
        var enm = enumerable.GetEnumerator();
        //while (index-- >= 0)
        //    enm.MoveNext();
        //return enm.Current;

        for (int i = 0; i <= index; i++)
        {
            if (!enm.MoveNext()) return null;
        }

        return enm.Current;
    }
}
}