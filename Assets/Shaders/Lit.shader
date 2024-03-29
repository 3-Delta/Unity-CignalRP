﻿Shader "CignalRP/Lit"
{
    Properties
    {
        _BaseMap("Texture", 2D) = "white" {}
		_BaseColor("Color", Color) = (0.5, 0.5, 0.5, 1.0)
		
		_Cutoff ("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
		[Toggle(_CLIPPING)] _Clipping ("Alpha Clipping", Float) = 0
		
		[Toggle(_RECEIVE_SHADOWS)] _ReceiveShadows ("Receive Shadows", Float) = 1
		[KeywordEnum(Clip, Dither, Off)] _Shadows ("Shadows", Float) = 0
		
    	// metallic, occulation, detail, smoothness
    	[NoScaleOffset] _MaskMap("Mask (MODS)", 2D) = "white" {}
    	
		_Metallic ("Metallic", Range(0, 1)) = 0
		_Smoothness ("Smoothness", Range(0, 1)) = 0.5
    	_Fresnal("Fresnal", Range(0, 1)) = 1
    	
	    [NoScaleOffset] _EmissionMap("Emission", 2D) = "white" {}
		[HDR] _EmissionColor("Emission", Color) = (0.0, 0.0, 0.0, 0.0)

		[Toggle(_PREMULTIPLY_ALPHA)] _PremulAlpha ("Premultiply Alpha", Float) = 0
        
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
		[Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
		[Enum(Off, 0, On, 1)] _ZWrite ("Z Write", Float) = 1
    }

    SubShader
    {
    	HLSLINCLUDE
    	#pragma enable_d3d11_debug_symbols
    	
    	#include "../ShaderLibrary/Common.hlsl"
    	#include "../ShaderLibrary/Light/LitInput.hlsl"
    	ENDHLSL
    	
        Pass
        {
			Tags {
				"LightMode" = "CRPLit"
			}
			
			Blend [_SrcBlend] [_DstBlend], One OneMinusSrcAlpha
			ZWrite [_ZWrite]
			
            HLSLPROGRAM
            // unity默认2.5
            #pragma target 3.5
            #pragma shader_feature _CLIPPING
            #pragma shader_feature _PREMULTIPLY_ALPHA
            
            #pragma shader_feature _RECEIVE_SHADOWS
			#pragma multi_compile _ _DIRECTIONAL_PCF3 _DIRECTIONAL_PCF5 _DIRECTIONAL_PCF7
            #pragma multi_compile _ _OTHER_PCF3 _OTHER_PCF5 _OTHER_PCF7

            // lightmap, 静态物体的该宏会生效，动态物体不生效， 也就是物体的Renderer组件上启用了Lightmap Static属性时，该物体就可以使用Lightmap
            // 这可以区别lightmap和lightprobe
			#pragma multi_compile _ LIGHTMAP_ON

            // shadowmask
            #pragma multi_compile _ _SHADOW_MASK_DISTANCE _SHADOW_MASK_ALWAYS

            #pragma multi_compile _ _LIGHTS_PER_OBJECT
            
            // 支持gpu instance, 会有宏INSTANCE_ON定义
            #pragma multi_compile_instancing

            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment
            
            #include "LitPass.hlsl"
            ENDHLSL
        }
        
        Pass
        {
			Tags {
				"LightMode" = "ShadowCaster"
			}
			
			// 只写入depth，不写入color
			// framedebugger观察到：
			// blend one zero 也就是直接覆盖
			ColorMask 0
			// Blend One Zero
            // ztest lessequal
            // zwrite on
						
            HLSLPROGRAM
            #pragma target 3.5
            
            #pragma shader_feature _ _SHADOWS_CLIP _SHADOW_DITHER
            
            // 支持gpu instance, 会有宏INSTANCE_ON定义
            #pragma multi_compile_instancing

            #pragma vertex ShadowCasterPassVertex
            #pragma fragment ShadowCasterPassFragment
            
            #include "../ShaderLibrary/Light/ShadowCaster.hlsl"
            ENDHLSL
        }
    	
    	pass
    	{
            // meta的作用就是：烘焙lightmap的时候，将二次弹射光线的表面夜曲模仿漫反射，
            // 否则默认就是纯白色
            
    		// 编辑器 渲染lightmap
    		Tags
    		{
    			"LightMode" = "Meta"
            }
    		
    		Cull off
    		
    		HLSLPROGRAM
    		#pragma target 3.5
    		#pragma vertex MetaVertex
    		#pragma fragment MetaFragment
    		#include "MetaPass.hlsl"
    		ENDHLSL
        }
    }
}
