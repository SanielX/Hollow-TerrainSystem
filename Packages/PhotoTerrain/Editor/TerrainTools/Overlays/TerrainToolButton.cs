using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.Toolbars;
using UnityEngine.UIElements;

namespace HollowEditor.TerrainSystem
{
internal class TerrainToolButton : EditorToolbarToggle
{
    public TerrainToolButton(TerrainToolsStripOverlay overlay, TerrainTool tool, TerrainToolAttribute attribute) : base()
    {
        Tool    = tool;
        tooltip = attribute.Tooltip;
        icon    = tool.GetIcon();

        RegisterCallback<MouseDownEvent>(evt =>
        {
            if (evt.ctrlKey)
            {
                var helpPopupContent = tool.CreateHelpPopupContent();
                OverlayPopup.Show(overlay.containerWindow, helpPopupContent, overlay.layout, overlay.isInToolbar, this.worldBound);
                evt.StopPropagation();
            }
            else if (evt.button == 1)
            {
                GenericMenu menu = new();

                Type propertyWindowType = Type.GetType("UnityEditor.PropertyEditor,UnityEditor.CoreModule");
                var open = propertyWindowType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                                             .First((m) => m.Name == "OpenPropertyEditor" && m.GetParameters().Length == 2 &&
                                                           m.GetParameters()[0].ParameterType == typeof(UnityEngine.Object));

                menu.AddItem(new("Properties..."), false, () => open.Invoke(null, new object[] { tool, true }));

                menu.ShowAsContext();
                evt.StopPropagation();
            }
        });

        RegisterCallback<ChangeEvent<bool>>((evt) =>
        {
            if (Tool && TerrainTools.ActiveTool != Tool)
            {
                TerrainTools.TrySetTool(Tool);
                UpdateActiveState();
            }
        });

        RegisterCallback<AttachToPanelEvent>(evt => { ToolManager.activeToolChanged += UpdateActiveState; });

        RegisterCallback<DetachFromPanelEvent>(evt => { ToolManager.activeToolChanged -= UpdateActiveState; });
        schedule.Execute(() =>
        {
            if (!Tool)
            {
                overlay.RefreshTools(true);
            }

            SetValueWithoutNotify(ToolManager.activeToolType == Tool.GetType());

            SetEnabled(true) ; // Tool.SupportsLayer(TerrainTools.SelectedLayer) == TerrainToolSupport.Available);
        }).Every(50);

        UpdateActiveState();
    }

    void UpdateActiveState()
    {
        var newValue = TerrainTools.ActiveTool == Tool;
        SetValueWithoutNotify(newValue);
    }

    public TerrainTool Tool;
}
}