using System;
using System.Collections.Generic;
using System.IO;
using Hollow.TerrainSystem;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Object = UnityEngine.Object;

namespace HollowEditor.TerrainSystem
{
[System.Serializable]
class TerrainTextureSetDesc
{
    public string    Name;
    public string    GUID;
    public Texture2D Albedo;
    public Texture2D Normal;
    public Texture2D Mask;
}

[ScriptedImporter(1, "txs")]
public class TerrainTextureSetCollectionImporter : ScriptedImporter
{
    enum TextureAction
    {
        Invalid,
        Copy,
        Reformat,
        Resize,
    }

    [SerializeField]         internal TerrainMaterialAlbedoFormat    AlbedoFormat     = TerrainMaterialAlbedoFormat   .DXT5;
    [SerializeField]         internal TerrainMaterialArrayResolution AlbedoResolution = TerrainMaterialArrayResolution.x2048;
    [Space] [SerializeField] internal TerrainMaterialNormalFormat    NormalFormat     = TerrainMaterialNormalFormat   .BC5;
    [SerializeField]         internal TerrainMaterialArrayResolution NormalResolution = TerrainMaterialArrayResolution.x2048;
    [Space] [SerializeField] internal TerrainMaterialMaskFormat      MaskFormat       = TerrainMaterialMaskFormat     .BC7;
    [SerializeField]         internal TerrainMaterialArrayResolution MaskResolution   = TerrainMaterialArrayResolution.x2048;
    [Space] [SerializeField] internal TerrainTextureSetDesc[]        TextureSets;

    string assetName, projectFolderPath;

    public override void OnImportAsset(AssetImportContext ctx)
    {
        projectFolderPath = Application.dataPath[..^"/Assets".Length];
        assetName         = Path.GetFileNameWithoutExtension(ctx.assetPath);

        TerrainTextureSetCollection collection = ScriptableObject.CreateInstance<TerrainTextureSetCollection>();
        ctx.AddObjectToAsset("main", collection);
        ctx.SetMainObject   (collection);

        if (importSettingsMissing)
            return;

        collection.textureSets = new TerrainTextureSet[TextureSets.Length];
        for (int i = 0; i < TextureSets.Length; i++)
        {
            var setDesc = TextureSets[i];

            var set = ScriptableObject.CreateInstance<TerrainTextureSet>();
            set.name         = setDesc.Name;
            set.collection = collection;

            collection.textureSets[i] = set;

            var albedoAssetPath = AssetDatabase.GetAssetPath(setDesc.Albedo);
            var albedo          = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoAssetPath);
            ctx.AddObjectToAsset(setDesc.GUID, set, albedo);
        }

        BuildTextureArray(ctx,
                          desc     => desc.Albedo,
                          (set, i, tex) =>
                          {
                              set.albedoIndex = i;
                              set.albedoPrototype = tex;
                          },
                          array    =>
                          {
                              array.name               = assetName + "_AlbedoArray";
                              collection.albedoArray = array;
                              ctx.AddObjectToAsset("albedo-array", array);
                          },
                          TextureSets, collection.textureSets,
                          (TextureFormat)AlbedoFormat,
                          (int)AlbedoResolution,
                          allowReformat: true,
                          arraySRGB:     true);

        BuildTextureArray(ctx,
                          desc     => desc.Normal,
                          (set, i, tex) => set.normalIndex = i,
                          array    =>
                          {
                              array.name               = assetName + "_NormalArray";
                              collection.normalArray = array;
                              ctx.AddObjectToAsset("normal-array", array);
                          },
                          TextureSets, collection.textureSets,
                          (TextureFormat)NormalFormat,
                          (int)NormalResolution,
                          allowReformat: false,
                          arraySRGB:     false);

