#pragma once

// C#: BrushGPUState
struct BrushState
{
    float4x4 objectToWorld;
    float4   brushBounds;
    
   // float4x4 projMatrix;
   // float4x4 viewMatrix;
    
    float4   brushRotation;
    float3   brushPosition;
    float3   brushSize;
    float3   brushPickingNormal;
    
    uint     isValid;
    uint     terrainInstanceId;
    
    float4x4 viewProjMatrix[8];
};

struct TerrainTextureRegion
{
    float4 worldBounds;
};

struct TerrainInstancePaintingGlobals
{
    int   instanceID;
    float maxHeight;
};

static float4x4 float4x4_TRS(float3 t, float4 r, float3 s)
{
    float m11 = (1.0f - 2.0f * (r.y * r.y + r.z * r.z)) * s.x;
    float m21 = (r.x * r.y + r.z * r.w) * s.x * 2.0f;
    float m31 = (r.x * r.z - r.y * r.w) * s.x * 2.0f;
    float m41 = 0.0f;
    float m12 = (r.x * r.y - r.z * r.w) * s.y * 2.0f;
    float m22 = (1.0f - 2.0f * (r.x * r.x + r.z * r.z)) * s.y;
    float m32 = (r.y * r.z + r.x * r.w) * s.y * 2.0f;
    float m42 = 0.0f;
    float m13 = (r.x * r.z + r.y * r.w) * s.z * 2.0f;
    float m23 = (r.y * r.z - r.x * r.w) * s.z * 2.0f;
    float m33 = (1.0f - 2.0f * (r.x * r.x + r.y * r.y)) * s.z;
    float m43 = 0.0f;
    float m14 = t.x;
    float m24 = t.y;
    float m34 = t.z;
    float m44 = 1.0f;

    return float4x4(m11, m12, m13, m14, m21, m22, m23, m24, m31, m32, m33, m34, m41, m42, m43, m44);
}

float4x4 float4x4_Ortho(float left, float right, float bottom, float top, float near, float far)
{
    float rcpdx = 1.0f / (right - left);
    float rcpdy = 1.0f / (top - bottom);
    float rcpdz = 1.0f / (far - near);

    return float4x4(
        2.0f * rcpdx,   0.0f,           0.0f,               -(right + left) * rcpdx,
        0.0f,           2.0f * rcpdy,   0.0f,               -(top + bottom) * rcpdy,
        0.0f,           0.0f,          -2.0f * rcpdz,       -(far + near) * rcpdz,
        0.0f,           0.0f,           0.0f,                1.0f
        );
}

float4x4 float4x4_Scale(float x, float y, float z)
{
    return float4x4(x,    0.0f, 0.0f, 0.0f,
                    0.0f, y,    0.0f, 0.0f,
                    0.0f, 0.0f, z,    0.0f,
                    0.0f, 0.0f, 0.0f, 1.0f);
}

float3x3 float4x4_LookRotation(float3 forward, float3 up)
{
    float3 t = normalize(cross(up, forward));
    return float3x3(t, cross(forward, t), forward);
}

float4x4 float4x4_LookAt(float3 eye, float3 target, float3 up)
{
    float3x3 rot = float4x4_LookRotation(normalize(target - eye), up);

    float4x4 mat;
    mat._m00_m10_m20_m30 = float4(rot._m00_m10_m20, 0.0);
    mat._m01_m11_m21_m31 = float4(rot._m01_m11_m21, 0.0);
    mat._m02_m12_m22_m32 = float4(rot._m02_m12_m22, 0.0);
    mat._m03_m13_m23_m33 = float4(eye, 1.0);
    return mat;
}

float4x4 float4x4_Inverse(float4x4 m)
{
    float n11 = m[0][0], n12 = m[1][0], n13 = m[2][0], n14 = m[3][0];
    float n21 = m[0][1], n22 = m[1][1], n23 = m[2][1], n24 = m[3][1];
    float n31 = m[0][2], n32 = m[1][2], n33 = m[2][2], n34 = m[3][2];
    float n41 = m[0][3], n42 = m[1][3], n43 = m[2][3], n44 = m[3][3];

    float t11 = n23 * n34 * n42 - n24 * n33 * n42 + n24 * n32 * n43 - n22 * n34 * n43 - n23 * n32 * n44 + n22 * n33 * n44;
    float t12 = n14 * n33 * n42 - n13 * n34 * n42 - n14 * n32 * n43 + n12 * n34 * n43 + n13 * n32 * n44 - n12 * n33 * n44;
    float t13 = n13 * n24 * n42 - n14 * n23 * n42 + n14 * n22 * n43 - n12 * n24 * n43 - n13 * n22 * n44 + n12 * n23 * n44;
    float t14 = n14 * n23 * n32 - n13 * n24 * n32 - n14 * n22 * n33 + n12 * n24 * n33 + n13 * n22 * n34 - n12 * n23 * n34;

    float det = n11 * t11 + n21 * t12 + n31 * t13 + n41 * t14;
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

float4x4 float4x4_getGpuProjection(float4x4 m)
{
    m._m10 = -m._m10;
    m._m11 = -m._m11;
    m._m12 = -m._m12;
    m._m13 = -m._m13;   

    m._m20 = m._m20 * 0.5f + m._m30 * 0.5f;
    m._m21 = m._m21 * 0.5f + m._m31 * 0.5f;
    m._m22 = m._m22 * 0.5f + m._m32 * 0.5f;
    m._m23 = m._m23 * 0.5f + m._m33 * 0.5f;
    
    return m;
}

float4x4 ComputeViewMatrixForAABB(float3 minPosition, float3 maxPosition)
{
    float3 center = (minPosition+maxPosition) / 2.0;
    float3 boxTop = float3(center.x, maxPosition.y, center.z);
    
    float4x4 lookAt = float4x4(1,  0,  0, boxTop.x,
                               0,  0, -1, boxTop.y,
                               0, -1,  0, boxTop.z,
                               0,  0,  0, 1);
    
    float4x4 lookAtInverse = float4x4_Inverse(lookAt); // affine matrix
    float4x4 cameraScale   = float4x4_Scale(1, 1, -1);
            
    float4x4 view = mul(cameraScale, lookAtInverse);
    return view;
}

float4x4 ComputeProjectionMatrixForAABB(in float3 boundsSize)
{
    float3 size = boundsSize * 0.5f;
            
    // var ortho = float4x4.OrthoOffCenter(-size.x, size.x, -size.z, size.z, 0, boundsSize.y * 2);
    float4x4 ortho = float4x4_Ortho(-size.x, size.x, -size.z, size.z, 0, boundsSize.y * 2);
    return ortho;
}