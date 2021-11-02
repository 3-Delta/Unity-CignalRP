﻿#ifndef CRP_POSTPROCESS_INCLUDED
#define CRP_POSTPROCESS_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"

TEXTURE2D(_PostProcessSource1);
TEXTURE2D(_PostProcessSource2);
SAMPLER(sampler_linear_clamp);

float4 _PostProcessSource_TexelSize;

// unity会设置 
// 投影控制
float4 _ProjectionParams;

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 screenUV : VAR_SCREEN_UV;
};

float4 GetSourceTexelSize() 
{
    return _PostProcessSource_TexelSize;
}

float4 GetSource1(float2 screenUV)
{
    // return SAMPLE_TEXTURE2D(_PostProcessSource1, sampler_linear_clamp, screenUV);

    // SAMPLE_TEXTURE2D会自动选择一个合适的lod,而我们原纹理没有lod
    // 所以使用SAMPLE_TEXTURE2D_LOD设定一个固定的lod
    return SAMPLE_TEXTURE2D_LOD(_PostProcessSource1, sampler_linear_clamp, screenUV, 0);
}

float4 GetSource2(float2 screenUV)
{
    return SAMPLE_TEXTURE2D_LOD(_PostProcessSource2, sampler_linear_clamp, screenUV, 0);
}

// 上采样过程中,因为也是双线性过滤,会显得图像更向块状,所以使用三线性过滤
float4 GetSource1Bicubic(float2 screenUV) 
{
    return SampleTexture2DBicubic(
        TEXTURE2D_ARGS(_PostProcessSource1, sampler_linear_clamp), screenUV,
        _PostProcessSource_TexelSize.zwxy, 1.0, 0.0);
}

Varyings DefaultVertex(uint vertexID : SV_VertexID)
{
    Varyings output;
    float x = vertexID <= 1 ? -1 : 3;
    float y = vertexID == 1 ? 3 : -1;
    output.positionCS = float4(x, y, 0, 1);

    x = vertexID <= 1 ? 0 : 2;
    y = vertexID == 1 ? 2 : 0;
    output.screenUV = float2(x, y);

    if (_ProjectionParams.x < 0)
    {
        output.screenUV.y = 1 - output.screenUV.y;
    }

    return output;
}

float4 CopyFragment(Varyings input) : SV_TARGET
{
    return GetSource1(input.screenUV);
}

float4 BloomHorizontalFragment(Varyings input) : SV_TARGET
{
   float3 color = 0.0;
    float offsets[] = {
        -4.0, -3.0, -2.0, -1.0, 0.0, 1.0, 2.0, 3.0, 4.0
    };
    float weights[] = {
        // 杨辉三角形 确认权重
        0.01621622, 0.05405405, 0.12162162, 0.19459459, 0.22702703,
        0.19459459, 0.12162162, 0.05405405, 0.01621622
    };
    for (int i = 0; i < 9; i++) {
        // 将2* -> 1* 分辨率, 所以uv*2
        float offset = offsets[i] * 2.0 * GetSourceTexelSize().x;
        float2 uv = input.screenUV + float2(offset, 0.0);
        color += GetSource1(uv).rgb * weights[i];
    }

    return float4(color, 1.0);
}

float4 BloomVerticalFragment(Varyings input) : SV_TARGET {
    float3 color = 0.0;
    float offsets[] = {
        -3.23076923, -1.38461538, 0.0, 1.38461538, 3.23076923
    };
    float weights[] = {
        0.07027027, 0.31621622, 0.22702703, 0.31621622, 0.07027027
    };
    for (int i = 0; i < 5; i++) {
        // 不 *2
        float offset = offsets[i] * GetSourceTexelSize().y;
        float2 uv = input.screenUV + float2(0.0, offset);
        color += GetSource1(uv).rgb * weights[i];
    }

    return float4(color, 1.0);
}

bool _BloomBicubicUpsampling;
float _BloomIntensity;
float4 BloomCombineFragment(Varyings input) : SV_TARGET {
    float3 low;
    if (_BloomBicubicUpsampling) {
        low = GetSource1Bicubic(input.screenUV).rgb;
    }
    else {
        low = GetSource1(input.screenUV).rgb;
    }

    float3 high = GetSource2(input.screenUV).rgb;
    return float4(low * _BloomIntensity + high, 1.0);
}

float4 ToneMapACESFragment(Varyings input) : SV_TARGET{
    float4 color = GetSource1(input.screenUV);
    color.rgb = min(color.rgb, 60.0);
    color.rgb = AcesTonemap(unity_to_ACES(color.rgb));
    return color;
}

float4 ToneMapNeutralFragment(Varyings input) : SV_TARGET
{
    float4 color = GetSource1(input.screenUV);
    color.rgb = min(color.rgb, 60.0);
    color.rgb = NeutralTonemap(color.rgb);
    return color;
}

float4 ToneMapReinhardFragment(Varyings input) : SV_TARGET
{
    float4 color = GetSource1(input.screenUV);
    color.rgb = min(color.rgb, 60.0);
    color.rgb /= (color.rgb + 1.0);
    return color;
} 

#endif
