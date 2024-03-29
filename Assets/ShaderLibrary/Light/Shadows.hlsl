﻿#ifndef CRP_SHADOW_INCLUDED
#define CRP_SHADOW_INCLUDED

#include "HLSLSupport.cginc"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"
#include "Surface.hlsl"

#if defined(_DIRECTIONAL_PCF3)
    #define DIRECTIONAL_FILTER_SAMPLES 4
    #define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3
#elif defined(_DIRECTIONAL_PCF5)
    #define DIRECTIONAL_FILTER_SAMPLES 9
    #define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
#elif defined(_DIRECTIONAL_PCF7)
    #define DIRECTIONAL_FILTER_SAMPLES 16
    #define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
#endif

#define MAX_SHADOW_DIRECTIONAL_LIGHT_COUNT 4

// 其实就是:TEXTURE2D(textureName)
TEXTURE2D_SHADOW(_DirectionalLightShadowAtlas);

#if defined(_OTHER_PCF3)
    #define OTHER_FILTER_SAMPLES 4
    #define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3
#elif defined(_OTHER_PCF5)
    #define OTHER_FILTER_SAMPLES 9
    #define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
#elif defined(_OTHER_PCF7)
    #define OTHER_FILTER_SAMPLES 16
    #define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
#endif

#define MAX_SHADOW_OTHER_LIGHT_COUNT 16

// 其实就是:TEXTURE2D(textureName)
TEXTURE2D_SHADOW(_OtherLightShadowAtlas);

#define MAX_CASCADE_COUNT 4

#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);

CBUFFER_START(_CRPShadows)
    int _CascadeCount;
    // 针对同一个相机,所有光源共用
    float4 _CascadeCullingSpheres[MAX_CASCADE_COUNT];
    float4 _CascadeData[MAX_CASCADE_COUNT];

    float4 _ShadowDistanceVSFade; // 1/MaxDistance, 1/fadeDistance
    float4 _ShadowAtlasSize; // new Vector4(atlasSize, 1f / atlasSize)

    float4x4 _DirectionalShadowLightMatrices[MAX_SHADOW_DIRECTIONAL_LIGHT_COUNT * MAX_CASCADE_COUNT];
    float4x4 _OtherShadowLightMatrices[MAX_SHADOW_OTHER_LIGHT_COUNT];
    float4 _OtherShadowTiles[MAX_SHADOW_OTHER_LIGHT_COUNT];
CBUFFER_END

struct DirectionalShadowData
{
    float shadowStrength;
    int tileIndexInShadowmap;
    float normalBias;

    int shadowMaskChannel;
};

struct OtherShadowData
{
    float shadowStrength;
    int tileIndex;
    int shadowMaskChannel;

    bool isPointLight;
    
    float3 lightPosWS;
    float3 spotDirectionWS;

    float3 lightDirectionWS;
};

struct ShadowMask
{
    bool isAlways;
    bool isDistance;
    float4 shadow; // rgba4个channel存储4个光源的shadow
};

struct ShadowData
{
    int cascadeIndex;

    // strength 其实是 == inAnyCascade * inMaxVSShadowDistance
    fixed inAnyCascade;
    // 是否超过了maxDistance
    fixed inMaxVSShadowDistance;

    ShadowMask shadowMask;

    float GetStrength()
    {
        return inAnyCascade * inMaxVSShadowDistance;
    }
};

static const float3 pointShadowPlanes[6] = {
    float3(-1.0, 0.0, 0.0),
    float3(1.0, 0.0, 0.0),
    float3(0.0, -1.0, 0.0),
    float3(0.0, 1.0, 0.0),
    float3(0.0, 0.0, -1.0),
    float3(0.0, 0.0, 1.0),
};

float FadedShadowStrength(float depthVS, float maxVSDistance, float fade)
{
    // 因为maxDistance处会直接突兀的截断shadow,所以需要在maxDistance之前的某个距离开始到maxDistance进行平滑过度
    return saturate((1 - depthVS * maxVSDistance) * fade);
}

ShadowData GetShadowData(FragSurface surface)
{
    // Assets\ShaderLibrary\Light\maxDistance和cullsphere.png
    ShadowData data;
    data.shadowMask.isDistance = false;
    data.shadowMask.isAlways = false;
    data.shadowMask.shadow = 1.0;

    data.inAnyCascade = 1;
    // 本来是 超过最大阴影距离，则认为不接受阴影，参照Assets\ShaderLibrary\Light\maxDistance和cullsphere.png的绿色和蓝色
    // 为了做最大距离的阴影不会突然被切掉，所以从距离最大距离之前的fadeDistance之前就要开始渐变
    data.inMaxVSShadowDistance = FadedShadowStrength(surface.depthVS, _ShadowDistanceVSFade.x, _ShadowDistanceVSFade.y);

    int i;
    for (i = 0; i < _CascadeCount; ++ i)
    {
        float4 sphere = _CascadeCullingSpheres[i];
        float distanceSqr = DistanceSquare(surface.positionWS, sphere.xyz);
        if (distanceSqr < sphere.w) // 在某个cascade的裁剪球之内
        {   
            if (i == _CascadeCount - 1)
            {
                data.inMaxVSShadowDistance *= FadedShadowStrength(distanceSqr, _CascadeData[i].x, _ShadowDistanceVSFade.z);
            }
            // 如果一个vertex处于两个球体中,则这里只计算cascadeIndex小的球体.因为这里break了
            break;
        }
    }

    if (i == _CascadeCount && _CascadeCount > 0)
    {
        // 片元不在任何一个cascade之内
        // 参照Assets\ShaderLibrary\Light\maxDistance和cullsphere.png，黄色就是在maxShadowDistance之内，但是不在裁剪球内
        data.inAnyCascade = 0;
    }

    data.cascadeIndex = i;
    return data;
}

