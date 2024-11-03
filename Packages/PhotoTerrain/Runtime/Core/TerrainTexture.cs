using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Hollow.TerrainSystem
{
public interface ITerrainTextureStorage
{
    public int Version { get; set; }
}

[System.Serializable]
public class TerrainTexture
{
    public const string HEIGHT = "height";
    public const string HOLES  = "holes";
    public const string SPLAT  = "splat";

    public const GraphicsFormat HEIGHT_FORMAT = GraphicsFormat.R16_UNorm;
    public const GraphicsFormat HOLES_FORMAT  = GraphicsFormat.R8_UNorm;
    public const GraphicsFormat SPLAT_FORMAT  = GraphicsFormat.R16_UNorm;

    public const int SPLAT_DEFAULT_VALUE = 0;

    public TerrainTexture(UnityEngine.Object parent, string name, int defaultSize,
                          GraphicsFormat     format, Color  defaultColor = default, int defaultValue = 0,
                          TextureWrapMode    wrap = TextureWrapMode.Clamp, FilterMode filter = FilterMode.Point)
    {
        this.parent        = parent;
        resourceName       = name;
        this.defaultSize   = defaultSize;
        this.defaultFormat = format;
        this.defaultColor  = defaultColor;
        this.defaultValue  = defaultValue;

        defaultWrap   = wrap;
        defaultFilter = filter;
    }
    
    public int Size => GetLastTexture().width;

    [System.NonSerialized] UnityEngine.Object parent;
    [System.NonSerialized] string             resourceName;
    [System.NonSerialized] int                defaultSize;
    [System.NonSerialized] GraphicsFormat     defaultFormat;
    [System.NonSerialized] Color              defaultColor;
    [System.NonSerialized] int                defaultValue;

    [System.NonSerialized] FilterMode      defaultFilter;
    [System.NonSerialized] TextureWrapMode defaultWrap;

    [System.NonSerialized] TextureRecord undoRecord;

    [SerializeField] Texture2D texture;

    void SetDirty()
    {
#if UNITY_EDITOR
        if (parent)
        {
            EditorUtility.SetDirty(parent);
            if (parent is ITerrainTextureStorage s) s.Version++;
        }
#endif
    }

    public void EnsureRenderTarget()
    {
#if UNITY_EDITOR
        EnsureInitialized();
        var lastTex = undoRecord.GetLastTexture();
        if (lastTex is RenderTexture rt)
            return;

        undoRecord.RecordUndo();
        SetDirty();
#endif
    }

    public void RecordUndo()
    {
#if UNITY_EDITOR
        EnsureInitialized();
        if (parent) Undo.RecordObject(parent, "");
        undoRecord.RecordUndo();
        SetDirty();
#endif
    }

    public Texture GetLastTexture()
    {
        EnsureInitialized();
        return undoRecord.GetLastTexture();
    }

    public RenderTexture GetRenderTexture()
    {
        EnsureInitialized();
        var res = undoRecord.GetLastTexture();

        return res as RenderTexture;
    }

    /*void ReplaceHeadInUndo()
    {
#if UNITY_EDITOR
        for (int i = 0; i < undoRecord.undoStack.Count; i++)
        {
            if (undoRecord.undoStack[i].texture == texture)
            {
                var data = undoRecord.undoStack[i];
                data.texture = TextureUtility.CreateRTFromTexture(data.texture);
                data.texture.hideFlags |= HideFlags.DontSave;

                undoRecord.undoStack[i] = data;
                return;
            }
        }

        for (int i = 0; i < undoRecord.redoStack.Count; i++)
        {
            if (undoRecord.redoStack[i].texture == texture)
            {
                var data = undoRecord.redoStack[i];
                data.texture = TextureUtility.CreateRTFromTexture(data.texture);
                data.texture.hideFlags |= HideFlags.DontSave;

                undoRecord.redoStack[i] = data;

                return;
            }
        }
#endif
    }*/

    public void SaveToDisk()
    {
#if !UNITY_EDITOR
            throw new System.Exception("Can't save terrain texture to disk at runtime");
#else
        if (!parent || !EditorUtility.IsPersistent(parent))
            return;

        if (!texture || !undoRecord)
            return;

        var lastTexture = undoRecord.GetLastTexture();
        var rt          = lastTexture as RenderTexture; // EDGECASE: lastTexture might be equal to texture

        if (!rt)
            return;

        if (EditorUtility.IsPersistent(texture))
        {
            var tempTex = TextureUtility.CreateTexture2DFromRT(rt);
            var bytes   = ImageConversion.EncodeArrayToPNG(tempTex.GetRawTextureData(), tempTex.graphicsFormat, (uint)tempTex.width, (uint)tempTex.height);

            string path    = AssetDatabase.GetAssetPath(texture);
            string absPath = HollowEditor.TerrainSystem.AssetUtility.ProjectRelativeToAbsolutePath(path);
            File.WriteAllBytes(absPath, bytes);

            AssetDatabase.ImportAsset(path);

            ObjectUtility.SafeDestroy(ref tempTex);
        }
        else // Need to create new PNG file
        {
            // If there's a changed texture, create texture2D
            if (rt) texture = TextureUtility.CreateTexture2DFromRT(rt);

            var bytes = ImageConversion.EncodeArrayToPNG(texture.GetRawTextureData(), texture.graphicsFormat, (uint)texture.width, (uint)texture.height);

            int             w      = texture.width;
            int             h      = texture.height;
            FilterMode      filter = texture.filterMode;
            TextureWrapMode wrap   = texture.wrapMode;
            if (!EditorUtility.IsPersistent(texture))
                ObjectUtility.SafeDestroy(ref texture);

            var    path    = HollowEditor.TerrainSystem.AssetUtility.SafeRelativeSubPath(parent, resourceName + ".png");
            string absPath = HollowEditor.TerrainSystem.AssetUtility.ProjectRelativeToAbsolutePath(path);
            File.WriteAllBytes(absPath, bytes);

            AssetDatabase.ImportAsset(path);

            var importer = AssetImporter.GetAtPath(path);
            if (importer is not TextureImporter textureImporter)
                throw new("Path doesn't provide texture importer");

            textureImporter.npotScale          = Mathf.IsPowerOfTwo(w) && Mathf.IsPowerOfTwo(h) ? TextureImporterNPOTScale.ToNearest : TextureImporterNPOTScale.None;
            textureImporter.sRGBTexture        = GraphicsFormatUtility.IsSRGBFormat(defaultFormat);
            textureImporter.filterMode         = filter;
            textureImporter.wrapMode           = wrap;
            textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
            textureImporter.mipmapEnabled      = false;
            textureImporter.isReadable         = true;
            textureImporter.maxTextureSize     = 4096*2;

            var settings = textureImporter.GetDefaultPlatformTextureSettings();
            settings.format = defaultFormat switch
            {
                GraphicsFormat.R16_UNorm     => TextureImporterFormat.R16,
                GraphicsFormat.R8_UNorm      => TextureImporterFormat.R8,
                GraphicsFormat.R16G16_UNorm  => TextureImporterFormat.RG16,
                GraphicsFormat.R16_SFloat    => TextureImporterFormat.RHalf,
                GraphicsFormat.R32_SFloat    => TextureImporterFormat.RFloat,
                GraphicsFormat.R16G16_SFloat => TextureImporterFormat.RGHalf,
                GraphicsFormat.R32G32_SFloat => TextureImporterFormat.RGFloat,
                _                            => settings.format
            };

            textureImporter.SetPlatformTextureSettings(settings);
            textureImporter.SaveAndReimport();

            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
#endif
    }

    void EnsureInitialized()
    {
        if (!texture)
        {
            texture           =  CreateEmptyTexture();
            texture.name      =  resourceName + "_Temp";
            texture.hideFlags |= HideFlags.DontSave;
        }

        if (!undoRecord)
        {
            undoRecord = ScriptableObject.CreateInstance<TextureRecord>();
            undoRecord.Init(texture);
        }
    }

    private Texture2D CreateEmptyTexture()
    {
        Texture2D targetTexture = new(defaultSize, defaultSize,
                                      defaultFormat, 1,
                                      TextureCreationFlags.None);
        targetTexture.wrapMode   = defaultWrap;
        targetTexture.filterMode = defaultFilter;

        if (defaultValue == 0)
        {
            Color[] clearColor = new Color[defaultSize * defaultSize];
            System.Array.Fill(clearColor, defaultColor);

            targetTexture.SetPixels(clearColor);
        }
        else
        {
            uint blockSize = GraphicsFormatUtility.GetBlockSize(defaultFormat);
            if (blockSize == 1) // 8bit
            {
                var shorts = targetTexture.GetRawTextureData<byte>();
                for (int i = 0; i < shorts.Length; i++)
                {
                    shorts[i] = (byte)defaultValue;
                }
            }
            else if (blockSize == 2) // 16bit
            {
                var shorts = targetTexture.GetRawTextureData<ushort>();
                for (int i = 0; i < shorts.Length; i++)
                {
                    shorts[i] = (ushort)defaultValue;
                }
            }
            else if (blockSize == 4) // 32bit
            {
                var shorts = targetTexture.GetRawTextureData<uint>();
                for (int i = 0; i < shorts.Length; i++)
                {
                    shorts[i] = (uint)defaultValue;
                }
            }
            else throw new System.Exception($"Graphics format '{defaultFormat}' can not be filled with 32bit integer");
        }

        targetTexture.Apply(updateMipmaps: true);
        return targetTexture;
    }

    public void Resize(int newWidth)
    {
        EnsureRenderTarget();
        var rt = GetLastTexture() as RenderTexture;

        var desc = rt.descriptor;
        rt.filterMode = defaultFilter;
        desc.height = desc.width = newWidth;
        var newRT = new RenderTexture(desc);

        Graphics.Blit(rt, newRT);
        undoRecord.SetLastTexture(newRT);

        ObjectUtility.SafeDestroy(ref rt);
    }
}
}