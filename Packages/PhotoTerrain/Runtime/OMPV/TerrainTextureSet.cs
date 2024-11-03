using UnityEngine;
using UnityEngine.Serialization;

namespace Hollow.TerrainSystem
{
public class TerrainTextureSet : ScriptableObject
{
#if UNITY_EDITOR
    [SerializeField] internal Texture2D albedoPrototype;
#endif

    [SerializeField] internal TerrainTextureSetCollection collection;
    [SerializeField] internal int                         albedoIndex;
    [SerializeField] internal int                         normalIndex;
    [SerializeField] internal int                         maskIndex;
}
}