#ifndef CRP_INPUT_INCLUDED
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

	// lightprobo 
	// r, g, b
	// A, B, C 可能是因为三阶球协的原因,这里只有A/B/C三个
	float4 unity_SHAr;
	float4 unity_SHAg;
	float4 unity_SHAb;
	float4 unity_SHBr;
	float4 unity_SHBg;
	float4 unity_SHBb;
	float4 unity_SHC;

	// lppv相关
	float4 unity_ProbeVolumeParams;
	float4x4 unity_ProbeVolumeWorldToObject;
	float4 unity_ProbeVolumeSizeInv;
	float4 unity_ProbeVolumeMin;

	// shadowmask对应的动态物体的 probe
	float4 unity_ProbesOcclusion;
	float4 unity_SpecCube0_HDR;

	// 逐像素光源
	real4 unity_LightData; // y:灯光数量
	real4 unity_LightIndices[2]; // 共支持8个光源,每个是一个otherLightIndex

	// mesh renderer的layermask
	float4 unity_RenderingLayer;

	float4 _ProjectionParams;
	// 正交相机信息,如果是正交相机，w为1，否则0
	float4 unity_OrthoParams;
	float4 _ScreenParams; // xy是宽高
	float4 _ZBufferParams;
CBUFFER_END

float4x4 unity_MatrixV;	// v
float4x4 unity_MatrixVP;	// vp
float4x4 glstate_matrix_projection; // p矩阵

// urp中有传递
float3 _WorldSpaceCameraPos;

#endif
