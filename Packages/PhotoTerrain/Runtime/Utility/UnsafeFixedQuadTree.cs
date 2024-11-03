using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Hollow
{
internal unsafe struct UnsafeFixedQuadTree<T> : IDisposable where T : unmanaged
{
    public UnsafeFixedQuadTree(Allocator allocator, int levelCount, int minLevel = 0)
    {
        levelCount -= minLevel;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        if (levelCount < 1)
            throw new ArgumentOutOfRangeException(nameof(levelCount), "Deepness of a quad tree can not be less than 1");
#endif

        levelNodesIndices = (int*)UnsafeUtility.Malloc(sizeof(int) * (levelCount + 1), UnsafeUtility.AlignOf<int>(), allocator);

        int totalNodesCount = 0;

        for (int i = 0; i < levelCount; i++)
        {
            levelNodesIndices[i] =  totalNodesCount;
            totalNodesCount      += (int)Math.Pow(4, i);
        }

        levelNodesIndices[levelCount] = totalNodesCount;

        nodes = (T*)UnsafeUtility.Malloc(sizeof(T) * totalNodesCount, UnsafeUtility.AlignOf<T>(), allocator);
        UnsafeUtility.MemClear(nodes, sizeof(T) * totalNodesCount);

        this.allocator = allocator;

        LevelCount = levelCount;
        NodesCount = totalNodesCount;
    }

    public UnsafeFixedQuadTree(T* nodes, int* levelNodesIndices, int levelCount, int nodeCount)
    {
        allocator              = Allocator.Invalid;
        this.nodes             = nodes;
        this.levelNodesIndices = levelNodesIndices;
        LevelCount             = levelCount;
        NodesCount             = nodeCount;
    }

    private Allocator allocator;

    [NativeDisableUnsafePtrRestriction] private T* nodes;

    [NativeDisableUnsafePtrRestriction] private int* levelNodesIndices;

    public int LevelCount { get; private set; }

    public int NodesCount { get; private set; }

    public ref T this[int index]
    {
        get
        {
            CheckIndex(index);

            return ref nodes[index];
        }
    }

    void CheckIndex(int index)
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        if (nodes is null)
            throw new System.InvalidOperationException("Tree is in invalid state (Nodes array is null)");

        if (index < 0 || index >= NodesCount)
            throw new System.IndexOutOfRangeException($"Index '{index}' is out of bounds (Length: '{NodesCount}')");
#endif
    }

    public T* PtrAt(int index)
    {
        CheckIndex(index);
        return nodes + index;
    }

    public NativeArray<T> AsNativeArray()
    {
        var array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(nodes, NodesCount, allocator);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, AtomicSafetyHandle.Create());
#endif

        return array;
    }

    public bool HasChildren(int nodeIndex) => ChildrenStartIndex(nodeIndex) < NodesCount;

    public readonly int CountNodesAtLevel (int level)     => 1 << (2 * level);
    public readonly int ChildrenStartIndex(int nodeIndex) => (nodeIndex << 2) + 1;

    public readonly int ParentIndex       (int nodeIndex) => (nodeIndex - 1) >> 2; // integer division always floors so it will work for any child,

    // also mono is bad at math optimization so use shifts instead
    public readonly int FirstNodeAtLevel  (int level) => levelNodesIndices[level];

    public readonly int GetNodeLevel      (int nodeIndex)
    {
        return math.floorlog2(nodeIndex * 3 + 1) >> 1;
    }

    public readonly int IteratorAtLevelStart(int level) => FirstNodeAtLevel(level);
    public readonly int IteratorAtLevelEnd  (int level) => levelNodesIndices[level + 1];

    public void Dispose()
    {
        UnsafeUtility.Free(this.nodes,             allocator);
        UnsafeUtility.Free(this.levelNodesIndices, allocator);
    }

    //public readonly FixedQuadTree<T> ToSafeQuadTree()
    //{
    //    FixedQuadTree<T> result = new();
    //    result.levelNodesIndices = new Span<int>(levelNodesIndices, LevelCount+1).ToArray();
    //    result.nodes             = new Span<T>  (nodes, NodesCount).ToArray();
    //    
    //    return result;
    //}

    public void Clear()
    {
        UnsafeUtility.MemClear(nodes, NodesCount * sizeof(T));
    }
}
}