Shader "Hidden/My Pipeline/PostEffectStack"
{
    SubShader
    {
        Cull OFF
        ZTest ALWAYS
        ZWrite OFF

        Pass    // 0 Copy
        {
            HLSLPROGRAM
            #pragma target 3.5
            #include "PostEffectStack.hlsl"
            #pragma vertex DefaultPassVertex
            #pragma fragment CopyPassFragment
            ENDHLSL
        }

        Pass    // 1 Blur
        {
            HLSLPROGRAM
            #pragma target 3.5
            #include "PostEffectStack.hlsl"
            #pragma vertex DefaultPassVertex
            #pragma fragment BlurPassFragment
            ENDHLSL
        }
    }
}
