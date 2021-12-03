#ifndef CRP_FRAGMENT_INCLUDED
#define CRP_FRAGMENT_INCLUDED

#include "../ShaderLibrary/UnityInput.hlsl"

TEXTURE2D(_CameraColorRT);
TEXTURE2D(_CameraDepthRT);

struct Fragment
{
    float2 positionSS; // 屏幕空间位置,uv整体在左下角做了(0.5, 0.5)的平移
    float zToCamera; // 距离相机xy平面,而不是近平面的距离

    float2 scerrnUV; // 屏幕空间uv
    float zBuffer; // 深度缓冲
};

Fragment GetFragment(float4 positionSS)
{
    Fragment fg;
    fg.positionSS = positionSS.xy;
    // 正交矩阵的w永远为1，不能正确表达depth, 透视的w则是-z
    fg.zToCamera = IsOrthoCamera() ? OrthoDepthBufferToLinear(positionSS.z) : positionSS.w;

    fg.scerrnUV = fg.positionSS / _ScreenParams.xy;
    
    float zToNear = SAMPLE_DEPTH_TEXTURE_LOD(_CameraDepthRT, sampler_point_clamp, fg.scerrnUV, 0);
    fg.zBuffer = IsOrthoCamera() ? OrthoDepthBufferToLinear(zToNear) : LinearEyeDepth(zToNear, _ZBufferParams);
    return fg;
}

float4 GetBufferColor(Fragment fg, float2 uvOffset = float2(0.0, 0.0))
{
    float2 uv = fg.scerrnUV + uvOffset;
    float4 color = SAMPLE_TEXTURE2D_LOD(_CameraColorRT, sampler_CameraColorRT, uv, 0);
}

#endif
