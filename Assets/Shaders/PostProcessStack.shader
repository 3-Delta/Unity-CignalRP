Shader "CignalRP/PostProcessStack"
{
    SubShader
    {
        cull off
        
        ZTest Always
        ZWrite off
        // 默认是one zero的混合模式，也就是后者直接覆盖前者
        Blend one zero
        
        // 第一次见这个关键字
        HLSLINCLUDE
            #pragma enable_d3d11_debug_symbols
            
            #include "../ShaderLibrary/Common.hlsl"
            #include "../ShaderLibrary/PostProcess/PostProcessStack.hlsl"
        ENDHLSL
        
        Pass
        {
            Name "Bloom Combine"

            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex DefaultVertex
                #pragma fragment BloomCombineFragment
            ENDHLSL
        }
        Pass
        {
            // 单纯下采样得不到非常块状结果,需要配合高斯模糊
            // 高斯模糊比较消耗,结合下采样,会得到n*n* 4的效果
            Name "Bloom Horizontal"

            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex DefaultVertex
                #pragma fragment BloomHorizontalFragment
            ENDHLSL
        }
        Pass
        {
            Name "Bloom Vertical"

            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex DefaultVertex
                #pragma fragment BloomVerticalFragment
            ENDHLSL
        }

        Pass
        {
            Name "ColorGrade None"

            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex DefaultVertex
                #pragma fragment ColorGradeNoneFragment
            ENDHLSL
        }
        Pass
        {
            Name "ColorGrade ACES"

            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex DefaultVertex
                #pragma fragment ColorGradeACESFragment
            ENDHLSL
        }
        Pass
        {
            Name "ColorGrade Neutral"

            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex DefaultVertex
                #pragma fragment ColorGradeNeutralFragment
            ENDHLSL
        }
        Pass
        {
            Name "ColorGrade Reinhard"

            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex DefaultVertex
                #pragma fragment ColorGradeReinhardFragment
            ENDHLSL
        }
        Pass
        {
            Name "ColorGrade Final"
            Blend [_FinalSrcBlend] [_FinalDestBlend]

            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex DefaultVertex
                #pragma fragment FinalFragment
            ENDHLSL
        }

        Pass
        {
			Name "Copy" // 其实就是Blit
			
			HLSLPROGRAM
				#pragma target 3.5
				#pragma vertex DefaultVertex
	            #pragma fragment CopyColorFragment
			ENDHLSL
        }
        Pass
        {
			Name "FinalScale"
            Blend [_FinalSrcBlend] [_FinalDestBlend]
			
			HLSLPROGRAM
				#pragma target 3.5			
				#pragma vertex DefaultVertex
	            #pragma fragment FinalScaleFragment
			ENDHLSL
        }
    }
}
