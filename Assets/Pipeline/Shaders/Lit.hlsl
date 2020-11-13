#ifndef MYRP_LIT_INCLUDED
    #define MYRP_LIT_INCLUDED

    // 必须在include UnityInstancing.hlsl之前，UnityInstancing中有对UNITY_MATRIX_M的重定义
    #define UNITY_MATRIX_M unity_ObjectToWorld

    #include "ShaderLibrary/Common.hlsl"
    #include "ShaderLibrary/UnityInstancing.hlsl"
    #include "ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"

    // 只有某些平台支持constant buffer, 具体可以看ShaderLibrary/API中各平台的CBUFFER_START(name)和CBUFFER_END宏定义
    // 由于一次cbuffer改动会造成整个cbuffer struct刷新因此按照不同的更新频率对cbuffer进行分组
    CBUFFER_START(UnityPerFrame)
    float4x4 unity_MatrixVP;
    CBUFFER_END

    CBUFFER_START(UnityPerDraw)
    float4x4 unity_ObjectToWorld;
    float4 unity_LightIndicesOffsetAndCount;
    float4 unity_4LightIndices0, unity_4LightIndices1;
    CBUFFER_END

    CBUFFER_START(UnityPerCamera)
    float3 _WorldSpaceCameraPos;
    CBUFFER_END

/****** 灯光 ******/
    #define MAX_VISIBLE_LIGHTS 16

    CBUFFER_START(_LightBuffer)
    float4 _VisibleLightColors[MAX_VISIBLE_LIGHTS];
    float4 _VisibleLightDirectionsOrPositions[MAX_VISIBLE_LIGHTS];
    float4 _VisibleLightAttenuations[MAX_VISIBLE_LIGHTS];
    float4 _VisibleLightSpotDirections[MAX_VISIBLE_LIGHTS];
    CBUFFER_END

    UNITY_INSTANCING_BUFFER_START(PerInstance)
    UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
    UNITY_INSTANCING_BUFFER_END(PerInstance)

    float3 DiffuseLight (int index, float3 normal, float3 worldPos, float shadowAttenuation) 
    {
        float3 lightColor = _VisibleLightColors[index].rgb;
        float4 lightPositionOrDirection = _VisibleLightDirectionsOrPositions[index];
        float4 lightAttenuation = _VisibleLightAttenuations[index];
        float3 lightSpotDirection = _VisibleLightSpotDirections[index].xyz;

        float3 lightVector = lightPositionOrDirection.xyz - worldPos * lightPositionOrDirection.w;
        float3 lightDirection = normalize(lightVector);

        float diffuse = saturate(dot(normal, lightDirection));
        // lwrp用的是平缓的衰减: (1 - (d^2 / r^2)^2)^2   d:distance  r:light range 
        // lightAttenuation = 1 / r^2
        // ? https://catlikecoding.com/unity/tutorials/scriptable-render-pipeline/lights/
        // ? 这里d^2/r^2没有2次幂，不清楚是公式有问题还是少了一步
        float rangeFade = dot(lightVector, lightVector) * lightAttenuation.x;       // d^2/r^2
        rangeFade = saturate(1.0 - rangeFade * rangeFade);      // 1 - d^2/r^2
        rangeFade *= rangeFade;         // (1 - d^2/r^2)^2

        float spotFade = dot(lightSpotDirection, lightDirection);
        spotFade = saturate(spotFade * lightAttenuation.z + lightAttenuation.w);
        spotFade *= spotFade;

        // ? 这里他又除以d^2不清楚为什么
        // float distanceSqr = max(dot(lightVector, lightVector), 0.00001); 
        // diffuse *= spotFade * rangeFade / distanceSqr; 
        diffuse *= shadowAttenuation * spotFade * rangeFade; 
        return diffuse * lightColor;
    }

