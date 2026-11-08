using Ink_Canvas.Helpers;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Media3D;

namespace Ink_Canvas.Shaders
{
    /// <summary>
    /// 展台 GPU 逆透视（梯形）矫正着色器（ps_3_0）。
    /// 利用 HLSL 像素着色器直接在 Direct3D 纹理采样阶段执行逆单应变换（Homography Inversion），
    /// 完全在 GPU 内部完成纸张透视放平与裁切，实现 0 CPU 开销、0 显存拷贝、原生 60FPS 丝滑预览。
    /// </summary>
    public sealed class PerspectiveWarpEffect : ShaderEffect
    {
        private static PixelShader _shared;

        public static readonly DependencyProperty InputProperty =
            RegisterPixelShaderSamplerProperty(nameof(Input), typeof(PerspectiveWarpEffect), 0);

        public static readonly DependencyProperty MatrixRow0Property =
            DependencyProperty.Register(nameof(MatrixRow0), typeof(Point4D), typeof(PerspectiveWarpEffect),
                new UIPropertyMetadata(new Point4D(1, 0, 0, 0), PixelShaderConstantCallback(0)));

        public static readonly DependencyProperty MatrixRow1Property =
            DependencyProperty.Register(nameof(MatrixRow1), typeof(Point4D), typeof(PerspectiveWarpEffect),
                new UIPropertyMetadata(new Point4D(0, 1, 0, 0), PixelShaderConstantCallback(1)));

        public static readonly DependencyProperty MatrixRow2Property =
            DependencyProperty.Register(nameof(MatrixRow2), typeof(Point4D), typeof(PerspectiveWarpEffect),
                new UIPropertyMetadata(new Point4D(0, 0, 1, 0), PixelShaderConstantCallback(2)));

        public static readonly DependencyProperty ParamsProperty =
            DependencyProperty.Register(nameof(Params), typeof(Point4D), typeof(PerspectiveWarpEffect),
                new UIPropertyMetadata(new Point4D(0, 0, 0, 0), PixelShaderConstantCallback(3)));

        public Brush Input
        {
            get => (Brush)GetValue(InputProperty);
            set => SetValue(InputProperty, value);
        }

        public Point4D MatrixRow0
        {
            get => (Point4D)GetValue(MatrixRow0Property);
            set => SetValue(MatrixRow0Property, value);
        }

        public Point4D MatrixRow1
        {
            get => (Point4D)GetValue(MatrixRow1Property);
            set => SetValue(MatrixRow1Property, value);
        }

        public Point4D MatrixRow2
        {
            get => (Point4D)GetValue(MatrixRow2Property);
            set => SetValue(MatrixRow2Property, value);
        }

        public Point4D Params
        {
            get => (Point4D)GetValue(ParamsProperty);
            set => SetValue(ParamsProperty, value);
        }

        public static bool IsShaderAvailable { get; private set; }

        public PerspectiveWarpEffect()
        {
            PixelShader = EnsureShader();

            UpdateShaderValue(InputProperty);
            UpdateShaderValue(MatrixRow0Property);
            UpdateShaderValue(MatrixRow1Property);
            UpdateShaderValue(MatrixRow2Property);
            UpdateShaderValue(ParamsProperty);
        }

        private static PixelShader EnsureShader()
        {
            if (_shared != null) return _shared;

            try
            {
                var shader = new PixelShader
                {
                    UriSource = new Uri(
                        "pack://application:,,,/InkCanvasForClass;component/Shaders/PerspectiveWarpEffect.ps",
                        UriKind.Absolute)
                };
                _shared = shader;
                IsShaderAvailable = true;
                return _shared;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"PerspectiveWarpEffect: 加载像素着色器失败: {ex.Message}", LogHelper.LogType.Warning);
                IsShaderAvailable = false;
                return null;
            }
        }

        /// <summary>
        /// 根据原始视频帧中的 4 个角点（像素坐标系，按 tl, tr, br, bl 顺序），
        /// 计算从目标标准矩形到源画面的逆透视单应变换矩阵并更新着色器常量。
        /// </summary>
        public bool UpdateCorners(IList<OpenCvSharp.Point> corners, double frameWidth, double frameHeight)
        {
            if (corners == null || corners.Count != 4 || frameWidth <= 0 || frameHeight <= 0)
            {
                Params = new Point4D(0, 0, 0, 0); // 关闭纠偏
                return false;
            }

            try
            {
                // 归一化源角点坐标到 [0, 1]
                var srcNorm = new Point2f[4];
                for (int i = 0; i < 4; i++)
                {
                    srcNorm[i] = new Point2f(
                        (float)(corners[i].X / frameWidth),
                        (float)(corners[i].Y / frameHeight));
                }

                // 目标标准矩形：[0, 1] 空间下的四角 (tl, tr, br, bl)
                var dstNorm = new[]
                {
                    new Point2f(0f, 0f), // tl
                    new Point2f(1f, 0f), // tr
                    new Point2f(1f, 1f), // br
                    new Point2f(0f, 1f)  // bl
                };

                // 计算从目标坐标映射回源纹理坐标的单应矩阵 H (dst -> src)
                using var h = Cv2.GetPerspectiveTransform(dstNorm, srcNorm);
                if (h.Empty()) return false;

                double h00 = h.At<double>(0, 0);
                double h01 = h.At<double>(0, 1);
                double h02 = h.At<double>(0, 2);

                double h10 = h.At<double>(1, 0);
                double h11 = h.At<double>(1, 1);
                double h12 = h.At<double>(1, 2);

                double h20 = h.At<double>(2, 0);
                double h21 = h.At<double>(2, 1);
                double h22 = h.At<double>(2, 2);

                MatrixRow0 = new Point4D(h00, h01, h02, 0);
                MatrixRow1 = new Point4D(h10, h11, h12, 0);
                MatrixRow2 = new Point4D(h20, h21, h22, 0);
                Params = new Point4D(1, 0, 0, 0); // 启用纠偏

                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"PerspectiveWarpEffect.UpdateCorners 异常: {ex.Message}", LogHelper.LogType.Warning);
                Params = new Point4D(0, 0, 0, 0);
                return false;
            }
        }

        /// <summary>重置着色器，恢复为原样透传。</summary>
        public void Reset()
        {
            Params = new Point4D(0, 0, 0, 0);
        }
    }
}
