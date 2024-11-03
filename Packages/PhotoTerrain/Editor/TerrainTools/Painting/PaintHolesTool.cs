using Hollow;
using Hollow.TerrainSystem;
using UnityEngine;

namespace HollowEditor.TerrainSystem
{
[TerrainTool("Paint Holes", HeightBrushToolOrder + 1000), System.Serializable]
public class PaintHolesTool : TerrainPaintTool
{
    private Material paintMaterial;

    public override Texture2D GetIcon() => Resources.Load<Texture2D>("PTEditor/d_Holes");

    protected override void OnEnable()
    {
        base.OnEnable();

        paintMaterial = new Material(Shader.Find("Hidden/PhotoTerrainEditor/PaintHeight"));
        paintMaterial.hideFlags |= HideFlags.DontSave;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        ObjectUtility.SafeDestroy(ref paintMaterial);
    }

    protected override void Blit()
    {
        var ctx = SetupContext(0, TerrainTexture.HOLES, ContextMode.Vertex);

        cmd.SetRenderTarget(ctx.rt);
        BlitAtMousePosition(cmd, paintMaterial, Event.current.shift ? 0 : 1);
        ApplyContext(ctx);
    }
}
}