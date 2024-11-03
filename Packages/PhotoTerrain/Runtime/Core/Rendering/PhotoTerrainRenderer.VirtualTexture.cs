using System.Collections.Generic;
using System.Runtime.InteropServices;
using Hollow.Extensions;
using Hollow.Rendering;
using Hollow.VirtualTexturing;
using Unity.Collections;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hollow.TerrainSystem
{
[StructLayout(LayoutKind.Sequential)]
public struct TerrainWorldGridCellGPU
{
    // Would be nice to shrink by 1 more float tbh
    public float2 virtualUv;
    public float  virtualUvSize;
    public float  derivativeScale;
    public float  maxMip;
}

[StructLayout(LayoutKind.Sequential)]
public struct TerrainWorldGridCommonGPU
{
    public float2 minWorldPosition;
    public float2 worldSizeRcp;
    public int2   worldGridResolution;
    public float  cellWorldSize;
    public float  cellWorldSizeRcp;
}

public static partial class PhotoTerrainRenderer
{
    // There are 2 cases for allocating virtual image
    // A) - is a normal world cell. In this case, grid can be quite large, 12x12km is default so about 200x200 cells. (GPU buffer will be ~1.5mb)
    // B) - is for special cases such as terrains
    // During VT page rendering we only get imageId and need to render part of the world into it (so get world space rect from imageId)
    // On GPU side, we need to transform world space coordinate into cellId and get virtual uv range
    // 
    // When rendering VT page we get page info (mip) and local coordinates (in pixels) for page offset
    // This can be translated to world space rect if you know which imageId corresponds to which cell
    // Problem is that for GPU lookup to remain simple we need to have uniform 64x64m grid to sample virtual uv range from

    // When updating VT grid
    // Go over every terrain, "rasterize it" along world grid
    // check each cell and based on distance map, resize or free its virtual image
    // if cell has no image of its own, write to GPU background image of the terrain itself

    // Terrain is instantiated, when refreshes -> allocate virtual image if none (based on user settings, but say some small, 4px one)
    // 

    public static GlobalKeyword                      PT_AVT_ENABLED;
    public static AdaptiveVirtualTexture             worldVirtualTexture;
    public static PhotoTerrainWorldGrid              worldGrid;
    public static GraphicsBuffer                     worldGridBuffer;
    public static CBuffer<TerrainWorldGridCommonGPU> worldGridCommonCBuffer;

    public static VirtualImageRegion[] virtualImages;

    public struct VirtualImageRegion
    {
        public int          gridIndex;
        public int          gridSize; // both x&y, regions can only be squares size: multiple of cell size
        public PhotoTerrain terrain;
    }

    private static bool worldGridDirty;

    private static NativeArray<UnsafeUniqueIndirectionUpdateList> workers;

    static void InitVirtualTexture()
    {
        var settings = PhotoTerrainSettings.Instance;
        worldGridCommonCBuffer = new();

        virtualImages = new VirtualImageRegion[256];
        for (int i = 0; i < virtualImages.Length; i++)
        {
            virtualImages[i] = new() { gridIndex = -1, gridSize = -1, };
        }

        workers = new(Mathf.Max(1, (JobsUtility.JobWorkerCount + 1) / 2), Allocator.Persistent);
        for (int i = 0; i < workers.Length; i++)
        {
            workers[i] = new(256, Allocator.Persistent);
        }

        VirtualTextureDescriptor vtDesc;
        vtDesc.IndirectionTextureSize = settings.indirectionTextureSize;

        vtDesc.TileSize      = (ushort)settings.cacheTileSize;
        vtDesc.TileCountWide = (ushort)settings.cacheTileCountWide;
        vtDesc.TileBorder    = settings.cacheTileBorderSize;

        vtDesc.ScratchBuffersCount = settings.maxPageRenderedPerFrame;
        vtDesc.CacheTextureDescriptors = new CacheTextureDescriptor[]
        {
            new()
            {
                Name        = "_Albedo",
                Compression = CacheTextureCompression.BC1_RGB
            },
            new()
            {
                Name        = "_VTGBuffer1",
                Compression = CacheTextureCompression.BC1_RGB
            },
            new()
            {
                Name        = "_VTGBuffer2",
                Compression = CacheTextureCompression.BC1_RGB
            }
        };

        AdaptiveVirtualTextureDescriptor avtDesc = new()
        {
            maxVirtualImageSize = 512,
        };

        worldVirtualTexture           =  AdaptiveVirtualTexture.CreateInstance(vtDesc, avtDesc);
        worldVirtualTexture.hideFlags |= HideFlags.DontSave;
        RecreateWorldGrid();
#if UNITY_EDITOR
        UnityEditor.SceneView.duringSceneGui += VTSceneDebugGUI;
#endif
    }

    static void ReleaseVirtualTexture()
    {
        ObjectUtility.SafeDestroy(ref worldVirtualTexture);

        for (int i = 0; i < workers.Length; i++)
        {
            workers[i].Dispose();
        }

        workers.Dispose();
        workers = default;

        ObjectUtility.SafeDispose(ref worldGridBuffer);
        ObjectUtility.SafeDispose(ref worldGridCommonCBuffer);

        worldGrid = null;

#if UNITY_EDITOR
        virtualImages = null;

        foreach (var terrain in UnityEngine.Object.FindObjectsOfType<PhotoTerrain>())
            terrain.virtualImageId = -1;

        UnityEditor.SceneView.duringSceneGui -= VTSceneDebugGUI;
#endif
    }

    private static unsafe void RefreshVirtualTextureWorld(List<Camera> cameras)
    {
        PhotoTerrainWorld.RefreshDecalBoundsTree();

        Camera gameCamera = null;
        for (int i = 0; i < cameras.Count; i++)
        {
            if (cameras[i].cameraType == CameraType.Game &&
                cameras[i].TryGetComponent<PhotoTerrainVirtualTextureUser>(out var user) &&
                user.enabled)
            {
                gameCamera = cameras[i];
                break;
            }
        }

        if (gameCamera)
        {
            ResizeVirtualImages(gameCamera);
        }
    }

    private static unsafe void ResizeVirtualImages(Camera camera)
    {
        if (worldGrid is null || worldGrid.grid.Length == 0)
            return;

        Vector3 cameraPosition = camera.transform.position;
        var     allTerrains    = PhotoTerrainWorld.ActiveTerrains;

        var stn = PhotoTerrainSettings.Instance;

        var worldInstanceId = worldVirtualTexture.GetInstanceID();
        for (int i = 0; i < allTerrains.Count; i++)
        {
            var iTerrain = allTerrains[i];
            var pos      = iTerrain.transform.position;
            var size     = iTerrain.Size;

            int2 gridStart = worldGrid.WorldCoordToWorldGridCoord(pos.XZ());
            int2 gridEnd   = worldGrid.WorldCoordToWorldGridCoord((pos + size).XZ());

            if (iTerrain.virtualImageId < 0 || iTerrain.virtualTextureInstanceId != worldInstanceId)
            {
                int newImageId = worldVirtualTexture.AllocateVirtualImage(8);
                if (newImageId >= 0)
                {
                    iTerrain.virtualImageId = newImageId;
                    iTerrain.virtualTextureInstanceId = worldInstanceId;

                    int2 gridCoord = worldGrid.WorldCoordToWorldGridCoord(iTerrain.transform.position.XZ());
                    int  gridSize  = (int)(iTerrain.Size.x / worldGrid.cellSize);
                    virtualImages[iTerrain.virtualImageId] = new()
                    {
                        gridIndex = worldGrid.WorldGridCoordToCellIndex(gridCoord),
                        gridSize  = gridSize,
                        terrain   = iTerrain,
                    };
                }
            }

            for (int x = gridStart.x; x < gridEnd.x; x++)
            {
                for (int y = gridStart.y; y < gridEnd.y; y++)
                {
                    float2 cellCoord = new float2(x, y);
                    int  iCell     = worldGrid.WorldGridCoordToCellIndex(new(x, y));

                    var cellOffset = cellCoord * worldGrid.cellSize;

                    const float cell_height = 512;
                    var         minBounds   = new float3(cellOffset.x, 0, cellOffset.y);
                    UBounds     cellBounds  = new UBounds(minBounds, minBounds + new float3(worldGrid.cellSize, cell_height, worldGrid.cellSize));

                    float distanceToCamera = cellBounds.DistanceTo(cameraPosition);

                    int     cellSize = stn.VirtualImageResolutionAt(distanceToCamera);
                    ref var rImageId = ref worldGrid.grid[iCell].virtualImageId;

                    if (cellSize >= 0)
                    {
                        if (rImageId >= 0)
                        {
                            worldVirtualTexture.ResizeVirtualImage(rImageId, cellSize);
                        }
                        else
                        {
                            int newImageId = worldVirtualTexture.AllocateVirtualImage(cellSize);
                            if (newImageId >= 0)
                            {
                                rImageId = (short)newImageId;
                                virtualImages[rImageId] = new()
                                {
                                    gridIndex = iCell, gridSize = 1, terrain = iTerrain,
                                };
                            }
                        }
                    }
                    else
                    {
                        if (rImageId >= 0)
                        {
                            virtualImages[rImageId] = new()
                            {
                                gridIndex = -1, gridSize = -1, terrain = null,
                            };

                            worldVirtualTexture.FreeVirtualImage(rImageId);
                            rImageId = -1;
                        }
                    }
                }
            }
        }
    }

    private static unsafe void RecreateWorldGrid()
    {
        if (worldVirtualTexture)
        {
            for (int i = 0; i < virtualImages.Length; i++)
            {
                if (virtualImages[i].gridIndex >= 0)
                    worldVirtualTexture.FreeVirtualImage(i);
            }
        }

        var newWorldGrid = new PhotoTerrainWorldGrid();

        float cellSize  = PhotoTerrainSettings.Instance.adaptiveVirtualCellSize;
        int   cellCount = PhotoTerrainSettings.Instance.adaptiveVirtualWorldCellCount;
        newWorldGrid.Create(cellSize, cellCount);

        if (newWorldGrid.grid.Length == 0)
        {
            worldGrid = newWorldGrid;
            return;
        }

        worldGridBuffer?.Dispose();
        worldGridBuffer = new(GraphicsBuffer.Target.Structured, newWorldGrid.grid.Length, sizeof(TerrainWorldGridCellGPU));

        // Translate world POS to grid index
        // POS = floor((POS - minPosition) / resolution)
        // POS.Y * xCellCount + POS.X 
        ref var rWorldCommon = ref worldGridCommonCBuffer.GetDataForWriting();
        rWorldCommon.minWorldPosition    = newWorldGrid.gridWorldRange.xy;
        rWorldCommon.worldSizeRcp        = (float2)(new double2(1.0) / (newWorldGrid.gridWorldRange.zw - newWorldGrid.gridWorldRange.xy));
        rWorldCommon.worldGridResolution = new int2(newWorldGrid.xLength, newWorldGrid.yLength);
        rWorldCommon.cellWorldSize       = newWorldGrid.cellSize;
        rWorldCommon.cellWorldSizeRcp    = (float)(1.0 / newWorldGrid.cellSize);

        for (int i = 0; i < newWorldGrid.grid.Length; i++)
        {
            newWorldGrid.grid[i].virtualImageId = -1;
        }

        worldGridCommonCBuffer.Update();
        worldGrid = newWorldGrid;
    }

    public static void BindVirtualTexture(CommandBuffer cmd, PhotoTerrainVirtualTextureUser reader)
    {
        reader.BindFeedbackUAV(cmd);

        cmd.SetGlobalTexture("AVT_IndirectionTexture",   worldVirtualTexture.IndirectionTexture);
        cmd.SetGlobalTexture("AVT_AlbedoCacheTexture",   worldVirtualTexture.GetCacheTextureAt(0));
        cmd.SetGlobalTexture("AVT_GBuffer1CacheTexture", worldVirtualTexture.GetCacheTextureAt(1));
        cmd.SetGlobalTexture("AVT_GBuffer2CacheTexture", worldVirtualTexture.GetCacheTextureAt(2));
        cmd.SetGlobalConstantBuffer(worldVirtualTexture.CommonCBuffer.GraphicsBuffer, "AVT_Common_CBuffer",          0, worldVirtualTexture.CommonCBuffer.GraphicsBuffer.stride);
        cmd.SetGlobalConstantBuffer(worldGridCommonCBuffer.GraphicsBuffer,            "AVT_WorldGridCommon_CBuffer", 0, worldGridCommonCBuffer.GraphicsBuffer.stride);
        cmd.SetGlobalBuffer("AVT_WorldGrid", worldGridBuffer);
    }

    public static unsafe JobHandle ScheduleFeedbackAnalysis(PhotoTerrainVirtualTextureUser reader)
    {
        if (!reader)
            return default;

        int workPerThread = (reader.LatestData.Length + (workers.Length - 1)) / workers.Length; // ceil(a / b) with integers 

        JobHandle fbAnalysisHandle = default;
        for (int i = 0; i < workers.Length; i++)
        {
            workers.PtrAt(i)->Clear();
            AVTFeedbackAnalyzeJob fbAnalysis = new AVTFeedbackAnalyzeJob()
            {
                input                        = reader.LatestData,
                virtualImages                = worldVirtualTexture.Images,
                output                       = workers.PtrAt(i),
                inputReadRange               = new(i * workPerThread, i * workPerThread + workPerThread),
                indirectionTextureResolution = worldVirtualTexture.IndirectionTextureSize,
            };

            fbAnalysisHandle = JobHandle.CombineDependencies(fbAnalysisHandle, fbAnalysis.Schedule());
        }

        CombineIndirectionUpdateLists combineJob;
        combineJob.Lists  = workers;
        combineJob.Output = worldVirtualTexture.UpdateQueue;
        fbAnalysisHandle  = combineJob.Schedule(fbAnalysisHandle);

        IncludeLowestMipJob includeLowestMipJob;
        includeLowestMipJob.images     = worldVirtualTexture.Images;
        includeLowestMipJob.updateList = worldVirtualTexture.UpdateQueue;

        fbAnalysisHandle = includeLowestMipJob.Schedule(fbAnalysisHandle);

        fbAnalysisHandle = worldVirtualTexture.ScheduleQueuedRequestsUpdate(16, fbAnalysisHandle);
        return fbAnalysisHandle;
    }

    public static void UpdateVirtualTexture(CommandBuffer cmd, JobHandle feedbackAnalysisHandle, List<VTRenderRequest> renderRequests = null)
    {
        worldVirtualTexture.UpdateQueuedRequests(cmd, feedbackAnalysisHandle, renderRequests);

        // Update data about world sectors
        NativeArray<TerrainWorldGridCellGPU> gpuWorldCells = new(worldGrid.grid.Length, Allocator.Temp);

        var allTerrains = PhotoTerrainWorld.ActiveTerrains;
        for (int i = 0; i < allTerrains.Count; i++)
        {
            var iTerrain = allTerrains[i];
            var pos      = iTerrain.transform.position;
            var size     = iTerrain.Size;

            int2 gridStart = worldGrid.WorldCoordToWorldGridCoord(pos.XZ());
            int2 gridEnd   = worldGrid.WorldCoordToWorldGridCoord((pos + size).XZ());

            for (int x = gridStart.x; x < gridEnd.x; x++)
            {
                for (int y = gridStart.y; y < gridEnd.y; y++)
                {
                    int2 cellCoord = new int2(x, y);
                    int  iCell     = worldGrid.WorldGridCoordToCellIndex(new(x, y));

                    var imageId  = worldGrid.grid[iCell].virtualImageId;
                    // Use cell-local image only if it has anything mapped to it 
                    // and since only 1 page means 1 pixel mip, wait until we have a level more
                    // TODO: This won't work so easily, need fragment shader to output desired virtual UV otherwise we won't ever load local pages
                    if (imageId >= 0 /*&& worldVirtualTexture.VT.PageCache.MappedPagesAt(imageId).Length > 1*/)
                    {
                        var image = worldVirtualTexture.GetVirtualImage(imageId);
                        gpuWorldCells[iCell] = new()
                        {
                            virtualUv       = image.uvPosition,
                            virtualUvSize   = image.uvSize,
                            derivativeScale = image.derivativeScale,
                            maxMip          = image.mipCount - 1,
                        };
                    }
                    else if (iTerrain.virtualImageId >= 0)
                    {
                        int fallbackImageId = iTerrain.virtualImageId;

                        var image = worldVirtualTexture.GetVirtualImage(fallbackImageId);
                        Assert.IsTrue(image.size > 0, "image.size > 0");

                        VirtualImageRegion terrainImageInfo = virtualImages[fallbackImageId];
                        int2               terrainCoord     = worldGrid.IndexToGridCoord(terrainImageInfo.gridIndex);
                        float2             intOffset        = cellCoord - terrainCoord;
                        float2             offset           = new float2(image.uvSize, image.uvSize) * (intOffset / terrainImageInfo.gridSize);
                        float              uvSize           = image.uvSize / terrainImageInfo.gridSize;

                        gpuWorldCells[iCell] = new()
                        {
                            virtualUv       = image.uvPosition + offset,
                            virtualUvSize   = uvSize,
                            derivativeScale = image.derivativeScale / terrainImageInfo.gridSize,
                            maxMip          = image.mipCount - 1,
                        };
                    }
                }
            }
        }

        cmd.SetBufferData(worldGridBuffer, gpuWorldCells);
        gpuWorldCells.Dispose();
    }

    public static void VirtualTextureBlit(CommandBuffer cmd, PhotoTerrain terrain)
    {
        var mat   = terrain.Material;
        var block = terrain.propertyBlock;

        int pass = mat.FindPass("VirtualTextureBlit");
        if (pass >= 0)
            cmd.DrawProcedural(default, mat, pass, MeshTopology.Triangles, 3, 1, block);
    }

#if UNITY_EDITOR
    static void VTSceneDebugGUI(SceneView obj)
    {
        /*if (worldGrid is null)
            return;

        for (var iCell = 0; iCell < worldGrid.grid.Length; iCell++)
        {
            var    cell       = worldGrid.grid[iCell];
            float2 cellCoord  = (float2)worldGrid.IndexToGridCoord(iCell);
            var    cellOffset = cellCoord * worldGrid.cellSize;

            Vector3 center = cellOffset.X0Z() + (worldGrid.cellSize * 0.5f);
            Handles.DrawWireCube(center, new(worldGrid.cellSize, worldGrid.cellSize, worldGrid.cellSize));
        }*/
    }
#endif
}
}