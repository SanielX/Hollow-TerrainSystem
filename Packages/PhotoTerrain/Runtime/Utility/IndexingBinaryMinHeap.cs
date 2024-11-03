using System;
using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Hollow
{
/// <summary>
/// Heap that stores uint->int mapped values, useful to use when indexing an array.
/// For example, list of nodes in graph, each node has index, you can use this heap to keep track of some "key" value associated with node indices
/// </summary>
[DebuggerTypeProxy(typeof(IndixingBinaryHeapDebugView))]
public unsafe struct IndexingBinaryMinHeap : System.IDisposable
{
    [NativeDisableUnsafePtrRestriction] internal uint* _keys;
    [NativeDisableUnsafePtrRestriction] internal int * _heap;
    [NativeDisableUnsafePtrRestriction] internal int * _heapIndices;
    private Allocator    _allocator;
    private int          _capacity, _heapCount;

    public ref uint Key(int index)
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        if (index < 0 || index >= _capacity)
            throw new System.IndexOutOfRangeException($"Index '{index}' is out of capacity limit '{_capacity}'");
#endif
        return ref _keys[index];
    }

    public ref int Heap(int index)
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        if (index < 0 || index >= _capacity)
            throw new System.IndexOutOfRangeException($"Index '{index}' is out of capacity limit '{_capacity}'");
#endif
        return ref _heap[index];
    }

    public ref int HeapIndex(int index)
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        if (index < 0 || index >= _capacity)
            throw new System.IndexOutOfRangeException($"Index '{index}' is out of capacity limit '{_capacity}'");
#endif
        return ref _heapIndices[index];
    }

    public int Count    => _heapCount;
    public int Capacity => _capacity;

    public IndexingBinaryMinHeap(int capacity, Allocator allocator) : this()
    {
        Ctor(capacity, allocator);
    }

    public void Ctor(int capacity, Allocator allocator)
    {
        _capacity    = capacity;
        _allocator   = allocator;

        _heap        = (int*) UnsafeUtility.Malloc(sizeof(int)  * capacity, UnsafeUtility.AlignOf<int>(),  allocator);
        _keys        = (uint*)UnsafeUtility.Malloc(sizeof(uint) * capacity, UnsafeUtility.AlignOf<uint>(), allocator);
        _heapIndices = (int*) UnsafeUtility.Malloc(sizeof(int)  * capacity, UnsafeUtility.AlignOf<int>(),  allocator);

        UnsafeUtility.MemClear(_heapIndices, sizeof(int) * capacity);
    }

    public void Dispose()
    {
        UnsafeUtility.Free(_keys,        _allocator);
        UnsafeUtility.Free(_heap,        _allocator);
        UnsafeUtility.Free(_heapIndices, _allocator);
    }

    public bool Contains(int index)
    {
        if (index >= Count)
            return false;

        return HeapIndex(index) != -1;
    }

    public uint GetKey(int index)
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        if (!Contains(index))
            throw new System.ArgumentException($"Index '{index}' is not mapped", index.ToString());
#endif

        return Key(index);
    }

    public int GetTopIndex()
    {
        return Heap(0);
    }

    /// <param name="key">Key defines priority of an index</param>
    /// <param name="index">Index to any other container to keep "heap sorted"</param>
    /// <exception cref="OutOfMemoryException">If you add more items than capacity allows</exception>
    public void Add(uint key, int index)
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        if (_heapCount == _capacity)
            throw new System.OutOfMemoryException();

        if (Contains(index))
            throw new System.Exception($"Heap already contains index {index}");
#endif

        int heapIndex    = _heapCount++;
        Heap(heapIndex)  = index;

        Key(index)       = key;
        HeapIndex(index) = heapIndex;

        SortUp(heapIndex);
    }

    public void Update(uint key, int index)
    {
        _keys[index] = key;

        int heapIndex = _heapIndices[index];
        int parent    = (heapIndex - 1) >> 1;

        if (heapIndex > 0 && key < _keys[_heap[parent]])
        {
            SortUp(heapIndex);
        }
        else
        {
            SortDown(heapIndex);
        }
    }

    public void Remove(int index)
    {
        if (!Contains(index))
            return;

        uint key      = _keys[index];
        int heapIndex = _heapIndices[index];

        _heap[heapIndex] = _heap[--_heapCount];
        _heapIndices[_heap[heapIndex]] = heapIndex;
        _heapIndices[index] = -1;

        if (key < _keys[_heap[heapIndex]])
        {
            SortDown(heapIndex);
        }
        else
        {
            SortUp(heapIndex);
        }
    }

    void SortUp(int heapIndex)
    {
        int moving = Heap(heapIndex);
        int i      = heapIndex;
        int parent = (i - 1) >> 1;

        while (i > 0 && Key(moving) < Key(Heap(parent)))
        {
            Heap        (i)        = Heap(parent);
            HeapIndex   (_heap[i]) = i;

            i = parent;
            parent = (i - 1) >> 1;
        }

        if (i != heapIndex)
        {
            Heap(i) = moving;
            HeapIndex(_heap[i]) = i;
        }
    }

    void SortDown(int heapIndex)
    {
        int moving = Heap( heapIndex );
        int i      = heapIndex;
        int left   = (i << 1) + 1;
        int right  = left + 1;

        while ( left < _heapCount )
        {
            int smallest = left;
            if ( right < _heapCount )
            {
                ref var lefint  = ref Key(Heap(left));
                ref var righint = ref Key(Heap(right));

                smallest = (lefint < righint) ? left : right;
            }

            if (Key(Heap(smallest)) < Key(moving))
            {
                Heap(i) = Heap(smallest);
                HeapIndex(Heap(i)) = i;

                i = smallest;
                left = (i << 1) + 1;
                right = left + 1;
            }
            else
            {
                break;
            }
        }

        if ( i != heapIndex )
        {
            Heap(i) = moving;
            HeapIndex(Heap(i)) = i;
        }
    }
}

internal unsafe class IndixingBinaryHeapDebugView
{
    public IndixingBinaryHeapDebugView(IndexingBinaryMinHeap heap)
    {
        _heap = heap;
    }

    public struct KeyIndexPair
    {
        public int Key, Index;
    }

    public int Count => _heap.Count;

    public KeyIndexPair[] KeyIndexPairs
    {
        get
        {
            if (_heap._keys is null || _heap._heapIndices is null)
                return System.Array.Empty<KeyIndexPair>();

            Span<int> keys    = new(_heap._keys, _heap.Count);
            Span<int> indices = new(_heap._heapIndices, _heap.Count);

            KeyIndexPair[] pair = new KeyIndexPair[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                pair[i] = new()
                {
                    Key = keys[i],
                    Index = indices[i]
                };
            }

            return pair;
        }
    }

    public IndexingBinaryMinHeap _heap;
}
}