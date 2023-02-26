#ifndef CRP_SHADOW_CASTER_INCLUDED
#define CRP_SHADOW_CASTER_INCLUDED

struct Attributes {
    float3 positionOS : POSITION;
    float2 baseUV : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID 
};

struct Varyings {
    float4 positionCS_SS : SV_POSITION;
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
    output.positionCS_SS = TransformWorldToHClip(positionWS);

    // 阴影平坠其实是为了尽可能的减少光源位置的虚拟相机的视锥体的近裁剪和远裁剪的距离，也就是尽量让近裁剪靠近远裁剪
    // 这就可能导致一些长条形物体的z不被渲染到shadowmap中,但是正常camera中却会渲染该物体。
    // 最终导致该物体看起来没有投射阴影，或者投射了不完整的阴影
    if(_ShadowPancaking)
    {
#if UNITY_REVERSED_Z
        output.positionCS_SS.z = min(output.positionCS_SS.z, output.positionCS_SS.w * UNITY_NEAR_CLIP_VALUE);
#else
        // -w <= z <= w 属于ndc中有效片元
        // 这里影响shadowmap的形成，其实就是在被裁剪的区域也有shadowmap形成，最终采样的时候不形成镂空的阴影
        // https://answer.uwa4d.com/question/617fa23f8f8c834241fbbd7a
        // UNITY_NEAR_CLIP_VALUE，近裁剪面NDC空间的Z值，DX下为1，OpenGL为-1
        output.positionCS_SS.z = max(output.positionCS_SS.z, output.positionCS_SS.w * UNITY_NEAR_CLIP_VALUE);
#endif
    }
    
    output.baseUV = TransformBaseUV(input.baseUV);
    return output;
}

// 片元着色器可以返回void，但是也必须不设置 SV_TARGET
void ShadowCasterPassFragment(Varyings input)
{
    UNITY_SETUP_INSTANCE_ID(input);
    InputConfig config = GetInputConfig(input.positionCS_SS, input.baseUV); 
    float4 base = GetBase(config);

    // 因为需要对于alphatest的镂空区域过滤，否则镂空区域会进入shadowmap,造成阴影错误
    #if defined(_SHADOWS_CLIP)
        float cutoff = GetCutoff(config);
        clip(base.a - cutoff);
    #elif defined(_SHADOWS_DITHER)
        float dither = InterleavedGradientNoise(input.positionCS_SS.xy, 0);
        clip(base.a - dither);
    #endif
}

#endif
