﻿#ifndef CRP_LIGHTING_INCLUDED
#define CRP_LIGHTING_INCLUDED

#include "Surface.hlsl"
#include "Light.hlsl"
#include "BRDF.hlsl"

// https://zhuanlan.zhihu.com/p/393174880
float3 IncomingLight(FragSurface surface, Light light)
{
    float dotNL = dot(surface.normalWS, light.directionWS);
    // 光源的衰减和阴影的衰减合成一起 [light.shadowAttenuation; 如果在阴影中,为0,否则大于0 小于1]
    dotNL *= light.shadowAttenuation;
    dotNL = saturate(dotNL);
    return dotNL * light.color;
}

float3 GetLighting(FragSurface surface, BRDF brdf, Light light)
{
    float3 incomeLight = IncomingLight(surface, light);
    // 输入光源 * brdf系数
    // 最常用的BRDF公式为Cook-Torrance,
    float3 cookTorrance = DirectBRDF(surface, brdf, light);
    return incomeLight * cookTorrance;
}

float3 GetLighting(FragSurface surface, BRDF brdf)
{
    ShadowData shadowData = GetShadowData(surface);
    float color = 0;
    // 一个片元受到多个光照影响，就是color叠加
    for(int i = 0, dirLightCount = GetDirectionalLightCount(); i < dirLightCount; ++ i)
    {
        Light light = GetDirectionalLight(i, surface, shadowData);
        color += GetLighting(surface, brdf, light);
    }
    return color;
}

#endif
