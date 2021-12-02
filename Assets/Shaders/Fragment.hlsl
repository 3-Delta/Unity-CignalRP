#ifndef CRP_FRAGMENT_INCLUDED
#define CRP_FRAGMENT_INCLUDED

struct Fragment
{
    float2 positionSS; // 屏幕空间位置,uv整体在左下角做了(0.5, 0.5)的平移
    float fragDepth; // 距离相机xy平面,而不是近平面的距离
};

Fragment GetFragment(float4 positionSS)
{
    Fragment fg;
    fg.positionSS = positionSS.xy;
    // 正交矩阵的w永远为1，不能正确表达depth 所以手动计算
    fg.fragDepth = IsOrthoCamera() ? OrthoDepthBufferToLinear(positionSS.z) : positionSS.w;
    return fg;
}

#endif
