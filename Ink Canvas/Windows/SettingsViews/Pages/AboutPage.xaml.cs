using Ink_Canvas.Helpers;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
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
            try
            {
                AppVersionTextBlock.Text = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                RefreshDeviceInfo();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载关于页面设置时出错: {ex.Message}");
            }
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
    }
}
