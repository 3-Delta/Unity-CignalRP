#ifndef CRP_UNLIT_PASS_INCLUDED
#define CRP_UNLIT_PASS_INCLUDED

#include "../ShaderLibrary/Common.hlsl"

float4 UnlitPassVertex(float3 positionOS : POSITION) : SV_POSITION
{
    // 对于点来说，三维变四维的时候，需要第四维为1，对于方向来说，三维变四维的时候，需要第四维为0
    // 因为在平移变换的时候，方向不受到平移的影响，而三维变四维也主要是需要进行平移变换。
    return float4(positionOS, 1);
}

float4 UnlitPassFragment(float4 vertex : SV_POSITION) : SV_Target
{
    return float4(0, 0, 0, 1);
}

#endif
