using System.IO;
using Hollow.Extensions;
using Hollow.TerrainSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TerrainLayer = Hollow.TerrainSystem.TerrainLayer;

namespace HollowEditor.TerrainSystem
{
internal class CreatePhotoTerrainObject
{
    [MenuItem("GameObject/3D Object/Photo Terrain")]
    internal static void CreatePhotoTerrain(MenuCommand cmd)
    {
        EditorApplication.ExecuteMenuItem("GameObject/Create Empty");
        var createdObject = Selection.activeGameObject;

        // Like with any terrain data such as navigation we're going to create data in a folder with name similar to 
        // scene name, otherwise in Assets folder
        string dataFolder = AssetUtility.GetSceneDataDirectory(createdObject.scene, "Assets/_TerrainProjects");
        AssetUtility.EnsureFolderExists(dataFolder);

        string projectFolder = dataFolder + '/' + "Terrain";
        projectFolder = AssetUtility.GenerateUniqueDirectoryName(projectFolder, ".ptproj");

        AssetUtility.EnsureFolderExists(projectFolder);

        string projName             = Path.GetFileNameWithoutExtension(projectFolder);
        string unityTerrainDataName = "UnityTerrainData.asset";
        string photoTerrainDataName = $"{projName}.asset";

        string unityTerrainDataPath = projectFolder + '/' + unityTerrainDataName;
        string photoTerrainDataPath = projectFolder + '/' + photoTerrainDataName;
        string baseLayerDataPath    = projectFolder + '/' + projName + "_BaseLayer.asset";

        var unityTerrainData = new TerrainData();
        var photoTerrainData = ScriptableObject.CreateInstance<AdditionalTerrainData>();
        var baseLayer = ScriptableObject.CreateInstance<TerrainLayer>();

        unityTerrainData.heightmapResolution = 2049;
        unityTerrainData.alphamapResolution  = 16; // This is unused
        unityTerrainData.baseMapResolution   = 16; // This is unused
        unityTerrainData.size                = new(512, 1024, 512);

        photoTerrainData.unityData = unityTerrainData;

        AssetDatabase.CreateAsset(unityTerrainData, unityTerrainDataPath);
        AssetDatabase.CreateAsset(photoTerrainData, photoTerrainDataPath);
        AssetDatabase.CreateAsset(baseLayer,        baseLayerDataPath);

        var unityTerrain         = createdObject.AddComponent<Terrain>();
        var unityTerrainCollider = createdObject.AddComponent<TerrainCollider>();
        var photoTerrain         = createdObject.AddComponent<PhotoTerrain>();

        // Assign terrain data
        unityTerrain        .terrainData = unityTerrainData;
        unityTerrainCollider.terrainData = unityTerrainData;
        photoTerrain.outputData           = photoTerrainData;
        photoTerrain.Material            = PhotoTerrainSettings.Instance.defaultTerrainMaterial;
        photoTerrain.baseLayer              = baseLayer;

        // Don't draw unity terrain, leave it only so that collider works
        unityTerrain.drawHeightmap       = false;
        unityTerrain.drawTreesAndFoliage = false;

        photoTerrain.enabled = false;
        photoTerrain.enabled = true;
        /*

        string unityTerrainDataName = "TerrainData.asset";
        string photoTerrainDataName = "PhotoTerrainData.asset";

        string unityTerrainDataPath = dataFolder + '/' + unityTerrainDataName;
        unityTerrainDataPath = AssetDatabase.GenerateUniqueAssetPath(unityTerrainDataPath);

        string photoTerrainDataPath = dataFolder + '/' + photoTerrainDataName;
        photoTerrainDataPath = AssetDatabase.GenerateUniqueAssetPath(photoTerrainDataPath);

        var unityTerrainData = new TerrainData();
        var photoTerrainData = ScriptableObject.CreateInstance<PhotoTerrainData>();
        // Need to keep unity terrain data to access heightmap so that it works with collider
        photoTerrainData.UnityTerrainData = unityTerrainData;

        // Assign reasonable defaults
        unityTerrainData.heightmapResolution = 2049;
        unityTerrainData.alphamapResolution  = 16; // This is unused
        unityTerrainData.baseMapResolution   = 16; // This is unused
        unityTerrainData.size                = new(512, 1024, 512);

        AssetDatabase.CreateAsset(unityTerrainData, unityTerrainDataPath);
        AssetDatabase.CreateAsset(photoTerrainData, photoTerrainDataPath);

        var unityTerrain         = createdObject.AddComponent<Terrain>();
        var unityTerrainCollider = createdObject.AddComponent<TerrainCollider>();
        var photoTerrain         = createdObject.AddComponent<PhotoTerrain>();

        // Assign terrain data
        unityTerrain        .terrainData = unityTerrainData;
        unityTerrainCollider.terrainData = unityTerrainData;
        photoTerrain.m_PhotoTerrainData = photoTerrainData;
        photoTerrain.Material           = TerrainResources.Instance.DefaultTerrainMaterial;

        // Don't draw unity terrain, leave it only so that collider works
        unityTerrain.drawHeightmap       = false;
        unityTerrain.drawTreesAndFoliage = false;

        photoTerrain.enabled = false;
        photoTerrain.enabled = true;*/
    }
}
}