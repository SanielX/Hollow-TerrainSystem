#pragma once

uint packUnorm4x8(float4 v) 
{
    v = saturate(v); // saturate is basically free instruction
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