﻿#ifndef CRP_GI_INCLUDED
#define CRP_GI_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ImageBasedLighting.hlsl"

// lightmap
TEXTURE2D(unity_Lightmap);
SAMPLER(samplerunity_Lightmap);

// lppv存储在unity_ProbeVolumeSH的3d纹理中
TEXTURE3D_FLOAT(unity_ProbeVolumeSH);
SAMPLER(samplerunity_ProbeVolumeSH);

// shadowmask
TEXTURE2D(unity_ShadowMask);
SAMPLER(samplerunity_ShadowMask);

// 间接光反射（高光反射）, 默认是天空盒，unity会自己决定将reflectionProbe还是skybox传递给当前片元
TEXTURECUBE(unity_SpecCube0);
SAMPLER(samplerunity_SpecCube0);

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
    // 静态物体 lightmap
    float3 diffuse; // 漫反射颜色, gi都是漫反射, 因为间接光照的光源位置不固定. 高光反射都是lightprobo提供
    float3 specular; // 镜面反射

    // 静态物体 shadowMask
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

// 默认情况下的反射只有skybox, 没有其他物体，为了丰富效果，需要reflectProbe
// gi 高光, 其实是从cubemap中采样，默认是skybox
float3 SampleEnvironment(FragSurface surface, BRDF brdf)
{
    float3 uvw = reflect(-surface.frag2CameraWS, surface.normalWS);
    float mipmap = PerceptualRoughnessToMipmapLevel(brdf.perceptualRoughness);
    float4 environment = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, uvw, mipmap);
    return DecodeHDREnvironment(environment, unity_SpecCube0_HDR);
}

// 当没有GI的时候，光线就会不真实，数值上来说这里返回的是0
GI GetGI(float2 lightmapUV, FragSurface surface, BRDF brdf)
{
    GI gi;
    // 获取间接光的diffuse，间接光可以当做一个临时的直接光处理
    gi.diffuse = SampleLightmap(lightmapUV); // 静态物体lightmap
    gi.diffuse += SampleLightProbe(surface); // 动态物体lightprobe

    gi.specular = SampleEnvironment(surface, brdf); // 高光

    // 烘焙阴影
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
