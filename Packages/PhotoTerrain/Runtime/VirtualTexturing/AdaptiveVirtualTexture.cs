using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Hollow.Rendering;
using Hollow.Extensions;

namespace Hollow.VirtualTexturing
{
[System.Serializable]
public struct AdaptiveVirtualTextureDescriptor
{
    public ushort maxVirtualImageSize;
}

public struct VirtualImage
{
    public int    atlasAllocID;
    public ushort x;
    public ushort y;
    public ushort size;
    public byte   mipCount;

    // public float4 uvRange;
    public float2 uvPosition;
    public float  uvSize;
    public float  derivativeScale;

    public int4 area => new (x, y, x + size, y + size);

    public int2 coords
    {
        get => new(x, y);
        set
        {
            x = (ushort)value.x;
            y = (ushort)value.y;
        }
    }
}

public sealed class AdaptiveVirtualTexture : ScriptableObject
{
    private AdaptiveVirtualTextureDescriptor               desc;
    private VirtualTexture                                 vt;
    private AtlasAllocatorPD2                              atlas;
    private NativeArray<VirtualImage>                      images;
    private NativeList <(int imageID, VirtualImage image)> delayedImages;

    public int                         LastRegionResizeFrame      { get; private set; }
    public UniqueIndirectionUpdateList UpdateQueue                => vt.UpdateQueue;
    public Texture                     IndirectionTexture         => vt.IndirectionTexture;
    public int                         IndirectionTextureSize     => vt.IndirectionTextureSize;
    public int                         IndirectionTextureMipCount => vt.IndirectionTextureMipCount;
    public CBuffer<VTCommon_CBuffer>   CommonCBuffer              => vt.CommonCBuffer;
    public int                         TileSize                   => vt.desc.TileSize;
    public int                         TileSizeWithBorder         => (int)(vt.desc.TileSize + ((int)vt.desc.TileBorder * 2));
    public VirtualTexture              VT                         => vt;
    public NativeArray<VirtualImage>   Images                     => images;

    public static AdaptiveVirtualTexture CreateInstance(VirtualTextureDescriptor vtDesc, AdaptiveVirtualTextureDescriptor avtDesc)
    {
        var avt = ScriptableObject.CreateInstance<AdaptiveVirtualTexture>();
        avt.desc          =  avtDesc;
        avt.vt            =  VirtualTexture.CreateInstance(vtDesc);
        avt.vt.hideFlags  |= HideFlags.DontSave;
        avt.atlas         =  new((int)vtDesc.IndirectionTextureSize);
        avt.images        =  new(256, Allocator.Persistent);
        avt.delayedImages =  new(256, Allocator.Persistent);

        for (int i = 0; i < avt.images.Length; i++)
        {
            avt.images.At(i).atlasAllocID = -1;
        }

        return avt;
    }

    public JobHandle ScheduleQueuedRequestsUpdate(int maxRenderRequests, JobHandle dependsOn = default)
    {
        return vt.ScheduleQueuedRequestsUpdate(maxRenderRequests, dependsOn);
    }

    public void UpdateQueuedRequests(CommandBuffer cmd, JobHandle scheduleUpdatesHandle, List<VTRenderRequest> renderRequests = null)
    {
        vt.UpdateQueuedRequests(cmd, scheduleUpdatesHandle, renderRequests);

        for (int i = 0; i < delayedImages.Length; i++)
        {
            ref var delayedImage = ref delayedImages.ElementAt(i);
            images[delayedImage.imageID] = delayedImage.image;
        }

        delayedImages.Clear();
    }

    public VirtualImage GetVirtualImage(int id) => images[id];

