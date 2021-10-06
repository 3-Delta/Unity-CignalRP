#ifndef CRP_INPUT_INCLUDED
#define CRP_INPUT_INCLUDED

// 定义cpu传递到gpu的矩阵
// 例如this.context.SetupCameraProperties(this.camera);
// unity为什么不一次性定义MVP矩阵呢?

// cbuffer的一些限制， 这个cbuffer中的参数，一般urp中没有显式传递，查找不到
// https://zhuanlan.zhihu.com/p/137455866
CBUFFER_START(UnityPerDraw)
	float4x4 unity_ObjectToWorld;
	float4x4 unity_WorldToObject;
	float4 unity_LODFade;
	real4 unity_WorldTransformParams;

	// 如果将vp. v矩阵放到这里，则会出现clip渲染不正确的现象
CBUFFER_END

float4x4 unity_MatrixV;
float4x4 unity_MatrixVP;
float4x4 glstate_matrix_projection;

// urp中有传递
float3 _WorldSpaceCameraPos;

#endif
