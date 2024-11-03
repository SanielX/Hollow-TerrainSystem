using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Hollow.TerrainSystem
{
[ExecuteAlways]
public class TerrainDecal : MonoBehaviour
{
    internal Matrix4x4 transformMatrix;
    internal UBounds   bounds;

    public Material material;

    public void Refresh()
    {
        transformMatrix = transform.localToWorldMatrix;
        RenderUtility.TransformBounds(new UBounds(-0.5f, 0.5f), transformMatrix, out bounds);
    }

    void OnEnable()
    {
        PhotoTerrainWorld.RegisterDecal(this);
    }

    void OnDisable()
    {
        PhotoTerrainWorld.UnregisterDecal(this);
    }

    void OnDrawGizmos()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color  = Color.yellow.WithAlpha(0.1f);
        Gizmos.DrawCube(Vector3.zero, Vector3.one);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
    }

    public static bool IsValid(TerrainDecal d) => d && d.material;
}
}