    public void ResizeVirtualImage(int imageID, int size)
    {
        ref var image = ref images.At(imageID);
        //            Assert.IsTrue(image.mipCount != 0, "image.mipCount != 0");

        if (image.size == size || image.mipCount == 0)
            return;

        var allocID = atlas.Alloc(size);
        if (allocID < 0)
            return;

        LastRegionResizeFrame = Time.frameCount;
        atlas.Free(image.atlasAllocID);

        int2 newCoords = atlas.TextureCoords(allocID);

        VirtualImage updatedImage = image;
        updatedImage.atlasAllocID = allocID;
        updatedImage.coords       = newCoords;
        updatedImage.size         = (ushort)size;
        UpdateVirtualImage(ref updatedImage);

        int deltaMip = image.mipCount - updatedImage.mipCount;

        RemapRequest remapRequest = new(image.coords, updatedImage.coords, updatedImage.size,
                                        deltaMip, updatedImage.mipCount - 1, imageID);
        vt.RemapQueue.Add(remapRequest);

        delayedImages.Add((imageID, updatedImage));
    }

    public int AllocateVirtualImage(int initialSize)
    {
        int imageIndex = FindFreeImageSlot();

        if (imageIndex < 0)
        {
            return -1;
        }

        int allocID = atlas.Alloc(initialSize);
        if (allocID < 0)
        {
            return -2;
        }

        int2    coords = atlas.TextureCoords(allocID);
        ref var rImage = ref images.At(imageIndex);
        rImage.atlasAllocID = allocID;
        rImage.x            = (ushort)coords.x;
        rImage.y            = (ushort)coords.y;
        rImage.size         = (ushort)initialSize;
        UpdateVirtualImage(ref rImage);

        return imageIndex;
    }

    void UpdateVirtualImage(ref VirtualImage rImage)
    {
        rImage.mipCount = (byte)(math.floorlog2((uint)rImage.size) + 1);

        float2 bl = new float2(rImage.x, rImage.y) ;
        float2 tr = new float2(rImage.x, rImage.y) + rImage.size;

        // rImage.uvRange         = new(bl / IndirectionTextureSize, tr / IndirectionTextureSize);
        rImage.uvPosition = bl / IndirectionTextureSize;
        rImage.uvSize     = rImage.size / (float)IndirectionTextureSize;
        rImage.derivativeScale = rImage.size * (vt.desc.TileSize - 2 * (int)vt.desc.TileBorder);
    }

    public int2 ToLocalCoords(int2 indirectionCoords, int mipLevel, int ownerID)
    {
        return ((indirectionCoords << mipLevel) - images[ownerID].coords) >> mipLevel;
    }

    private int FindFreeImageSlot()
    {
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i].atlasAllocID < 0 && images[i].mipCount == 0)
                return i;
        }

        return -1;
    }

    private void OnDestroy()
    {
        images.Dispose();
        images = default;

        delayedImages.Dispose();
        delayedImages = default;

        ObjectUtility.SafeDestroy(ref vt);

        atlas.Dispose();
    }

    public void Flush(int imageID = -1)
    {
        if (imageID < 0)
        {
            for (int i = 0; i < images.Length; i++)
            {
                var          image        = images[i];
                RemapRequest remapRequest = new(image.coords, image.coords, 0, -20, 0, imageID);
                vt.RemapQueue.Add(remapRequest);
            }
        }
        else
        {
            ref var image = ref images.At(imageID);

            RemapRequest remapRequest = new(image.coords, image.coords, 0, -20, 0, imageID);
            vt.RemapQueue.Add(remapRequest);
        }
    }

    public void FreeVirtualImage(int imageID)
    {
        ref var image = ref images.At(imageID);
        if (image.atlasAllocID < 0)
            return;

        atlas.Free(image.atlasAllocID);
        image.atlasAllocID = -1;

        LastRegionResizeFrame = Time.frameCount;
        // atlas.Free(image.atlasAllocID);
        VirtualImage updatedImage = default;
        // If we do remap where every remap is invalid it will essentially just delete every 
        // mapped page, so just do that!
        RemapRequest remapRequest = new(image.coords, 0, 0, 20, 0, imageID);
        vt.RemapQueue.Add(remapRequest);

        delayedImages.Add((imageID, updatedImage));
    }

    public void CopyToCache(CommandBuffer cmd, List<VTRenderRequest> renderRequests)
    {
        if (renderRequests is not null)
        {
            vt.CopyScratchToCache(cmd, renderRequests);
        }
    }

    public Texture2D GetCacheTextureAt(int i)
    {
        return vt.GetCacheTextureAt(i);
    }
}
}