#pragma once

typedef uint  FfxUInt32;
typedef uint2 FfxUInt32x2;

FfxUInt32 FFX_DNSR_Reflections_BitfieldExtract(FfxUInt32 src, FfxUInt32 off, FfxUInt32 bits) {
    FfxUInt32 mask = (1 << bits) - 1;
    return (src >> off) & mask;
}

FfxUInt32 FFX_DNSR_Reflections_BitfieldInsert(FfxUInt32 src, FfxUInt32 ins, FfxUInt32 bits) {
    FfxUInt32 mask = (1 << bits) - 1;
    return (ins & mask) | (src & (~mask));
}

//  LANE TO 8x8 MAPPING
//  ===================
//  00 01 08 09 10 11 18 19
//  02 03 0a 0b 12 13 1a 1b
//  04 05 0c 0d 14 15 1c 1d
//  06 07 0e 0f 16 17 1e 1f
//  20 21 28 29 30 31 38 39
//  22 23 2a 2b 32 33 3a 3b
//  24 25 2c 2d 34 35 3c 3d
//  26 27 2e 2f 36 37 3e 3f
uint2 FFX_DNSR_Reflections_RemapLane8x8(uint lane) {
    return FfxUInt32x2(FFX_DNSR_Reflections_BitfieldInsert(FFX_DNSR_Reflections_BitfieldExtract(lane, 2u, 3u), lane, 1u),
                       FFX_DNSR_Reflections_BitfieldInsert(FFX_DNSR_Reflections_BitfieldExtract(lane, 3u, 3u),
                                                           FFX_DNSR_Reflections_BitfieldExtract(lane, 1u, 2u), 2u));
}

// Assumes (8,8,1) group size
uint3 SwizzleDispatchThreadID(uint3 groupID, uint groupIndex)
{
    uint3 groupThreadID = uint3(FFX_DNSR_Reflections_RemapLane8x8(groupIndex), 0);
    uint3 dispatchID = (groupID * uint3(8,8,1)) + groupThreadID;

    return dispatchID;
}