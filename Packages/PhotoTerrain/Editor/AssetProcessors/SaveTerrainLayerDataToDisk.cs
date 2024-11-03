using System;
using Hollow.TerrainSystem;
using UnityEditor;
using UnityEngine;
using TerrainLayer = Hollow.TerrainSystem.TerrainLayer;

namespace HollowEditor.TerrainSystem
{
internal class SaveTerrainLayerDataToDisk : AssetModificationProcessor
{
    private static string[] OnWillSaveAssets(string[] paths)
    {
        for (int i = 0; i < paths.Length; i++)
        {
            if (!paths[i].EndsWith(".asset")) // Don't care about assets that aren't photo terrain ones
                continue;

            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(paths[i]);

            if (!asset)
                continue;

            try
            {
                if (asset is TerrainLayer project)
                    project.SaveToDisk();
                else if (asset is AdditionalTerrainData ad)
                    ad.SaveToDisk();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        return paths;
    }
}
}