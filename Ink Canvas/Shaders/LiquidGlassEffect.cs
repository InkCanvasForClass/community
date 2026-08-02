using Ink_Canvas.Helpers;
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Ink_Canvas.Shaders
{
    /// <summary>
    /// 液态玻璃折射着色器（ps_3_0）。移植自 AndroidLiquidGlass（Apache-2.0）：
    /// SDF 圆角矩形只在边缘带（<see cref="RefractionHeight"/>）内做折射，中心区域原样
    /// 透出桌面截图；可选 7 采样色散；最后叠一层沿圆角法线分布的高光。
    /// 着色器源码见 Shaders/LiquidGlassEffect.hlsl（用 d3dcompiler_47 的 D3DCompile 编译）。
    /// </summary>
    public sealed class LiquidGlassEffect : ShaderEffect
    {
        private static PixelShader _shared;

        public static readonly DependencyProperty InputProperty =
            RegisterPixelShaderSamplerProperty(nameof(Input), typeof(LiquidGlassEffect), 0);

        public static readonly DependencyProperty TextureSizeProperty =
            DependencyProperty.Register(nameof(TextureSize), typeof(Point), typeof(LiquidGlassEffect),
                new UIPropertyMetadata(new Point(1.0, 1.0), PixelShaderConstantCallback(0)));

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(nameof(CornerRadius), typeof(float), typeof(LiquidGlassEffect),
                new UIPropertyMetadata(20f, PixelShaderConstantCallback(1)));

        public static readonly DependencyProperty RefractionHeightProperty =
            DependencyProperty.Register(nameof(RefractionHeight), typeof(float), typeof(LiquidGlassEffect),
                new UIPropertyMetadata(10f, PixelShaderConstantCallback(2)));

        public static readonly DependencyProperty RefractionAmountProperty =
            DependencyProperty.Register(nameof(RefractionAmount), typeof(float), typeof(LiquidGlassEffect),
                new UIPropertyMetadata(-8f, PixelShaderConstantCallback(3)));

        public static readonly DependencyProperty DepthEffectProperty =
            DependencyProperty.Register(nameof(DepthEffect), typeof(float), typeof(LiquidGlassEffect),
                new UIPropertyMetadata(0f, PixelShaderConstantCallback(4)));

        public static readonly DependencyProperty ChromaticAberrationProperty =
            DependencyProperty.Register(nameof(ChromaticAberration), typeof(float), typeof(LiquidGlassEffect),
                new UIPropertyMetadata(0.5f, PixelShaderConstantCallback(5)));

        public static readonly DependencyProperty HighlightAngleProperty =
            DependencyProperty.Register(nameof(HighlightAngle), typeof(float), typeof(LiquidGlassEffect),
                new UIPropertyMetadata((float)(Math.PI / 2.0), PixelShaderConstantCallback(6)));

        public static readonly DependencyProperty HighlightFalloffProperty =
            DependencyProperty.Register(nameof(HighlightFalloff), typeof(float), typeof(LiquidGlassEffect),
                new UIPropertyMetadata(1f, PixelShaderConstantCallback(7)));

        public static readonly DependencyProperty HighlightStrengthProperty =
            DependencyProperty.Register(nameof(HighlightStrength), typeof(float), typeof(LiquidGlassEffect),
                new UIPropertyMetadata(0.35f, PixelShaderConstantCallback(8)));

        public static readonly DependencyProperty HighlightWidthProperty =
            DependencyProperty.Register(nameof(HighlightWidth), typeof(float), typeof(LiquidGlassEffect),
                new UIPropertyMetadata(6f, PixelShaderConstantCallback(9)));

        public static readonly DependencyProperty BlurRadiusProperty =
            DependencyProperty.Register(nameof(BlurRadius), typeof(float), typeof(LiquidGlassEffect),
                new UIPropertyMetadata(4f, PixelShaderConstantCallback(10)));

        /// <summary>着色器二进制是否成功加载。失败时调用方应退回纯色/亚克力背景。</summary>
        public static bool IsShaderAvailable { get; private set; }

        public LiquidGlassEffect()
        {
            PixelShader = EnsureShader();

            UpdateShaderValue(InputProperty);
            UpdateShaderValue(TextureSizeProperty);
            UpdateShaderValue(CornerRadiusProperty);
            UpdateShaderValue(RefractionHeightProperty);
            UpdateShaderValue(RefractionAmountProperty);
            UpdateShaderValue(DepthEffectProperty);
            UpdateShaderValue(ChromaticAberrationProperty);
            UpdateShaderValue(HighlightAngleProperty);
            UpdateShaderValue(HighlightFalloffProperty);
            UpdateShaderValue(HighlightStrengthProperty);
            UpdateShaderValue(HighlightWidthProperty);
            UpdateShaderValue(BlurRadiusProperty);
        }

        private static PixelShader EnsureShader()
        {
            if (_shared != null) return _shared;

            try
            {
                var shader = new PixelShader
                {
                    UriSource = new Uri(
                        "pack://application:,,,/InkCanvasForClass;component/Shaders/LiquidGlassEffect.ps",
                        UriKind.Absolute)
                };
                _shared = shader;
                IsShaderAvailable = true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile(
                    $"液态玻璃着色器加载失败，将退回无折射背景: {ex.Message}", LogHelper.LogType.Warning);
                _shared = new PixelShader();
                IsShaderAvailable = false;
            }

            return _shared;
        }

        public Brush Input
        {
            get => (Brush)GetValue(InputProperty);
            set => SetValue(InputProperty, value);
        }

        /// <summary>输入纹理（折射层）尺寸，DIP。</summary>
        public Point TextureSize
        {
            get => (Point)GetValue(TextureSizeProperty);
            set => SetValue(TextureSizeProperty, value);
        }

        /// <summary>圆角半径，DIP（胶囊=高度一半）。</summary>
        public float CornerRadius
        {
            get => (float)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        /// <summary>边缘折射带宽（px）。中心区域不折射，原样透出桌面。</summary>
        public float RefractionHeight
        {
            get => (float)GetValue(RefractionHeightProperty);
            set => SetValue(RefractionHeightProperty, value);
        }

        /// <summary>折射位移幅度（px）。负值=向内侧采样（边缘透镜放大）。</summary>
        public float RefractionAmount
        {
            get => (float)GetValue(RefractionAmountProperty);
            set => SetValue(RefractionAmountProperty, value);
        }

        /// <summary>0 或 1：叠加径向分量，增强边缘立体感。</summary>
        public float DepthEffect
        {
            get => (float)GetValue(DepthEffectProperty);
            set => SetValue(DepthEffectProperty, value);
        }

        /// <summary>0 关闭色散；>0 打开 7 采样色差（强度系数）。</summary>
        public float ChromaticAberration
        {
            get => (float)GetValue(ChromaticAberrationProperty);
            set => SetValue(ChromaticAberrationProperty, value);
        }

        /// <summary>高光方向（弧度）。</summary>
        public float HighlightAngle
        {
            get => (float)GetValue(HighlightAngleProperty);
            set => SetValue(HighlightAngleProperty, value);
        }

        /// <summary>高光衰减指数。</summary>
        public float HighlightFalloff
        {
            get => (float)GetValue(HighlightFalloffProperty);
            set => SetValue(HighlightFalloffProperty, value);
        }

        /// <summary>高光强度 0..1。</summary>
        public float HighlightStrength
        {
            get => (float)GetValue(HighlightStrengthProperty);
            set => SetValue(HighlightStrengthProperty, value);
        }

        /// <summary>
        /// 高光带宽（px）。高光只出现在距边界这个距离内，中心不加白。
        /// 铺满整面会让扁胶囊整体发雾（梯度是单位法线、不随深度衰减）。
        /// </summary>
        public float HighlightWidth
        {
            get => (float)GetValue(HighlightWidthProperty);
            set => SetValue(HighlightWidthProperty, value);
        }

        /// <summary>内部连续高斯模糊半径（px）。着色器对清晰截图模糊，玻璃的磨砂感。</summary>
        public float BlurRadius
        {
            get => (float)GetValue(BlurRadiusProperty);
            set => SetValue(BlurRadiusProperty, value);
        }
    }
}
