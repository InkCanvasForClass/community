using Ink_Canvas.Helpers;
using Ink_Canvas.Shaders;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using System.Threading.Tasks;

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

        /// <summary>SetWindowDisplayAffinity：窗口照常显示，但不被截屏 API 采集（Win10 2004+）。</summary>
        private const uint WdaExcludeFromCapture = 0x00000011;

        /// <summary>背景模糊半径。WPF BlurEffect 的 GPU 高斯，玻璃的磨砂感。</summary>
        private const double GlassBlurRadius = 14.0;

        private readonly MainWindow _owner;
        private ImageBrush _backdropBrush;
        private LiquidGlassEffect _effect;
        private DispatcherTimer _refreshTimer;
        private RectangleGeometry _glassRootClip;
        private RectangleGeometry _glassLayersClip;
        private bool _isCapturing;
        private bool _isClosing;

        /// <summary>
        /// 本窗是否已被系统排除在截屏之外。为 true 时重抓背景无需隐藏自己，
        /// 也就没有隐藏/显示造成的闪烁。
        /// </summary>
        private bool _excludedFromCapture;

        /// <summary>胶囊圆角半径上限。与 XAML 中各层的 CornerRadius 保持一致。</summary>
        private const double GlassCornerRadius = 20;

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

                    // 让本窗不被截屏 API 采集（Win10 2004+）。这样重抓玻璃背景时
                    // 不必先隐藏自己再显示回来，也就没有那一下闪烁。
                    // 失败（旧系统/远程会话）时回落到隐藏式截图，见 CaptureBehindSelfAsync。
                    _excludedFromCapture = SetWindowDisplayAffinity(hwnd, WdaExcludeFromCapture);
                    LogHelper.WriteLogToFile(
                        $"液态玻璃浮动栏 SetWindowDisplayAffinity 返回={_excludedFromCapture} err={Marshal.GetLastWin32Error()}",
                        LogHelper.LogType.Warning);

                    // 模糊由 BlurEffect 层完成（见 SetupBackdrop），不走 DWM blur-behind——
                    // 分层窗口（AllowsTransparency=True）对 DWM blur-behind 无效。
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
            // 放大镜宿主与本窗同生共死：关栏时释放，避免残留离屏窗口
            LiquidGlassMagnifier.Shutdown();
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

            // 截图画刷作底。折射层直采同一张画刷（清晰截图），着色器在内部做轻量模糊
            // + SDF 折射 + 色散，单元素单 Effect 完成 blur → refraction 管线。
            BackdropLayer.Background = _backdropBrush;
            BackdropLayer.Effect = null;
        }

        private void SetupEffect()
        {
            if (RefractionLayer == null) return;

            _effect ??= new LiquidGlassEffect();
            if (!LiquidGlassEffect.IsShaderAvailable)
            {
                // 着色器不可用时退回：背景层显示清晰截图，无玻璃效果
                RefractionLayer.Effect = null;
                RefractionLayer.Background = null;
                BackdropLayer.Opacity = 1.0;
                return;
            }

            // 折射层直采同一张清晰截图画刷（不是 VisualBrush——引用可视树元素容易采到
            // 空/黑）。着色器在内部做轻量模糊 + SDF 折射 + 色散，单元素单 Effect 完成
            // blur → refraction 管线，无黑屏。
            RefractionLayer.Background = _backdropBrush;
            RefractionLayer.Effect = _effect;
            // 背景层被折射层盖住（alpha=1），隐藏避免叠两遍
            BackdropLayer.Opacity = 0;

            UpdateEffectParameters();
        }

        private void UpdateEffectParameters()
        {
            if (_effect == null || RefractionLayer == null) return;

            double w = Math.Max(1.0, RefractionLayer.ActualWidth);
            double h = Math.Max(1.0, RefractionLayer.ActualHeight);

            _effect.TextureSize = new Point(w, h);
            // 边缘折射参数参考 AndroidLiquidGlass 的 lens()：只在边缘带内折射，中心清晰。
            // 胶囊高 40 → RefractionHeight 取 10（带约占 1/4 高度），RefractionAmount 传负值
            // 向内侧采样（透镜放大）。色散与高光保持克制，避免在小尺寸上显脏。
            _effect.CornerRadius = (float)Math.Min(GlassCornerRadius, h / 2);
            // 折射带做窄、位移做强：折射集中在贴边一圈，边框本身看起来就是一道透镜环，
            // 中心区域完全不动。折射只重定向采样坐标，不改 alpha、不加白，所以透明度不变。
            _effect.RefractionHeight = 8f;
            _effect.RefractionAmount = -14f;
            _effect.DepthEffect = 0f;
            // 边缘色散：固定像素偏移色差（着色器内 edgeMask 贴边才非零），
            // ChromaticAberration 直接控制偏移量。0.4 × BlurRadius(4) ≈ 1.6px，
            // 边缘一圈淡彩、不刺眼。
            _effect.ChromaticAberration = 0.4f;
            // 高光上下对称。之前 -PI/2「光从上方来」+ 着色器里的 facing 迎光加权，
            // 让顶部明显比底部白，深色桌面上很扎眼。这里配合着色器去掉方向偏置。
            _effect.HighlightAngle = (float)(Math.PI / 2.0);
            _effect.HighlightFalloff = 1.6f;
            _effect.HighlightStrength = 0.11f;
            _effect.HighlightWidth = 3.5f;
            // 内部模糊：轻量磨砂（半径 6），中间不至于太糊。着色器内 SampleBlur 连续高斯。
            _effect.BlurRadius = 6f;
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
        /// 抓屏在后台线程执行，方法立即返回，完成后自动裁剪并应用新帧，调用方无需等待。
        /// </summary>
        internal void RefreshBackdrop(bool recapture)
        {
            if (_isClosing) return;

            if (recapture)
            {
                _ = CaptureBehindSelfAsync();
                return;
            }

            CropBackdropToWindow();
            UpdateEffectParameters();
        }

        /// <summary>
        /// 抓屏一帧。优先走 Magnification API：排除本窗 HWND 后直接读"栏正下方"，
        /// 全程不动窗口状态，因此不闪。失败（缺 DLL / 合成全黑 / 驱动不支持）时
        /// 回退到 Hide → BitBlt → Show 老路径。
        /// </summary>
        private async Task CaptureBehindSelfAsync()
        {
            if (_isCapturing) return;
            _isCapturing = true;

            try
            {
                // —— 路径 1：Magnifier（首选，零闪烁）——
                if (TryCaptureViaMagnifier())
                {
                    UpdateEffectParameters();
                    return;
                }

                LogHelper.WriteLogToFile(
                    $"液态玻璃放大镜失败，回退抓屏。_excludedFromCapture={_excludedFromCapture}",
                    LogHelper.LogType.Warning);

                // —— 路径 2：WDA 已排除自身时直接整屏 BitBlt（分层窗口上通常走不到）——
                // —— 路径 3：Hide/Show 回退（会闪，仅作兜底）——
                await CaptureViaHideShowAsync();
                CropBackdropToWindow();
                UpdateEffectParameters();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"液态玻璃浮动栏截图失败: {ex.Message}", LogHelper.LogType.Warning);
            }
            finally
            {
                _isCapturing = false;
            }
        }

        /// <summary>
        /// 用放大镜抓取 GlassRoot 当前屏幕矩形下、排除本窗后的画面。
        /// 成功时直接把小图挂到画刷上（Viewbox 置满），返回 true；失败返回 false。
        /// 必须在 UI 线程调用（Magnification API 非线程安全）。
        /// </summary>
        private bool TryCaptureViaMagnifier()
        {
            if (_backdropBrush == null || GlassRoot == null) return false;
            if (WindowState == WindowState.Minimized) return false;
            if (GlassRoot.ActualWidth <= 0 || GlassRoot.ActualHeight <= 0) return false;

            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return false;

            Point topLeft, bottomRight;
            try
            {
                topLeft = GlassRoot.PointToScreen(new Point(0, 0));
                bottomRight = GlassRoot.PointToScreen(
                    new Point(GlassRoot.ActualWidth, GlassRoot.ActualHeight));
            }
            catch
            {
                return false; // 窗口尚未连上 PresentationSource
            }

            int left = (int)Math.Round(topLeft.X);
            int top = (int)Math.Round(topLeft.Y);
            int width = Math.Max(1, (int)Math.Round(bottomRight.X - topLeft.X));
            int height = Math.Max(1, (int)Math.Round(bottomRight.Y - topLeft.Y));

            // 诊断 DPI/坐标：先无条件打一条"已到达"，再打详情
            LogHelper.WriteLogToFile(
                $"液态玻璃放大镜已到达 CaptureRegion: 源=({left},{top},{width}x{height})",
                LogHelper.LogType.Warning);
            try
            {
                var src = PresentationSource.FromVisual(this);
                double scaleX = src?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
                double scaleY = src?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;
                int phCx = GetSystemMetrics(0), phCy = GetSystemMetrics(1);
                LogHelper.WriteLogToFile(
                    $"液态玻璃放大镜坐标详情: Actual=({GlassRoot.ActualWidth}x{GlassRoot.ActualHeight}) " +
                    $"PointToScreen=({topLeft.X},{topLeft.Y})-({bottomRight.X},{bottomRight.Y}) " +
                    $"DPI缩放=({scaleX},{scaleY}) 主屏物理={phCx}x{phCy}",
                    LogHelper.LogType.Warning);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"液态玻璃放大镜坐标诊断异常: {ex.Message}", LogHelper.LogType.Warning);
            }

            var bitmap = LiquidGlassMagnifier.CaptureRegion(hwnd, left, top, width, height);
            if (bitmap == null) return false;

            // 小图本身就是栏区域：Viewbox 置满整张图，不再依赖虚拟屏原点裁剪。
            // 不 Publish 到 LiquidGlassCapture.Snapshot——那是整屏帧的槽位；
            // 拖动中 ScheduleCrop 若拿到这张小图会按虚拟屏裁出空矩形。
            // 拖动期间就冻住这一帧，松手后 recapture 再换新位置的画面。
            _backdropBrush.ImageSource = bitmap;
            _backdropBrush.Viewbox = new Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight);
            return true;
        }

        /// <summary>
        /// Hide/Show 回退：UI 线程隐藏自己 → BitBlt 放到后台线程 → 恢复。
        /// 会闪，仅在放大镜不可用时使用。
        /// </summary>
        private async Task CaptureViaHideShowAsync()
        {
            var hwnd = IntPtr.Zero;
            if (!_excludedFromCapture)
            {
                hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    ShowWindow(hwnd, SwHide);
                    Dispatcher.Invoke(DispatcherPriority.Render, new Action(() => { }));
                }
            }

            try
            {
                // BitBlt 抓整个虚拟桌面耗时数毫秒到数十毫秒，放后台线程避免阻塞 UI。
                await Task.Run(() => LiquidGlassCapture.Capture());
            }
            finally
            {
                if (!_excludedFromCapture && hwnd != IntPtr.Zero)
                {
                    ShowWindow(hwnd, SwShowNoActivate);
                    Dispatcher.Invoke(DispatcherPriority.Render, new Action(() => { }));
                }
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

            var image = snapshot.Bitmap;

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

            int x = (int)Math.Round(topLeft.X - snapshot.VirtualScreenX);
            int y = (int)Math.Round(topLeft.Y - snapshot.VirtualScreenY);
            int w = Math.Max(1, (int)Math.Round(bottomRight.X - topLeft.X));
            int h = Math.Max(1, (int)Math.Round(bottomRight.Y - topLeft.Y));

            if (x < 0) { w += x; x = 0; }
            if (y < 0) { h += y; y = 0; }
            if (x + w > image.PixelWidth) w = image.PixelWidth - x;
            if (y + h > image.PixelHeight) h = image.PixelHeight - y;
            if (w <= 0 || h <= 0) return;

            if (!ReferenceEquals(_backdropBrush.ImageSource, image))
                _backdropBrush.ImageSource = image;

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

            // 注意：这里不能用 SetWindowRgn 裁圆角——它会连外层 Grid 的 DropShadowEffect
            // 一起裁掉，阴影消失。AllowsTransparency=True 的分层窗口原生支持圆角
            // （GlassRoot.CornerRadius），无需窗口级 region。
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

        // —— 选中态同步 ——

        /// <summary>
        /// 按主窗当前状态点亮对应按钮。选中用 Tag="on" 驱动样式里的填充胶囊，
        /// 参考 GitHub 移动端底栏：玻璃背景下只换图标颜色区分度不够。
        /// </summary>
        /// <param name="toolMode">主窗的 _currentToolMode。</param>
        /// <param name="penType">0 = 普通笔，1 = 荧光笔。</param>
        /// <param name="color">当前画笔颜色，用于点亮颜色圆点。</param>
        internal void SyncActiveState(string toolMode, int penType, Color? color)
        {
            bool isPen = string.Equals(toolMode, "pen", StringComparison.Ordinal);

            SetActive(BtnPen, isPen && penType != 1);
            SetActive(BtnHighlighter, isPen && penType == 1);
            SetActive(BtnEraser, string.Equals(toolMode, "eraser", StringComparison.Ordinal)
                                 || string.Equals(toolMode, "eraserByStrokes", StringComparison.Ordinal));
            SetActive(BtnSelect, string.Equals(toolMode, "select", StringComparison.Ordinal));

            // 颜色圆点：只在画笔类工具下点亮，橡皮/选择时全部熄灭
            bool colorMeaningful = isPen && color.HasValue;
            SetActive(DotBlack, colorMeaningful && IsSameColor(color.Value, Colors.Black));
            SetActive(DotRed, colorMeaningful && IsSameColor(color.Value, Colors.Red));
            SetActive(DotBlue, colorMeaningful && IsSameColor(color.Value, Color.FromRgb(37, 99, 235)));
            SetActive(DotYellow, colorMeaningful && IsSameColor(color.Value, Colors.Yellow));
        }

        private static void SetActive(Border button, bool active)
        {
            if (button == null) return;
            // 样式里用 Tag 触发；置 null 而非 "off"，避免多余的触发器分支
            button.Tag = active ? "on" : null;
        }

        /// <summary>比较 RGB，忽略 alpha——荧光笔会改 alpha 但仍是同一支颜色。</summary>
        private static bool IsSameColor(Color a, Color b)
            => a.R == b.R && a.G == b.G && a.B == b.B;

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

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowDisplayAffinity(IntPtr hwnd, uint affinity);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);
    }
}
