#ifndef MYRP_UNLIT_INCLUDED
    #define MYRP_UNLIT_INCLUDED

    // 必须在include UnityInstancing.hlsl之前，UnityInstancing中有对UNITY_MATRIX_M的重定义
    #define UNITY_MATRIX_M unity_ObjectToWorld

    #include "ShaderLibrary/Common.hlsl"
    #include "ShaderLibrary/UnityInstancing.hlsl"

    // 只有某些平台支持constant buffer, 具体可以看ShaderLibrary/API中各平台的CBUFFER_START(name)和CBUFFER_END宏定义
    // 由于一次cbuffer改动会造成整个cbuffer struct刷新因此按照不同的更新频率对cbuffer进行分组
    CBUFFER_START(UnityPerFrame)
    float4x4 unity_MatrixVP;
    CBUFFER_END

    CBUFFER_START(UnityPerDraw)
    float4x4 unity_ObjectToWorld;
    CBUFFER_END

    UNITY_INSTANCING_BUFFER_START(PerInstance)
    UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
    UNITY_INSTANCING_BUFFER_END(PerInstance)

    struct VertexInput 
    {
        float4 pos : POSITION;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct VertexOutput 
    {
        float4 clipPos : SV_POSITION;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    VertexOutput UnlitPassVertex (VertexInput input) 
    {
        VertexOutput output;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_TRANSFER_INSTANCE_ID(input, output);
        float4 worldPos = mul(UNITY_MATRIX_M, float4(input.pos.xyz, 1.0));
        output.clipPos = mul(unity_MatrixVP, worldPos);
        return output;
    }

    float4 UnlitPassFragment (VertexOutput input) : SV_TARGET 
    {
        UNITY_SETUP_INSTANCE_ID(input);
        return UNITY_ACCESS_INSTANCED_PROP(PerInstance, _Color);
    }

#endif // MYRP_UNLIT_INCLUDED