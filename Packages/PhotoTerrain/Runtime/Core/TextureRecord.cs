using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Hollow.TerrainSystem
{
public class TextureRecord : ScriptableObject, ISerializationCallbackReceiver
{
    public struct TextureData
    {
        public TextureData(Texture tex, TextureChangeType changeType, int ver)
        {
            texture    = tex;
            version    = ver;
            ChangeType = changeType;
        }

        public Texture           texture;
        public int               version;
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
    private static event System.Action                           ClearRedoStack;
    private static List<(TextureRecord stack, TextureData data)> GlobalChangeList = new();

    private int               currentUndoGroup;
    internal List<TextureData> undoStack = new();
    internal List<TextureData> redoStack = new();

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

    public Texture GetLastTexture()
    {
        return undoStack[^1].texture;
    }

    public void SetLastTexture(Texture tex)
    {
        var last = undoStack[^1];
        last.texture = tex;
        undoStack[^1] = last;
    }

    public void RecordUndo()
    {
#if UNITY_EDITOR
        if (Undo.GetCurrentGroup() != currentUndoGroup)
        {
            Undo.RecordObject(this, "Get RT");

            var bestVersion     = IndexOfBestUndoVersion(version);

            if (bestVersion < 0 || !undoStack[bestVersion].texture)
            {
                Debug.LogError($"Could not create undo record for the RecordTexture object '{this}'", this);
                return;
            }

            var lastBestTexture = undoStack[bestVersion];

            currentUndoGroup = Undo.GetCurrentGroup();
            ClearRedoStack?.Invoke();

            var newRT = TextureUtility.CreateRTFromTexture(lastBestTexture.texture);
            newRT.hideFlags |= HideFlags.DontSave;

            version++;
            previousVersion++;

            TextureData change = new(newRT, TextureChangeType.Generic, version);
            PushChange(change);
        }

        EditorUtility.SetDirty(this);
#endif
    }

    int IndexOfBestUndoVersion(int version)
    {
        for (int i = undoStack.Count - 1; i >= 0 ; i--)
        {
            if (undoStack[i].texture && undoStack[i].version <= version)
                return i;
        }

        return 0;
    }

    public void PushChange(TextureData change, bool removeAfterLimit = true)
    {
        GlobalChangeList.Add((this, change));
        undoStack.Add(change);

        if (removeAfterLimit && GlobalChangeList.Count > 35)
        {
            for (int i = 0; i < GlobalChangeList.Count; i++)
            {
                if (!GlobalChangeList[i].data.texture)
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

    void OnDestroy()
    {
#if UNITY_EDITOR
        ClearRedoStack -= ClearRedoStackCallback;
        for (int i = 0; i < redoStack.Count; i++)
        {
            if (redoStack[i].texture && !EditorUtility.IsPersistent(redoStack[i].texture))
                DestroyImmediate(redoStack[i].texture);
        }

        for (int i = 0; i < undoStack.Count; i++)
        {
            if (undoStack[i].texture && !EditorUtility.IsPersistent(undoStack[i].texture))
                DestroyImmediate(undoStack[i].texture);
        }
#endif
    }

    private bool DeleteChange(TextureData oldestData)
    {
#if UNITY_EDITOR
        // Don't delete anything if this is the last version of this texture
        var bestUndo = IndexOfBestUndoVersion(version);
        for (int i = 0; i < bestUndo; i++)
        {
            TextureData data = undoStack[i];
            if (data.texture == oldestData.texture)
            {
                if (!EditorUtility.IsPersistent(data.texture))
                {
                    DestroyImmediate(data.texture);
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

    void ClearRedoStackCallback()
    {
        for (int i = 0; i < redoStack.Count; i++)
        {
#if UNITY_EDITOR
            if (EditorUtility.IsPersistent(redoStack[i].texture))
                continue;
#endif
            if (RenderTexture.active == redoStack[i].texture)
                RenderTexture.active = null;

            DestroyImmediate(redoStack[i].texture);
        }

        redoStack.Clear();
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
                if (redoStack[i].version <= version)
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
                if (undoStack[i].version > version)
                {
                    redoStack.Add(undoStack[i]);
                    undoStack.RemoveAt(i--);
                }
            }
        }

        previousVersion = version;
    }
}
}