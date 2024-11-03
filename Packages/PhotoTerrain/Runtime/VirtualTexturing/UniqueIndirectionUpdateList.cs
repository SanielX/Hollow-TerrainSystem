using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Hollow.VirtualTexturing
{
public struct UnsafeUniqueIndirectionUpdateList : IDisposable
{
    public UnsafeUniqueIndirectionUpdateList(int initialCapacity, Allocator allocator)
    {
        packedUpdatesSet = new(initialCapacity + 16, allocator);
        updates          = new(initialCapacity, allocator);
        updatesCount     = new(initialCapacity, allocator);
    }

    private UnsafeParallelHashMap<int, int> packedUpdatesSet;
    private UnsafeList<IndirectionUpdate> updates;
    private UnsafeList<ushort>            updatesCount;

    public Allocator allocator => updates.Allocator.ToAllocator;
    public int       Length    => updates.Length;

    public ref IndirectionUpdate At(int index)
    {
        return ref updates.ElementAt(index);
    }

    public ref ushort CountAt(int index)
    {
        return ref updatesCount.ElementAt(index);
    }

    public void CopyFrom(UnsafeUniqueIndirectionUpdateList other)
    {
        packedUpdatesSet.Clear();
        foreach (var pair in other.packedUpdatesSet)
        {
            packedUpdatesSet.Add(pair.Key, pair.Value);
        }

        updates.Clear();
        updates.CopyFrom(other.updates);

        updatesCount.Clear();
        updatesCount.CopyFrom(other.updatesCount);
    }

    public bool Add(IndirectionUpdate update, ushort worth = 1)
    {
        var hash = update.GetHashCode();
        if (packedUpdatesSet.TryGetValue(hash, out var index))
        {
            // Making sure we don't overflow
            updatesCount[index] = (ushort)Mathf.Clamp((updatesCount[index] + (int)worth), 0, ushort.MaxValue);
        }
        else
        {
            updates.Add(update);
            updatesCount.Add(worth);
            packedUpdatesSet[hash] = updates.Length - 1;

            return true;
        }

        return false;
    }

    public unsafe void Clear()
    {
        UnsafeUtility.MemSet(updates.Ptr, 0, updates.Capacity * sizeof(IndirectionUpdate));
        updates.Clear();
        packedUpdatesSet.Clear();

        UnsafeUtility.MemSet(updatesCount.Ptr, 0, updatesCount.Capacity * sizeof(ushort));
        updatesCount.Clear();
    }

    public void Dispose()
    {
        updatesCount.Dispose();
        packedUpdatesSet.Dispose();
        updates.Dispose();
    }
}

[NativeContainer]
public unsafe struct UniqueIndirectionUpdateList : IDisposable
{
    public UniqueIndirectionUpdateList(int initialCapacity, Allocator allocator)
    {
        list = (UnsafeUniqueIndirectionUpdateList*)
            UnsafeUtility.Malloc(sizeof(UnsafeUniqueIndirectionUpdateList), UnsafeUtility.AlignOf<UnsafeUniqueIndirectionUpdateList>(), allocator);
        *list = new(initialCapacity, allocator);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        if (s_safetyID == 0)
            s_safetyID = AtomicSafetyHandle.NewStaticSafetyId<UniqueIndirectionUpdateList>();

        DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, 4, allocator);
        AtomicSafetyHandle.SetStaticSafetyId(ref m_Safety, s_safetyID);
#endif
    }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
    private static int s_safetyID;

    private AtomicSafetyHandle m_Safety;

    [NativeSetClassTypeToNullOnSchedule] private DisposeSentinel    m_DisposeSentinel;
#endif

    [NativeDisableUnsafePtrRestriction] private UnsafeUniqueIndirectionUpdateList* list;

    public int Length
    {
        get
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            return list->Length;
        }
    }

    public IndirectionUpdate this[int index]
    {
        get
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
            if (index < 0 || index >= list->Length)
                throw new System.IndexOutOfRangeException($"Index '{index}' out of range (0; {list->Length})");
#endif
            return list->At(index);
        }

        set
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
            if (index < 0 || index >= list->Length)
                throw new System.IndexOutOfRangeException($"Index '{index}' out of range (0; {list->Length})");
#endif

            list->At(index) = value;
        }
    }

    public bool Add(IndirectionUpdate update, ushort worth = 1)
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
        return list->Add(update, worth);
    }

    public void Merge(UnsafeUniqueIndirectionUpdateList other)
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif

        for (int i = 0; i < other.Length; i++)
        {
            list->Add(other.At(i), other.CountAt(i));
        }
    }

    public void Merge(IndirectionUpdate[] other)
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif

        for (int i = 0; i < other.Length; i++)
        {
            Add(other[i]);
        }
    }

    public void Clear()
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
        list->Clear();
    }

    public void Dispose()
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        DisposeSentinel.Dispose(ref m_Safety, ref m_DisposeSentinel);
#endif

        var allocator = list->allocator;
        list->Dispose();
        UnsafeUtility.Free(list, allocator);
    }

    public int CountAt(int i)
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
        return list->CountAt(i);
    }
}
}