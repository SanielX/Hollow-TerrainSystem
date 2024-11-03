using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Hollow.TerrainSystem
{
[System.Serializable]
public struct TerrainResourceDescriptor
{
    public enum Fill
    {
        Color,
        Value16bit
    }

    public const string height_t = "height";
    public const string splat_t  = "splat";
    public const string mask_t   = "mask";

    public int w;
    public int h;

    public GraphicsFormat format;

    public Fill   initialFillMode;
    public Color  initialColor;
    public ushort initialUshortValue;

    public TextureWrapMode wrap;
    public FilterMode      filter;

    public static TerrainResourceDescriptor DefaultHeight()
    {
        return new()
        {
            format       = GraphicsFormat.R16_UNorm,
            initialColor = Color.clear,
            wrap         = TextureWrapMode.Clamp,
            filter       = FilterMode.Point,
        };
    }

    public static TerrainResourceDescriptor DefaultSplat()
    {
        return new()
        {
            format       = GraphicsFormat.R16_UNorm,
            wrap         = TextureWrapMode.Clamp,
            filter       = FilterMode.Point,

            initialFillMode = Fill.Value16bit,
            initialUshortValue = 0xFC00, // blend set to 1, background & foreground = 0
        };
    }

    public static TerrainResourceDescriptor DefaultMask()
    {
        return new()
        {
            format       = GraphicsFormat.R16_UNorm,
            initialColor = Color.white,
            wrap         = TextureWrapMode.Clamp,
            filter       = FilterMode.Bilinear,
        };
    }
}
}