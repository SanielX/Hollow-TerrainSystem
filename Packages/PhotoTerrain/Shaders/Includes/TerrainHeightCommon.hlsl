#pragma once
#define UNITY_HEIGHTMAP_MAX_VALUE (32766.0 / 65535.0)
//#define UNITY_HEIGHTMAP_MAX_VALUE (1.0)

SamplerState sampler_linear_clamp_hm;
SamplerState sampler_point_clamp_hm;

float NormalizeUnityHeightmap(float unityHeight)
{
    return saturate(unityHeight / UNITY_HEIGHTMAP_MAX_VALUE);
}

float HeightToUnityHeightmap(float height)
{
    return saturate(height) * UNITY_HEIGHTMAP_MAX_VALUE;
}

float4 PT_SampleTextureCatmullRom(Texture2D tex, SamplerState s, float4 rtMetrics, float2 uv)
{
    float2 position       = rtMetrics.zw * uv;
    float2 centerPosition = floor(position - 0.5) + 0.5;
    float2 f              = position - centerPosition;
    float2 f2             = f * f;
    float2 f3             = f * f2;


    const float c  = 0.4; //note: [0;1] ( SMAA_FILMIC_REPROJECTION_SHARPNESS / 100.0 )
    float2      w0 = -c * f3 + 2.0 * c * f2 - c * f;
    float2      w1 = (2.0 - c) * f3 - (3.0 - c) * f2 + 1.0;
    float2      w2 = -(2.0 - c) * f3 + (3.0 - 2.0 * c) * f2 + c * f;
    float2      w3 = c * f3 - c * f2;

    float2 w12         = w1 + w2;
    float2 tc12        = rtMetrics.xy * (centerPosition + w2 / w12);
    float3 centerColor = tex.SampleLevel(s, float2(tc12.x, tc12.y), 0).rgb;

    float2 tc0   = rtMetrics.xy * (centerPosition - 1.0);
    float2 tc3   = rtMetrics.xy * (centerPosition + 2.0);
    float4 color = float4(tex.SampleLevel(s, float2(tc12.x, tc0.y), 0).rgb, 1.0) * (w12.x * w0.y) +
                   float4(tex.SampleLevel(s, float2(tc0.x, tc12.y), 0).rgb, 1.0) * (w0.x * w12.y) +
                   float4(centerColor, 1.0) * (w12.x * w12.y) +
                   float4(tex.SampleLevel(s, float2(tc3.x, tc12.y), 0).rgb, 1.0) * (w3.x * w12.y) +
                   float4(tex.SampleLevel(s, float2(tc12.x, tc3.y), 0).rgb, 1.0) * (w12.x * w3.y);
                
    return float4(color.rgb / color.a, 1.0);
}

// https://iquilezles.org/articles/hwinterpolation/
float4 PT_SampleImproved( Texture2D sam, SamplerState s, float2 uv )
{
    uint sw, sh;
    sam.GetDimensions(sw,sh);
    int2 ires = int2(sw,sh);
    float2  fres = float2( ires );
            
    float2 st = uv*fres - 0.5;
    float2 i = floor( st );
    float2 w = frac( st );
            
    float4 a =  sam.Sample(s, (i+float2(0.5,0.5))/fres );
    float4 b =  sam.Sample(s, (i+float2(1.5,0.5))/fres );
    float4 c =  sam.Sample(s, (i+float2(0.5,1.5))/fres );
    float4 d =  sam.Sample(s, (i+float2(1.5,1.5))/fres );
            
    return lerp(lerp(a, b, w.x), lerp(c, d, w.x), w.y);
}