float SampleDirectionalShadowAtlas(float3 positionSTS)
{
    // https://blog.csdn.net/weixin_43675955/article/details/85226485
    // SAMPLE_TEXTURE2D_SHADOW 因为shadowmap没有mipmap,所以采样的就是0级，而且其实是使用xy坐标的shadowmap的depth和z比较大小
    // 返回值在[0, 1]之间，也就是要么被遮挡，要么不被遮挡,要么部分被遮挡
    // todo 能否返回类型修改为fixed
    // 片元在阴影中为0,否则为1
    return SAMPLE_TEXTURE2D_SHADOW(_DirectionalLightShadowAtlas, SHADOW_SAMPLER, positionSTS);
}

// 利用pcf机制对于positionSTS周边的filterSize*filterSize的矩形进行遮挡情况的计算
// 获取filtersize区域内，每个vertex的positionSTS对应的vertex是否被遮挡
// 目的是为了阴影锯齿 或者 软阴影
float FilterDirectionalShadow(float3 positionSTS)
{
    #if defined(DIRECTIONAL_FILTER_SETUP) // 如果是pcf机制
        float weights[DIRECTIONAL_FILTER_SAMPLES];
        float2 positions[DIRECTIONAL_FILTER_SAMPLES];
        float4 size = _ShadowAtlasSize.yyxx;
        DIRECTIONAL_FILTER_SETUP(size, positionSTS.xy, weights, positions);
        fixed shadow = 0;
        // 片段完全被阴影覆盖，那么我们将得到零，而如果根本没有阴影，那么我们将得到一。之间的值表示片段被部分遮挡。
        // 也就是概率
        for (int i = 0; i < DIRECTIONAL_FILTER_SAMPLES; i++) {
            shadow += weights[i] * SampleDirectionalShadowAtlas(float3(positions[i].xy, positionSTS.z));
        }
        return shadow;
    #else // 非pcf机制
    return SampleDirectionalShadowAtlas(positionSTS);
    #endif
}

float SampleOtherShadowAtlas(float3 positionSTS, float3 bounds)
{
    positionSTS.xy = clamp(positionSTS.xy, bounds.xy, bounds.xy + bounds.z);
    return SAMPLE_TEXTURE2D_SHADOW(_OtherLightShadowAtlas, SHADOW_SAMPLER, positionSTS);
}

float FilterOtherShadow(float3 positionSTS, float3 bounds)
{
#if defined(OTHER_FILTER_SETUP) // 如果是pcf机制
    float weights[OTHER_FILTER_SAMPLES];
    float2 positions[OTHER_FILTER_SAMPLES];
    float4 size = _ShadowAtlasSize.wwzz;
    OTHER_FILTER_SETUP(size, positionSTS.xy, weights, positions);
    fixed shadow = 0;
    for (int i = 0; i < OTHER_FILTER_SAMPLES; i++) {
        shadow += weights[i] * SampleOtherShadowAtlas(float3(positions[i].xy, positionSTS.z), bounds);
    }
    return shadow;
#else // 非pcf机制
    return SampleOtherShadowAtlas(positionSTS, bounds);
#endif
}

float GetOtherShadow(OtherShadowData other, ShadowData globalShadowData, FragSurface surface)
{
    float tileIndex = other.tileIndex;
    float3 lightPlane = other.spotDirectionWS;
    if(other.isPointLight)
    {
        float faceOffset = CubeMapFaceID(-other.lightDirectionWS);
        tileIndex += faceOffset;
        lightPlane += pointShadowPlanes[faceOffset];
    }
    
    float4 tileData = _OtherShadowTiles[tileIndex];
    float3 surfaceToLight = other.lightPosWS - surface.positionWS;
    float distanceToLightPlane = dot(surfaceToLight, lightPlane);
    float3 normalBias = surface.interpolatedNormal * ( distanceToLightPlane * tileData.w);
    float4 p = float4(surface.positionWS + normalBias, 1.0);
    float4 positionSTS = mul(_OtherShadowLightMatrices[tileIndex], p);
    return FilterOtherShadow(positionSTS.xyz / positionSTS.w, tileData.xyz);
}

