using System.Collections;
using System.Collections.Generic;
using System.IO;
using Hollow.Extensions;
using UnityEngine;

namespace Hollow.TerrainSystem
{
public enum TerrainMaterialAlbedoFormat
{
    RGB8 = TextureFormat.RGB24,
    DXT1 = TextureFormat.DXT1,
    DXT5 = TextureFormat.DXT5,
    BC7  = TextureFormat.BC7
}

public enum TerrainMaterialNormalFormat
{
    BC5                                = TextureFormat.BC5,
    [InspectorName("DXTnm|BC3")] DXTnm = TextureFormat.DXT5
}

public enum TerrainMaterialMaskFormat
{
    DXT5 = TextureFormat.DXT5,
    BC7 = TextureFormat.BC7
}

public enum TerrainMaterialArrayResolution
{
    x64   = 64,
    x128  = 128,
    x256  = 256,
    x512  = 512,
    x1024 = 1024,
    x2048 = 2048,
    x4096 = 4096,
}

[System.Serializable]
public class TerrainMaterial /*: ScriptableObject*/
{
    public Texture2D albedo;
    public Texture2D normal;
    public Texture2D mask;

    public Vector4 scaleOffset      = new(1, 1, 0, 0);
    [Range(0, 1)] public float   smoothness       = 1;
    [Range(0, 1)] public float   metallic         = 1;
    public float   normalStrength   = 1;
    [Range(0, 1)] public float   heightTransition = 0.5f;
    public Vector2 heightRemap      = new(0, 1f);

    public PhysicMaterial physicsMaterial;

    public int GetContentsHash()
    {
        int hash = 17;
        hash = HashHelper.Combine(hash, albedo ? albedo.GetHashCode() : 0);
        hash = HashHelper.Combine(hash, normal ? normal.GetHashCode() : 0);
        hash = HashHelper.Combine(hash, mask ?   mask  .GetHashCode() : 0);
        hash = HashHelper.Combine(hash, scaleOffset.GetHashCode());
        hash = HashHelper.Combine(hash, metallic.GetHashCode());
        hash = HashHelper.Combine(hash, smoothness.GetHashCode());
        hash = HashHelper.Combine(hash, normalStrength.GetHashCode());
        hash = HashHelper.Combine(hash, heightTransition.GetHashCode());
        hash = HashHelper.Combine(hash, heightRemap.GetHashCode());
        return hash;
    }
}

// TODO: Integrate with texture set
[CreateAssetMenu]
public class TerrainMaterialPalette : ScriptableObject
{
    private DynamicTexture2DArray albedoArray;
    private DynamicTexture2DArray normalArray;
    private DynamicTexture2DArray maskArray;

    [SerializeField, HideInInspector] DynamicTexture2DArray albedoArraySerialized;
    [SerializeField, HideInInspector] DynamicTexture2DArray normalArraySerialized;
    [SerializeField, HideInInspector] DynamicTexture2DArray maskArraySerialized;

    public Texture2D ompvNoise;
    [Min(0.0001f)] public float     ompvNoiseSize;
    [Min(0.0001f)] public float     ompvNoiseStrength;
    [Space] 
    public TerrainMaterialArrayResolution albedoResolution = TerrainMaterialArrayResolution.x1024;
    public TerrainMaterialAlbedoFormat    albedoFormat     = TerrainMaterialAlbedoFormat.DXT5;
    [Space] 
    public TerrainMaterialArrayResolution normalResolution = TerrainMaterialArrayResolution.x1024;
    public TerrainMaterialNormalFormat    normalFormat     = TerrainMaterialNormalFormat.BC5;
    [Space] 
    public TerrainMaterialArrayResolution maskResolution = TerrainMaterialArrayResolution.x1024;
    public TerrainMaterialMaskFormat      maskFormat     = TerrainMaterialMaskFormat.BC7;
    [Space] public TerrainMaterial[] materials;

    public TerrainTextureSet textureSet;

    private GraphicsBuffer              gpuMaterialsBuffer;
    private OMPVTerrainMaterialGPU[] gpuMaterials;
    private int lastRefreshHash;

    internal GraphicsBuffer MaterialsBuffer => gpuMaterialsBuffer;
    internal Texture2DArray AlbedoArray     => albedoArray.texture2DArray;
    internal Texture2DArray NormalArray     => normalArray.texture2DArray;
    internal Texture2DArray MaskArray       => maskArray.texture2DArray;

    void OnEnable()
    {
        // In editor we dynamically create arrays, at runtime, use already created ones
#if !UNITY_EDITOR
            albedoArray = albedoArraySerialized; 
            normalArray = normalArraySerialized;
            maskArray   = maskArraySerialized;
#endif
    }

    void OnDisable()
    {
        albedoArray?.Dispose();
        normalArray?.Dispose();
        maskArray?.Dispose();
        ObjectUtility.SafeDispose(ref gpuMaterialsBuffer);
    }

