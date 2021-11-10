#ifndef CRP_GI_INCLUDED
#define CRP_GI_INCLUDED

#if defined(LIGHTMAP_ON)
    #define GI_ATTRIBUTE_DATA float2 lightmapUV : TEXCOORD1;
    #define GI_VARYINGS_DATA float2 lightmapUV : VAR_LIGHT_MAP_UV;
    #define TRANSFER_GI_DATA(input, output) output.lightmapUV = input.lightmapUV * unity_LightmapST.xy + unity_LightmapST.zw;
    #define GI_FRAGMENT_DATA(input) input.lightmapUV
#else
    #define GI_ATTRIBUTE_DATA
    #define GI_VARYINGS_DATA
    #define TRANSFER_GI_DATA(input, output)
    #define GI_FRAGMENT_DATA(input) 0.0
#endif

struct GI
{
    float3 diffuse; // 漫反射颜色, gi都是漫反射, 因为间接光照的光源位置不固定. 高光反射都是lightprobo提供
};

GI GetGI(float2 lightmapUV)
{
    GI gi;
    gi.diffuse = float3(lightmapUV, 0.0);
    return gi;
}

#endif
