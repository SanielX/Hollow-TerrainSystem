using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using Hollow.Extensions;

namespace Hollow.VirtualTexturing
{
[BurstCompile]
public struct UpdateVirtualTextureJob : IJob
{
    public int indirectionMipCount;
    public int unityFrame;
    public int maxRenderRequests;

    public IndirectionMapDelta         delta;
    public UniqueIndirectionUpdateList queuedUpdates;
    public NativeList<int4>            queuedFlushes;
    public NativeList<RemapRequest>    queuedRemaps;
    public VTPageCache                 pageCache;

    public void Execute()
    {
        if (queuedFlushes.Length > 0)
        {
            var allMappedPageIds = pageCache._pageMap.GetValueArray(Allocator.Temp);

            for (int iPage = 0; iPage < allMappedPageIds.Length; iPage++)
            {
                int pageId = allMappedPageIds[iPage];
                var page = pageCache[pageId];

                Assert.IsTrue(page.IsMapped);

                for (int iFlushRegion = 0; iFlushRegion < queuedFlushes.Length; iFlushRegion++)
                {
                    int2 flushAreaBegin = queuedFlushes[iFlushRegion].xy >> page.mip;
                    int2 flushAreaEnd   = queuedFlushes[iFlushRegion].zw >> page.mip;

                    if (page.x >= flushAreaBegin.x && page.y >= flushAreaBegin.y &&
                        page.x < flushAreaEnd.x && page.y < flushAreaEnd.y)
                    {
                        delta.Unmap(page.x, page.y, page.mip, out _);
                        pageCache.Unmap(pageId);
                    }
                }
            }

            allMappedPageIds.Dispose();
        }

        // for (int i = 0; i < queuedFlushes.Length; i++)
        // {
        //     var flush = queuedFlushes[i];
        //     int pageId = pageCache.GetPageId(flush.x, flush.y, flush.mip);
        //     if (pageId >= 0)
        //     {
        //         delta.Unmap(flush.x, flush.y, flush.mip, out _);
        //         pageCache.Unmap(pageId);
        //     }
        // }

        IndexingBinaryMinHeap updateHeap = new(queuedUpdates.Length, Allocator.Temp);
        int                   pagesToMap = 0;

        // First we go through list of queued updates to determine if page is already mapped,
        // if it is, we just update it and move on, as a result updateHeap only containts unmapped pages
        for (int i = 0; i < queuedUpdates.Length; i++)
        {
            var update    = queuedUpdates[i];
            int pageIndex = pageCache.GetPageId(update.x, update.y, update.mip);

            if (pageIndex >= 0)
            {
                pageCache.Touch(pageIndex, unityFrame);
                updateHeap.Add(uint.MaxValue, i);
            }
            else
            {
                const uint _28bit_max_value = 0xFFF_FFFFu;
                uint       mipPriority      = (uint)indirectionMipCount - queuedUpdates[i].mip;
                uint       countPriority    = _28bit_max_value - (((uint)queuedUpdates.CountAt(i)) * mipPriority);
                uint       priorityKey      = (mipPriority & 0xF) | (countPriority << 4);

                updateHeap.Add(priorityKey, i);
                pagesToMap++;
            }
        }

        int requestedUpdatesCount = Mathf.Min(maxRenderRequests, pagesToMap);

        int c                    = pagesToMap;
        int possibleUpdatesCount = requestedUpdatesCount;
        while (possibleUpdatesCount > 0 && c > 0)
        {
            if (!pageCache.HasAvailablePages(unityFrame, 3))
                break;

            var topUpdateIndex = updateHeap.GetTopIndex();
            var update         = queuedUpdates[topUpdateIndex];

            updateHeap.Update(uint.MaxValue, topUpdateIndex);
            c--;

            int    leastUsedCachePageIndex = pageCache.Top();
            VTPage leastUsedPage           = pageCache[leastUsedCachePageIndex];

            if (leastUsedPage.IsMapped)
            {
                pageCache.Unmap(leastUsedCachePageIndex);
                delta.Unmap(leastUsedPage.x, leastUsedPage.y, leastUsedPage.mip, out _);
            }

            int mappedPageAddress = pageCache.Map(update.x, update.y, update.ownerID, update.mip, unityFrame);
            if (mappedPageAddress < 0)
            {
                // Couldn't map the page for some reason (currently only reason is if page is already mapped)
                // Which btw shouldn't happen
                continue;
            }

            var pageOffset = pageCache.GetPageOffset(mappedPageAddress);
            delta.Map(update.x, update.y, update.mip, pageOffset.x, pageOffset.y, update.mip, 255);

            possibleUpdatesCount--;
        }

        for (int i = 0; i < queuedRemaps.Length; i++)
        {
            var  remapRequest = queuedRemaps[i];
            int2 dest         = new int2(remapRequest.destX,   remapRequest.destY);
            int2 origin       = new int2(remapRequest.originX, remapRequest.originY);
            // int2 deltaCoords = dest - origin;

            // TODO: Remove copy!
            // it is necessary because we unmap pages sometimes which causes mappedPagesAt to mutate :(
            UnsafeList <ushort> mappedPagesAt = pageCache.MappedPagesAt(remapRequest.ownerID);
            NativeArray<ushort> pages         = new NativeArray<ushort>(mappedPagesAt.AsNativeArray(), Allocator.Temp);

            for (int j = 0; j < pages.Length; j++)
            {
                int pageIndex = pages[j];
                var page      = pageCache[pageIndex];

                Assert.IsTrue(page.IsMapped);

                int newMip = page.mip - remapRequest.deltaMip;

                // Used flag is divided into usedFlag which is general case and render flag
                // render flag is required only for CPU to know if this page should be rendered this frame
                // Reason why we keep it like this is because certain page can be mapped and then remapped pretty much immediately
                // so we need to keep which page is actually being rendered to
                delta.Unmap(page.x, page.y, page.mip, out byte renderFlag);
                if (newMip >= 0 && newMip <= remapRequest.maxMip)
                {
                    var pageOffset = pageCache.GetPageOffset(pageIndex);

                    int2 pageCoord  = new(page.x, page.y);
                    int2 localCoord = pageCoord - (origin >> page.mip);

                    int2 newCoord = (dest >> newMip) + localCoord;

                    // When remapping, we remap render flag as well
                    delta.Map(newCoord.x, newCoord.y, newMip, pageOffset.x, pageOffset.y, newMip, renderFlag);
                    pageCache.Remap(pageIndex, newCoord.x, newCoord.y, newMip,
                                    remapRequest.ownerID, unityFrame);
                }
                else
                {
                    pageCache.Unmap(pageIndex);
                }
            }

            pages.Dispose();
        }
    }
}
}