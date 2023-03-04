#ifndef CRP_LIGHTING_INCLUDED
#define CRP_LIGHTING_INCLUDED

#include "Surface.hlsl"
#include "Light.hlsl"
#include "BRDF.hlsl"
#include "GI.hlsl"

// https://zhuanlan.zhihu.com/p/393174880
float3 IncomingLight(FragSurface surface, Light light)
{
    float dotNL = dot(surface.normalWS, light.fragToLightDirectionWS);
    // 光源的衰减和阴影的衰减合成一起 [light.shadowAttenuation; 如果在阴影中,为0,否则大于0 小于1]
    dotNL *= light.GetAttenuation();
    dotNL = saturate(dotNL);
    // 其实就是《shader入门精要》漫反射的计算方式
    // light.color是cpu传递的，其实是color*intensity
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

bool RenderingMayerOverlap(FragSurface surface, Light light)
{
    return (surface.meshRenderingLayerMask & light.lightRenderingLayerMask) != 0.0;
}

float3 GetLighting(FragSurface surface, BRDF brdf, GI gi)
{
    ShadowData globalShadowData = GetShadowData(surface); // 获取实时阴影衰减等参数

    // 获取烘焙阴影参数
    // shadowData的gi ShadowMask = gi的shadowmask 
    globalShadowData.shadowMask = gi.shadowMask;

    float3 color = 0.0;
    
    /* baked */
    // gi的漫反射中影响漫反射
    float3 giColor = IndirectBRDF(surface, brdf, gi.diffuse, gi.specular);
    color += giColor;

    /* realtime */
    // 一个片元受到多个光照影响，就是color叠加
    for(int i = 0, dirLightCount = GetDirectionalLightCount(); i < dirLightCount; ++ i)
    {
        Light light = GetDirectionalLight(i, surface, globalShadowData);
        if(RenderingMayerOverlap(surface, light))
        {
            color += GetLighting(surface, brdf, light);
        }
    }

    #if defined(_LIGHTS_PER_OBJECT)
        for (int i = 0; i < min(unity_LightData.y, 8); ++ i)
        {
            int lightIndex = unity_LightIndices[(uint)i / 4][(uint)i % 4];
            Light light = GetOtherLight(lightIndex, surface, globalShadowData);
            if(RenderingMayerOverlap(surface, light))
            {
                color += GetLighting(surface, brdf, light);
            }
        }
    #else
        for (int i = 0; i < GetOtherLightCount(); ++ i)
        {
            Light light = GetOtherLight(i, surface, globalShadowData);
            if(RenderingMayerOverlap(surface, light))
            {
                color += GetLighting(surface, brdf, light);
            }
        }
    #endif
    return color;
}

#endif
