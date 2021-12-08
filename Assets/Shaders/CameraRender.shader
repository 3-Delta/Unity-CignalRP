Shader "CignalRP/CameraRender"
{
    SubShader
    {
        cull off
        
        ZTest Always
        ZWrite off

        HLSLINCLUDE
        	#pragma enable_d3d11_debug_symbols

            #include "../ShaderLibrary/Common.hlsl"
            #include "../ShaderLibrary/UnityInput.hlsl"
            #include "../ShaderLibrary/PostProcess/CameraRenderPass.hlsl"
        ENDHLSL

        Pass
        {
			Name "CopyColor"
        	
        	// 不写入depth
			
			HLSLPROGRAM
				#pragma target 3.5
				#pragma vertex DefaultVertex
	            #pragma fragment CopyColorFragment
			ENDHLSL
        }
    	
    	Pass
        {
			Name "CopyDepth"
        	
        	ColorMask 0 // 不写入color, 只写入depth
        	Zwrite On
			
			HLSLPROGRAM
				#pragma target 3.5
				#pragma vertex DefaultVertex
	            #pragma fragment CopyDepthFragment
			ENDHLSL
        }
    }
}
