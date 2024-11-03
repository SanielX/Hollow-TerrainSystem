using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;

namespace Hollow
{
[BurstCompile]
public static class RenderUtility
{
    public static float2 IntersectAABB(float3 rayOrigin, float3 rayDir, float3 boxMin, float3 boxMax)
    {
        float3 tMin  = (boxMin - rayOrigin) / rayDir;
        float3 tMax  = (boxMax - rayOrigin) / rayDir;
        float3 t1    = math.min(tMin, tMax);
        float3 t2    = math.max(tMin,                 tMax);
        float  tNear = math.max(math.max(t1.x, t1.y), t1.z);
        float  tFar  = math.min(math.min(t2.x, t2.y), t2.z);

        return math.float2(tNear, tFar);
    }

    [BurstCompile]
    public static void BoundsToScreenRect(in UBounds bounds, in float4x4 vpMatrix, out Rect r)
    {
        Span<float3> points = stackalloc float3[8];
        WriteBoundsPoints(bounds, points);

        float2 minPoint = (World2Screen(points[0], vpMatrix).xy + 1) * 0.5f;
        float2 maxPoint = minPoint;
        for (int i = 1; i < points.Length; i++)
        {
            var screenPoint = (World2Screen(points[i], vpMatrix).xy + 1) * 0.5f;
            // screenPoint.xy /= screenPoint.w;

            minPoint = min(minPoint, screenPoint.xy);
            maxPoint = max(maxPoint, screenPoint.xy);
        }

        r = new(minPoint, maxPoint - minPoint);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float4 World2Screen(in float3 point, in float4x4 proj, in float4x4 view)
    {
        float4 p = float4(point, 1.0f);
        p   =  mul(mul(proj, view), p);
        p.x /= p.w;
        p.y /= p.w;

        return p;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float4 World2Screen(in float3 point, in float4x4 vpMatrix)
    {
        float4 p = float4(point, 1.0f);
        p   =  mul(vpMatrix, p);
        p.x /= p.w;
        p.y /= p.w;

        return p;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 Screen2Clip(in float4 p)
    {
        return float3((p.xy + 1) * 0.5f, p.z);
    }

    public static float3 getLossyScale(this in float4x4 trs)
    {
        return float3(length(trs.c0), length(trs.c1), length(trs.c2));
    }

    [BurstDiscard]
    public static float4x4 TRS(this Transform transform)
    {
        quaternion rot = transform.rotation;

        TRS(transform.position, rot.value, transform.lossyScale, out var result);
        return result;
    }

    private unsafe delegate void InverseTRSDelegate(in Vector3 p, in Quaternion r, out Matrix4x4 invTrs);

    /// <summary>
    /// Identical to <see cref="Matrix4x4.TRS(Vector3, Quaternion, Vector3)"/> but burst compiled, so it's faster
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Strict), AOT.MonoPInvokeCallback(typeof(trsDelegate))]
    public static void InverseTransform(in Vector3 pos, in Quaternion rot, out RigidTransform invTrs)
    {
        RigidTransform transform = new(rot, pos);
        invTrs = math.inverse(transform);
    }

    [BurstDiscard]
    public static Matrix4x4 TRSNoScale(this Transform transform)
    {
        transform.GetPositionAndRotation(out var pos, out var rot);

        TRS(pos, float4(rot.x, rot.y, rot.z, rot.w), 1, out var result);
        return result;
    }

    [BurstDiscard]
    public static Matrix4x4 InverseTRSNoScale(this Transform transform)
    {
        transform.GetPositionAndRotation(out var pos, out var rot);

        TRSInverse(pos, float4(rot.x, rot.y, rot.z, rot.w), 1, out var result);
        return result;
    }

    [BurstCompile(FloatMode = FloatMode.Strict), AOT.MonoPInvokeCallback(typeof(trsDelegate))]
    public static void TRSInverse(in float3 t, in float4 r, in float3 s, out float4x4 res)
    {
        TRS(t, r, s, out res);
        res = math.inverse(res);
    }

    private unsafe delegate void trsDelegate(in float3 t, in float4 r, in float3 s, out float4x4 res);

    /// <summary>
    /// Identical to <see cref="Matrix4x4.TRS(Vector3, Quaternion, Vector3)"/> but burst compiled, so it's faster
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Strict), AOT.MonoPInvokeCallback(typeof(trsDelegate))]
    public static void TRS(in float3 t, in float4 r, in float3 s, out float4x4 res)
    {
        float m11 = (1.0f - 2.0f * (r.y * r.y + r.z * r.z)) * s.x;
        float m21 = (r.x * r.y + r.z * r.w) * s.x * 2.0f;
        float m31 = (r.x * r.z - r.y * r.w) * s.x * 2.0f;
        float m41 = 0.0f;
        float m12 = (r.x * r.y - r.z * r.w) * s.y * 2.0f;
        float m22 = (1.0f - 2.0f * (r.x * r.x + r.z * r.z)) * s.y;
        float m32 = (r.y * r.z + r.x * r.w) * s.y * 2.0f;
        float m42 = 0.0f;
        float m13 = (r.x * r.z + r.y * r.w) * s.z * 2.0f;
        float m23 = (r.y * r.z - r.x * r.w) * s.z * 2.0f;
        float m33 = (1.0f - 2.0f * (r.x * r.x + r.y * r.y)) * s.z;
        float m43 = 0.0f;
        float m14 = t.x;
        float m24 = t.y;
        float m34 = t.z;
        float m44 = 1.0f;

        res = new float4x4(m11, m12, m13, m14, m21, m22, m23, m24, m31, m32, m33, m34, m41, m42, m43, m44);
    }

