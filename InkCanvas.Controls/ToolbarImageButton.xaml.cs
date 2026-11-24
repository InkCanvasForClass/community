using Ink_Canvas.Helpers.Motion;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas.Controls
{
    public partial class ToolbarImageButton : UserControl
    {
        private static ToolbarImageButton _lastPressedButton;
        private bool _isVerticalOrientation;
        private SpringTranslateAnimator _contentSpring;

        /// <summary>内容层的弹簧驱动器：位移全部由它接管 ButtonContent 的 RenderTransform。</summary>
        private SpringTranslateAnimator ContentSpring =>
            _contentSpring ??= SpringTranslateAnimator.For(ButtonContent);

        public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
            nameof(Label), typeof(string), typeof(ToolbarImageButton),
            new PropertyMetadata(string.Empty, (d, e) => ((ToolbarImageButton)d).LabelTextBlock.Text = (string)e.NewValue));

        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public static readonly DependencyProperty IconGeometryDrawingProperty = DependencyProperty.Register(
            nameof(IconGeometryDrawing), typeof(GeometryDrawing), typeof(ToolbarImageButton),
            new PropertyMetadata(null, OnIconGeometryDrawingChanged));

        private static void OnIconGeometryDrawingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var button = (ToolbarImageButton)d;
            if (e.NewValue is GeometryDrawing newDrawing)
            {
                button.IconGeometryInternal.Geometry = newDrawing.Geometry;
                button.IconGeometryInternal.Brush = newDrawing.Brush;
            }
        }

        public GeometryDrawing IconGeometryDrawing
        {
            get => (GeometryDrawing)GetValue(IconGeometryDrawingProperty);
            set => SetValue(IconGeometryDrawingProperty, value);
        }

        public GeometryDrawing Icon => IconGeometryInternal;

        public GeometryDrawing Badge => BadgeGeometryInternal;

        public GeometryDrawing GeometryDrawing => IconGeometryInternal;

        public static readonly DependencyProperty IconBrushProperty = DependencyProperty.Register(
            nameof(IconBrush), typeof(Brush), typeof(ToolbarImageButton),
            new PropertyMetadata(null, (d, e) => ((ToolbarImageButton)d).IconGeometryInternal.Brush = (Brush)e.NewValue));

        public Brush IconBrush
        {
            get => (Brush)GetValue(IconBrushProperty);
            set => SetValue(IconBrushProperty, value);
        }

        private GeometryDrawing _iconInnerOutlineOverlay;

        /// <summary>
        /// 为图标添加向内描边：描边完全位于图标轮廓内部，不会改变图标的视觉大小。
        /// 实现方式为以 2 倍线宽居中描边，并将整层裁剪到图标轮廓内，外半圈被裁掉，只保留向内的半圈。
        /// </summary>
        /// <param name="outlineBrush">描边颜色</param>
        /// <param name="width">描边可见宽度（图标 24x24 坐标系单位）</param>
        public void SetIconInnerOutline(Brush outlineBrush, double width)
        {
            if (IconGeometryInternal == null || IconClipGroup == null) return;
            var geometry = IconGeometryInternal.Geometry;
            if (geometry == null) return;

            if (_iconInnerOutlineOverlay == null)
            {
                _iconInnerOutlineOverlay = new GeometryDrawing();
                IconClipGroup.Children.Add(_iconInnerOutlineOverlay);
            }

            IconClipGroup.ClipGeometry = geometry;
            _iconInnerOutlineOverlay.Geometry = geometry;
            _iconInnerOutlineOverlay.Brush = null;
            _iconInnerOutlineOverlay.Pen = new Pen(outlineBrush, width * 2)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
        }

        /// <summary>
        /// 移除图标描边。
        /// </summary>
        public void ClearIconInnerOutline()
        {
            if (IconClipGroup == null) return;

            IconClipGroup.ClipGeometry = null;
            if (_iconInnerOutlineOverlay != null)
            {
                IconClipGroup.Children.Remove(_iconInnerOutlineOverlay);
                _iconInnerOutlineOverlay = null;
            }
        }

        public static readonly DependencyProperty LabelBrushProperty = DependencyProperty.Register(
            nameof(LabelBrush), typeof(Brush), typeof(ToolbarImageButton),
            new PropertyMetadata(null, (d, e) =>
            {
                var b = (ToolbarImageButton)d;
                if (e.NewValue is Brush brush) b.LabelTextBlock.Foreground = brush;
            }));

        public Brush LabelBrush
        {
            get => (Brush)GetValue(LabelBrushProperty);
            set => SetValue(LabelBrushProperty, value);
        }

        public new Brush Background
        {
            get => ButtonBorder.Background;
            set => ButtonBorder.Background = value;
        }

        public double LabelFontSize
        {
            get => LabelTextBlock.FontSize;
            set => LabelTextBlock.FontSize = value;
        }

        public TextBlock LabelTextBlockControl => LabelTextBlock;

        public double IconHeight
        {
            get => ButtonImage.Height;
            set => ButtonImage.Height = value;
        }

        public void ApplyOrientation(bool isVertical)
        {
            _isVerticalOrientation = isVertical;

            // 未选中态的内容偏移（横排下移 2px、竖排左移 2px）必须建控件时就落在位上。
            // 否则"从没选中过"的按钮停在 0 偏移，比"选中过又取消"的按钮高 2px，
            // 于是同一工具栏会出现三种高度：没选中过的 / 选中的 / 选中过但已取消的。
            ContentSpring.SetOffset(isVertical ? new Vector(-2, 0) : new Vector(0, 2));

            if (isVertical)
            {
                // 竖排滑槽实际就是 52x44（ButtonPanel 43x44 + 左右边距 2/7），
                // 写死在这里，首次测量不再等图标内容。
                Width = 52;
                Height = 44;
                ButtonPanel.Width = 43;
                ButtonPanel.Height = 44;
                ButtonBorder.Margin = new Thickness(2, 0, 7, 0);
            }
            else
            {
                // 横排滑槽 44x52（ButtonPanel 44x43 + 上下边距 2/7）。
                Width = 44;
                Height = 52;
                ButtonPanel.Width = 44;
                ButtonPanel.Height = 43;
                ButtonBorder.Margin = new Thickness(0, 2, 0, 7);
            }
        }

        /// <summary>
        /// 应用紧凑浮动栏模式：开启后隐藏常驻文字标签，并让图标在保持纵横比的前提下拉伸填满空出的区域。
        /// </summary>
        public void ApplyCompactMode(bool compact)
        {
            if (compact)
            {
                LabelTextBlock.Visibility = Visibility.Collapsed;
                ButtonImage.HorizontalAlignment = HorizontalAlignment.Stretch;
                ButtonImage.VerticalAlignment = VerticalAlignment.Stretch;
                ButtonImage.Stretch = Stretch.Uniform;
                ButtonImage.Width = double.NaN;
                ButtonImage.Height = double.NaN;
                ButtonImage.Margin = new Thickness(2);
            }
            else
            {
                LabelTextBlock.Visibility = Visibility.Visible;
                ButtonImage.HorizontalAlignment = HorizontalAlignment.Center;
                ButtonImage.VerticalAlignment = VerticalAlignment.Top;
                ButtonImage.Stretch = Stretch.Uniform;
                ButtonImage.Width = 24;
                ButtonImage.Height = 24;
                ButtonImage.Margin = new Thickness(0, 1, 0, 0);
            }
        }

        /// <summary>
        /// 切换选中/未选中的视觉偏移（选中上移 2px，未选中居中）。
        /// </summary>
        /// <param name="isSelected">是否处于选中态。</param>
        /// <param name="spec">弹簧手感；不传时用轻快档（小位移不需要慢速）。</param>
        public void SetSelectedVisualOffset(bool isSelected, SpringSpec? spec = null)
        {
            if (ButtonContent == null) return;

            // 边距随状态瞬时切换：每次切换只触发一次布局，逐帧位移交给弹簧在合成线程收敛。
            if (_isVerticalOrientation)
            {
                // 选定项：左边距归零，内容不动
                // 非选定项：恢复左边距，内容向左偏移2px居中
                ButtonBorder.Margin = isSelected
                    ? new Thickness(0, 0, 7, 0)
                    : new Thickness(2, 0, 7, 0);
            }
            else
            {
                // 选定项：上边距归零，内容不动
                // 非选定项：恢复上边距，内容下移2px居中
                ButtonBorder.Margin = isSelected
                    ? new Thickness(0, 0, 0, 7)
                    : new Thickness(0, 2, 0, 7);
            }

            var target = _isVerticalOrientation
                ? new Vector(isSelected ? 0 : -2, 0)
                : new Vector(0, isSelected ? 0 : 2);

            var spring = ContentSpring;
            var from = spring.CurrentOffset;

            // 已经稳稳停在目标上就不必再起一条弹簧（多数是启动后的第一次同步）。
            if (!spring.IsRunning && (from - target).Length < 0.5) return;

            // 起点与初速度都取自当前弹簧状态：动画中途再次切换是重定向，不会停一下重来。
            _ = spring.RunAsync(from, target, spring.CurrentVelocity, spec ?? SpringSpec.Snappy);
        }

        public event MouseButtonEventHandler ButtonMouseDown;
        public event MouseEventHandler ButtonMouseLeave;
        public event MouseButtonEventHandler ButtonMouseUp;

        public ToolbarImageButton()
        {
            InitializeComponent();
            ButtonPanel.Background = Brushes.Transparent;
            IsEnabledChanged += ToolbarImageButton_IsEnabledChanged;
        }

        private void ToolbarImageButton_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var isEnabled = (bool)e.NewValue;
            ButtonImage.Opacity = isEnabled ? 1.0 : 0.5;
            LabelTextBlock.Opacity = isEnabled ? 1.0 : 0.5;
            ButtonPanel.IsEnabled = isEnabled;
        }

        private void ButtonPanel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsEnabled) return;
            if (_lastPressedButton != null && _lastPressedButton != this)
            {
                _lastPressedButton.PressedBackground.Background = Brushes.Transparent;
            }
            _lastPressedButton = this;
            PressedBackground.Background = new SolidColorBrush(Color.FromArgb(28, 24, 24, 27));
            ButtonMouseDown?.Invoke(this, e);
        }

        private void ButtonPanel_MouseLeave(object sender, MouseEventArgs e)
        {
            if (!IsEnabled) return;
            if (_lastPressedButton == this)
            {
                PressedBackground.Background = Brushes.Transparent;
                _lastPressedButton = null;
            }
            ButtonMouseLeave?.Invoke(this, e);
        }

        private void ButtonPanel_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!IsEnabled) return;
            if (_lastPressedButton == this)
            {
                PressedBackground.Background = Brushes.Transparent;
                _lastPressedButton = null;
            }
            ButtonMouseUp?.Invoke(this, e);
        }
    }
}
