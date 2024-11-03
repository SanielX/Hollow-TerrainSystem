// https://github.com/zeux/meshoptimizer
// Modified by me by adding C# bindings. Library is compiled in debug mode so I wouldn't recommend using in out of editor

using System.Runtime.InteropServices;

namespace Hollow.MeshSimplifyLib
{
[StructLayout(LayoutKind.Sequential)]
public struct meshopt_Meshlet
{
    /* offsets within meshlet_vertices and meshlet_triangles arrays with meshlet data */
    public uint vertex_offset;
    public uint triangle_offset;

    /* number of vertices and triangles used in the meshlet; data is stored in consecutive range defined by offset and count */
    public uint vertex_count;
    public uint triangle_count;
};

public unsafe class meshopt
{
    private delegate void optimizeVertexCache_Delegate(uint* destination,
                                                       uint* indices,
                                                       int   index_count,
                                                       int   vertex_count);

    private delegate void optimizeVertexCacheShort_Delegate(uint* destination,
                                                            uint* indices,
                                                            int   index_count,
                                                            int   vertex_count);

    private delegate void optimizeOverdraw_Delegate(uint*  destination,
                                                    uint*  indices,
                                                    int    index_count,
                                                    float* vertices,
                                                    int    vertex_count,
                                                    int    vertexStride,
                                                    float  threshold);

    private delegate int optimizeVertexFetch_Delegate(void* output,
                                                      uint* indices,
                                                      int   indices_count,
                                                      void* vertices,
                                                      int   vertex_count,
                                                      int   vertex_stride);

    private delegate int buildMeshlets_Delegate(meshopt_Meshlet* meshletOutput,         uint* verticesOutputRemap,
                                                byte*            meshletTriangles,      uint* indices,              int   indicesCount,
                                                float*           vertices,              int   verticesCount,        int   vertexStride,
                                                int              maxVerticesPerMeshlet, int   maxIndicesPerMeshlet, float coneWeight);


    private delegate int buildMeshletsScan_Delegate(meshopt_Meshlet* meshletOutput, uint* meshlet_vertices, byte* meshlet_triangles,
                                                    uint*            indices,       int   index_count, int vertex_count, int max_vertices,
                                                    int              max_triangles);

    private delegate int buildMeshletsShort_Delegate(meshopt_Meshlet* meshletOutput,    uint*   verticesOutputRemap,
                                                     byte*            meshletTriangles, ushort* indices,
                                                     int              indicesCount,
                                                     float*           vertices,              int verticesCount,        int   vertexStride,
                                                     int              maxVerticesPerMeshlet, int maxIndicesPerMeshlet, float coneWeight);


    private delegate int buildMeshletsScanShort_Delegate(meshopt_Meshlet* meshletOutput, uint* meshlet_vertices, byte* meshlet_triangles,
                                                         ushort*          indices,       int   index_count,      int   vertex_count,
                                                         int              max_vertices,
                                                         int              max_triangles);

    private delegate int buildMeshletsBound_Delegate(int index_count, int max_vertices, int max_triangles);

    [DllImport("SharpMeshOptimizer.dll"), AOT.MonoPInvokeCallback(typeof(optimizeVertexCache_Delegate))]
    public static extern void optimizeVertexCache(uint* destination,
                                                  uint* indices,
                                                  int   index_count,
                                                  int   vertex_count);

    [DllImport("SharpMeshOptimizer.dll"), AOT.MonoPInvokeCallback(typeof(optimizeVertexCache_Delegate))]
    public static extern void optimizeVertexCacheShort(ushort* destination,
                                                       ushort* indices,
                                                       int     index_count,
                                                       int     vertex_count);

    [DllImport("SharpMeshOptimizer.dll"), AOT.MonoPInvokeCallback(typeof(optimizeOverdraw_Delegate))]
    public static extern void optimizeOverdraw(uint*  destination,
                                               uint*  indices,
                                               int    index_count,
                                               float* vertices,
                                               int    vertex_count,
                                               int    vertexStride,
                                               float  threshold);

    [DllImport("SharpMeshOptimizer.dll"), AOT.MonoPInvokeCallback(typeof(optimizeVertexFetch_Delegate))]
    public static extern int optimizeVertexFetch(void* output,
                                                 uint* indices,
                                                 int   indices_count,
                                                 void* vertices,
                                                 int   vertex_count,
                                                 int   vertex_stride);

    /**
     * Meshlet builder
     * Splits the mesh into a set of meshlets where each meshlet has a micro index buffer indexing into meshlet vertices that refer to the original vertex buffer
     * The resulting data can be used to render meshes using NVidia programmable mesh shading pipeline, or in other cluster-based renderers.
     * When using buildMeshlets, vertex positions need to be provided to minimize the size of the resulting clusters.
     * When using buildMeshletsScan, for maximum efficiency the index buffer being converted has to be optimized for vertex cache first.
     *
     * meshlets must contain enough space for all meshlets, worst case size can be computed with meshopt_buildMeshletsBound
     * meshlet_vertices must contain enough space for all meshlets, worst case size is equal to max_meshlets * max_vertices
     * meshlet_triangles must contain enough space for all meshlets, worst case size is equal to max_meshlets * max_triangles * 3
     * vertex_positions should have float3 position in the first 12 bytes of each vertex
     * max_vertices and max_triangles must not exceed implementation limits (max_vertices <= 255 - not 256!, max_triangles <= 512)
     * cone_weight should be set to 0 when cone culling is not used, and a value between 0 and 1 otherwise to balance between cluster size and cone culling efficiency
     */
    [DllImport("SharpMeshOptimizer.dll"), AOT.MonoPInvokeCallback(typeof(buildMeshletsBound_Delegate))]
    public static extern int buildMeshletsBound(int index_count, int max_vertices, int max_triangles);

    [DllImport("SharpMeshOptimizer.dll"), AOT.MonoPInvokeCallback(typeof(buildMeshlets_Delegate))]
    public static extern int buildMeshlets(meshopt_Meshlet* meshletOutput,         uint* verticesOutputRemap,
                                           byte*            meshletTriangles,      uint* indices,              int   indicesCount,
                                           float*           vertices,              int   verticesCount,        int   vertexStride,
                                           int              maxVerticesPerMeshlet, int   maxIndicesPerMeshlet, float coneWeight);

    [DllImport("SharpMeshOptimizer.dll"), AOT.MonoPInvokeCallback(typeof(buildMeshletsScan_Delegate))]
    public static extern int buildMeshletsScan(meshopt_Meshlet* meshletOutput, uint* meshlet_vertices, byte* meshlet_triangles,
                                               uint* indices, int index_count, int vertex_count, int max_vertices, int max_triangles);

    [DllImport("SharpMeshOptimizer.dll"), AOT.MonoPInvokeCallback(typeof(buildMeshletsShort_Delegate))]
    public static extern int buildMeshletsShort(meshopt_Meshlet* meshletOutput,    uint*   verticesOutputRemap,
                                                byte*            meshletTriangles, ushort* indices,
                                                int              indicesCount,
                                                float*           vertices, int verticesCount,
                                                int              vertexStride,
                                                int              maxVerticesPerMeshlet, int maxIndicesPerMeshlet, float coneWeight);

    [DllImport("SharpMeshOptimizer.dll"), AOT.MonoPInvokeCallback(typeof(buildMeshletsScanShort_Delegate))]
    public static extern int buildMeshletsScanShort(meshopt_Meshlet* meshletOutput, uint* meshlet_vertices, byte* meshlet_triangles,
                                                    ushort* indices, int index_count, int vertex_count, int max_vertices, int max_triangles);
}
}