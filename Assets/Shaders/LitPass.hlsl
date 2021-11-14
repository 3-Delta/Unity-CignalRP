#ifndef CRP_LIT_PASS_INCLUDED
#define CRP_LIT_PASS_INCLUDED

#include "../ShaderLibrary/Common.hlsl"
#include "../ShaderLibrary/Light/Surface.hlsl"
#include "../ShaderLibrary/Light/Shadows.hlsl"
#include "../ShaderLibrary/Light/Light.hlsl"
#include "../ShaderLibrary/Light/Lighting.hlsl"
#include "../ShaderLibrary/Light/BRDF.hlsl"
#include "../ShaderLibrary/Light/GI.hlsl"

struct Attributes {
    float3 positionOS : POSITION;
    float2 baseUV : TEXCOORD0;

    float3 normalOS : NORMAL;
    
    // https://www.xuanyusong.com/archives/4633
    // 烘焙Lightmap以后unity会自动给参与烘焙的所有mesh添加uv2的属性，例如，三角形每个顶点都会有UV2它记录着这个每个顶点对应Lightmap图中的UV值
    // 这样拥有3个顶点的三角形面就可以通过UV2在Lightmap中线性采样烘焙颜色了。
    GI_ATTRIBUTE_DATA	// float2 lightMapUV : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID 
};

struct Varyings {
    float4 positionCS : SV_POSITION;
    float3 positionWS : VAR_POSITION;
    float2 baseUV : VAR_BASE_UV;

    float3 normalWS : VAR_NORMAL;

    GI_VARYINGS_DATA
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings LitPassVertex(Attributes input)
{
    Varyings output;

    UNITY_SETUP_INSTANCE_ID(input);
    // 类似于 output.instanceID = input.instanceID
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    TRANSFER_GI_DATA(input, output);
    
    float3 positionWS = TransformObjectToWorld(input.positionOS);
    output.positionCS = TransformWorldToHClip(positionWS);
    output.positionWS = positionWS;
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);

    output.baseUV = TransformBaseUV(input.baseUV);
    return output;
}

float4 LitPassFragment(Varyings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    float4 base = GetBase(input.baseUV);

    #if defined(_CLIPPING)
        clip(base.a - GetCutoff(input.baseUV));
    #endif

    // 填充surface
    FragSurface surface;
    surface.positionWS = input.positionWS;
    surface.normalWS = normalize(input.normalWS);
    surface.color = base.rgb;
    surface.alpha = base.a;
    surface.viewDirectionWS = normalize(_WorldSpaceCameraPos - input.positionWS);
    surface.depthVS = -TransformWorldToView(input.positionWS).z;
    surface.metallic = GetMetallic(input.baseUV);
    surface.smoothness = GetSmoothness(input.baseUV);
    
    GI gi = GetGI(GI_FRAGMENT_DATA(input), surface);
    // 填充BRDF
    BRDF brdf = GetBRDF(surface);
    float3 color = GetLighting(surface, brdf, gi);
    return float4(color, surface.alpha);
}

#endif
