using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Hollow.Rendering;
using Hollow.TerrainSystem;

namespace Hollow.VirtualTexturing
{
[StructLayout(LayoutKind.Sequential)]
public struct VTCommon_CBuffer
{
    public float2 IndirectionTextureSize;
    public uint   IndirectionTextureSizeInt;
    public float  PhysicalTextureRcpSize;

    // Gpu will treat these as single integer, because CPU&GPU are little endian we've got weird ordering
    public float CacheTileSize;
    public float CacheBorderSize;
    public float CacheTileCountWide;

    public float CacheScale;
}

public struct ScratchBuffer
{
    public CacheTextureCompression compressionType;
    public RenderTexture           renderTarget;
    public RenderTexture           compressTarget;

    public void Compress(CommandBuffer cmd)
    {
        switch (compressionType)
        {
        case CacheTextureCompression.BC1_RGB:
            GPUCompressor.CompressBC1(cmd, renderTarget, compressTarget);
            break;

        case CacheTextureCompression.BC5_RG:
            throw new System.NotImplementedException();

        default:
            throw new ArgumentOutOfRangeException();
        }
    }
}

public struct CacheTexture
{
    static int getBlockWidthBC1 (int w)
    {
        return (w + 3)  / 4;
    }

    static int getBlockHeightBC1(int h)
    {
        return (h + 3) / 4;
    }

    public unsafe CacheTexture(int size, int scratchSize, int scratchCount, CacheTextureDescriptor desc)
    {
        Texture           =  new(size, size, (GraphicsFormat)desc.Compression, TextureCreationFlags.None);
        Texture.hideFlags |= HideFlags.DontSave;
        Texture.name      =  desc.Name;

        var d = Texture.GetRawTextureData<byte>();
        UnsafeUtility.MemSet(d.GetUnsafePtr(), 0, d.Length);
        Texture.Apply();

        TextureID = Shader.PropertyToID(desc.Name);

        GraphicsFormat scratchRenderFormat = desc.Compression == CacheTextureCompression.BC1_RGB
            ? GraphicsFormat.R8G8B8A8_UNorm
            : GraphicsFormat.R8G8_UNorm;

        ScratchBuffers = new ScratchBuffer[scratchCount];
        for (int i = 0; i < scratchCount; i++)
        {
            var renderTarget = new RenderTexture(scratchSize, scratchSize, scratchRenderFormat, GraphicsFormat.None);
            renderTarget.hideFlags        |= HideFlags.DontSave;
            renderTarget.enableRandomWrite =  true;
            renderTarget.Create();

            // TODO: Other types of compression if required
            var compressTarget = new RenderTexture(getBlockWidthBC1(scratchSize), getBlockHeightBC1(scratchSize),
                                                   GraphicsFormat.R32G32_UInt, GraphicsFormat.None);
            compressTarget.hideFlags         |= HideFlags.DontSave;
            compressTarget.enableRandomWrite =  true;
            compressTarget.Create();

            ScratchBuffers[i] = new()
            {
                compressionType = desc.Compression,
                renderTarget    = renderTarget,
                compressTarget  = compressTarget,
            };
        }

        RenderTextureDescriptor depthDesc = new(scratchSize, scratchSize, GraphicsFormat.None, GraphicsFormat.D16_UNorm);
        DepthTexture = new(depthDesc) { hideFlags = HideFlags.DontSave };
        DepthTexture.Create();
    }

    public Texture2D       Texture;
    public int             TextureID;
    public ScratchBuffer[] ScratchBuffers;
    public RenderTexture   DepthTexture;

    public void Dispose()
    {
        ObjectUtility.SafeDestroy(Texture);
        ObjectUtility.SafeDestroy(DepthTexture);
        for (int i = 0; i < ScratchBuffers.Length; i++)
        {
            ObjectUtility.SafeDestroy(ScratchBuffers[i].renderTarget);
            ObjectUtility.SafeDestroy(ScratchBuffers[i].compressTarget);
        }
    }
}

[System.Serializable]
public struct RemapRequest
{
    public RemapRequest(int2 oldCoords, int2 newCoords, int destSize, int deltaMip, int maxMip, int ownerID)
    {
        originX = (ushort)oldCoords.x;
        originY = (ushort)oldCoords.y;

        destX = (ushort)newCoords.x;
        destY = (ushort)newCoords.y;

        this.destSize = (ushort)destSize;

        this.deltaMip = (sbyte)deltaMip;
        this.ownerID  = (byte)ownerID;
        this.maxMip   = (byte)maxMip;
    }

