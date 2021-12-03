Shader "CignalRP/Particle/Unlit"
{
    Properties
    {
        _BaseMap("Texture", 2D) = "white" {}
		_BaseColor("Color", Color) = (1.0, 1.0, 1.0, 1.0)
		
		_Cutoff ("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
		[Toggle(_CLIPPING)] _Clipping ("Alpha Clipping", Float) = 0
    	
    	// 顶点色
    	[Toggle(_VERTEX_COLOR)] _VertexColor ("VertexColor", Float) = 0
    	[Toggle(_FLIPBOOK_BLEND)] _FlipBookBlend ("FlipBookBlend", Float) = 0
        
    	[Toggle(_NEAR_FADE)] _NearFade ("NearFade", Float) = 0
    	_NearFadeDistance("NearFadeDistance", Range(0.0, 10.0)) = 1
    	_NearFadeRange("NearFadeRange", Range(0.01, 10.0)) = 1
    	
    	[Toggle(_SOFT_PARTICLES)] _SoftParticles ("SoftParticles", Float) = 0
    	_SoftParticlesDistance("SoftParticlesDistance", Range(0.0, 10.0)) = 1
    	_SoftParticlesRange("SoftParticlesRange", Range(0.01, 10.0)) = 1
    	
    	// 扰动
    	[Toggle(_DISTORTION)] _Distortion ("Distortion", Float) = 0
    	[NoScaleOffset] _DistortionTexture ("DistortionTexture", 2D) = "bumb" {}
    	_DistortionStrength("DistortionStrength", Range(0.0, 0.2)) = 0.1
    	_DistortionBlend("DistortionBlend", Range(0.0, 1.0)) = 1
        
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
		[Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
		[Enum(Off, 0, On, 1)] _ZWrite ("Z Write", Float) = 1
    }

    SubShader
    {
    	HLSLINCLUDE
    	#include "../ShaderLibrary/Common.hlsl"
    	#include "UnlitInput.hlsl"
    	ENDHLSL
    	
        Pass
        {
			Blend [_SrcBlend] [_DstBlend]
			ZWrite [_ZWrite]
			
            HLSLPROGRAM
            #pragma shader_feature _CLIPPING
            #pragma shader_feature _VERTEX_COLOR
            #pragma shader_feature _FLIPBOOK_BLEND

            #pragma shader_feature _NEAR_FADE
            #pragma shader_feature _SOFT_PARTICLES
            #pragma shader_feature _DISTORTION
            
            // 支持gpu instance, 会有宏INSTANCE_ON定义
            #pragma multi_compile_instancing

            #pragma vertex UnlitPassVertex
            #pragma fragment UnlitPassFragment
            
            #include "UnlitPass.hlsl"
            ENDHLSL
        }
    }
}
