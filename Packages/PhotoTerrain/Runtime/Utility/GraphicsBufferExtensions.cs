using Hollow.Rendering;
using UnityEngine;

namespace Hollow.Extensions
{
internal static class GraphicsBufferExtensions
{
    public static bool IsNullOrInvalid(this GraphicsBuffer buffer)
    {
        return buffer is null || !buffer.IsValid();
    }

    public static bool IsNullOrInvalid<T>(this CBuffer<T> buffer) where T : unmanaged
    {
        return buffer is null || buffer.GraphicsBuffer is null || !buffer.GraphicsBuffer.IsValid();
    }

    public static bool IsNullOrInvalid(this RenderTexture rt)
    {
        return !rt;
    }
}
}