    public ushort originX, originY;
    public ushort destX,   destY;
    public ushort destSize;

    public sbyte deltaMip;
    public byte  maxMip;
    public byte  ownerID;
}

public struct VTRenderRequest
{
    public VTPage page;
    public int2   pageCoord;

    public ArraySegment<ScratchBuffer> scratchBuffers;
    public RenderTexture dummyDepthTarget;

    public void Compress(CommandBuffer cmd)
    {
        for (int i = 0; i < scratchBuffers.Count; i++)
        {
            scratchBuffers[i].Compress(cmd);
        }
    }

    public override string ToString()
    {
        return $"{page}, Page Offset: {pageCoord}";
    }
}

public sealed class VirtualTexture : ScriptableObject
{
    private static ComputeShader PopulateIndirectionTableShader => TerrainResources.Instance.PopulateIndirectionMapShader;

    private static readonly ArrayPool<ScratchBuffer> s_ScratchPool = ArrayPool<ScratchBuffer>.Create(16, 128);

    private int                       _vtFrame;
    private GraphicsBuffer            _textureUpdatesBuffer;
    private CBuffer<VTCommon_CBuffer> _commonCBuffer;

    private IndirectionMapDelta _delta;

    private int[]          _cacheTextureIDs;
    private CacheTexture[] _cacheTextures;
    private int            _totalTileSize;

    private RenderTexture _indirectionTexture;
    private int           _indirectionMipCount;
    private VTPageCache   _pageCache;

    private UniqueIndirectionUpdateList _queuedUpdates;
    private NativeList<int4>            _queuedFlushes;
    private NativeList<RemapRequest>    _queuedRemaps;

    public UniqueIndirectionUpdateList UpdateQueue => _queuedUpdates;
    public NativeList<int4>            FlushQueue  => _queuedFlushes;
    public NativeList<RemapRequest>    RemapQueue  => _queuedRemaps;

    public VirtualTextureDescriptor  desc                       { get; private set; }
    public int                       IndirectionTextureSize     => (int)desc.IndirectionTextureSize;
    public int                       IndirectionTextureMipCount => _indirectionMipCount;
    public CBuffer<VTCommon_CBuffer> CommonCBuffer              => _commonCBuffer;
    public Texture                   IndirectionTexture         => _indirectionTexture;

    public int[]     CacheTextureNameIDs          => _cacheTextureIDs;
    public Texture2D GetCacheTextureAt(int index) => _cacheTextures[index].Texture;

    public VTPageCache PageCache => _pageCache;

    public static VirtualTexture CreateInstance(VirtualTextureDescriptor descriptor)
    {
        var vt = ScriptableObject.CreateInstance<VirtualTexture>();
        vt.desc = descriptor;
        vt.Initialize();

        return vt;
    }

    unsafe void Initialize()
    {
        _indirectionMipCount = math.floorlog2((int)desc.IndirectionTextureSize) + 1;

        _indirectionTexture = new((int)desc.IndirectionTextureSize, (int)desc.IndirectionTextureSize,
                                  GraphicsFormat.R8G8B8A8_UNorm, GraphicsFormat.None, _indirectionMipCount) { hideFlags = HideFlags.DontSave };
        _indirectionTexture.useMipMap         = true;
        _indirectionTexture.enableRandomWrite = true;
        _indirectionTexture.wrapMode          = TextureWrapMode.Clamp;
        _indirectionTexture.filterMode        = FilterMode.Bilinear;
        _indirectionTexture.anisoLevel        = (int)desc.TileBorder;
        _indirectionTexture.Create();

        _totalTileSize = desc.TileSize + ((int)desc.TileBorder * 2);

        _pageCache     = new((int)desc.TileCountWide);
        _queuedUpdates = new(255, Allocator.Persistent);
        _queuedRemaps  = new(255, Allocator.Persistent);
        _queuedFlushes = new(255, Allocator.Persistent);

        _cacheTextures   = new CacheTexture[desc.CacheTextureDescriptors.Length];
        _cacheTextureIDs = new int         [desc.CacheTextureDescriptors.Length];

        for (int i = 0; i < _cacheTextures.Length; i++)
        {
            _cacheTextureIDs[i] = Shader.PropertyToID(desc.CacheTextureDescriptors[i].Name);
            _cacheTextures  [i] = new(desc.CacheTextureSize,    _totalTileSize,
                                      desc.ScratchBuffersCount, desc.CacheTextureDescriptors[i]);
        }

        _textureUpdatesBuffer = new(GraphicsBuffer.Target.Structured, IndirectionTextureSize * IndirectionTextureSize * 2, sizeof(IndirectionTextureUpdate));

        _commonCBuffer = new();

        VTCommon_CBuffer commonData = default;

        commonData.IndirectionTextureSize    = new(IndirectionTexture.width, IndirectionTexture.height);
        commonData.IndirectionTextureSizeInt = (uint)commonData.IndirectionTextureSize.x;
        commonData.CacheBorderSize           = (byte)desc.TileBorder;
        commonData.CacheTileCountWide        = (byte)desc.TileCountWide;
        commonData.CacheTileSize             = (ushort)_totalTileSize;
        commonData.PhysicalTextureRcpSize    = 1f / desc.CacheTextureSize;
        commonData.CacheScale                = (float)(commonData.CacheTileSize - 2.0 * commonData.CacheBorderSize) * commonData.PhysicalTextureRcpSize;

        _delta = new(_indirectionMipCount, Allocator.Persistent);
        _commonCBuffer.Update(commonData); // We have to set per view data so I guess its fine to update it together with vt data
    }

