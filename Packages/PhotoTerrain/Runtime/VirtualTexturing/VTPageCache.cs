using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine.Assertions;
using Hollow.Extensions;

namespace Hollow.VirtualTexturing
{
public struct VTPage
{
    public int  hash;

    /// <summary>
    /// Mip level that cache page is mapped to
    /// </summary>
    public int  mip;

    /// <summary>
    /// Coordinates cache page is mapped to (on indirection texture), taking into account mip level
    /// </summary>
    public int  x, y;

    public int  imageId;

    public bool IsMapped => imageId >= 0;

    public override string ToString()
    {
        return $"VTPage: ({x}:{y}, mip:{mip}, ownerID: {imageId})";
    }
}

/// <summary>
/// Manages key->index data type where key is priority of a cache texture page, and index is its index on grid.
/// 
/// </summary>
public unsafe struct VTPageCache : IDisposable
{
    public const int k_MaxPages = 256;

    public VTPageCache(int sizeWide) : this()
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        if (sizeWide <= 0)
            throw new System.ArgumentException("Heap size can not be 0 or less", nameof(sizeWide));
#endif

        _pageRows = sizeWide;
        _pages    = new NativeArray<VTPage>    (sizeWide * sizeWide, Allocator.Persistent);
        _pageMap  = new NativeParallelHashMap<int, int>(sizeWide * sizeWide, Allocator.Persistent);

        _mappedPagesPerOwnerID = new(k_MaxPages, Allocator.Persistent);
        for (int i = 0; i < k_MaxPages; i++)
        {
            _mappedPagesPerOwnerID.At(i) = new(256, Allocator.Persistent);
        }

        _pageHeap  = (IndexingBinaryMinHeap*)UnsafeUtility.Malloc(sizeof(IndexingBinaryMinHeap), UnsafeUtility.AlignOf<IndexingBinaryMinHeap>(), Allocator.Persistent);
        UnsafeUtility.MemClear(_pageHeap, sizeof(IndexingBinaryMinHeap));
        _pageHeap->Ctor(sizeWide * sizeWide, Allocator.Persistent);

        for (int i = 0; i < _pages.Length; i++)
        {
            _pages.At(i).imageId = -1;
            _pageHeap->Add(0, i);
        }
    }

    public void Dispose()
    {
        _pageHeap->Dispose();
        _pages    .Dispose();
        _pageMap  .Dispose();

        for (int i = 0; i < k_MaxPages; i++)
        {
            _mappedPagesPerOwnerID.At(i).Dispose();
        }

        _mappedPagesPerOwnerID.Dispose();

        UnsafeUtility.Free(_pageHeap, Allocator.Persistent);

        _pageHeap              = default;
        _pages                 = default;
        _pageMap               = default;
        _mappedPagesPerOwnerID = default;
    }

    [NativeDisableUnsafePtrRestriction] public  IndexingBinaryMinHeap*  _pageHeap;
    public  NativeArray<VTPage>     _pages;
    public  NativeParallelHashMap<int, int> _pageMap;
    private NativeArray<UnsafeList<ushort>> _mappedPagesPerOwnerID;
    public int                     _pageRows;

    public UnsafeList<ushort> MappedPagesAt(int ownerID) => _mappedPagesPerOwnerID[ownerID];
    public int PageCount => _pages.Length;

    public bool IsFull => _pageHeap->Count == PageCount;
    public bool IsCreated => _pages.IsCreated;

    public ref VTPage this[int index] => ref _pages.At(index);

    public int2 GetPageOffset(int index)
    {
        // TODO: Should use Morton encoding
        // var res = Morton.Decode((ulong)index);
        int2 res = 0;
        res.x = index % _pageRows;
        res.y = index / _pageRows;

        return (int2)res;
    }

    private ref VTPage GetPage(int index)
    {
        return ref _pages.At(index);
    }

    public bool IsMapped(int pageIndex) => GetPage(pageIndex).IsMapped;

    public void Lock(int pageIndex)
    {
        _pageHeap->Remove(pageIndex);
    }

    public void Unlock(int pageIndex, int frame)
    {
        ref var rPage = ref GetPage(pageIndex);
        uint key = ToPriorityKey(frame, rPage.mip);

        _pageHeap->Add(key, pageIndex);
    }

    public void Touch(int pageIndex, int frame)
    {
        // Assert.IsTrue(IsMapped(pageIndex));
        Assert.IsTrue(_pageHeap is not null);

        var key = _pageHeap->GetKey(pageIndex);
        var mip = MipFromKey(key);

        key = ToPriorityKey(frame, (int)mip);
        _pageHeap->Update(key, pageIndex);
    }

    public int Top() => _pageHeap->GetTopIndex();

    // public int ClosestPage(int x, int y, int mip)
    // {
    //     while (x > 1 && y > 1)
    //     {
    //         int hash = IndirectionTexelHash(x,y,mip);
    //         if(_pageMap.TryGetValue(hash, out var res))
    //             return res;
    //         
    //         x >>= 1;
    //         y >>= 1;
    //         mip++;
    //     }
    //     
    //     return -1;
    // }

