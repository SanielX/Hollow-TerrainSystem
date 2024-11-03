#pragma once
struct OMPVTerrainMaterial
{
    int AlbedoIndex;
    int NormalIndex;
    int MaskIndex;
    float __pad;
        
    float4 ScaleOffset;
        
    float Smoothness;
    float Metallic;
    float NormalStrength;
    float HeightTransition;

    float2 HeightRemap;
};

struct OMPVSample
{
    float4 albedo;
    float3 tangentNormal;
    float  smoothness;
    float  metallic;
    float  height;
};

struct OMPVSplatSample
{
    uint  backgroundLayer;
    uint  foregroundLayer;
    float blend;
};

float EncodeOMPVSplatSample(OMPVSplatSample sample)
{
    uint r = 0;
    r |= (sample.backgroundLayer & 0x1F);
    r |= (sample.foregroundLayer & 0x1F) << 5;
    r |= (int(saturate(sample.blend) * 0x3F) & 0x3F) << 10;

    return float(r) / 65535.0;
}

OMPVSplatSample DecodeOMPVSplatSample(float value)
{
    OMPVSplatSample o;
    uint v = value * 65535.0;

    o.backgroundLayer = (v     )  & 0x1F;
    o.foregroundLayer = (v >> 5)  & 0x1F;
    o.blend           = ((v >> 10) & 0x3F) / float(0x3F);

    return o;
}

OMPVSample OMPVSampleDefault() { return (OMPVSample)0; }

void sortOMPVIndices(inout int indices[4], inout float alphas[4])
{
    int j;
    
    for(int i = 1; i < 4; i++)
    {
        int value = indices[i];
        float alphaValue = alphas[i];

        for (j = i - 1; j >= 0 && indices[j] > value; j--)
        {
            indices[j + 1] = indices[j];
            alphas[j + 1]  = alphas[j];
        }
        
        indices[j + 1] = value;
        alphas[j+1] = alphaValue;
    }
}

// https://bgolus.medium.com/normal-mapping-for-a-triplanar-shader-10bf39dca05a#ddd9
// https://blog.selfshadow.com/publications/blending-in-detail/

/*
// Whiteout blend
// Triplanar uvs
float2 uvX = i.worldPos.zy; // x facing plane
float2 uvY = i.worldPos.xz; // y facing plane
float2 uvZ = i.worldPos.xy; // z facing plane
// Tangent space normal maps
half3 tnormalX = UnpackNormal(tex2D(_BumpMap, uvX));
half3 tnormalY = UnpackNormal(tex2D(_BumpMap, uvY));
half3 tnormalZ = UnpackNormal(tex2D(_BumpMap, uvZ));
// Swizzle world normals into tangent space and apply Whiteout blend
tnormalX = half3(
// worldNormal.zyx
    tnormalX.xy + i.worldNormal.zy,
    abs(tnormalX.z) * i.worldNormal.x
    );
    // worldNormal.xzy
tnormalY = half3(
    tnormalY.xy + i.worldNormal.xz,
    abs(tnormalY.z) * i.worldNormal.y
    );
tnormalZ = half3(
// worldNormal.xyz, apparently
    tnormalZ.xy + i.worldNormal.xy,
    abs(tnormalZ.z) * i.worldNormal.z
    );
// Swizzle tangent normals to match world orientation and triblend
half3 worldNormal = normalize(
    tnormalX.zyx * blend.x +
    tnormalY.xzy * blend.y +
    tnormalZ.xyz * blend.z
    );

    To sample OMPV we need to do triplanar
 */

// Assuming 2 tangent space normals n1 and n2 in [-1; 1],
// blends them together while preserving detail
float3 OMPVNormalBlend(float3 n1, float3 n2)
{
    return float3(n1.xy + n2.xy, abs(n1.z) * n2.z);
}