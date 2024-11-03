using Hollow;
using Hollow.Extensions;
using Hollow.TerrainSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace HollowEditor.TerrainSystem
{
[TerrainTool("Set Height", HeightBrushToolOrder), System.Serializable]
public class SetHeightTool : TerrainPaintTool
{
    private Material brushMat;

    [FormerlySerializedAs("m_HeightTarget")] [SerializeField]
    internal float heightTarget;

    protected override void OnEnable()
    {
        base.OnEnable();
        brushMat = new(Shader.Find("Hidden/PhotoTerrainEditor/SetHeight"));
        brushMat.hideFlags |= HideFlags.DontSave;

        paintToolParameters.displayHeightTargetSlider = true;
    }

    public override Texture2D GetIcon() => Resources.Load<Texture2D>("PTEditor/d_SetHeight");

    protected override void DrawBrushGizmo(ToolState toolState)
    {
        base.DrawBrushGizmo(toolState);

        if (toolState == ToolState.ChangingParams)
        {
            var ztest = Handles.zTest;

            Handles.color = Color.yellow;
            Handles.zTest = CompareFunction.LessEqual;
            Handles.DrawSolidDisc(capturedBrushPosition.WithY(heightTarget), Vector3.up, Mathf.Max(BrushPreset.Size.x, BrushPreset.Size.y) * 0.5f);

            Handles.color = Color.yellow.WithAlpha(0.1f);
            Handles.zTest = CompareFunction.Greater;
            Handles.DrawSolidDisc(capturedBrushPosition.WithY(heightTarget), Vector3.up, Mathf.Max(BrushPreset.Size.x, BrushPreset.Size.y) * 0.5f);

            Handles.zTest = ztest;
        }
    }

    protected override void OnMouseChangeOpacity()
    {
        heightTarget += -Event.current.delta.y * 0.01f;
    }

    protected override void Blit()
    {
        var ctx = SetupContext(0, TerrainTexture.HEIGHT, ContextMode.Vertex);
        int tempHeight = Shader.PropertyToID("TempHeightTex");

        cmd.GetTemporaryRT(tempHeight, ctx.desc);
        cmd.Blit(ctx.rt, tempHeight);
        cmd.SetGlobalTexture("_HeightTex", tempHeight);

        cmd.SetRenderTarget(ctx.rt);

        float localHeight = ConvertToLocalHeight(heightTarget);
        cmd.SetGlobalFloat("_TargetHeight", localHeight);
        BlitAtMousePosition(cmd, brushMat);

        ApplyContext(ctx);
    }

    protected void BlitBrush(RenderTexture rt, bool holdingShift)
    {
        int tempHeight = Shader.PropertyToID("TempHeightTex");

        cmd.GetTemporaryRT(tempHeight, rt.descriptor);
        cmd.Blit(rt, tempHeight);
        cmd.SetGlobalTexture("_HeightTex", tempHeight);

        cmd.SetRenderTarget(rt);

        float localHeight = ConvertToLocalHeight(heightTarget);
        cmd.SetGlobalFloat("_TargetHeight", localHeight);
        BlitAtMousePosition(cmd, brushMat);
    }

    protected override bool OnPicking(bool holdingShift)
    {
        var picking = PickAtMousePosition(cmd, Camera.current);
        if (picking.TerrainInstanceID != 0)
        {
            heightTarget = picking.Position.y;
        }

        return true;
    }
}
}