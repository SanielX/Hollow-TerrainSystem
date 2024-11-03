using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using static Unity.Mathematics.math;

namespace Hollow.Rendering
{
/// <summary>
/// Absoluterly idiotic atlas allocator that can supports freeing individual chunks
/// </summary>
[BurstCompile]
internal class AtlasAllocatorPD2 : IDisposable
{
    public struct AtlasAllocation
    {
        /// <summary>
        /// Count of children that are allocated below this node
        /// </summary>
        public ushort refCount;

        public bool isAllocated;
    }

    public AtlasAllocatorPD2(int textureSize)
    {
        this.textureSize = textureSize;
        mipCount         = floorlog2(textureSize) + 1;
        allocator        = new(Allocator.Persistent, mipCount);
    }

    public AtlasAllocatorPD2(int textureSize, int minSubTextureSize)
    {
        int minMip = floorlog2(minSubTextureSize) + 1;
        this.textureSize = textureSize;
        mipCount         = floorlog2(textureSize) + 1;
        allocator        = new(Allocator.Persistent, mipCount, minMip);
    }

    private int                                   textureSize;
    private int                                   mipCount;
    private UnsafeFixedQuadTree<AtlasAllocation> allocator;

    public int NodeCount   => allocator.NodesCount;
    public int TextureSize => textureSize;
    public int MipCount    => mipCount;

    public int2 TextureCoords(int nodeIndex)
    {
        TextureCoords(nodeIndex, textureSize, out var res, out _);
        return res;
    }

    public int2 TextureCoords(int nodeIndex, out int level)
    {
        TextureCoords(nodeIndex, textureSize, out var res, out level);
        return res;
    }

    static readonly uint[] morton2dLUT =
    {
        0x0000, 0x0001, 0x0004, 0x0005, 0x0010, 0x0011, 0x0014, 0x0015,
        0x0040, 0x0041, 0x0044, 0x0045, 0x0050, 0x0051, 0x0054, 0x0055,
        0x0100, 0x0101, 0x0104, 0x0105, 0x0110, 0x0111, 0x0114, 0x0115,
        0x0140, 0x0141, 0x0144, 0x0145, 0x0150, 0x0151, 0x0154, 0x0155,
        0x0400, 0x0401, 0x0404, 0x0405, 0x0410, 0x0411, 0x0414, 0x0415,
        0x0440, 0x0441, 0x0444, 0x0445, 0x0450, 0x0451, 0x0454, 0x0455,
        0x0500, 0x0501, 0x0504, 0x0505, 0x0510, 0x0511, 0x0514, 0x0515,
        0x0540, 0x0541, 0x0544, 0x0545, 0x0550, 0x0551, 0x0554, 0x0555,
        0x1000, 0x1001, 0x1004, 0x1005, 0x1010, 0x1011, 0x1014, 0x1015,
        0x1040, 0x1041, 0x1044, 0x1045, 0x1050, 0x1051, 0x1054, 0x1055,
        0x1100, 0x1101, 0x1104, 0x1105, 0x1110, 0x1111, 0x1114, 0x1115,
        0x1140, 0x1141, 0x1144, 0x1145, 0x1150, 0x1151, 0x1154, 0x1155,
        0x1400, 0x1401, 0x1404, 0x1405, 0x1410, 0x1411, 0x1414, 0x1415,
        0x1440, 0x1441, 0x1444, 0x1445, 0x1450, 0x1451, 0x1454, 0x1455,
        0x1500, 0x1501, 0x1504, 0x1505, 0x1510, 0x1511, 0x1514, 0x1515,
        0x1540, 0x1541, 0x1544, 0x1545, 0x1550, 0x1551, 0x1554, 0x1555,
        0x4000, 0x4001, 0x4004, 0x4005, 0x4010, 0x4011, 0x4014, 0x4015,
        0x4040, 0x4041, 0x4044, 0x4045, 0x4050, 0x4051, 0x4054, 0x4055,
        0x4100, 0x4101, 0x4104, 0x4105, 0x4110, 0x4111, 0x4114, 0x4115,
        0x4140, 0x4141, 0x4144, 0x4145, 0x4150, 0x4151, 0x4154, 0x4155,
        0x4400, 0x4401, 0x4404, 0x4405, 0x4410, 0x4411, 0x4414, 0x4415,
        0x4440, 0x4441, 0x4444, 0x4445, 0x4450, 0x4451, 0x4454, 0x4455,
        0x4500, 0x4501, 0x4504, 0x4505, 0x4510, 0x4511, 0x4514, 0x4515,
        0x4540, 0x4541, 0x4544, 0x4545, 0x4550, 0x4551, 0x4554, 0x4555,
        0x5000, 0x5001, 0x5004, 0x5005, 0x5010, 0x5011, 0x5014, 0x5015,
        0x5040, 0x5041, 0x5044, 0x5045, 0x5050, 0x5051, 0x5054, 0x5055,
        0x5100, 0x5101, 0x5104, 0x5105, 0x5110, 0x5111, 0x5114, 0x5115,
        0x5140, 0x5141, 0x5144, 0x5145, 0x5150, 0x5151, 0x5154, 0x5155,
        0x5400, 0x5401, 0x5404, 0x5405, 0x5410, 0x5411, 0x5414, 0x5415,
        0x5440, 0x5441, 0x5444, 0x5445, 0x5450, 0x5451, 0x5454, 0x5455,
        0x5500, 0x5501, 0x5504, 0x5505, 0x5510, 0x5511, 0x5514, 0x5515,
        0x5540, 0x5541, 0x5544, 0x5545, 0x5550, 0x5551, 0x5554, 0x5555
    };

