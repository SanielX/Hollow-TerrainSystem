using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Hollow.VirtualTexturing
{
public enum VTTileBorder
{
    x0  = 0,
    x4  = 4,
    x8  = 8,
    x16 = 16,
}

public enum CacheTextureCompression
{
    BC1_RGB = GraphicsFormat.RGBA_DXT1_UNorm,
    BC5_RG  = GraphicsFormat.RG_BC5_UNorm,
}

[Serializable]
public struct CacheTextureDescriptor
{
    public string                  Name;
    public CacheTextureCompression Compression;
}

public enum IndirectionTextureSize
{
    x256 = 256,
    x512 = 512,
    x1024 = 1024,
    x2048 = 2048,
    x4096 = 4096
}

[System.Serializable]
public struct VirtualTextureDescriptor
{
    [Header("Indirection Texture")] public IndirectionTextureSize IndirectionTextureSize;

    [Header("Cache Texture")] public ushort       TileCountWide;
    public ushort       TileSize;
    public VTTileBorder TileBorder;

    public int                      ScratchBuffersCount;
    public CacheTextureDescriptor[] CacheTextureDescriptors;

    public int CacheTextureSize => TileCountWide * (TileSize + (int)TileBorder * 2);
}

[CreateAssetMenu]
public class VirtualTextureDescriptorAsset : ScriptableObject
{
    // TODO:
    // [InlineProperty]
    public VirtualTextureDescriptor Descriptor = new()
    {
        IndirectionTextureSize = IndirectionTextureSize.x512,

        TileCountWide          = 16,
        TileSize               = 256,
        TileBorder             = VTTileBorder.x8,

        ScratchBuffersCount = 4,
        CacheTextureDescriptors = new[]
        {
            new CacheTextureDescriptor() { Name = "_Albedo", Compression = CacheTextureCompression.BC1_RGB },
        }
    };
}
}