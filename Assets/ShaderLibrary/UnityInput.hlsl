#ifndef CRP_UNITY_INPUT_INCLUDED
#define CRP_UNITY_INPUT_INCLUDED

// 定义cpu传递到gpu的矩阵
// 例如this.context.SetupCameraProperties(this.camera);
// unity为什么不一次性定义MVP矩阵呢?

CBUFFER_START(UnityPerDraw)
	float4x4 unity_ObjectToWorld;
	float4x4 unity_WorldToObject;
	float4 unity_LODFade;
	real4 unity_WorldTransformParams;

	// 如果将vp. v矩阵放到这里，则会出现clip渲染不正确的现象， 应该是和gpuinstance等功能有关系
CBUFFER_END

float4x4 unity_MatrixV;
float4x4 unity_MatrixVP;
float4x4 glstate_matrix_projection;

#endif