    [BurstCompile]
    public static ulong NodeIndex(uint x, uint y, int nodeLevel)
    {
        var p = 1 << (2 * nodeLevel);
        var levelStart = (1 - p) / (1 - 4);

        ulong key;
        if (Unity.Burst.Intrinsics.X86.Bmi2.IsBmi2Supported)
        {
            const ulong x2_mask = 0xAAAAAAAAAAAAAAAA; //0b...10101010
            const ulong y2_mask = 0x5555555555555555; //0b...01010101

            key = Unity.Burst.Intrinsics.X86.Bmi2.pdep_u64(y, y2_mask) | Unity.Burst.Intrinsics.X86.Bmi2.pdep_u64(x, x2_mask);
        }
        else
        {
            key = morton2dLUT[(x >> 24) & 0xFF] << 1 |
                  morton2dLUT[(y >> 24) & 0xFF];
            key = key << 16 |
                  morton2dLUT[(x >> 16) & 0xFF] << 1 |
                  morton2dLUT[(y >> 16) & 0xFF];
            key = key << 16 |
                  morton2dLUT[(x >> 8) & 0xFF] << 1 |
                  morton2dLUT[(y >> 8) & 0xFF];
            key = key << 16 |
                  morton2dLUT[x & 0xFF] << 1 |
                  morton2dLUT[y & 0xFF];
        }

        return (ulong)levelStart + key;
    }

    [BurstCompile]
    public static void TextureCoords(int nodeIndex, int textureSize, out int2 coords, out int outLevel)
    {
        var m = nodeIndex * 3 + 1;

        var nodeLevel = math.floorlog2(m) >> 1;
        var p = 1 << (2 * nodeLevel);
        var levelStart = (1 - p) / (1 - 4);

        var sectorSize = textureSize >> nodeLevel;

        var i = (ulong)(nodeIndex - levelStart);

        outLevel = nodeLevel;

        ulong x, y;
        if (Unity.Burst.Intrinsics.X86.Bmi2.IsBmi2Supported)
        {
            const ulong x2_mask = 0xAAAAAAAAAAAAAAAA; //0b...10101010
            const ulong y2_mask = 0x5555555555555555; //0b...01010101

            x = Unity.Burst.Intrinsics.X86.Bmi2.pext_u64(i, x2_mask);
            y = Unity.Burst.Intrinsics.X86.Bmi2.pext_u64(i, y2_mask);
        }
        else
        {
            x = compactBits(i >> 1);
            y = compactBits(i);
        }

        coords = int2((int)x, (int)y) * sectorSize;
    }

    // https://github.com/aavenel/mortonlib/blob/master/include/morton2d.h
    static ulong compactBits(ulong n)
    {
        n &= 0x5555555555555555;
        n = (n ^ (n >> 1)) & 0x3333333333333333;
        n = (n ^ (n >> 2)) & 0x0f0f0f0f0f0f0f0f;
        n = (n ^ (n >> 4)) & 0x00ff00ff00ff00ff;
        n = (n ^ (n >> 8)) & 0x0000ffff0000ffff;
        n = (n ^ (n >> 16)) & 0x00000000ffffffff;

        return n;
    }

    public bool Free(int nodeIndex)
    {
        if (!allocator[nodeIndex].isAllocated)
        {
            return false;
            // throw new System.InvalidOperationException($"Node {nodeIndex} is not allocated therefore can't be freed");
        }

        allocator[nodeIndex].isAllocated = false;
        decrementRefCountInParent(nodeIndex);
        return true;

        void decrementRefCountInParent(int nodeIndex)
        {
            var parent = allocator.ParentIndex(nodeIndex);

            allocator[parent].refCount--;
            Assert.IsTrue(allocator[parent].refCount >= 0);

            if (parent != 0)
                decrementRefCountInParent(parent);
        }
    }

