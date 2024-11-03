using System;

namespace Hollow.VirtualTexturing
{
[System.Serializable]
public struct IndirectionUpdate : IEquatable<IndirectionUpdate>
{
    public IndirectionUpdate(int x, int y, int mip, int ownerID)
    {
        this.x = (ushort)x;
        this.y = (ushort)y;
        this.mip = (byte)mip;
        this.ownerID = (byte)ownerID;
    }

    public ushort x, y;
    public byte   mip;
    public byte   ownerID;

    public bool Equals(IndirectionUpdate other)
    {
        return x == other.x && y == other.y && mip == other.mip && ownerID == other.ownerID;
    }

    public override bool Equals(object obj)
    {
        return obj is IndirectionUpdate other && Equals(other);
    }

    public override int GetHashCode()
    {
        return VTPageCache.IndirectionTexelHash(x, y, mip);
    }
}
}