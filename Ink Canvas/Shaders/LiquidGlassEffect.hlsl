// LiquidGlassEffect.ps —— 液态玻璃边缘折射像素着色器 (ps_3_0) 源文件
//
// 移植自 AndroidLiquidGlass (https://github.com/Kyant0/AndroidLiquidGlass, Apache-2.0)
// 的 RoundedRectRefractionShaderString / RoundedRectRefractionWithDispersionShaderString /
// DefaultHighlightShaderString。核心思路：
//   - SDF 圆角矩形（sdRoundedRect）只在边缘带（RefractionHeight）内做折射，
//     中心区域原样透出桌面截图，避免「整面均匀扭曲+模糊」带来的糊感；
//   - 折射位移用 circleMap(x)=1-sqrt(1-x*x) 球面衰减，方向取 SDF 梯度（表面法线），
//     RefractionAmount 传负值 → 向内侧采样 → 边缘轻微放大（透镜效果）；
//   - 可选 7 采样色散（ChromaticAberration>0），红通道取正向偏移、蓝通道取负向偏移。
//
// 高光与 Android 原版的差异（重要）：
//   原版 DefaultHighlight 是作为**轮廓描边**单独绘制的（drawBackdrop 的 highlight 通道，
//   BlendMode.Plus），不是铺满整个表面。gradSdRoundedRect 返回的是单位法线、不随深度衰减，
//   一旦铺满，对 1200x40 这种扁胶囊（gradRadius 被 min 到 halfSize.y）中间上千像素的
//   |dot(grad,normal)| 恒为 1，等于给整条栏无差别 +HighlightStrength 白 → 整体发雾、对比度尽失。
//   所以这里用 HighlightWidth 把高光按 SDF 距离限制在边缘带内，并按迎光/背光加权。
//
// 编译方式（fxc 的等价物，Windows 10 自带 d3dcompiler_47.dll 的 D3DCompile）：
//   D3DCompile(src, len, name, NULL, NULL, "main", "ps_3_0", 0, 0, &code, &err)
//
// 常量寄存器布局（与 LiquidGlassEffect.cs 的 PixelShaderConstantCallback 一一对应）：
//   c0 = TextureSize (xy)        采样输入尺寸，DIP
//   c1 = CornerRadius            圆角半径，DIP（胶囊=高度一半）
//   c2 = RefractionHeight        边缘折射带宽，px
//   c3 = RefractionAmount        折射位移幅度，px（传负值=向内侧放大）
//   c4 = DepthEffect             0 或 1：叠加径向分量增强边缘立体感
//   c5 = ChromaticAberration     0 关闭色散；>0 打开（RGB 三通道全模糊一致，仅圆角处）
//   c6 = HighlightAngle          高光方向（弧度，屏幕坐标 y 向下，-PI/2 = 顶部受光）
//   c7 = HighlightFalloff        高光沿法线的衰减指数
//   c8 = HighlightStrength       高光强度 0..1
//   c9 = HighlightWidth          高光带宽，px（限制高光只出现在边缘）

sampler2D implicitInputSampler : register(s0);

float2 TextureSize : register(c0);
float CornerRadius : register(c1);
float RefractionHeight : register(c2);
float RefractionAmount : register(c3);
float DepthEffect : register(c4);
float ChromaticAberration : register(c5);
float HighlightAngle : register(c6);
float HighlightFalloff : register(c7);
float HighlightStrength : register(c8);
float HighlightWidth : register(c9);
float BlurRadius : register(c10);

// 零向量归一化会得到 NaN，而 0 * NaN 仍是 NaN，会污染采样坐标。
// 胶囊水平中心线上 cornerCoord 恰好是零向量，必须走这个安全版本。
float2 SafeNormalize(float2 v)
{
    float len = length(v);
    return len > 1e-5 ? v / len : float2(0.0, 0.0);
}

// 圆角矩形有符号距离场
float sdRoundedRect(float2 coord, float2 halfSize, float radius)
{
    float2 cornerCoord = abs(coord) - (halfSize - float2(radius, radius));
    float outside = length(max(cornerCoord, 0.0)) - radius;
    float inside = min(max(cornerCoord.x, cornerCoord.y), 0.0);
    return outside + inside;
}

// 圆角矩形 SDF 梯度（表面法线方向）
float2 gradSdRoundedRect(float2 coord, float2 halfSize, float radius)
{
    float2 cornerCoord = abs(coord) - (halfSize - float2(radius, radius));
    if (cornerCoord.x >= 0.0 || cornerCoord.y >= 0.0)
    {
        return sign(coord) * SafeNormalize(max(cornerCoord, 0.0));
    }
    else
    {
        float gradX = step(cornerCoord.y, cornerCoord.x);
        return sign(coord) * float2(gradX, 1.0 - gradX);
    }
}

// 球面衰减：x∈[0,1] → 0..1，边缘最快
float circleMap(float x)
{
    return 1.0 - sqrt(1.0 - x * x);
}

