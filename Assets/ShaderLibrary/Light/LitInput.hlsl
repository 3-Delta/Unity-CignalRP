#ifndef CRP_LIT_INPUT_INCLUDED
#define CRP_LIT_INPUT_INCLUDED

#include "../Common.hlsl"
#include "Surface.hlsl"
#include "Shadows.hlsl"
#include "Light.hlsl"
#include "Lighting.hlsl"
#include "BRDF.hlsl"
#include "GI.hlsl"

// 默认纹理
TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

TEXTURE2D(_EmissionMap);

UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
    UNITY_DEFINE_INSTANCED_PROP(float4, _EmissionColor)
    UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)

    UNITY_DEFINE_INSTANCED_PROP(float, _Metallic)
    UNITY_DEFINE_INSTANCED_PROP(float, _Smoothness)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

float2 TransformBaseUV(float2 baseUV)
{
    float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST);
    return baseUV * baseST.xy + baseST.zw;
}

float4 GetBase(float2 baseUV)
{
    float4 texelColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, baseUV);
    float4 color = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor);
    return texelColor * color;
}

// 自发光选项
float3 GetEmission(float2 baseUV) {
    float4 texelColor = SAMPLE_TEXTURE2D(_EmissionMap, sampler_BaseMap, baseUV);
    float4 color = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _EmissionColor);
    return texelColor.rgb * color.rgb;
}

float GetCutoff(float2 baseUV)
{
    return UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Cutoff);
}

float GetMetallic(float2 baseUV)
{
    return UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Metallic);
}

float GetSmoothness(float2 baseUV)
{
    return UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Smoothness);
}

#endif
