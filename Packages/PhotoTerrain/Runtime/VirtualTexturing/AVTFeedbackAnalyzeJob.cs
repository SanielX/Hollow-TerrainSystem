// #define SIMPLE_STUPID_SEARCH

using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;

namespace Hollow.VirtualTexturing
{
[BurstCompile]
public unsafe struct AVTFeedbackAnalyzeJob : IJob
{
    // These markers add a lot of overhead :P
    // private static readonly ProfilerMarker virtualImageSearchMarker = new(ProfilerCategory.Render, "Search Virtual Image");
    // private static readonly ProfilerMarker addPixelMarker           = new(ProfilerCategory.Render, "Add pixel");

    [ReadOnly] public NativeArray<int> input;
    public int2             inputReadRange;

    public int indirectionTextureResolution;

    [ReadOnly] public NativeArray<VirtualImage> virtualImages;

    [NativeDisableUnsafePtrRestriction] public UnsafeUniqueIndirectionUpdateList* output;

    public void Execute()
    {
        inputReadRange.y = math.min(inputReadRange.y, input.Length);

        int lastValidImage = virtualImages.Length;
        for (int i = lastValidImage - 1; i >= 0 ; i--)
        {
            var image = virtualImages[i];
            if (image.atlasAllocID >= 0)
            {
                lastValidImage = i + 1;
                break;
            }
        }

        int lastUsedImage = -1;
        for (int i = inputReadRange.x; i < inputReadRange.y; i++)
        {
            var feedbackValueInt = input[i];
            if (feedbackValueInt >= 0) // written flag is 1 << 31 so any value that is not negative was not actually written to
                continue;

            int absX = (ushort)((feedbackValueInt >>  0 ) & 0xFFF);
            int absY = (ushort)((feedbackValueInt >> 12 ) & 0xFFF);

            // So uh
            // We need to figure out which virtual image do these coords belong to
            // We could encode that directly into feedback buffer but I'm not sure if that's actually fittable into 32b
            // So lets just do linear search :P
            int imageID = -1;
            int maxMip  = 0;

            //  virtualImageSearchMarker.Begin();
#if SIMPLE_STUPID_SEARCH
                for (int j = 0; j < lastValidImage; j++)
                {
                    var image = virtualImages[j];
                    if (image.atlasAllocID < 0)
                        continue;

                    if (absX >= image.x &&
                        absY >= image.y &&
                        absX < image.x + image.size &&
                        absY < image.y + image.size)
                    {
                        maxMip    = image.mipCount - 1;
                        imageID   = j;
                        lastUsedImage = j;
                        break;
                    }
                }
#else
            if (lastUsedImage < 0)
            {
                for (int j = 0; j < lastValidImage; j++)
                {
                    var image = virtualImages[j];
                    if (image.atlasAllocID < 0)
                        continue;

                    if (absX >= image.x &&
                        absY >= image.y &&
                        absX < image.x + image.size &&
                        absY < image.y + image.size)
                    {
                        maxMip    = image.mipCount - 1;
                        imageID   = j;
                        lastUsedImage = j;
                        break;
                    }
                }
            }
            else
            {
                // Observation: pixels that are near usually share virtual image, so start search from there
                // This algorithm speeds up feedback analysis from 2ms to 0.2 or something so yeah, it works
                // Do same loop from last used imageId downwards
                for (int j = lastUsedImage; j >= 0; j--)
                {
                    var image = virtualImages[j];
                    if (image.atlasAllocID < 0)
                        continue;

                    if (absX >= image.x &&
                        absY >= image.y &&
                        absX < image.x + image.size &&
                        absY < image.y + image.size)
                    {
                        maxMip        = image.mipCount - 1;
                        imageID       = j;
                        lastUsedImage = j;
                        break;
                    }
                }

                if (imageID < 0)
                {
                    // Do same loop upwards
                    for (int j = lastUsedImage + 1; j < lastValidImage; j++)
                    {
                        var image = virtualImages[j];
                        if (image.atlasAllocID < 0)
                            continue;

                        if (absX >= image.x &&
                            absY >= image.y &&
                            absX < image.x + image.size &&
                            absY < image.y + image.size)
                        {
                            maxMip        = image.mipCount - 1;
                            imageID       = j;
                            lastUsedImage = j;
                            break;
                        }
                    }
                }
            }
#endif

            //    virtualImageSearchMarker.End();

            if (imageID < 0)
            {
#if UNITY_ASSERTIONS
                //      if(!everErrored)
                //      {
                //          Debug.LogError("Couldn't find imageID for a feedback pixel");
                //          everErrored = true;
                //      }
#endif
                continue;
            }

            //   addPixelMarker.Begin();
            // Mip is written as visible mip + 1,
            // Idea is add automatic image resize in future when resolution isn't sufficient
            int mip = ((feedbackValueInt >> 24) & 0xF) - 1;
            mip = math.clamp(mip, 0, maxMip);

            absX >>= mip;
            absY >>= mip;

            output->Add(new(absX, absY, mip, imageID));

            if (mip < maxMip) // Each feedback texel should request self and mip below it for trilinear filtering
            {
                absX >>= 1;
                absY >>= 1;
                mip++;
                output->Add(new(absX, absY, mip, imageID));
            }
            //     addPixelMarker.End();
        }
    }
}
}