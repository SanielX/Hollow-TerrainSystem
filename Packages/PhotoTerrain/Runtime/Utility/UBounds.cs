using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using System.Runtime.CompilerServices;

namespace Hollow
{
/// <summary>
/// These bounds store min and max instead of center and extends like <see cref="Bounds"/>
/// </summary>
[System.Serializable]
public struct UBounds : IEquatable<UBounds>
{
    public UBounds(in float3 min, in float3 max)
    {
        Assert.IsTrue(math.all(max >= min), "math.all(max >= min)");

        this.min = min;
        this.max = max;
    }

    public float3 min;
    public float3 max;

    public float3 center
    {
        readonly get => (min + max) * 0.5f;

        set
        {
            var extends = this.extents;

            min = value - extends;
            max = value + extends;
        }
    }

    /// <summary>
    /// <code>
    /// center + extends = max; 
    /// center - extends = min;
    /// </code>
    /// </summary>
    public float3 extents
    {
        readonly get => (max - min) * 0.5f;

        set
        {
            var ctr = center;

            min = ctr - value;
            max = ctr + value;
        }
    }

    /// <summary>
    /// Twice as big as <see cref="extents"/>
    /// </summary>
    public float3 size
    {
        readonly get => max - min;
        set
        {
            var ctr = center;
            value *= .5f;

            min = ctr - value;
            max = ctr + value;
        }
    }

    public readonly float volume()
    {
        float3 _size = size;
        return _size.x * _size.y * _size.z;
    }

    public override readonly string ToString()
    {
        return $"(min: ({min}); max: ({max}))";
    }

    public static implicit operator Bounds(in  UBounds b) => new Bounds { min  = b.min, max = b.max };
    public static implicit operator UBounds(in Bounds  b) => new UBounds { min = b.min, max = b.max };

    public UBounds WithSize(float3 size1)
    {
        return new()
        {
            center = this.center,
            size   = size1
        };
    }

    public bool Equals(UBounds other)
    {
        return min.Equals(other.min) && max.Equals(other.max);
    }

    public override bool Equals(object obj)
    {
        return obj is UBounds other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(min, max);
    }
}

public static class UBoundsUtility
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rect ToRect(this UBounds bounds)
    {
        return new(bounds.min.xz, bounds.size.xz);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Overlaps(this in UBounds b0, in UBounds b1)
    {
        return (b0.min.x <= b1.max.x && b0.max.x >= b1.min.x) &&
               (b0.min.y <= b1.max.y && b0.max.y >= b1.min.y) &&
               (b0.min.z <= b1.max.z && b0.max.z >= b1.min.z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool InteresectsSphere(this in UBounds b, in float4 sphere)
    {
        var closestPoint = ClosestPoint(b, sphere.xyz);
        var diff         = sphere.xyz - closestPoint;

        float distSqr   = math.lengthsq(diff);
        float radiusSqr = sphere.w * sphere.w;

        return distSqr < radiusSqr;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 ClosestPoint(this in UBounds b, in float3 point)
    {
        return math.clamp(point, b.min, b.max);
    }

    public static float DistanceTo(this in UBounds b, float3 point)
    {
        return math.length(ClosestPoint(b, point) - (point));
    }

    /// <summary>
    /// Add point, so bound volume will extend if needed
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Encapsulate(this ref UBounds b, in float3 point)
    {
        b.min = math.select(b.min, point, point < b.min);
        b.max = math.select(b.max, point, point > b.max);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UBounds Scale(this UBounds b, float scale)
    {
        b.min *= scale;
        b.max *= scale;
        return b;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UBounds Scale(this UBounds b, float3 scale)
    {
        b.extents *= scale;
        return b;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UBounds Combine(this UBounds b0, in UBounds b1)
    {
        b0.Encapsulate(b1.min);
        b0.Encapsulate(b1.max);
        return b0;
    }
}
}