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
    float4 positionCS_SS : SV_POSITION;
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
    output.positionCS_SS = TransformWorldToHClip(positionWS);
    output.positionWS = positionWS;
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);

    output.baseUV = TransformBaseUV(input.baseUV);
    return output;
}

float4 LitPassFragment(Varyings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    // positionCS_SS在顶点着色器中是cs位置，但是在片元着色器中就是ss位置，因为顶点着色器之后的流程，引擎会自动将cs位置转换为屏幕位置
    // 屏幕位置就是左下为(0, 0)， size为1的viewport空间
    InputConfig config = GetInputConfig(input.positionCS_SS, input.baseUV);
    float4 base = GetBase(config);

#if defined(_CLIPPING)
    clip(base.a - GetCutoff(config));
#endif

    // 填充surface
    FragSurface surface;
    surface.positionWS = input.positionWS;
#if defined(_NORMAL_MAP)
    surface.normalWS = NormalTangentToWorld(GetNormalTS(config), input.normalWS, input.tangentWS);
    surface.interpolatedNormal = input.normalWS;
#else
    surface.normalWS = normalize(input.normalWS);
    surface.interpolatedNormal = surface.normalWS;
#endif
    surface.color = base.rgb;
    surface.alpha = base.a;
    surface.viewDirectionWS = normalize(_WorldSpaceCameraPos - input.positionWS);
    surface.depthVS = -TransformWorldToView(input.positionWS).z;
    surface.metallic = GetMetallic(config);
    surface.smoothness = GetSmoothness(config);
    surface.fresnalStrength = GetFresnal(config);
    surface.meshRenderingLayerMask = asuint(unity_RenderingLayer.x);

    // 填充BRDF， brdf是用来计算直接光的，
    // 间接光其实是通过烘焙来的，烘焙的时候会使用brdf计算漫反射
    BRDF brdf = GetBRDF(surface);
    
    GI gi = GetGI(GI_FRAGMENT_DATA(input), surface, brdf);

    // realtime光 + gi光
    float3 color = GetLighting(surface, brdf, gi);
    
    // 其实自发光在meta中已经贡献了一部分GI，会给周边投射自己的光线形成GI
    // 这里继续贡献一部分非GI
    color += GetEmission(config);
    return float4(color, GetFinalAlpha(surface.alpha));
}

#endif