float3 HeightToNormal(Texture2D _HeightMap, SamplerState sampler_HeightMap, float4 _HeightMap_TexelSize, float2 uv, float scale)
{
    
    const float d = 0.5;
                
    float2 du = float2(_HeightMap_TexelSize.x * d, 0);
    float u1 = NormalizeUnityHeightmap(_HeightMap.Sample(sampler_HeightMap, uv - du).r);
    float u2 = NormalizeUnityHeightmap(_HeightMap.Sample(sampler_HeightMap, uv + du).r);
    //	float3 tu = float3(1, u2 - u1, 0);
                
    float2 dv = float2(0, _HeightMap_TexelSize.y * d);
    float v1 = NormalizeUnityHeightmap(_HeightMap.Sample(sampler_HeightMap, uv - dv).r);
    float v2 = NormalizeUnityHeightmap(_HeightMap.Sample(sampler_HeightMap, uv + dv).r);
    //	float3 tv = float3(0, v2 - v1, 1);
                
    //	i.normal = cross(tv, tu);
    float3 normal = float3(u1 - u2, 1, v1 - v2);
    normal.xz *= scale;
    normal = normalize(normal);
    return normal;
    /*const float d = 1;
    scale *= 2;

    float2 du = float2(_HeightMap_TexelSize.x * d, 0);
    #define HIGH_QUALITY_PER_PIXEL_NORMAL 0
    #if HIGH_QUALITY_PER_PIXEL_NORMAL
        float u1 = NormalizeUnityHeightmap(PT_SampleTextureCatmullRom(_HeightMap, sampler_HeightMap, _HeightMap_TexelSize, uv - du).r); //_HeightMap.Sample(sampler_HeightMap, uv - du));
        float u2 = NormalizeUnityHeightmap(PT_SampleTextureCatmullRom(_HeightMap, sampler_HeightMap, _HeightMap_TexelSize, uv + du).r); //_HeightMap.Sample(sampler_HeightMap, uv + du));
        //	float3 tu = float3(1, u2 - u1, 0);
                    
        float2 dv = float2(0, _HeightMap_TexelSize.y * d);
        float v1 = NormalizeUnityHeightmap(PT_SampleTextureCatmullRom(_HeightMap, sampler_HeightMap, _HeightMap_TexelSize, uv - dv).r); //_HeightMap.Sample(sampler_HeightMap, uv - dv));
        float v2 = NormalizeUnityHeightmap(PT_SampleTextureCatmullRom(_HeightMap, sampler_HeightMap, _HeightMap_TexelSize, uv + dv).r); // _HeightMap.Sample(sampler_HeightMap, uv + dv));
    #else
        float u1 = NormalizeUnityHeightmap(textureGood_(_HeightMap, sampler_HeightMap, uv - du).r);
        float u2 = NormalizeUnityHeightmap(textureGood_(_HeightMap, sampler_HeightMap, uv + du).r);
        //	float3 tu = float3(1, u2 - u1, 0);
    
        float2 dv = float2(0, _HeightMap_TexelSize.y * d);
        float  v1 = NormalizeUnityHeightmap(textureGood_(_HeightMap, sampler_HeightMap, uv - dv).r);
        float  v2 = NormalizeUnityHeightmap(textureGood_(_HeightMap, sampler_HeightMap, uv + dv).r);
    #endif
    //	float3 tv = float3(0, v2 - v1, 1);

    //	i.normal = cross(tv, tu);
    float3 normal = float3(u1 - u2, 1, v1 - v2);
    normal.xz *= scale;
    normal = normalize(normal);

    return normal;*/
}

float3 HeightToNormalLevel(Texture2D _HeightMap, SamplerState sampler_HeightMap, float4 _HeightMap_TexelSize, float2 uv, float scale, int level)
{
    const float d = 1;
    scale *= 2.0;

    float2 du = float2(_HeightMap_TexelSize.x * d, 0);
    float  u1 = NormalizeUnityHeightmap(_HeightMap.SampleLevel(sampler_HeightMap, uv - du, level).r);
    float  u2 = NormalizeUnityHeightmap(_HeightMap.SampleLevel(sampler_HeightMap, uv + du, level).r);
    //	float3 tu = float3(1, u2 - u1, 0);

    float2 dv = float2(0, _HeightMap_TexelSize.y * d);
    float  v1 = NormalizeUnityHeightmap(_HeightMap.SampleLevel(sampler_HeightMap, uv - dv, level).r);
    float  v2 = NormalizeUnityHeightmap(_HeightMap.SampleLevel(sampler_HeightMap, uv + dv, level).r);
    //	float3 tv = float3(0, v2 - v1, 1);

    //	i.normal = cross(tv, tu);
    float3 normal = float3(u1 - u2, 1, v1 - v2);
    normal.xz *= scale;
    normal = normalize(normal);

    return normal;
}
