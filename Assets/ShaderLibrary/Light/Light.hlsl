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

DirectionalShadowData GetDirectionalShadowData(int lightIndex, ShadowData shadowData)
{
    DirectionalShadowData data;
    float lightShadowStrength = _DirectionalLightShadowData[lightIndex].x;
    data.shadowStrength = lightShadowStrength * shadowData.inAnyCascade * shadowData.inMaxVSShadowDistance;
    data.tileIndexInShadowmap = _DirectionalLightShadowData[lightIndex].y + shadowData.cascadeIndex;
    data.normalBias = _DirectionalLightShadowData[lightIndex].z;
    return data;
}

Light GetDirectionalLight(int lightIndex, FragSurface surface, ShadowData shadowData)
{
    Light light;
    light.color = _DirectionalLightColors[lightIndex];
    light.directionWS = _DirectionalLightWSDirections[lightIndex];

    DirectionalShadowData dirShadowData = GetDirectionalShadowData(lightIndex, shadowData);
    light.shadowAttenuation = GetDirectionalShadowAttenuation(dirShadowData, shadowData, surface);
    return light;
}

#endif
