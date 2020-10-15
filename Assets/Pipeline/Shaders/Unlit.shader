Shader "MyRP/Unlit"
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
            // 只考虑规则缩放，不会将normal需要的消除不规则缩放影响的M矩阵包含进来
            #pragma instancing_options assumeuniformscaling

            #include "Unlit.hlsl"

            #pragma vertex UnlitPassVertex
            #pragma fragment UnlitPassFragment

            ENDHLSL
        }
    }
}