    int GetMaterialsHash()
    {
        int hash = 17;
        for (int i = 0; i < materials.Length; i++)
        {
            hash = HashHelper.Combine(hash, materials[i].GetContentsHash());
        }

        return hash;
    }

    internal unsafe bool Refresh()
    {
        bool needToRefresh = false;

#if UNITY_EDITOR
        static void ensureArray(ref DynamicTexture2DArray array, ref bool needRefresh, TextureFormat format, int resolution, int depth, bool sRGB)
        {
            if (array is null || !array.texture2DArray ||
                array.texture2DArray.format != format  ||
                array.texture2DArray.depth != depth    ||
                array.texture2DArray.width != resolution)
            {
                array?.Dispose();
                array = DynamicTexture2DArray.CreateInstance(resolution, depth, format, sRGB);
                needRefresh = true;
            }
        }

        ensureArray(ref albedoArray, ref needToRefresh, (TextureFormat)albedoFormat, (int)albedoResolution, materials.Length, true);
        ensureArray(ref normalArray, ref needToRefresh, (TextureFormat)normalFormat, (int)normalResolution, materials.Length, false);
        ensureArray(ref maskArray,   ref needToRefresh, (TextureFormat)maskFormat,   (int)maskResolution,   materials.Length, false);
#endif

        int newMaterialHash = GetMaterialsHash();
        needToRefresh |= newMaterialHash != lastRefreshHash;

        lastRefreshHash = newMaterialHash;

        if (gpuMaterialsBuffer.IsNullOrInvalid() || gpuMaterialsBuffer.count < materials.Length)
        {
            gpuMaterialsBuffer?.Dispose();
            gpuMaterialsBuffer = new(GraphicsBuffer.Target.Structured, materials.Length, sizeof(OMPVTerrainMaterialGPU));
            needToRefresh = true;
        }

        if (needToRefresh)
        {
#if UNITY_EDITOR
            for (int i = 0; i < materials.Length; i++)
            {
                albedoArray.SetTexture(i, materials[i].albedo);
                maskArray  .SetTexture(i, materials[i].mask);
                normalArray.SetTexture(i, materials[i].normal);
            }
#endif
            if (gpuMaterials is null || gpuMaterials.Length < materials.Length)
                gpuMaterials = new OMPVTerrainMaterialGPU[materials.Length];

            for (int i = 0; i < materials.Length; i++)
            {
                var mat = materials[i];

                OMPVTerrainMaterialGPU gpuMaterial = default;
                gpuMaterial.albedoIndex      = albedoArray.IndexOf(mat.albedo);
                gpuMaterial.normalIndex      = normalArray.IndexOf(mat.normal);
                gpuMaterial.maskIndex        = maskArray  .IndexOf(mat.mask);
                gpuMaterial.smoothness       = mat.smoothness;
                gpuMaterial.metallic         = mat.metallic;
                gpuMaterial.scaleOffset      = mat.scaleOffset;
                gpuMaterial.heightTransition = mat.heightTransition;
                gpuMaterial.normalStrength   = mat.normalStrength;
                gpuMaterial.heightRemap      = mat.heightRemap;

                gpuMaterials[i] = gpuMaterial;
            }

            gpuMaterialsBuffer.SetData(gpuMaterials, 0, 0, materials.Length);
        }

        return needToRefresh;
    }

#if UNITY_EDITOR
    [ContextMenu("Create Texture Array")]
    void CreateTextureArray()
    {
        Refresh();

        var pathToSelf = UnityEditor.AssetDatabase.GetAssetPath(this);
        var dir = Path.GetDirectoryName(pathToSelf);
        {
            var path = dir + "/Albedo_Array.asset";
            path = UnityEditor.AssetDatabase.GenerateUniqueAssetPath(path);
            albedoArray.texture2DArray.hideFlags = HideFlags.None;
            UnityEditor.AssetDatabase.CreateAsset(albedoArray.texture2DArray, path);
            albedoArraySerialized = albedoArray;
        }
        {
            var path = dir + "/Normal_Array.asset";
            path = UnityEditor.AssetDatabase.GenerateUniqueAssetPath(path);
            normalArray.texture2DArray.hideFlags = HideFlags.None;
            UnityEditor.AssetDatabase.CreateAsset(normalArray.texture2DArray, path);
            normalArraySerialized = normalArray;
        }
        {
            var path = dir + "/Mask_Array.asset";
            path = UnityEditor.AssetDatabase.GenerateUniqueAssetPath(path);
            maskArray.texture2DArray.hideFlags = HideFlags.None;
            UnityEditor.AssetDatabase.CreateAsset(maskArray.texture2DArray, path);
            maskArraySerialized = maskArray;
        }
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
}