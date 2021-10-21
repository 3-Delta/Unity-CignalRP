#ifndef CRP_SHADOW_INCLUDED
#define CRP_SHADOW_INCLUDED

#include "HLSLSupport.cginc"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"
#include "Surface.hlsl"

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

    float _MaxVSShadowDistance;

    float4x4 _DirectionalShadowLightMatrices[MAX_SHADOW_DIRECTIONAL_LIGHT_COUNT * MAX_CASCADE_COUNT];
CBUFFER_END

struct DirectionalShadowData
{
    float shadowStrength;
    int tileIndexInShadowmap;
};

struct ShadowData {
    int cascadeIndex;
    
    fixed inAnyCascade;
    // 是否超过了maxDistance
    fixed inMaxVSShadowDistance;
};

ShadowData GetShadowData(FragSurface surface) {
    ShadowData data;
    data.inAnyCascade = 1;
    data.inMaxVSShadowDistance = surface.depthVS < _MaxVSShadowDistance ? 1 : 0;
    int i;
    for (i = 0; i < _CascadeCount; ++ i)
    {
        float4 sphere = _CascadeCullingSpheres[i];
        float distanceSqr = DistanceSquare(surface.positionWS, sphere.xyz);
        if(distanceSqr < sphere.w)
        {
            // 如果一个vertex处于两个球体中,则这里只计算cascadeIndex小的球体.因为这里break了
            break;
        }
    }

    if(i == _CascadeCount)
    {
        data.inAnyCascade = 0;
    }
    
    data.cascadeIndex = i;
    return data;
}

fixed SampleDirectionalShadowAtlas(float3 positionSTS)
{
    // https://blog.csdn.net/weixin_43675955/article/details/85226485
    // SAMPLE_TEXTURE2D_SHADOW 因为shadowmap没有mipmap,所以采样的就是0级，而且其实是使用xy坐标的shadowmap的depth和z比较大小
    // 返回值在[0, 1]之间，也就是要么被遮挡，要么不被遮挡,要么部分被遮挡
    // todo 能否返回类型修改为fixed
    // 片元在阴影中为0,否则为1
    return (fixed)SAMPLE_TEXTURE2D_SHADOW(_DirectionalLightShadowAtlas, SHADOW_SAMPLER, positionSTS);
}

// 采样到shadowmap之后,还需要考虑shadowstrength的影响,其实strength==0的时候,可以不生成shadowmap的,这样子节省
// 这里配合返回值其实是配合IncomingLight的乘法计算的,所以在阴影中为0
fixed GetDirectionalShadowAttenuation(DirectionalShadowData shadowData, FragSurface surface)
{
    // 因为strength==0的时候,其实不应该有shadowmap
    // needSampleShadowmap为0,这里直接return,不会采样shadowmap
    if(shadowData.shadowStrength <= 0)
    {
        return 1;
    }
    
    float4x4 world2shadow = _DirectionalShadowLightMatrices[shadowData.tileIndexInShadowmap];
    float4 ws = float4(surface.positionWS, 1);
    // 将世界的z转换为光源阴影空间的z, 从而比对z
    float3 positionSTS = mul(world2shadow, ws).xyz;
    fixed shadow = SampleDirectionalShadowAtlas(positionSTS);
    return (fixed)lerp(1, shadow, shadowData.shadowStrength);
}

#endif
