using System;
using Hollow.VirtualTexturing;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Serialization;

namespace Hollow.TerrainSystem
{
public class PhotoTerrainSettings : ScriptableObject
{
#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    static void Init()
    {
        EditorApplication.delayCall += () =>
        {
            var asset = AssetDatabase.LoadAssetAtPath<PhotoTerrainSettings>("Assets/Resources/PhotoTerrainSettings.asset");
            if (!asset)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                {
                    AssetDatabase.CreateFolder("Assets", "Resources");
                }

                var instance = ScriptableObject.CreateInstance<PhotoTerrainSettings>();
                instance.isActualSettingsAsset = true;
                AssetDatabase.CreateAsset(instance, "Assets/Resources/PhotoTerrainSettings.asset");
            }
        };
    }
#endif

    // Don't wanna null check all the time at runtime, load at startup instead
    // Used in PhotoTerrainRenderer.Init()
    internal static void RuntimeInit()
    {
        instance = Resources.Load<PhotoTerrainSettings>("PhotoTerrainSettings");
        Assert.IsNotNull(instance);
    }

    private static PhotoTerrainSettings instance;

    public static PhotoTerrainSettings Instance
    {
        get
        {
#if UNITY_EDITOR
            if (!instance)
                instance = Resources.Load<PhotoTerrainSettings>("PhotoTerrainSettings");
#endif
            return instance;
        }
    }

    public static System.Action OnTerrainSettingsChanged;

    public enum CacheTileSize
    {
        x64  = 64,
        x128 = 128,
        x256 = 256,
    }

    public enum VirtualImageSize
    {
        x256  = 256,
        x512  = 512,
        x1024 = 1024,
    }

    [HideInInspector] [SerializeField] bool isActualSettingsAsset;

    [Header("Virtual Texture")] [Tooltip("Size in world units that virtual texture can cover")] [SerializeField, Delayed]
    public int adaptiveVirtualWorldCellCount = 200;

    [Tooltip("When using virtual texture, world is split into cells of this size in meters")] [SerializeField, Delayed]
    public float adaptiveVirtualCellSize = 64;

    public VirtualImageSize adaptiveVirtualImageMaxSize = VirtualImageSize.x512;
    public float[] virtualImageDistances       = { 5f, 15, 50f, 250f };

    [Space]
    public IndirectionTextureSize indirectionTextureSize      = IndirectionTextureSize.x2048;

    [Space] [Min(8), Delayed]
    public int cacheTileCountWide = 32;

    public CacheTileSize          cacheTileSize               = CacheTileSize.x256;
    public VTTileBorder           cacheTileBorderSize         = VTTileBorder.x8;

    [Space] [Delayed, Min(1)]
    public int maxPageRenderedPerFrame = 4;

    [Space] public Material defaultTerrainMaterial;


    void OnValidate()
    {
        if (isActualSettingsAsset)
            OnTerrainSettingsChanged?.Invoke();
    }

    /// <summary>
    /// Downsamples max virtual image size, clamps at 1
    /// </summary>
    public int VirtualImageResolutionAt(float distanceToCamera)
    {
        int  downsample = 0;
        bool valid      = false;
        for (int iDistance = 0; iDistance < virtualImageDistances.Length; iDistance++)
        {
            if (distanceToCamera < virtualImageDistances[iDistance])
            {
                valid = true;
                break;
            }

            downsample++;
        }

        if (!valid)
            return -1;

        return Mathf.Max(1, (int)adaptiveVirtualImageMaxSize >> downsample);
    }
}
}