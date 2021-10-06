#ifndef CRP_INTERFACE_INCLUDED
#define CRP_INTERFACE_INCLUDED

#include "UnityInput.hlsl"

float3 MeshCenterWS()
{
    float3 center = mul(unity_ObjectToWorld , float4(0, 0, 0, 1)).xyz;
    return center;
}

#endif
