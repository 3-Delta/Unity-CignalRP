#ifndef CRP_COMMON_INCLUDED
#define CRP_COMMON_INCLUDED

// 定义宏,因为SpaceTransforms中使用到了这些宏
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "UnityInput.hlsl"

#define UNITY_MATRIX_M unity_ObjectToWorld
#define UNITY_MATRIX_I_M unity_WorldToObject
#define UNITY_MATRIX_V unity_MatrixV
#define UNITY_MATRIX_VP unity_MatrixVP
#define UNITY_MATRIX_P glstate_matrix_projection

#if defined(_SHADOW_MASK_DISTANCE) || defined(_SHADOW_MASK_ALWAYS)
    #define SHADOWS_SHADOWMASK // 给UnityInstancing.hlsl定义
#endif

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

// 获取cbuffer的某个属性
#define GetCbufferProp(cbuffer, name) UNITY_ACCESS_INSTANCED_PROP(cbuffer, name)

bool IsOrthoCamera()
{
    return unity_OrthoParams.w;
}

// depth [0, 1], 0是近裁剪面,1是远裁剪面
// [-1, 1]的情况下,-1是近裁剪面,1是远裁剪面
// 是Zndc[-1, 1]然后 +1/2存储的纹理depth[0, 1]
// 从到近平面的距离 转换为 到相机的距离
float OrthoDepthBufferToLinear(float depth)
{
#if UNITY_REVERSED_Z
    depth = 1.0 - depth;
#endif
    float near = _ProjectionParams.y;
    float far = _ProjectionParams.z;
    return (far - near) * depth + near;
}

SAMPLER(sampler_linear_clamp);
SAMPLER(sampler_point_clamp);

SAMPLER(sampler_CameraColorRT);
#include "Fragment.hlsl"

float Square (float x) {
    return x * x;
}

float DistanceSquare (float3 x, float3 y) {
    float3 diff = x - y;
    return dot(diff, diff);
}

float3 DecodeNormal (float4 sample, float scale) {
    #if defined(UNITY_NO_DXT5nm)
    return UnpackNormalRGB(sample, scale);
    #else
    return UnpackNormalmapRGorAG(sample, scale);
    #endif
}

#endif
