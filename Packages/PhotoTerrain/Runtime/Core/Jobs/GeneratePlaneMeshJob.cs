using Hollow.MeshSimplifyLib;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;

namespace Hollow.TerrainSystem
{
[BurstCompile]
public unsafe struct GenerateUnitPlaneMeshJob : IJob
{
    public bool                       CheckerboardPattern;
    public int                        Resolution;
    public NativeArray<TerrainVertex> OutputVertices;
    public NativeArray<ushort>        OutputIndices;

    public void Init(int resolution, Allocator allocator = Allocator.Temp)
    {
        Resolution          = resolution;
        CheckerboardPattern = true;
        OutputVertices      = new(Resolution * Resolution,   allocator, NativeArrayOptions.UninitializedMemory);
        OutputIndices       = new((Resolution - 1) * (Resolution - 1) * 2 * 3, allocator, NativeArrayOptions.UninitializedMemory);
    }

    public void Dispose()
    {
        OutputVertices.Dispose();
        OutputIndices.Dispose();
    }

    public void Execute()
    {
        for (int y = 0; y < Resolution; y++)
        {
            for (int x = 0; x < Resolution; x++)
            {
                Vector3 p = new(x / (float)Resolution, 0, y / (float)Resolution);
                OutputVertices[vertexCoordToIndex(x, y)] = new TerrainVertex(p);
            }
        }

        int triIndex = 0;
        for (int x = 0; x < Resolution - 1; x++)
        {
            for (int y = 0; y < Resolution - 1; y++)
            {
                // int triIndex = (x * Resolution * 2) + (y * 2);
                int vert00, vert01, vert02, vert10, vert11, vert12;

                if (CheckerboardPattern && ((x + y) & 1) == 0)
                {
                    // *--*
                    // |\ |
                    // | \|
                    // *--*
                    vert00 = vertexCoordToIndex(x + 0, y + 0);
                    vert01 = vertexCoordToIndex(x + 0, y + 1);
                    vert02 = vertexCoordToIndex(x + 1, y + 0);

                    vert10 = vertexCoordToIndex(x + 1, y + 0);
                    vert11 = vertexCoordToIndex(x + 0, y + 1);
                    vert12 = vertexCoordToIndex(x + 1, y + 1);
                }
                else
                {
                    // *--*
                    // | /|
                    // |/ |
                    // *--*
                    vert00 = vertexCoordToIndex(x + 0, y + 0);
                    vert01 = vertexCoordToIndex(x + 0, y + 1);
                    vert02 = vertexCoordToIndex(x + 1, y + 1);

                    vert10 = vertexCoordToIndex(x + 1, y + 1);
                    vert11 = vertexCoordToIndex(x + 1, y + 0);
                    vert12 = vertexCoordToIndex(x + 0, y + 0);
                }

                OutputIndices[triIndex * 3 + 0]       = (ushort)vert00;
                OutputIndices[triIndex * 3 + 1]       = (ushort)vert01;
                OutputIndices[triIndex * 3 + 2]       = (ushort)vert02;
                OutputIndices[(triIndex + 1) * 3 + 0] = (ushort)vert10;
                OutputIndices[(triIndex + 1) * 3 + 1] = (ushort)vert11;
                OutputIndices[(triIndex + 1) * 3 + 2] = (ushort)vert12;

                triIndex += 2;
            }
        }

        /*(uint* destination,
           uint* indices,
           int index_count,
           int vertex_count)*/
        meshopt.optimizeVertexCacheShort((ushort*)OutputIndices.GetUnsafePtr(), (ushort*)OutputIndices.GetUnsafePtr(), OutputIndices.Length,
                                         OutputVertices.Length);
    }

