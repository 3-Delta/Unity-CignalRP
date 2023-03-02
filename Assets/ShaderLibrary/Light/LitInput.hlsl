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
    UNITY_DEFINE_INSTANCED_PROP(float, _Fresnal)

    UNITY_DEFINE_INSTANCED_PROP(float, _ZWrite)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

#define GetInputProp(name) UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, name)

struct InputConfig
{
    float2 baseUV;

    Fragment fragment;
};

InputConfig GetInputConfig(float4 fragPositionSS, float2 baseUV)
{
    InputConfig c;
    c.baseUV = baseUV;
    c.fragment = GetFragment(fragPositionSS);
    return c;
}

float2 TransformBaseUV(float2 baseUV)
{
    float4 baseST = GetInputProp(_BaseMap_ST);
    return baseUV * baseST.xy + baseST.zw;
}

float4 GetBase(InputConfig input)
{
    float4 texelColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseUV);
    float4 color = GetInputProp(_BaseColor);
    return texelColor * color;
}

// 自发光选项
float3 GetEmission(InputConfig input)
{
    float4 texelColor = SAMPLE_TEXTURE2D(_EmissionMap, sampler_BaseMap, input.baseUV);
    float4 color = GetInputProp(_EmissionColor);
    return texelColor.rgb * color.rgb;
}

float GetCutoff(InputConfig input)
{
    return GetInputProp(_Cutoff);
}

float GetMetallic(InputConfig input)
{
    return GetInputProp(_Metallic);
}

float GetSmoothness(InputConfig input)
{
    return GetInputProp(_Smoothness);
}

float GetFresnal(InputConfig input)
{
    return GetInputProp(_Fresnal);
}

float GetFinalAlpha(float alpha)
{
    return GetInputProp(_ZWrite) ? 1.0 : alpha;
}

#endif
