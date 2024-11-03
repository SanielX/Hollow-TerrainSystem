using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Hollow.Extensions
{
internal static class VectorExtensions
{
    // https://www.jwwalker.com/pages/angle-between-vectors.html
    public static float Angle(this in Vector3 v, in Vector3 u)
    {
        var vm = v.magnitude;
        var um = u.magnitude;

        Vector3 vmu = vm * u;
        Vector3 umv = um * v;
        float y = (vmu - umv).magnitude;
        float x = (vmu + umv).magnitude;
        return (Mathf.Rad2Deg * 2.0f) * Mathf.Atan2(y, x);
    }

    /// <summary> Assumes both vectors are the same length </summary>
    public static float AngleFast(this in Vector3 v, in Vector3 u)
    {
        float y = (u - v).magnitude;
        float x = (u + v).magnitude;
        return (Mathf.Rad2Deg * 2.0f) * Mathf.Atan2(y, x);
    }

    public static Vector3 Normalize(this in Vector3 vec, out float magnitude)
    {
        magnitude = vec.magnitude;
        return magnitude > float.Epsilon ? vec / magnitude : vec;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 Reorient(this in Vector3 vec, in Vector3 up, in Vector3 normal)
    {
        var cross0 = Vector3.Cross(up, vec);
        return Vector3.Cross(cross0, normal);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 Reorient(this in Vector3 vec, in Vector3 normal)
    {
        var cross0 = Vector3.Cross(Vector3.up, vec);
        return Vector3.Cross(cross0, normal);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 ProjectOnPlane(this in Vector3 vec, in Vector3 normal)
    {
        return Vector3.ProjectOnPlane(vec, normal);
    }

    /// <summary>
    /// Distance between 2 points projected on XZ plane
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float HorizontalDistanceTo(this in Vector3 a, in Vector3 b)
    {
        return (float)System.Math.Sqrt(((a.x - b.x) * (a.x - b.x) + (a.z - b.z) * (a.z - b.z)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float DistanceTo(this in Vector3 a, in Vector3 b)
    {
        return (float)System.Math.Sqrt(((a.x - b.x) * (a.x - b.x) + (a.y - b.y) * (a.y - b.y) + (a.z - b.z) * (a.z - b.z)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float DistanceToSqr(this in Vector3 a, in Vector3 b)
    {
        return (a.x - b.x) * (a.x - b.x) + (a.y - b.y) * (a.y - b.y) + (a.z - b.z) * (a.z - b.z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RandomizeThis(this ref Vector3 vec, in Vector3 mins, in Vector3 maxs)
    {
        vec.x = Random.Range(mins.x, maxs.x);
        vec.y = Random.Range(mins.y, maxs.y);
        vec.z = Random.Range(mins.z, maxs.z);
    }

    /// <summary> Both vectors are supposed to be in a same space </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 DirectionTo(this Vector3 a, in Vector3 b)
    {
        a.x = b.x - a.x;
        a.y = b.y - a.y;
        a.z = b.z - a.z;

        return a;
    }

    /// <summary> Both vectors are supposed to be in a same space </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 DirectionTo(this in float3 a, in float3 b)
    {
        return b - a;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float DistanceTo(this in float3 a, in float3 b)
    {
        return (float)System.Math.Sqrt(((a.x - b.x) * (a.x - b.x) + (a.y - b.y) * (a.y - b.y) + (a.z - b.z) * (a.z - b.z)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float DistanceToSqr(this in float3 a, in float3 b)
    {
        return (a.x - b.x) * (a.x - b.x) + (a.y - b.y) * (a.y - b.y) + (a.z - b.z) * (a.z - b.z);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 Reflect(this Vector3 v, Vector3 normal)
    {
        var velocityDotProduct = Vector3.Dot(normal, v);
        v -= ((2 * velocityDotProduct) * normal);
        return v;
    }

    public static Vector3 WithX(this in Vector3 vec, float x) => new Vector3(x,     vec.y, vec.z);
    public static Vector3 WithY(this in Vector3 vec, float y) => new Vector3(vec.x, y,     vec.z);
    public static Vector3 WithZ(this in Vector3 vec, float z) => new Vector3(vec.x, vec.y, z);

    public static Vector3 X0Z(this in Vector2 vec) => new Vector3(vec.x, 0, vec.y);
    public static float3  X0Z(this in float2  vec) => new float3 (vec.x, 0, vec.y);

    /// <returns>Vector with coordinates (<paramref name="vec"/>.x, 0, <paramref name="vec"/>.z)</returns>
    public static Vector3 X0Z(this in Vector3 vec) => new Vector3(vec.x, 0, vec.z);

    public static Vector2 XZ(this in  Vector3 vec) => new Vector3(vec.x, vec.z);
    public static Vector3 XYO(this in Vector3 vec) => new Vector3(vec.x, vec.y, 0);
    public static Vector3 OYO(this in Vector3 vec) => new Vector3(0,     vec.y, 0);
}
}