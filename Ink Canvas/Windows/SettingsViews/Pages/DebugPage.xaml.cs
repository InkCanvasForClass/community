using Ink_Canvas.Helpers;
using Ink_Canvas.Windows.SettingsViews.Helpers;
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

        private void BtnTestCrash_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "确定要触发一次模拟崩溃吗？\n\n应用将立即退出并尝试自动重启。",
                "崩溃测试确认",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            LogHelper.WriteLogToFile("[Debug] 用户手动触发模拟崩溃", LogHelper.LogType.Warning);
            throw new System.InvalidOperationException("[Debug] 用户手动触发的模拟崩溃异常");
        }

        private void BtnTestHeartbeatTimeout_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "确定要停止心跳计时器吗？\n\n约10秒后守护检查将检测到心跳超时并触发重启。",
                "心跳超时测试确认",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            App.DebugStopHeartbeat();
        }

        private void BtnResetStartupCount_Click(object sender, RoutedEventArgs e)
        {
            StartupCount.Reset();
            LogHelper.WriteLogToFile("[Debug] 熔断计数器已手动重置", LogHelper.LogType.Warning);
            MessageBox.Show("熔断计数器已重置为 0。", "重置完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}