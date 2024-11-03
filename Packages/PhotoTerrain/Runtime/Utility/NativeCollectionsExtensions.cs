using System;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Hollow.Extensions
{
internal static class NativeCollectionsEtensions
{
    public static unsafe ref T At<T>(this NativeList<T> list, int index) where T : unmanaged
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        if (index < 0 || index >= list.Length)
            throw new System.IndexOutOfRangeException($"Index '{index}' is out of range of NativeList (Length: {list.Length})");
#endif

        return ref ((T*)list.GetUnsafePtr())[index];
    }

    public static unsafe ref T At<T>(this NativeArray<T> array, int index) where T : unmanaged
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        if (index < 0 || index >= array.Length)
            throw new System.ArgumentOutOfRangeException($"Index '{index}' is out of range [0; {array.Length})");
#endif

        return ref ((T*)array.GetUnsafePtr())[index];
    }

    public static unsafe T* PtrAt<T>(this NativeArray<T> array, int index) where T : unmanaged
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        if (index < 0 || index >= array.Length)
            throw new System.ArgumentOutOfRangeException($"Index '{index}' is out of range [0; {array.Length})");
#endif

        return ((T*)array.GetUnsafePtr()) + index;
    }

    public static unsafe ref readonly T AtReadonly<T>(this NativeArray<T> array, int index) where T : unmanaged
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        _ = array[index];
#endif

        return ref ((T*)array.GetUnsafeReadOnlyPtr())[index];
    }

    public static unsafe NativeArray<T> Subarray<T>(this NativeList<T> list, int startIndex, int count) where T : unmanaged
    {
        var array = list.AsArray();

        var subarray = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>((T*)array.GetUnsafePtr() + startIndex,
                                                                                    count,
                                                                                    list.GetUnsafeList()->Allocator.ToAllocator);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        var safety = NativeArrayUnsafeUtility.GetAtomicSafetyHandle(array);
        NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref subarray, safety);
#endif

        return subarray;
    }

    public static unsafe Span<T> AsSpan<T>(this NativeList<T> list) where T : unmanaged
    {
        return new(list.GetUnsafePtr(), list.Length);
    }

    public static unsafe ReadOnlySpan<T> AsReadonlySpan<T>(this NativeList<T> list) where T : unmanaged
    {
        return new(list.GetUnsafeReadOnlyPtr(), list.Length);
    }

    public static unsafe void CopyFrom<T>(this ref NativeList<T> list, ReadOnlySpan<T> span) where T : unmanaged
    {
        list.Resize(span.Length, NativeArrayOptions.UninitializedMemory);
        var ptr = list.GetUnsafePtr();

        span.CopyTo(new(ptr, span.Length));
    }

    public static unsafe int AtomicAddLength<T>(this ref NativeList<T> list, int length) where T : unmanaged
    {
        ref var listLength = ref list.GetUnsafeList()->m_length;
        return Interlocked.Add(ref listLength, length);
    }

    public static unsafe Span<T> AsSpan<T>(this UnsafeList<T> list) where T : unmanaged
    {
        return new(list.Ptr, list.Length);
    }

    public static unsafe NativeArray<T> AsNativeArray<T>(this ref UnsafeList<T> list) where T : unmanaged
    {
        var array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(list.Ptr, list.Length, list.Allocator.ToAllocator);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        var handle = AtomicSafetyHandle.Create();
        NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, handle);
#endif

        return array;
    }
}
}