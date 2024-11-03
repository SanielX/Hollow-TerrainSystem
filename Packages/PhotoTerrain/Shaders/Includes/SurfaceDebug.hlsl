#pragma once

Texture2D PT_DebugWorldMask;
SamplerState samplerPT_DebugWorldMask;
float4x4  PT_DebugWorldToMaskMatrix;

float4 PT_SampleDebugMask(float3 worldPosition, bool clipUV = true)
{
    float4 local = mul(PT_DebugWorldToMaskMatrix, float4(worldPosition, 1));
    float2 uv = local.xz;

    if(clipUV)
    {
        if(any(uv > 1) || any(uv < 0))
            return 0.0f;
    }

    return PT_DebugWorldMask.SampleLevel(samplerPT_DebugWorldMask, uv, 0);
}