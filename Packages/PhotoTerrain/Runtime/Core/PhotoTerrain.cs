using System;
using System.Collections;
using System.Runtime.InteropServices;
using Hollow.Extensions;
using Hollow.Rendering;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace Hollow.TerrainSystem
{
[StructLayout(LayoutKind.Sequential)]
internal struct TerrainGPUInstanceInfo
{
    public float3 InstancePosition;
    public uint   GridPatchResolution;
    public float4 InstanceSize;
    public float4 InstanceSizeRcp;
}

[StructLayout(LayoutKind.Sequential)]
internal struct TerrainGPUPatchInfo
{
    public float2 Scale;
    public float2 Offset;
    public float2 MinMaxHeight;
    public float  Deviation; // Stored in [0;1] range, meaning its relative to terrain max height!
    public uint   Lod;
    public uint2  TexelCoordinates;
}

[ExecuteAlways]
[RequireComponent(typeof(Terrain))]
[RequireComponent(typeof(TerrainCollider))]
public unsafe class PhotoTerrain : MonoBehaviour
{
    internal enum CompileMode
    {
        OnChange,
        Always,
        Never
    }

    internal TerrainCompiler compiler;

    // TODO: Should instead create 1 virtual image to cover whole world as a fallback
    internal int virtualTextureInstanceId;
    internal int virtualImageId = -1; // fallback virtual image

    private  bool            needToRecomputeLODErrors;
    private int              lodTreeHash, instanceDataHash;

    /// <summary>
    /// HACK: This is how culling knows if terrain is visible by currently rendering camera
    /// </summary>
    internal bool isPotentiallyVisibleNow;

    internal MaterialPropertyBlock propertyBlock;

    internal RenderTexture  lodLevelLookupTexture;
    internal Texture2D      lodNodeIndexLookupTexture;
    internal GraphicsBuffer nodeArrayBuffer;
    internal CBuffer<TerrainGPUInstanceInfo> terrainGPUDataCBuffer;

    internal GraphicsBuffer finalIndirectArgumentsBuffer;
    internal GraphicsBuffer visibleNodeIndicesBuffer; // Recomputed once per camera render
    internal GraphicsBuffer nodeSplitAndSeamArrayBuffer;

    [SerializeField] internal bool _renderDocBrush;

    [SerializeField] internal TerrainLayer           baseLayer;
    [SerializeField] internal AdditionalTerrainData  outputData;
    [SerializeField] internal CompileMode            updateMode;
    [SerializeField] internal TerrainMaterialPalette palette;
    [SerializeField]          Material               material;
    [Range(0, 10f)] 
    [SerializeField] internal float                  pixelError = 4f;

    [FormerlySerializedAs("m_DrawPatchHeights")] [Header("Debug")] [SerializeField]
    internal bool _drawPatchHeights;

    public RenderTexture    Heightmap         => outputData.unityData.heightmapTexture;
    public TerrainData      NativeTerrainData => outputData.unityData;

    public float MaxHeight => outputData.unityData.heightmapScale.y;

    public Bounds ComputeBounds()
    {
        if (!outputData || !outputData.unityData)
            return default;

        var offset = transform.position;
        var size   = outputData.unityData.size;

        return new Bounds() { min = offset, max = offset + size };
    }

    /// <summary>
    /// Material used by terrain. Every terrain has unique material instance
    /// </summary>
    public Material Material
    {
        get
        {
            if (!material)
                return PhotoTerrainSettings.Instance.defaultTerrainMaterial;

            return material;
        }
        set => material = value;
    }

    public Vector3 Size => outputData.unityData.size;

    void OnEnable()
    {
        if (!outputData)
            return;

        outputData.unityData.SyncHeightmap();

        PhotoTerrainWorld.RegisterActiveTerrain(this);
        compiler = new(this);
    }

    void OnDisable()
    {
        PhotoTerrainWorld.UnregisterActiveTerrain(this);
        ObjectUtility.SafeDispose(ref nodeArrayBuffer);
        ObjectUtility.SafeDispose(ref terrainGPUDataCBuffer);
        ObjectUtility.SafeDispose(ref finalIndirectArgumentsBuffer);
        ObjectUtility.SafeDispose(ref visibleNodeIndicesBuffer);
        ObjectUtility.SafeDispose(ref nodeSplitAndSeamArrayBuffer);

        ObjectUtility.SafeDestroy(ref lodLevelLookupTexture);
        ObjectUtility.SafeDestroy(ref lodNodeIndexLookupTexture);
    }

    void OnValidate()
    {
        if (!IsValidInstance())
            return;

        var terrain = GetComponent<Terrain>();
        terrain.terrainData = NativeTerrainData;

        var terrainCollider         = GetComponent<TerrainCollider>();
        terrainCollider.terrainData = NativeTerrainData;
    }

    IEnumerator Start()
    {
        lodTreeHash = 0;
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Refresh(true);
            yield break;
        }
#endif
        yield return null;
        Refresh(true);
    }

#if UNITY_EDITOR
    void Update()
    {
        if (Application.isPlaying || !IsValidInstance())
            return;

        if (updateMode == CompileMode.Never) return;

        bool changed = compiler.RefreshLayerStack();
        if (updateMode == CompileMode.Always || changed)
        {
            //                Debug.Log("Detected change in terrain layer stack");
            compiler.RecordCompile(null);
            needToRecomputeLODErrors = true;
        }

        if (needToRecomputeLODErrors /*&&
           (PhotoTerrainWorld.ActiveEditProcess is null)*/)
        {
            Refresh(true);
            needToRecomputeLODErrors = false;
        }
    }
#endif

    internal bool IsValidInstance()
    {
        return this && outputData;
    }

    internal class ShaderID
    {
        public static readonly GlobalKeyword MaskDebugEnabled = GlobalKeyword.Create("PT_WORLD_DEBUG_MASK");

        public static readonly int PhotoTerrainInstanceData   = Shader.PropertyToID("PhotoTerrainInstanceData");
        public static readonly int PhotoTerrainLODList        = Shader.PropertyToID("PhotoTerrainLODList");
        public static readonly int PhotoTerrainVisibleLODList = Shader.PropertyToID("PhotoTerrainVisibleLODList");
        public static readonly int PtHeightmap                = Shader.PropertyToID("PT_Heightmap");
        public static readonly int PtHolesmap                 = Shader.PropertyToID("PT_Holesmap");
        public static readonly int LODLevelMap                = Shader.PropertyToID("LODLevelMap");
        public static readonly int TerrainInstanceID          = Shader.PropertyToID("_TerrainInstanceID");
        public static readonly int PhotoTerrainSeamList       = Shader.PropertyToID("PhotoTerrainSeamList");

        public static readonly int OmpvMaterials              = Shader.PropertyToID("OMPV_Materials");
        public static readonly int OmpvLayers                 = Shader.PropertyToID("OMPV_Layers");
        public static readonly int OmpvAlbedoArray            = Shader.PropertyToID("_OMPV_AlbedoArray");
        public static readonly int OmpvNormalArray            = Shader.PropertyToID("_OMPV_NormalArray");
        public static readonly int OmpvMaskArray              = Shader.PropertyToID("_OMPV_MaskArray");
        public static readonly int OmpvSplatMap               = Shader.PropertyToID("_OMPV_SplatMap");


        public static readonly int OmpvSampleNoise       = Shader.PropertyToID("_OMPV_SplatNoise");
        public static readonly int OmpvSampleNoiseParams = Shader.PropertyToID("_OMPV_SplatNoiseParams");
    }

    internal void Refresh(bool forceRecalculateErrors = false)
    {
        if (finalIndirectArgumentsBuffer.IsNullOrInvalid())
        {
            finalIndirectArgumentsBuffer = new(GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Structured, 1,
                                               GraphicsBuffer.IndirectDrawIndexedArgs.size);
        }

        if (visibleNodeIndicesBuffer.IsNullOrInvalid())
        {
            visibleNodeIndicesBuffer  = new(GraphicsBuffer.Target.Structured, ushort.MaxValue, sizeof(uint));
        }

        bool rebuildLODData = false;
        int newLodHash = ComputeLODTreeHash();
        if (newLodHash != lodTreeHash)
        {
            RefreshLODTreeData();
            lodTreeHash = newLodHash;
            rebuildLODData = true;
        }

        int newInstanceHash = ComputeInstanceDataHash();
        if (newInstanceHash != instanceDataHash)
        {
            RefreshInstanceData();
            instanceDataHash          = newInstanceHash;
        }

        propertyBlock ??= new();

        {
            propertyBlock.SetConstantBuffer(ShaderID.PhotoTerrainInstanceData,   terrainGPUDataCBuffer.GraphicsBuffer, 0,
                                            terrainGPUDataCBuffer.GraphicsBuffer.stride);
            propertyBlock.SetBuffer        (ShaderID.PhotoTerrainLODList,        nodeArrayBuffer);
            propertyBlock.SetBuffer        (ShaderID.PhotoTerrainVisibleLODList, visibleNodeIndicesBuffer);
            propertyBlock.SetTexture       (ShaderID.PtHeightmap,               outputData.unityData.heightmapTexture);
            propertyBlock.SetTexture       (ShaderID.PtHolesmap,                outputData.unityData.holesTexture);
            propertyBlock.SetTexture       (ShaderID.LODLevelMap,               lodLevelLookupTexture);
            propertyBlock.SetFloat         (ShaderID.TerrainInstanceID,         GetInstanceID());
            propertyBlock.SetBuffer        (ShaderID.PhotoTerrainSeamList,      nodeSplitAndSeamArrayBuffer);
            SphericalHarmonicsUtility.SetSHCoefficients(transform.position, propertyBlock);

            if (palette)
            {
                palette.Refresh();
                propertyBlock.SetBuffer(ShaderID.OmpvMaterials, palette.MaterialsBuffer);
                // propertyBlock.SetBuffer(ShaderID.OmpvLayers,    m_Palette.LayerBuffer);

                propertyBlock.SetTexture(ShaderID.OmpvAlbedoArray, palette.AlbedoArray);
                propertyBlock.SetTexture(ShaderID.OmpvNormalArray, palette.NormalArray);
                propertyBlock.SetTexture(ShaderID.OmpvMaskArray,   palette.MaskArray);

                propertyBlock.SetTexture(ShaderID.OmpvSplatMap,    outputData.Splat);

                propertyBlock.SetTexture(ShaderID.OmpvSampleNoise, palette.ompvNoise ? palette.ompvNoise : Texture2D.blackTexture);

                propertyBlock.SetVector(ShaderID.OmpvSampleNoiseParams, new(palette.ompvNoiseStrength, palette.ompvNoiseSize));
            }

            if (PhotoTerrainWorld.GetDebugMask(out var worldToMask, out var maskTexture))
            {
                propertyBlock.SetTexture("PT_DebugWorldMask",         maskTexture);
                propertyBlock.SetMatrix ("PT_DebugWorldToMaskMatrix", worldToMask);

                Shader.SetKeyword(ShaderID.MaskDebugEnabled, true);
            }
            else
            {
                Shader.SetKeyword(ShaderID.MaskDebugEnabled, false);
            }
        }

        if (forceRecalculateErrors || rebuildLODData)
        {
            //Debug.Log($"Terrain '{name}' rebuilt LOD Data", this);
            CalculateLODErrors();
        }
    }

    int ComputeLODTreeHash()
    {
        int hash = HashCode.Combine(outputData.unityData.size.GetHashCode(),
                                    outputData.unityData.heightmapResolution,
                                    lodNodeIndexLookupTexture != null,
                                    nodeSplitAndSeamArrayBuffer.IsNullOrInvalid(),
                                    nodeArrayBuffer.IsNullOrInvalid());

        return hash;
    }

    internal int ComputeInstanceDataHash()
    {
        int hash = HashCode.Combine(terrainGPUDataCBuffer.IsNullOrInvalid(),
                                    transform.position,
                                    outputData.unityData.size);
        return hash;
    }

    internal int ComputeLOD0GridResolution()
    {
        int heightmapResolution = outputData.unityData.heightmapResolution - 1; // heightmap res is pow2 + 1
        int lod0Resolution      = heightmapResolution / (PhotoTerrainRenderer.PatchResolution - 1);
        return lod0Resolution;
    }

    void RefreshInstanceData()
    {
        if (terrainGPUDataCBuffer.IsNullOrInvalid())
            terrainGPUDataCBuffer = new();

        ref TerrainGPUInstanceInfo gpuInstanceInfo   = ref terrainGPUDataCBuffer.GetDataForWriting();
        Bounds                     terrainBounds     = ComputeBounds();
        Vector3                    terrainBoundsSize = terrainBounds.size;

        gpuInstanceInfo.InstancePosition    = terrainBounds.min;
        gpuInstanceInfo.InstanceSize        = new float4(terrainBoundsSize, 1f);
        gpuInstanceInfo.InstanceSizeRcp     = 1f / gpuInstanceInfo.InstanceSize;
        gpuInstanceInfo.GridPatchResolution = PhotoTerrainRenderer.PatchResolution;
        terrainGPUDataCBuffer.Update();
    }

    internal int ComputeVisibleLODLevelCount()
    {
        return Mathf.Min(lodNodeIndexLookupTexture.mipmapCount - 1, 5) + 1;
    }

#if UNITY_EDITOR
    [ContextMenu("Refresh Patches")]
    void RefreshLODTreeDataCapture()
    {
        //UnityEditor.GameView
        UnityEditorInternal.RenderDoc.BeginCaptureRenderDoc(EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor.CoreModule")));
        CalculateLODErrors();
        UnityEditorInternal.RenderDoc.EndCaptureRenderDoc(EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor.CoreModule")));
    }
#endif

    void RefreshLODTreeData()
    {
        int lod0Resolution = ComputeLOD0GridResolution();

        {
            ObjectUtility.SafeDestroy(ref lodNodeIndexLookupTexture);
            // It should be fucking R16_Uint but unity moment, can't upload shit from CPU to render texture
            lodNodeIndexLookupTexture = new(lod0Resolution, lod0Resolution, GraphicsFormat.R16_UNorm, TextureCreationFlags.MipChain             |
                                                                                                      TextureCreationFlags.DontInitializePixels |
                                                                                                      TextureCreationFlags.IgnoreMipmapLimit);
        }

        {
            ObjectUtility.SafeDestroy(ref lodLevelLookupTexture);
            lodLevelLookupTexture = new(lod0Resolution, lod0Resolution, GraphicsFormat.R8_UInt, GraphicsFormat.None);
            lodLevelLookupTexture.useMipMap = false;
            lodLevelLookupTexture.enableRandomWrite = true;
            lodLevelLookupTexture.Create();
        }

        int lodLevelsCount = math.floorlog2(lod0Resolution) + 1;
        UnsafeFixedQuadTree<TerrainGPUPatchInfo> lodTree = new(Allocator.Temp, lodLevelsCount);

        {
            ObjectUtility.SafeDispose(ref nodeArrayBuffer);
            nodeArrayBuffer = new(GraphicsBuffer.Target.Structured, lodTree.NodesCount, sizeof(TerrainGPUPatchInfo));
        }

        {
            ObjectUtility.SafeDispose(ref nodeSplitAndSeamArrayBuffer);
            nodeSplitAndSeamArrayBuffer = new(GraphicsBuffer.Target.Structured, lodTree.NodesCount, sizeof(uint));
        }

        float2 terrainSize = ((float3)outputData.unityData.size).xz;
        for (int nodeIndex = 0; nodeIndex < lodTree.NodesCount; nodeIndex++)
        {
            // atlas allocator also uses quad tree structure
            // this method returns texture coordinate (in mip0) based on morton encoding of index to quad tree
            // it assumes quad tree is linear (index 0 is biggest single node with 4 children etc)
            AtlasAllocatorPD2.TextureCoords(nodeIndex, lod0Resolution, out var coords, out var lodTreeLevel);

            Assert.IsTrue(nodeIndex <= ushort.MaxValue);
            int  mipLevel    = lodLevelsCount - lodTreeLevel - 1;
            int2 localCoords = coords >> mipLevel;

            NativeArray<ushort> mipData    = lodNodeIndexLookupTexture.GetPixelData<ushort>(mipLevel);
            int                 mipSize    = (lodNodeIndexLookupTexture.width >> mipLevel);
            int                 pixelIndex = localCoords.x + localCoords.y * mipSize;

            Assert.IsFalse(localCoords.x >= mipSize || localCoords.y >= mipSize);
            mipData[pixelIndex] = (ushort)nodeIndex;

            float2 size   = terrainSize / mipSize;
            float2 offset = (coords / (float2)lod0Resolution) * terrainSize;

            lodTree[nodeIndex] = new()
            {
                Offset           = offset,
                Scale            = size,
                MinMaxHeight     = new float2(0, outputData.unityData.size.y),
                Deviation        = 0.01f * mipLevel,
                Lod              = (uint)mipLevel,
                TexelCoordinates = (uint2)coords,
            };
        }

        lodNodeIndexLookupTexture.Apply(false);
        nodeArrayBuffer.SetData(lodTree.AsNativeArray());
    }

    internal void CalculateLODErrors(CommandBuffer cmd = null)
    {
        bool executeCommandBuffer = cmd is null;

        cmd ??= new();
        // UnityEditorInternal.RenderDoc.BeginCaptureRenderDoc(UnityEditor.SceneView.lastActiveSceneView);

        int terrainPatchMapRTId = Shader.PropertyToID("_TerrainPatchAndTriangleID");
        RenderTextureDescriptor desc = new(Heightmap.width, Heightmap.height, GraphicsFormat.R16G16_UInt, GraphicsFormat.D16_UNorm);
        cmd.GetTemporaryRT(terrainPatchMapRTId, desc);

        TerrainRenderingUtility.SetupTerrainTopDownViewProjection(cmd, this);

        {
            int           texSize        = Heightmap.width;
            ComputeShader lodErrorShader = TerrainResources.Instance.TerrainLODErrorCalculationShader;

            // Need to set deviation to 0 and minmax height to (0; FLT_MAX)
            {
                cmd.BeginSample("Error Calculation - Clear");
                int clearPatchesKernel = lodErrorShader.FindKernel("CSClearErrorAndMinMaxHeight");
                cmd.SetComputeBufferParam(lodErrorShader, clearPatchesKernel, "RWTerrainPatches", nodeArrayBuffer);
                cmd.DispatchCompute      (lodErrorShader, clearPatchesKernel, (nodeArrayBuffer.count + 63) / 64, 1, 1);
                cmd.EndSample("Error Calculation - Clear");
            }

            // LOD0 has deviation of 0, so use special kernel which only calculates minmax height
            {
                cmd.BeginSample("LOD 0 MinMax Calculation");
                int minmaxKernel = lodErrorShader.FindKernel("CSCalculateMinMaxHeight");

                cmd.SetComputeTextureParam(lodErrorShader, minmaxKernel, "Heightmap",      Heightmap);
                cmd.SetComputeTextureParam(lodErrorShader, minmaxKernel, "TerrainLodPatchMap", lodNodeIndexLookupTexture);
                cmd.SetComputeBufferParam (lodErrorShader, minmaxKernel, "RWTerrainPatches", nodeArrayBuffer);
                cmd.DispatchCompute(lodErrorShader, minmaxKernel, (texSize + 7) / 8, (texSize + 7) / 8, 1);
                cmd.EndSample("LOD 0 MinMax Calculation");
            }

            int visibleLODs = ComputeVisibleLODLevelCount();
            for (int lodLevel = 1; lodLevel < visibleLODs; lodLevel++)
            {
                string sampleName = "LOD " + lodLevel;
                cmd.BeginSample(sampleName);

                PhotoTerrainRenderer.CullTerrainInstanceLOD(cmd, this, lodLevel, finalIndirectArgumentsBuffer);

                cmd.SetRenderTarget  (terrainPatchMapRTId);
                cmd.ClearRenderTarget(RTClearFlags.All, default, 1, 0);
                TerrainRenderingUtility.SetupTerrainTopDownViewProjection(cmd, this);

                PhotoTerrainRenderer.RenderTerrainInstance(new()
                {
                    cmd            = cmd,
                    shaderPassName = "VBufferPass",
                }, this);

                int errorKernel = lodErrorShader.FindKernel("CSCalculateErrorAndMinMaxHeight");
                cmd.SetComputeBufferParam (lodErrorShader, errorKernel, "RWTerrainPatches",   nodeArrayBuffer);
                cmd.SetComputeBufferParam (lodErrorShader, errorKernel, "TerrainIndexBuffer", TerrainResources.Instance.PatchMesh.FatIndexBuffer);
                cmd.SetComputeTextureParam(lodErrorShader, errorKernel, "TerrainLodPatchMap", lodNodeIndexLookupTexture);

                cmd.SetComputeTextureParam(lodErrorShader, errorKernel, "Heightmap",      Heightmap);
                cmd.SetComputeTextureParam(lodErrorShader, errorKernel, "TerrainVBuffer", terrainPatchMapRTId);
                cmd.SetComputeIntParam    (lodErrorShader, "LODLevel", lodLevel);

                cmd.SetComputeConstantBufferParam(lodErrorShader, "PhotoTerrainInstanceData",
                                                  terrainGPUDataCBuffer.GraphicsBuffer, 0, terrainGPUDataCBuffer.GraphicsBuffer.stride);

                cmd.DispatchCompute(lodErrorShader, errorKernel, (texSize + 7) / 8, (texSize + 7) / 8, 1);
                cmd.EndSample(sampleName);
            }

            // Convert integers to back to floats (Interlocked doesn't work on floats using cs5.0)
            {
                int convertKernel = lodErrorShader.FindKernel("CSConvertIntDeviation");
                cmd.SetComputeBufferParam(lodErrorShader, convertKernel, "RWTerrainPatches", nodeArrayBuffer);
                cmd.DispatchCompute      (lodErrorShader, convertKernel, (nodeArrayBuffer.count + 63) / 64, 1, 1);
            }
        }

        if (executeCommandBuffer)
        {
            Graphics.ExecuteCommandBuffer(cmd);
        }
        //UnityEditorInternal.RenderDoc.EndCaptureRenderDoc(UnityEditor.SceneView.lastActiveSceneView);
    }

    void OnDrawGizmosSelected()
    {
        var bounds = ComputeBounds();

        var minY = bounds.min.y;
        bounds.extents = bounds.extents.WithY(0.1f);
        bounds.center = bounds.center.WithY(minY);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }
}
}