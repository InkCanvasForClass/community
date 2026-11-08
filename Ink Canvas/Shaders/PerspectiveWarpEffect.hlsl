// PerspectiveWarpEffect.hlsl —— 展台 GPU 逆透视（梯形）矫正着色器 (ps_3_0)
//
// 常量寄存器布局：
//   c0: float4 MatrixRow0 (h00, h01, h02, 0)
//   c1: float4 MatrixRow1 (h10, h11, h12, 0)
//   c2: float4 MatrixRow2 (h20, h21, h22, 0)
//   c3: float4 Params     (x=IsEnabled, y=FillBlack, z=Reserved, w=Reserved)

sampler2D implicitInputSampler : register(s0);

float4 MatrixRow0 : register(c0);
float4 MatrixRow1 : register(c1);
float4 MatrixRow2 : register(c2);
float4 Params : register(c3);

float4 main(float2 uv : TEXCOORD) : COLOR
{
    // 未启用时直接原样透传
    if (Params.x < 0.5f)
    {
        return tex2D(implicitInputSampler, uv);
    }

    float x = uv.x;
    float y = uv.y;

    float w = MatrixRow2.x * x + MatrixRow2.y * y + MatrixRow2.z;
    if (abs(w) < 0.000001f)
    {
        return float4(0, 0, 0, 0);
    }

    float u = (MatrixRow0.x * x + MatrixRow0.y * y + MatrixRow0.z) / w;
    float v = (MatrixRow1.x * x + MatrixRow1.y * y + MatrixRow1.z) / w;

    // 超出源画面采样范围
    if (u < 0.0f || u > 1.0f || v < 0.0f || v > 1.0f)
    {
        if (Params.y > 0.5f)
        {
            return float4(0, 0, 0, 1); // 纯黑边框
        }
        return float4(0, 0, 0, 0); // 透明
    }

    return tex2D(implicitInputSampler, float2(u, v));
}
