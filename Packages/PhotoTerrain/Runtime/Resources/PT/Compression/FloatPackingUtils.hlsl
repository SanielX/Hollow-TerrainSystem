#pragma once

#define FLOAT_1_MINUS_EPS (0.999999940395355224609375)

float mod(float a, float b) { return fmod(a,b); }

float max3(float a, float b, float c)
{
    return max(max(a, b), c);
}

uint packUnorm4x8(float4 v) 
{
    v = saturate(v);
    uint4 a = uint4((uint)((v.x * 255)), (uint)((v.y * 255)), (uint)((v.z * 255)), (uint)((v.w * 255)));

    return uint( (a.x << 24)
               | (a.y << 16)
               | (a.z << 8 )
               | (a.w << 0 ) ); 
}

float4 unpackUnorm4x8(uint a) 
{
    float4 v = (float4)(uint4( (a & 0xFF000000u) >> 24
                             , (a & 0x00FF0000u) >> 16
                             , (a & 0x0000FF00u) >> 8
                             , (a & 0x000000FFu) >> 0   )); 

    return saturate(v / 255);
}

float4 rUnpackUnorm4x8(uint a) 
{
    float4 v = (float4)(uint4( (a & 0x000000FFu) >> 0
                             , (a & 0x0000FF00u) >> 8
                             , (a & 0x00FF0000u) >> 16
                             , (a & 0xFF000000u) >> 24   )); 

    return saturate(v / 255);
}

float PackFloatInt(float f, uint i, uint numBitI, uint numBitTarget)
{
    // Constant optimize by compiler
    float precision = float(1 << numBitTarget);
    float maxi = float(1 << numBitI);
    float precisionMinusOne = precision - 1.0;
    float t1 = ((precision / maxi) - 1.0) / precisionMinusOne;
    float t2 = (precision / maxi) / precisionMinusOne;

    // Code
    return t1 * f + t2 * float(i);
}

void UnpackFloatInt(float val, uint numBitI, uint numBitTarget, out float f, out uint i)
{
    // Constant optimize by compiler
    float precision = float(1 << numBitTarget);
    float maxi = float(1 << numBitI);
    float precisionMinusOne = precision - 1.0;
    float t1 = ((precision / maxi) - 1.0) / precisionMinusOne;
    float t2 = (precision / maxi) / precisionMinusOne;

    // Code
    // extract integer part
    // + rcp(precisionMinusOne) to deal with precision issue
    i = int((val / t2) + rcp(precisionMinusOne));
    // Now that we have i, solve formula in PackFloatInt for f
    //f = (val - t2 * float(i)) / t1 => convert in mads form
    f = saturate((-t2 * float(i) + val) / t1); // Saturate in case of precision issue
}

inline float float_encode3_16_8_8(in float3 color)
{
    int rgb = ((int)(color.x * 512.0) << 16) | ((int)(color.y * 256.0) << 8) | 
    ((int)(color.z * 256.0));
    return asfloat(rgb);
}

inline float3 float_decode3_16_8_8(float value)
{
    int rgb = asint (value);
    float r = (float)(((rgb & 0xffff0000) >> 16) / 512.0);
    float g = (float)(((rgb & 0x0000ff00) >> 8) / 256.0);
    float b = (float)(((rgb & 0x000000ff)) / 256.0);
    return float3(r, g, b);
}

inline float float_encode2(float2 color)
{
    int rg = ((int)(color.x * 512.0) << 16) | ((int)(color.y * 512.0));
    return asfloat(rg);
}

inline float2 float_decode2(float value)
{
    int rg = asint (value);
    float r = (float)(((rg & 0xffff0000) >> 16) / 512.0);
    float g = (float)(((rg & 0x0000ffff)) / 512.0);
    return float2(r, g);
}

inline float float_encode4(float4 color)
{
    int rgba = ((int)(color.x * 255.0) << 24) | ((int)(color.y * 255.0) << 16) |
    ((int)(color.z * 255.0) << 8) | ((int)(color.w * 255.0));
    return asfloat(rgba);
}

inline float4 float_decode4(float value)
{
    int rgba = asint (value);
    float r = (float)(((rgba & 0xff000000) >> 24) / 255.0);
    float g = (float)(((rgba & 0x00ff0000) >> 16) / 255.0);
    float b = (float)(((rgba & 0x0000ff00) >> 8)  / 255.0);
    float a = (float)( (rgba & 0x000000ff) / 255.0);
    return float4(r, g, b, a);
}

float2 encode_normal (float3 n, float3 view)
{
    half2 enc = normalize(n.xy) * (sqrt(-n.z*0.5+0.5));
    enc = enc*0.5+0.5;
    return enc;
}

float3 decode_normal (float2 enc, float3 view)
{
    half4 nn = half4(enc.x * 2.0, enc.y * 2.0, 0, 0) + half4(-1,-1,1,-1);
    half l = dot(nn.xyz,-nn.xyw);
    nn.z = l;
    nn.xy *= sqrt(l);
    return nn.xyz * 2 + half3(0,0,-1);
}