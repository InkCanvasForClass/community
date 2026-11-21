using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using Ink_Canvas.Helpers;
using Ink_Canvas.Helpers.Motion;

namespace Ink_Canvas.PopupDiagnostics
{
    /// <summary>
    /// 一个最小可用的菜单弹出环境：触发按钮 + Popup + 与生产同款的包装与变换。
    /// </summary>
    /// <remarks>
    /// 这里刻意**不**链接 AnimationsHelper：那个文件带着整套 WPF/日志/系统交互依赖，
    /// 链进来会把诊断工程变成半个主工程。代价是打开/收起流程要在这里复刻一遍，
    /// 所以下面每一步都注明它对应 AnimationsHelper 里的哪一处 —— 顺序就是被测对象。
    ///
    /// 复刻的顺序（AnimationsHelper.DrivePopup / ShowPopupWithSlideAndFade）：
    ///   1. PopupOverflowRoom.Ensure      包装宿主，预留过冲空间
    ///   2. 宿主与内容都设为 Visible       ← 曾经只设内容，导致第二次打开原点算错
    ///   3. popup.IsOpen = true
    ///   4. RefreshPadding
    ///   5. ClearTransformAnimations      归位，避免残留变换污染坐标换算
    ///   6. PopupScaleOrigin.Resolve      解析缩放原点
    ///   7. SetPopupProgress              按当前进度写初值
    /// </remarks>
    internal sealed class MenuHarness
    {
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out Rect32 rect);

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect32
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private readonly Popup _popup;
        private readonly Button _trigger;

        private FrameworkElement _content;
        private FrameworkElement _host;
        private ScaleTransform _scale;
        private TranslateTransform _slide;
        private PopupCurveShape _shape;

        private MenuHarness(Popup popup, Button trigger)
        {
            _popup = popup;
            _trigger = trigger;
        }

        /// <summary>宿主当前的留白，直接体现过冲空间的多少。</summary>
        public Thickness HostPadding
        {
            get
            {
                var border = _host as Border;
                return border == null ? default(Thickness) : border.Padding;
            }
        }

        /// <summary>
        /// 搭好触发按钮与弹窗，并复刻 PopupManagerHelper 在关闭时折叠宿主的行为。
        /// </summary>
        public static MenuHarness Create(Window window)
        {
            var grid = window.Content as Grid ?? new Grid();
            var trigger = new Button
            {
                Width = 49,
                Height = 49,
                Content = "触发",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom
            };
            grid.Children.Add(trigger);

            var content = new Border
            {
                Width = 260,
                Height = 300,
                Background = new SolidColorBrush(Color.FromRgb(0x46, 0x82, 0xB4))
            };

            var popup = new Popup
            {
                Name = "DiagnosticMenu",
                AllowsTransparency = true,
                StaysOpen = true,
                Focusable = true,
                PlacementTarget = trigger,
                Placement = PlacementMode.Custom,
                Child = content,
                CustomPopupPlacementCallback = (popupSize, targetSize, offset) =>
                    new[]
                    {
                        new CustomPopupPlacement(
                            new Point(targetSize.Width / 2 - popupSize.Width / 2, -popupSize.Height - 8),
                            PopupPrimaryAxis.Vertical)
                    }
            };

            // PopupManagerHelper.OnPopupClosed 的原样行为：折叠 popup.Child（即宿主 Border）。
            // 第二次打开原点算错，正是从这里来的 —— 诊断工具必须保留它，否则测不出回归。
            popup.Closed += (sender, args) =>
            {
                if (popup.Child is FrameworkElement child) child.Visibility = Visibility.Collapsed;
            };

            return new MenuHarness(popup, trigger);
        }

        /// <summary>
        /// 打开菜单，返回**同步阶段**解析到的缩放原点（这一刻就是曾经出错的地方）。
        /// </summary>
        public Point OpenAndResolveOrigin(double startProgress)
        {
            _content = PopupOverflowRoom.Ensure(_popup, out _shape) ?? _popup.Child as FrameworkElement;
            _host = _popup.Child as FrameworkElement;

            if (_host != null && _host.Visibility != Visibility.Visible) _host.Visibility = Visibility.Visible;
            if (_content != null) _content.Visibility = Visibility.Visible;

            _popup.IsOpen = true;
            PopupOverflowRoom.RefreshPadding(_popup, _shape);

            AttachTransform();
            ResetTransform();
            var origin = PopupScaleOrigin.Resolve(_popup, _content);
            _content.RenderTransformOrigin = origin;
            ApplyProgress(startProgress);

            return origin;
        }

        /// <summary>当前生效的缩放原点（用于和打开那一刻的取值对比）。</summary>
        public Point CurrentOrigin
        {
            get { return _content == null ? new Point(0.5, 0.5) : _content.RenderTransformOrigin; }
        }

        /// <summary>
        /// 原点（RenderTransformOrigin 指向的那个点）当前所在的屏幕坐标。
        /// </summary>
        /// <remarks>
        /// 这个点按定义就是"缩放时保持不动"的那个点，因此把它的位移和
        /// 设计滑动量相减，就能验证渲染出来的位移确实只来自滑动。
        /// 注意 <c>PointToScreen</c> 会被物理像素对齐量化到整像素，
        /// 所以残差在 1px 以内属于度量误差，不是几何误差。
        /// </remarks>
        public Point OriginScreenPoint()
        {
            var origin = _content.RenderTransformOrigin;
            return _content.PointToScreen(new Point(origin.X * _content.ActualWidth, origin.Y * _content.ActualHeight));
        }

