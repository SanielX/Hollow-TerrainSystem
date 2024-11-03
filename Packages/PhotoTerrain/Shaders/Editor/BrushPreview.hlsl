#pragma once
#include "UnityCG.cginc"
#define UNITY_MATRIX_VP unity_MatrixVP 
#include "PaintingCommon.hlsl"
#include "Painting/Brush.hlsl"

float4 _Color;

float4x4 Inverse(float4x4 m)
{
    float n11 = m[0][0], n12 = m[1][0], n13 = m[2][0], n14 = m[3][0];
    float n21 = m[0][1], n22 = m[1][1], n23 = m[2][1], n24 = m[3][1];
    float n31 = m[0][2], n32 = m[1][2], n33 = m[2][2], n34 = m[3][2];
    float n41 = m[0][3], n42 = m[1][3], n43 = m[2][3], n44 = m[3][3];

    float t11 = n23 * n34 * n42 - n24 * n33 * n42 + n24 * n32 * n43 - n22 * n34 * n43 - n23 * n32 * n44 + n22 * n33 * n44;
    float t12 = n14 * n33 * n42 - n13 * n34 * n42 - n14 * n32 * n43 + n12 * n34 * n43 + n13 * n32 * n44 - n12 * n33 * n44;
    float t13 = n13 * n24 * n42 - n14 * n23 * n42 + n14 * n22 * n43 - n12 * n24 * n43 - n13 * n22 * n44 + n12 * n23 * n44;
    float t14 = n14 * n23 * n32 - n13 * n24 * n32 - n14 * n22 * n33 + n12 * n24 * n33 + n13 * n22 * n34 - n12 * n23 * n34;

    float det  = n11 * t11 + n21 * t12 + n31 * t13 + n41 * t14;
    float idet = 1.0f / det;

    float4x4 ret;

    ret[0][0] = t11 * idet;
    ret[0][1] = (n24 * n33 * n41 - n23 * n34 * n41 - n24 * n31 * n43 + n21 * n34 * n43 + n23 * n31 * n44 - n21 * n33 * n44) * idet;
    ret[0][2] = (n22 * n34 * n41 - n24 * n32 * n41 + n24 * n31 * n42 - n21 * n34 * n42 - n22 * n31 * n44 + n21 * n32 * n44) * idet;
    ret[0][3] = (n23 * n32 * n41 - n22 * n33 * n41 - n23 * n31 * n42 + n21 * n33 * n42 + n22 * n31 * n43 - n21 * n32 * n43) * idet;

    ret[1][0] = t12 * idet;
    ret[1][1] = (n13 * n34 * n41 - n14 * n33 * n41 + n14 * n31 * n43 - n11 * n34 * n43 - n13 * n31 * n44 + n11 * n33 * n44) * idet;
    ret[1][2] = (n14 * n32 * n41 - n12 * n34 * n41 - n14 * n31 * n42 + n11 * n34 * n42 + n12 * n31 * n44 - n11 * n32 * n44) * idet;
    ret[1][3] = (n12 * n33 * n41 - n13 * n32 * n41 + n13 * n31 * n42 - n11 * n33 * n42 - n12 * n31 * n43 + n11 * n32 * n43) * idet;

    ret[2][0] = t13 * idet;
    ret[2][1] = (n14 * n23 * n41 - n13 * n24 * n41 - n14 * n21 * n43 + n11 * n24 * n43 + n13 * n21 * n44 - n11 * n23 * n44) * idet;
    ret[2][2] = (n12 * n24 * n41 - n14 * n22 * n41 + n14 * n21 * n42 - n11 * n24 * n42 - n12 * n21 * n44 + n11 * n22 * n44) * idet;
    ret[2][3] = (n13 * n22 * n41 - n12 * n23 * n41 - n13 * n21 * n42 + n11 * n23 * n42 + n12 * n21 * n43 - n11 * n22 * n43) * idet;

    ret[3][0] = t14 * idet;
    ret[3][1] = (n13 * n24 * n31 - n14 * n23 * n31 + n14 * n21 * n33 - n11 * n24 * n33 - n13 * n21 * n34 + n11 * n23 * n34) * idet;
    ret[3][2] = (n14 * n22 * n31 - n12 * n24 * n31 - n14 * n21 * n32 + n11 * n24 * n32 + n12 * n21 * n34 - n11 * n22 * n34) * idet;
    ret[3][3] = (n12 * n23 * n31 - n13 * n22 * n31 + n13 * n21 * n32 - n11 * n23 * n32 - n12 * n21 * n33 + n11 * n22 * n33) * idet;

    return ret;
}

struct appdata
{
    float4 vertex : POSITION;
    float2 uv : TEXCOORD0;
};

struct v2f
{
    float  isCameraInside : VAR_COLORMULTIPLIER;
    float2 uv : TEXCOORD0;
    float4 vertex : SV_POSITION;

    float4 screenUV : VAR_SCREENUV;

    //float3 ray : VAR_RAY;
    //float3 orientation : VAR_ORIENT;
    //float3 orientationX : VAR_ORIENT_X;
    //float3 orientationZ : VAR_ORIENT_Z;

    float4x4 worldToObject : VAR_OBJTOWORLD;
};

sampler2D _MainTex;
float4    _MainTex_ST;

Texture2D    _CameraDepthTexture;
SamplerState sampler_CameraDepthTexture;

