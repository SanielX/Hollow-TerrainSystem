#pragma once
#include "OMPVCommon.hlsl"
// #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
// #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

#ifndef POINT_CLAMP_SAMPLER
    SamplerState ompv_sampler_point_clamp;
    #define POINT_CLAMP_SAMPLER ompv_sampler_point_clamp
#endif
#ifndef LINEAR_CLAMP_SAMPLER
    SamplerState ompv_sampler_linear_clamp;
    #define LINEAR_CLAMP_SAMPLER ompv_sampler_linear_clamp
#endif

StructuredBuffer<OMPVTerrainMaterial> OMPV_Materials;

#define OMPV_SPLAT_MAX_VALUE (255)

Texture2D    _OMPV_SplatNoise;
SamplerState sampler_OMPV_SplatNoise;
float2    _OMPV_SplatNoiseParams;

#define OMPV_SplatNoiseStrength (_OMPV_SplatNoiseParams.x)
#define OMPV_SplatNoiseSize     (_OMPV_SplatNoiseParams.y) 

Texture2D<float> _OMPV_SplatMap;
float4           _OMPV_SplatMap_TexelSize;

Texture2DArray _OMPV_AlbedoArray;
SamplerState   sampler_OMPV_AlbedoArray;

Texture2DArray _OMPV_NormalArray;
SamplerState   sampler_OMPV_NormalArray;

Texture2DArray _OMPV_MaskArray;
SamplerState   sampler_OMPV_MaskArray;


float3 UnpackNormalAG(float4 packedNormal, float scale = 1.0)
{
    float3 normal;
    normal.xy = packedNormal.ag * 2.0 - 1.0;
    normal.z = max(1.0e-16, sqrt(1.0 - saturate(dot(normal.xy, normal.xy))));

    // must scale after reconstruction of normal.z which also
    // mirrors UnpackNormalRGB(). This does imply normal is not returned
    // as a unit length vector but doesn't need it since it will get normalized after TBN transformation.
    // If we ever need to blend contributions with built-in shaders for URP
    // then we should consider using UnpackDerivativeNormalAG() instead like
    // HDRP does since derivatives do not use renormalization and unlike tangent space
    // normals allow you to blend, accumulate and scale contributions correctly.
    normal.xy *= scale;
    return normal;
}

// Unpack normal as DXT5nm (1, y, 0, x) or BC5 (x, y, 0, 1)
float3 UnpackNormalmapRGorAG(float4 packedNormal, float scale = 1.0)
{
    // Convert to (?, y, 0, x)
    packedNormal.a *= packedNormal.r;
    return UnpackNormalAG(packedNormal, scale);
}

float ompv_unlerp(float a, float b, float x)
{
    return (x - a) / (b - a);
}
float ompv_remap(float a, float b, float c, float d, float x)
{
    return lerp(c, d, ompv_unlerp(a, b, x));
}

OMPVSample SampleOMPVLayer(float2 uv, float2 du, float2 dv, OMPVTerrainMaterial mat)
{
    uv = mat.ScaleOffset.xy * uv + mat.ScaleOffset.zw;
    du *= mat.ScaleOffset.xy;
    dv *= mat.ScaleOffset.xy;
    
    float4 albedoSample = _OMPV_AlbedoArray.SampleGrad(sampler_OMPV_AlbedoArray, float3(uv, mat.AlbedoIndex), du, dv);
    float3 normalSample;

    if (mat.NormalIndex >= 0)
    {
        float4 nrmTexel = _OMPV_NormalArray.SampleGrad(sampler_OMPV_NormalArray, float3(uv, mat.NormalIndex), du, dv);
        normalSample = UnpackNormalmapRGorAG(nrmTexel, mat.NormalStrength);
    }
    else
    {
        normalSample = float3(0, 0, 1);
    }
    
    float4 maskSample;

    if (mat.MaskIndex >= 0)
        maskSample = _OMPV_MaskArray.SampleGrad(sampler_OMPV_MaskArray, float3(uv, mat.MaskIndex), du, dv);
    else
        maskSample = float4(1, 0, 0.5, 1);
    
    //maskSample.b *= 2;

    OMPVSample o = (OMPVSample)0;
    o.albedo        = albedoSample;
    o.tangentNormal = normalSample;

    o.smoothness = maskSample.a * mat.Smoothness;
    o.metallic   = maskSample.r * mat.Metallic;
    //o.height     = saturate(ompv_remap(mat.HeightRemap.x, mat.HeightRemap.y, 0, 1, maskSample.b));
    o.height     = saturate(ompv_remap(0, 1, mat.HeightRemap.x, mat.HeightRemap.y, maskSample.b));
    return o;
}

float OMPV_Grayscale(float3 color)
{
    return 0.21 * color.r + 0.72 * color.g + 0.07 * color.b;
}

static float2 debug_ompvHeightsBlend;

