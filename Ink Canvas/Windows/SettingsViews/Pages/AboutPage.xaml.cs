using Ink_Canvas.Helpers;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public class AvatarItem
    {
        public string AvatarPath { get; set; }
        public string Name { get; set; }
        public string Role { get; set; }
    }

    public partial class AboutPage : iNKORE.UI.WPF.Modern.Controls.Page
    {
        private bool _isLoaded = false;
        private bool _isChangingTelemetryInternally;
        private bool _isChangingTelemetryPrivacyInternally;
        private DispatcherTimer _usageRefreshTimer;
        private long _savedTotalSeconds;
        private DateTime _sessionStartTime;

        public AboutPage()
        {
            InitializeComponent();
            Loaded += AboutPage_Loaded;
            Unloaded += AboutPage_Unloaded;
            InitializeAvatarData();
        }

        private void InitializeAvatarData()
        {
            var developers = new ObservableCollection<AvatarItem>
            {
                new AvatarItem { AvatarPath = "/Resources/DeveloperAvatars/CJKmkp.jpg", Name = "CJK_mkp", Role = LocalizationHelper.GetString("About_Dev_ICCCE") },
                new AvatarItem { AvatarPath = "/Resources/DeveloperAvatars/dubi906w.jpg", Name = "Dubi906w", Role = LocalizationHelper.GetString("About_Dev_ICC") },
                new AvatarItem { AvatarPath = "/Resources/DeveloperAvatars/ChangSakura.png", Name = "ChangSakura", Role = LocalizationHelper.GetString("About_Dev_ICA") },
                new AvatarItem { AvatarPath = "/Resources/DeveloperAvatars/WXRIW.png", Name = "WXRIW", Role = LocalizationHelper.GetString("About_Dev_InkCanvas") }
            };

            var contributors = new ObservableCollection<AvatarItem>
            {
                new AvatarItem { AvatarPath = "/Resources/DeveloperAvatars/RaspberryKan.jpg", Name = "Raspberry Kan" },
                new AvatarItem { AvatarPath = "/Resources/DeveloperAvatars/kengwang.png", Name = "Kengwang" },
                new AvatarItem { AvatarPath = "/Resources/DeveloperAvatars/jiajiaxd.jpg", Name = "Charles Jia" },
                new AvatarItem { AvatarPath = "/Resources/DeveloperAvatars/clover-yan.png", Name = "clover_yan" },
                new AvatarItem { AvatarPath = "/Resources/DeveloperAvatars/NetheriteBowl.png", Name = "Netherite_Bowl" },
                new AvatarItem { AvatarPath = "/Resources/DeveloperAvatars/NotYoojun.png", Name = "Yoojun Zhou" },
                new AvatarItem { AvatarPath = "/Resources/DeveloperAvatars/yuwenhui2020.png", Name = "YuWenHui2020" },
                new AvatarItem { AvatarPath = "/Resources/DeveloperAvatars/STBBRD.png", Name = "ZongziTEK" },
                new AvatarItem { AvatarPath = "/Resources/DeveloperAvatars/aaaaaaccd.jpg", Name = "Aesthed" },
                new AvatarItem { AvatarPath = "/Resources/DeveloperAvatars/wwei.png", Name = "Wei" },
                new AvatarItem { AvatarPath = "/Resources/DeveloperAvatars/Alan-CRL.png", Name = "Alan-CRL" },
                new AvatarItem { AvatarPath = "/Resources/DeveloperAvatars/PrefacedCorg.jpg", Name = "PrefacedCorg" },
                new AvatarItem { AvatarPath = "/Resources/DeveloperAvatars/PANDA-JSR.jpg", Name = "PANDA-JSR" }
            };

            DeveloperItemsControl.ItemsSource = developers;
            ContributorItemsControl.ItemsSource = contributors;
        }

        private void AboutPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();
            _isLoaded = true;

            if (_usageRefreshTimer == null)
            {
                _usageRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                _usageRefreshTimer.Tick += UsageRefreshTimer_Tick;
            }
            _usageRefreshTimer.Start();
        }

        private void AboutPage_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_usageRefreshTimer != null)
            {
                _usageRefreshTimer.Stop();
                _usageRefreshTimer.Tick -= UsageRefreshTimer_Tick;
                _usageRefreshTimer = null;
            }
        }

        private void UsageRefreshTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                long currentSessionSeconds = (long)(DateTime.Now - _sessionStartTime).TotalSeconds;
                TotalUsageTextBlock.Text = DeviceIdentifier.FormatDuration(_savedTotalSeconds + currentSessionSeconds);
            }
            catch { }
        }

        private void LoadSettings()
        {
            _isLoaded = false;

            try
            {
                AppVersionTextBlock.Text = Assembly.GetExecutingAssembly().GetName().Version.ToString();

                var settings = SettingsManager.Settings;
                if (settings?.Startup != null)
                {
                    int idx = 0;
                    switch (settings.Startup.TelemetryUploadLevel)
                    {
                        case TelemetryUploadLevel.None:
                            idx = 0;
                            break;
                        case TelemetryUploadLevel.Basic:
                            idx = 1;
                            break;
                        case TelemetryUploadLevel.Extended:
                            idx = 2;
                            break;
                        default:
                            idx = 0;
                            break;
                    }
                    ComboBoxTelemetryUploadLevel.SelectedIndex = idx;
                    CheckBoxTelemetryPrivacyAccepted.IsChecked = settings.Startup.HasAcceptedTelemetryPrivacy;
                }

                RefreshDeviceInfo();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载关于页面设置时出错: {ex.Message}");
            }

            _isLoaded = true;
        }

        private void RefreshDeviceInfo_Click(object sender, RoutedEventArgs e)
        {
            RefreshDeviceInfo();
        }

        private void RefreshDeviceInfo()
        {
            try
            {
                string deviceId = DeviceIdentifier.GetDeviceId();
                DeviceIdTextBlock.Text = deviceId;

                var usageFrequency = DeviceIdentifier.GetUsageFrequency();
                string frequencyText;
                switch (usageFrequency)
                {
                    case DeviceIdentifier.UsageFrequency.High:
                        frequencyText = "高频用户";
                        break;
                    case DeviceIdentifier.UsageFrequency.Medium:
                        frequencyText = "中频用户";
                        break;
                    case DeviceIdentifier.UsageFrequency.Low:
                        frequencyText = "低频用户";
                        break;
                    default:
                        frequencyText = "未知";
                        break;
                }
                UsageFrequencyTextBlock.Text = frequencyText;

                var updatePriority = DeviceIdentifier.GetUpdatePriority();
                string priorityText;
                switch (updatePriority)
                {
                    case DeviceIdentifier.UpdatePriority.High:
                        priorityText = "高优先级（优先推送更新）";
                        break;
                    case DeviceIdentifier.UpdatePriority.Medium:
                        priorityText = "中优先级（正常推送更新）";
                        break;
                    case DeviceIdentifier.UpdatePriority.Low:
                        priorityText = "低优先级（延迟推送更新）";
                        break;
                    default:
                        priorityText = "未知";
                        break;
                }
                UpdatePriorityTextBlock.Text = priorityText;

                var (launchCount, totalSeconds, avgSessionSeconds, _) = DeviceIdentifier.GetUsageStats();
                _savedTotalSeconds = totalSeconds;
                _sessionStartTime = DateTime.Now;
                LaunchCountTextBlock.Text = launchCount.ToString();
                TotalUsageTextBlock.Text = DeviceIdentifier.FormatDuration(totalSeconds);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"刷新设备信息失败: {ex.Message}", LogHelper.LogType.Error);
                DeviceIdTextBlock.Text = "获取失败";
                UsageFrequencyTextBlock.Text = "获取失败";
                UpdatePriorityTextBlock.Text = "获取失败";
                LaunchCountTextBlock.Text = "获取失败";
                TotalUsageTextBlock.Text = "获取失败";
            }
        }

        private void ComboBoxTelemetryUploadLevel_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            if (_isChangingTelemetryInternally) return;
            var oldLevel = SettingsManager.Settings.Startup.TelemetryUploadLevel;
            var item = ComboBoxTelemetryUploadLevel?.SelectedItem as ComboBoxItem;
            if (item == null) return;

            var tag = item.Tag?.ToString() ?? "0";
            var newLevel = TelemetryUploadLevel.None;
            switch (tag)
            {
                case "1":
                    newLevel = TelemetryUploadLevel.Basic;
                    break;
                case "2":
                    newLevel = TelemetryUploadLevel.Extended;
                    break;
                default:
                    newLevel = TelemetryUploadLevel.None;
                    break;
            }

            if (newLevel == TelemetryUploadLevel.None &&
                oldLevel != TelemetryUploadLevel.None &&
                SettingsManager.Settings.Startup.UpdateChannel != UpdateChannel.Release)
            {
                var result = MessageBox.Show(
                    "关闭匿名使用数据上传后，将无法继续使用预览/测试通道，系统会自动切换回正式通道（Release）。\n\n是否确认关闭？",
                    "确认关闭遥测",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                {
                    _isChangingTelemetryInternally = true;
                    try
                    {
                        int idx = 0;
                        switch (oldLevel)
                        {
                            case TelemetryUploadLevel.Basic:
                                idx = 1;
                                break;
                            case TelemetryUploadLevel.Extended:
                                idx = 2;
                                break;
                            default:
                                idx = 0;
                                break;
                        }
                        ComboBoxTelemetryUploadLevel.SelectedIndex = idx;
                    }
                    finally
                    {
                        _isChangingTelemetryInternally = false;
                    }
                    return;
                }

                SettingsManager.Settings.Startup.UpdateChannel = UpdateChannel.Release;
                DeviceIdentifier.UpdateUsageChannel(UpdateChannel.Release);
            }

            if (newLevel != TelemetryUploadLevel.None && !SettingsManager.Settings.Startup.HasAcceptedTelemetryPrivacy)
            {
                MessageBox.Show(
                    "在开启匿名使用数据上传前，请先阅读并勾选上方的隐私说明。",
                    "需要同意隐私说明",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                _isChangingTelemetryInternally = true;
                try
                {
                    SettingsManager.Settings.Startup.TelemetryUploadLevel = TelemetryUploadLevel.None;
                    if (ComboBoxTelemetryUploadLevel != null)
                    {
                        ComboBoxTelemetryUploadLevel.SelectedIndex = 0;
                    }
                }
                finally
                {
                    _isChangingTelemetryInternally = false;
                }

                return;
            }

            SettingsManager.Settings.Startup.TelemetryUploadLevel = newLevel;
            SettingsManager.SaveSettingsToFile();
        }

        private void CheckBoxTelemetryPrivacyAccepted_Checked(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            if (_isChangingTelemetryPrivacyInternally) return;

            bool isChecked = CheckBoxTelemetryPrivacyAccepted.IsChecked == true;

            if (isChecked)
            {
                if (!PrivacyFileExists())
                {
                    MessageBox.Show(
                        "未找到隐私说明文件（privacy / privacy.txt），暂时无法启用匿名使用数据上传。",
                        "隐私说明缺失",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    _isChangingTelemetryPrivacyInternally = true;
                    try
                    {
                        CheckBoxTelemetryPrivacyAccepted.IsChecked = false;
                    }
                    finally
                    {
                        _isChangingTelemetryPrivacyInternally = false;
                    }

                    SettingsManager.Settings.Startup.HasAcceptedTelemetryPrivacy = false;
                    SettingsManager.SaveSettingsToFile();
                    return;
                }

                var privacyWindow = new PrivacyAgreementWindow();
                bool? dialogResult = privacyWindow.ShowDialog();

                if (dialogResult == true && privacyWindow.UserAccepted)
                {
                    SettingsManager.Settings.Startup.HasAcceptedTelemetryPrivacy = true;
                    SettingsManager.SaveSettingsToFile();
                }
                else
                {
                    _isChangingTelemetryPrivacyInternally = true;
                    try
                    {
                        CheckBoxTelemetryPrivacyAccepted.IsChecked = false;
                    }
                    finally
                    {
                        _isChangingTelemetryPrivacyInternally = false;
                    }

                    SettingsManager.Settings.Startup.HasAcceptedTelemetryPrivacy = false;
                    SettingsManager.SaveSettingsToFile();
                }
            }
            else
            {
                var result = MessageBox.Show(
                    "取消同意隐私说明后，将关闭匿名使用数据上传，并切回正式通道（Release）。\n\n是否确认？",
                    "确认取消隐私同意",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                {
                    _isChangingTelemetryPrivacyInternally = true;
                    try
                    {
                        CheckBoxTelemetryPrivacyAccepted.IsChecked = true;
                    }
                    finally
                    {
                        _isChangingTelemetryPrivacyInternally = false;
                    }
                    return;
                }

                _isChangingTelemetryInternally = true;
                try
                {
                    SettingsManager.Settings.Startup.TelemetryUploadLevel = TelemetryUploadLevel.None;
                    if (ComboBoxTelemetryUploadLevel != null)
                    {
                        ComboBoxTelemetryUploadLevel.SelectedIndex = 0;
                    }
                }
                finally
                {
                    _isChangingTelemetryInternally = false;
                }

                if (SettingsManager.Settings.Startup.UpdateChannel != UpdateChannel.Release)
                {
                    SettingsManager.Settings.Startup.UpdateChannel = UpdateChannel.Release;
                    DeviceIdentifier.UpdateUsageChannel(UpdateChannel.Release);
                }

                SettingsManager.Settings.Startup.HasAcceptedTelemetryPrivacy = false;
                SettingsManager.SaveSettingsToFile();
            }
        }

        private static bool PrivacyFileExists()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "Ink_Canvas.privacy.txt";
                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    return stream != null;
                }
            }
            catch
            {
                return false;
            }
        }

    }
}