        BuildTextureArray(ctx,
                          desc     => desc.Mask,
                          (set, i, tex) => set.maskIndex = i,
                          array    =>
                          {
                              array.name             = assetName + "_MaskArray";
                              collection.maskArray = array;
                              ctx.AddObjectToAsset("mask-array", array);
                          },
                          TextureSets, collection.textureSets,
                          (TextureFormat)MaskFormat,
                          (int)MaskResolution,
                          allowReformat: true,
                          arraySRGB:     false);
    }

    void BuildTextureArray(AssetImportContext                     ctx,
                           Func<TerrainTextureSetDesc, Texture2D> getTexture,
                           Action<TerrainTextureSet, int, Texture2D> setTextureIndex,
                           setTextureArrayDelegate                arraySetter,
                           TerrainTextureSetDesc[]                textureSetDescs,
                           TerrainTextureSet[]                    textureSets,
                           TextureFormat                          arrayFormat,
                           int                                    arrayResolution,
                           bool                                   allowReformat,
                           bool                                   arraySRGB)
    {
        List<(Texture2D texture, TextureAction action)> validTextures = new();

        for (int i = 0; i < textureSets.Length; i++)
        {
            var setDesc = textureSetDescs[i];
            var set     = textureSets[i];
            var texture = getTexture(setDesc);

            TextureAction action = DetermineTextureAction(ctx,         texture,   arrayResolution,
                                                          arrayFormat, arraySRGB, allowReformat);

            if (action != TextureAction.Invalid)
            {
                setTextureIndex(set, validTextures.Count, texture);
                validTextures.Add((texture, action));
            }
            else
            {
                setTextureIndex(set, -1, texture);
            }
        }

        if (validTextures.Count == 0)
        {
            Texture2DArray dummyArray = new(4, 4, 1, arrayFormat, mipChain: false);
            arraySetter(dummyArray);
            return;
        }

        var arrayGraphicsFormat = GraphicsFormatUtility.GetGraphicsFormat(arrayFormat, arraySRGB);

        if (arrayFormat == TextureFormat.RGB24)
            arrayGraphicsFormat = GraphicsFormat.R8G8B8A8_SRGB;

        Texture2DArray textureArray = new(arrayResolution, arrayResolution, validTextures.Count, arrayGraphicsFormat,
                                          TextureCreationFlags.MipChain);
        textureArray.filterMode = FilterMode.Trilinear;
        PackTextureArray(ctx, textureArray, validTextures);

        arraySetter(textureArray);
    }

    TextureAction DetermineTextureAction(AssetImportContext ctx,             Texture2D     texture,
                                         int                arrayResolution, TextureFormat arrayFormat, bool arraySRGB,
                                         bool               allowReformat)
    {
        if (!texture)
            return TextureAction.Invalid;

        // Adding new texture, figure out what to do with it
        TextureAction action = TextureAction.Copy;

        // Can be copied if resolution & format just match
        var copyCheck = CanBeCopiedToTextureArray(texture, arrayResolution, arrayFormat, arraySRGB);
        if (copyCheck.HasFlag(CopyCheckResult.WrongSize) && !copyCheck.HasFlag(CopyCheckResult.WrongFormat))
        {
            action = TextureAction.Resize;
        }
        else if (copyCheck != CopyCheckResult.Ok && !allowReformat)
        {
            PrintCopyError(arrayFormat, arrayResolution, copyCheck, texture);
            return TextureAction.Invalid;
        }
        else if (copyCheck != CopyCheckResult.Ok && allowReformat)
        {
            var reformatCheck = CanReformatTexture(texture, out var textureAssetPath);
            if (reformatCheck == ReformatCheckResult.Ok)
            {
                action = TextureAction.Reformat;
            }
            else
            {
                PrintReformatError(ctx, reformatCheck, textureAssetPath);
                return TextureAction.Invalid;
            }
        }

        return action;
    }

