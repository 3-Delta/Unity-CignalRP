#ifndef CRP_POSTPROCESS_INCLUDED
#define CRP_POSTPROCESS_INCLUDED

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

float4 GetSource(float2 screenUV)
{
    // return SAMPLE_TEXTURE2D(_PostProcessSource1, sampler_linear_clamp, screenUV);

    // SAMPLE_TEXTURE2D会自动选择一个合适的lod,而我们原纹理没有lod
    // 所以使用SAMPLE_TEXTURE2D_LOD设定一个固定的lod
    return SAMPLE_TEXTURE2D_LOD(_PostProcessSource1, sampler_linear_clamp, screenUV, 0);
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
    return GetSource(input.screenUV);
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
        color += GetSource(uv).rgb * weights[i];
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
        color += GetSource(uv).rgb * weights[i];
    }

    return float4(color, 1.0);
}


#endif
