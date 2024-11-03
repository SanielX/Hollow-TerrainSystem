using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Hollow.TerrainSystem
{
public static partial class PhotoTerrainRenderer
{
    [StructLayout(LayoutKind.Sequential)]
    public struct TerrainPickingResult
    {
        public float3 Position;
        public float3 Normal;
        public int    TerrainInstanceID;

#if UNITY_EDITOR
        internal PhotoTerrain TerrainInstance => TerrainInstanceID == 0 ? null : EditorUtility.InstanceIDToObject(TerrainInstanceID) as PhotoTerrain;
#endif
    };

#if UNITY_EDITOR

    private static          GraphicsBuffer         pickingResultsBuffer;
    private static          TerrainPickingResult[] pickingResultArray = new TerrainPickingResult[1];
    private static readonly int                    pickingRtId        = Shader.PropertyToID("TerrainScenePickingTexture");
    private static readonly int                    pickingDepthRtId   = Shader.PropertyToID("_TerrainPickingDepthTexture");

    internal static unsafe GraphicsBuffer CreatePickingResultsBuffer() => new(GraphicsBuffer.Target.Structured, 1, sizeof(TerrainPickingResult));

    internal static TerrainPickingResult ReadPickingResult(GraphicsBuffer resultBuffer)
    {
        resultBuffer.GetData(pickingResultArray);
        return pickingResultArray[0];
    }

    // position - The top-left of the window is (0,0), and the bottom-right is (Screen.width, Screen.height).
    // ignore - An array of GameObjects that will not be considered when selecting the nearest GameObject.
    // filter - An array of GameObjects that will be tested for picking intersection.
    //          If this argument is not null, only GameObjects in the filter array will be selected.
    internal static GameObject TerrainScenePick(Camera  cam, int layerMask, Vector2 position, GameObject[] ignore, GameObject[] filter,
                                                out int materialindex)
    {
        materialindex = -1;
        CommandBuffer cmd = new() { name = "Terrain Scene Pick" };

        int maybeVisibleTerrains = RenderTerrainsPicking(cmd, cam, pickingResultsBuffer, position, ignore, filter, pickingRtId, layerMask);

        if (maybeVisibleTerrains == 0)
        {
            return null;
        }

        Graphics.ExecuteCommandBuffer(cmd);
        pickingResultsBuffer.GetData(pickingResultArray);

        var obj = UnityEditor.EditorUtility.InstanceIDToObject((int)pickingResultArray[0].TerrainInstanceID);
        if (obj)
            return ((Component)obj).gameObject;

        return null;
    }

    internal static bool TerrainScenePlace(Vector2 guiposition, out Vector3 position, out Vector3 normal)
    {
        guiposition.y = Camera.current.pixelHeight - guiposition.y;

        CommandBuffer cmd = new() { name = "Terrain Scene Pick" };
        int maybeVisibleTerrains = RenderTerrainsPicking(cmd,                  Camera.current,
                                                         pickingResultsBuffer, guiposition,
                                                         null, null, pickingRtId, Camera.current.cullingMask);

        position = default;
        normal   = default;
        if (maybeVisibleTerrains == 0)
            return false;

        Graphics.ExecuteCommandBuffer(cmd);
        pickingResultsBuffer.GetData(pickingResultArray);

        var obj = UnityEditor.EditorUtility.InstanceIDToObject((int)pickingResultArray[0].TerrainInstanceID);
        if (obj)
        {
            position = pickingResultArray[0].Position;
            normal   = pickingResultArray[0].Normal;
            return true;
        }

        return false;
    }


    internal static int RenderTerrainsPicking(CommandBuffer  cmd,
                                              Camera         camera,
                                              GraphicsBuffer results, // Structured
                                              Vector2        mousePosition,
                                              GameObject[]   ignore    = null,
                                              GameObject[]   filter    = null,
                                              int            rtId      = 0,
                                              int            layerMask = ~0,
                                              bool           ignoreSceneVisibilityManager = false,
                                              bool           drawHoles = true)
    {
        if (rtId == 0)
            rtId = pickingRtId;

        RenderTextureDescriptor pickingTextureDesc = new(camera.pixelWidth, camera.pixelHeight,
                                                         GraphicsFormat.R32G32_SFloat, GraphicsFormat.None);
        RenderTextureDescriptor pickingTextureDepthDesc = new(camera.pixelWidth, camera.pixelHeight,
                                                              GraphicsFormat.None, GraphicsFormat.D32_SFloat);
        cmd.GetTemporaryRT   (rtId,             pickingTextureDesc);
        cmd.GetTemporaryRT   (pickingDepthRtId, pickingTextureDepthDesc);
        cmd.SetViewProjectionMatrices(camera.worldToCameraMatrix,
                                      camera.projectionMatrix);
        cmd.SetRenderTarget  (color: rtId, depth: pickingDepthRtId);
        cmd.ClearRenderTarget(RTClearFlags.All, Color.clear, 1, 0);

        int potentialTerrains = 0;
        for (int i = 0; i < PhotoTerrainWorld.ActiveTerrains.Count; i++)
        {
            var terrain = PhotoTerrainWorld.ActiveTerrains[i];
            if (!terrain.IsValidInstance() ||
                (layerMask & (1 << terrain.gameObject.layer)) == 0)
                continue;

            if (ignore is not null && System.Array.IndexOf(ignore, terrain.gameObject) >= 0)
                continue;
            if (filter is not null && System.Array.IndexOf(filter, terrain.gameObject) < 0)
                continue;
            if (!ignoreSceneVisibilityManager && SceneVisibilityManager.instance.IsPickingDisabled(terrain.gameObject))
                continue;

            terrain.Refresh();

            Vector2 rayPosition = mousePosition;
            rayPosition.y = camera.pixelHeight - rayPosition.y;

            CullTerrainInstance(cmd, new()
            {
                terrain             = terrain,
                camera              = camera,
                indirectArguments   = terrain.finalIndirectArgumentsBuffer,
                finalNodeList       = terrain.visibleNodeIndicesBuffer,
                populateLODLevelMap = true,
                verticalResolution  = camera.pixelHeight,
                ray                 = HandleUtility.GUIPointToWorldRay(rayPosition),
            });

            RenderTerrainInstance(new()
            {
                cmd            = cmd,
                camera         = camera,
                shaderPassName = "ScenePicking",
                drawHoles      = drawHoles,
            }, terrain);
            potentialTerrains++;
        }

        var invProj = math.inverse(GL.GetGPUProjectionMatrix(camera.projectionMatrix, true));
        var invView = math.fastinverse(camera.worldToCameraMatrix);

        Matrix4x4 inverseVP = math.mul(invView, invProj);

        var pickingShader = TerrainResources.Instance.PickingShader;
        cmd.SetComputeTextureParam(pickingShader, 0, "SelectionTexture",   rtId);
        cmd.SetComputeTextureParam(pickingShader, 0, "CameraDepthTexture", pickingDepthRtId);
        cmd.SetComputeBufferParam (pickingShader, 0, "PickingResults",     results);
        cmd.SetComputeVectorParam (pickingShader,    "PickingCoord",       mousePosition);
        cmd.SetComputeMatrixParam (pickingShader,    "unity_InverseMatrixVP", inverseVP);
        cmd.DispatchCompute       (pickingShader, 0, 1, 1, 1);

        cmd.ReleaseTemporaryRT(rtId);
        cmd.ReleaseTemporaryRT(pickingDepthRtId);
        return potentialTerrains;
    }

#endif
}
}