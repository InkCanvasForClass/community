using Ink_Canvas.Helpers;
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Ink_Canvas.Shaders
{
    /// <summary>
    /// 液态玻璃折射着色器（ps_3_0）。对输入纹理做边缘折射 + 轻微模糊，
    /// 模拟一块厚玻璃压在桌面截图上的效果。
    /// 着色器二进制来自 wpf-liquid-glass-window（MIT）。
    /// </summary>
    public sealed class LiquidGlassEffect : ShaderEffect
    {
        private static PixelShader _shared;

        public static readonly DependencyProperty InputProperty =
            RegisterPixelShaderSamplerProperty(nameof(Input), typeof(LiquidGlassEffect), 0);

        public static readonly DependencyProperty TextureSizeProperty =
            DependencyProperty.Register(nameof(TextureSize), typeof(Point), typeof(LiquidGlassEffect),
                new UIPropertyMetadata(new Point(1.0, 1.0), PixelShaderConstantCallback(0)));

        public static readonly DependencyProperty GlassCenterProperty =
            DependencyProperty.Register(nameof(GlassCenter), typeof(Point), typeof(LiquidGlassEffect),
                new UIPropertyMetadata(new Point(0.0, 0.0), PixelShaderConstantCallback(1)));

        public static readonly DependencyProperty GlassSizeProperty =
            DependencyProperty.Register(nameof(GlassSize), typeof(Point), typeof(LiquidGlassEffect),
                new UIPropertyMetadata(new Point(120.0, 80.0), PixelShaderConstantCallback(2)));

        public static readonly DependencyProperty BlurIntensityProperty =
            DependencyProperty.Register(nameof(BlurIntensity), typeof(float), typeof(LiquidGlassEffect),
                new UIPropertyMetadata(0.35f, PixelShaderConstantCallback(3)));

        /// <summary>着色器二进制是否成功加载。失败时调用方应退回纯色/亚克力背景。</summary>
        public static bool IsShaderAvailable { get; private set; }

        public LiquidGlassEffect()
        {
            PixelShader = EnsureShader();

            UpdateShaderValue(InputProperty);
            UpdateShaderValue(TextureSizeProperty);
            UpdateShaderValue(GlassCenterProperty);
            UpdateShaderValue(GlassSizeProperty);
            UpdateShaderValue(BlurIntensityProperty);
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

        /// <summary>输入纹理尺寸（DIP）。</summary>
        public Point TextureSize
        {
            get => (Point)GetValue(TextureSizeProperty);
            set => SetValue(TextureSizeProperty, value);
        }

        /// <summary>玻璃中心（相对纹理左上角，DIP）。</summary>
        public Point GlassCenter
        {
            get => (Point)GetValue(GlassCenterProperty);
            set => SetValue(GlassCenterProperty, value);
        }

        /// <summary>玻璃体尺寸（DIP）。</summary>
        public Point GlassSize
        {
            get => (Point)GetValue(GlassSizeProperty);
            set => SetValue(GlassSizeProperty, value);
        }

        /// <summary>模糊强度，0 为不模糊。</summary>
        public float BlurIntensity
        {
            get => (float)GetValue(BlurIntensityProperty);
            set => SetValue(BlurIntensityProperty, value);
        }
    }
}
