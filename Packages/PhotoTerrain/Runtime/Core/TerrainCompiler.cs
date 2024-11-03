using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace Hollow.TerrainSystem
{
public ref struct TerrainCompilationContext
{
    private static Dictionary<Shader, Material> s_MaterialMap = new(36);

    public CommandBuffer          cmd;

    public PhotoTerrain           targetTerrain;
    public string                 targetTextureType;
    public RenderTargetIdentifier targetTexture;

    public UBounds                terrainBounds;

    /// <summary> View & Projection matrices that cover target terrain </summary>
    public Matrix4x4 view, proj;

    public readonly bool IsTarget(string type) => targetTextureType.Equals(type, StringComparison.OrdinalIgnoreCase);

    public readonly Material GetMaterial(string shaderName)
    {
        var shader = Shader.Find(shaderName);
        if (!shader)
            throw new System.Exception($"Shader '{shaderName}' was not found");

        return GetMaterial(shader);
    }

    public readonly Material GetMaterial(Shader shader)
    {
        Assert.IsNotNull(shader);

        if (s_MaterialMap.TryGetValue(shader, out var mat) && mat)
            return mat;

        mat                   = new Material(shader) { hideFlags = HideFlags.DontSave };
        s_MaterialMap[shader] = mat;
        return mat;
    }

    public readonly MaterialPropertyBlock GetPropertyBlock() => new();
}
// TODO: Doesn't really need to be its own class either
internal class TerrainCompiler
{
    public TerrainCompiler(PhotoTerrain terrain)
    {
        TargetTerrain = terrain;
        CommandBuffer = new() { name = $"{TargetTerrain.name}_CompilationBuffer" };

        convertToUnityHeightmapMat           =  new(TerrainResources.Instance.PhotoToUnityHeightmapBlitter); // Shader.Find("Hidden/PhotoTerrainEditor/ConvertToUnityHeightmap"));
        convertToUnityHeightmapMat.hideFlags |= HideFlags.DontSave;
    }

    private static readonly int _TempHeightTex = Shader.PropertyToID("_TempHeightTex");
    private static readonly int _MainTex       = Shader.PropertyToID("_MainTex");

    private Material convertToUnityHeightmapMat;
    private int oldStackHash, oldTerrainInstanceHash;

    public CommandBuffer           CommandBuffer { get; }
    public PhotoTerrain            TargetTerrain { get; }

    /// <summary>
    /// Returns true if stack contains unrecorded changes
    /// </summary>
    public bool RefreshLayerStack()
    {
        var instanceHash = TargetTerrain.ComputeInstanceDataHash();
        if (oldTerrainInstanceHash != instanceHash)
        {
            oldTerrainInstanceHash = instanceHash;
            return true;
        }

        int stackHash = 17;
        int terrainObjectLayer = 1 << TargetTerrain.gameObject.layer;
        stackHash = HashHelper.Combine(stackHash, TargetTerrain.baseLayer.Version);

        bool result = stackHash != oldStackHash;
        oldStackHash = stackHash;
        return result;
    }

    public void RecordCompile(CommandBuffer cmd)
    {
        bool nullCmd = cmd is null;
        if (cmd is null)
            cmd = CommandBuffer;

        // Unity height map is in [0;.5] range because its in SNORM_16 format for some reason,
        // This is not converted back so having values > .5 results in integer overflow and bad behaviour
        // since we want to use native terrain collider, do everything in [0;1] range then convert to [0; 0.5] 
        cmd.GetTemporaryRT(_TempHeightTex, TargetTerrain.Heightmap.descriptor);

        TerrainCompilationContext ctx = default;
        ctx.targetTerrain = TargetTerrain;
        ctx.terrainBounds = TargetTerrain.ComputeBounds();
        ctx.cmd           = cmd;
        TerrainRenderingUtility.ComputeTerrainTopDownViewProjectionMatrices(TargetTerrain, out ctx.view, out ctx.proj);
        BlitStack(ref ctx, "Height", _TempHeightTex,                  Color.clear);
        BlitStack(ref ctx, "Splat",  TargetTerrain.outputData.SplatRT, Color.clear);

        {
            var hm = TargetTerrain.baseLayer.heightMap.GetLastTexture();
            cmd.Blit(hm, TargetTerrain.Heightmap, convertToUnityHeightmapMat);

            var sm = TargetTerrain.baseLayer.splatMap.GetLastTexture();
            cmd.Blit(sm, TargetTerrain.outputData.SplatRT);

            var holes = TargetTerrain.baseLayer.holesMap.GetLastTexture();
            Assert.IsTrue(TargetTerrain.outputData.unityData.holesTexture is RenderTexture);
            cmd.Blit(holes, TargetTerrain.outputData.unityData.holesTexture);

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(TargetTerrain.outputData);
            UnityEditor.EditorUtility.SetDirty(TargetTerrain.outputData.unityData);
#endif
        }

        if (nullCmd)
        {
            Graphics.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }
    }

    void BlitStack(ref TerrainCompilationContext ctx, string textureType, RenderTargetIdentifier texture, Color defaultColor)
    {
        ctx.targetTextureType = textureType;
        ctx.targetTexture     = texture;

        // ref readonly TerrainWorkTextureDescriptor desc = ref textureType.GetDescriptor();
        ctx.cmd.SetRenderTarget(ctx.targetTexture);
        ctx.cmd.ClearRenderTarget(RTClearFlags.All, defaultColor, 1, 0);
    }
}
}