    private void OnDestroy()
    {
        _queuedUpdates       .Dispose();
        _queuedFlushes       .Dispose();
        _queuedRemaps        .Dispose();
        _pageCache           .Dispose();
        _textureUpdatesBuffer.Dispose();
        _commonCBuffer.Dispose();
        _delta.Dispose();

        for (int i = 0; i < _cacheTextures.Length; i++)
        {
            var cacheTexture = _cacheTextures[i];
            ObjectUtility.SafeDestroy(ref cacheTexture.Texture);
            ObjectUtility.SafeDestroy(ref cacheTexture.DepthTexture);

            for (int j = 0; j < cacheTexture.ScratchBuffers.Length; j++)
            {
                ObjectUtility.SafeDestroy(ref cacheTexture.ScratchBuffers[j].renderTarget);
                ObjectUtility.SafeDestroy(ref cacheTexture.ScratchBuffers[j].compressTarget);
            }
        }

        ObjectUtility.SafeDestroy(ref _indirectionTexture);
    }

    public JobHandle ScheduleQueuedRequestsUpdate(int maxRenderRequests, JobHandle dependsOn = default)
    {
        int unityFrame = Time.frameCount;

        UpdateVirtualTextureJob updateJob;
        updateJob.unityFrame          = unityFrame;
        updateJob.indirectionMipCount = _indirectionMipCount;
        updateJob.delta               = _delta;
        updateJob.queuedUpdates       = _queuedUpdates;
        updateJob.queuedFlushes       = _queuedFlushes;
        updateJob.queuedRemaps        = _queuedRemaps;
        updateJob.pageCache           = _pageCache;
        updateJob.maxRenderRequests   = Mathf.Min(desc.ScratchBuffersCount, maxRenderRequests);

        var handle = updateJob.Schedule(dependsOn);
        return handle;
    }

