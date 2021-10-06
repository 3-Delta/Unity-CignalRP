#ifndef CRP_LIGHT_INCLUDED
#define CRP_LIGHT_INCLUDED

#include "Surface.hlsl"

#define MAX_DIR_LIGHT_COUNT 4

CBUFFER_START(_CRPLight)
    int _DirectionalLightCount; 
    float4 _DirectionalLightColors[MAX_DIR_LIGHT_COUNT];
    float4 _DirectionalLightWSDirections[MAX_DIR_LIGHT_COUNT];
CBUFFER_END

// 光源属性
struct Light
{
    float3 color;
    float3 directionWS;
};

int GetDirectionalLightCount()
{
    return _DirectionalLightCount;
}

Light GetDirectionalLight(int index)
{
    Light light;
    light.color = _DirectionalLightColors[index];
    light.directionWS = _DirectionalLightWSDirections[index];
    return light;
}

#endif
