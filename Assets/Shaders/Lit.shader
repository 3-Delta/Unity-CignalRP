Shader "CignalRP/Lit"
{
    Properties
    {
        _BaseMap("Texture", 2D) = "white" {}
		_BaseColor("Color", Color) = (0.5, 0.5, 0.5, 1.0)
		
		_Cutoff ("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
		[Toggle(_CLIPPING)] _Clipping ("Alpha Clipping", Float) = 0
		
		_Metallic ("Metallic", Range(0, 1)) = 0
		_Smoothness ("Smoothness", Range(0, 1)) = 0.5
		
		[Toggle(_PREMULTIPLY_ALPHA)] _PremulAlpha ("Premultiply Alpha", Float) = 0
        
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
		[Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
		[Enum(Off, 0, On, 1)] _ZWrite ("Z Write", Float) = 1
    }

    SubShader
    {
        Pass
        {
			Tags {
				"LightMode" = "CRPLit"
			}
			
			Blend [_SrcBlend] [_DstBlend]
			ZWrite [_ZWrite]
			
            HLSLPROGRAM
            // unity默认2.5
            #pragma target 3.5
            
            #pragma shader_feature _CLIPPING
            #pragma shader_feature _PREMULTIPLY_ALPHA
            
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
						
            HLSLPROGRAM
            #pragma target 3.5
            
            #pragma shader_feature _CLIPPING
            
            // 支持gpu instance, 会有宏INSTANCE_ON定义
            #pragma multi_compile_instancing

            #pragma vertex ShadowCasterPassVertex
            #pragma fragment ShadowCasterPassFragment
            
            #include "../ShaderLibrary/Light/ShadowCaster.hlsl"
            ENDHLSL
        }
    }
}
