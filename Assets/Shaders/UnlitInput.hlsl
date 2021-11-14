#ifndef CRP_UNLIT_INPUT_INCLUDED
#define CRP_UNLIT_INPUT_INCLUDED

// 默认纹理
TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
    UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)

    // 因为不受光,所以不需要
    // UNITY_DEFINE_INSTANCED_PROP(float, _Metallic)
    // UNITY_DEFINE_INSTANCED_PROP(float, _Smoothness)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

float2 TransformBaseUV(float2 baseUV)
{
    float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST);
    return baseUV * baseST.xy + baseST.zw;
}

float4 GetBase(float2 baseUV)
{
    float4 texelColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, baseUV);
    float4 color = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor);
    return texelColor * color;
}

float GetCutoff(float2 baseUV)
{
    return UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Cutoff);
}

float GetMetallic (float2 baseUV) {
    return 0.0;
}

float GetSmoothness (float2 baseUV) {
    return 0.0;
}

#endif