    private unsafe delegate bool isInsideFrustumDelegate(in UBounds bounds, float4* planes);

    [BurstCompile(FloatMode = FloatMode.Fast), AOT.MonoPInvokeCallback(typeof(isInsideFrustumDelegate))]
    public static unsafe bool IsInsideFrustum(in UBounds bounds, float4* planes)
    {
        var center = bounds.center;
        var extent = bounds.extents;

        bool isInside = true;
        for (int i = 0; i < 6; i++)
        {
            var   plane    = planes[i];
            var   signFlip = sign(plane.xyz);
            float d        = dot(center + signFlip * extent, plane.xyz);
            isInside &= d > -plane.w;
        }

        return isInside;
    }

    /// <summary>
    /// True if point is on positive side of the plane (where plane normal "looks")
    /// </summary>
    public static bool HalfSpaceTest(in float4 plane, in float3 point)
    {
        return dot(plane.xyz, point) + plane.w > 0f;
    }

    public static unsafe void WriteCullFrustumPlanes(Camera camera, Vector4[] planes)
    {
        fixed (Vector4* p = &planes[0])
            WriteCullFrustumPlanes(camera.VPMatrix(), (float4*)p);
    }

    private unsafe delegate void writeCullPlanesDelegate(in float4x4 vp, float4* planes);

