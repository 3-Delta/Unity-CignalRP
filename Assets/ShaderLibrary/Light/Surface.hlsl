#ifndef CRP_SURFACE_INCLUDED
#define CRP_SURFACE_INCLUDED

struct FragSurface
{
    float3 positionWS;
    float3 interpolatedNormal;
    float3 normalWS;
    
    float3 color;
    float alpha;

    float3 viewDirectionWS;
    float depthVS; // 观察空间深度，距离camera位置的深度，不是距离近裁剪面的深度

    float metallic;
    float smoothness;

    // 菲涅尔反射强度
    float fresnalStrength;

    // meshrenderer的layermask
    uint meshRenderingLayerMask;
};

#endif
