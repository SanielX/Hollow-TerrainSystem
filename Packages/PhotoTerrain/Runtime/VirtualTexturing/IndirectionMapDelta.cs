using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Assertions;
using Hollow.Extensions;

namespace Hollow.VirtualTexturing
{
/// <summary>
/// When updating indirection texture, this allows to make sure that each indirection texel is written only once,
/// therefore no race condition happens in compute shader.
/// E.g. if same page is unmapped and then mapped to something else
/// </summary>
[NativeContainer]
public unsafe struct IndirectionMapDelta : IDisposable
{
    public IndirectionMapDelta(int mipLevels, Allocator allocator)
    {
        indirectionUpdates = new(mipLevels, allocator);
        for (int i = 0; i < indirectionUpdates.Length; i++)
        {
            indirectionUpdates[i] = new(32, allocator);
        }

        updateMap = new(64, allocator);
        length    = (int*)UnsafeUtility.Malloc(sizeof(int), 4, allocator);
        length[0] = 0;

        this.allocator = allocator;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        m_Safety = AtomicSafetyHandle.Create();
        AtomicSafetyHandle.SetStaticSafetyId(ref m_Safety, s_SafetyId);
        AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite(m_Safety, true);
#endif
    }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
    private static readonly int s_SafetyId = AtomicSafetyHandle.NewStaticSafetyId<IndirectionMapDelta>();

    private AtomicSafetyHandle m_Safety;
#endif

    private                                     Allocator                                         allocator;
    private                                     NativeArray<UnsafeList<IndirectionTextureUpdate>> indirectionUpdates;
    private                                     NativeParallelHashMap<int, int>                   updateMap;
    [NativeDisableUnsafePtrRestriction] private int*                                              length;

    public int Length => *length;

    public int MipCount              => indirectionUpdates.Length;

    public int MipCountAt(int index)
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle.CheckReadAndThrow(m_Safety);

        if (index < 0 || index >= indirectionUpdates.Length)
        {
            throw new System.IndexOutOfRangeException($"Index '{index}' is out of range [0; {indirectionUpdates.Length})");
        }
#endif

        return indirectionUpdates.At(index).Length;
    }

    public void Map(int x, int y, int mip, int cacheX, int cacheY, int cacheMip, byte renderFlag)
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
        int                      hash         = VTPageCache.IndirectionTexelHash(x, y, mip);
        IndirectionTextureUpdate valueToWrite = IndirectionTextureUpdate.Map(x, y, cacheX, cacheY, cacheMip);
        valueToWrite.content.renderFlag = renderFlag;

        Write(mip, hash, valueToWrite, out _);
    }

    public void Unmap(int x, int y, int mip, out byte renderFlag)
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
        int                      hash         = VTPageCache.IndirectionTexelHash(x, y, mip);
        IndirectionTextureUpdate valueToWrite = IndirectionTextureUpdate.Unmap(x, y);

        Write(mip, hash, valueToWrite, out renderFlag);
    }

    public unsafe void Combine(Span<int> offsets, NativeArray<IndirectionTextureUpdate> combinedArray)
    {
        Assert.IsTrue(combinedArray.Length == *length);

        IndirectionTextureUpdate* updatesCombinedPtr = (IndirectionTextureUpdate*)combinedArray.GetUnsafePtr();

        for (int i = 0; i < indirectionUpdates.Length; i++)
        {
            ref UnsafeList<IndirectionTextureUpdate> updatesForThisMip = ref indirectionUpdates.At(i);

            offsets[i + 1] = offsets[i] + updatesForThisMip.Length;
            if (updatesForThisMip.Length > 0)
            {
                UnsafeUtility.MemCpy(updatesCombinedPtr + offsets[i],
                                     updatesForThisMip.Ptr,
                                     sizeof(IndirectionTextureUpdate) * updatesForThisMip.Length);
            }
        }
    }

    private void Write(int mip, int hash, IndirectionTextureUpdate valueToWrite, out byte renderFlag)
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
        if (mip < 0 || mip >= indirectionUpdates.Length)
            throw new System.IndexOutOfRangeException($"Invalid mip '{mip}'");
#endif
        ref var updatesPerMip = ref indirectionUpdates.At(mip);
        if (updateMap.TryGetValue(hash, out var localIndex))
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (localIndex < 0 || localIndex >= updatesPerMip.Length)
                throw new System.IndexOutOfRangeException(nameof(localIndex));
#endif

            renderFlag = updatesPerMip[localIndex].content.renderFlag;

            updatesPerMip[localIndex] = valueToWrite;
            return;
        }

        renderFlag = 0;
        int index = updatesPerMip.Length;
        updatesPerMip.Add(valueToWrite);

        updateMap.Add(hash, index);
        length[0]++;
    }

    public void Clear()
    {
        for (int i = 0; i < indirectionUpdates.Length; i++)
        {
            indirectionUpdates.At(i).Clear();
        }

        updateMap.Clear();
        *length = 0;
    }

    public void Dispose()
    {
        UnsafeUtility.Free(length, allocator);
        for (int i = 0; i < indirectionUpdates.Length; i++)
        {
            indirectionUpdates.At(i).Dispose();
        }

        indirectionUpdates.Dispose();
        updateMap.Dispose();
    }
}
}