    /// <summary>
    /// Reads from <see cref="UpdateQueue"/> and based on page usage populates renderRequests list
    /// </summary>
    public unsafe void UpdateQueuedRequests(CommandBuffer cmd, JobHandle requestsUpdateHandle, List<VTRenderRequest> renderRequests = null)
    {
        requestsUpdateHandle.Complete();
        _queuedRemaps.Clear();
        _queuedUpdates.Clear();
        _queuedFlushes.Clear();

        if (_delta.Length == 0)
        {
            _delta.Clear();

            return;
        }

        // Here we take all indirection updates that were written per mip level
        // And compress them into single array to upload to GPU at once
        Span<int>                             offsets         = stackalloc int[_indirectionMipCount + 1];
        NativeArray<IndirectionTextureUpdate> updatesCombined = new(_delta.Length, Allocator.Temp);
        _delta.Combine(offsets, updatesCombined);

        if (renderRequests is not null)
        {
            int scratchIndex = 0;
            for (int i = 0; i < updatesCombined.Length; i++)
            {
                IndirectionTextureUpdate update = updatesCombined[i];
                // Assert.IsTrue((update.content.usedFlag == 0).Means(update.content.renderFlag == 0));
                Assert.IsTrue(update.content.usedFlag != 0 || (update.content.renderFlag == 0));

                if (update.content.renderFlag != 0)
                {
                    // kind of a waste :(
                    int address = _pageCache.GetPageId(update.x, update.y, update.content.mip);

                    ArraySegment<ScratchBuffer> scratchBuffers
                        = new ArraySegment<ScratchBuffer>(s_ScratchPool.Rent(CacheTextureNameIDs.Length), 0, CacheTextureNameIDs.Length);
                    for (int iScratch = 0; iScratch < CacheTextureNameIDs.Length; iScratch++)
                    {
                        scratchBuffers[iScratch] = _cacheTextures[iScratch].ScratchBuffers[scratchIndex];
                    }

                    scratchIndex++;

                    renderRequests.Add(new()
                    {
                        page       = _pageCache[address],
                        pageCoord = new(update.content.pageX, update.content.pageY),

                        scratchBuffers = scratchBuffers,
                        dummyDepthTarget = _cacheTextures[0].DepthTexture,
                    });

                    // if(scratchIndex >= desc.ScratchBuffersCount)
                    //     break;
                }
            }
        }

        cmd.BeginSample("Virtual Texture Update");
        cmd.SetBufferData(_textureUpdatesBuffer, updatesCombined);

        var populateIndirectionTableShader = PopulateIndirectionTableShader;
        Assert.IsNotNull(populateIndirectionTableShader);

        int writeUpdatesKernel = populateIndirectionTableShader.FindKernel("AVT_WriteTextureUpdates");
        cmd.SetComputeBufferParam(populateIndirectionTableShader, writeUpdatesKernel, "TextureUpdateList", _textureUpdatesBuffer);

        int maxTouchedMip = 0;
        for (int i = 0; i < _delta.MipCount; i++)
        {
            int updatesCount = _delta.MipCountAt(i);
            if (updatesCount == 0)
                continue;

            cmd.SetComputeTextureParam(populateIndirectionTableShader, writeUpdatesKernel, "TargetMip", _indirectionTexture, mipLevel: i);
            cmd.SetComputeIntParam(populateIndirectionTableShader, "BaseUpdateIndex",    offsets[i]);
            cmd.SetComputeIntParam(populateIndirectionTableShader, "TextureUpdateCount", updatesCount);
            cmd.DispatchCompute(populateIndirectionTableShader, writeUpdatesKernel, Mathf.CeilToInt(updatesCount / 64f), 1, 1);

            maxTouchedMip = i;
        }

        // Might need to populate unused parts of indirection tree with mips from lower levels
        // Sadly, a lot of dependency and sync for GPU here :(, but this is due to shaders being unable to map all mips of a texture as UAV so
        if (maxTouchedMip > 0)
        {
            // Btw, this shader writes any value from lower mip levels, even if lower level is also unused,
            // which means it'll automatically clear up unused parts of the texture
            int writeMissingPixelsKernel = populateIndirectionTableShader.FindKernel("AVT_WriteMissingPixels");
            for (int i = _indirectionMipCount - 2; i >= 0; i--)
            {
                int mipSize = IndirectionTextureSize >> i;
                cmd.SetComputeIntParam(populateIndirectionTableShader, "IndirectionAABSSize", mipSize);
                cmd.SetComputeTextureParam(populateIndirectionTableShader, writeMissingPixelsKernel, "TargetMip",   _indirectionTexture,
                                           mipLevel: i);
                cmd.SetComputeTextureParam(populateIndirectionTableShader, writeMissingPixelsKernel, "PreviousMip", _indirectionTexture,
                                           mipLevel: i + 1);

                int kernelSize = Mathf.Max(1, Mathf.CeilToInt((mipSize / 8f)));
                cmd.DispatchCompute(populateIndirectionTableShader, writeMissingPixelsKernel, kernelSize, kernelSize, 1);
            }
        }

        cmd.EndSample("Virtual Texture Update");
        updatesCombined.Dispose();
        _delta.Clear();
    }

    // -- Copy all scratch buffers to virtual texture cache
    public void CopyScratchToCache(CommandBuffer cmd, List<VTRenderRequest> renderRequests)
    {
        for (int iRequest = 0; iRequest < renderRequests.Count; iRequest++)
        {
            var request        = renderRequests[iRequest];
            var scratchBuffers = request.scratchBuffers;

            int2 dstCoords = request.pageCoord * _totalTileSize;

            for (int iScratch = 0; iScratch < scratchBuffers.Count; iScratch++)
            {
                var scratch = scratchBuffers[iScratch];
                var cache   = _cacheTextures[iScratch];

                cmd.CopyTexture(scratch.compressTarget, 0, 0, 0, 0, scratch.compressTarget.width, scratch.compressTarget.height,
                                cache.Texture, 0, 0, dstCoords.x, dstCoords.y);
            }

            s_ScratchPool.Return(renderRequests[iRequest].scratchBuffers.Array, false);
        }
    }
}
}