using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Hollow.TerrainSystem
{
internal struct TextureDescriptor
{
    public TextureDescriptor(int             width, int height, GraphicsFormat graphicsFormat,
                             int             mipCount,
                             TextureWrapMode wrapMode   = TextureWrapMode.Clamp,
                             FilterMode      filterMode = FilterMode.Point)
    {
        if (mipCount < 0)
            mipCount = Unity.Mathematics.math.floorlog2(Mathf.Max(width, height)) + 1;

        this.width          = width;
        this.height         = height;
        this.mipCount       = mipCount;
        this.graphicsFormat = graphicsFormat;
        this.wrapMode       = wrapMode;
        this.filterMode     = filterMode;
    }

    public TextureDescriptor(Texture texture)
    {
        width          = texture.width;
        height         = texture.height;
        mipCount       = texture.mipmapCount;
        graphicsFormat = texture.graphicsFormat;
        wrapMode       = texture.wrapMode;
        filterMode     = texture.filterMode;
    }

    public int width;
    public int height;
    public int mipCount;

    public GraphicsFormat  graphicsFormat;
    public TextureWrapMode wrapMode;
    public FilterMode      filterMode;

    public RenderTextureDescriptor GetRTDescriptor()
    {
        return new(width, height, graphicsFormat, GraphicsFormat.None, mipCount);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(TextureDescriptor d0, TextureDescriptor d1) =>  d0.Equals(d1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(TextureDescriptor d0, TextureDescriptor d1) => !d0.Equals(d1);

    public bool Equals(TextureDescriptor other)
    {
        return width == other.width       &&
               height == other.height     &&
               mipCount == other.mipCount &&
               graphicsFormat == other.graphicsFormat     &&
               wrapMode == other.wrapMode &&
               filterMode == other.filterMode;
    }

    public override bool Equals(object obj)
    {
        return obj is TextureDescriptor other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(width, height, mipCount, (int) graphicsFormat, (int) wrapMode, (int) filterMode);
    }
}
}