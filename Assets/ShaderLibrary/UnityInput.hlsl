#ifndef CRP_INPUT_INCLUDED
#define CRP_INPUT_INCLUDED

// 定义cpu传递到gpu的矩阵
// 例如this.context.SetupCameraProperties(this.camera);
// unity为什么不一次性定义MVP矩阵呢?

// cbuffer的一些限制， 这个cbuffer中的参数，一般urp中没有显式传递，查找不到
// https://zhuanlan.zhihu.com/p/137455866
// https://zhuanlan.zhihu.com/p/378781638查看所有的内置的PerDraw属性
// cbuffer的使用中，需要注意的一些潜规则：https://www.xuanyusong.com/archives/4932 https://zhuanlan.zhihu.com/p/560076693

// 其实更应该叫做UnityPerObject, 也就是针对每个meshrender的，所以vp矩阵不在这里定义，o2w在这里， lightmap相关在这里
CBUFFER_START(UnityPerDraw)
	float4x4 unity_ObjectToWorld;
	float4x4 unity_WorldToObject;
	float4 unity_LODFade;
	real4 unity_WorldTransformParams;

	// 如果将vp. v矩阵放到这里，则会出现clip渲染不正确的现象

	// lightmap相关
	// 给vertex的每个lightmapUV传递scaleoffset, 为什么不在UnityPerMaterial中定义呢？
	// 正常一个纹理的ST，每个文素ST都应该是一样的，所以都是在UnityPerMaterial定义，
	// 但是lightmap比较特殊，他是很多个物体的烘焙光组合在一起的，每个物体都有自己的ST，所以不能放到UnityPerMaterial
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

	// y,near, z, far, w = 1+1/far
	float4 _ProjectionParams;

	// x,正交相机width, y, 正交相机height w,正交相机信息,如果是正交相机，w为1，否则0
	float4 unity_OrthoParams;

	// xy是宽高(rendertarget的宽高),z=1+1/width, w=1+1/height, 影响后处理的rt的宽高 
	float4 _ScreenParams;

	// Values used to linearize the Z buffer (http://www.humus.name/temp/Linearize%20depth.txt)
	// x = 1-far/near
	// y = far/near
	// z = x/far
	// w = y/far
	// or in case of a reversed depth buffer (UNITY_REVERSED_Z is 1)
	// x = -1+far/near
	// y = 1
	// z = x/far
	// w = 1/far
	// zbuffer->ZVS
	float4 _ZBufferParams;
CBUFFER_END
// https://zhuanlan.zhihu.com/p/594539671
// 定义UnityPerDraw这个CBUFFER时需要注意，所有变量在CBUFFER中都必须以组为单位被定义，意味着CBUFFER中出现的一个变量其对应Block Feature中所有变量都需要同时出现

// 为什么v, p矩阵不在定义呢？
// 因为vp是每个go都一样的，是公用的
float4x4 unity_MatrixV;	// v
float4x4 unity_MatrixVP;	// vp
float4x4 glstate_matrix_projection; // p矩阵

// urp中有传递
float3 _WorldSpaceCameraPos;

#endif
