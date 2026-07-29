using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Ink_Canvas.Properties;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 实时墨迹 FPS / 端到端延迟悬浮窗。
    /// 数据源(Steady-Ink 风格):
    /// - FPS:<see cref="InkPerformanceMonitor"/> 中活跃呈现间隔滑窗,空闲&gt;1s 自动清空。
    ///   旧墨迹由 FrameScheduler.OnRendering record_frame,新墨迹由 WetInkWindowHost.Apply record_frame。
    /// - Latency:同样由 record_frame 上报 dirty_started → presented 差值。
    /// HUD 不订阅任何事件,周期调 Snapshot() 读快照。
    /// </summary>
    internal sealed class RealtimeInkFpsOverlay : PerformanceTransparentWin
    {
        private const int RefreshIntervalMs = 100;

        private readonly TextBlock _fpsText;
        private readonly TextBlock _latencyText;
        private readonly TextBlock _footerText;
        private readonly DispatcherTimer _refreshTimer;
        private bool _disposed;

        public RealtimeInkFpsOverlay()
        {
            Width = 168;
            Height = 76;
            ShowInTaskbar = false;
            Topmost = true;
            Focusable = false;
            ShowActivated = false;
            ResizeMode = ResizeMode.NoResize;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            IsHitTestVisible = false;

            var root = new Border
            {
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromArgb(180, 20, 20, 20)),
                Padding = new Thickness(10, 6, 10, 6),
                Child = new StackPanel
                {
                    Orientation = Orientation.Vertical
                }
            };

            _fpsText = new TextBlock
            {
                Foreground = Brushes.LimeGreen,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Text = "FPS  --"
            };
            _latencyText = new TextBlock
            {
                Foreground = Brushes.White,
                FontSize = 12,
                Margin = new Thickness(0, 2, 0, 0),
                Text = "延迟  -- ms"
            };
            _footerText = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170)),
                FontSize = 10,
                Margin = new Thickness(0, 2, 0, 0),
                Text = "实时墨迹 (空闲)"
            };
            ((StackPanel)root.Child).Children.Add(_fpsText);
            ((StackPanel)root.Child).Children.Add(_latencyText);
            ((StackPanel)root.Child).Children.Add(_footerText);

            Content = root;

            _refreshTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(RefreshIntervalMs)
            };
            _refreshTimer.Tick += RefreshTimer_Tick;

            InkPerformanceMonitor.SetEnabled(true);
            _refreshTimer.Start();
            Loaded += (_, _) => PositionInTopRight();

            Closed += (_, _) =>
            {
                if (_disposed) return;
                _disposed = true;
                _refreshTimer.Stop();
                InkPerformanceMonitor.SetEnabled(false);
            };
        }

        private void RefreshTimer_Tick(object sender, EventArgs e)
        {
            if (_disposed) return;

            var snap = InkPerformanceMonitor.Snapshot();

            _fpsText.Text = snap.Fps > 0
                ? string.Format(CultureInfo.InvariantCulture, "FPS  {0:F1}", snap.Fps)
                : "FPS  --";
            _latencyText.Text = snap.InputSampleCount > 0
                ? string.Format(CultureInfo.InvariantCulture,
                    "延迟  {0:F1} ms / max {1:F1}", snap.AverageInputLatencyMs, snap.MaxInputLatencyMs)
                : "延迟  -- ms";

            var idleText = AdvancedStrings.RealtimeInkFpsOverlay_IdleText;
            if (string.IsNullOrEmpty(idleText)) idleText = "Realtime ink (idle)";
            var activeFmt = AdvancedStrings.RealtimeInkFpsOverlay_ActiveText;
            if (string.IsNullOrEmpty(activeFmt)) activeFmt = "Realtime ink (n={0})";

            _footerText.Text = snap.FrameCount > 0
                ? string.Format(CultureInfo.InvariantCulture, activeFmt, snap.FrameCount)
                : idleText;

            // FPS 颜色提示:>=55 绿,40-55 黄,<40 红;空闲保留灰色。
            if (snap.Fps > 0)
            {
                _fpsText.Foreground = snap.Fps >= 55
                    ? Brushes.LimeGreen
                    : (snap.Fps >= 40
                        ? new SolidColorBrush(Color.FromRgb(255, 200, 80))
                        : new SolidColorBrush(Color.FromRgb(255, 90, 90)));
            }
            else
            {
                _fpsText.Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170));
            }
        }

        private void PositionInTopRight()
        {
            try
            {
                var workArea = SystemParameters.WorkArea;
                Left = workArea.Right - Width - 16;
                Top = workArea.Top + 16;
            }
            catch
            {
                Left = 100;
                Top = 100;
            }
        }
    }
}