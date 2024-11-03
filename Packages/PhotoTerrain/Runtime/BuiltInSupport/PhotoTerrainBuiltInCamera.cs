using System.Collections.Generic;
using Hollow.VirtualTexturing;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hollow.TerrainSystem
{
[ExecuteAlways, RequireComponent(typeof(Camera))]
internal class PhotoTerrainBuiltInCamera : MonoBehaviour
{
    private static bool initialized;
    private static bool initializedRuntime;
#if UNITY_EDITOR
    [InitializeOnLoadMethod]
    static void Init_Editor()
    {
        if (GraphicsSettings.currentRenderPipeline != null)
            return;

        AssemblyReloadEvents.beforeAssemblyReload += PhotoTerrainRenderer.Release;

        EditorApplication.playModeStateChanged += change =>
        {
            if (change is PlayModeStateChange.EnteredPlayMode or PlayModeStateChange.EnteredEditMode)
            {
                PhotoTerrainRenderer.Release();
                initialized = false;
            }
        };

        Init_Runtime();
    }
#endif
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init_Runtime()
    {
        if (initializedRuntime || GraphicsSettings.currentRenderPipeline != null)
            return;

        initializedRuntime = true;

        Camera.onPreRender += GlobalOnPreRender;
        Camera.onPreCull   += GlobalPreCull;
    }

    static List<Camera> camerasCached = new(1) { null };

    static void GlobalPreCull(Camera cam)
    {
        if (!initialized) // This is not supposed to be lazily initialized but legacy RP is kinda shitty when it comes to even ordering
        {
            PhotoTerrainRenderer.Initialize();
            initialized = true;
        }

        {
            camerasCached[0] = cam;
            // Only thing this does is update size of virtual texture according to current
            // camera position, so pass game camera only
            PhotoTerrainRenderer.RefreshWorldData(camerasCached);
        }

        // Send Graphics.Draw to unity rendering, inefficient though :P
        // Need to send on pre cull because after that Graphics.Draw api does nothing
        PhotoTerrainRenderer.RenderRequest request = new();
        request.camera = cam;
        request.cmd    = null;

        PhotoTerrainRenderer.RenderTerrainInstances(request);
    }

    static void GlobalOnPreRender(Camera cam)
    {
        if (cam.cameraType == CameraType.Preview || !initialized)
            return;

        PhotoTerrainBuiltInCamera builtInCamera = cam.GetComponent<PhotoTerrainBuiltInCamera>();
        if (!builtInCamera)
        {
            builtInCamera = cam.gameObject.AddComponent<PhotoTerrainBuiltInCamera>();
        }

        builtInCamera.RecordBuffers();
    }

    new Camera camera;

    CommandBuffer cmd_beforeOpaque;
    CommandBuffer afterOpaque;

    void OnEnable()
    {
        camera = GetComponent<Camera>();
        cmd_beforeOpaque = new() { name = "PT_Before Opaque" };
        afterOpaque  = new() { name = "PT_After Opaque" };

        camera.AddCommandBuffer(CameraEvent.BeforeGBuffer, cmd_beforeOpaque);
        camera.AddCommandBuffer(CameraEvent.AfterGBuffer,  afterOpaque);
    }

    void OnDisable()
    {
        camera.RemoveCommandBuffer(CameraEvent.BeforeGBuffer, cmd_beforeOpaque);
        camera.RemoveCommandBuffer(CameraEvent.AfterGBuffer,  afterOpaque);
    }

    private static List<VTRenderRequest>    renderRequests = new();
    private static RenderTargetIdentifier[] vtBlitIds      = new RenderTargetIdentifier[3];
    private static List<TerrainDecal> decalsCache = new(512);

    void RecordBuffers()
    {
        Profiler.BeginSample("PhotoTerrain - Record Buffers");

        cmd_beforeOpaque.Clear();
        afterOpaque.Clear();

        PhotoTerrainVirtualTextureUser vtUser = GetComponent<PhotoTerrainVirtualTextureUser>();
        bool usesVt = vtUser && vtUser.isActiveAndEnabled;

        Shader.SetKeyword(PhotoTerrainRenderer.PT_AVT_ENABLED, usesVt);
        if (usesVt)
        {
            // Create feedback buffer or recreate if screen size changed
            vtUser.Refresh(camera.pixelWidth, camera.pixelHeight);
        }

        PhotoTerrainRenderer.CullRequest cullRequest;
        cullRequest.mode               = PhotoTerrainRenderer.CullMode.Normal;
        cullRequest.cmd                = cmd_beforeOpaque;
        cullRequest.verticalResolution = camera.pixelHeight;
        cullRequest.camera             = camera;

        PhotoTerrainRenderer.CullTerrainInstances(cullRequest);

        // -- VIRTUAL TEXTURE UPDATE & RENDER
        if (usesVt)
        {
            Profiler.BeginSample("Virtual Texture Update");
            // With render graph this job can be started while graph is recording and drawing commands issued later which is kinda cool
            var vtUpdateJobHandle = PhotoTerrainRenderer.ScheduleFeedbackAnalysis(vtUser);

            // TODO: Have it be part of PhotoTerrainRenderer
            // Now that I'm thinking about it, if one were to implement texture streaming with this
            // You'd need to make virtual texture more async, each page could be requested or loaded
            // Render requests would need to be a queue that you then mark as loaded and *only then* actually map it on GPU
            renderRequests.Clear();
            PhotoTerrainRenderer.UpdateVirtualTexture(cmd_beforeOpaque, vtUpdateJobHandle, renderRequests);

            Profiler.EndSample();
            Profiler.BeginSample("Page Rendering");
            cmd_beforeOpaque.BeginSample("Render VT Pages");
            var vt = PhotoTerrainRenderer.worldVirtualTexture;
            var worldGrid = PhotoTerrainRenderer.worldGrid;
            for (int i = 0; i < renderRequests.Count; i++)
            {
                var request = renderRequests[i];
                vtBlitIds[0] = request.scratchBuffers[0].renderTarget;
                vtBlitIds[1] = request.scratchBuffers[1].renderTarget;
                vtBlitIds[2] = request.scratchBuffers[2].renderTarget;
                cmd_beforeOpaque.SetRenderTarget(vtBlitIds, request.dummyDepthTarget);

                ref readonly var page = ref request.page;
                var imageId     = page.imageId;
                var image       = vt.GetVirtualImage(imageId);
                var cellInfo    = PhotoTerrainRenderer.virtualImages[imageId];
                Assert.IsTrue(cellInfo.gridIndex >= 0, "cellIndex >= 0");

                ref var cell = ref PhotoTerrainRenderer.worldGrid.grid[cellInfo.gridIndex];

                float2 cellWorldOffset = (float2)worldGrid.IndexToGridCoord(cellInfo.gridIndex) * (worldGrid.cellSize);

                int   m    = image.mipCount - 1 - page.mip;
                float size = (cellInfo.gridSize * worldGrid.cellSize) / Mathf.Pow(2, m); // Sector size grows as mip gets smaller

                int2   localCoords    = vt.ToLocalCoords(new(page.x, page.y), page.mip, page.imageId);
                float2 inSectorCoords = localCoords * (float2)size;
                float2 worldCoords    = cellWorldOffset + inSectorCoords;

                float sizeWithBorder = (size * vt.TileSizeWithBorder) / (float)vt.TileSize;
                float offset         = sizeWithBorder - size;

                worldCoords -= offset * 0.5f;
                float4 worldRect = new float4(worldCoords, worldCoords + sizeWithBorder);

                UBounds worldBounds = new UBounds(new float3(worldRect.x, -2000, worldRect.y),
                                                  new float3(worldRect.z,  2000, worldRect.w));
                cmd_beforeOpaque.SetGlobalVector("_CellWorldPositionRange", worldRect);
                cmd_beforeOpaque.SetGlobalFloat ("_CellId", cellInfo.gridIndex);
                cmd_beforeOpaque.SetGlobalFloat ("_FlipScreenSpace", 1); // NOTE: _ProjectionParams.x is -1 for both built in and my SRP and yet only built in needs flip???

                if (cellInfo.terrain)
                {
                    PhotoTerrainRenderer.VirtualTextureBlit(cmd_beforeOpaque, cellInfo.terrain);

                    decalsCache.Clear();
                    PhotoTerrainWorld.GetAllDecalsWithinBounds(worldBounds, decalsCache);
                    if (decalsCache.Count > 0)
                    {
                        RenderUtility.ComputeViewProjMatricesForAABB(worldBounds.min, worldBounds.max,
                                                                     out var view, out var proj);
                        decalsCache.Sort(static (d0, d1) => d0.transformMatrix.m13.CompareTo(d1.transformMatrix.m13));

                        cmd_beforeOpaque.SetViewProjectionMatrices(view, proj);
                        for (int j = 0; j < decalsCache.Count; j++)
                        {
                            var decal = decalsCache[j];

                            cmd_beforeOpaque.DrawMesh(ProceduralMesh.HorizontalQuad, decal.transformMatrix, decal.material, 0, 0, cellInfo.terrain.propertyBlock);
                        }
                    }
                }

                request.Compress(cmd_beforeOpaque);
            }

            cmd_beforeOpaque.EndSample("Render VT Pages");

            vt.CopyToCache(cmd_beforeOpaque, renderRequests);
            PhotoTerrainRenderer.BindVirtualTexture(cmd_beforeOpaque, vtUser);

            vtUser.RequestFeedbackUAVRead(afterOpaque);
            Profiler.EndSample(); // -- Page Rendering
        }

        Profiler.EndSample();
    }
}
}