    /// <summary>
    /// Writes planes that can be used for frustum culling (all w components are mulitplied by 2)
    /// </summary>
    /// <param name="vpMatrix">View-Projection matrix of the camera</param>
    /// <param name="planes">Array planes will be written into</param>
    [BurstCompile(FloatMode = FloatMode.Fast), AOT.MonoPInvokeCallback(typeof(writeCullPlanesDelegate))]
    public static unsafe void WriteCullFrustumPlanes(in float4x4 vp, float4* planes)
    {
        // zNear
        planes[0] = float4(vp[0][3] + vp[0][0],
                           vp[1][3] + vp[1][0],
                           vp[2][3] + vp[2][0],
                           vp[3][3] + vp[3][0]);

        planes[1] = float4(vp[0][3] - vp[0][0],
                           vp[1][3] - vp[1][0],
                           vp[2][3] - vp[2][0],
                           vp[3][3] - vp[3][0]);

        planes[2] = float4(vp[0][3] + vp[0][1],
                           vp[1][3] + vp[1][1],
                           vp[2][3] + vp[2][1],
                           vp[3][3] + vp[3][1]);

        planes[3] = float4(vp[0][3] - vp[0][1],
                           vp[1][3] - vp[1][1],
                           vp[2][3] - vp[2][1],
                           vp[3][3] - vp[3][1]);

        planes[4] = float4(vp[0][2] + vp[0][2],
                           vp[1][2] + vp[1][2],
                           vp[2][2] + vp[2][2],
                           vp[3][3] + vp[3][2]);

        planes[5] = float4(vp[0][3] - vp[0][2],
                           vp[1][3] - vp[1][2],
                           vp[2][3] - vp[2][2],
                           vp[3][3] - vp[3][2]);

        for (int i = 0; i < 6; i++)
        {
            planes[i] /= length(planes[i].xyz);
        }
    }

    public static Matrix4x4 ViewMatrix(this Camera cam)
    {
        return cam.worldToCameraMatrix;
    }

    public static float4x4  VPMatrix(this    Camera cam) => cam.projectionMatrix * cam.ViewMatrix();
    public static Matrix4x4 VPMatrix4x4(this Camera cam) => cam.projectionMatrix * cam.ViewMatrix();

    public static Bounds CalculateBounds(Mesh mesh, in float4x4 trs)
    {
        Bounds bounds = mesh.bounds;
        TransformBounds(bounds, trs, out var ubounds);

        return ubounds;
    }

    [BurstCompile]
    public static void ComputeViewMatrixForAABB(in float3 minPosition, in float3 maxPosition, out float4x4 view)
    {
        float3 center = (minPosition + maxPosition) / 2.0f;
        float3 boxTop = float3(center.x, maxPosition.y, center.z);

        float4x4 lookAt = float4x4(1, 0, 0,  boxTop.x,
                                   0, 0, -1, boxTop.y,
                                   0, 1, 0,  boxTop.z,
                                   0, 0, 0,  1);

        float4x4 lookAtInverse = fastinverse(lookAt); // affine matrix
        float4x4 cameraScale   = Unity.Mathematics.float4x4.Scale(1, 1, -1);

        view = mul(cameraScale, lookAtInverse);
    }

    [BurstCompile]
    public static void ComputeProjMatrixForAABB(in float3 boundsSize, out float4x4 proj)
    {
        float3 size = boundsSize * 0.5f;

        // var ortho = float4x4.OrthoOffCenter(-size.x, size.x, -size.z, size.z, 0, boundsSize.y * 2);
        proj = Unity.Mathematics.float4x4.OrthoOffCenter(-size.x, size.x, -size.z, size.z, 0, size.y * 2);
    }

    [BurstCompile]
    public static void ComputeViewProjMatricesForAABB(in float3 minPosition, in float3 maxPosition,
                                                      out float4x4 view, out float4x4 proj)
    {
        ComputeViewMatrixForAABB(minPosition, maxPosition,  out view);
        ComputeProjMatrixForAABB(maxPosition - minPosition, out proj);
    }

    private delegate void transformBoundsDelegate(in UBounds meshBounds, in float4x4 tm, out UBounds result);