float GetRealTimeShadow(DirectionalShadowData dirShadowData, ShadowData globalShadowData, FragSurface surface)
{
    // 每个级联应该使用的不是相同的pcfFilterSize
    float pcfFilterSize = _CascadeData[globalShadowData.cascadeIndex].y;
    float3 normalBias = surface.normalWS * (dirShadowData.normalBias * pcfFilterSize);
    float4x4 world2shadow = _DirectionalShadowLightMatrices[dirShadowData.tileIndexInShadowmap];
    // 增加normalbias之后，其实
    float4 ws = float4(surface.positionWS + normalBias, 1);
    // 将世界的z转换为光源阴影空间的z, 从而比对z
    float3 positionSTS = mul(world2shadow, ws).xyz;
    float shadow = FilterDirectionalShadow(positionSTS);
    return shadow;
}

float GetBakedShadow(ShadowMask shadowMask, int channel)
{
    float shadow = 1.0;
    if (shadowMask.isDistance || shadowMask.isAlways)
    {
        if (channel >= 0)
        {
            shadow = shadowMask.shadow[channel]; // r是深度,还是一个是否在阴影中的bool值?
        }
    }
    return shadow;
}

float GetBakedShadow(ShadowMask shadowMask, int channel, float realTimeLightShadowStrength)
{
    float shadow = 1.0;
    if (shadowMask.isDistance || shadowMask.isAlways)
    {
        shadow = lerp(1.0, GetBakedShadow(shadowMask, channel), realTimeLightShadowStrength);
    }
    return shadow;
}

// 混合某个片元的阴影
// 片元不受阴影，返回1
float MixBakedAndRealTimeShadow(ShadowData globalShadowData, float realTimeShadow, int channel, float realTimeLightShadowStrength)
{
    float bakedShadow = GetBakedShadow(globalShadowData.shadowMask, channel);
    // distanceStrength其实是受到MaxShadowDistance和CullSphere影响的一个阴影强度
    float distanceStrength = globalShadowData.GetStrength();
    
    if (globalShadowData.shadowMask.isAlways)
    {
        realTimeShadow = lerp(1.0, realTimeShadow, distanceStrength);
        // always下，只是检验一下min,没有和bake进行fade
        // todo
        float shadow = min(bakedShadow, realTimeShadow);
        return lerp(1.0, shadow, realTimeLightShadowStrength);
    }

    if (globalShadowData.shadowMask.isDistance)
    {
        // lerp的过程中处理了超过maxDistance的时候的shadow的情况
        // 如果在maxShadowDistance或cullPhere之外，则distanceStrength为0，此时当前片元必然应该都是shadowmask的烘焙阴影，也就是bakedShadow
        // 如果在maxShadowDistance和cullPhere之内，则distanceStrength为1，此时当前片元必然应该都是从shadowmap采样的实时阴影，也就是realTimeShadow
        realTimeShadow = lerp(bakedShadow, realTimeShadow, distanceStrength);

        // 阴影强度越小，则衰减越小，强度为0时，肯定当前片元完全不受阴影阴影，所以返回1
        return lerp(1.0, realTimeShadow, realTimeLightShadowStrength);
    }

    return lerp(1.0, realTimeShadow, realTimeLightShadowStrength * distanceStrength);
}

// 采样到shadowmap之后,还需要考虑shadowstrength的影响,其实strength==0的时候,可以不生成shadowmap的,这样子节省
// 这里配合返回值其实是配合IncomingLight的乘法计算的,所以在阴影中为0
float GetDirectionalShadowAttenuation(DirectionalShadowData dirShadowData, ShadowData globalShadowData, FragSurface surface)
{
    #if !defined(_RECEIVE_SHADOWS)
        return 1.0;
    #endif

    float shadow;
    // 因为strength==0的时候,其实不应该有shadowmap
    // needSampleShadowmap为0,这里直接return,不会采样shadowmap
    if (dirShadowData.shadowStrength * globalShadowData.GetStrength() <= 0)
    {
        // 被裁减的时候dirShadowData.shadowStrength是负数, 所以需要abs
        shadow = GetBakedShadow(globalShadowData.shadowMask, abs(dirShadowData.shadowStrength));
    }
    else
    {
        float realTimeShadow = GetRealTimeShadow(dirShadowData, globalShadowData, surface);
        shadow = MixBakedAndRealTimeShadow(globalShadowData, realTimeShadow, dirShadowData.shadowMaskChannel, dirShadowData.shadowStrength);
    }
    return shadow;
}

float GetOtherShadowAttenuation(OtherShadowData other, ShadowData globalShadowData, FragSurface surface)
{
    #if !defined(_RECEIVE_SHADOWS)
        return 1.0;
    #endif

    float shadow;
    if (other.shadowStrength * globalShadowData.GetStrength() <= 0.0)
    {
        shadow = GetBakedShadow(globalShadowData.shadowMask, other.shadowMaskChannel, other.shadowStrength);
    }
    else
    {
        shadow = GetOtherShadow(other, globalShadowData, surface);
        shadow = MixBakedAndRealTimeShadow(globalShadowData, shadow, other.shadowMaskChannel, other.shadowStrength);
    }

    return shadow;
}

#endif
