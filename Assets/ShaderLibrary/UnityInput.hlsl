#ifndef CRP_UNITY_INPUT_INCLUDED
#define CRP_UNITY_INPUT_INCLUDED

// 定义cpu传递到gpu的矩阵
// 例如this.context.SetupCameraProperties(this.camera);
float4x4 unity_ObjectToWorld;
float4x4 unity_WorldToObject;

float4x4 unity_MatrixV;
float4x4 unity_MatrixVP;
float4x4 glstate_matrix_projection;

real4 unity_WorldTransformParams;

#endif