/****** 阴影 ******/
    CBUFFER_START(_ShadowBuffer)
    float4x4 _WorldToShadowMatrices[MAX_VISIBLE_LIGHTS];
    float4 _ShadowData[MAX_VISIBLE_LIGHTS];
    float4 _GlobalShadowData;
    float4 _ShadowMapSize;
    CBUFFER_END

    TEXTURE2D_SHADOW(_ShadowMap);
    SAMPLER_CMP(sampler_ShadowMap);

    float HardShadowAttenuation (float4 shadowPos)
    {
        return SAMPLE_TEXTURE2D_SHADOW(_ShadowMap, sampler_ShadowMap, shadowPos.xyz);
    }

    float DistanceToCameraSqr (float3 worldPos)
    {
        float3 cameraToFragment = worldPos - _WorldSpaceCameraPos;
        return dot(cameraToFragment, cameraToFragment);
    }

    float SoftShadowAttenuation (float4 shadowPos)
    {
        real tentWeights[9];
        real2 tentUVs[9];
        SampleShadow_ComputeSamples_Tent_5x5(_ShadowMapSize, shadowPos.xy, tentWeights, tentUVs);
        float attenuation = 0;
        for (int i = 0; i < 9; i++) 
        {
            attenuation += tentWeights[i] * SAMPLE_TEXTURE2D_SHADOW(_ShadowMap, sampler_ShadowMap, float3(tentUVs[i].xy, shadowPos.z));
        }
        return attenuation;
    }

    float ShadowAttenuation (int index, float3 worldPos) 
    {
#if !defined(_SHADOWS_HARD) && !defined(_SHADOWS_SOFT)
        return 1.0;
#endif
        if (_ShadowData[index].x <= 0 || DistanceToCameraSqr(worldPos) > _GlobalShadowData.y)
            return 1.0;

        float4 shadowPos = mul(_WorldToShadowMatrices[index], float4(worldPos, 1.0));
        shadowPos.xyz /= shadowPos.w;
        shadowPos.xy = saturate(shadowPos.xy);
        shadowPos.xy = shadowPos.xy * _GlobalShadowData.x + _ShadowData[index].zw;

        float attenuation;
#if defined(_SHADOWS_HARD)
    #if defined(_SHADOWS_SOFT)
        if (_ShadowData[index].y == 0) 
            attenuation = HardShadowAttenuation(shadowPos);
        else
            attenuation = SoftShadowAttenuation(shadowPos);
    #else
            attenuation = HardShadowAttenuation(shadowPos);
    #endif
#else
        attenuation = SoftShadowAttenuation(shadowPos);
#endif

        return lerp(1, attenuation, _ShadowData[index].x);
    }

    struct VertexInput 
    {
        float4 pos : POSITION;
        float3 normal : NORMAL;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct VertexOutput 
    {
        float4 clipPos : SV_POSITION;
        float3 normal : TEXCOORD0;
        float3 worldPos : TEXCOORD1;
        float3 vertexLighting : TEXCOORD2;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    VertexOutput LitPassVertex (VertexInput input) 
    {
        VertexOutput output;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_TRANSFER_INSTANCE_ID(input, output);

        float4 worldPos = mul(UNITY_MATRIX_M, float4(input.pos.xyz, 1.0));
        output.clipPos = mul(unity_MatrixVP, worldPos);

        output.normal = mul((float3x3) UNITY_MATRIX_M, input.normal);

        output.worldPos = worldPos.xyz;

        output.vertexLighting = 0;
        for (int i = 4; i < min(unity_LightIndicesOffsetAndCount.y, 8); i++) 
        {
            int lightIndex = unity_4LightIndices1[i - 4];
            output.vertexLighting += DiffuseLight(lightIndex, output.normal, output.worldPos, 1);
        }

        return output;
    }

    float4 LitPassFragment (VertexOutput input) : SV_TARGET 
    {
        UNITY_SETUP_INSTANCE_ID(input);

        input.normal = normalize(input.normal);

        float3 albedo = UNITY_ACCESS_INSTANCED_PROP(PerInstance, _Color).rgb;

        float3 diffuseLight = input.vertexLighting;
        for (int i = 0; i < min(unity_LightIndicesOffsetAndCount.y, 4); i++)
        {
            int lightIndex = unity_4LightIndices0[i];
            float shadowAttenuation = ShadowAttenuation(lightIndex, input.worldPos);
            diffuseLight += DiffuseLight(lightIndex, input.normal, input.worldPos, shadowAttenuation);
        }

        float3 color = albedo * diffuseLight;

        return float4(color, 1.0);
    }

#endif // MYRP_LIT_INCLUDED