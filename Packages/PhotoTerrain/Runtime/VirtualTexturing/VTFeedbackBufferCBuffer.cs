using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hollow.VirtualTexturing
{
[StructLayout(LayoutKind.Sequential)]
public struct VTFeedbackBufferCBuffer
{
    public int2 size;
    public int  frame;

    // If CBuffer is too small it won't be binded 
    private uint __pad;
    private uint4 _pad;
}
}