    int vertexCoordToIndex(int x, int y)
    {
        return x + Resolution * y;
    }
}

// [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
// public unsafe struct GeneratePlaneMeshJob : IJob
// {
//     [ReadOnly] public float2 offset;
//     [ReadOnly] public float2 padding;
//     [ReadOnly] public int2   resolution;
//     [ReadOnly] public bool   checkerboardPattern;
//     
//     public float3             terrainSize;
//     [NativeDisableUnsafePtrRestriction]
//     public float* outputMaxHeight;
//     
//     [ReadOnly] public NativeArray2D<byte>  holesMap;
//     [ReadOnly] public NativeArray2D<float> heightmap;
//
//     [NativeDisableContainerSafetyRestriction]
//     public NativeResizableArray<float3> outputVertices;
//
//     [NativeDisableContainerSafetyRestriction]
//     public NativeResizableArray<int> outputIndices;
//
//     [NativeDisableUnsafePtrRestriction]
//     public MeshSection* outputSection;
//
//     public void Execute()
//     {
//         Assert.IsTrue(outputIndices.IsCreated && outputVertices.IsCreated);
//
//         Assert.IsTrue(all(resolution >= 2), "Resolution must be equal or greated than 2");
//         // Assert.IsTrue(outputVerticesStartIndex is not null, "Output verticies start index must not be null");
//         // Assert.IsTrue(outputIndicesStartIndex is not null,  "Output indices start index must not be null");
//
//         Hint.Assume(all(resolution >= 2));
//
//         int indicesCount  = ((resolution.x - 1) * (resolution.y - 1)) * 2 * 3;
//         int verticesCount = resolution.x * resolution.y;
//
//         Assert.IsTrue(verticesCount <= ushort.MaxValue, "Plane resolution is too high and won't be able to fit into 16bit index buffer");
//         Hint  .Assume(verticesCount <= ushort.MaxValue);
//
//         var baseVertex = outputVertices.AddLengthAtomic(verticesCount);
//
//         float3* vertices = outputVertices.GetUnsafeArray()->data;
//
//         float maxHeight = 0;
//         for (int i = 0; i < resolution.y; i++)
//         {
//             for (int j = 0; j < resolution.x; j++)
//             {
//                 float3 vertex = float3(j * padding.x + offset.x, 0, i * padding.y + offset.y);
//                 int2 coords = (int2)math.round((heightmap.Rows-1) * (vertex.xz / terrainSize.xz));
//                 vertex.y = lerp(0, terrainSize.y, heightmap.ReadAt(coords.x, coords.y));
//                 maxHeight = max(maxHeight, vertex.y);
//                 
//                 vertices[baseVertex+vertexCoordToIndex(j, i)] = vertex;
//             }
//         }
//         
//         if(outputMaxHeight is not null)
//             outputMaxHeight[0] = maxHeight;
//
//         // int* triangles = outputIndices.GetUnsafeArray()->data; 
//         
//         NativeList<int> triangles  = new(indicesCount,  Allocator.Temp);
//         
//         for (int x = 0; x < resolution.x - 1; x++)
//         {
//             for (int y = 0; y < resolution.y - 1; y++)
//             {
//                 int triIndex = (x * (resolution.x-1) * 2) + (y * 2);
//
//                 int vert00, vert01, vert02, vert10, vert11, vert12;
//                 
//                 if (checkerboardPattern && ((x + y) & 1) == 0)
//                 {
//                     vert00 = vertexCoordToIndex(x + 0, y + 0);
//                     vert01 = vertexCoordToIndex(x + 0, y + 1);
//                     vert02 = vertexCoordToIndex(x + 1, y + 0);
//                                                                     
//                     vert10 = vertexCoordToIndex(x + 1, y + 0);
//                     vert11 = vertexCoordToIndex(x + 0, y + 1);
//                     vert12 = vertexCoordToIndex(x + 1, y + 1);
//                 }                                                   
//                 else                                                
//                 {                                                   
//                     vert00 = vertexCoordToIndex(x + 0, y + 0);
//                     vert01 = vertexCoordToIndex(x + 0, y + 1);
//                     vert02 = vertexCoordToIndex(x + 1, y + 1);
//                                                                     
//                     vert10 = vertexCoordToIndex(x + 1, y + 1);
//                     vert11 = vertexCoordToIndex(x + 1, y + 0);
//                     vert12 = vertexCoordToIndex(x + 0, y + 0);
//                 }
//                 
//                 {
//                     triangles.Add(vert00);
//                     triangles.Add(vert01);
//                     triangles.Add(vert02);
//                 }
//                 {
//                     triangles.Add(vert10);
//                     triangles.Add(vert11);
//                     triangles.Add(vert12);
//                 }
//             }
//         }
//     }
//
//     int vertexCoordToIndex(int x, int y)
//     {
//         return x + resolution.x * y;
//     }
// }
}