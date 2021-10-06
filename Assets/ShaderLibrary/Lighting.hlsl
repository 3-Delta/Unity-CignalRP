#ifndef CRP_LIGHTING_INCLUDED
#define CRP_LIGHTING_INCLUDED

#include "Surface.hlsl"
#include "Light.hlsl"

float3 IncomingLight(Surface surface, Light light)
{
    float dotNL = dot(surface.nromalWS, light.directionWS);
    dotNL = saturate(dotNL);
    return dotNL * light.color;
}

float3 GetLighting(Surface surface, Light light)
{
    return IncomingLight(surface, light) * surface.color;
}

float3 GetLighting(Surface surface)
{
    float color = 0;
    // 一个片元受到多个光照影响，就是color叠加
    for(int i = 0, dirLightCount = GetDirectionalLightCount(); i < dirLightCount; ++ i)
    {
        color += GetLighting(surface, GetDirectionalLight(i));
    }
    return color;
}

#endif
