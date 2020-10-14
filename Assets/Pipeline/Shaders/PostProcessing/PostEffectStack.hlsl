#ifndef MYRP_POST_EFFECT_STACK_INCLUDED
    #define MYRP_POST_EFFECT_STACK_INCLUDED

    #include "Library/StdLib.hlsl"

    TEXTURE2D_SAMPLER2D(_MainTex, sampler_MainTex);

    struct VertexInput 
    {
        float4 pos : POSITION;
    };

    struct VertexOutput 
    {
        float4 clipPos : SV_POSITION;
        float2 uv : TEXCOORD0;
    };

    VertexOutput DefaultPassVertex (VertexInput input) 
    {
        VertexOutput output;
        output.clipPos = float4(input.pos.xy, 0.0, 1.0);
        output.uv = input.pos.xy * 0.5 + 0.5;
        // 防止图像反转
        if (_ProjectionParams.x < 0.0)
        output.uv.y = 1.0 - output.uv.y;

        return output;
    }

    float4 CopyPassFragment (VertexOutput input) : SV_TARGET 
    {
        return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
    }

    float4 BlurSample (float2 uv, float uOffset = 0.0, float vOffset = 0.0) 
    {
        uv += float2(uOffset * ddx(uv.x), vOffset * ddy(uv.y));
        return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
    }

    float4 BlurPassFragment (VertexOutput input) : SV_TARGET 
    {
        return BlurSample(input.uv, 0.5, 0.5);
    }

#endif // MYRP_POST_EFFECT_STACK_INCLUDED