        /// <summary>菜单停稳后与触发按钮顶边之间的间距（与 PlacementCallback 里的值一致）。</summary>
        internal const double PlacementGapPixels = 8.0;

        /// <summary>
        /// 本次打开所用的曲线形状：起点按菜单自然宽度导出，峰值与位移都从它派生。
        /// </summary>
        /// <remarks>
        /// 生产路径由 <see cref="PopupOverflowRoom.Ensure"/> 一次算好并放进动画 token；
        /// 这里在它还没算过时（例如从未打开过就调用 Close）按老菜单的宽度兜个底，
        /// 免得后续除零或写进 NaN 缩放——NaN 会让 WPF 直接不绘制。
        /// </remarks>
        public PopupCurveShape Shape
        {
            get
            {
                if (_shape.StartScale > 0.0) return _shape;
                _shape = _content == null
                    ? PopupCurveShape.ForContentWidth(PopupCurve.CompactWidth)
                    : PopupCurveShape.ForContentWidth(ContentWidth);
                return _shape;
            }
        }

        /// <summary>菜单内容的自然宽度（DIP）：布局宽度优先，未布局时退回显式 Width。</summary>
        private double ContentWidth
        {
            get
            {
                if (_content == null) return PopupCurve.CompactWidth;
                if (_content.ActualWidth > 0.0) return _content.ActualWidth;
                if (!double.IsNaN(_content.Width) && _content.Width > 0.0) return _content.Width;
                return PopupCurve.CompactWidth;
            }
        }

        /// <summary>当前生效的纵向滑动量（DIP，正=向下）。</summary>
        public double CurrentSlideY
        {
            get { return _slide == null ? 0.0 : _slide.Y; }
        }

        /// <summary>触发按钮顶边中点的屏幕坐标，用来核对不动点是否指向按钮。</summary>
        public Point TriggerTopCenterOnScreen()
        {
            return _trigger.PointToScreen(new Point(_trigger.ActualWidth / 2.0, 0.0));
        }

        /// <summary>按进度写缩放与位移，等价于 AnimationsHelper.SetPopupProgress 的几何部分。</summary>
        public void ApplyProgress(double progress)
        {
            if (_content == null) return;

            var sample = Shape.Evaluate(progress);
            _scale.ScaleX = sample.Scale;
            _scale.ScaleY = sample.Scale;
            _slide.Y = sample.SlideY;
            _content.Opacity = sample.OpacityWeight;
        }

        /// <summary>关闭菜单，走与 Hidden 结束后相同的收尾。</summary>
        public void Close()
        {
            if (_popup == null || !_popup.IsOpen) return;
            ApplyProgress(0.0);
            _popup.IsOpen = false;
        }

        /// <summary>
        /// 量出内容缩放后越出弹窗窗口的最大像素数。0 表示没有裁切。
        /// </summary>
        public double MeasureOverflow(out double scale)
        {
            scale = _scale == null ? 1.0 : _scale.ScaleX;

            var source = PresentationSource.FromVisual(_content) as HwndSource;
            if (source == null) return double.NaN;

            Rect32 rect;
            if (!GetWindowRect(source.Handle, out rect)) return double.NaN;

            var topLeft = _content.PointToScreen(new Point(0, 0));
            var bottomRight = _content.PointToScreen(new Point(_content.ActualWidth, _content.ActualHeight));

            // PointToScreen 与 GetWindowRect 都返回屏幕物理像素，单位一致，无需 DPI 换算。
            var left = rect.Left - topLeft.X;
            var top = rect.Top - topLeft.Y;
            var right = bottomRight.X - rect.Right;
            var bottom = bottomRight.Y - rect.Bottom;

            var overflow = Math.Max(Math.Max(left, top), Math.Max(right, bottom));
            return overflow > 0.0 ? overflow : 0.0;
        }

        private void AttachTransform()
        {
            var group = _content.RenderTransform as TransformGroup;
            if (group == null || group.Children.Count < 2 ||
                !(group.Children[0] is ScaleTransform) || !(group.Children[1] is TranslateTransform))
            {
                group = new TransformGroup();
                _scale = new ScaleTransform(1.0, 1.0);
                _slide = new TranslateTransform(0.0, 0.0);
                group.Children.Add(_scale);
                group.Children.Add(_slide);
                _content.RenderTransform = group;
            }
            else
            {
                _scale = (ScaleTransform)group.Children[0];
                _slide = (TranslateTransform)group.Children[1];
            }
        }

        /// <summary>等价于 ClearTransformAnimations(element, 1.0, 0.0)：摘掉动画并归位。</summary>
        private void ResetTransform()
        {
            _scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            _scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            _slide.BeginAnimation(TranslateTransform.YProperty, null);
            _scale.ScaleX = 1.0;
            _scale.ScaleY = 1.0;
            _slide.Y = 0.0;
        }

        /// <summary>等到下一个渲染帧，让布局与窗口矩形都收敛。</summary>
        public static Task WaitForFrameAsync()
        {
            var completion = new TaskCompletionSource<bool>();
            EventHandler handler = null;
            handler = (sender, args) =>
            {
                CompositionTarget.Rendering -= handler;
                completion.TrySetResult(true);
            };
            CompositionTarget.Rendering += handler;
            return completion.Task;
        }

        /// <summary>等若干帧，用于让弹窗窗口矩形完成重定位。</summary>
        public static async Task WaitForFramesAsync(int frames)
        {
            for (var i = 0; i < frames; i++) await WaitForFrameAsync();
        }
    }
}
