using UnityEngine;
using UnityEngine.Serialization;

namespace Hollow.TerrainSystem
{
    public class TerrainTextureSetCollection : ScriptableObject
    {
        [FormerlySerializedAs("m_TextureSets")] [SerializeField] internal TerrainTextureSet[] textureSets;
        [FormerlySerializedAs("m_AlbedoArray")] [SerializeField] internal Texture2DArray      albedoArray;
        [FormerlySerializedAs("m_NormalArray")] [SerializeField] internal Texture2DArray      normalArray;
        [FormerlySerializedAs("m_MaskArray")] [SerializeField] internal Texture2DArray      maskArray;
    }
}