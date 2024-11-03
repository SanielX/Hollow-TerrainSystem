using System.Runtime.InteropServices;

namespace Hollow.VirtualTexturing
{
[System.Serializable]
[StructLayout(LayoutKind.Sequential)]
public struct IndirectionTextureContent
{
    public byte pageX, pageY, mip, _usedFlag;

    public byte usedFlag
    {
        get => (byte)(_usedFlag & 0b1);
        set => _usedFlag = (byte)((_usedFlag & ~0b1) | (value & 0b1));
    }

    public byte renderFlag
    {
        get => (byte)(_usedFlag & 0b10);
        set => _usedFlag = (byte)((_usedFlag & ~0b10) | (value & 0b10));
    }

    public override string ToString()
    {
        return $"IndirectionTextureContent(pageX: {pageX}, pageY:{pageY}, pageMip:{mip}, isMapped:{usedFlag != 0})";
    }
}
}