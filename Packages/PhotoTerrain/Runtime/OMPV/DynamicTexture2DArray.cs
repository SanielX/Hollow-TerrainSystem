using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Hollow.TerrainSystem
{
[System.Serializable]
public class DynamicTexture2DArray : IDisposable
{
    [SerializeField] Texture2D[] referenceTextures;
    
    public Texture2DArray texture2DArray;

    public static DynamicTexture2DArray CreateInstance(int size, int capacity, TextureFormat format, bool sRGB)
    {
        DynamicTexture2DArray instance = new(); // ScriptableObject.CreateInstance<DynamicTexture2DArray>();
        // Sub-resource should be marked as dont save so they don't get automatically deleted by unity
        var graphicsFormat = GraphicsFormatUtility.GetGraphicsFormat(format, sRGB);
        instance.texture2DArray = new(size, size, capacity, graphicsFormat,
                                      TextureCreationFlags.DontUploadUponCreate | TextureCreationFlags.MipChain | TextureCreationFlags.DontInitializePixels);
        instance.texture2DArray.hideFlags |= HideFlags.DontSave;

        instance.referenceTextures = new Texture2D[capacity];

        return instance;
    }

    public int IndexOf(Texture2D texture) => texture ? System.Array.IndexOf(referenceTextures, texture) : -1;

    public void SetTexture(int index, Texture2D texture)
    {
        if (index < 0 || index >= texture2DArray.depth)
            throw new($"Texture array index {index} is out of bounds (length: {texture2DArray.depth})");

        if (!texture)
        {
            referenceTextures[index] = null;
            return;
        }

        if (referenceTextures[index] == texture || IndexOf(texture) >= 0)
            return;

        if (texture.width == texture2DArray.width &&
            texture.height == texture2DArray.height &&
            texture.graphicsFormat == texture2DArray.graphicsFormat)
        {
            for (int iMip = 0; iMip < texture2DArray.mipmapCount; iMip++)
            {
                Graphics.CopyTexture(texture, 0, iMip, texture2DArray, index, iMip);
            }
        }
        else
        {
#if UNITY_EDITOR
            var tempFormat = GetUncompressedFormat(texture2DArray.graphicsFormat);

            RenderTexture target = RenderTexture.GetTemporary(new(texture2DArray.width, texture2DArray.height, tempFormat, GraphicsFormat.None));
            Graphics.Blit(texture, target);

            // Creating temp with mip chain crashes unity for some reason so uhhhh
            Texture2D temp = TextureUtility.CreateTexture2DFromRT(target, mipChain: false);
            Texture2D temp2 = new(temp.width, temp.height, temp.graphicsFormat, TextureCreationFlags.MipChain);
            Graphics.CopyTexture(temp, 0, 0, temp2, 0, 0);
            temp2.Apply(updateMipmaps: true);

            UnityEditor.EditorUtility.CompressTexture(temp2, texture2DArray.format, UnityEditor.TextureCompressionQuality.Fast);

            for (int iMip = 0; iMip < texture2DArray.mipmapCount; iMip++)
            {
                Graphics.CopyTexture(temp2, 0, iMip, texture2DArray, index, iMip);
            }

            ObjectUtility.SafeDestroy(temp);
            ObjectUtility.SafeDestroy(temp2);
            RenderTexture.ReleaseTemporary(target);
#else
                throw new("Can't put texture into 2D array with mismatching size/format at runtime");
#endif
        }

        referenceTextures[index] = texture;
    }

    public static GraphicsFormat GetUncompressedFormat(GraphicsFormat format)
    {
        return format switch
        {
            GraphicsFormat.RG_BC5_UNorm => GraphicsFormat.R8G8_UNorm,
            GraphicsFormat.RG_BC5_SNorm => GraphicsFormat.R8G8_SNorm,

            GraphicsFormat.RGBA_DXT1_UNorm => GraphicsFormat.R8G8B8A8_UNorm,
            GraphicsFormat.RGBA_DXT1_SRGB  => GraphicsFormat.R8G8B8A8_SRGB,

            GraphicsFormat.RGBA_BC7_UNorm => GraphicsFormat.R8G8B8A8_UNorm,
            GraphicsFormat.RGBA_BC7_SRGB  => GraphicsFormat.R8G8B8A8_SRGB,

            _ => format
        };
    }

    public void Dispose()
    {
#if UNITY_EDITOR
        if (UnityEditor.EditorUtility.IsPersistent(texture2DArray)) return;
#endif

        ObjectUtility.SafeDestroy(ref texture2DArray);
    }
}
}