    /// <summary>
    /// Transforms bounds from some space to another using <paramref name="tm"/>
    /// </summary>
    // [BurstCompile(FloatMode = FloatMode.Fast), MonoPInvokeCallback(typeof(transformBoundsDelegate))]
    // public static void TransformBounds(in UBounds meshBounds, in float4x4 tm, out UBounds result)
    // {
    //     float3 b0 = meshBounds.min;
    //     float3 b1 = meshBounds.max;
    // 
    //     float4 p0 = mul(tm, float4(b0, 1));
    //     float4 p1 = mul(tm, float4(b1, 1));
    //     float4 p2 = mul(tm, float4(b0.x, b0.y, b1.z, 1));
    //     float4 p3 = mul(tm, float4(b0.x, b1.y, b0.z, 1));
    //     float4 p4 = mul(tm, float4(b1.x, b0.y, b0.z, 1));
    //     float4 p5 = mul(tm, float4(b0.x, b1.y, b1.z, 1));
    //     float4 p6 = mul(tm, float4(b1.x, b0.y, b1.z, 1));
    //     float4 p7 = mul(tm, float4(b1.x, b1.y, b0.z, 1));
    // 
    //     float4 minPoint = min(min(min(p0, p1), min(p2, p3)), min(min(p4, p5), min(p6, p7)));
    //     float4 maxPoint = max(max(max(p0, p1), max(p2, p3)), max(max(p4, p5), max(p6, p7)));
    // 
    //     result = new UBounds()
    //     {
    //         min = minPoint.xyz,
    //         max = maxPoint.xyz
    //     };
    // }

    // https://zeux.io/2010/10/17/aabb-from-obb-with-component-wise-abs/
    [BurstCompile(FloatMode = FloatMode.Fast), AOT.MonoPInvokeCallback(typeof(transformBoundsDelegate))]
    public static void TransformBounds(in UBounds meshBounds, in float4x4 tm, out UBounds result)
    {
        float3 center  = meshBounds.center;
        float3 extents = meshBounds.extents;

        float4x4 tmAbs      = float4x4(abs(tm.c0), abs(tm.c1), abs(tm.c2), abs(tm.c3));
        float3   newCenter  = mul(tm,    float4(center.xyz,  1)).xyz;
        float3   newExtents = mul(tmAbs, float4(extents.xyz, 0)).xyz;

        result = new(newCenter - newExtents, newCenter + newExtents);
    }

    public static void ReadPixels(this Texture2D texture, RenderTexture rt, bool recalculateMips = false)
    {
        RenderTexture currentActiveRT = RenderTexture.active;
        RenderTexture.active = rt;
        texture.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0, recalculateMips);

        RenderTexture.active = currentActiveRT;
    }

    public static NativeArray<float3> GetBoundPoints(this in UBounds bounds, Allocator arrayAllocator)
    {
        NativeArray<float3> points = new NativeArray<float3>(8, arrayAllocator, options: NativeArrayOptions.UninitializedMemory);

        points[0] = bounds.min;
        points[1] = bounds.max;
        points[2] = float3(points[0].x, points[0].y, points[1].z);
        points[3] = float3(points[0].x, points[1].y, points[0].z);
        points[4] = float3(points[1].x, points[0].y, points[0].z);
        points[5] = float3(points[0].x, points[1].y, points[1].z);
        points[6] = float3(points[1].x, points[0].y, points[1].z);
        points[7] = float3(points[1].x, points[1].y, points[0].z);

        return points;
    }

    public static void WriteBoundsPoints(this in UBounds bounds, in Span<float3> points)
    {
        points[0] = bounds.min;
        points[1] = bounds.max;
        points[2] = float3(points[0].x, points[0].y, points[1].z);
        points[3] = float3(points[0].x, points[1].y, points[0].z);
        points[4] = float3(points[1].x, points[0].y, points[0].z);
        points[5] = float3(points[0].x, points[1].y, points[1].z);
        points[6] = float3(points[1].x, points[0].y, points[1].z);
        points[7] = float3(points[1].x, points[1].y, points[0].z);
    }

    public static float LinearZ(float z, float4 zBufferParams, float isOrtho)
    {
        float isPers = 1.0f - isOrtho;
        z *= zBufferParams.x;
        return (1.0f - isOrtho * z) / (isPers * z + zBufferParams.y);
    }
}
}