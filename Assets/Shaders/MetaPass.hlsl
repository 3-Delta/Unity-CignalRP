#ifndef CRP_META_PASS_INCLUDED
#define CRP_META_PASS_INCLUDED

#include "../ShaderLibrary/Light/Surface.hlsl"
#include "../ShaderLibrary/Light/Shadows.hlsl"
#include "../ShaderLibrary/Light/Light.hlsl"
#include "../ShaderLibrary/Light/BRDF.hlsl"
#include "../ShaderLibrary/UnityInput.hlsl"

// https://zhuanlan.zhihu.com/p/337121368
// 设置了X标志，则要求使用漫反射率
bool4 unity_MetaFragmentControl;
float unity_OneOverOutputBoost;
float unity_MaxOutputValue;

struct Attributes
{
    float3 positionOS : POSITION;
    float2 baseUV : TEXCOORD0;
    float3 lightmapUV : TEXCOORD1;
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 baseUV : VAR_BASE_UV;
};

Varyings MetaVertex(Attributes input)
{
    Varyings output;
    input.positionOS.xy = input.lightmapUV * unity_LightmapST.xy + unity_LightmapST.zw;
    input.positionOS.z = input.positionOS.z > 0.0 ? FLT_MIN : 0.0;
    
    output.positionCS = TransformWorldToHClip(input.positionOS);
    output.baseUV = TransformBaseUV(input.baseUV);
    return output;
}

// 烘培到lightmap中
float4 MetaFragment(Varyings input) : SV_TARGET
{
    InputConfig config = GetInputConfig(input.baseUV);
    float4 base = GetBase(config);

    FragSurface surface;
    ZERO_INITIALIZE(FragSurface, surface);
    surface.color = base.rgb;
    surface.metallic = GetMetallic(config);
    surface.smoothness = GetSmoothness(config);

    BRDF brdf = GetBRDF(surface);
    float4 meta = 0.0;
    // x表示使用漫反射烘焙
    if (unity_MetaFragmentControl.x) {
        meta = float4(brdf.diffuse, 1.0);
        meta.rgb += brdf.specular * brdf.roughness * 0.5;
        meta.rgb = min(PositivePow(meta.rgb, unity_OneOverOutputBoost), unity_MaxOutputValue);
    }
    // y表示使用自发光烘焙
    else if (unity_MetaFragmentControl.y) {
        meta = float4(GetEmission(config), 1.0);
    }
    return meta;
}

#endif
