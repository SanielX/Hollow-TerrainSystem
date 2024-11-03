using System;
using Hollow.TerrainSystem;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace HollowEditor.TerrainSystem
{
[System.Serializable]
[TerrainTool("paint-splat", "Paint Material", SplatBrushToolOrder)]
public class PaintSplatTool : TerrainPaintTool
{
    private Material paintMaterial;

    internal enum BlendPaintingMode
    {
        Progressive,
        Exact,
    }

    internal enum PaintTarget
    {
        Background,
        Foreground
    }

    internal enum VisualsMode
    {
        None,
        RedMask,
        BackgroundOnly,
    }

    [SerializeField] internal PaintTarget       paintTarget;
    [SerializeField] internal BlendPaintingMode blendingMode = BlendPaintingMode.Progressive;

    [SerializeField] internal VisualsMode visualsMode = VisualsMode.None;

    [SerializeField] internal int backgroundIndex;
    [SerializeField] internal int foregroundIndex;

    public override Texture2D GetIcon() => Resources.Load<Texture2D>("PTEditor/d_PaintMaterials");

    protected override void OnEnable()
    {
        base.OnEnable();
        paintToolParameters.mode = PaintingToolParameters.BrushPaintingMode.Splat;
        paintMaterial            = CreateEditorMaterial(Shader.Find("Hidden/PhotoTerrain/PaintOMPVSplatMap"));

        red_mask_filter_keyword = GlobalKeyword.Create("PT_OMPV_RED_MASK");
        only_background_keyword = GlobalKeyword.Create("PT_OMPV_BACKGROUND_ONLY");
    }

    public override void OnWillBeDeactivated()
    {
        base.OnWillBeDeactivated();
        Shader.SetKeyword(red_mask_filter_keyword, false);
        Shader.SetKeyword(only_background_keyword, false);
    }

    private static GlobalKeyword red_mask_filter_keyword;
    private static GlobalKeyword only_background_keyword;

    protected override void Blit()
    {
        var ctx = SetupContext(0, TerrainTexture.SPLAT, ContextMode.UV);

        int previous = Shader.PropertyToID("_PreviousSplat");
        cmd.GetTemporaryRT(previous, ctx.desc);
        cmd.Blit(ctx.rt, previous);

        int blendModeValue = 0;
        if (blendingMode == BlendPaintingMode.Progressive)
        {
            blendModeValue = Event.current.shift ? 2 : 1;
        }

        if (paintTarget == PaintTarget.Background)
        {
            blendModeValue = -1;
        }

        cmd.SetGlobalFloat("_BlendPaintMode", blendModeValue);

        var foregroundIndex = paintTarget == PaintTarget.Foreground ? this.foregroundIndex : -1;
        var backgroundIndex = paintTarget == PaintTarget.Background ? this.backgroundIndex : -1;

        if (Event.current.control)
        {
            foregroundIndex = -1;
            backgroundIndex = -1;
        }

        cmd.SetGlobalFloat("_TargetForegroundIndex", foregroundIndex);
        cmd.SetGlobalFloat("_TargetBackgroundIndex", backgroundIndex);

        cmd.SetRenderTarget(ctx.rt);
        BlitAtMousePosition(cmd, paintMaterial, 0);

        cmd.ReleaseTemporaryRT(previous);

        ApplyContext(ctx);
    }

    EditorToolbarButton blendModeButton, paintTargetButton;
    EditorToolbarDropdown debugButton;
    IntegerField targetLayerElement;
    SplatMaterialButton targetLayerButton;

    public override void CreateEarlyBrushContent(VisualElement root)
    {
        VisualElement modeTogglesGroup = new();
        modeTogglesGroup.style.flexDirection = root.style.flexDirection;

        // Blend mode
        {
            blendModeButton = new();
            blendModeButton.clicked += ChangeBlendMode;

            modeTogglesGroup.Add(blendModeButton);
        }

        SerializedObject selfSerialized = new(this);
        selfSerialized.Update();

        // Background button+dropdown 
        {
            paintTargetButton = new();
            paintTargetButton.text = "";
            paintTargetButton.clicked += ChangePaintTarget;

            modeTogglesGroup.Add(paintTargetButton);
        }

        {
            debugButton = new();
            debugButton.clicked += () =>
            {
                GenericMenu menu = new();
                menu.AddItem(new GUIContent("None"),            false, SetDebugMode, VisualsMode.None);
                menu.AddItem(new GUIContent("Background Only"), false, SetDebugMode, VisualsMode.BackgroundOnly);
                menu.AddItem(new GUIContent("Red Mask"),        false, SetDebugMode, VisualsMode.RedMask);
                menu.ShowAsContext();
            };

            modeTogglesGroup.Add(debugButton);
        }

        EditorToolbarUtility.SetupChildrenAsButtonStrip(modeTogglesGroup);
        root.Add(modeTogglesGroup);

        {
            targetLayerElement = new IntegerField();
            targetLayerElement.style.maxWidth = 50;
            targetLayerElement.BindProperty(selfSerialized.FindProperty(nameof(backgroundIndex)));

            targetLayerButton = new();

            root.Add(targetLayerElement);
            root.Add(targetLayerButton);
        }

        RefreshBlendMode();
        RefreshPaintTarget();
        RefreshDebugMode();
    }

    public void ChangeBlendMode()
    {
        Undo.RecordObject(this, "Change blend painting mode");
        if (blendingMode == BlendPaintingMode.Exact)
        {
            blendingMode = BlendPaintingMode.Progressive;
        }
        else
        {
            blendingMode = BlendPaintingMode.Exact;
        }

        RefreshBlendMode();
    }

    public void ChangePaintTarget()
    {
        Undo.RecordObject(this, "Changing paint target");
        if (paintTarget == PaintTarget.Foreground)
            paintTarget = PaintTarget.Background;
        else
            paintTarget = PaintTarget.Foreground;

        RefreshPaintTarget();
    }

    void SetButtonDisabled(VisualElement button, bool disabled)
    {
        if (disabled)
        {
            button.AddToClassList("unity-disabled");
        }
        else
        {
            button.RemoveFromClassList("unity-disabled");
        }
    }

    void RefreshBlendMode()
    {
        if (blendingMode == BlendPaintingMode.Progressive)
            blendModeButton.icon = Resources.Load<Texture2D>("PTEditor/d_SplatBlend_Progressive");
        else
            blendModeButton.icon = Resources.Load<Texture2D>("PTEditor/d_SplatBlend_Exact");

        if (blendingMode == BlendPaintingMode.Exact)
        {
            blendModeButton.tooltip = "Exact blend painting. Use LMB to set exact blend value";
        }
        else
        {
            blendModeButton.tooltip = "Progressive blend painting. Use LMB to add blend value. LMB+Shift to reduce blend value";
        }
    }

    void RefreshPaintTarget()
    {
        if (paintTarget == PaintTarget.Background)
        {
            paintTargetButton.icon = Resources.Load<Texture2D>("PTEditor/d_LayerBackground");

            SerializedObject self = new(this);
            targetLayerElement.Unbind();
            targetLayerButton .Unbind();
            targetLayerElement.BindProperty(self.FindProperty(nameof(backgroundIndex)));
            targetLayerButton .BindProperty(self.FindProperty(nameof(backgroundIndex)));
        }
        else
        {
            paintTargetButton.icon = Resources.Load<Texture2D>("PTEditor/d_LayerForeground");

            SerializedObject self = new(this);
            targetLayerElement.Unbind();
            targetLayerButton .Unbind();
            targetLayerElement.BindProperty(self.FindProperty(nameof(foregroundIndex)));
            targetLayerButton .BindProperty(self.FindProperty(nameof(foregroundIndex)));
        }
    }

    public Texture2D GetBlendModeIcon()
    {
        if (blendingMode == BlendPaintingMode.Progressive)
            return Resources.Load<Texture2D>("PTEditor/d_SplatBlend_Progressive");
        else
            return Resources.Load<Texture2D>("PTEditor/d_SplatBlend_Exact");
    }

    public Texture2D GetPaintTargetIcon()
    {
        if (paintTarget == PaintTarget.Background)
        {
            return Resources.Load<Texture2D>("PTEditor/d_LayerBackground");
        }
        else
        {
            return Resources.Load<Texture2D>("PTEditor/d_LayerForeground");
        }
    }

    void RefreshDebugMode()
    {
        switch (visualsMode)
        {
        case VisualsMode.None:
            Shader.SetKeyword(red_mask_filter_keyword, false);
            Shader.SetKeyword(only_background_keyword, false);
            debugButton.text = "--";
            break;
        case VisualsMode.RedMask:
            Shader.SetKeyword(red_mask_filter_keyword, true);
            Shader.SetKeyword(only_background_keyword, false);
            debugButton.text = "RM";
            break;
        case VisualsMode.BackgroundOnly:
            Shader.SetKeyword(red_mask_filter_keyword, false);
            Shader.SetKeyword(only_background_keyword, true);
            debugButton.text = "BG";
            break;
        default:
            throw new ArgumentOutOfRangeException();
        }
    }

    void SetDebugMode(object value)
    {
        Undo.RecordObject(this, "Set splat paint debug mode");
        visualsMode = (VisualsMode)value;
        RefreshDebugMode();
    }
}
}