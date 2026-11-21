using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 渲染帧率 HUD 悬浮窗：显示 WPF 真实出帧率、最差帧耗时与刷新率基准。
    /// </summary>
    /// <remarks>
    /// 数据源是 <see cref="RenderFrameRateMonitor"/>（进程实际出帧），
    /// 不是墨迹提交帧率——后者空闲时归零，看不出界面卡顿。
    ///
    /// 读数分三行，各自回答一个不同的问题：
    /// - 帧率：最近 1 秒平均，稳定但不敏感；
    /// - 最差帧：最近 1 秒最慢的一帧，掉帧尖峰都在这里；
    /// - 刷新率：显示器物理上限，用来判断"当前是不是已经跑满"。
    /// 只看平均帧率会漏掉"大部分帧 4ms、偶发一帧 200ms"这种典型卡顿。
    /// </remarks>
    internal sealed class RenderFpsOverlay : PerformanceTransparentWin
    {
        private const int RefreshIntervalMs = 100;

        private readonly TextBlock _fpsText;
        private readonly TextBlock _worstText;
        private readonly TextBlock _envText;
        private readonly DispatcherTimer _refreshTimer;
        private bool _disposed;
        private DispatcherTimer _zOrderGuard;
        private IntPtr _handle;

        // 检查间隔：白板进出、窗口激活都会瞬间改变 Z 序，
        // 500ms 足以让用户感觉不到，又不至于频繁调 Win32。
        private static readonly TimeSpan ZOrderGuardInterval = TimeSpan.FromMilliseconds(500);

        public RenderFpsOverlay()
        {
            MinWidth = 188;
            Width = 260;
            Height = 84;
            ShowInTaskbar = false;
            Topmost = true;
            Focusable = false;
            ShowActivated = false;
            ResizeMode = ResizeMode.NoResize;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            IsHitTestVisible = false;

            SourceInitialized += OnSourceInitialized;

            var panel = new StackPanel { Orientation = Orientation.Vertical };
            var root = new Border
            {
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromArgb(190, 18, 18, 18)),
                Padding = new Thickness(10, 6, 10, 6),
                Child = panel
            };

            _fpsText = new TextBlock
            {
                Foreground = Brushes.LimeGreen,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Text = "FPS  --",
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap
            };
            _worstText = new TextBlock
            {
                Foreground = Brushes.White,
                FontSize = 11,
                Margin = new Thickness(0, 2, 0, 0),
                Text = "最差  -- ms",
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap
            };
            _envText = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromRgb(165, 165, 165)),
                FontSize = 10,
                Margin = new Thickness(0, 2, 0, 0),
                Text = "--",
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap
            };

            panel.Children.Add(_fpsText);
            panel.Children.Add(_worstText);
            panel.Children.Add(_envText);
            Content = root;

            _refreshTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(RefreshIntervalMs)
            };
            _refreshTimer.Tick += RefreshTimer_Tick;

            RenderFrameRateMonitor.Start();
            _refreshTimer.Start();

            Loaded += (_, _) =>
            {
                PositionBelowSettingsButton();
                EnsureTopmost();
                StartZOrderGuard();
            };
            SizeChanged += (_, _) => PositionBelowSettingsButton();

            Closed += (_, _) =>
            {
                if (_disposed) return;
                _disposed = true;
                _refreshTimer.Stop();
                _refreshTimer.Tick -= RefreshTimer_Tick;
                _zOrderGuard?.Stop();
                _zOrderGuard = null;
                if (Application.Current?.MainWindow != null)
                {
                    Application.Current.MainWindow.Activated -= OnMainWindowActivated;
                }
                RenderFrameRateMonitor.Stop();
            };
        }

        private void RefreshTimer_Tick(object sender, EventArgs e)
        {
            if (_disposed) return;

            var snap = RenderFrameRateMonitor.TakeSnapshot();
            var reference = RenderPacingController.DisplayRefreshRate;

            // 空闲优先判定：WPF 静止界面不渲染，此时"帧率低"是正常现象，
            // 若直接显示数字，用户会误以为掉帧。
            if (snap.IsIdle)
            {
                _fpsText.Text = "FPS  空闲";
                _fpsText.Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150));

                _worstText.Text = string.Format(CultureInfo.InvariantCulture,
                    "已 {0:F0} ms 未出帧（静止界面）", snap.SinceLastFrameMs);
                _worstText.Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150));

                var idlePieces = RenderPacingController.DescribeEnvironment();
                if (RenderPacingController.IsUnlimitedFrameRate) idlePieces += " / 无限帧率";
                _envText.Text = idlePieces;
                return;
            }

            if (snap.Fps > 0)
            {
                _fpsText.Text = string.Format(CultureInfo.InvariantCulture, "FPS  {0:F1}", snap.Fps);

                // 以显示器刷新率为基准判定：>=92% 满帧，>=75% 轻微掉帧，其余明显掉帧
                double ratio = reference > 0 ? snap.Fps / reference : 0;
                if (reference <= 0)
                {
                    _fpsText.Foreground = snap.Fps >= 55 ? Brushes.LimeGreen
                        : snap.Fps >= 40 ? new SolidColorBrush(Color.FromRgb(255, 200, 80))
                        : new SolidColorBrush(Color.FromRgb(255, 90, 90));
                }
                else
                {
                    _fpsText.Foreground = ratio >= 0.92 ? Brushes.LimeGreen
                        : ratio >= 0.75 ? new SolidColorBrush(Color.FromRgb(255, 200, 80))
                        : new SolidColorBrush(Color.FromRgb(255, 90, 90));
                }
            }
            else
            {
                _fpsText.Text = "FPS  --";
                _fpsText.Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170));
            }

            if (snap.FrameCount > 0)
            {
                _worstText.Text = string.Format(CultureInfo.InvariantCulture, "最差  {0:F1} ms / 平均 {1:F1} ms",
                    snap.WorstFrameMs, snap.AverageFrameMs);

                // 超过一帧预算即说明这一秒内确实掉过帧，标红便于一眼定位。
                // 阈值取 1.0 倍预算：60Hz 下 >16.7ms 就是掉帧，不需要系数放大。
                var budgetMs = RenderPacingController.FrameBudgetMs;
                _worstText.Foreground = budgetMs > 0 && snap.WorstFrameMs > budgetMs
                    ? new SolidColorBrush(Color.FromRgb(255, 120, 120))
                    : Brushes.White;
            }
            else
            {
                _worstText.Text = "最差  -- ms";
                _worstText.Foreground = Brushes.White;
            }

            // 环境行：刷新率/渲染模式 + 一帧的时间预算。
            // 有预算才有判断依据——"最差 41ms"在 60Hz（预算 16.7ms）下是掉两帧，
            // 在 30Hz 下却完全正常，单看毫秒数会误判。
            var pieces = RenderPacingController.DescribeEnvironment();
            var budget = RenderPacingController.FrameBudgetMs;
            if (budget > 0)
            {
                pieces += string.Format(CultureInfo.InvariantCulture, " / 预算 {0:F1} ms", budget);
            }
            if (RenderPacingController.IsUnlimitedFrameRate) pieces += " / 无限帧率";
            _envText.Text = pieces;
        }

        /// <summary>
        /// 把 HUD 提升到 TOPMOST 顶部。
        /// </summary>
        /// <remarks>
        /// 不走 <see cref="WindowTopmostManager"/>：那个管理器服务于"主窗口是否置顶"这一套语义，
        /// 当主窗口未开「始终置顶」时，它的 ApplyZOrderCore 会走 else 分支主动把子窗口
        /// 取消置顶——白板下主窗口通常不置顶，注册进去反而会让 HUD 被压下去。
        ///
        /// 而 <c>Topmost = true</c> 本身只是标志位：多个 TOPMOST 窗口之间，
        /// 层级由"谁最后被激活/提升"决定。进白板会激活主窗口，把 HUD 压到它下面。
        /// 所以这里自己持有 HWND 并定期重新提到 TOPMOST 顶（SWP_NOACTIVATE，不抢焦点）。
        /// </remarks>
        private void EnsureTopmost()
        {
            try
            {
                if (_handle == IntPtr.Zero)
                    _handle = new WindowInteropHelper(this).Handle;

                if (_handle == IntPtr.Zero) return;

                NativeWindowHelper.SetTopmost(_handle);
            }
            catch
            {
                // 置顶失败不应影响 HUD 显示
            }
        }

        private void StartZOrderGuard()
        {
            _zOrderGuard?.Stop();
            _zOrderGuard = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = ZOrderGuardInterval
            };
            _zOrderGuard.Tick += (_, _) =>
            {
                if (_disposed) return;
                EnsureTopmost();
            };
            _zOrderGuard.Start();

            // 只靠定时器轮询有一个可见缺陷：主窗口进白板时会激活并重新提升到 TOPMOST 组首位，
            // HUD 要等下一次 tick 才回来，中间最长 500ms 被遮住。
            // 因此再订阅主窗口的 Activated 事件做即时补救。
            if (Application.Current?.MainWindow != null)
            {
                Application.Current.MainWindow.Activated += OnMainWindowActivated;
            }
        }

        /// <summary>
        /// 主窗口被激活（进白板、退出放映、切回应用等）时，TOPMOST 组内它会被提升到顶部，
        /// HUD 随之被压住。这里立刻把 HUD 重新提到顶部，避免出现最长一个轮询周期的遮挡。
        /// </summary>
        private void OnMainWindowActivated(object sender, EventArgs e)
        {
            if (_disposed) return;

            // 用 Loaded 优先级排队：等主窗口这次激活引发的 Z 序调整落定后再提升，
            // 否则刚提上去就被对方随后的激活流程覆盖。
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!_disposed) EnsureTopmost();
            }), DispatcherPriority.Loaded);
        }

        private void PositionBelowSettingsButton()
        {
            try
            {
                var workArea = SystemParameters.WorkArea;
                Left = workArea.Right - ActualWidth - 16;
                Top = workArea.Top + 16;
            }
            catch
            {
                Left = 100;
                Top = 100;
            }
        }

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;

                // 禁用激活 + 任务栏隐藏：HUD 不能抢主窗口焦点，也不能出现在 Alt+Tab 里
                var exStyle = PInvoke.GetWindowLong(new HWND(hwnd), WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
                exStyle |= (int)(WINDOW_EX_STYLE.WS_EX_NOACTIVATE | WINDOW_EX_STYLE.WS_EX_TOOLWINDOW | WINDOW_EX_STYLE.WS_EX_LAYERED);
                PInvoke.SetWindowLong(new HWND(hwnd), WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, exStyle);

                if (HwndSource.FromHwnd(hwnd) is HwndSource source)
                    source.AddHook(WndProcNoActivate);
            }
            catch
            {
                // 样式设置失败不影响 HUD 显示
            }
        }

        private IntPtr WndProcNoActivate(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_MOUSEACTIVATE = 0x0021;
            const int MA_NOACTIVATE = 3;
            const int WM_NCHITTEST = 0x0084;
            const int HTTRANSPARENT = -1;

            if (msg == WM_MOUSEACTIVATE)
            {
                handled = true;
                return new IntPtr(MA_NOACTIVATE);
            }

            if (msg == WM_NCHITTEST)
            {
                handled = true;
                return new IntPtr(HTTRANSPARENT);
            }

            return IntPtr.Zero;
        }
    }
}
