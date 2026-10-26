using System;
using System.Windows;
using System.Windows.Media;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 继承自 FrameworkElement，自行管理 Source + Stretch 渲染。
    /// 行为与 WPFMediaKit 的 VideoCaptureElement 一致：
    ///   MeasureOverride / ArrangeOverride 都返回可用空间本身（填满容器），
    ///   OnRender 在 RenderSize 内按 Stretch=Uniform 居中绘制图像（有黑边但居中）。
    /// 不继承 Image：Image.ArrangeOverride 返回按图像比例 fit 后的尺寸（小于容器），
    /// 且内部 _arrangedSize 在 LayoutTransform 旋转后会与实际不匹配导致拉伸。
    /// 用于拍照后的照片预览，使其与实时画面走完全相同的变换管线。
    /// </summary>
    public class FillImage : FrameworkElement
    {
        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
            "Source", typeof(ImageSource), typeof(FillImage),
            new FrameworkPropertyMetadata(null,
                FrameworkPropertyMetadataOptions.AffectsMeasure |
                FrameworkPropertyMetadataOptions.AffectsRender));

        public ImageSource Source
        {
            get => (ImageSource)GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        public static readonly DependencyProperty StretchProperty = DependencyProperty.Register(
            "Stretch", typeof(Stretch), typeof(FillImage),
            new FrameworkPropertyMetadata(Stretch.Uniform,
                FrameworkPropertyMetadataOptions.AffectsRender));

        public Stretch Stretch
        {
            get => (Stretch)GetValue(StretchProperty);
            set => SetValue(StretchProperty, value);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            // 强制返回可用空间本身，让元素填满容器（与 VideoCaptureElement 一致）
            return availableSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            // 返回 finalSize，让 RenderSize = finalSize = 容器尺寸
            return finalSize;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            var source = Source;
            if (source == null) return;

            double imgW = source.Width;
            double imgH = source.Height;
            if (imgW <= 0 || imgH <= 0) return;

            double renderW = RenderSize.Width;
            double renderH = RenderSize.Height;
            if (renderW <= 0 || renderH <= 0) return;

            // 按 Stretch=Uniform 在 RenderSize 内 fit 居中绘制（与 VideoCaptureElement D3D 行为一致）
            double scaleX = renderW / imgW;
            double scaleY = renderH / imgH;
            double scale = Math.Min(scaleX, scaleY);

            double drawW = imgW * scale;
            double drawH = imgH * scale;
            double offsetX = (renderW - drawW) / 2.0;
            double offsetY = (renderH - drawH) / 2.0;

            drawingContext.DrawImage(source, new Rect(offsetX, offsetY, drawW, drawH));
        }
    }
}
