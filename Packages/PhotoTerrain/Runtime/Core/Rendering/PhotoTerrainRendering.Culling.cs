using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hollow.TerrainSystem
{
public static partial class PhotoTerrainRenderer
{
    public enum CullMode
    {
        /// <summary> Used to render terrain on screen, must be performed before first geometry pass (prepass, deferred etc) </summary>
        Normal,

        /// <summary> Must be called for every shadowcaster, uses separate visible patches list </summary>
        Shadowcaster,
    }

    public struct CullRequest
    {
        public CullMode mode;

        /// <summary> Must not be null, culling is only done in immediate mode </summary>
        public CommandBuffer cmd;

        public Camera camera;
        public int    verticalResolution;
    }
    
    private static Vector4[] frustumPlanes = new Vector4[6]; 

    public static void CullTerrainInstances(in CullRequest cullRequest)
    {
        cullRequest.cmd.BeginSample("Cull terrain instances");
        
        RenderUtility.WriteCullFrustumPlanes(cullRequest.camera, frustumPlanes);
        cullRequest.cmd.SetComputeVectorArrayParam(TerrainResources.Instance.TerrainLODCullingShader, "CameraFrustumPlanes", frustumPlanes);
        
        for (int i = 0; i < PhotoTerrainWorld.ActiveTerrains.Count; i++)
        {
            var terrain = PhotoTerrainWorld.ActiveTerrains[i];
            if (terrain.isPotentiallyVisibleNow)
            {
                CullTerrainInstance(cullRequest.cmd, new()
                {
                    terrain             = terrain,
                    indirectArguments   = terrain.finalIndirectArgumentsBuffer,
                    finalNodeList       = terrain.visibleNodeIndicesBuffer,
                    camera              = cullRequest.camera,
                    verticalResolution  = cullRequest.verticalResolution,
                    populateLODLevelMap = true,
                });
            }
        }

        cullRequest.cmd.EndSample("Cull terrain instances");
    }

    private static bool IsTerrainVisible(PhotoTerrain terrain, Camera camera)
    {
        if (!terrain.IsValidInstance())
            return false;

        if (camera.cameraType == CameraType.Preview)
            return false;

#if UNITY_EDITOR
        if (camera.cameraType == CameraType.SceneView && SceneVisibilityManager.instance.IsHidden(terrain.gameObject))
            return false;
#endif

        return true;
    }

    public struct TerrainCullParams
    {
        public bool   populateLODLevelMap;
        public int    verticalResolution;
        public Camera camera;

        public PhotoTerrain   terrain;
        public GraphicsBuffer indirectArguments;
        public GraphicsBuffer finalNodeList;

        public Ray? ray;
    }

    internal static void CullTerrainInstanceLOD(CommandBuffer cmd, PhotoTerrain terrain, int lodLevel, GraphicsBuffer indirectArguments)
    {
        NativeArray<GraphicsBuffer.IndirectDrawIndexedArgs> indirectArgs = new(1, Allocator.Temp);
        indirectArgs[0] = new()
        {
            instanceCount         = 0,
            indexCountPerInstance = (uint)TerrainResources.Instance.PatchMesh.IndexBuffer.count,
        };

        cmd.SetBufferData(indirectArguments, indirectArgs);
        ComputeShader cullShader = TerrainResources.Instance.TerrainLODCullingShader;

        int size = terrain.lodNodeIndexLookupTexture.width >> lodLevel;

        int kernel = cullShader.FindKernel("CSSelectSpecificLOD");

        cmd.SetComputeTextureParam(cullShader, kernel, "LODNodeMap",                        terrain.lodNodeIndexLookupTexture);
        cmd.SetComputeBufferParam (cullShader, kernel, "TerrainInstanceIndirectArgsBuffer", indirectArguments);
        cmd.SetComputeBufferParam (cullShader, kernel, "VisiblePatchesList",                terrain.visibleNodeIndicesBuffer);
        cmd.SetComputeBufferParam (cullShader, kernel, "LODSeamMap",                        terrain.nodeSplitAndSeamArrayBuffer);
        cmd.SetComputeIntParam    (cullShader, "CullLODLevel", lodLevel);
        cmd.SetComputeIntParam    (cullShader, "CullLODSize",  size);

        cmd.DispatchCompute(cullShader, kernel, (size + 7) / 8, (size + 7) / 8, 1);
    }

    internal static void CullTerrainInstance(CommandBuffer cmd, in TerrainCullParams cullParams)
    {
        var terrain = cullParams.terrain;

        int lodLevel    = terrain.ComputeVisibleLODLevelCount() - 1;
        int maxLodLevel = lodLevel;

        // Init data for culling, setup cullState, node sets, etc
        {
            int lodLookupSize           = terrain.lodNodeIndexLookupTexture.width;
            int lodLookupSizeAtFirstMip = lodLookupSize >> lodLevel;
            int nodeCountsAtFirstMip    = lodLookupSizeAtFirstMip * lodLookupSizeAtFirstMip;

            NativeArray<uint3> initialDispatchSize = new(1, Allocator.Temp);
            initialDispatchSize[0] = new(((uint)nodeCountsAtFirstMip + 63) / 64, 1, 1);
            cmd.SetBufferData(workingCullIndirectArguments, initialDispatchSize);

            NativeArray<uint2> pendingCoords = new(nodeCountsAtFirstMip, Allocator.Temp);
            for (uint x = 0; x < lodLookupSizeAtFirstMip; x++)
            {
                for (uint y = 0; y < lodLookupSizeAtFirstMip; y++)
                {
                    pendingCoords[(int)(x + y * lodLookupSizeAtFirstMip)] = new uint2(x, y);
                }
            }

            cmd.SetBufferData(workingNodeSet0, pendingCoords);

            initialDispatchSize[0] = new((uint)nodeCountsAtFirstMip, 0, 0);
            cmd.SetBufferData(workingCullState, initialDispatchSize);

            NativeArray<GraphicsBuffer.IndirectDrawIndexedArgs> indirectArgs = new(1, Allocator.Temp);
            indirectArgs[0] = new()
            {
                instanceCount         = 0,
                indexCountPerInstance = (uint)TerrainResources.Instance.PatchMesh.IndexBuffer.count,
            };
            cmd.SetBufferData(cullParams.indirectArguments, indirectArgs);

            // Temp allocator still needs freeing because it might have silently fell back on another allocator
            initialDispatchSize.Dispose();
            indirectArgs       .Dispose();
            pendingCoords      .Dispose();
        }

        // TODO: This is okay for perspective, but orthographic projection ends up having very low FOV, meaning LOD0 is selected all the time
        var cameraNearClipPlane = cullParams.camera.nearClipPlane;
        float pixelError        = terrain.pixelError * (cameraNearClipPlane * Mathf.Sin(cullParams.camera.fieldOfView * 0.5f * Mathf.Deg2Rad));

        float         lodDistanceMultiplier = ((cameraNearClipPlane * cullParams.verticalResolution) / (2 * pixelError));
        ComputeShader cullShader            = TerrainResources.Instance.TerrainLODCullingShader;

        GraphicsBuffer workingSetA              = workingNodeSet0;
        GraphicsBuffer workingSetB              = workingNodeSet1;
        var            cullWithRay              = cullParams.ray is not null;

        int            selectLODPatchesKernel   = cullWithRay ? cullShader.FindKernel("CSSelectLODPatchesWithRay") : cullShader.FindKernel("CSSelectLODPatches");
        int            computeArgsKernel        = cullShader.FindKernel("CSComputeIndirectArgs");
        int            computeLODLevelMapKernel = cullShader.FindKernel("CSComputeLODMap");
        int            computeLODSeamsKernel    = cullShader.FindKernel("CSComputeLODSeams");

        // Culling with ray is used for scene picking, removing all LOD slices that do not intersect mouse
        if (cullWithRay)
        {
            cmd.SetComputeVectorParam(cullShader, "LODSelectionRayOrigin",    cullParams.ray.Value.origin);
            cmd.SetComputeVectorParam(cullShader, "LODSelectionRayDirection", cullParams.ray.Value.direction);
        }

        while (lodLevel >= 0)
        {
            cmd.SetComputeConstantBufferParam(cullShader, "PhotoTerrainInstanceData", terrain.terrainGPUDataCBuffer.GraphicsBuffer,
                                              0, terrain.terrainGPUDataCBuffer.GraphicsBuffer.stride);
            cmd.SetComputeBufferParam(cullShader, selectLODPatchesKernel, "WorkingBufferA",     workingSetA);
            cmd.SetComputeBufferParam(cullShader, selectLODPatchesKernel, "WorkingBufferB",     workingSetB);
            cmd.SetComputeBufferParam(cullShader, selectLODPatchesKernel, "VisiblePatchesList", cullParams.finalNodeList);
            cmd.SetComputeBufferParam(cullShader, selectLODPatchesKernel, "CullingStateBuffer", workingCullState);

            cmd.SetComputeIntParam   (cullShader, "LODTreeLevel", lodLevel);
            cmd.SetComputeVectorParam(cullShader, "CameraPosition", cullParams.camera.transform.position);
            cmd.SetComputeFloatParam (cullShader, "LODMaxDistanceMultiplier", lodDistanceMultiplier);

            cmd.SetComputeTextureParam(cullShader, selectLODPatchesKernel, "LODNodeMap", terrain.lodNodeIndexLookupTexture);
            cmd.SetComputeBufferParam (cullShader, selectLODPatchesKernel, "LODNodeList", terrain.nodeArrayBuffer);

            cmd.SetComputeBufferParam (cullShader, selectLODPatchesKernel, "SplitMapRW", terrain.nodeSplitAndSeamArrayBuffer);

            cmd.DispatchCompute(cullShader, selectLODPatchesKernel, workingCullIndirectArguments, 0);

            // if (lodLevel > 0)
            {
                cmd.SetComputeBufferParam(cullShader, computeArgsKernel, "WorkingIndirectArguments",          workingCullIndirectArguments);
                cmd.SetComputeBufferParam(cullShader, computeArgsKernel, "TerrainInstanceIndirectArgsBuffer", cullParams.indirectArguments);
                cmd.SetComputeBufferParam(cullShader, computeArgsKernel, "CullingStateBuffer",                workingCullState);
                cmd.SetComputeIntParam   (cullShader, "PassTotalNodeCountToNextJob", lodLevel == 0 ? 1 : 0);

                cmd.DispatchCompute(cullShader, computeArgsKernel, 1, 1, 1);

                var workingSetTemp = workingSetA;
                workingSetA = workingSetB;
                workingSetB = workingSetTemp;
            }

            lodLevel--;
        }

        // This is cool and all but this kind of stitching won't work across different terrain instances! Damn it!
        if (cullParams.populateLODLevelMap)
        {
            int lod0Resolution = terrain.ComputeLOD0GridResolution();
            cmd.SetComputeBufferParam (cullShader, computeLODLevelMapKernel, "SplitMap", terrain.nodeSplitAndSeamArrayBuffer);
            cmd.SetComputeTextureParam(cullShader, computeLODLevelMapKernel, "LODLevelMap", terrain.lodLevelLookupTexture);
            cmd.SetComputeTextureParam(cullShader, computeLODLevelMapKernel, "LODNodeMap",  terrain.lodNodeIndexLookupTexture);
            cmd.SetComputeIntParam    (cullShader, "MaxLODLevel", maxLodLevel);
            cmd.DispatchCompute       (cullShader, computeLODLevelMapKernel, (lod0Resolution + 7) / 8, (lod0Resolution + 7) / 8, 1);

            cmd.SetComputeTextureParam(cullShader, computeLODSeamsKernel, "LODNodeMap",  terrain.lodNodeIndexLookupTexture);
            cmd.SetComputeTextureParam(cullShader, computeLODSeamsKernel, "LODLevelMap", terrain.lodLevelLookupTexture);
            cmd.SetComputeBufferParam (cullShader, computeLODSeamsKernel, "LODSeamMap", terrain.nodeSplitAndSeamArrayBuffer);
            cmd.SetComputeBufferParam (cullShader, computeLODSeamsKernel, "TerrainPatchList", terrain.nodeArrayBuffer);
            cmd.SetComputeBufferParam (cullShader, computeLODSeamsKernel, "SRWVisiblePatchesList", terrain.visibleNodeIndicesBuffer);
            cmd.DispatchCompute       (cullShader, computeLODSeamsKernel, workingCullIndirectArguments, 0);
        }
    }
}
}