OMPVSample SampleOMPV(float2 uv, float2 du, float2 dv, OMPVSplatSample sample)
{
#if PT_OMPV_RED_MASK
    float mask_alpha = sample.blend;
#endif

    #if PT_OMPV_BACKGROUND_ONLY
        sample.blend = 0;
    #endif
    
    if(sample.blend == 1)
    {
        sample.blend = 0;
        sample.backgroundLayer = sample.foregroundLayer;
    }
    
    OMPVTerrainMaterial mainMat    = OMPV_Materials[sample.backgroundLayer];
    OMPVSample          mainSample = SampleOMPVLayer(uv, du, dv, mainMat);
    
    #if PT_OMPV_ONLY_FOREGROUND
   //     mainSample.albedo *= 0.1;
   //     mainSample.height = 0.5;
    #endif
    
    if (sample.blend > 0)
    {
        float2 weights = float2(1 - sample.blend, sample.blend);

        OMPVTerrainMaterial foregroundMaterial = OMPV_Materials[sample.foregroundLayer];
        OMPVSample          blendSample        = SampleOMPVLayer(uv, du, dv, foregroundMaterial);

        float  transition      = mainMat.HeightTransition;
        float  maxHeight       = max(mainSample.height, blendSample.height);
        
        float2 weightedHeights = float2(mainSample.height, blendSample.height) - maxHeight.xx;
        weightedHeights        = (max(0, weightedHeights + transition) + 1e-6) * weights;
        float sumHeight        = weightedHeights.x + weightedHeights.y;

        float blendAmount = saturate(weights.y / sumHeight);

        mainSample.albedo        = lerp(mainSample.albedo,        blendSample.albedo,        blendAmount);
        mainSample.smoothness    = lerp(mainSample.smoothness,    blendSample.smoothness,    blendAmount);
        mainSample.metallic      = lerp(mainSample.metallic,      blendSample.metallic,      blendAmount);
        mainSample.tangentNormal = lerp(mainSample.tangentNormal, blendSample.tangentNormal, blendAmount);
       // mainSample.albedo = blendSample.height;
    }

    #if PT_OMPV_RED_MASK
        mainSample.albedo.r += mask_alpha * 0.5;
    #endif 

    return mainSample;
}

float4 debug_OmpvAlphas;

OMPVSample SampleOMPVMaterial(float2 splatUV, float2 textureUV, float2 worldUV)
{
    float2 du = ddx(textureUV);
    float2 dv = ddy(textureUV);
    
    float4 noise = (_OMPV_SplatNoise.Sample(sampler_OMPV_SplatNoise, worldUV * OMPV_SplatNoiseSize) - 0.5) * OMPV_SplatNoiseStrength;
    splatUV += noise.xy;
    
    float4 ompvIndicesSample = _OMPV_SplatMap.GatherRed(POINT_CLAMP_SAMPLER, splatUV, 0);
    
    {
        OMPVSplatSample splat = DecodeOMPVSplatSample(ompvIndicesSample[0]);
        OMPVSample sample = SampleOMPV(textureUV, du, dv, splat);
        return sample;
    }
}

OMPVSample SampleOMPVMaterialBackground(float2 splatUV, float2 textureUV, float2 worldUV)
{
    float2 du = ddx(textureUV);
    float2 dv = ddy(textureUV);
    
    float4 noise = (_OMPV_SplatNoise.Sample(sampler_OMPV_SplatNoise, worldUV * OMPV_SplatNoiseSize) - 0.5) * OMPV_SplatNoiseStrength;
    splatUV += noise.xy;
    
    float4 ompvIndicesSample = _OMPV_SplatMap.GatherRed(POINT_CLAMP_SAMPLER, splatUV, 0);
    
    {
        OMPVSplatSample splat = DecodeOMPVSplatSample(ompvIndicesSample[0]);
        splat.blend = 0;
        OMPVSample sample = SampleOMPV(textureUV, du, dv, splat);
        return sample;
    }
}

OMPVSample SampleOMPVMaterialGather(float2 splatUV, float2 textureUV, float2 worldUV)
{
    float2 du = ddx(textureUV);
    float2 dv = ddy(textureUV);
    
    float4 noise = (_OMPV_SplatNoise.Sample(sampler_OMPV_SplatNoise, worldUV * OMPV_SplatNoiseSize) - 0.5) * OMPV_SplatNoiseStrength;
    splatUV += noise.xy;
    
    float4 ompvIndicesSample = _OMPV_SplatMap.GatherRed(POINT_CLAMP_SAMPLER, splatUV, 0);

    bool allSame = true;
    [unroll]
    for(int iSample = 1; iSample < 4; iSample++)
    {
        if(ompvIndicesSample[0] != ompvIndicesSample[iSample])
            allSame = false;
    }
    
    if(allSame)
    {
        OMPVSample sample = SampleOMPV(textureUV, du, dv, DecodeOMPVSplatSample(ompvIndicesSample[0])); 
        return sample;
    }

    OMPVSplatSample splatSamples[4] = {
        DecodeOMPVSplatSample(ompvIndicesSample[0]),
        DecodeOMPVSplatSample(ompvIndicesSample[1]),
        DecodeOMPVSplatSample(ompvIndicesSample[2]),
        DecodeOMPVSplatSample(ompvIndicesSample[3]),
    };

    // https://www.reedbeta.com/blog/texture-gathers-and-coordinate-precision/
    // This trick only works on power of 2 textures btw
    const float pixel_offset = 1.0 / 512.0;

    float2 pixelCoord = splatUV.xy * _OMPV_SplatMap_TexelSize.zw;
    float2 subpixel   = frac(pixelCoord + (-.5 + pixel_offset));
    float4 alphas     = float4((1 - subpixel.x) *      subpixel.y,
                                    subpixel.x  *      subpixel.y,
                                    subpixel.x  * (1 - subpixel.y),
                               (1 - subpixel.x) * (1 - subpixel.y));

    OMPVSample outputSample = OMPVSampleDefault();
    for (int iMat = 0; iMat < 4; ++iMat)
    {
        OMPVSplatSample splat = splatSamples[iMat];

        OMPVSample sample = SampleOMPV(textureUV, du, dv, splat);

        // sample.albedo = float4(iMat == 0, iMat == 1, iMat == 2, 1);
        outputSample.albedo        += sample.albedo        * alphas[iMat];
        outputSample.smoothness    += sample.smoothness    * alphas[iMat];
        outputSample.metallic      += sample.metallic      * alphas[iMat];
        outputSample.tangentNormal += sample.tangentNormal * alphas[iMat];
    }

    return outputSample;
}
