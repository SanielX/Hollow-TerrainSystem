using System;
using Hollow.Extensions;
using Unity.Collections;
using UnityEngine;

namespace Hollow
{
internal static class ObjectUtility
{
    /// <summary>
    /// Destroys objects if it isn't destroyed already and selects Destroy/DestroyImmediate based on play state.
    /// Automatically sets reference to null
    /// </summary>
    public static void SafeDestroy<T>(ref T obj) where T : UnityEngine.Object
    {
        if (obj)
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(obj);
            else
                UnityEngine.Object.DestroyImmediate(obj);
#else
                    UnityEngine.Object.Destroy(obj);
#endif

            obj = null;
        }
    }

    public static void SafeDestroy<T>(T obj) where T : UnityEngine.Object
    {
        if (obj)
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(obj);
            else
                UnityEngine.Object.DestroyImmediate(obj);
#else
                    UnityEngine.Object.Destroy(obj);
#endif
        }
    }

    public static void SafeDispose<T>(NativeArray<T> array) where T : unmanaged
    {
        if (array.IsCreated)
            array.Dispose();
    }

    public static void SafeDispose(ref GraphicsBuffer obj)
    {
        if (!obj.IsNullOrInvalid())
        {
            obj.Dispose();
            obj = null;
        }
    }

    public static void SafeDispose<T>(ref T obj) where T : IDisposable
    {
        if (obj is not null)
        {
            obj.Dispose();
            obj = default(T);
        }
    }
}
}