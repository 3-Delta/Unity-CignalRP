﻿#ifndef CRP_POSTPROCESS_INCLUDED
#define CRP_POSTPROCESS_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"

TEXTURE2D(_PostProcessSource1);
TEXTURE2D(_PostProcessSource2);

float4 _PostProcessSource_TexelSize;

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

    if (_ProjectionParams.x < 0) // dx和opengl屏幕坐标原点不一致，导致y刚好是 1和 的关系
    {
        output.screenUV.y = 1 - output.screenUV.y;
    }

    return output;
}

float4 CopyColorFragment(Varyings input) : SV_TARGET
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
    for (int i = 0; i < 9; i++)
    {
        // 将2* -> 1* 分辨率, 所以uv*2
        float offset = offsets[i] * 2.0 * GetSourceTexelSize().x;
        float2 uv = input.screenUV + float2(offset, 0.0);
        color += GetSource1(uv).rgb * weights[i];
    }

    return float4(color, 1.0);
}

float4 BloomVerticalFragment(Varyings input) : SV_TARGET
{
    float3 color = 0.0;
    float offsets[] = {
        -3.23076923, -1.38461538, 0.0, 1.38461538, 3.23076923
    };
    float weights[] = {
        0.07027027, 0.31621622, 0.22702703, 0.31621622, 0.07027027
    };
    for (int i = 0; i < 5; i++)
    {
        // 不 *2
        float offset = offsets[i] * GetSourceTexelSize().y;
        float2 uv = input.screenUV + float2(0.0, offset);
        color += GetSource1(uv).rgb * weights[i];
    }

    return float4(color, 1.0);
}

bool _BloomBicubicUpsampling;
float _BloomIntensity;

float4 BloomCombineFragment(Varyings input) : SV_TARGET
{
    float3 low;
    if (_BloomBicubicUpsampling)
    {
        low = GetSource1Bicubic(input.screenUV).rgb;
    }
    else
    {
        low = GetSource1(input.screenUV).rgb;
    }

    float4 high = GetSource2(input.screenUV);
    return float4(low * _BloomIntensity + high.rgb, high.a);
}

float Luminance(float3 color, bool useACES)
{
    color = useACES ? AcesLuminance(color) : Luminance(color);
    return color;
}

float4 _ColorAdjust;
float4 _ColorFilter;

// 曝光
float3 ColorGradePostExposure(float3 color)
{
    // 曝光度就是:亮度???
    return color * _ColorAdjust.x;
}

// 对比度
float3 ColorGradeContrast(float3 color, bool useACES)
{
    color = useACES ? ACES_to_ACEScc(unity_to_ACES(color)) : LinearToLogC(color);
    color = (color - ACEScc_MIDGRAY) * _ColorAdjust.y + ACEScc_MIDGRAY;
    color = useACES ? ACES_to_ACEScg(ACEScc_to_ACES(color)) : LogCToLinear(color);
    return color;
}

// 滤镜, 就是颜色相乘,比如红色*蓝色就是黑色, 也就是红色球体不反射蓝色光线
float3 ColorGradeColorFilter(float3 color)
{
    color *= _ColorFilter.rgb;
    return color;
}

// hueshift
float3 ColorGradeHueShift(float3 color)
{
    color = RgbToHsv(color);
    float hue = color.x + _ColorAdjust.z;
    color.x = RotateHue(hue, 0.0, 1.0);
    color = HsvToRgb(color);
    return color;
}

// saturation https://www.cnblogs.com/crazylights/p/3957566.html
// rgb越接近,饱和度越小
float3 ColorGradeSaturation(float3 color, bool useACES)
{
    // 灰度值
    float luminance = Luminance(color, useACES);
    float3 diff = color - luminance;
    diff *= _ColorAdjust.w;
    color = luminance + diff;
    return color;
}

float4 _WhiteBalance;

float3 ColorGradeWhiteBalance(float3 color)
{
    color = LinearToLMS(color);
    color *= _WhiteBalance.rgb;
    color = LMSToLinear(color);
    return color;
}

float4 _SplitToneShadow;
float4 _SplitToneSpecular;

