#ifndef CRP_SHADOW_INCLUDED
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
#define MAX_CASCADE_COUNT 4

// 其实就是:TEXTURE2D(textureName)
TEXTURE2D_SHADOW(_DirectionalLightShadowAtlas);
#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);

CBUFFER_START(_CRPShadows)
int _CascadeCount;
// 针对同一个相机,所有光源共用
float4 _CascadeCullingSpheres[MAX_CASCADE_COUNT];
float4 _CascadeData[MAX_CASCADE_COUNT];

float4 _ShadowDistanceVSFade;
float4 _ShadowAtlasSize; // new Vector4(atlasSize, 1f / atlasSize)

float4x4 _DirectionalShadowLightMatrices[MAX_SHADOW_DIRECTIONAL_LIGHT_COUNT * MAX_CASCADE_COUNT];
CBUFFER_END

struct DirectionalShadowData
{
    float shadowStrength;
    int tileIndexInShadowmap;
    float normalBias;

    int shadowMaskChannel;
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
    data.inMaxVSShadowDistance = FadedShadowStrength(surface.depthVS, _ShadowDistanceVSFade.x, _ShadowDistanceVSFade.y);

    int i;
    for (i = 0; i < _CascadeCount; ++ i)
    {
        float4 sphere = _CascadeCullingSpheres[i];
        float distanceSqr = DistanceSquare(surface.positionWS, sphere.xyz);
        if (distanceSqr < sphere.w)
        {
            if (i == _CascadeCount - 1)
            {
                data.inMaxVSShadowDistance *= FadedShadowStrength(distanceSqr, _CascadeData[i].x, _ShadowDistanceVSFade.z);
            }
            // 如果一个vertex处于两个球体中,则这里只计算cascadeIndex小的球体.因为这里break了
            break;
        }
    }

    if (i == _CascadeCount)
    {
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
        if(channel >= 0)
        {
            shadow = shadowMask.shadow[channel]; // r是深度,还是一个是否在阴影中的bool值?
        }
    }
    return shadow;
}

float GetBakedShadow(ShadowMask shadowMask, int channel, float strength)
{
    float shadow = 1.0;
    if (shadowMask.isDistance || shadowMask.isAlways)
    {
        shadow = lerp(1.0, GetBakedShadow(shadowMask, channel), strength);
    }
    return shadow;
}

float MixBakedAndRealTimeShadow(ShadowData globalShadowData, float realTimeShadow, int channel, float shadowStrength)
{
    float bakedShadow = GetBakedShadow(globalShadowData.shadowMask, channel);
    if (globalShadowData.shadowMask.isAlways)
    {
        realTimeShadow = lerp(1.0, realTimeShadow, globalShadowData.GetStrength());
        // always下，只是检验一下min,没有和bake进行fade
        // todo
        float shadow = min(bakedShadow, realTimeShadow);
        return lerp(1.0, shadow, shadowStrength);
    }

    if (globalShadowData.shadowMask.isDistance)
    {
        // lerp的过程中处理了超过maxDistance的时候的shadow的情乱
        realTimeShadow = lerp(bakedShadow, realTimeShadow, globalShadowData.GetStrength());
        return lerp(1.0, realTimeShadow, shadowStrength);
    }

    return lerp(1.0, realTimeShadow, shadowStrength * globalShadowData.GetStrength());
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

#endif
