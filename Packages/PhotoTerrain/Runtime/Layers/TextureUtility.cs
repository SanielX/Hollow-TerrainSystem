using System;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Hollow.TerrainSystem
{
internal static class TextureUtility
{
    public static RenderTexture CreateRTFromTexture(Texture texture, bool blitContents = true)
    {
        var rt = new RenderTexture(texture.width, texture.height, texture.graphicsFormat, GraphicsFormat.None);
        rt.filterMode = texture.filterMode;
        rt.wrapMode   = texture.wrapMode;
        rt.name       = texture.name;
        rt.dimension  = texture.dimension;

        rt.Create();

        if (blitContents)
        {
            Graphics.CopyTexture(texture, rt);
            //   Graphics.Blit(texture, rt);
        }

        return rt;
    }

    public static RenderTexture CreateRTFromTexture(Texture texture, int newWidth, int newHeight, bool blitContents = true)
    {
        var rt = new RenderTexture(newWidth, newHeight, texture.graphicsFormat, GraphicsFormat.None);
        rt.filterMode = texture.filterMode;
        rt.wrapMode   = texture.wrapMode;
        rt.name       = texture.name;
        rt.dimension  = texture.dimension;

        rt.Create();

        if (blitContents)
            Graphics.Blit(texture, rt);

        return rt;
    }

    public static Texture2D ConvertRTToTexture2D(RenderTexture targetRT, Texture2D targetTexture)
    {
        if (targetRT.width == targetTexture.width && targetRT.height == targetTexture.height)
        {
            targetTexture.ReadPixels(targetRT, recalculateMips: true);
            targetTexture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
#if UNITY_EDITOR
            EditorUtility.SetDirty(targetTexture);
#endif
            return targetTexture;
        }

        return CreateTexture2DFromRT(targetRT);
    }

    public static Texture2D CreateTexture2DFromRT(RenderTexture texture, bool mipChain = false)
    {
        Texture2D targetTexture = CreateTexture2DFromDescriptor(new(texture), Color.clear, false, mipChain);
        targetTexture.ReadPixels(texture, recalculateMips: true);
        targetTexture.Apply(updateMipmaps: mipChain, makeNoLongerReadable: false);
#if UNITY_EDITOR
        EditorUtility.SetDirty(targetTexture);
#endif
        return targetTexture;
    }

    public static Texture2D CreateTexture2DFromDescriptor(TextureDescriptor desc, Color fill, bool uploadOnCreate = true, bool mipChain = false)
    {
        // 1 << 10 is internal flag for "dont upload upon create".
        // It is exposed in newer unity versions
        TextureCreationFlags flags = uploadOnCreate ? TextureCreationFlags.None : TextureCreationFlags.DontUploadUponCreate;
        if (mipChain) flags |= TextureCreationFlags.MipChain;

        Texture2D targetTexture = new(desc.width,          desc.height,
                                      desc.graphicsFormat, desc.mipCount,
                                      flags);

        targetTexture.wrapMode   = desc.wrapMode;
        targetTexture.filterMode = desc.filterMode;

        if (uploadOnCreate)
        {
            Color[] clearColor = new Color[desc.width * desc.height];
            Array.Fill(clearColor, fill);

            targetTexture.SetPixels(clearColor);

            targetTexture.Apply(updateMipmaps: true, makeNoLongerReadable: false);
        }

        return targetTexture;
    }

    public static Texture2D CreateWorkingTexture2D(in TerrainResourceDescriptor desc, bool uploadOnCreate = true)
    {
        // 1 << 10 is internal flag for "dont upload upon create".
        // It is exposed in newer unity versions
        TextureCreationFlags flags = uploadOnCreate ? TextureCreationFlags.None : (TextureCreationFlags)(1 << 10);

        Texture2D targetTexture = new(desc.w, desc.h,
                                      desc.format, 1,
                                      flags);

        targetTexture.wrapMode   = desc.wrap;
        targetTexture.filterMode = desc.filter;

        if (uploadOnCreate)
        {
            if (desc.initialFillMode == TerrainResourceDescriptor.Fill.Value16bit)
            {
                var shorts = targetTexture.GetRawTextureData<ushort>();
                for (int i = 0; i < shorts.Length; i++)
                {
                    shorts[i] = desc.initialUshortValue;
                }
            }
            else
            {
                Color[] clearColor = new Color[desc.w * desc.h];
                Array.Fill(clearColor, desc.initialColor);

                targetTexture.SetPixels(clearColor);
            }

            targetTexture.Apply(updateMipmaps: true);
        }

        return targetTexture;
    }

    public static RenderTexture ReformatTexture(Texture texture, TextureDescriptor desc)
    {
        if (desc.width          == texture.width          &&
            desc.height         == texture.height         &&
            desc.graphicsFormat == texture.graphicsFormat &&
            desc.mipCount       == texture.mipmapCount)
        {
            if (texture is RenderTexture r)
            {
                texture.filterMode = desc.filterMode;
                texture.wrapMode   = desc.wrapMode;
                return r;
            }
        }

        var rt = new RenderTexture(desc.width, desc.height, desc.graphicsFormat, GraphicsFormat.None, desc.mipCount);
        rt.filterMode = desc.filterMode;
        rt.wrapMode   = desc.wrapMode;

        rt.Create();

        Graphics.Blit(texture, rt);

        return rt;
    }
}
}