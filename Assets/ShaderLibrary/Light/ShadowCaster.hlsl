#ifndef CRP_SHADOW_CASTER_INCLUDED
#define CRP_SHADOW_CASTER_INCLUDED

struct Attributes {
    float3 positionOS : POSITION;
    float2 baseUV : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID 
};

struct Varyings {
    float4 positionCS : SV_POSITION;
    float2 baseUV : VAR_BASE_UV;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

bool _ShadowPancaking;

Varyings ShadowCasterPassVertex(Attributes input)
{
    Varyings output;

    UNITY_SETUP_INSTANCE_ID(input);
    // 类似于 output.instanceID = input.instanceID
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    float3 positionWS = TransformObjectToWorld(input.positionOS);
    output.positionCS = TransformWorldToHClip(positionWS);

    if(_ShadowPancaking)
    {
        #if UNITY_REVERSED_Z
        output.positionCS.z = min(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
        #else
        // -w <= z <= w 属于ndc中有效片元
        // 这里影响shadowmap的形成，其实就是在被裁剪的区域也有shadowmap形成，最终采样的时候不形成镂空的阴影
        // https://answer.uwa4d.com/question/617fa23f8f8c834241fbbd7a
        // UNITY_NEAR_CLIP_VALUE，近裁剪面NDC空间的Z值，DX下为1，OpenGL为-1
        output.positionCS.z = max(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
        #endif
    }
    
    output.baseUV = TransformBaseUV(input.baseUV);
    return output;
}

// 片元着色器可以返回void，但是也必须不设置 SV_TARGET
void ShadowCasterPassFragment(Varyings input)
{
    UNITY_SETUP_INSTANCE_ID(input);
    InputConfig config = GetInputConfig(input.baseUV);
    float4 base = GetBase(config);

    // 因为需要对于alphatest的镂空区域过滤，否则镂空区域会进入shadowmap,造成阴影错误
    #if defined(_SHADOWS_CLIP)
        float cutoff = GetCutoff(config);
        clip(base.a - cutoff);
    #elif defined(_SHADOWS_DITHER)
        float dither = InterleavedGradientNoise(input.positionCS.xy, 0);
        clip(base.a - dither);
    #endif
}

#endif
