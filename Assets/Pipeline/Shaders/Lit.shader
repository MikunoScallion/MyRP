Shader "MyRP/Lit"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Pass
        {
            HLSLPROGRAM

            #pragma target 3.5

            #pragma multi_compile_instancing
            #pragma multi_compile _ _CASCADED_SHADOWS_HARD _CASCADED_SHADOWS_SOFT
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _SHADOWS_HARD
            // 只考虑规则缩放，不会将normal需要的消除不规则缩放影响的M矩阵包含进来
            #pragma instancing_options assumeuniformscaling

            #include "Lit.hlsl"

            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

            ENDHLSL
        }

        Pass
        {
            Tags 
            {
                "LightMode" = "ShadowCaster"
            }
            HLSLPROGRAM

            #pragma target 3.5

            #pragma multi_compile_instancing
            // 只考虑规则缩放，不会将normal需要的消除不规则缩放影响的M矩阵包含进来
            #pragma instancing_options assumeuniformscaling

            #include "ShadowCaster.hlsl"

            #pragma vertex ShadowCasterPassVertex
            #pragma fragment ShadowCasterPassFragment

            ENDHLSL
        }
    }
}
