﻿#ifndef CRP_INPUT_INCLUDED
#define CRP_INPUT_INCLUDED

// 定义cpu传递到gpu的矩阵
// 例如this.context.SetupCameraProperties(this.camera);
// unity为什么不一次性定义MVP矩阵呢?

// cbuffer的一些限制， 这个cbuffer中的参数，一般urp中没有显式传递，查找不到
// https://zhuanlan.zhihu.com/p/137455866
// https://zhuanlan.zhihu.com/p/378781638查看所有的内置的PerDraw属性
CBUFFER_START(UnityPerDraw)
	float4x4 unity_ObjectToWorld;
	float4x4 unity_WorldToObject;
	float4 unity_LODFade;
	real4 unity_WorldTransformParams;

	// 如果将vp. v矩阵放到这里，则会出现clip渲染不正确的现象

	// lightmap相关
	// 给vertex的每个lightmapUV传递scaleoffset, 为什么不在顶点属性中传递呢?
	// 对于纹理的scaleoffset好像也是外部传递的, 因为一个纹理中,所有的scaleoffset都应该是一样的,而不是每个vertex不一样
	float4 unity_LightmapST; 
	float4 unity_DynamicLightmapST;
CBUFFER_END

float4x4 unity_MatrixV;
float4x4 unity_MatrixVP;
float4x4 glstate_matrix_projection;

// urp中有传递
float3 _WorldSpaceCameraPos;

#endif
