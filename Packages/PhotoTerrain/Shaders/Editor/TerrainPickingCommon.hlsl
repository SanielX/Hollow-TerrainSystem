#pragma once

#ifndef FLT_MAX 
    #define FLT_MAX  3.402823466e+38 // Maximum representable floating-point number
#endif

struct TerrainPickingResult
{
    float3 Position;
    float3 Normal;
    int    TerrainInstanceID;
};

uint EncodePickingNormal(float3 n)
{
    float3 OutN;

    n /= ( abs( n.x ) + abs( n.y ) + abs( n.z ) );

    OutN.y = n.y *  0.5  + 0.5;
    OutN.x = n.x *  0.5  + OutN.y;
    OutN.y = n.x * -0.5  + OutN.y;

    OutN.z = saturate(n.z*FLT_MAX);
    // return float4(OutN.xy, 0, OutN.z);

    uint o = 0;
    o |= (int(OutN.x * 4096) & 0xFFF);
    o |= (int(OutN.y * 4096) & 0xFFF) << 12;
    o |= (int(OutN.z) & 1) << 25;
    return o;
}

float3 DecodePickingNormal(uint i)
{
    float4 n;
    n.x = (i & 0xFFF) / 4096.0;
    n.y = ((i >> 12) & 0xFFF) / 4096.0;
    n.w = (i >> 25) & 1;
    
    n.z = n.w;
    
    float3 OutN;

    OutN.x = (n.x - n.y);
    OutN.y = (n.x + n.y) - 1.0;
    OutN.z = n.z * 2.0 - 1.0;
    OutN.z = OutN.z * ( 1.0 - abs(OutN.x) - abs(OutN.y));
 
    OutN = normalize( OutN );
    return OutN;
}

#undef FLT_MAX