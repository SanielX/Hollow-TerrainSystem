using Hollow.Extensions;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hollow.TerrainSystem
{
public static class TerrainRenderingUtility
{
    public static Matrix4x4 ComputeViewMatrixForOBB(in Vector3 position, in Quaternion rotation, in Vector3 size)
    {
        var     trs                 = Matrix4x4.TRS(position, rotation, size);
        Vector4 localCameraPosition = Vector3.up * 0.5f;
        localCameraPosition.w = 1;

        Vector4 cameraPosition = trs * localCameraPosition;
        Vector3 down           = trs * Vector3.down;
        Vector3 up             = trs * Vector3.up;
        Vector3 forward        = trs * Vector3.forward;
        float   zAngle         = Vector3.SignedAngle(Vector3.forward, forward, Vector3.up);

        Quaternion cameraRotation = Quaternion.LookRotation(down, up);
        cameraRotation.eulerAngles = cameraRotation.eulerAngles.WithZ(zAngle >= 180 ? zAngle : -zAngle);

        var cameraScale = Matrix4x4.Scale(new Vector3(1, 1, -1));

        var cameraTRS = Matrix4x4.TRS(cameraPosition, cameraRotation, Vector3.one);
        var view      = Matrix4x4.Inverse(cameraTRS);

        return cameraScale * view;
    }

    public static Matrix4x4 ComputeProjectionMatrixForAABB(in Vector3 boundsSize)
    {
        Vector3 size = boundsSize * 0.5f;

        // var ortho = float4x4.OrthoOffCenter(-size.x, size.x, -size.z, size.z, 0, boundsSize.y * 2);
        var ortho = Matrix4x4.Ortho(-size.x, size.x, -size.z, size.z, 0, boundsSize.y * 2);
        return ortho;
    }

    public static void SetupTerrainTopDownViewProjection(CommandBuffer cmd, PhotoTerrain layerTransform)
    {
        ComputeTerrainTopDownViewProjectionMatrices(layerTransform, out var view, out var proj);
        cmd.SetViewProjectionMatrices(view, proj);
    }

    public static void ComputeTerrainTopDownViewProjectionMatrices(PhotoTerrain layerTransform, out Matrix4x4 view, out Matrix4x4 proj)
    {
        var bounds = layerTransform.ComputeBounds();
        view   = TerrainRenderingUtility.ComputeViewMatrixForOBB       (bounds.center, Quaternion.identity, bounds.size);
        proj   = TerrainRenderingUtility.ComputeProjectionMatrixForAABB(bounds.size);
    }
}
}