    void PackTextureArray(AssetImportContext                              ctx,
                          Texture2DArray                                  textureArray,
                          List<(Texture2D texture, TextureAction action)> validTextures)
    {
        int targetResolution = textureArray.width;
        for (int i = 0; i < validTextures.Count; i++)
        {
            var textureInfo   = validTextures[i];
            var texture       = textureInfo.texture;
            var textureAction = textureInfo.action;

            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(texture, out string guid, out long localId))
            {
                ctx.DependsOnArtifact(new GUID(guid));
                // Apparently if you "depend on artifact" you need to load the asset using asset database
                // otherwise it'll generate warnings :/
                AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(guid));
            }

            if (textureAction == TextureAction.Copy)
            {
                if (texture.mipmapCount != textureArray.mipmapCount)
                {
                    Debug.LogError("Wrong mipmap count pls fix", this);
                    continue;
                }

                if (texture.graphicsFormat == textureArray.graphicsFormat)
                {
                    for (int j = 0; j < texture.mipmapCount; j++)
                    {
                        Graphics.CopyTexture(texture,      srcElement: 0, srcMip: j,
                                             textureArray, dstElement: i, dstMip: j);
                    }
                }
                else
                {
                    for (int j = 0; j < texture.mipmapCount; j++)
                    {
                    }
                }

                textureArray.Apply(false);
                continue;
            }

            if (textureAction == TextureAction.Resize)
            {
                int mipDifference = texture.mipmapCount - textureArray.mipmapCount;
                // If texture is bigger than target array size, just copy smaller mips
                if (texture.mipmapCount > textureArray.mipmapCount)
                {
                    for (int j = 0; j < textureArray.mipmapCount; j++)
                    {
                        Graphics.CopyTexture(texture,      srcElement: 0, srcMip: j + mipDifference,
                                             textureArray, dstElement: i, dstMip: j);
                    }

                    continue;
                }
                else // Texture is bigger!
                {
                    mipDifference = -mipDifference; // Going to be negative, so flip
                    for (int j = 0; j < texture.mipmapCount; j++)
                    {
                        Graphics.CopyTexture(texture,      srcElement: 0, srcMip: j,
                                             textureArray, dstElement: i, dstMip: j + mipDifference);
                    }

                    for (int j = 0; j < mipDifference; j++)
                    {
                        int mipSize = texture.width << (j + 1); // Multiply by 2

                        GraphicsFormat imageFormat = texture.isDataSRGB ? GraphicsFormat.R8G8B8A8_SRGB : GraphicsFormat.R8G8B8A8_UNorm;
                        RenderTextureDescriptor desc = new(mipSize, mipSize);
                        desc.graphicsFormat     = imageFormat;
                        desc.depthStencilFormat = GraphicsFormat.None;

                        RenderTexture rt = RenderTexture.GetTemporary(desc);
                        RenderTexture.active = rt;

                        Graphics.Blit(texture, rt);
                        Texture2D resizedImage = new(mipSize, mipSize, imageFormat, TextureCreationFlags.DontInitializePixels);
                        resizedImage.ReadPixels(new Rect(0.0f, 0.0f, mipSize, mipSize), 0, 0);
                        resizedImage.Apply();

                        EditorUtility.CompressTexture(resizedImage, textureArray.format, TextureCompressionQuality.Best);
                        Graphics.CopyTexture(resizedImage, srcElement: 0, srcMip: 0,
                                             textureArray, dstElement: i, dstMip: (mipDifference - j - 1));

                        RenderTexture.ReleaseTemporary(rt);
                    }
                }
            }