// 连续高斯模糊：沿 x、y 方向各 4 级采样，步长 = radius/4（覆盖 ±radius），
// 权重按 exp(-t²) 连续衰减，权重总和归一到 1。采样点铺满整个半径范围，
// 无空隙 → 不会产生「多张错开的清晰副本」那种重影。
float4 SampleBlur(float2 uv, float2 texel, float radius)
{
    float sigma = max(radius / 4.0, 0.5);
    float inv2s2 = 1.0 / (2.0 * sigma * sigma);
    float2 h = float2(texel.x, 0.0);
    float2 v = float2(0.0, texel.y);

    float4 acc = tex2D(implicitInputSampler, uv);
    float sum = 1.0;

    float w1 = exp(-1.0 * inv2s2);
    float w2 = exp(-4.0 * inv2s2);
    float w3 = exp(-9.0 * inv2s2);
    float w4 = exp(-16.0 * inv2s2);

    acc += (tex2D(implicitInputSampler, uv + h) + tex2D(implicitInputSampler, uv - h)) * w1;
    acc += (tex2D(implicitInputSampler, uv + 2.0 * h) + tex2D(implicitInputSampler, uv - 2.0 * h)) * w2;
    acc += (tex2D(implicitInputSampler, uv + 3.0 * h) + tex2D(implicitInputSampler, uv - 3.0 * h)) * w3;
    acc += (tex2D(implicitInputSampler, uv + 4.0 * h) + tex2D(implicitInputSampler, uv - 4.0 * h)) * w4;
    acc += (tex2D(implicitInputSampler, uv + v) + tex2D(implicitInputSampler, uv - v)) * w1;
    acc += (tex2D(implicitInputSampler, uv + 2.0 * v) + tex2D(implicitInputSampler, uv - 2.0 * v)) * w2;
    acc += (tex2D(implicitInputSampler, uv + 3.0 * v) + tex2D(implicitInputSampler, uv - 3.0 * v)) * w3;
    acc += (tex2D(implicitInputSampler, uv + 4.0 * v) + tex2D(implicitInputSampler, uv - 4.0 * v)) * w4;

    sum += 4.0 * (w1 + w2 + w3 + w4);
    return acc / sum;
}

float4 main(float2 uv : TEXCOORD0) : COLOR0
{
    float2 coord = uv * TextureSize;
    float2 halfSize = TextureSize * 0.5;
    float2 centeredCoord = coord - halfSize;

    float sd = sdRoundedRect(centeredCoord, halfSize, CornerRadius);
    float gradRadius = min(CornerRadius * 1.5, min(halfSize.x, halfSize.y));
    float2 grad = gradSdRoundedRect(centeredCoord, halfSize, gradRadius);

    // 到边界的距离（内部为正）。折射与高光都按它衰减。
    float depth = max(-sd, 0.0);

    // 折射位移。clamp 保证中心区域（depth ≥ RefractionHeight）时 x=0 → d=0 → 原样透出，
    // 既消除 circleMap 在 x>1 时的 NaN，也省掉逐像素动态分支。
    float d = circleMap(clamp(1.0 - depth / RefractionHeight, 0.0, 1.0)) * RefractionAmount;
    float2 refractedGrad = SafeNormalize(grad + DepthEffect * SafeNormalize(centeredCoord));
    float2 refractedCoord = coord + d * refractedGrad;

    // 输入纹理是清晰截图。对着色器内部做连续高斯模糊（Blur 层）+ SDF 折射采样：
    // 边缘带内位移（透镜感），中心模糊背景原样透出。单元素单 Effect 完成 blur→refraction。
    float2 texel = 1.0 / TextureSize;
    float4 color = SampleBlur(refractedCoord / TextureSize, texel, BlurRadius);

    if (ChromaticAberration > 0.0)
    {
        // 边缘色散：只沿玻璃边缘出现（edgeMask 贴边才非零），中心无色散。
        // 用固定像素偏移（不乘任何衰减因子）做 R/B 色差，确保肉眼可见：
        //   ChromaticAberration 控制偏移量，颜色分道沿折射法线方向。
        float edgeMask = saturate(1.0 - depth / max(RefractionHeight, 1e-3));
        float amount = ChromaticAberration * BlurRadius * edgeMask;
        float2 dispersedCoord = refractedGrad * amount;

        // R/B 也走模糊采样，与 G 清晰度一致，不放大色偏
        float2 uv2 = refractedCoord / TextureSize;
        float4 r = SampleBlur(uv2 + dispersedCoord / TextureSize, texel, BlurRadius);
        float4 b = SampleBlur(uv2 - dispersedCoord / TextureSize, texel, BlurRadius);
        color = float4(r.r, color.g, b.b, color.a);
    }

    // 边缘镜面高光：只在 HighlightWidth 带内，平方收紧使其更贴边，中心完全不加白。
    float edge = saturate(1.0 - depth / max(HighlightWidth, 1e-3));
    edge *= edge;

    // 用 |ndl| 做上下对称的边缘光：之前额外乘 facing（saturate(ndl*0.5+0.5)）会让迎光侧
    // 比背光侧亮一倍多，深色桌面上顶边明显比底边白。玻璃两侧受光本就该接近，去掉偏置。
    // lerp 下限保证圆头（法线垂直于光向）也有一圈微光，不会突然断掉。
    float ndl = dot(grad, normalize(float2(cos(HighlightAngle), sin(HighlightAngle))));
    float spec = lerp(0.25, 1.0, pow(abs(ndl), HighlightFalloff));

    color.rgb += spec * edge * HighlightStrength;

    // 整面显示模糊背景 + 边缘折射，alpha 恒为 1（不露任何下层，无黑屏）
    color.a = 1.0;
    return color;
}
