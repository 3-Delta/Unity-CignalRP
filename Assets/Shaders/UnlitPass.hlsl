#ifndef CRP_UNLIT_PASS_INCLUDED
#define CRP_UNLIT_PASS_INCLUDED

#include "../ShaderLibrary/Common.hlsl"

// UNITY_VERTEX_INPUT_INSTANCE_ID 其实就是： uint instanceID;
// 因为gpuinstane就是针对每个渲染物体，而不是每个顶点，做的将mesh以及材质，transform等数据存储到gpu中
// 所以渲染的时候，需要根据顶点拿到这个顶点需要的渲染数据。
// 可能在预处理的时候，将顶点id和gpu中的渲染队列数据建立了联系，所以这里顶点中定义一个id去索引到gpu显存中的
// 渲染设置数据

/*
// srpbatcher使用
CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor;
CBUFFER_END

为什么下面的gouinstance的结构定义之后，居然还可以支持srpbatcher？
因为 在支持instance并且instaceon宏开启的时候，定义是数组，否则定义就是一个cbuffer,也就是为了srpbatcher定义的cbuffer
#if defined(UNITY_SUPPORT_INSTANCING) && defined(INSTANCING_ON)
    #define UNITY_INSTANCING_ENABLED
#endif

#if !defined(UNITY_INSTANCING_ENABLED)
    #define UNITY_INSTANCING_BUFFER_START(buf)          CBUFFER_START(buf)
    #define UNITY_INSTANCING_BUFFER_END(arr)            CBUFFER_END
#else
    定义一个数组
#endif
*/

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

struct Attributes
{
    float3 positionOS : POSITION;
    float4 color : COLOR;
    float2 baseUV : TEXCOORD0;

    // 其实就是：uint instanceID; 这里是针对每个顶点做了和显存渲染数据的联系
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
#if defined(_VERTEX_COLOR)
    float4 color : VAR_COLOR;
#endif
    float2 baseUV : VAR_BASE_UV;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings UnlitPassVertex(Attributes input)
{
    // 对于点来说，三维变四维的时候，需要第四维为1，对于方向来说，三维变四维的时候，需要第四维为0
    // 因为在平移变换的时候，方向不受到平移的影响，而三维变四维也主要是需要进行平移变换。
    // return float4(positionOS, 1);

    Varyings output;

    // 类似于 static uint unity_InstanceID = input.instanceID + baseId;
    UNITY_SETUP_INSTANCE_ID(input);
    // 类似于 output.instanceID = input.instanceID
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    float3 positionWS = TransformObjectToWorld(input.positionOS);
    output.positionCS = TransformWorldToHClip(positionWS);

#if defined(_VERTEX_COLOR)
    output.color = input.color;
#endif
    float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST);
    output.baseUV = input.baseUV * baseST.xy + baseST.zw;
    return output;
}

float4 UnlitPassFragment(Varyings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    float4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseUV);
    // 类似于 arrayUnityPerMaterial[unity_InstanceID]._BaseColor
    // 根据一个vertex的static获取显存数据
    float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor);
    float4 base = baseMap * baseColor;

    #if defined(_CLIPPING)
    clip(base.a - UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Cutoff));
    #endif

    return base;
}

#endif
