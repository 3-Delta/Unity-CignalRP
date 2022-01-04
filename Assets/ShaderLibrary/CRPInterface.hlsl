#ifndef CRP_INTERFACE_INCLUDED
#define CRP_INTERFACE_INCLUDED

#include "UnityInput.hlsl"

// 获取cbuffer的某个属性
#define GetCbufferProp(cbuffer, name) UNITY_ACCESS_INSTANCED_PROP(cbuffer, name)
// https://zhuanlan.zhihu.com/p/33458843
// UI小地图mask的overdraw的优化，其实就是小的显示区域，去采样大地图的uv，然后展示出来，这样在显示区域之外的overdraw就可以避免
#define UVScaleOffset(uv, ScaleOffset) (uv * ScaleOffset.zw + ScaleOffset.xy) 

float3 MeshCenterWS()
{
    float3 center = mul(unity_ObjectToWorld , float4(0, 0, 0, 1)).xyz;
    return center;
}

// https://edu.uwa4d.com/lesson-detail/285/1348/0?isPreview=0
float2 UVDirToCenter(float2 uv, float2 center = float2(0.5, 0.5))
{
    return uv -= center;
}

// https://zhuanlan.zhihu.com/p/33458843

#endif
