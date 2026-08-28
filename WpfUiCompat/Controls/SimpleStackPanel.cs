using System.Windows;
using System.Windows.Controls;

namespace WpfUiCompat.Controls
{
    /// <summary>
    /// 支持子元素间距（Spacing）的堆叠面板，兼容 iNKORE SimpleStackPanel API。
    /// </summary>
    public class SimpleStackPanel : Panel
    {
        public static readonly DependencyProperty OrientationProperty =
            DependencyProperty.Register(
                nameof(Orientation),
                typeof(Orientation),
                typeof(SimpleStackPanel),
                new FrameworkPropertyMetadata(Orientation.Vertical, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

        public Orientation Orientation
        {
            get => (Orientation)GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        public static readonly DependencyProperty SpacingProperty =
            DependencyProperty.Register(
                nameof(Spacing),
                typeof(double),
                typeof(SimpleStackPanel),
                new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

        public double Spacing
        {
            get => (double)GetValue(SpacingProperty);
            set => SetValue(SpacingProperty, value);
        }

        protected override System.Windows.Size MeasureOverride(System.Windows.Size constraint)
        {
            var size = new System.Windows.Size();
            bool isHorizontal = Orientation == Orientation.Horizontal;
            double spacing = Spacing;
            bool first = true;

            // 测量阶段不扩展约束高度：StackPanel 家族的惯例是把 stack 方向约束传 PositiveInfinity，
            // 让子元素按内容测量（否则子元素会被截断/引发 Grid 内部参数异常）。
            var childConstraint = isHorizontal
                ? new System.Windows.Size(constraint.Width, double.PositiveInfinity)
                : new System.Windows.Size(double.PositiveInfinity, constraint.Height);

            foreach (UIElement child in InternalChildren)
            {
                if (child == null) continue;

                child.Measure(childConstraint);

                var desired = child.DesiredSize;
                if (isHorizontal)
                {
                    size.Width += desired.Width + (first ? 0 : spacing);
                    size.Height = System.Math.Max(size.Height, desired.Height);
                }
                else
                {
                    size.Height += desired.Height + (first ? 0 : spacing);
                    size.Width = System.Math.Max(size.Width, desired.Width);
                }
                first = false;
            }

            return size;
        }

        protected override System.Windows.Size ArrangeOverride(System.Windows.Size arrangeSize)
        {
            bool isHorizontal = Orientation == Orientation.Horizontal;
            double spacing = Spacing;
            double offset = 0;

            foreach (UIElement child in InternalChildren)
            {
                if (child == null) continue;

                var desired = child.DesiredSize;
                if (isHorizontal)
                {
                    double height = System.Math.Max(desired.Height, arrangeSize.Height);
                    child.Arrange(new System.Windows.Rect(offset, 0, desired.Width, height));
                    offset += desired.Width + spacing;
                }
                else
                {
                    double width = System.Math.Max(desired.Width, arrangeSize.Width);
                    child.Arrange(new System.Windows.Rect(0, offset, width, desired.Height));
                    offset += desired.Height + spacing;
                }
            }

            return arrangeSize;
        }
    }
}