Shader "MyRP/Lit"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _MainTex ("RGB: Color Texture A: Alpha", 2D) = "white" {}
        _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5
        _Metallic ("Metallic", Range(0, 1)) = 0
        [KeywordEnum(Off, On, Shadows)] _Clipping ("Alpha Clipping", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
        [Enum(Off, 0, On, 1)] _ZWrite ("Z Write", Float) = 1
        [Toggle(_RECEIVE_SHADOWS)] _ReceiveShadows ("Receive Shadows", Float) = 1
        [Toggle(_PREMULTIPLY_ALPHA)] _PremulAlpha ("Premultiply Alpha", Float) = 0
    }

    SubShader
    {
        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            Cull [_Cull]
            ZWrite [_ZWrite]

            HLSLPROGRAM

            #pragma target 3.5

            #pragma multi_compile_instancing
            #pragma multi_compile _ _CASCADED_SHADOWS_HARD _CASCADED_SHADOWS_SOFT
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _SHADOWS_HARD

            #pragma shader_feature _CLIPPING_ON
            #pragma shader_feature _RECEIVE_SHADOWS
            #pragma shader_feature _PREMULTIPLY_ALPHA

            // // 只考虑规则缩放，不会将normal需要的消除不规则缩放影响的M矩阵包含进来
            // #pragma instancing_options assumeuniformscaling

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

            Cull [_Cull]

            HLSLPROGRAM

            #pragma target 3.5

            #pragma multi_compile_instancing

            #pragma shader_feature _CLIPPING_OFF

            #include "ShadowCaster.hlsl"

            #pragma vertex ShadowCasterPassVertex
            #pragma fragment ShadowCasterPassFragment

            ENDHLSL
        }
    }

    CustomEditor "LitShaderGUI"
}
