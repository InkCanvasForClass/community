using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas.Controls
{
    /// <summary>
    /// 在父容器可用空间内计算最大的固定宽高比矩形，
    /// 子元素按真实布局尺寸排列（无任何缩放变换），多余空间居中留白。
    /// </summary>
    public class FixedAspectRatioPanel : Panel
    {
        public double AspectRatio
        {
            get => (double)GetValue(AspectRatioProperty);
            set => SetValue(AspectRatioProperty, value);
        }

        public static readonly DependencyProperty AspectRatioProperty =
            DependencyProperty.Register(
                nameof(AspectRatio),
                typeof(double),
                typeof(FixedAspectRatioPanel),
                new PropertyMetadata(16.0 / 9.0, (d, _) => ((FixedAspectRatioPanel)d).InvalidateMeasure()));

        protected override Size MeasureOverride(Size availableSize)
        {
            double ratio = AspectRatio;
            double contentWidth, contentHeight;

            double widthByHeight = availableSize.Height * ratio;
            if (widthByHeight <= availableSize.Width)
            {
                contentWidth = widthByHeight;
                contentHeight = availableSize.Height;
            }
            else
            {
                contentWidth = availableSize.Width;
                contentHeight = availableSize.Width / ratio;
            }

            if (double.IsInfinity(contentWidth)) contentWidth = 0;
            if (double.IsInfinity(contentHeight)) contentHeight = 0;

            foreach (UIElement child in InternalChildren)
                child.Measure(new Size(contentWidth, contentHeight));

            return new Size(contentWidth, contentHeight);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            double ratio = AspectRatio;
            double contentWidth, contentHeight;

            double widthByHeight = finalSize.Height * ratio;
            if (widthByHeight <= finalSize.Width)
            {
                contentWidth = widthByHeight;
                contentHeight = finalSize.Height;
            }
            else
            {
                contentWidth = finalSize.Width;
                contentHeight = finalSize.Width / ratio;
            }

            double left = (finalSize.Width - contentWidth) / 2;
            double top = (finalSize.Height - contentHeight) / 2;

            foreach (UIElement child in InternalChildren)
                child.Arrange(new Rect(left, top, contentWidth, contentHeight));

            return finalSize;
        }
    }
}
