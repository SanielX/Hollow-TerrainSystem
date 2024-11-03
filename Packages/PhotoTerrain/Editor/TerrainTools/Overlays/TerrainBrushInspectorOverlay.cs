using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.Overlays;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace HollowEditor.TerrainSystem
{
[Overlay(typeof(SceneView), "pterrain-brush-inspector", "Terrain Brush Inspector", defaultDockPosition = DockPosition.Bottom,
         defaultDockZone = DockZone.BottomToolbar, defaultLayout = Layout.HorizontalToolbar)]
public class TerrainBrushInspectorOverlay : Overlay, ITransientOverlay, ICreateHorizontalToolbar
{
    protected override Layout supportedLayouts => Layout.HorizontalToolbar | Layout.Panel;

    private CommandBuffer cmd;
    private Material      brushBlitMaterial;
    private RenderTexture brushPreviewImage;
    private Editor        presetEditor;

    public TerrainBrushInspectorOverlay()
    {
        cmd = new();
        minSize = new(200, 200);
        maxSize = new(500, 500);

        ToolManager.activeToolChanged += OnActiveToolChanged;
    }

    public override void OnWillBeDestroyed()
    {
        ToolManager.activeToolChanged -= OnActiveToolChanged;
    }

    void OnActiveToolChanged()
    {
        RefreshToolCustomElements();
    }

    public override VisualElement CreatePanelContent()
    {
        var imgui = new IMGUIContainer(() =>
        {
            var tool = TerrainTools.ActiveTool;
            if (!tool || tool is not TerrainPaintTool paintTool)
                return;

            Editor.CreateCachedEditor(paintTool.BrushPreset, null, ref presetEditor);

            float labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 75f;

            presetEditor.OnInspectorGUI();
            presetEditor.serializedObject.ApplyModifiedProperties();

            EditorGUIUtility.labelWidth = labelWidth;
        });

        imgui.RegisterCallback<GeometryChangedEvent>(evt =>
        {
            // FUCK OFF
            imgui.parent.tooltip        = "";
            imgui.parent.parent.tooltip = "";
            imgui.parent.parent.parent.parent.tooltip = "";
        });

        imgui.style.minWidth = 200f;
        return imgui;
    }

    public bool visible => TerrainTools.ActiveTool is TerrainPaintTool;

    private VisualElement toolEarlyCustomElements;

    public OverlayToolbar CreateHorizontalToolbarContent()
    {
        OverlayToolbar root = new();
        root.RegisterCallback<GeometryChangedEvent>(evt =>
        {
            // FUCK OFF
            root.parent.tooltip               = "";
            root.parent.parent.tooltip        = "";
            root.parent.parent.parent.tooltip = "";
            root.parent.parent.parent.parent.tooltip = "";
        });

        var sheet = Resources.Load<StyleSheet>("PTEditor/PhotoTerrainStyles");
        if (sheet)
            root.styleSheets.Add(sheet);

        EditorToolbarDropdown inspectorDropdown = new();
        inspectorDropdown.clicked += () =>
        {
            var popup = OverlayPopup.Show(containerWindow, CreatePanelContent(), layout, true, inspectorDropdown.worldBound);
            popup.style.minWidth = 200f;
            popup.style.maxWidth = 300f;
            popup.style.width    = 300f;
        };

        var imagineDraw = new IMGUIContainer(() =>
        {
            var tool = TerrainTools.ActiveTool;
            if (!tool || tool is not TerrainPaintTool paintTool)
                return;

            var rect = GUILayoutUtility.GetRect(16, 16, 16, 16);
            if (!brushPreviewImage)
            {
                brushPreviewImage            =  new(16, 16, GraphicsFormat.R8G8B8A8_UNorm, GraphicsFormat.None);
                brushPreviewImage.name       =  "Brush Preview RT";
                brushPreviewImage.filterMode =  FilterMode.Bilinear;
                brushPreviewImage.wrapMode   =  TextureWrapMode.Clamp;
                brushPreviewImage.hideFlags  |= HideFlags.DontSave;
                brushPreviewImage.Create();

                brushBlitMaterial           =  new(Shader.Find("Hidden/PhotoTerrain/BrushPreviewBlit"));
                brushBlitMaterial.hideFlags |= HideFlags.DontSave;
            }

            if (Event.current.type == EventType.Repaint)
            {
                if (paintTool && paintTool.BrushPreset)
                {
                    cmd.Clear();

                    paintTool.UploadBrushPresetParameters(cmd, paintTool.BrushPreset);
                    cmd.Blit(Texture2D.whiteTexture, brushPreviewImage, brushBlitMaterial);
                    Graphics.ExecuteCommandBuffer(cmd);

                    GUI.DrawTexture(rect, brushPreviewImage, ScaleMode.ScaleToFit, true);
                }
            }
        });
        imagineDraw.style.width     = 16;
        imagineDraw.style.height    = 16;
        imagineDraw.style.alignSelf = Align.Center;
        inspectorDropdown.Insert(0, imagineDraw);

        root.Add(inspectorDropdown);

        toolEarlyCustomElements = new();
        toolEarlyCustomElements.style.flexDirection = FlexDirection.Row;
        root.Add(toolEarlyCustomElements);

        IMGUIContainer brushSettings = new(() =>
        {
            var tool = TerrainTools.ActiveTool;
            if (!tool || tool is not TerrainPaintTool paintTool)
                return;

            ref readonly var brushParams = ref paintTool.paintToolParameters;
            float labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 35f;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.Space(2);

            SerializedObject brushPresetSerialized = new(paintTool.BrushPreset);
            var sizeProp    = brushPresetSerialized.FindProperty(nameof(TerrainBrushPreset.size));
            var opacityProp = brushPresetSerialized.FindProperty(nameof(TerrainBrushPreset.opacity));

            sizeProp.floatValue = TerrainToolsGUI.DrawSizeSliderLayout(sizeProp.floatValue, GUILayout.MaxWidth(220f));

            // -- Drawing opacity slider
            {
                EditorGUILayout.Space(2);
                EditorGUIUtility.labelWidth = 50f;

                opacityProp.floatValue
                    = TerrainToolsGUI.DrawOpacitySliderLayout(opacityProp.floatValue, TerrainTools.ActiveTool, GUILayout.MaxWidth(220f));
            }

            // TODO: Implement
            // if(brushParams.displayHeightTargetSlider)
            // {
            //     EditorGUILayout.Space(2); 
            //     EditorGUIUtility.labelWidth = 50f;
            //     
            //     var     heightProp = brushPresetSerialized.FindProperty(nameof(TerrainBrushPreset.m_HeightTarget));
            //     Vector2 range      = paintTool.GetActiveWorldHeightRange();
            //     
            //     heightProp.floatValue = EditorGUILayout.Slider("Height", heightProp.floatValue,range.x, range.y);
            // }

            brushPresetSerialized.ApplyModifiedProperties();
            brushPresetSerialized.Dispose();

            EditorGUILayout.EndHorizontal();

            EditorGUIUtility.labelWidth = labelWidth;
        });

        root.Add(brushSettings);

        RefreshToolCustomElements();
        return root;
    }

    void RefreshToolCustomElements()
    {
        if (toolEarlyCustomElements is null)
            return;

        while (toolEarlyCustomElements.childCount > 0)
            toolEarlyCustomElements.RemoveAt(0);

        if (TerrainTools.ActiveTool && TerrainTools.ActiveTool is TerrainPaintTool paintTool)
        {
            paintTool.CreateEarlyBrushContent(toolEarlyCustomElements);
        }
    }
}
}