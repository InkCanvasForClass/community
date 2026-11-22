using Ink_Canvas.Helpers;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Threading.Tasks;
using System.Windows;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class DebugPage : Page
    {
        private bool _isLoaded;

        public DebugPage()
        {
            InitializeComponent();
            Loaded += (s, e) =>
            {
                ToggleSwitchDebugConsole.IsOn = SettingsManager.Settings.Advanced.IsDebugConsoleEnabled;
                ToggleSwitchPPTComDebugProbe.IsOn = SettingsManager.Settings.Advanced.IsPPTComDebugProbeEnabled;
                ToggleSwitchPPTPageFlipPreview.IsOn = SettingsManager.Settings.Advanced.IsPPTPageFlipPreviewVisible;
                ToggleSwitchRealtimeInkDebugLog.IsOn = SettingsManager.Settings.Advanced.IsRealtimeInkDebugLogEnabled;
                ToggleSwitchRenderFpsOverlay.IsOn = SettingsManager.Settings.Advanced.IsRenderFpsOverlayEnabled;
                ToggleSwitchUnlimitedFrameRate.IsOn = SettingsManager.Settings.Advanced.IsUnlimitedFrameRateEnabled;
                ToggleSwitchPopupAnimationLog.IsOn = SettingsManager.Settings.Advanced.IsPopupAnimationLogEnabled;

                // 恢复全局动画播放速度：滑杆位置 = log10(倍率)
                double multiplier = SettingsManager.Settings.Advanced.AnimationSpeedMultiplier;
                if (multiplier <= 0) multiplier = 1.0;
                multiplier = Math.Max(AnimationSpeed.MinMultiplier,
                    Math.Min(AnimationSpeed.MaxMultiplier, multiplier));
                if (SliderAnimationSpeed != null)
                {
                    SliderAnimationSpeed.Value = Math.Log10(multiplier);
                }
                UpdateSpeedLabel(multiplier);
                AnimationSpeed.Set(multiplier);
                StartAnimationSpeedSaveTimerIfNeeded();

                _isLoaded = true;
            };
            Unloaded += (s, e) => _isLoaded = false;
        }

        private void ToggleSwitchDebugConsole_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            bool isOn = ToggleSwitchDebugConsole.IsOn;
            SettingsManager.Settings.Advanced.IsDebugConsoleEnabled = isOn;
            SettingsManager.SaveSettingsToFile();

            if (isOn) DebugConsoleManager.Show();
            else DebugConsoleManager.Hide();
        }

        private void ToggleSwitchPPTComDebugProbe_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Advanced.IsPPTComDebugProbeEnabled = ToggleSwitchPPTComDebugProbe.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchPPTPageFlipPreview_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Advanced.IsPPTPageFlipPreviewVisible = ToggleSwitchPPTPageFlipPreview.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchRealtimeInkDebugLog_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            bool isOn = ToggleSwitchRealtimeInkDebugLog.IsOn;
            SettingsManager.Settings.Advanced.IsRealtimeInkDebugLogEnabled = isOn;
            if (MainWindow.Settings?.Advanced != null)
                MainWindow.Settings.Advanced.IsRealtimeInkDebugLogEnabled = isOn;
            SettingsManager.SaveSettingsToFile();
            RealtimeInkPerformanceMonitor.SetDebugLoggingEnabled(isOn);

            LogHelper.WriteLogToFile(
                isOn
                    ? "[Debug] 实时笔迹详细调试日志已开启（落笔后查看 Configs/RealtimeInkDebugLive.json / PerformanceHistory.json）"
                    : "[Debug] 实时笔迹详细调试日志已关闭并尝试保存历史",
                LogHelper.LogType.Info);
        }

        private void ToggleSwitchPopupAnimationLog_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            bool isOn = ToggleSwitchPopupAnimationLog.IsOn;
            SettingsManager.Settings.Advanced.IsPopupAnimationLogEnabled = isOn;
            if (MainWindow.Settings?.Advanced != null)
                MainWindow.Settings.Advanced.IsPopupAnimationLogEnabled = isOn;
            SettingsManager.SaveSettingsToFile();
            AnimationLog.SetEnabled(isOn);
        }

        private void SliderAnimationSpeed_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // 滑杆位置是 log10(倍率)：[-1, 1] -> [0.1x, 10x]，中点 0 = 1x
            double multiplier = Math.Pow(10, e.NewValue);

            if (TextAnimationSpeed != null)
            {
                // 行内只显示数值，宽度固定不动；
                // 速度含义放 ToolTip —— 若拼进 Text，文本变长会把滑杆往左挤，
                // 拖动中滑块会"自己跳"。
                TextAnimationSpeed.Text = multiplier.ToString("0.##") + "x";
                string feel = multiplier < 0.99 ? "慢放（动画变慢便于观察，非卡顿）"
                    : multiplier > 1.01 ? "加速（动画变快）"
                    : "原始速度";
                TextAnimationSpeed.ToolTip = $"{multiplier:0.##}x {feel}";
            }

            if (!_isLoaded) return;

            double applied = AnimationSpeed.Set(multiplier);
            SettingsManager.Settings.Advanced.AnimationSpeedMultiplier = applied;
            if (MainWindow.Settings?.Advanced != null)
                MainWindow.Settings.Advanced.AnimationSpeedMultiplier = applied;

            // 拖动滑块会连续触发，用防抖延后写盘：停手 400ms 后保存一次
            _animationSpeedSaveTimer?.Stop();
            _animationSpeedSaveTimer?.Start();
        }

        private void ButtonResetAnimationSpeed_Click(object sender, RoutedEventArgs e)
        {
            if (SliderAnimationSpeed != null)
            {
                SliderAnimationSpeed.Value = 0;   // log10(1) = 0，触发 ValueChanged 完成设置
            }
        }

        private void UpdateSpeedLabel(double multiplier)
        {
            if (TextAnimationSpeed == null) return;
            TextAnimationSpeed.Text = multiplier.ToString("0.##") + "x";
            string feel = multiplier < 0.99 ? "慢放（动画变慢便于观察，非卡顿）"
                : multiplier > 1.01 ? "加速（动画变快）"
                : "原始速度";
            TextAnimationSpeed.ToolTip = $"{multiplier:0.##}x {feel}";
        }

        /// <summary>滑块防抖保存计时器：避免拖动过程中反复写配置文件。</summary>
        private System.Windows.Threading.DispatcherTimer _animationSpeedSaveTimer;

        private void StartAnimationSpeedSaveTimerIfNeeded()
        {
            if (_animationSpeedSaveTimer != null) return;

            _animationSpeedSaveTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(400)
            };
            _animationSpeedSaveTimer.Tick += (_, _) =>
            {
                _animationSpeedSaveTimer.Stop();
                SettingsManager.SaveSettingsToFile();
            };
        }

        private void ToggleSwitchRenderFpsOverlay_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            bool isOn = ToggleSwitchRenderFpsOverlay.IsOn;
            SettingsManager.Settings.Advanced.IsRenderFpsOverlayEnabled = isOn;
            SettingsManager.SaveSettingsToFile();

            // 设置页可能与主窗口的 Settings 实例不同步，两个都写一遍
            var mw = Application.Current?.MainWindow as MainWindow;
            if (mw != null)
            {
                MainWindow.Settings.Advanced.IsRenderFpsOverlayEnabled = isOn;
                if (isOn) mw.ShowRenderFpsOverlay();
                else mw.HideRenderFpsOverlay();
            }
            else if (isOn)
            {
                LogHelper.WriteLogToFile("[Debug] 主窗口尚未就绪，帧率显示将在下次启动时生效", LogHelper.LogType.Warning);
            }
        }

        private void ToggleSwitchUnlimitedFrameRate_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            bool isOn = ToggleSwitchUnlimitedFrameRate.IsOn;
            SettingsManager.Settings.Advanced.IsUnlimitedFrameRateEnabled = isOn;
            if (MainWindow.Settings?.Advanced != null)
                MainWindow.Settings.Advanced.IsUnlimitedFrameRateEnabled = isOn;
            SettingsManager.SaveSettingsToFile();

            RenderPacingController.ProbeDisplayRefreshRate();
            RenderPacingController.ApplyUnlimitedFrameRate(isOn);
        }

        private void BtnTestCrash_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBoxHelper.Show(this,
                "确定要触发一次模拟崩溃吗？\n\n应用将立即退出并尝试自动重启。",
                "崩溃测试确认",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            LogHelper.WriteLogToFile("[Debug] 用户手动触发模拟崩溃", LogHelper.LogType.Warning);
            throw new System.InvalidOperationException("[Debug] 用户手动触发的模拟崩溃异常");
        }

        private void BtnResetStartupCount_Click(object sender, RoutedEventArgs e)
        {
            StartupCount.Reset();
            LogHelper.WriteLogToFile("[Debug] 熔断计数器已手动重置", LogHelper.LogType.Warning);
            MessageBoxHelper.Show(this, "熔断计数器已重置为 0。", "重置完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Issue #546 排查辅助:输出详细内存占用清单到 Logs 目录并写一行摘要到 LogHelper。
        private void BtnDumpMemory_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                BtnDumpMemory.IsEnabled = false;
                var path = MemoryBreakdownHelper.DumpToFile();
                if (!string.IsNullOrEmpty(path))
                {
                    var sizeKb = new System.IO.FileInfo(path).Length / 1024;
                    LogHelper.WriteLogToFile(
                        $"[MemoryBreakdown] 用户触发的内存清单已保存: {path} ({sizeKb} KB)",
                        LogHelper.LogType.Info);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[MemoryBreakdown] 用户触发输出异常: {ex.Message}", LogHelper.LogType.Error);
            }
            finally
            {
                BtnDumpMemory.IsEnabled = true;
            }
        }

        // Issue #546 排查辅助:强制 GC 一次,记录前后 Working Set。不可滥用,仅用于诊断。
        private async void BtnForceGC_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                BtnForceGC.IsEnabled = false;
                var summary = await Task.Run(MemoryBreakdownHelper.ForceFullGc);
                LogHelper.WriteLogToFile($"[MemoryBreakdown] 强制 GC 触发 - {summary}", LogHelper.LogType.Info);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[MemoryBreakdown] 强制 GC 异常: {ex.Message}", LogHelper.LogType.Error);
            }
            finally
            {
                BtnForceGC.IsEnabled = true;
            }
        }
    }
}