v2f vert(appdata i)
{
    v2f o;

    // We create TRS matrix right here on GPU
    float3 brushSize = BrushStateBuffer[0].brushSize;
    brushSize.y      = 1000.0;

    // WE WILL COMPUTE THIS THING WHICH IS USUALLY PER OBJECT FOR EVERY VERTEX
    float4x4 obj2World = float4x4_TRS(BrushStateBuffer[0].brushPosition, BrushStateBuffer[0].brushRotation, brushSize);

    float4x4 world2obj = Inverse(obj2World);
    float4 localCameraPos = mul(float4(_WorldSpaceCameraPos.xyz, 1), world2obj);
    localCameraPos /= localCameraPos.w;
    
    // CPU can't know if camera is inside the cube or not, determine it per vertex instead!
    bool   isCameraInside = all(localCameraPos.xyz >= -.5) && all(localCameraPos.xyz <= .5);
    o.isCameraInside      = isCameraInside;

    float4 worldPos = mul(obj2World, i.vertex);

    o.vertex   = mul(unity_MatrixVP, float4(worldPos.xyz, 1));
    o.uv       = i.vertex.xz + 0.5;
    o.screenUV = ComputeScreenPos(o.vertex);

    // This should be transpose. Transpose doesn't work. Why?
    o.worldToObject = world2obj;

    // if brush is in invalid state (doesn't hit terrain), then
    // this will divide by 0, meaning o.vertex is NaN, meaning it won't get rendered anywhere
    o.vertex /= BrushStateBuffer[0].isValid;

    return o;
}

float3 positionFromDepth(in float2 uv, in float z, in float4x4 InvVP)
{
    float  x          = uv.x * 2.0f - 1.0f;
    float  y          = (1.0 - uv.y) * 2.0f - 1.0f;
    float4 position_s = float4(x, y, z, 1.0f);
    float4 position_v = mul(InvVP, position_s);
    return position_v.xyz / position_v.w;
}

float3 sobel(float3x3 c)
{    
    float3x3 x = float3x3(1.0, 0.0, -1.0,
                          2.0, 0.0, -2.0,
                          1.0, 0.0, -1.0);

    float3x3 y = float3x3(1.0,  2.0,  1.0,
                          0.0,  0.0,  0.0,
                         -1.0, -2.0, -1.0);
 
    x = x * c;
    y = y * c;
    float cx =  x[0][0] + x[0][2] + 
                x[1][0] + x[1][2] +
                x[2][0] + x[2][2];
 
    float cy =  y[0][0] + y[0][1] +
                y[0][2] + y[2][0] +
                y[2][1] + y[2][2];
             
    float cz =  sqrt(1-(cx*cx+cy*cy));
 
    return float3(cx, cy, cz);
}

float4 frag(v2f i, bool isFacing : SV_IsFrontFace) : SV_Target
{
    float2 screenUV = i.screenUV.xy / i.screenUV.w;

    uint depthW, depthH;
    _CameraDepthTexture.GetDimensions(depthW, depthH);
    float2 maxDerivative = 2.0 / float2(depthW, depthH);

    float  depth = _CameraDepthTexture.SampleLevel(sampler_CameraDepthTexture, screenUV, 0);
    float3 wpos  = positionFromDepth(screenUV, depth, Inverse(unity_MatrixVP));
    float3 opos  = mul(i.worldToObject, float4(wpos, 1)).xyz;

    float2 uv = opos.xz + .5;
    float2 uv_dx = ddx_fine(uv);
    float2 uv_dy = ddy_fine(uv);
    uv_dx = clamp(uv_dx, -maxDerivative, maxDerivative);
    uv_dy = clamp(uv_dy, -maxDerivative, maxDerivative);
    float brushValue = SampleBrushNoWeight(wpos, uv);

    float3x3 s;
    const float threshold = 0.005;
    s[1][1] = brushValue > threshold;
    s[1][2] = SampleBrushNoWeight(wpos, uv + uv_dy) > threshold;
    s[1][0] = SampleBrushNoWeight(wpos, uv - uv_dy) > threshold;
    s[2][1] = SampleBrushNoWeight(wpos, uv + uv_dx) > threshold;
    s[0][1] = SampleBrushNoWeight(wpos, uv - uv_dx) > threshold;
    
    s[2][2] = SampleBrushNoWeight(wpos, uv + uv_dy + uv_dx) > threshold;
    s[0][0] = SampleBrushNoWeight(wpos, uv - uv_dy - uv_dx) > threshold;
    s[0][2] = SampleBrushNoWeight(wpos, uv + uv_dy - uv_dx) > threshold;
    s[2][0] = SampleBrushNoWeight(wpos, uv - uv_dy + uv_dx) > threshold;
    
    float3 ss = sobel(s); 

    int2 pos = i.vertex.xy;
    // float2 dmul = float2(pos.x & 1, pos.y & 1) * -2 + 1.0; 
    // float ddxBrush = ddx_fine(brushValue);
    // float ddyBrush = ddy_fine(brushValue);
    // 
    // float brushToX = brushValue + ddxBrush * dmul.x;
    // float brushToY = brushValue + ddyBrush * dmul.y;
    // if(brushValue < 0.01 && brushValue > 0)
    //     return float4(1, 0, 0, 1);

    float4 color = brushValue.xxxx * _Color;

    clip(abs(opos.xyz) > 0.5 ? -1 : 1);

    // Normally you'd invert culling mode on CPU side
    // but CPU doesn't know where the brush is so we have to do it manually
    if (i.isCameraInside)
    {
        clip(isFacing ? -1 : 1);
    }
    else
    {
        clip(isFacing ? 1 : -1);
    }

    bool outline = pow(abs(ss.x) + abs(ss.y), 10) != 0;
    // if(outline)
    // {
    //     color.rgb = float3(0.8, 0.8, 0);
    //     color.a = 0.95;
    // }
    // else return 0;
    
 // #if QUANTIZED_PAINTING
 //     SplatPaintingClip(brushValue);

 //     color = _Color;
 //     color.a = 0.6;
 // #endif 
    
    color.a = min(color.a, 0.6);
    
    return color;
}
