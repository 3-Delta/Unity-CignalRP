Shader "CignalRP/Unlit"
{
    Properties
    {
        _BaseMap("Texture", 2D) = "white" {}
		[HDR] _BaseColor("Color", Color) = (1.0, 1.0, 1.0, 1.0)
		
		_Cutoff ("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
		[Toggle(_CLIPPING)] _Clipping ("Alpha Clipping", Float) = 0
        
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
			Blend [_SrcBlend] [_DstBlend], One OneMinusSrcAlpha
			ZWrite [_ZWrite]
			
            HLSLPROGRAM
            #pragma shader_feature _CLIPPING
            
            // 支持gpu instance, 会有宏INSTANCE_ON定义
            #pragma multi_compile_instancing

            #pragma vertex UnlitPassVertex
            #pragma fragment UnlitPassFragment
            
            #include "UnlitPass.hlsl"
            ENDHLSL
        }
    	
    	pass // 粒子是动态的，所以不需要这个给lightmap的pass
    	{
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
