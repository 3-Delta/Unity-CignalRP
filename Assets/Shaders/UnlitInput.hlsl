#ifndef CRP_UNLIT_INPUT_INCLUDED
#define CRP_UNLIT_INPUT_INCLUDED

// 默认纹理
TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

TEXTURE2D(_DistortionTexture);
SAMPLER(sampler_DistortionTexture);

UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
    UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)

    // 因为不受光,所以不需要
    // UNITY_DEFINE_INSTANCED_PROP(float, _Metallic)
    // UNITY_DEFINE_INSTANCED_PROP(float, _Smoothness)

    UNITY_DEFINE_INSTANCED_PROP(float, _ZWrite)

    UNITY_DEFINE_INSTANCED_PROP(float, _NearFadeDistance)
    UNITY_DEFINE_INSTANCED_PROP(float, _NearFadeRange)
    UNITY_DEFINE_INSTANCED_PROP(float, _SoftParticlesDistance)
    UNITY_DEFINE_INSTANCED_PROP(float, _SoftParticlesRange)

    UNITY_DEFINE_INSTANCED_PROP(float, _DistortionStrength)
    UNITY_DEFINE_INSTANCED_PROP(float, _DistortionBlend)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

#define GetInputProp(name) UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, name)

struct InputConfig
{
    float2 baseUV;
    float4 color;

    float3 flipBookUVB;
    bool useFlipBookBlend;

    Fragment fragment;
    bool nearFade;
    bool softParticles;
};

InputConfig GetInputConfig(float4 positionSS, float2 baseUV)
{
    InputConfig c;
    c.baseUV = baseUV;
    c.color = 1.0;
    c.flipBookUVB = 0.0;
    c.useFlipBookBlend = false;

    c.nearFade = false;
    c.softParticles = false;
    c.fragment = GetFragment(positionSS);
    return c;
}

float2 TransformBaseUV(float2 baseUV)
{
    float4 baseST = GetInputProp(_BaseMap_ST);
    return baseUV * baseST.xy + baseST.zw;
}

float4 GetBase(InputConfig input)
{
    float4 texelColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseUV);
    if(input.useFlipBookBlend)
    {
        float4 preTexelColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.flipBookUVB.xy);
        texelColor = lerp(texelColor, preTexelColor, input.flipBookUVB.z);
    }
    
    if(input.nearFade)
    {
        float nearAttenuation = (input.fragment.fragZview - GetInputProp(_NearFadeDistance)) / GetInputProp(_NearFadeRange);
        texelColor.a = saturate(nearAttenuation);
    }

    if(input.softParticles)
    {
        float depthDelta = input.fragment.bufferZview - input.fragment.fragZview;
        float nearAttenuation = (depthDelta - GetInputProp(_SoftParticlesDistance)) / GetInputProp(_SoftParticlesRange);
        texelColor.a = saturate(nearAttenuation);
    }
    
    float4 color = GetInputProp(_BaseColor);
    return texelColor * color * input.color;
}

// 自发光选项
float3 GetEmission(InputConfig input)
{
    return GetBase(input).rgb;
}

float GetCutoff(InputConfig input)
{
    return GetInputProp(_Cutoff);
}

float GetMetallic(InputConfig input)
{
    return 0.0;
}

float GetSmoothness(InputConfig input)
{
    return 0.0;
}

float GetFresnel(InputConfig c) {
    return 0.0;
}

// 扰动
float2 GetDistortion(InputConfig input)
{
    float4 texelColor = SAMPLE_TEXTURE2D(_DistortionTexture, sampler_DistortionTexture, input.baseUV);
    if(input.useFlipBookBlend)
    {
        float4 preTexelColor = SAMPLE_TEXTURE2D(_DistortionTexture, sampler_DistortionTexture, input.flipBookUVB.xy);
        texelColor = lerp(texelColor, preTexelColor, input.flipBookUVB.z);
    }
    return DecodeNormal(texelColor, GetInputProp(_DistortionStrength)).xy;
}

float GetDistortionBlend(InputConfig input)
{
    return GetInputProp(_DistortionBlend);
}

float GetFinalAlpha(float alpha)
{
    return GetInputProp(_ZWrite) ? 1.0 : alpha;
}

#endif