    public int Alloc(int size)
    {
        int level = TextureSizeToLevel(size);
#if UNITY_ASSERTIONS
        if (level < 0 || level > mipCount)
            throw new System.IndexOutOfRangeException($"Texture size '{size}' mip='{floorlog2(size)}' is out of range for Atlas allocator (size: {mipCount})");
#endif

        if (level == 0)
        {
            if (allocator[0].isAllocated || allocator[0].refCount > 0)
                return -1;

            return 0;
        }

        return AllocateNode_Bursted(level, ref allocator);
    }

    public int Alloc(int size, out int2 texCoords)
    {
        texCoords = default;
        int level = TextureSizeToLevel(size);
#if UNITY_ASSERTIONS
        if (level < 0 || level > mipCount)
            throw new System.IndexOutOfRangeException($"Texture size '{size}' mip='{floorlog2(size)}' is out of range for Atlas allocator (size: {mipCount})");
#endif

        if (level == 0)
        {
            if (allocator[0].isAllocated || allocator[0].refCount > 0)
                return -1;

            return 0;
        }

        return AllocateNode_Bursted(level, textureSize, out texCoords, ref allocator);
    }

    /// <summary>
    /// Allocates specific node at X, Y and level. X and Y are not scaled by texture size
    /// </summary>
    public int AllocRaw(int x, int y, int level)
    {
        return AllocateRaw_Bursted(x, y, level, ref allocator);
    }

    [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
    private static int AllocateRaw_Bursted(int x, int y, int level, ref UnsafeFixedQuadTree<AtlasAllocation> allocator)
    {
        ulong nodeIndex = NodeIndex((uint)x, (uint)y, level);

        allocator[(int)nodeIndex].isAllocated = true;
        MarkAllocInParents(ref allocator, (int)nodeIndex);

        return (int)nodeIndex;
    }

    [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
    private static int AllocateNode_Bursted(int level, int textureSize, out int2 coords, ref UnsafeFixedQuadTree<AtlasAllocation> allocator)
    {
        int nodeID = AllocateNode_Bursted(level, ref allocator);
        coords = default;
        if (nodeID >= 0)
        {
            TextureCoords(nodeID, textureSize, out coords, out _);
        }

        return nodeID;
    }

    [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
    private static int AllocateNode_Bursted(int level, ref UnsafeFixedQuadTree<AtlasAllocation> allocator)
    {
        var iStart = allocator.IteratorAtLevelStart(level);
        var iEnd   = allocator.IteratorAtLevelEnd  (level);

        for (int i = iStart; i < iEnd; i++)
        {
            if (allocator[i].isAllocated || allocator[i].refCount > 0)
                continue;

            // For each child we want to walk up the tree and check whether any chunk
            // was already allocated. If so, then we can skip by amount of elements that is already occupied parent contains

            {
                int skip = nodesSkip(allocator, i, level);
                if (skip > 0)
                {
                    // If child at 0 was allocated, then we need to skip 'skip - 1' elements
                    int localChildIndex = i - iStart;
                    i += max(0, skip - localChildIndex);
                    continue;
                }
            }

            {
                allocator[i].isAllocated = true;
                MarkAllocInParents(ref allocator, i);

                return i;
            }
        }

        return -1;
    }

    private static void MarkAllocInParents(ref UnsafeFixedQuadTree<AtlasAllocation> allocator, int nodeIndex)
    {
        var parent = allocator.ParentIndex(nodeIndex);

        allocator[parent].refCount++;

        if (parent != 0)
            MarkAllocInParents(ref allocator, parent);
    }

    private static int nodesSkip(UnsafeFixedQuadTree<AtlasAllocation> allocator, int nodeIndex, int initialLevel)
    {
        int allocLevel = initialLevel - findAllocationLevel(nodeIndex, initialLevel);

        // Difference of geometrical sums
        // 2+r^(i-1)-r^i
        // --------------
        //     1 - r

        const int r = 4;
        int i = allocLevel + 1;

        int i0 = 1 << (2 * (i - 1)); // 4^x = 2^2x
        int i1 = 1 << (2 * (i));

        int res = (int) ((2 + i0 - i1)) / (1 - r);

        return max(0, res - 1);

        int findAllocationLevel(int nodeIndex, int level)
        {
            var parent = allocator.ParentIndex(nodeIndex);

            if (allocator[parent].isAllocated)
                return level - 1;

            if (parent == 0)
                return initialLevel;

            return findAllocationLevel(parent, level - 1);
        }
    }

    private int TextureSizeToLevel(int size) => mipCount - floorlog2(size) - 1;

    public void Dispose()
    {
        allocator.Dispose();
        GC.SuppressFinalize(this);
    }

#if UNITY_ASSERTIONS
    ~AtlasAllocatorPD2()
    {
        Debug.LogError("Atlas allocator was not disposed properly!");
        Dispose();
    }
#endif
    public void Reset()
    {
        allocator.Clear();
    }
}
}