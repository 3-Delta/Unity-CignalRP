Shader "CignalRP/CameraRender"
{
    SubShader
    {
        cull off
        
        ZTest Always
        ZWrite off
        
        // 第一次见这个关键字
        HLSLINCLUDE
            #include "../ShaderLibrary/Common.hlsl"
            #include "../ShaderLibrary/UnityInput.hlsl"
            #include "../ShaderLibrary/PostProcess/CameraRenderPass.hlsl"
        ENDHLSL

        Pass
        {
			Name "CopyColor"
			
			HLSLPROGRAM
				#pragma target 3.5
				#pragma vertex DefaultVertex
	            #pragma fragment CopyColorFragment
			ENDHLSL
        }
    	
    	Pass
        {
			Name "CopyDepth"
        	
        	ColorMask 0
        	Zwrite On
			
			HLSLPROGRAM
				#pragma target 3.5
				#pragma vertex DefaultVertex
	            #pragma fragment CopyDepthFragment
			ENDHLSL
        }
    }
}
