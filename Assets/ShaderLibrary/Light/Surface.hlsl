#ifndef CRP_SURFACE_INCLUDED
#define CRP_SURFACE_INCLUDED

struct Surface
{
    float3 positionWS;
    float3 normalWS;
    
    float3 color;
    float alpha;

    float3 viewDirWS;

    float metallic;
    float smoothness;
};

#endif
