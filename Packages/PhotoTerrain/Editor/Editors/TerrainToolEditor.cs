using UnityEditor;
using UnityEngine;

namespace HollowEditor.TerrainSystem
{
[CustomEditor(typeof(TerrainTool), true)]
public class TerrainToolEditor : Editor
{
    protected bool useBaseInspectorGUI = true;

    public override void OnInspectorGUI()
    {
        if (!useBaseInspectorGUI)
            return;

        serializedObject.Update();
        var prop = serializedObject.GetIterator();
        if (prop.NextVisible(true))
        {
            do
            {
                if (prop.name == "m_Script") continue;

                EditorGUILayout.PropertyField(prop, includeChildren: true);
            } while (prop.NextVisible(false));
        }
    }
}
}