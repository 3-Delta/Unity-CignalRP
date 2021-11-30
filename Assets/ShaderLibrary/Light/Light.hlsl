#ifndef CRP_LIGHT_INCLUDED
#define CRP_LIGHT_INCLUDED

#include "Surface.hlsl"
#include "Shadows.hlsl"

#define MAX_DIR_LIGHT_COUNT 4
#define MAX_OTHER_LIGHT_COUNT 64

CBUFFER_START(_CRPLight)
    int _DirectionalLightCount; 
    float4 _DirectionalLightColors[MAX_DIR_LIGHT_COUNT];
    float4 _DirectionalLightWSDirectionsAndMasks[MAX_DIR_LIGHT_COUNT];
    float4 _DirectionalLightShadowData[MAX_DIR_LIGHT_COUNT];

    int _OtherLightCount;
    float4 _OtherLightColors[MAX_OTHER_LIGHT_COUNT];
    float4 _OtherLightWSPositions[MAX_OTHER_LIGHT_COUNT];
    // 聚光灯方向 
    float4 _OtherLightWSDirectionsAndMasks[MAX_OTHER_LIGHT_COUNT];
    float4 _OtherLightSpotAngles[MAX_OTHER_LIGHT_COUNT];

    float4 _OtherLightShadowData[MAX_OTHER_LIGHT_COUNT];
CBUFFER_END

// 光源属性
struct Light
{
    float3 color;
    float3 fragToLightDirectionWS;
    float shadowAttenuation;
    float lightAttenuation;

    // 光源的layerMask
    uint renderingLayerMask;

    float GetAttenuation()
    {
        return shadowAttenuation * lightAttenuation;
    }
};

int GetDirectionalLightCount()
{
    return _DirectionalLightCount;
}

int GetOtherLightCount()
{
    return _OtherLightCount;
}

DirectionalShadowData GetDirectionalShadowData(int lightIndex, ShadowData globalShadowData)
{
    DirectionalShadowData data;
    float lightShadowStrength = _DirectionalLightShadowData[lightIndex].x;
    data.shadowStrength = lightShadowStrength; // * globalShadowData.inAnyCascade * globalShadowData.inMaxVSShadowDistance;
    data.tileIndexInShadowmap = _DirectionalLightShadowData[lightIndex].y + globalShadowData.cascadeIndex;
    data.normalBias = _DirectionalLightShadowData[lightIndex].z;
    data.shadowMaskChannel = _DirectionalLightShadowData[lightIndex].w;
    return data;
}

OtherShadowData GetOtherShadowData(int lightIndex)
{
    OtherShadowData data;
    data.shadowStrength = _OtherLightShadowData[lightIndex].x;
    data.tileIndex = _OtherLightShadowData[lightIndex].y;
    data.shadowMaskChannel = _OtherLightShadowData[lightIndex].w;

    data.lightPosWS = 0.0;
    data.isPointLight = _OtherLightShadowData[lightIndex].z == 1.0;
    data.spotDirectionWS = 0.0;
    data.lightDirectionWS = 0.0;
    return data;
}

Light GetDirectionalLight(int lightIndex, FragSurface surface, ShadowData shadowData)
{
    Light light;
    light.color = _DirectionalLightColors[lightIndex];
    light.fragToLightDirectionWS = _DirectionalLightWSDirectionsAndMasks[lightIndex];
    light.lightAttenuation = 1;
    light.renderingLayerMask = asuint(_DirectionalLightWSDirectionsAndMasks[lightIndex].w);

    DirectionalShadowData dirShadowData = GetDirectionalShadowData(lightIndex, shadowData);
    light.shadowAttenuation = GetDirectionalShadowAttenuation(dirShadowData, shadowData, surface);
    return light;
}

Light GetOtherLight(int lightIndex, FragSurface surface, ShadowData globalShadowData)
{
    Light light;
    light.color = _OtherLightColors[lightIndex].rgb;
    float3 lightDirectionWS =  _OtherLightWSPositions[lightIndex].xyz - surface.positionWS;
    light.fragToLightDirectionWS = normalize(lightDirectionWS);
    light.renderingLayerMask = asuint(_OtherLightWSDirectionsAndMasks[lightIndex].w);

    float distanceSqr = max(dot(lightDirectionWS, lightDirectionWS), 0.00001);
    // 因为球体范围，球体表面积是4Pi*R*R, 所以衰减是R*R的反比
    float rangeAttenuation = Square(saturate(1 - Square(distanceSqr * _OtherLightWSPositions[lightIndex].w)));

    // 聚光灯多考虑 方向 和 角度 限制
    float3 spotDirection = _OtherLightWSDirectionsAndMasks[lightIndex].xyz;
    float4 apotAngle = _OtherLightSpotAngles[lightIndex];
    float spotAttenuation = Square(saturate(dot(spotDirection, light.fragToLightDirectionWS) * apotAngle.x + apotAngle.y));

    OtherShadowData otherShadowData = GetOtherShadowData(lightIndex);
    otherShadowData.lightPosWS = _OtherLightWSPositions[lightIndex].xyz;
    otherShadowData.lightDirectionWS = light.fragToLightDirectionWS;
    otherShadowData.spotDirectionWS = spotDirection;

    light.shadowAttenuation = GetOtherShadowAttenuation(otherShadowData, globalShadowData, surface);
    light.lightAttenuation = rangeAttenuation * spotAttenuation / distanceSqr;
    
    return light;
}

#endif
