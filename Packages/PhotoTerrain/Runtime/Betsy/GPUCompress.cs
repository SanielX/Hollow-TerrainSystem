using UnityEngine;
using UnityEngine.Rendering;

namespace Hollow
{
internal static class GPUCompressor
{
    private static ComputeShader _compressBC1_RGB;

    public static ComputeShader CompressBC1_RGB
    {
        get
        {
            if (_compressBC1_RGB)
                return _compressBC1_RGB;

            return _compressBC1_RGB = Resources.Load<ComputeShader>("PT/Compression/BC1");
        }
    }

    static int alignToNextMultiple(int offset, int alignment)
    {
        return ((offset + alignment - 1) / alignment) * alignment;
    }

    public static int GetBC1BlockWidth (int w) => (w + 3)  / 4;
    public static int GetBC1BlockHeight(int h) => (h + 3) / 4;

    public static void CompressBC1(CommandBuffer cmd, RenderTexture scratchBuffer, RenderTexture compressedBuffer)
    {
        if (!scratchBuffer)
        {
            Debug.LogError("Invalid scratch buffer");
            return;
        }

        var compressShader = CompressBC1_RGB;

        cmd.SetComputeTextureParam(compressShader, 0, "srcTex", scratchBuffer);
        cmd.SetComputeTextureParam(compressShader, 0, "dstTexture", compressedBuffer);
        cmd.DispatchCompute(compressShader, 0, alignToNextMultiple(scratchBuffer.width,  (8 * 4))  / (8 * 4),
                            alignToNextMultiple(scratchBuffer.height, (8 * 4))  / (8 * 4), 1);
    }
}
}