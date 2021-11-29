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
    float depthVS;

    float metallic;
    float smoothness;

    // 菲涅尔反射强度
    float fresnalStrength;
};

#endif
