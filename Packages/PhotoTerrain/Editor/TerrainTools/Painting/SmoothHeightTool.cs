using Hollow.TerrainSystem;
using UnityEngine;
using UnityEngine.Serialization;

namespace HollowEditor.TerrainSystem
{
[TerrainTool("Smooth Height", HeightBrushToolOrder + 10), System.Serializable]
public class SmoothHeightTool : TerrainPaintTool
{
    [FormerlySerializedAs("m_KernelSize")] [Range(0f, 1f)] [SerializeField]
    internal float kernelSize = 1f;

    [FormerlySerializedAs("m_BlurDirection")] [Range(-1f, 1f)] [SerializeField]
    internal float blurDirection;

    private Material brushMat;

    public override Texture2D GetIcon() => Resources.Load<Texture2D>("PhotoTerrainIcons/d_Smooth");

    protected override void OnEnable()
    {
        base.OnEnable();

        brushMat = new(Shader.Find("Hidden/PhotoTerrainEditor/SmoothHeight"));
        brushMat.hideFlags |= HideFlags.DontSave;
    }

    protected override void Blit()
    {
        float maxBrushSize = this.kernelSize * 256;
        var   kernelSize   = Mathf.Max(1, maxBrushSize * 0.1f);

        var ctx = SetupContext(0, TerrainTexture.HEIGHT, ContextMode.Vertex, (int)kernelSize);

        var rt = ctx.rt;
        int tempHeight = Shader.PropertyToID("TempHeightTex");

        cmd.GetTemporaryRT(tempHeight, ctx.desc);
        cmd.Blit(rt, tempHeight);

        cmd.SetGlobalTexture("_HeightTex", rt);
        cmd.SetRenderTarget(tempHeight);

        cmd.SetGlobalInt("_KernelSize", (int)kernelSize);

        cmd.SetGlobalVector("_BlurDirection", new Vector4(1, 0, 0, 0));
        cmd.SetGlobalFloat("_SmoothDirection", blurDirection);

        BlitAtMousePosition(cmd, brushMat);

        cmd.SetRenderTarget(rt);
        cmd.SetGlobalTexture("_HeightTex", tempHeight);

        cmd.SetGlobalVector("_BlurDirection", new Vector4(0, 1, 0, 0));

        BlitAtMousePosition(cmd, brushMat);

        cmd.ReleaseTemporaryRT(tempHeight);

        ApplyContext(ctx);
    }
}
}