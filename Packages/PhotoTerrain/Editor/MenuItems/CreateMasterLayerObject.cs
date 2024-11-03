using System;
using Hollow;
using Hollow.Extensions;
using Hollow.TerrainSystem;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace HollowEditor.TerrainSystem
{
[Overlay(typeof(SceneView), "Create Terrain Layer")]
class CreateLayerOverlay : Overlay, ITransientOverlay
{
    public override VisualElement CreatePanelContent()
    {
        return new IMGUIContainer(() =>
        {
            var creator = LayerCreateWizard.currentLayerCreator as CreateMasterLayerObject;
            if (!creator)
                return;

            EditorGUI.BeginChangeCheck();
            var newSize = EditorGUILayout.Vector2Field("Size", creator.rectangleSize);
            if (EditorGUI.EndChangeCheck())
                creator.rectangleSize = newSize;

            var labelWidth = EditorGUIUtility.labelWidth;

            EditorGUIUtility.labelWidth = 75;

            EditorGUILayout.Space();
            if (GUILayout.Button("Create"))
            {
                creator.CreateObject();
            }

            EditorGUIUtility.labelWidth = labelWidth;
        });
    }

    public bool visible => LayerCreateWizard.currentLayerCreator;
}

internal static class LayerCreateWizard
{
    public static EditorTool currentLayerCreator;
}

public class CreateMasterLayerObject : EditorTool
{
    [MenuItem("GameObject/PhotoTerrain/Master Layer")]
    internal static void CreateMasterLayer(MenuCommand cmd)
    {
        var tool = ScriptableObject.CreateInstance<CreateMasterLayerObject>();
        isAvailable = true;
        ToolManager.SetActiveTool(tool);
    }

    private static bool isAvailable;

    public override bool IsAvailable()
    {
        return isAvailable;
    }

    enum State
    {
        WaitingForMouse,
        SelectingSize,
    }

    State state;

    Vector3 beginPosition;
    Vector3 endPosition;

    internal Vector2                rectangleSize;

    public override void OnActivated()
    {
        LayerCreateWizard.currentLayerCreator = this;
        state = State.WaitingForMouse;
        // if(TerrainTools.SelectedLayer)
        //     targetGroup = TerrainTools.SelectedLayer.GetComponentInParent<PhotoTerrainLayerGroup>(); 
    }

    public override void OnWillBeDeactivated()
    {
        LayerCreateWizard.currentLayerCreator = null;
        isAvailable = false;
    }

    public void CreateObject()
    {
        if (rectangleSize == Vector2.zero)
            return;

        Selection.activeObject = null;

        var go = new GameObject();
        ObjectNames.SetNameSmart(go, "Master Layer");

        go.SetActive(false);

        Vector3[] rectanglePositions = new[]
        {
            beginPosition,
            beginPosition + Vector3.forward * rectangleSize.y,
            beginPosition + Vector3.right   * rectangleSize.x + Vector3.forward * rectangleSize.y,
            beginPosition + Vector3.right   * rectangleSize.x,
        };

        Vector3 minPos = rectanglePositions[0];
        Vector3 maxPos = rectanglePositions[0];

        for (int i = 1; i < rectanglePositions.Length; i++)
        {
            minPos = Vector3.Min(minPos, rectanglePositions[i]);
            maxPos = Vector3.Max(maxPos, rectanglePositions[i]);
        }

        go.transform.position   = minPos;
        go.transform.localScale = (maxPos - minPos).WithY(1);

        // if (targetGroup)
        // {
        //     go.transform.SetParent(targetGroup.transform, true);
        //     go.transform.SetAsFirstSibling();
        // }

        GameObjectUtility.EnsureUniqueNameForSibling(go);

        // var master = go.AddComponent<MasterTerrainLayer>();
        // master.m_Resolution = resolution;

        go.SetActive(true);
        Undo.RegisterCreatedObjectUndo(go, "Create Master Terrain Layer");

        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);
    }

    public override void OnToolGUI(EditorWindow window)
    {
        var evt = Event.current;

        switch (state)
        {
        case State.WaitingForMouse:
        {
            if (evt.button == 0 && evt.type == EventType.MouseDown)
            {
                var success = PhotoTerrainRenderer.TerrainScenePlace(evt.mousePosition, out var position, out _);
                if (success)
                {
                    endPosition = beginPosition = position;
                    state = State.SelectingSize;
                }

                evt.Use();
            }

            break;
        }
        case State.SelectingSize:
        {
            window.Repaint();

            Plane p = new(Vector3.up, beginPosition);

            if (evt.type == EventType.MouseDrag)
            {
                var guiPosition = evt.mousePosition;
                guiPosition.y = Camera.current.pixelHeight - guiPosition.y;
                var ray       = Camera.current.ScreenPointToRay(guiPosition);

                bool success = p.Raycast(ray, out float enter);
                if (success)
                {
                    endPosition = ray.origin + ray.direction.normalized * enter;
                }
            }
            else if (evt.type == EventType.MouseUp)
            {
                state = State.WaitingForMouse;
            }

            {
                Vector3 rectangleDirection = endPosition - beginPosition;
                rectangleSize.x = rectangleDirection.x;
                rectangleSize.y = rectangleDirection.z;

                if (!evt.shift)
                {
                    float maxComponent = Mathf.Max(Mathf.Abs(rectangleSize.x), Mathf.Abs(rectangleSize.y));
                    rectangleSize = new(maxComponent * Mathf.Sign(rectangleSize.x), maxComponent * Mathf.Sign(rectangleSize.y));
                }
            }
            break;
        }
        default:
            throw new ArgumentOutOfRangeException();
        }

        if (rectangleSize != Vector2.zero)
        {
            Vector3[] rectanglePositions = new[]
            {
                Vector3.up * 0.12f + beginPosition,
                Vector3.up * 0.12f + beginPosition + Vector3.forward * rectangleSize.y,
                Vector3.up * 0.12f + beginPosition + Vector3.right   * rectangleSize.x + Vector3.forward * rectangleSize.y,
                Vector3.up * 0.12f + beginPosition + Vector3.right   * rectangleSize.x,
            };

            Handles.zTest = CompareFunction.LessEqual;
            Handles.DrawSolidRectangleWithOutline(rectanglePositions, Color.white.WithAlpha(0.12f), Color.cyan);

            Handles.zTest = CompareFunction.Greater;
            Handles.DrawSolidRectangleWithOutline(rectanglePositions, Color.white.WithAlpha(0.01f), Color.cyan.WithAlpha(0.8f));
        }
    }
}
}