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
    bool useFlipBookBlend; // 序列帧粒子动画

    Fragment fragment;
    bool nearFade;
    bool softParticles;
};

InputConfig GetInputConfig(float4 fragPositionSS, float2 baseUV)
{
    InputConfig c;
    c.baseUV = baseUV;
    c.color = 1.0;
    c.flipBookUVB = 0.0;
    c.useFlipBookBlend = false;

    c.nearFade = false;
    c.softParticles = false;
    c.fragment = GetFragment(fragPositionSS);
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
    if(input.useFlipBookBlend) // 序列帧粒子动画, 因为是同一张纹理中有n*n的子图，比如爆炸图，所以render知道当前的uv和上一次的uv
    {
        float4 preTexelColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.flipBookUVB.xy);
        texelColor = lerp(texelColor, preTexelColor, input.flipBookUVB.z);
    }
    
    if(input.nearFade) // 距离相位置一定距离,开始fade， 而不是距离相机近平面一定距离
    {
        float diff = (input.fragment.zVS - GetInputProp(_NearFadeDistance));
        float nearAttenuation = diff / GetInputProp(_NearFadeRange);
        // 随着zVS变化，修改alpha
        texelColor.a = saturate(nearAttenuation);
    }

    // 软粒子 https://zhuanlan.zhihu.com/p/347081329
    // 粒子系统用来渲染烟雾、灰尘、火焰和爆炸等体积效果，通常通过使用alpha混合多层和屏幕对齐的方块纹理来实现，但有个缺陷是当方块和几何体相交时会造成明显接缝。软粒子就是用来隐藏接缝的技术，技术原理是对深度缓存采样然后在粒子接近几何体时使粒子衰退，从而造成无缝的效果。
    // 为什么有接缝：粒子在opaque之前则展示粒子和opaque的blend效果，否则展示opaque，这自然会在粒子和opaque接触的边缘有明显接缝
    // 为了渐变这个接缝，可以把在opaque之前的粒子按照距离opaque的距离远近修改粒子的alpha，越靠近opaque则alpha越小即可
    if(input.softParticles)
    {
        float depthDelta = input.fragment.zbufferVS - input.fragment.zVS;
        float nearAttenuation = (depthDelta - GetInputProp(_SoftParticlesDistance)) / GetInputProp(_SoftParticlesRange);
        // frag的z和zbuffer越接近，alpha越小
        texelColor.a *= saturate(nearAttenuation);
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
