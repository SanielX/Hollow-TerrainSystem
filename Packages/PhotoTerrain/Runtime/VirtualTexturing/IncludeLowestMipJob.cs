using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Hollow.VirtualTexturing
{
[BurstCompile]
public struct IncludeLowestMipJob : IJob
{
    public UniqueIndirectionUpdateList updateList;
    public NativeArray<VirtualImage>   images;

    public void Execute()
    {
        for (int i = 0; i < images.Length; i++)
        {
            var image = images[i];
            if (image.atlasAllocID < 0 || image.mipCount == 0)
                continue;

            int x = image.x >> (image.mipCount - 1);
            int y = image.y >> (image.mipCount - 1);

            updateList.Add(new(x, y, image.mipCount - 1, i), ushort.MaxValue);
        }
    }
}
}