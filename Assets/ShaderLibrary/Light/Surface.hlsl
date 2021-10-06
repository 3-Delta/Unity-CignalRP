#ifndef CRP_SURFACE_INCLUDED
#define CRP_SURFACE_INCLUDED

struct Surface
{
    float3 nromalWS;
    
    float3 color;
    float alpha;

    float3 viewDirWS;

    float metallic;
    float smoothness;
};

#endif
