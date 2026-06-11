using Ink_Canvas.Helpers;
using Ink_Canvas.Properties;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class PerformancePage : Page
    {
        private bool _isLoaded;
        private DispatcherTimer _uiUpdateTimer;

        public PerformancePage()
        {
            InitializeComponent();
            Loaded += Page_Loaded;
            Unloaded += Page_Unloaded;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = true;

            // Load toggle state
            ToggleSwitchEnableMonitoring.IsOn = SettingsManager.Settings.Performance.IsMonitoringEnabled;

            // Update current session UI
            UpdateCurrentSessionUI();

            // Load history
            RefreshHistoryDisplay();

            // Load device score
            RefreshDeviceScoreDisplay();

            // Load ink smoothing stats
            RefreshInkSmoothingStats();

            // Set up UI refresh timer for live monitoring data
            _uiUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _uiUpdateTimer.Tick += (s, args) =>
            {
                UpdateCurrentSessionUI();
                RefreshInkSmoothingStats();
            };
            _uiUpdateTimer.Start();
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = false;
            _uiUpdateTimer?.Stop();
            _uiUpdateTimer = null;
        }

        private void ToggleSwitchEnableMonitoring_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            bool isOn = ToggleSwitchEnableMonitoring.IsOn;
            SettingsManager.Settings.Performance.IsMonitoringEnabled = isOn;
            SettingsManager.SaveSettingsToFile();

            if (isOn)
            {
                PerformanceMonitorHelper.Start();
            }
            else
            {
                PerformanceMonitorHelper.StopWithoutSaving();
                SettingsManager.Settings.Performance.History.Clear();
                SettingsManager.SaveSettingsToFile();
                PerformanceMonitorHelper.ClearHistory();
            }

            UpdateCurrentSessionUI();
            RefreshHistoryDisplay();
        }

        private void UpdateCurrentSessionUI()
        {
            if (PerformanceMonitorHelper.IsMonitoring)
            {
                CardCurrentStatus.Header = PerformanceStrings.MonitoringActive;
                PanelCurrentMetrics.Visibility = Visibility.Visible;
                TextCurrentCpu.Text = $"{PerformanceMonitorHelper.CurrentAvgCpu:F1} %";
                TextCurrentMemory.Text = $"{PerformanceMonitorHelper.CurrentMemoryMb:F0} MB";
                TextSystemCpu.Text = $"{PerformanceMonitorHelper.CurrentSystemCpuPercent:F1} %";
            }
            else
            {
                CardCurrentStatus.Header = PerformanceStrings.MonitoringInactive;
                PanelCurrentMetrics.Visibility = Visibility.Collapsed;
            }
        }

        private void RefreshHistoryDisplay()
        {
            var history = PerformanceMonitorHelper.LoadHistory();

            if (history.Count == 0)
            {
                TextHistorySummary.Text = PerformanceStrings.NoHistory;
                PanelHistoryStats.Visibility = Visibility.Collapsed;
                TextSmoothingHistorySummary.Text = PerformanceStrings.NoHistory;
                PanelSmoothingHistoryStats.Visibility = Visibility.Collapsed;
                PanelSmoothingHistoryPointStats.Visibility = Visibility.Collapsed;
                CardClearHistory.IsEnabled = false;
                return;
            }

            CardClearHistory.IsEnabled = true;
            TextHistorySummary.Text = string.Format(PerformanceStrings.HistoryRunCount, history.Count);
            PanelHistoryStats.Visibility = Visibility.Visible;

            TextHistoryAvgCpu.Text = $"{history.Average(r => r.AvgCpuPercent):F1} %";
            TextHistoryPeakCpu.Text = $"{history.Max(r => r.PeakCpuPercent):F1} %";
            TextHistoryAvgMem.Text = $"{history.Average(r => r.AvgMemoryMb):F0} MB";
            TextHistoryPeakMem.Text = $"{history.Max(r => r.PeakMemoryMb):F0} MB";

            // 墨迹平滑历史
            var smoothingHistory = history.Where(r => r.SmoothingSampleCount > 0).ToList();
            if (smoothingHistory.Count == 0)
            {
                TextSmoothingHistorySummary.Text = PerformanceStrings.InkSmoothingInactive;
                PanelSmoothingHistoryStats.Visibility = Visibility.Collapsed;
                PanelSmoothingHistoryPointStats.Visibility = Visibility.Collapsed;
            }
            else
            {
                TextSmoothingHistorySummary.Text = string.Format(PerformanceStrings.HistoryRunCount, smoothingHistory.Count);
                PanelSmoothingHistoryStats.Visibility = Visibility.Visible;
                PanelSmoothingHistoryPointStats.Visibility = Visibility.Visible;

                TextSmoothingHistoryAvgTotal.Text = $"{smoothingHistory.Average(r => r.SmoothingAvgTotalMs):F2} ms";
                TextSmoothingHistoryMaxTotal.Text = $"{smoothingHistory.Max(r => r.SmoothingMaxTotalMs):F2} ms";
                TextSmoothingHistoryAvgBezier.Text = $"{smoothingHistory.Average(r => r.SmoothingAvgBezierMs):F2} ms";
                TextSmoothingHistoryAvgResample.Text = $"{smoothingHistory.Average(r => r.SmoothingAvgResampleMs):F2} ms";
                TextSmoothingHistoryAvgInput.Text = $"{smoothingHistory.Average(r => r.SmoothingAvgInputPoints):F0}";
                TextSmoothingHistoryAvgOutput.Text = $"{smoothingHistory.Average(r => r.SmoothingAvgOutputPoints):F0}";
                TextSmoothingHistorySampleCount.Text = smoothingHistory.Sum(r => r.SmoothingSampleCount).ToString();
            }
        }

        private void RefreshDeviceScoreDisplay()
        {
            var perf = SettingsManager.Settings.Performance;

            if (perf.DeviceScore < 0)
            {
                PanelDeviceScoreResult.Visibility = Visibility.Collapsed;
                TextDeviceScorePlaceholder.Visibility = Visibility.Visible;
                return;
            }

            PanelDeviceScoreResult.Visibility = Visibility.Visible;
            TextDeviceScorePlaceholder.Visibility = Visibility.Collapsed;

            TextOverallScore.Text = perf.DeviceScore.ToString();
            TextCpuScore.Text = perf.CpuScore.ToString();
            TextMemoryScore.Text = perf.MemoryScore.ToString();
            TextDiskScore.Text = perf.DiskScore.ToString();

            // Color the overall score
            TextOverallScore.Foreground = GetScoreBrush(perf.DeviceScore);
            TextScoreDescription.Text = GetScoreDescription(perf.DeviceScore);

            if (!string.IsNullOrEmpty(perf.LastTestTime))
            {
                TextLastTestTime.Text = $"{PerformanceStrings.LastTestTime}: {perf.LastTestTime}";
                TextLastTestTime.Visibility = Visibility.Visible;
            }
            else
            {
                TextLastTestTime.Visibility = Visibility.Collapsed;
            }
        }

        private async void CardRunDeviceTest_Click(object sender, RoutedEventArgs e)
        {
            BtnRunDeviceTest.IsEnabled = false;
            BtnRunDeviceTest.Content = PerformanceStrings.TestInProgress;

            try
            {
                var (overall, cpu, memory, disk) = await PerformanceMonitorHelper.RunDeviceEvaluationAsync();

                var perf = SettingsManager.Settings.Performance;
                perf.DeviceScore = overall;
                perf.CpuScore = cpu;
                perf.MemoryScore = memory;
                perf.DiskScore = disk;
                perf.LastTestTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                SettingsManager.SaveSettingsToFile();

                RefreshDeviceScoreDisplay();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"PerformancePage: Device evaluation failed: {ex.Message}", LogHelper.LogType.Error);
            }
            finally
            {
                BtnRunDeviceTest.IsEnabled = true;
                BtnRunDeviceTest.Content = PerformanceStrings.RunDeviceTest;
            }
        }

        private async void CardClearHistory_Click(object sender, RoutedEventArgs e)
        {
            var result = await MessageBox.ShowAsync(
                PerformanceStrings.ClearHistoryConfirm,
                PerformanceStrings.ClearHistory,
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.OK)
            {
                PerformanceMonitorHelper.ClearHistory();
                SettingsManager.Settings.Performance.History.Clear();
                SettingsManager.SaveSettingsToFile();
                RefreshHistoryDisplay();
            }
        }

        private void RefreshInkSmoothingStats()
        {
            try
            {
                var mainWindow = Application.Current.MainWindow as MainWindow;
                var manager = mainWindow?.InkSmoothingManagerInstance;

                if (manager == null)
                {
                    TextInkSmoothingInactive.Visibility = Visibility.Visible;
                    PanelInkSmoothingStats.Visibility = Visibility.Collapsed;
                    PanelInkSmoothingPointStats.Visibility = Visibility.Collapsed;
                    return;
                }

                var stats = manager.GetDetailedStats();

                if (stats.SampleCount == 0)
                {
                    TextInkSmoothingInactive.Visibility = Visibility.Visible;
                    PanelInkSmoothingStats.Visibility = Visibility.Collapsed;
                    PanelInkSmoothingPointStats.Visibility = Visibility.Collapsed;
                    return;
                }

                TextInkSmoothingInactive.Visibility = Visibility.Collapsed;
                PanelInkSmoothingStats.Visibility = Visibility.Visible;
                PanelInkSmoothingPointStats.Visibility = Visibility.Visible;

                TextSmoothingAvgTotal.Text = $"{stats.AvgTotalMs:F2} ms";
                TextSmoothingMaxTotal.Text = $"{stats.MaxTotalMs:F2} ms";
                TextSmoothingAvgBezier.Text = $"{stats.AvgBezierMs:F2} ms";
                TextSmoothingAvgResample.Text = $"{stats.AvgResampleMs:F2} ms";
                TextSmoothingAvgInput.Text = $"{stats.AvgInputPoints:F0}";
                TextSmoothingAvgOutput.Text = $"{stats.AvgOutputPoints:F0}";
                TextSmoothingSampleCount.Text = stats.SampleCount.ToString();
            }
            catch
            {
                // Silently handle cases where MainWindow isn't ready
            }
        }

        private static string GetScoreDescription(int score)
        {
            if (score >= 85) return PerformanceStrings.DeviceScoreExcellent;
            if (score >= 65) return PerformanceStrings.DeviceScoreGood;
            if (score >= 45) return PerformanceStrings.DeviceScoreFair;
            return PerformanceStrings.DeviceScorePoor;
        }

        private static System.Windows.Media.Brush GetScoreBrush(int score)
        {
            if (score >= 85)
                return (System.Windows.Media.Brush)System.Windows.Application.Current.FindResource("SystemFillColorGoodBrush");
            if (score >= 65)
                return (System.Windows.Media.Brush)System.Windows.Application.Current.FindResource("SystemFillColorCautionBrush");
            if (score >= 45)
                return (System.Windows.Media.Brush)System.Windows.Application.Current.FindResource("SystemFillColorCautionBrush");
            return (System.Windows.Media.Brush)System.Windows.Application.Current.FindResource("SystemFillColorCriticalBrush");
        }
    }
}
