#ifndef CRP_GI_INCLUDED
#define CRP_GI_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"

// lightmap
TEXTURE2D(unity_Lightmap);
SAMPLER(samplerunity_Lightmap);

// lppv存储在unity_ProbeVolumeSH的3d纹理中
TEXTURE3D_FLOAT(unity_ProbeVolumeSH);
SAMPLER(samplerunity_ProbeVolumeSH);

// shadowmask
TEXTURE2D(unity_ShadowMask);
SAMPLER(samplerunity_ShadowMask);

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
    ShadowMask shadowMask;
};

// 静态物体采样lightmap
float3 SampleLightmap(float2 lightmapUV)
{
    bool encodedLightmap = false;
#if defined(UNITY_LIGHTMAP_FULL_HDR)
    // 是否压缩lightmap, 应该就是在lightmapsetting中设置的,可以renderdoc调试看一下
    encodedLightmap = false;
#else
    encodedLightmap = true;
#endif
    
#if defined(LIGHTMAP_ON)
    float4 scaleOffset = float4(1.0, 1.0, 0.0, 0.0);
    return SampleSingleLightmap(TEXTURE2D_ARGS(unity_Lightmap, samplerunity_Lightmap), lightmapUV, scaleOffset,
        encodedLightmap, float4(LIGHTMAP_HDR_MULTIPLIER, LIGHTMAP_HDR_EXPONENT, 0.0, 0.0));
#else
    return 0.0;
#endif
}

// 动态物体采样lightprobe
float3 SampleLightProbe(FragSurface surface)
{
#if defined(LIGHTMAP_ON)
    return 0.0;
#else
    if(unity_ProbeVolumeParams.x)
    {
        // 使用了lppv?
        return SampleProbeVolumeSH4(TEXTURE3D_ARGS(unity_ProbeVolumeSH, samplerunity_ProbeVolumeSH), surface.positionWS, surface.normalWS,
            unity_ProbeVolumeWorldToObject, unity_ProbeVolumeParams.y, unity_ProbeVolumeParams.z,
            unity_ProbeVolumeMin.xyz, unity_ProbeVolumeSizeInv.xyz);
    }
    else
    {
        // 正常的lightprobe
        float4 coefficients[7];
        coefficients[0] = unity_SHAr;
        coefficients[1] = unity_SHAg;
        coefficients[2] = unity_SHAb;
        coefficients[3] = unity_SHBr;
        coefficients[4] = unity_SHBg;
        coefficients[5] = unity_SHBb;
        coefficients[6] = unity_SHC;
        return max(0.0, SampleSH9(coefficients, surface.normalWS));
    }
#endif
}

float4 SampleBakedShadow(float2 lightmapUV, FragSurface surface)
{
#if defined(LIGHTMAP_ON)
    // 静态物体
    return SAMPLE_TEXTURE2D(unity_ShadowMask, samplerunity_ShadowMask, lightmapUV);
#else
    if(unity_ProbeVolumeParams.x)
    {
        // 使用了lppv的shadowmask
        return SampleProbeOcclusion(TEXTURE3D_ARGS(unity_ProbeVolumeSH, samplerunity_ProbeVolumeSH), surface.positionWS, 
            unity_ProbeVolumeWorldToObject, unity_ProbeVolumeParams.y, unity_ProbeVolumeParams.z,
            unity_ProbeVolumeMin.xyz, unity_ProbeVolumeSizeInv.xyz);
    }
    else
    {
        return unity_ProbesOcclusion;
    }
#endif
}

GI GetGI(float2 lightmapUV, FragSurface surface)
{
    GI gi;
    gi.diffuse = SampleLightmap(lightmapUV);
    gi.diffuse += SampleLightProbe(surface);

    gi.shadowMask.isDistance = false;
    gi.shadowMask.isAlways = false;
    gi.shadowMask.shadow = 1.0;

#if defined(_SHADOW_MASK_ALWAYS)
    gi.shadowMask.isAlways = true;
    gi.shadowMask.shadow = SampleBakedShadow(lightmapUV, surface);
#elif defined(_SHADOW_MASK_DISTANCE)
    gi.shadowMask.isDistance = true;
    gi.shadowMask.shadow = SampleBakedShadow(lightmapUV, surface);
#endif
    
    return gi;
}

#endif
