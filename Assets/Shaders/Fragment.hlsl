#ifndef CRP_FRAGMENT_INCLUDED
#define CRP_FRAGMENT_INCLUDED

#include "../ShaderLibrary/UnityInput.hlsl"

TEXTURE2D(_CameraColorRT);
TEXTURE2D(_CameraDepthRT);

float4 _CameraRenderSize;
// = _CameraRenderSize = (1f / renderSize.x, 1f / renderSize.y, renderSize.x, renderSize.y)

struct Fragment
{
    float2 positionSS; // 起点是左下的(0,0), 边长为1的矩形
    float2 screenUV; // 屏幕空间uv
    
    // 《Shader入门精要》P270中可以得知，zbuffer最小0就是near, 最大1就是far,也就是说zbuffer是到相机位置的距离，而不是到近裁剪面的距离
    float fragZview; // 观察空间的z,当前片元的depth
    float bufferZview; // 观察空间的z, zbuffer中的depth，后续会比较fragZview，进行过度，或者z-test操作
};

// http://scarletsky.github.io/2021/03/06/gl-depth-transformation/
// https://zhuanlan.zhihu.com/p/258036220
// https://www.zhihu.com/question/377102103/answer/1068061765
// https://zhuanlan.zhihu.com/p/92315967 最合理解释
// 片元着色器的positionCS_SS,也就是屏幕空间位置 
Fragment GetFragment(float4 positionSS)
{
    Fragment fg;
    fg.positionSS = positionSS.xy;
    // 正交矩阵的w永远为1,所以透视除法之后和原来一样, 所以z不能正确表达depth
    // 透视的w则是-z
    // <shader入门精要> P.79
    fg.fragZview = IsOrthoCamera() ? OrthoDepthBufferToLinear(positionSS.z) : positionSS.w;

    // 因为——ScreenParams的width, height是针对rendertarget的宽高，还需要考虑到后处理的renderscale的影响
    // 位置/宽高 = uv
    fg.screenUV = fg.positionSS * _CameraRenderSize.xy;
    
    float texelZ = SAMPLE_DEPTH_TEXTURE_LOD(_CameraDepthRT, sampler_point_clamp, fg.screenUV, 0);
    fg.bufferZview = IsOrthoCamera() ? OrthoDepthBufferToLinear(texelZ) : LinearEyeDepth(texelZ, _ZBufferParams);
    return fg;
}

float4 GetBufferColor(Fragment fg, float2 uvOffset = float2(0.0, 0.0))
{
    float2 uv = fg.screenUV + uvOffset;
    float4 color = SAMPLE_TEXTURE2D_LOD(_CameraColorRT, sampler_CameraColorRT, uv, 0);
}

#endif
