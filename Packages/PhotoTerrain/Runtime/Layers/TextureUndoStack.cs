using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Hollow.TerrainSystem
{
public class TextureUndoStack : ScriptableObject, ISerializationCallbackReceiver
{
    public struct TextureData
    {
        public TextureData(Texture tex, TextureChangeType changeType, int ver)
        {
            Texture = tex;
            Version = ver;
            ChangeType = changeType;
        }

        public Texture           Texture;
        public int               Version;
        public TextureChangeType ChangeType;
    }

    public enum TextureChangeType
    {
        Generic,
        Resize,
        Delete = 100,

        Save,
    }

    // Implement like this so that all redo textures get destroyed 
    // across undo stack instances
    private static event System.Action ClearRedoStack;
    private static List<(TextureUndoStack stack, TextureData data)> GlobalChangeList = new();

    private int               currentUndoGroup;
    private List<TextureData> undoStack = new();
    private List<TextureData> redoStack = new();

    // This is how we know OnAfterDeserialize what kind of change occured, since unity
    // undoes objects by deserializing their previous state
    // previousVersion will remain unchanged
    [NonSerialized ] int previousVersion;
    [SerializeField] int version;

    public void Init(Texture initialTexture)
    {
        hideFlags |= HideFlags.DontSave | HideFlags.DontUnloadUnusedAsset;

        undoStack.Add(new(initialTexture, TextureChangeType.Save, 0));
        version = 0;

        ClearRedoStack += ClearRedoStackCallback;
    }

    public Texture GetRT()
    {
        return undoStack[^1].Texture;
    }

    public RenderTexture GetRTWithUndo(bool undo)
    {
#if UNITY_EDITOR
        if (undo && Undo.GetCurrentGroup() != currentUndoGroup)
        {
            Undo.RecordObject(this, "Get RT");

            var bestVersion     = IndexOfBestUndoVersion(version);
            var lastBestTexture = undoStack[bestVersion];
            if (!lastBestTexture.Texture)
                return null;

            currentUndoGroup = Undo.GetCurrentGroup();
            ClearRedoStack?.Invoke();

            var newRT = TextureUtility.CreateRTFromTexture(lastBestTexture.Texture);
            newRT.hideFlags |= HideFlags.DontSave;

            version++;
            previousVersion++;

            TextureData change = new(newRT, TextureChangeType.Generic, version);
            PushChange(change);
            return newRT;
        }

        EditorUtility.SetDirty(this);
#endif

        {
            var lastTex = undoStack[^1].Texture;
            if (lastTex is RenderTexture rt) // Last texture is already best texture
            {
                return rt;
            }

            var newRT = TextureUtility.CreateRTFromTexture(lastTex);
            newRT.hideFlags |= HideFlags.DontSave;

            undoStack.Add(new(newRT, TextureChangeType.Generic, version));

            return newRT;
        }
    }

    public bool Resize(int w, int h)
    {
        if (w < 4 || h < 4)
            throw new System.Exception($"Invalid size of the texture ({w}, {h}), can't be less than 4 pixels");

        if (w > 4096 || h > 4096)
            throw new System.Exception($"Invalid size of the texture ({w}, {h}), can't greater than 4096");

#if UNITY_EDITOR
        if (Undo.GetCurrentGroup() != currentUndoGroup)
        {
            var bestVersion     = IndexOfBestUndoVersion(version);
            var lastBestTexture = undoStack[bestVersion];

            if (lastBestTexture.Texture.width  == w &&
                lastBestTexture.Texture.height == h)
                return false;

            Undo.RecordObject(this, "Resize RT");
            currentUndoGroup = Undo.GetCurrentGroup();
            ClearRedoStack?.Invoke();

            var newRT = TextureUtility.CreateRTFromTexture(lastBestTexture.Texture, w, h, true);
            newRT.hideFlags |= HideFlags.DontSave;

            version++;
            previousVersion++;

            TextureData change = new(newRT, TextureChangeType.Resize, version);
            PushChange(change);

            return true;
        }

        EditorUtility.SetDirty(this);
        // Somehow we're resizing same texture twice within undo group
        {
            var lastTex = undoStack[^1].Texture;

            if (lastTex is RenderTexture rt)
            {
                if (rt.width == w && rt.height == h)
                    return false;
            }

            // We're just gonna have to push multiple changes within same group then, which doesn't really matter
            // because it'll undo itself properly I think
            var newRT = TextureUtility.CreateRTFromTexture(lastTex, w, h, blitContents: true);
            newRT.hideFlags |= HideFlags.DontSave;

            undoStack.Add(new(newRT, TextureChangeType.Resize, version));
        }
        return true;
#else
            return false;
#endif
    }

    int IndexOfBestUndoVersion(int version)
    {
        for (int i = undoStack.Count - 1; i >= 0 ; i--)
        {
            if (undoStack[i].Texture && undoStack[i].Version <= version)
                return i;
        }

        return 0;
    }

    void ClearRedoStackCallback()
    {
        for (int i = 0; i < redoStack.Count; i++)
        {
#if UNITY_EDITOR
            if (EditorUtility.IsPersistent(redoStack[i].Texture))
                continue;
#endif
            if (RenderTexture.active == redoStack[i].Texture)
                RenderTexture.active = null;

            DestroyImmediate(redoStack[i].Texture);
        }

        redoStack.Clear();
    }

    public void PushChange(TextureData change, bool removeAfterLimit = true)
    {
        GlobalChangeList.Add((this, change));
        undoStack.Add(change);

        if (removeAfterLimit && GlobalChangeList.Count > 35)
        {
            for (int i = 0; i < GlobalChangeList.Count; i++)
            {
                if (!GlobalChangeList[i].data.Texture)
                {
                    GlobalChangeList.RemoveAt(i--);
                    continue;
                }

                if (GlobalChangeList[i].stack.DeleteChange(GlobalChangeList[i].data))
                {
                    GlobalChangeList.RemoveAt(i);
                    break;
                }
            }
        }
    }

    private bool DeleteChange(TextureData oldestData)
    {
#if UNITY_EDITOR
        // Don't delete anything if this is the last version of this texture
        var bestUndo = IndexOfBestUndoVersion(version);
        for (int i = 0; i < bestUndo; i++)
        {
            TextureData data = undoStack[i];
            if (data.Texture == oldestData.Texture)
            {
                if (!EditorUtility.IsPersistent(data.Texture))
                {
                    DestroyImmediate(data.Texture);
                    undoStack.RemoveAt(i);
                    return true;
                }
            }
        }

        return false;
#else
            return false;
#endif
    }

    public void OnBeforeSerialize()
    {
    }

    public void OnAfterDeserialize()
    {
        bool redo = version - previousVersion > 0;

        if (redo)
        {
            for (int i = 0; i < redoStack.Count; i++)
            {
                if (redoStack[i].Version <= version)
                {
                    undoStack.Add(redoStack[i]);
                    redoStack.RemoveAt(i--);
                }
            }
        }
        else if (version != previousVersion) // Undo
        {
            for (int i = undoStack.Count - 1; i >= 0 ; i--)
            {
                if (undoStack[i].Version > version)
                {
                    redoStack.Add(undoStack[i]);
                    undoStack.RemoveAt(i--);
                }
            }
        }

        previousVersion = version;
    }

    private void OnDestroy()
    {
#if UNITY_EDITOR
        ClearRedoStack -= ClearRedoStackCallback;
        for (int i = 0; i < redoStack.Count; i++)
        {
            if (redoStack[i].Texture && !EditorUtility.IsPersistent(redoStack[i].Texture))
                DestroyImmediate(redoStack[i].Texture);
        }

        for (int i = 0; i < undoStack.Count; i++)
        {
            if (undoStack[i].Texture && !EditorUtility.IsPersistent(undoStack[i].Texture))
                DestroyImmediate(undoStack[i].Texture);
        }
#endif
    }
}
}