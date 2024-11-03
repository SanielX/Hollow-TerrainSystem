using Hollow.TerrainSystem;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.Overlays;
using UnityEditor.Toolbars;
using UnityEngine.UIElements;

namespace HollowEditor.TerrainSystem
{
[Overlay(typeof(SceneView), "pterrain-tools-strip", "Terrain Tools", defaultLayout = Layout.VerticalToolbar, defaultDockZone = DockZone.LeftToolbar)]
public class TerrainToolsStripOverlay : Overlay, ITransientOverlay, ICreateVerticalToolbar, ICreateHorizontalToolbar
{
    private VisualElement     root;
    // private PhotoTerrainLayer lastLayer;

    public             bool   visible          => TerrainTools.ShouldShowTerrainTools;
    protected override Layout supportedLayouts => Layout.All;

    public override VisualElement CreatePanelContent()
    {
        // lastLayer = null;
        root = new();
        root.schedule.Execute(RefreshTools).ExecuteLater(50);

        root.RegisterCallback<GeometryChangedEvent>(evt =>
        {
            // FUCK OFF
            root.parent.tooltip               = "";
            root.parent.parent.tooltip        = "";
            root.parent.parent.parent.tooltip = "";
        });

        return root;
    }

    public override void OnCreated()
    {
        ToolManager.activeToolChanged += ToolChanged;
        base.OnCreated();
    }

    public override void OnWillBeDestroyed()
    {
        ToolManager.activeToolChanged -= ToolChanged;
        base.OnWillBeDestroyed();
    }

    void ToolChanged()
    {
    }

    internal void RefreshTools() => RefreshTools(false);

    internal void RefreshTools(bool force)
    {
        // var selectedLayer = TerrainTools.SelectedLayer;
        // if (!force/* && lastLayer == selectedLayer*/)
        // {
        //     // lastLayer = selectedLayer;
        //     return;
        // }

        while (root.childCount > 0)
            root.RemoveAt(0);

        // lastLayer = selectedLayer;
        // if (!selectedLayer)
        // {
        //     return;
        // }

        VisualElement currentToolsRoot = new();
        int lastSortingOrderThousand = 0;
        for (int i = 0; i < TerrainTools.ToolCount; i++)
        {
            var tool      = TerrainTools.GetToolAt(i);
            var attribute = TerrainTools.GetToolAttributeAt(i);

            int sortingOrderThousand = attribute.SortingOrder / 1000;
            if (lastSortingOrderThousand != sortingOrderThousand)
            {
                EditorToolbarUtility.SetupChildrenAsButtonStrip(currentToolsRoot);
                root.Add(currentToolsRoot);
                lastSortingOrderThousand = sortingOrderThousand;
                currentToolsRoot = new();
            }

            // var support = tool.SupportsLayer(lastLayer);
            // if (support == TerrainToolSupport.NotAvailable)
            // {
            //     continue;
            // }

            var button = new TerrainToolButton(this, tool, attribute);
            currentToolsRoot.Add(button);
        }

        EditorToolbarUtility.SetupChildrenAsButtonStrip(currentToolsRoot);
        root.Add(currentToolsRoot);

        var tooltipImage = new Image();
        tooltipImage.style.width = 20;
        tooltipImage.style.height = 20;

        tooltipImage.style.alignSelf  = Align.Center;
        tooltipImage.style.paddingTop = new StyleLength(new Length(EditorGUIUtility.standardVerticalSpacing));

        tooltipImage.image   = EditorGUIUtility.IconContent("console.infoicon").image;
        tooltipImage.tooltip = "Use Ctrl+LMB on available tools to open help overlay";

        root.Add(tooltipImage);
    }

    public OverlayToolbar CreateVerticalToolbarContent()
    {
        OverlayToolbar toolbar = new();
        toolbar.Add(CreatePanelContent());
        return toolbar;
    }

    public OverlayToolbar CreateHorizontalToolbarContent()
    {
        OverlayToolbar toolbar = new();
        toolbar.Add(CreatePanelContent());
        return toolbar;
    }
}
}