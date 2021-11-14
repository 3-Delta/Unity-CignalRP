#ifndef CRP_META_PASS_INCLUDED
#define CRP_META_PASS_INCLUDED

#include "../ShaderLibrary/Light/Surface.hlsl"
#include "../ShaderLibrary/Light/Shadows.hlsl"
#include "../ShaderLibrary/Light/Light.hlsl"
#include "../ShaderLibrary/Light/BRDF.hlsl"

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

float4 MetaFragment(Varyings input) : SV_TARGET
{
    float4 base = GetBase(input.baseUV);

    FragSurface surface;
    ZERO_INITIALIZE(FragSurface, surface);
    surface.color = base.rgb;
    surface.metallic = GetMetallic(input.baseUV);
    surface.smoothness = GetSmoothness(input.baseUV);

    BRDF brdf = GetBRDF(surface);
    float4 meta = 0.0;
    return meta;
}

#endif