            if (textureAction == TextureAction.Reformat)
            {
                var textureAssetPath = AssetDatabase.GetAssetPath(texture);
                var filePath         = projectFolderPath + "/" + textureAssetPath;
                var imageBytes       = File.ReadAllBytes(filePath);

                Texture2D image = new(2, 2, DefaultFormat.LDR,
                                      TextureCreationFlags.MipChain | TextureCreationFlags.DontInitializePixels);

                if (!image.LoadImage(imageBytes))
                {
                    Debug.LogError($"Couldn't load image ({textureAssetPath})");
                    continue;
                }

                if (image.width != targetResolution || image.height != targetResolution)
                {
                    RenderTexture rt = RenderTexture.GetTemporary(targetResolution, targetResolution, 0, RenderTextureFormat.ARGB32,
                                                                  RenderTextureReadWrite.Default);
                    RenderTexture.active = rt;

                    Graphics.Blit(image, rt);
                    image.Reinitialize(targetResolution, targetResolution, image.format, true);
                    image.filterMode = FilterMode.Bilinear;

                    image.ReadPixels(new Rect(0.0f, 0.0f, targetResolution, targetResolution), 0, 0);
                    image.Apply();

                    RenderTexture.ReleaseTemporary(rt);
                }

                EditorUtility.CompressTexture(image, textureArray.format, TextureCompressionQuality.Best);
                Graphics.CopyTexture(image,        srcElement: 0,
                                     textureArray, dstElement: i);

                Object.DestroyImmediate(image);
            }
        }
    }

    void BuildTextureArray(AssetImportContext ctx)
    {
    }

    [System.Flags]
    internal enum CopyCheckResult
    {
        Ok          = 0,
        WrongFormat = 1,
        WrongSize   = 2,
        WrongSRGB   = 4,
    }

    internal static CopyCheckResult CanBeCopiedToTextureArray(Texture2D texture, int arrayResolution, TextureFormat arrayFormat, bool isSRGB)
    {
        CopyCheckResult o = 0;
        if (texture.format != arrayFormat)
            o |= CopyCheckResult.WrongFormat;

        if (texture.width != arrayResolution ||
            texture.height != arrayResolution)
            o |= CopyCheckResult.WrongSize;

        if (texture.isDataSRGB != isSRGB)
            o |= CopyCheckResult.WrongSRGB;

        return o;
    }

    internal enum ReformatCheckResult
    {
        Ok,
        NonMainAsset,
        UnsupportedExtension,
    }

    private static ReformatCheckResult CanReformatTexture(Texture2D texture, out string assetPath)
    {
        assetPath = null;
        if (!AssetDatabase.IsMainAsset(texture))
            return ReformatCheckResult.NonMainAsset;

        assetPath = AssetDatabase.GetAssetPath(texture);

        if (!(assetPath.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
              assetPath.EndsWith(".jpg",  StringComparison.OrdinalIgnoreCase) ||
              assetPath.EndsWith(".png",  StringComparison.OrdinalIgnoreCase)))
        {
            return ReformatCheckResult.UnsupportedExtension;
        }

        return ReformatCheckResult.Ok;
    }

    private delegate void setIndexDelegate(ref TerrainTextureSet set, int index);

    private delegate Texture2D getTextureDelegate(TerrainTextureSetDesc mat);

    private delegate void setTextureArrayDelegate(Texture2DArray array);

    void PrintReformatError(AssetImportContext ctx, ReformatCheckResult reformatCheck, string textureAssetPath)
    {
        if (reformatCheck == ReformatCheckResult.UnsupportedExtension)
        {
            Debug.LogError(
                $"Can't reformat texture with non png/jpg extension (Terrain Palette: {ctx.assetPath}) (Texture: '{textureAssetPath}')",
                this);
        }
        else if (reformatCheck == ReformatCheckResult.NonMainAsset)
        {
            Debug.LogError(
                $"Can't reformat texture that is not main asset (Terrain Palette: {ctx.assetPath}) (Texture: '{textureAssetPath}')",
                this);
        }
    }

    void PrintCopyError(TextureFormat arrayFormat, int arrayResolution, CopyCheckResult copyCheck, Texture2D texture)
    {
        if (copyCheck == CopyCheckResult.WrongFormat)
        {
            Debug.LogError(
                $"Texture '{texture.name}' has wrong format ({texture.format} != {arrayFormat}) and can't be added to texture array",
                this);
        }
        else if (copyCheck == CopyCheckResult.WrongSize)
        {
            Debug.LogError(
                $"Texture '{texture.name}' has wrong size ({texture.width} != {arrayResolution}) and can't be added to texture array",
                this);
        }
    }
}
}