float3 ColorGradeSplitTone(float3 color, bool useACES)
{
    // 故意先转到gamma空间操作，为了适配adobe产品的SplitTone
    color = PositivePow(color, 1.0 / 2.2);
    float luminance = Luminance(saturate(color), useACES);
    float rate = saturate(luminance + _SplitToneShadow.w);

    float3 shadow = lerp(0.5, _SplitToneShadow.rgb, 1 - rate);
    float3 specular = lerp(0.5, _SplitToneSpecular.rgb, rate);

    color = SoftLight(color, shadow);
    color = SoftLight(color, specular);
    color = PositivePow(color, 2.2);
    return color;
}

float4 _ChannelMixerRed;
float4 _ChannelMixerGreen;
float4 _ChannelMixerBlue;

float3 ColorGradeChannelMixer(float3 color)
{
    // 其实就是通过矩阵相乘，将color的某些chanel进行结合
    float3x3 m = float3x3(_ChannelMixerRed.rgb, _ChannelMixerGreen.rgb, _ChannelMixerBlue.rgb);
    color = mul(m, color);
    return color;
}

float4 _SMHShadow;
float4 _SMHMidtone;
float4 _SMHSpecular;
float4 _SMHRange;

float3 ColorGradeSMH(float3 color, bool useACES)
{
    float luminance = Luminance(color, useACES);

    float shadowWeight = 1.0 - smoothstep(_SMHRange.x, _SMHRange.y, luminance);
    float specularWeight = smoothstep(_SMHRange.z, _SMHRange.w, luminance);
    float midtoneWeight = 1.0 - shadowWeight - specularWeight;

    color = color * _SMHShadow.rgb * shadowWeight +
        color * _SMHMidtone.rgb * midtoneWeight +
        color * _SMHSpecular.rgb * specularWeight;
    return color;
}

// 支持缺省
float3 ColorGrade(float3 color, bool useACES = false)
{
    color = ColorGradePostExposure(color);
    color = ColorGradeWhiteBalance(color);
    color = ColorGradeContrast(color, useACES);
    color = max(color, 0.0);

    color = ColorGradeColorFilter(color);
    color = max(color, 0.0);

    color = ColorGradeSplitTone(color, useACES);
    color = ColorGradeChannelMixer(color);
    color = max(color, 0.0);
    color = ColorGradeSMH(color, useACES);
    color = ColorGradeHueShift(color);

    color = ColorGradeSaturation(color, useACES);
    color = max(color, 0.0);

    return color;
}

float4 _ColorGradeLUTParams;
bool _ColorGradeLUTInLogC;

float3 GetColorGradeLUT(float2 uv, bool useACES = false)
{
    float3 color = GetLutStripValue(uv, _ColorGradeLUTParams);
    color = _ColorGradeLUTInLogC ? LogCToLinear(color) : color;
    color = ColorGrade(color, useACES);
    return color;
}

float4 ColorGradeNoneFragment(Varyings input) : SV_TARGET
{
    float3 color = GetColorGradeLUT(input.screenUV);
    return float4(color, 1.0);
}

float4 ColorGradeACESFragment(Varyings input) : SV_TARGET
{
    float3 color = GetColorGradeLUT(input.screenUV, true);
    color = AcesTonemap(color);
    return float4(color, 1.0);
}

float4 ColorGradeNeutralFragment(Varyings input) : SV_TARGET
{
    float3 color = GetColorGradeLUT(input.screenUV);
    color = NeutralTonemap(color);
    return float4(color, 1.0);
}

float4 ColorGradeReinhardFragment(Varyings input) : SV_TARGET
{
    float3 color = GetColorGradeLUT(input.screenUV);
    color /= (color + 1.0);
    return float4(color, 1.0);
}

TEXTURE2D(_ColorGradeLUT);

float3 ApplyColorGradeLUT(float3 color)
{
    color = _ColorGradeLUTInLogC ? LinearToLogC(color) : color;
    return ApplyLut2D(_ColorGradeLUT, sampler_linear_clamp, saturate(color), _ColorGradeLUTParams.xyz);
}

float4 FinalFragment(Varyings input) : SV_TARGET
{
    float4 color = GetSource1(input.screenUV);
    color.rgb = ApplyColorGradeLUT(color.rgb);
    return color;
}

bool _UseBicubicRescale;

float4 FinalScaleFragment(Varyings input) : SV_TARGET
{
    if(_UseBicubicRescale)
    {
        return GetSource1Bicubic(input.screenUV);
    }
    else
    {
        return GetSource1(input.screenUV);
    }
}

#endif
