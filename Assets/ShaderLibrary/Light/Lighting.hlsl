#ifndef CRP_LIGHTING_INCLUDED
#define CRP_LIGHTING_INCLUDED

#include "Surface.hlsl"
#include "Light.hlsl"
#include "BRDF.hlsl"

float3 IncomingLight(Surface surface, Light light)
{
    float dotNL = dot(surface.nromalWS, light.directionWS);
    dotNL = saturate(dotNL);
    return dotNL * light.color;
}

float3 GetLighting(Surface surface, BRDF brdf, Light light)
{
    float3 il = IncomingLight(surface, light);
    // 输入光源 * brdf系数
    float3 diffuseAndSpecular = DirectBRDF(surface, brdf, light);
    return il * diffuseAndSpecular;
}

float3 GetLighting(Surface surface, BRDF brdf)
{
    float color = 0;
    // 一个片元受到多个光照影响，就是color叠加
    for(int i = 0, dirLightCount = GetDirectionalLightCount(); i < dirLightCount; ++ i)
    {
        color += GetLighting(surface, brdf, GetDirectionalLight(i));
    }
    return color;
}

#endif
