﻿#ifndef CRP_COMMON_INCLUDED
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

bool IsOrthoCamera()
{
    return unity_OrthoParams.w; // 如果是正交相机，w为1，否则0
}

// zSS [0, 1], 0是近裁剪面,1是远裁剪面, zCS是距离近裁剪面的距离，不是距离相机的距离，zVS则是距离相机的距离
// [-1, 1]的情况下,-1是近裁剪面,1是远裁剪面
// 是Zndc[-1, 1]然后 +1/2存储的纹理depth[0, 1]
// 从到近平面的距离 转换为 到相机的距离
float OrthoDepthBufferToLinear(float zSS) // zCS,zSS其实就是zCS, 因为正交的w始终为1,而zCS是距离近裁剪面的距离
{
#if UNITY_REVERSED_Z
    zSS = 1.0 - zSS;
#endif
    // _ProjectionParams参数意义:  y,near, z, far, w = 1+1/far
    float near = _ProjectionParams.y;
    float far = _ProjectionParams.z;
    return (far - near) * zSS + near;
}

// 视图空间下的depth
float GetFragZVS(float4 fragPositionSS) // fragPositionSS是屏幕空间坐标
{
    // ss空间其实是引擎自动从cs空间转换来的，ss.z是cs.z插值来的， ss.z是距离近裁剪面的距离，不是距离相机的距离
    // 正交投影矩阵的ss.w永远为1,所以透视除法之后和原来一样, 所以ss.z不能正确表达depth
    // 透视的ss.w则是-vs.z
    // <shader入门精要> P.79
    float zVS = IsOrthoCamera() ? OrthoDepthBufferToLinear(fragPositionSS.z) : fragPositionSS.w;
    return zVS;
}

// 视图空间下的depth
float GetFragZVS(float zSS)
{
    float zVS = IsOrthoCamera() ? OrthoDepthBufferToLinear(zSS) : LinearEyeDepth(zSS, _ZBufferParams);
    return zVS;
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

// step(a, x)	    Returns (x >= a) ? 1 : 0
// sin(x)	        Returns the sine of x
// lerp(x, y, s)	Returns x + s(y - x).

// https://zhuanlan.zhihu.com/p/612263535
// 例子：if (x >= 3) { y += 5; }
// y += (5 * WhenGreaterEqual(x, 3));
// 大于等于返回1，否则返回0
float WhenGreaterEqual(float x, float target) {
    return step(x, target);
}

float WhenLessEqual(float x, float target) {
    return step(target, x);
}

// 例子：if (x == 3) { y += 5; }
// y += (5 * WhenEqual(x, 3));
// 
// 例子：if (x == 3) { b = a1; } else { b = a2; }
// b = lerp(a2, a1, WhenEqual(x, 3));
// 
// 等于返回1，否则返回0
float WhenEqual(float x, float target) {
    return 1.0 - abs(sign(x - target));
}

// 不等于返回1，否则返回0
float WhenNotEqual(float x, float target) {
    return abs(sign(x - target));
}

// 大于返回1，否则0
float WhenGreater(float x, float target) {
    return max(sign(x - target), 0.0);
}

// 小于返回1，否则0
float WhenLesser(float x, float target) {
    return max(sign(target - x), 0.0);
}

// SV_DispatchThreadID就是当前线程对应的整张texture中的像素位置
// https://zhuanlan.zhihu.com/p/368307575
#define CSKERNAL_ARGS uint3 groupId : SV_GroupID, uint3 groupThreadId : SV_GroupThreadID, uint3 dispatchThreadId : SV_DispatchThreadID, uint groupIndex : SV_GroupIndex

// 按照到相机的距离，决定做逐顶点光照，还是逐像素光照。这个判断肯定要在顶点着色器中执行
// q: 按照哪个顶点决定呢？
// 顶点是距离相机的距离是否在阈值之内
float InCameraDistance(float3 vertPosWS, float THRESHOLD)
{
    float dist = distance(vertPosWS, _WorldSpaceCameraPos);
    return WhenLesser(dist, THRESHOLD);
}

#endif
