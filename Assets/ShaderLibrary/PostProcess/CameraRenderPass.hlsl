#ifndef CRP_CAMERA_RENDER_INCLUDED
#define CRP_CAMERA_RENDER_INCLUDED

// 功能类似于blit

TEXTURE2D(_SourceRT);

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 screenUV : VAR_SCREEN_UV;
};

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

float4 CopyColorFragment(Varyings input) : SV_TARGET
{
    // 用linear的插值方式
    float4 color = SAMPLE_TEXTURE2D_LOD(_SourceRT, sampler_linear_clamp, input.screenUV, 0);
    return color;
}

float CopyDepthFragment(Varyings input) : SV_TARGET
{
    // 用point方式，不对depth进行插值，这也就是不支持MSAA的原因
    float4 color = SAMPLE_TEXTURE2D_LOD(_SourceRT, sampler_point_clamp, input.screenUV, 0);
    return color;
}

#endif
