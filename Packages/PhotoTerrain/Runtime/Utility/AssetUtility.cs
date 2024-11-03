#if UNITY_EDITOR
using System;
using System.IO;
using Hollow.Extensions;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HollowEditor.TerrainSystem
{
public static class AssetUtility
{
    public static string ProjectRelativeToAbsolutePath(string projectRelative)
    {
        // Assert.IsTrue(projectRelative.StartsWith("Assets/", StringComparison.Ordinal), "projectRelative.StartsWith('Assets/')");
        if (projectRelative.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
        {
            var path  = Application.dataPath + '/' + projectRelative["Assets/".Length..];
            return path;
        }
        else if (projectRelative.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFullPath(projectRelative);
        }

        throw new System.ArgumentException($"Invalid project relative path: {projectRelative}", nameof(projectRelative));
    }

    public static string GetSceneDataDirectory(Scene scene, string defaultPath = "Assets")
    {
        var pathToScene = scene.path;

        var isSceneOnDisk = !pathToScene.IsNullOrWhiteSpace();

        // Like with any terrain data such as navigation we're going to create data in a folder with name similar to 
        // scene name, otherwise in Assets folder
        string dataFolder;
        if (isSceneOnDisk)
        {
            string sceneDirectory = Path.GetDirectoryName(pathToScene);
            string sceneName      = scene.name;
            dataFolder = sceneDirectory + '/' + sceneName;
            if (!AssetDatabase.IsValidFolder(dataFolder))
                AssetDatabase.CreateFolder(sceneDirectory, sceneName);
        }
        else
        {
            dataFolder = defaultPath;
        }

        return dataFolder;
    }

    public static string SafeRelativePath(UnityEngine.Object obj, string filename)
    {
        var path = AssetDatabase.GetAssetPath(obj);
        path = Path.GetDirectoryName(path).Replace("\\", "/");

        path = path + '/' + filename;
        path = AssetDatabase.GenerateUniqueAssetPath(path);

        return path;
    }

    public static string SafeRelativeSubPath(UnityEngine.Object obj, string filename)
    {
        var    path = AssetDatabase.GetAssetPath(obj);

        string dir  = Path.GetDirectoryName(path).Replace("\\", "/");
        string name = Path.GetFileNameWithoutExtension(path);

        path = dir + '/' + name;

        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(dir, name);

        path = path + '/' + filename;
        path = AssetDatabase.GenerateUniqueAssetPath(path);

        return path;
    }

    public static void SaveAsset(Scene scene, string assetNameWithExtension, UnityEngine.Object obj)
    {
        string uniquePath = GetSceneDataDirectory(scene) + '/' + assetNameWithExtension;
        uniquePath = AssetDatabase.GenerateUniqueAssetPath(uniquePath);

        AssetDatabase.CreateAsset(obj, uniquePath);
    }

    public static void EnsureFolderExists(string folderPath)
    {
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            string parentFolder = GetParentFolder(folderPath);

            EnsureFolderExists(parentFolder);

            string folderName = Path.GetFileName(folderPath);

            AssetDatabase.CreateFolder(parentFolder, folderName);
        }
    }

    public static string GenerateUniqueDirectoryName(string directoryPath, string postfix)
    {
        string originalPath = directoryPath;
        directoryPath = directoryPath + postfix;

        int i = 1;
        while (AssetDatabase.IsValidFolder(directoryPath))
        {
            directoryPath = originalPath + " " + i + postfix;
            i++;
        }

        return directoryPath;
    }

    // Path.GetDirectoryName replaces '/' with '\\' in some scripting runtime versions,
    // so we have to roll our own.
    public static string GetParentFolder(string assetPath)
    {
        int endIndex = assetPath.LastIndexOf('/');

        return (endIndex > 0) ? assetPath.Substring(0, endIndex) : string.Empty;
    }
}
}
#endif