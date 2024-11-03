using System.Runtime.InteropServices;

namespace Hollow.VirtualTexturing
{
[System.Serializable]
[StructLayout(LayoutKind.Sequential)]
public struct IndirectionTextureUpdate
{
    public ushort x, y;
    public IndirectionTextureContent content;

    public static IndirectionTextureUpdate Map(int indirX, int indirY, int pageOffsetX, int pageOffsetY, int pageMip)
    {
        return new()
        {
            x = (ushort)indirX,
            y = (ushort)indirY,

            content = new()
            {
                pageX = (byte)pageOffsetX,
                pageY = (byte)pageOffsetY,
                mip   = (byte)pageMip,
                usedFlag = 255,
            }
        };
    }

    public static IndirectionTextureUpdate Unmap(int x, int y)
    {
        return new()
        {
            x = (ushort)x,
            y = (ushort)y,

            content = new()
            {
                _usedFlag = 0,
            }
        };
    }


    public static IndirectionTextureUpdate Unmap(int x, int y, int pageOffsetX, int pageOffsetY, int pageMip)
    {
        return new()
        {
            x = (ushort)x,
            y = (ushort)y,

            content = new()
            {
                pageX = (byte)pageOffsetX,
                pageY = (byte)pageOffsetY,
                mip   = (byte)pageMip,
                _usedFlag = 0,
            }
        };
    }
}
}