    public void UnmapTop()
    {
        Unmap(_pageHeap->GetTopIndex());
    }

    public void Unmap(int pageID)
    {
        ref var rPage = ref _pages.At(pageID);

        //if (rPage.IsMapped)
        {
            bool success = _pageMap.Remove(rPage.hash);
            Assert.IsTrue(success);

            ref UnsafeList<ushort> mappedPages = ref _mappedPagesPerOwnerID.At(rPage.imageId);
            mappedPages.RemoveAtSwapBack(mappedPages.IndexOf((ushort)pageID));
        }

        rPage.hash    = 0;
        rPage.imageId = -1;

        _pageHeap->Update(0, pageID);
    }

    public int Map(int x, int y, int ownerID, int mip, int frame)
    {
        int hash = IndirectionTexelHash(x, y, mip);

        return Map(hash, x, y, ownerID, mip, frame);
    }

    public int Map(int hash, int x, int y, int ownerID, int mip, int frame)
    {
        if (_pageMap.ContainsKey(hash))
            return -1;

        var topIndex  = _pageHeap->GetTopIndex();

        ref var rPage = ref _pages.At(topIndex);
        if (rPage.IsMapped)
        {
            // Debug.LogError("This is wrong");
            return -1;
        }

        rPage.mip       = mip;
        rPage.imageId   = ownerID;
        rPage.x         = x;
        rPage.y         = y;
        rPage.hash      = hash;

        _pageMap.Add(rPage.hash, topIndex);
        Assert.IsFalse(_mappedPagesPerOwnerID.At(ownerID).Contains((ushort)topIndex));
        _mappedPagesPerOwnerID.At(ownerID).Add((ushort)topIndex);

        var key = ToPriorityKey(frame, mip);
        _pageHeap->Update(key, topIndex);

        return topIndex;
    }

    public static int IndirectionTexelHash(int x, int y, int mip)
    {
        Assert.IsTrue(x   >= 0 && x   <= 0xFFF);
        Assert.IsTrue(y   >= 0 && y   <= 0xFFF);
        Assert.IsTrue(mip >= 0 && mip <= 0xF);

        int res;
        res  = (x   & 0xFFF);
        res |= (y   & 0xFFF) << 12;
        res |= (mip & 0xF)   << 24;

        return res;
    }

    public static void DeconstructTexelHash(int hash, out int x, out int y, out int mip)
    {
        x = hash & 0xFFF;
        y = (hash >> 12) & 0xFFF;
        mip = (hash >> 24) & 0xF;
    }

    public bool HasAvailablePages(int frame, int minFramesOld)
    {
        int topIndex = Top();
        if (!this[topIndex].IsMapped)
            return true;

        uint pageFrame = FrameFromKey((uint)topIndex); // ((uint)top) >> 4;

        return (frame - pageFrame) >= minFramesOld;
    }

    private uint ToPriorityKey(int frame, int mip)
    {
        uint key = 0;
        // mip = 0xF - mip;
        key |= (uint)mip & 0xF;
        key |= (uint)frame << 4;

        return key;
    }

    private uint MipFromKey(uint key)
    {
        return (key & 0xF);
    }

    private uint FrameFromKey(uint key) => key >> 4;

    public void Clear()
    {
        for (int i = 0; i < _pages.Length; i++)
        {
            _pageHeap->Update(0, i);

            _pages.At(i) = default;
            _pages.At(i).imageId = -1;
        }

        for (int i = 0; i < _mappedPagesPerOwnerID.Length; i++)
        {
            _mappedPagesPerOwnerID.At(i).Clear();
        }

        _pageMap.Clear();
    }

    public int GetPageId(ushort feedbackValueAbsX, ushort feedbackValueAbsY, byte feedbackValueMip)
    {
        int hash = IndirectionTexelHash(feedbackValueAbsX, feedbackValueAbsY, feedbackValueMip);
        if (_pageMap.TryGetValue(hash, out var index))
            return index;

        return -1;
    }

    public void Remap(int address, int x, int y, int mip, int spaceID, int frame)
    {
        int hash = IndirectionTexelHash(x, y, mip);

        ref var page = ref GetPage(address);
        bool success = _pageMap.Remove(page.hash);

        if (page.IsMapped && page.imageId != spaceID)
        {
            ref var list = ref _mappedPagesPerOwnerID.At(page.imageId);
            list.RemoveAtSwapBack(list.IndexOf((ushort)address));
        }

        var key = ToPriorityKey(frame, mip);
        _pageHeap->Update(key, address);

        ref var rPage = ref _pages.At(address);
        rPage.mip       = mip;
        rPage.imageId   = spaceID;
        rPage.x         = x;
        rPage.y         = y;
        rPage.hash      = hash;

        _pageMap[rPage.hash] = address;
    }
}
}