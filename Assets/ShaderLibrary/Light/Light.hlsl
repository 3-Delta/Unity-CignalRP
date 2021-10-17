#ifndef CRP_LIGHT_INCLUDED
#define CRP_LIGHT_INCLUDED

#include "Surface.hlsl"
#include "Shadows.hlsl"

#define MAX_DIR_LIGHT_COUNT 4

CBUFFER_START(_CRPLight)
    int _DirectionalLightCount; 
    float4 _DirectionalLightColors[MAX_DIR_LIGHT_COUNT];
    float4 _DirectionalLightWSDirections[MAX_DIR_LIGHT_COUNT];
    float4 _DirectionalLightShadowData[MAX_DIR_LIGHT_COUNT];
CBUFFER_END

// 光源属性
struct Light
{
    float3 color;
    float3 directionWS;
    float shadowAttenuation;
};

int GetDirectionalLightCount()
{
    return _DirectionalLightCount;
}

DirectionalShadowData GetDirectionalShadowData(int lightIndex)
{
    DirectionalShadowData data;
    data.shadowStrength = _DirectionalLightShadowData[lightIndex].x;
    data.tileIndexInShadowmap = _DirectionalLightShadowData[lightIndex].y;
    return data;
}

Light GetDirectionalLight(int lightIndex, Surface surface)
{
    Light light;
    light.color = _DirectionalLightColors[lightIndex];
    light.directionWS = _DirectionalLightWSDirections[lightIndex];

    DirectionalShadowData data = GetDirectionalShadowData(lightIndex);
    light.shadowAttenuation = GetDirectionalShadowAttenuation(data, surface);
    return light;
}

#endif
