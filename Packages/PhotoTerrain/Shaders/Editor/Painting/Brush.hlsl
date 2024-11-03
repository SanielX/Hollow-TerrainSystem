#pragma once

#define _UNLERP(a, b, x) ((x - a) / (b - a))

SamplerState brush_linear_repeat_sampler;
Texture2D _PT_Brush_Mask;

cbuffer PT_Brush_ContantBuffer
{
    float4 PT_BrushMaskWeights;
    float4 PT_BrushMaskScaleOffset;
    
    float2 PT_BrushSize;
    float2 PT_BrushFlip;

    float PT_BrushIsMaskWorldSpace;
    float PT_BrushMaskBrightness;
    float PT_BrushMaskContrast;
    
    float PT_Brush_RadiusInner;
    float PT_Brush_RadiusOuter;

    float PT_Brush_Weight;
};

float3 PT_Contrast(float3 In, float Contrast)
{
    float midpoint = pow(0.5, 2.2);
    return (In - midpoint) * Contrast + midpoint;
}

// Sample brush weight at uv[0;1]
// coordinates are in brush-space,
// meaning 2d plane where (0;0) are left down and (1;1) top right corners
float SampleBrushNoWeight(float3 worldPosition, float2 uv)
{
    float a = 1.0;

    if(any(PT_BrushMaskWeights) > 0)
    {
        float2 maskUV = PT_BrushIsMaskWorldSpace > 0? worldPosition.xz*0.1 : uv;
        maskUV = maskUV * PT_BrushMaskScaleOffset.xy + PT_BrushMaskScaleOffset.zw;
        float4 maskAlpha = _PT_Brush_Mask.SampleLevel(brush_linear_repeat_sampler, maskUV, 0);
        
        float maskTerm = dot(maskAlpha, PT_BrushMaskWeights);
        maskTerm *= PT_BrushMaskBrightness;
        maskTerm  = PT_Contrast(maskTerm, PT_BrushMaskContrast);

        a *= maskTerm;
    }

    {
        float diststanceToCenter = length(uv-0.5);
        float radiusTerm = _UNLERP(PT_Brush_RadiusInner, PT_Brush_RadiusOuter, diststanceToCenter);
        radiusTerm = 1 - saturate(radiusTerm);
        //radiusTerm = sqrt(radiusTerm);

        a *= radiusTerm;
    }

    return a;
}

// Sample brush weight at uv[0;1]
// coordinates are in brush-space,
// meaning 2d plane where (0;0) are left down and (1;1) top right corners
float SampleBrush(float3 worldPosition, float2 uv)
{
    float a = SampleBrushNoWeight(worldPosition, uv);

    return a * PT_Brush_Weight;
}

void SplatPaintingClip(float brushAlpha)
{
    float threshold = (1.001 - PT_Brush_Weight); 
    clip(brushAlpha - threshold);
}

#include "../PaintingCommon.hlsl"

StructuredBuffer<BrushState> BrushStateBuffer;

struct BrushVertexInput
{
    float4 vertex : POSITION;
    float2 uv     : TEXCOORD0;
};

struct BrushPixelInput
{
    float4 vertex    : SV_Position;
    float2 uv0       : TEXCOORD0;
    float3 worldPosition : TEXCOORD1;
};

#ifndef UNITY_MATRIX_VP
    float4x4 unity_MatrixVP;
    #define UNITY_MATRIX_VP unity_MatrixVP
#endif 

BrushPixelInput BrushVert(BrushVertexInput i)
{
    BrushPixelInput o;
    float4          worldPos = mul(BrushStateBuffer[0].objectToWorld, i.vertex);
    worldPos.y = 0;

    o.vertex = mul(BrushStateBuffer[0].viewProjMatrix[0], worldPos);
    o.uv0    = i.uv;
    o.worldPosition = worldPos;

    return o;
}
#undef _UNLERP