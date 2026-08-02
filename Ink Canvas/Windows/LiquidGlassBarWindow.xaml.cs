using Ink_Canvas.Helpers;
using Ink_Canvas.Shaders;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace Ink_Canvas
{
    /// <summary>
    /// 液态玻璃浮动栏：独立的置顶、不可激活胶囊窗口，把桌面截图经折射着色器处理后作为自身背景，
    /// 呈现一块厚玻璃压在桌面上的效果。工具按钮转发到 <see cref="MainWindow"/> 的既有处理器。
    /// 参考 wpf-liquid-glass-window（MIT）的三层结构：截图背景 → 折射 → 半透明内容。
    /// </summary>
    public partial class LiquidGlassBarWindow : Window
    {
        private const int GwlExStyle = -20;
        private const int WsExNoActivate = 0x08000000;
        private const int WsExToolWindow = 0x00000080;
        private const int SwHide = 0;
        private const int SwShowNoActivate = 4;

        private readonly MainWindow _owner;
        private ImageBrush _backdropBrush;
        private LiquidGlassEffect _effect;
        private DispatcherTimer _refreshTimer;
        private RectangleGeometry _glassRootClip;
        private RectangleGeometry _glassLayersClip;
        private bool _isCapturing;
        private bool _isClosing;

        /// <summary>胶囊圆角半径。与 XAML 中各层的 CornerRadius 保持一致。</summary>
        private const double GlassCornerRadius = 26;

        // 拖动状态
        private bool _dragging;
        private Point _dragOrigin;
        private double _dragStartLeft;
        private double _dragStartTop;
        private bool _dragMoved;

        internal LiquidGlassBarWindow(MainWindow owner)
        {
            _owner = owner;
            InitializeComponent();
        }

        /// <summary>玻璃体本身的不透明度（不含内容），由设置驱动。</summary>
        internal double GlassOpacity
        {
            get => GlassRoot?.Opacity ?? 1.0;
            set
            {
                if (GlassRoot != null) GlassRoot.Opacity = ClampOpacity(value);
            }
        }

        private static double ClampOpacity(double v) => v < 0.2 ? 0.2 : (v > 1.0 ? 1.0 : v);

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // 必须 NOACTIVATE：否则点击浮动栏会抢走全屏批注窗的焦点，
            // 主窗失活 → 隐藏并重新整屏截图 → 与本窗互相触发形成卡死循环。
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    var ex = GetWindowLong(hwnd, GwlExStyle).ToInt64();
                    SetWindowLong(hwnd, GwlExStyle,
                        new IntPtr(ex | WsExNoActivate | WsExToolWindow));
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"液态玻璃浮动栏设置窗口样式失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 注意：这里不能再订阅 Loaded 来做初始化，本方法已经是 Loaded 阶段。
            SetupBackdrop();
            SetupEffect();
            ApplyGlyphBrush();
            UpdateGlassClip();
            RefreshBackdrop(recapture: true);
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            _isClosing = true;
            StopRefreshTimer();
            _backdropBrush = null;
            _effect = null;
        }

        // —— 玻璃层搭建 ——

        private void SetupBackdrop()
        {
            if (BackdropLayer == null) return;

            _backdropBrush ??= new ImageBrush
            {
                Stretch = Stretch.Fill,
                AlignmentX = AlignmentX.Left,
                AlignmentY = AlignmentY.Top,
                ViewboxUnits = BrushMappingMode.Absolute
            };

            BackdropLayer.Background = _backdropBrush;
            // 折射层复用同一张画刷：着色器对它采样并做边缘位移
            if (RefractionLayer != null) RefractionLayer.Background = _backdropBrush;
        }

        private void SetupEffect()
        {
            if (RefractionLayer == null) return;

            _effect ??= new LiquidGlassEffect();
            if (!LiquidGlassEffect.IsShaderAvailable)
            {
                // 着色器不可用时退回无折射：背景层照常显示裁剪截图，只是不弯曲
                RefractionLayer.Effect = null;
                RefractionLayer.Background = null;
                return;
            }

            // 背景层交给折射层去画，避免同一张图叠两遍导致对比度过高
            if (BackdropLayer != null) BackdropLayer.Background = null;
            RefractionLayer.Effect = _effect;
            UpdateEffectParameters();
        }

        private void UpdateEffectParameters()
        {
            if (_effect == null || RefractionLayer == null) return;

            double w = Math.Max(1.0, RefractionLayer.ActualWidth);
            double h = Math.Max(1.0, RefractionLayer.ActualHeight);

            _effect.TextureSize = new Point(w, h);
            _effect.GlassCenter = new Point(w * 0.5, h * 0.5);
            _effect.GlassSize = new Point(w, h);
            _effect.BlurIntensity = 0.28f;
        }

        /// <summary>图标颜色跟随系统主题：亮色桌面用深字，暗色用白字。</summary>
        private void ApplyGlyphBrush()
        {
            try
            {
                bool light = ThemeHelper.IsSystemThemeLight();
                var brush = new SolidColorBrush(light
                    ? Color.FromRgb(0x1F, 0x1F, 0x22)
                    : Color.FromRgb(0xF2, 0xF2, 0xF2));
                brush.Freeze();
                Resources["GlassGlyphBrush"] = brush;
                SetGlyphForeground(brush);
            }
            catch
            {
                var fallback = new SolidColorBrush(Colors.White);
                fallback.Freeze();
                SetGlyphForeground(fallback);
            }
        }

        private void SetGlyphForeground(Brush brush)
        {
            foreach (var icon in new[]
                     {
                         IconPen, IconHighlighter, IconEraser, IconSelect,
                         IconUndo, IconRedo, IconClear, IconWhiteboard, IconMore
                     })
            {
                if (icon != null) icon.Foreground = brush;
            }
        }

        // —— 背景同步 ——

        /// <summary>
        /// 刷新玻璃背后的桌面内容。<paramref name="recapture"/> 为 true 时重新整屏截图
        /// （需要先把本窗隐藏，否则会把玻璃自己拍进去）；否则只重新裁剪缓存图。
        /// </summary>
        internal void RefreshBackdrop(bool recapture)
        {
            if (_isClosing) return;

            if (recapture) CaptureBehindSelf();
            CropBackdropToWindow();
            UpdateEffectParameters();
        }

        private void CaptureBehindSelf()
        {
            if (_isCapturing) return;
            _isCapturing = true;

            var hwnd = new WindowInteropHelper(this).Handle;
            try
            {
                if (hwnd != IntPtr.Zero)
                {
                    ShowWindow(hwnd, SwHide);
                    Dispatcher.Invoke(DispatcherPriority.Render, new Action(() => { }));
                }

                LiquidGlassCapture.Capture();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"液态玻璃浮动栏截图失败: {ex.Message}", LogHelper.LogType.Warning);
            }
            finally
            {
                if (hwnd != IntPtr.Zero)
                {
                    ShowWindow(hwnd, SwShowNoActivate);
                    Dispatcher.Invoke(DispatcherPriority.Render, new Action(() => { }));
                }
                _isCapturing = false;
            }
        }

        /// <summary>把整屏快照按本窗当前屏幕矩形裁剪到画刷的 Viewbox 上。</summary>
        private void CropBackdropToWindow()
        {
            if (_backdropBrush == null || GlassRoot == null) return;
            if (WindowState == WindowState.Minimized) return;

            var snapshot = LiquidGlassCapture.Snapshot;
            if (snapshot == null) return;
            if (GlassRoot.ActualWidth <= 0 || GlassRoot.ActualHeight <= 0) return;

            Point topLeft, bottomRight;
            try
            {
                topLeft = GlassRoot.PointToScreen(new Point(0, 0));
                bottomRight = GlassRoot.PointToScreen(
                    new Point(GlassRoot.ActualWidth, GlassRoot.ActualHeight));
            }
            catch
            {
                return; // 窗口尚未连上 PresentationSource
            }

            int x = (int)Math.Round(topLeft.X - LiquidGlassCapture.VirtualScreenX);
            int y = (int)Math.Round(topLeft.Y - LiquidGlassCapture.VirtualScreenY);
            int w = Math.Max(1, (int)Math.Round(bottomRight.X - topLeft.X));
            int h = Math.Max(1, (int)Math.Round(bottomRight.Y - topLeft.Y));

            if (x < 0) { w += x; x = 0; }
            if (y < 0) { h += y; y = 0; }
            if (x + w > snapshot.PixelWidth) w = snapshot.PixelWidth - x;
            if (y + h > snapshot.PixelHeight) h = snapshot.PixelHeight - y;
            if (w <= 0 || h <= 0) return;

            if (!ReferenceEquals(_backdropBrush.ImageSource, snapshot))
                _backdropBrush.ImageSource = snapshot;

            _backdropBrush.Viewbox = new Rect(x, y, w, h);
        }

        /// <summary>移动/尺寸变化后合并多次请求，只在空闲时裁剪一次。</summary>
        private void ScheduleCrop()
        {
            if (_isClosing) return;

            _refreshTimer ??= new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _refreshTimer.Tick -= OnRefreshTick;
            _refreshTimer.Tick += OnRefreshTick;
            if (!_refreshTimer.IsEnabled) _refreshTimer.Start();
        }

        private void OnRefreshTick(object sender, EventArgs e)
        {
            _refreshTimer?.Stop();
            CropBackdropToWindow();
            UpdateEffectParameters();
        }

        /// <summary>
        /// 用圆角矩形几何裁掉玻璃层。ClipToBounds 只按矩形裁剪，
        /// 着色器折射出的不透明像素会溢到圆角外，使胶囊看上去是方角。
        /// </summary>
        private void UpdateGlassClip()
        {
            ApplyRoundedClip(GlassRoot, ref _glassRootClip);
            ApplyRoundedClip(GlassLayers, ref _glassLayersClip);
        }

        private static void ApplyRoundedClip(FrameworkElement element, ref RectangleGeometry clip)
        {
            if (element == null) return;

            double w = element.ActualWidth;
            double h = element.ActualHeight;
            if (w <= 0 || h <= 0) return;

            // 半径取高度一半，保证始终是完整胶囊；不超过 XAML 里设定的视觉半径
            double r = Math.Min(GlassCornerRadius, h / 2);
            var rect = new Rect(0, 0, w, h);

            if (clip == null)
            {
                clip = new RectangleGeometry(rect, r, r);
                element.Clip = clip;
                return;
            }

            clip.Rect = rect;
            clip.RadiusX = r;
            clip.RadiusY = r;
        }

        private void StopRefreshTimer()
        {
            if (_refreshTimer == null) return;
            _refreshTimer.Stop();
            _refreshTimer.Tick -= OnRefreshTick;
            _refreshTimer = null;
        }

        protected override void OnLocationChanged(EventArgs e)
        {
            base.OnLocationChanged(e);
            ScheduleCrop();
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo info)
        {
            base.OnRenderSizeChanged(info);
            UpdateGlassClip();
            ScheduleCrop();
        }

        // —— 拖动 ——

        private void GlassRoot_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 只有手柄区域和空白处起拖，按钮上不起拖
            if (e.OriginalSource is FrameworkElement src && IsInteractiveElement(src)) return;

            _dragging = true;
            _dragMoved = false;
            _dragOrigin = PointToScreen(e.GetPosition(this));
            _dragStartLeft = Left;
            _dragStartTop = Top;
            GlassRoot.CaptureMouse();
        }

        private void GlassRoot_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragging) return;

            var now = PointToScreen(e.GetPosition(this));
            double dx = now.X - _dragOrigin.X;
            double dy = now.Y - _dragOrigin.Y;
            if (Math.Abs(dx) > 2 || Math.Abs(dy) > 2) _dragMoved = true;

            Left = _dragStartLeft + dx;
            Top = _dragStartTop + dy;
        }

        private void GlassRoot_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_dragging) return;

            _dragging = false;
            GlassRoot.ReleaseMouseCapture();

            if (!_dragMoved) return;

            ClampIntoWorkingArea();
            // 拖动结束才重新截图：新位置背后的桌面内容与拖动前不同
            RefreshBackdrop(recapture: true);
            _owner?.SaveLiquidGlassBarPosition(Left, Top);
        }

        /// <summary>按钮/圆点上不触发窗口拖动。</summary>
        private static bool IsInteractiveElement(FrameworkElement element)
        {
            for (var cur = element; cur != null; cur = cur.Parent as FrameworkElement)
            {
                if (cur.Name != null &&
                    (cur.Name.StartsWith("Btn", StringComparison.Ordinal) ||
                     cur.Name.StartsWith("Dot", StringComparison.Ordinal)))
                    return true;
            }
            return false;
        }

        /// <summary>把窗口夹回所在屏幕的工作区，避免拖出屏幕外找不回来。</summary>
        internal void ClampIntoWorkingArea()
        {
            try
            {
                var area = System.Windows.Forms.Screen
                    .FromRectangle(new System.Drawing.Rectangle(
                        (int)Left, (int)Top, (int)Math.Max(1, ActualWidth), (int)Math.Max(1, ActualHeight)))
                    .WorkingArea;

                double scale = 1.0;
                var source = PresentationSource.FromVisual(this);
                if (source?.CompositionTarget != null)
                    scale = source.CompositionTarget.TransformToDevice.M11;
                if (scale <= 0) scale = 1.0;

                double left = area.Left / scale;
                double top = area.Top / scale;
                double right = area.Right / scale;
                double bottom = area.Bottom / scale;

                double w = ActualWidth > 0 ? ActualWidth : Width;
                double h = ActualHeight > 0 ? ActualHeight : Height;

                if (Left < left) Left = left;
                if (Top < top) Top = top;
                if (Left + w > right) Left = Math.Max(left, right - w);
                if (Top + h > bottom) Top = Math.Max(top, bottom - h);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"液态玻璃浮动栏位置校正失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        // —— 悬停时提亮，便于操作 ——

        private void GlassRoot_MouseEnter(object sender, MouseEventArgs e)
        {
            if (GlassRoot != null) GlassRoot.Opacity = 1.0;
        }

        private void GlassRoot_MouseLeave(object sender, MouseEventArgs e)
        {
            if (GlassRoot == null || _dragging) return;
            GlassRoot.Opacity = ClampOpacity(MainWindow.Settings.Appearance.LiquidGlassBarOpacity);
        }

        // —— 工具转发：全部走 MainWindow 的既有处理器，不复制业务逻辑 ——

        private void BtnPen_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
            => _owner?.LiquidGlassBarSelectPen();

        private void BtnHighlighter_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
            => _owner?.LiquidGlassBarSelectHighlighter();

        private void BtnEraser_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
            => _owner?.LiquidGlassBarSelectEraser();

        private void BtnSelect_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
            => _owner?.LiquidGlassBarSelectLasso();

        private void DotBlack_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
            => _owner?.ApplyQuickColorByName("Black");

        private void DotRed_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
            => _owner?.ApplyQuickColorByName("Red");

        private void DotBlue_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
            => _owner?.ApplyQuickColorByName("Blue");

        private void DotYellow_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
            => _owner?.ApplyQuickColorByName("Yellow");

        private void BtnUndo_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
            => _owner?.LiquidGlassBarUndo();

        private void BtnRedo_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
            => _owner?.LiquidGlassBarRedo();

        private void BtnClear_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
            => _owner?.LiquidGlassBarClear();

        private void BtnWhiteboard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
            => _owner?.LiquidGlassBarToggleWhiteboard();

        private void BtnMore_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
            => _owner?.LiquidGlassBarOpenTools();

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLong(IntPtr hwnd, int index, IntPtr value);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hwnd, int cmd);
    }
}
