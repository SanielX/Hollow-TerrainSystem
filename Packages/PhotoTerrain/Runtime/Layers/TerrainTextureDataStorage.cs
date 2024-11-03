using System;
using System.Collections.Generic;
using System.IO;
using Hollow.Extensions;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hollow.TerrainSystem
{
[System.Serializable]
internal struct TerrainResource
{
    public string    name;
    public Texture2D texture;
}

[CreateAssetMenu, PreferBinarySerialization, System.Obsolete]
internal class TerrainTextureDataStorage : ScriptableObject
{
    [System.Serializable]
    internal struct TextureData
    {
        [FormerlySerializedAs("ID")] public string    TextureTypeName;
        public                              string    TextureName;
        public                              Texture2D Texture;
    }

    private Dictionary<string, TextureUndoStack> undoStackMap2 = new(StringComparer.OrdinalIgnoreCase);

    [SerializeField] internal int                   version;
    [SerializeField] internal List<TerrainResource> m_LayerResources = new();

#region Separate file workflow

    public bool CreateResource(string resourceName, in TerrainResourceDescriptor desc)
    {
#if UNITY_EDITOR
        int index = IndexOfResource(resourceName);
        if (index >= 0 && m_LayerResources[index].texture)
            return false;

        var pathToMe = AssetDatabase.GetAssetPath(this);
        if (pathToMe.IsNullOrEmpty())
            return false;

        resourceName = resourceName.ToLower();
        var texture = TextureUtility.CreateWorkingTexture2D(desc);
        if (!AddTexture(resourceName, desc.format, texture, out var path))
            return false;

        var newInfo = new TerrainResource()
        {
            name    = resourceName,
            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path),
        };

        if (index < 0)
            m_LayerResources.Add(newInfo);
        else
            m_LayerResources[index] = newInfo;

        version++;
        SetDirty();

        return true;
#else
            return false;
#endif
    }

    internal bool AddTexture(string resourceName, GraphicsFormat format, Texture2D texture, out string path)
    {
        path = default;
#if UNITY_EDITOR
        var exr = ImageConversion.EncodeArrayToPNG(texture.GetRawTextureData(), texture.graphicsFormat, (uint)texture.width, (uint)texture.height);

        int w = texture.width;
        int h = texture.height;
        FilterMode filter = texture.filterMode;
        TextureWrapMode wrap = texture.wrapMode;
        if (!EditorUtility.IsPersistent(texture))
            ObjectUtility.SafeDestroy(ref texture);

        path = HollowEditor.TerrainSystem.AssetUtility.SafeRelativeSubPath(this, resourceName + ".png");
        string absPath = HollowEditor.TerrainSystem.AssetUtility.ProjectRelativeToAbsolutePath(path);
        File.WriteAllBytes(absPath, exr);

        AssetDatabase.ImportAsset(path);

        var importer = AssetImporter.GetAtPath(path);
        if (importer is not TextureImporter textureImporter)
            return false;

        textureImporter.npotScale          = Mathf.IsPowerOfTwo(w) && Mathf.IsPowerOfTwo(h) ? TextureImporterNPOTScale.ToNearest : TextureImporterNPOTScale.None;
        textureImporter.sRGBTexture        = GraphicsFormatUtility.IsSRGBFormat(format);
        textureImporter.filterMode         = filter;
        textureImporter.wrapMode           = wrap;
        textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
        textureImporter.mipmapEnabled      = false;
        textureImporter.isReadable         = true;

        var settings = textureImporter.GetDefaultPlatformTextureSettings();
        settings.format = format switch
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

        return true;
#else
            return false;
#endif
    }

    public RenderTexture GetWritableResource(string resourceName)
    {
        var stack = GetOrCreateUndoStack(resourceName);
        if (!stack)
            return null;

#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(this, "GetRT");
#endif

        SetDirty();
        version++;
        return stack.GetRTWithUndo(true);
    }

    public Texture GetResource(string resourceName)
    {
        if (undoStackMap2.TryGetValue(resourceName, out var stack))
            return stack.GetRT();

        int i = IndexOfResource(resourceName);
        if (i >= 0)
            return m_LayerResources[i].texture;

        return null;
    }

    private TextureUndoStack GetOrCreateUndoStack(string textureName)
    {
        if (undoStackMap2.TryGetValue(textureName, out var stack))
            return stack;

        int i = IndexOfResource(textureName);
        if (i < 0)
        {
            Debug.LogError($"Working texture with id '{textureName}' was not defined", this);
            return null;
        }

        var res = m_LayerResources[i];
        TextureUndoStack textureUndoStack = ScriptableObject.CreateInstance<TextureUndoStack>();
        textureUndoStack.Init(res.texture);
        undoStackMap2[textureName] = textureUndoStack;

        return textureUndoStack;
    }

    private int IndexOfResource(string textureName)
    {
        for (int i = 0; i < m_LayerResources.Count; i++)
        {
            var textureData = m_LayerResources[i];
            if (textureData.name.Equals(textureName, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

#endregion

    new void SetDirty()
    {
#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    [ContextMenu("Save")]
    public void SaveChangedRTsToDisk()
    {
#if UNITY_EDITOR
        if (!EditorUtility.IsPersistent(this) || AssetDatabase.IsSubAsset(this))
            throw new System.InvalidOperationException("Layer should be made into asset before saving subresources to disk");

        AssetDatabase.StartAssetEditing();
        try
        {
            bool anyChanges = false;

            for (var i = 0; i < m_LayerResources.Count; i++)
            {
                var resourceInfo = m_LayerResources[i];
                var resource = GetResource(resourceInfo.name);

                if (!resource)
                {
                    Debug.LogError("Latest version of the resource is null");
                    continue;
                }

                if (resource == resourceInfo.texture || resource is not RenderTexture rt)
                {
                    continue;
                }

                if (!rt)
                    continue;

                var texture = TextureUtility.CreateTexture2DFromRT(rt);
                var exr     = ImageConversion.EncodeArrayToPNG(texture.GetRawTextureData(), texture.graphicsFormat, (uint)texture.width, (uint)texture.height);

                string path    = AssetDatabase.GetAssetPath(resourceInfo.texture);
                string absPath = HollowEditor.TerrainSystem.AssetUtility.ProjectRelativeToAbsolutePath(path);
                File.WriteAllBytes(absPath, exr);

                AssetDatabase.ImportAsset(path);

                ObjectUtility.SafeDestroy(ref texture);
                anyChanges = true;
            }

            /*  for (int i = 0; i < m_Resources.Count; i++)
              {
                  var resource        = m_Resources[i];
                  var currentResource = GetReadonlyRT(resource.TextureName, TerrainTextureType.FindID(resource.TextureTypeName));
                  if (!currentResource)
                  {
                      Debug.LogError("How??", this);
                      continue;
                  }

                  // If current resource is not a RenderTexture it means noone changed it, like, ever
                  // therefore we shouldn't save anything to disk
                  if (currentResource == resource.Texture || currentResource is not RenderTexture rt)
                  {
                      if (resource.Texture && !EditorUtility.IsPersistent(currentResource))
                      {
                          resource.Texture.hideFlags &= ~HideFlags.DontSave;

                          resource.Texture.name = name + "_" + resource.TextureTypeName;
                          AssetDatabase.AddObjectToAsset(resource.Texture, this);
                          m_Resources[i] = resource;

                          anyChanges = true;
                      }

                      continue;
                  }

                  if (!rt)
                      continue;

                  AssetDatabase.RemoveObjectFromAsset(resource.Texture);

                  resource.Texture = TextureUtility.CreateTexture2DFromRT(rt);
                  resource.Texture.name = name + "_" + resource.TextureTypeName;

                  AssetDatabase.AddObjectToAsset(resource.Texture, this);

                  m_Resources[i] = resource;

                  anyChanges = true;
              }*/

            if (anyChanges)
            {
                AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(this), ImportAssetOptions.ForceUpdate);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            //   AssetDatabase.SaveAssets();
        }
#else
            throw new System.InvalidOperationException("Saving RTs on disk is only available in editor");
#endif
    }
}
}