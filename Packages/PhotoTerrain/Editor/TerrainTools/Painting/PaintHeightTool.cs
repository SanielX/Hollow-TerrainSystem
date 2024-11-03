using Hollow;
using Hollow.TerrainSystem;
using UnityEngine;

namespace HollowEditor.TerrainSystem
{
[TerrainTool("Paint Height", HeightBrushToolOrder), System.Serializable]
public class PaintHeightTool : TerrainPaintTool
{
    private Material paintMaterial;

    public override Texture2D GetIcon() => Resources.Load<Texture2D>("PTEditor/d_PaintHeight");

    protected override void OnEnable()
    {
        base.OnEnable();
        paintToolParameters = PaintingToolParameters.DefaultHeight;

        paintMaterial       = new Material(Shader.Find("Hidden/PhotoTerrainEditor/PaintHeight"));
        paintMaterial.hideFlags |= HideFlags.DontSave;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        ObjectUtility.SafeDestroy(ref paintMaterial);
    }

    protected override void Blit()
    {
        var context = SetupContext(0, TerrainTexture.HEIGHT, ContextMode.Vertex);

        cmd.SetRenderTarget(context.rt);
        BlitAtMousePosition(cmd, paintMaterial, Event.current.shift ? 1 : 0);

        ApplyContext(context);
    }

    protected override float TranslateBrushOpacity(float opacity)
    {
        PhotoTerrain context = TerrainTools.GetMainTerrain();
        float contextHeight = context.MaxHeight;

        float minHeight = 1f / short.MaxValue; // This is minimum amount brush *could* influence at all
        float maxHeight = 10f / contextHeight; // This is maximum amount of height we can add in 1 stroke

        float t = Mathf.Pow(opacity, 4f);
        float value = Mathf.Lerp(minHeight, maxHeight, t);
        return value;
    }
}
}