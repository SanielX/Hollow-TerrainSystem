using System.Reflection;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;

namespace HollowEditor.TerrainSystem
{
public class OverlayPopup : VisualElement
{
    private bool cursorInside;
    private static VisualTreeAsset treeAsset => (VisualTreeAsset)EditorGUIUtility.Load("UXML/Overlays/overlay.uxml");

    internal static Rect ClampRectToBounds(Rect boundary, Rect rectToClamp)
    {
        if ((double) rectToClamp.x + (double) rectToClamp.width > (double) boundary.xMax)
            rectToClamp.x = boundary.xMax - rectToClamp.width;
        if ((double) rectToClamp.x < (double) boundary.xMin)
            rectToClamp.x = boundary.xMin;
        if ((double) rectToClamp.y + (double) rectToClamp.height > (double) boundary.yMax)
            rectToClamp.y = boundary.yMax - rectToClamp.height;
        if ((double) rectToClamp.y < (double) boundary.yMin)
            rectToClamp.y = boundary.yMin;
        return rectToClamp;
    }

    public VisualElement RootVisualElement(Overlay overlay)
    {
        var prop = typeof(Overlay).GetProperty("rootVisualElement", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return (VisualElement)prop.GetValue(overlay);
    }

    public static VisualElement BaseVisualElement(EditorWindow window)
    {
        var prop = typeof(EditorWindow).GetProperty("baseRootVisualElement", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return (VisualElement)prop.GetValue(window);
    }

    public static VisualElement Show(EditorWindow window, VisualElement content, Layout layout, bool isInToolbar, Rect button, bool allowOutofFocus = true)
    {
        return new OverlayPopup(window, content, layout, isInToolbar, button, allowOutofFocus);
    }

    OverlayPopup lastPopup;

    private OverlayPopup(EditorWindow window, VisualElement content, Layout layout, bool isInToolbar, Rect button, bool allowOutofFocus)
    {
        if (lastPopup is not null)
        {
            lastPopup.ClosePopup();
        }

        OverlayPopup overlayPopup = this;
        lastPopup = overlayPopup;

        name = "overlay-popup";
        treeAsset.CloneTree((VisualElement) this);
        this.Q("overlay-collapsed-content", (string) null)?.RemoveFromHierarchy();
        this.Q((string) null, "overlay-header")?.RemoveFromHierarchy();
        focusable = true;
        pickingMode = PickingMode.Position;
        AddToClassList(Overlay.ussClassName);
        style.position = (StyleEnum<Position>) Position.Absolute;
        VisualElement visualElement = this.Q("overlay-content", (string) null);
        // visualElement.renderHints = RenderHints.ClipWithScissors;
        visualElement.Add(content);

        RegisterCallback<MouseEnterEvent>((evt => overlayPopup.cursorInside = true));
        RegisterCallback<MouseLeaveEvent>((evt => overlayPopup.cursorInside = false));
        RegisterCallback<GeometryChangedEvent>((evt =>
        {
            VisualElement overlayRoot = BaseVisualElement(window).Q<VisualElement>("unity-overlay-canvas");

            Rect collapsedButtonRect = button;
            collapsedButtonRect.size = evt.newRect.size;

            Rect bounds = ClampRectToBounds(window.rootVisualElement.worldBound, collapsedButtonRect);
            if (!Mathf.Approximately(collapsedButtonRect.position.x, bounds.position.x))
                overlayPopup.EnableInClassList("overlay-popup--clamped", true);
            Rect worldBound1 = overlayRoot.worldBound;

            if (layout == Layout.HorizontalToolbar)
                overlayPopup.EnableInClassList("overlay-popup--from-horizontal", true);
            else if (layout == Layout.VerticalToolbar)
                overlayPopup.EnableInClassList("overlay-popup--from-vertical", true);
            if (!isInToolbar)
            {
                overlayPopup.EnableInClassList("overlay-popup--outside-toolbar", true);
                Rect worldBound2 = button;
                float num1 = worldBound2.x + worldBound2.width;
                float num2 = worldBound1.xMax - num1;
                float x1 = bounds.position.x;
                float x2;
                if ((double) num2 >= (double) bounds.width)
                {
                    x2 = num1;
                }
                else
                {
                    float num3 = bounds.x - overlayRoot.worldBound.x;
                    x2 = (double) num3 < (double) bounds.width
                        ? ((double) num2 <= (double) num3
                            ? worldBound2.x - bounds.width
                            : worldBound2.x + worldBound2.width)
                        : worldBound2.x - bounds.width;
                }

                bounds.position = new Vector2(x2, bounds.position.y);
            }

            overlayPopup.transform.position = (Vector3) (bounds.position - worldBound1.position);
        }));

        RegisterCallback<FocusOutEvent>((EventCallback<FocusOutEvent>) (evt =>
        {
            if (evt.relatedTarget is VisualElement relatedTarget2 && Contains(relatedTarget2))
                return;
            if (evt.relatedTarget == null && cursorInside)
                EditorApplication.delayCall += new EditorApplication.CallbackFunction(((Focusable) this).Focus);
            else if (!allowOutofFocus || EditorWindow.focusedWindow == window)
            {
                this.ClosePopup();

                if (lastPopup == this)
                    lastPopup = null;
            }
            // EditorApplication.delayCall += () =>
            // {
            //     
            // };
        }));

        VisualElement overlayRoot = BaseVisualElement(window).Q<VisualElement>("unity-overlay-canvas");
        overlayRoot.Add((VisualElement) this);
        Focus();
    }

    private void ClosePopup()
    {
        this.RemoveFromHierarchy();
    }
}
}