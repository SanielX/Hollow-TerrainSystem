using UnityEngine;
using UnityEngine.Rendering;

namespace Hollow.TerrainSystem
{
public class SphericalHarmonicsUtility
{
    // Source: https://github.com/keijiro/LightProbeUtility/blob/master/Assets/LightProbeUtility.cs
    private static int[] _idSHA =
    {
        Shader.PropertyToID("unity_SHAr"),
        Shader.PropertyToID("unity_SHAg"),
        Shader.PropertyToID("unity_SHAb")
    };

    private static int[] _idSHB =
    {
        Shader.PropertyToID("unity_SHBr"),
        Shader.PropertyToID("unity_SHBg"),
        Shader.PropertyToID("unity_SHBb")
    };

    private static int _idSHC = Shader.PropertyToID("unity_SHC");

    public static void SetSHCoefficients(Vector3 position, MaterialPropertyBlock properties)
    {
        SetSHCoefficients(position, properties, _idSHA, _idSHB, _idSHC);
    }

    // Set SH coefficients to MaterialPropertyBlock
    public static void SetSHCoefficients(Vector3 position, MaterialPropertyBlock properties, int[] idSHA, int[] idSHB, int idSHC)
    {
        SphericalHarmonicsL2 sh;
        LightProbes.GetInterpolatedProbe(position, null, out sh);

        // Constant + Linear
        for (var i = 0; i < 3; i++)
            properties.SetVector(idSHA[i], new Vector4(
                                     sh[i, 3], sh[i, 1], sh[i, 2], sh[i, 0] - sh[i, 6]
                                 ));

        // Quadratic polynomials
        for (var i = 0; i < 3; i++)
            properties.SetVector(idSHB[i], new Vector4(
                                     sh[i, 4], sh[i, 6], sh[i, 5] * 3, sh[i, 7]
                                 ));

        // Final quadratic polynomial
        properties.SetVector(idSHC, new Vector4(
                                 sh[0, 8], sh[2, 8], sh[1, 8], 1
                             ));
    }
}
}