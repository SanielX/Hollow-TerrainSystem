using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace HollowEditor.TerrainSystem
{
[StructLayout(LayoutKind.Sequential)]
public struct TerrainBrushPresetGPU
{
    public float4 maskWeights;
    public float4 maskScaleOffset;

    public float2 size;
    public float2 flip;

    public float maskWorldSpace;
    public float maskBrightness;
    public float maskContrast;

    public float radiusInner;
    public float radiusOuter;

    public float opacity;
}

[CreateAssetMenu, System.Serializable]
public sealed class TerrainBrushPreset : ScriptableObject
{
    [Min(0)]              [SerializeField] internal float size;

    [SerializeField] internal bool  flipX;
    [SerializeField] internal bool  flipY;

    [Range(-1, 1f)]       [SerializeField] internal float sizeRatio;
    [Range(0, 1)]         [SerializeField] internal float opacity;
    [Range(0, 360)]       [SerializeField] internal float rotate;

    [Range(0f, 1f)]       [SerializeField] internal float radius;
    [Range(0, 1)]         [SerializeField] internal float hardness;

    [Range(0f, 180f)]     [SerializeField] internal float jitter;

    [SerializeField] internal Texture2D mask;
    [SerializeField] internal bool      maskIsWorldSpace;
    [SerializeField] internal Vector4   maskScaleOffset;
    [Range(0.001f, 4f)] [SerializeField] internal float     maskBrightness;
    [Range(0.001f, 4f)] [SerializeField] internal float     maskContrast;

    public Texture2D Mask => mask ? mask : Texture2D.whiteTexture;
    public Vector2   Size => new(size, size);

    public static TerrainBrushPreset CreateDefaultBrush()
    {
        var preset = CreateInstance<TerrainBrushPreset>();
        preset.size     = 10f;
        preset.radius   = .0f;
        preset.hardness = 0.0f;
        preset.opacity  = 1.0f;
        preset.jitter   = 0;
        preset.maskScaleOffset = new(1, 1, 0, 0);

        return preset;
    }

    public void WriteGPUData(ref TerrainBrushPresetGPU gpuBrushConstants, float translatedOpacity = float.NaN)
    {
        const float unit_circle_radius = 0.707106781186547f; // sqrt(0.5), I don't think this version of C# supports comptime
        float radius = Mathf.Lerp(0.49f, unit_circle_radius, this.radius);

        if (mask)
            gpuBrushConstants.maskWeights = GraphicsFormatUtility.IsAlphaOnlyFormat(Mask.graphicsFormat) ? new(0, 0, 0, 1) : new(1, 0, 0, 0);
        else
            gpuBrushConstants.maskWeights = default;

        gpuBrushConstants.radiusOuter = radius;
        gpuBrushConstants.radiusInner = radius * (Mathf.Clamp01(hardness));
        gpuBrushConstants.maskScaleOffset = maskScaleOffset;
        gpuBrushConstants.maskWorldSpace  = maskIsWorldSpace ? 1 : 0;
        gpuBrushConstants.flip = new(flipX ? 1 : 0, flipY ? 1 : 0);
        gpuBrushConstants.maskContrast   = maskContrast;
        gpuBrushConstants.maskBrightness = maskBrightness;
        gpuBrushConstants.opacity = float.IsNaN(translatedOpacity) ? this.opacity : translatedOpacity;
        gpuBrushConstants.size    = Size;
    }

    private static Material _brushBlitMaterial;

    private static Material brushBlitMaterial
    {
        get
        {
            if (!_brushBlitMaterial)
                _brushBlitMaterial = new(Shader.Find("Hidden/PhotoTerrain/BrushPreviewBlit"));
            return _brushBlitMaterial;
        }
    }

    public void DrawBrush(CommandBuffer cmd, RenderTargetIdentifier rt)
    {
        cmd.Blit(Texture2D.whiteTexture, rt, brushBlitMaterial);
    }
}
}