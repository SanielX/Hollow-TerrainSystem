using System.Collections.Generic;
using Hollow.VirtualTexturing;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hollow.TerrainSystem
{
public static partial class PhotoTerrainRenderer
{
    private static GraphicsBuffer workingNodeSet0, workingNodeSet1, workingCullIndirectArguments, workingCullState;
    private static Material debugPatchHeightVisualizerMat;

    /// <summary> Is always going to be (Power of 2) + 1 </summary>
    internal const int PatchResolution = 16 + 1;

#region Public API

    public static unsafe void Initialize()
    {
        // KEEP THIS FIRST THING THAT HAPPENS, ALWAYS!!!
#if !UNITY_EDITOR
            PhotoTerrainSettings.RuntimeInit();
#endif

        PT_AVT_ENABLED = GlobalKeyword.Create("PT_AVT_ENABLED");
        InitVirtualTexture();

        // Worst case scenario is terrain with 4096x4096 heightmap rendering all LOD0
        // (for editing purposes for example), in which case we'd need space for 65k nodes, which is ushort.max
        workingNodeSet0              = new(GraphicsBuffer.Target.Structured, ushort.MaxValue, sizeof(uint2)); // node position
        workingNodeSet1              = new(GraphicsBuffer.Target.Structured, ushort.MaxValue, sizeof(uint2)); // on lookup texture
        workingCullIndirectArguments = new(GraphicsBuffer.Target.Structured | GraphicsBuffer.Target.IndirectArguments, 16, sizeof(uint3));
        workingCullState             = new(GraphicsBuffer.Target.Structured, 16, sizeof(uint3));

        debugPatchHeightVisualizerMat = new(TerrainResources.Instance.DebugHeightShader) { hideFlags = HideFlags.DontSave };
        debugPatchHeightVisualizerMat.enableInstancing = true;
        debugPatchHeightVisualizerMat.renderQueue = (int)RenderQueue.Overlay;

#if UNITY_EDITOR
        HandleUtility.pickGameObjectCustomPasses += TerrainScenePick;
        HandleUtility.placeObjectCustomPasses    += TerrainScenePlace;
        pickingResultsBuffer                     =  CreatePickingResultsBuffer();
#endif
    }

    public static void Release()
    {
        ReleaseVirtualTexture();
        workingNodeSet0.Dispose();
        workingNodeSet1.Dispose();
        workingCullState.Dispose();
        workingCullIndirectArguments.Dispose();
        ObjectUtility.SafeDestroy(ref debugPatchHeightVisualizerMat);

#if UNITY_EDITOR
        pickingResultsBuffer.Dispose();
        HandleUtility.pickGameObjectCustomPasses -= TerrainScenePick;
        HandleUtility.placeObjectCustomPasses    -= TerrainScenePlace;
#endif
    }

    public static void RefreshWorldData(List<Camera> cameras)
    {
        // Refresh these only 1 per frame, since terrain should really move between camera rendering or something
        for (int i = 0; i < PhotoTerrainWorld.ActiveTerrains.Count; i++)
        {
            if (PhotoTerrainWorld.ActiveTerrains[i].IsValidInstance())
            {
                PhotoTerrainWorld.ActiveTerrains[i].Refresh();
            }
        }

        RefreshVirtualTextureWorld(cameras);
    }

    public struct RenderRequest
    {
        /// <summary> If left null, <see cref="UnityEngine.Graphics"/> class will be used to issue draw commands </summary>
        public CommandBuffer cmd;

        public string shaderPassName;

        /// <summary> Can't be null </summary>
        public Camera camera;

        public bool   drawHoles;
    }

    public static void RenderTerrainInstances(in RenderRequest renderRequest)
    {
        Assert.IsNotNull(renderRequest.camera, "renderRequest.camera != null");

        for (int i = 0; i < PhotoTerrainWorld.ActiveTerrains.Count; i++)
        {
            var terrain = PhotoTerrainWorld.ActiveTerrains[i];
            RenderTerrainInstance(renderRequest, terrain);
        }
    }

#endregion

    internal static void RenderTerrainInstance(in RenderRequest renderRequest, PhotoTerrain terrain)
    {
        var isTerrainVisible = !renderRequest.camera || IsTerrainVisible(terrain, renderRequest.camera);
        terrain.isPotentiallyVisibleNow = isTerrainVisible;

        if (!isTerrainVisible)
            return;

        if (renderRequest.cmd is not null)
        {
            Material mat        = terrain.Material;
            int      shaderPass = terrain.Material.FindPass(renderRequest.shaderPassName);
            if (shaderPass < 0)
            {
                // fallback to default material
                shaderPass = PhotoTerrainSettings.Instance.defaultTerrainMaterial.FindPass(renderRequest.shaderPassName);
                if (shaderPass >= 0)
                {
                    mat = PhotoTerrainSettings.Instance.defaultTerrainMaterial;
                }
            }

            renderRequest.cmd.SetGlobalFloat("_PT_NO_HOLES", renderRequest.drawHoles ? 0 : 1);

            renderRequest.cmd.DrawProceduralIndirect(TerrainResources.Instance.PatchMesh.IndexBuffer,
                                                     default,
                                                     mat,
                                                     shaderPass,
                                                     MeshTopology.Triangles,
                                                     terrain.finalIndirectArgumentsBuffer,
                                                     0,
                                                     terrain.propertyBlock);

            renderRequest.cmd.SetGlobalFloat("_PT_NO_HOLES", 0); // Set back to 0 because for some reason setting global float in cmd persists 
        }
        else
        {
            //  if (!renderRequest.drawHoles)
            //  {
            //      terrain.propertyBlock.SetTexture(PhotoTerrain.ShaderID.PtHolesmap, Texture2D.blackTexture);
            //  }

            Graphics.DrawProceduralIndirect(terrain.Material, terrain.ComputeBounds(), MeshTopology.Triangles,
                                            indexBuffer: TerrainResources.Instance.PatchMesh.IndexBuffer,
                                            bufferWithArgs: terrain.finalIndirectArgumentsBuffer, argsOffset: 0,
                                            camera: renderRequest.camera,
                                            properties: terrain.propertyBlock,
                                            castShadows: ShadowCastingMode.On,
                                            layer: terrain.gameObject.layer);
#if UNITY_ASSERTIONS
            if (terrain._drawPatchHeights)
            {
                Graphics.DrawMeshInstancedIndirect(ProceduralMesh.Cube, 0, debugPatchHeightVisualizerMat, terrain.ComputeBounds(),
                                                   terrain.finalIndirectArgumentsBuffer,
                                                   camera: renderRequest.camera,
                                                   castShadows: ShadowCastingMode.Off,
                                                   layer: terrain.gameObject.layer,
                                                   properties: terrain.propertyBlock);
            }
#endif
        }
    }
}
}