using Ink_Canvas.Helpers;
using Ink_Canvas.Models;
using Ink_Canvas.Windows.SettingsViews;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Windows;
using System.Windows.Controls;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class NotificationPage : Page
    {
        private bool _isLoaded;

        public NotificationPage()
        {
            InitializeComponent();
            Loaded += NotificationPage_Loaded;
            Unloaded += NotificationPage_Unloaded;
        }

        private void NotificationPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();
            LoadProviders();
            _isLoaded = true;
        }

        private void NotificationPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = false;
        }

        private void LoadSettings()
        {
            _isLoaded = false;
            var notification = SettingsManager.Settings.Notification;

            CardEnableAnnouncements.IsOn = notification.IsAnnouncementEnabled;
            CardEnableForcePopup.IsOn = notification.IsForcePopupEnabled;
            ApiBaseUrlTextBox.Text = notification.AnnouncementApiBaseUrl ?? string.Empty;
            WebSocketUrlTextBox.Text = notification.AnnouncementWebSocketUrl ?? string.Empty;
            TokenTextBox.Text = notification.AnnouncementSoftwareToken ?? string.Empty;
            CardEnableDynamic.IsOn = notification.IsDynamicNotificationEnabled;
            CardEnableWindowsToast.IsOn = notification.IsWindowsToastEnabled;
            ToggleSwitchDictationDoNotDisturb.IsOn = notification.IsDictationDoNotDisturbEnabled;
            CheckBoxDictationDoNotDisturbPpt.IsChecked = notification.IsDictationDoNotDisturbInPptEnabled;
            CheckBoxDictationDoNotDisturbWhiteboard.IsChecked = notification.IsDictationDoNotDisturbInWhiteboardEnabled;

            SelectComboBoxItemByTag(ComboBoxPlacement, notification.Placement, "TopCenter");
            SelectComboBoxItemByTag(ComboBoxAnimationMode, notification.AnimationMode, "Standard");

            UpdateDurationSlider.Value = Math.Max(UpdateDurationSlider.Minimum, Math.Min(UpdateDurationSlider.Maximum, notification.UpdateDurationSeconds));
            UrgentDurationSlider.Value = Math.Max(UrgentDurationSlider.Minimum, Math.Min(UrgentDurationSlider.Maximum, notification.UrgentDurationSeconds));
            ImportantDurationSlider.Value = Math.Max(ImportantDurationSlider.Minimum, Math.Min(ImportantDurationSlider.Maximum, notification.ImportantDurationSeconds));
            ReminderDurationSlider.Value = Math.Max(ReminderDurationSlider.Minimum, Math.Min(ReminderDurationSlider.Maximum, notification.ReminderDurationSeconds));
            OtherDurationSlider.Value = Math.Max(OtherDurationSlider.Minimum, Math.Min(OtherDurationSlider.Maximum, notification.OtherDurationSeconds));
            UpdateDurationTexts();
        }

        private void SelectComboBoxItemByTag(ComboBox comboBox, string value, string fallback)
        {
            var target = string.IsNullOrWhiteSpace(value) ? fallback : value;
            foreach (ComboBoxItem item in comboBox.Items)
            {
                if (string.Equals(item.Tag?.ToString(), target, StringComparison.OrdinalIgnoreCase))
                {
                    comboBox.SelectedItem = item;
                    return;
                }
            }

            comboBox.SelectedIndex = 0;
        }

        private void SaveSettings()
        {
            if (!_isLoaded) return;
            SettingsManager.SaveSettingsToFile();
        }

        private void LoadProviders()
        {
            NotificationProviderRegistry.RegisterOrUpdate(new NotificationProviderStatus
            {
                ProviderId = "local",
                DisplayName = Ink_Canvas.Properties.Strings.GetString("Notification_Provider_Local") ?? "本地消息提供商",
                Description = Ink_Canvas.Properties.Strings.GetString("Notification_Provider_LocalDesc") ?? "接收应用内部提示、错误和插件消息。",
                IsEnabled = true,
                IsRunning = true,
                Status = Ink_Canvas.Properties.Strings.GetString("Notification_Provider_Running") ?? "运行中"
            });

            NotificationProviderRegistry.RegisterOrUpdate(new NotificationProviderStatus
            {
                ProviderId = "windows-toast",
                DisplayName = Ink_Canvas.Properties.Strings.GetString("Notification_Provider_WindowsToast") ?? "Windows 系统通知",
                Description = Ink_Canvas.Properties.Strings.GetString("Notification_Provider_WindowsToastDesc") ?? "将消息同步显示为系统 Toast 或托盘气泡。",
                IsEnabled = SettingsManager.Settings.Notification.IsWindowsToastEnabled,
                IsRunning = SettingsManager.Settings.Notification.IsWindowsToastEnabled,
                Status = SettingsManager.Settings.Notification.IsWindowsToastEnabled
                    ? Ink_Canvas.Properties.Strings.GetString("Notification_Provider_Running") ?? "运行中"
                    : Ink_Canvas.Properties.Strings.GetString("Notification_Provider_Disabled") ?? "已禁用"
            });

            ProviderItemsControl.ItemsSource = NotificationProviderRegistry.GetProviders();
        }

        private void UpdateDurationTexts()
        {
            if (UpdateDurationText != null && UpdateDurationSlider != null)
                UpdateDurationText.Text = $"{UpdateDurationSlider.Value:F0}s";
            if (UrgentDurationText != null && UrgentDurationSlider != null)
                UrgentDurationText.Text = $"{UrgentDurationSlider.Value:F0}s";
            if (ImportantDurationText != null && ImportantDurationSlider != null)
                ImportantDurationText.Text = $"{ImportantDurationSlider.Value:F0}s";
            if (ReminderDurationText != null && ReminderDurationSlider != null)
                ReminderDurationText.Text = $"{ReminderDurationSlider.Value:F0}s";
            if (OtherDurationText != null && OtherDurationSlider != null)
                OtherDurationText.Text = $"{OtherDurationSlider.Value:F0}s";
        }

        private void UpdateAnnouncementProviderEnabledState()
        {
            NotificationProviderRegistry.RegisterOrUpdate(new NotificationProviderStatus
            {
                ProviderId = "announcement",
                DisplayName = Ink_Canvas.Properties.Strings.GetString("Notification_Provider_Announcement") ?? "公告提供商",
                Description = Ink_Canvas.Properties.Strings.GetString("Notification_Provider_AnnouncementDesc") ?? "拉取远端公告并接收实时推送。",
                IsEnabled = SettingsManager.Settings.Notification.IsAnnouncementEnabled,
                IsRunning = false,
                Status = SettingsManager.Settings.Notification.IsAnnouncementEnabled
                    ? Ink_Canvas.Properties.Strings.GetString("Notification_Provider_WaitingRestart") ?? "将在下次启动时生效"
                    : Ink_Canvas.Properties.Strings.GetString("Notification_Provider_Disabled") ?? "已禁用"
            });
            LoadProviders();
        }

        private void ToggleSwitchEnableAnnouncements_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Notification.IsAnnouncementEnabled = CardEnableAnnouncements.IsOn;
            SaveSettings();
            UpdateAnnouncementProviderEnabledState();
        }

        private void ToggleSwitchEnableForcePopup_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Notification.IsForcePopupEnabled = CardEnableForcePopup.IsOn;
            SaveSettings();
        }

        private void ViewAnnouncementsButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = Window.GetWindow(this) as SettingsWindow;
            settingsWindow?.NavigateToPage("AnnouncementCenterPage");
        }

        private void ApiBaseUrlTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Notification.AnnouncementApiBaseUrl = ApiBaseUrlTextBox.Text.Trim();
            SaveSettings();
        }

        private void WebSocketUrlTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Notification.AnnouncementWebSocketUrl = WebSocketUrlTextBox.Text.Trim();
            SaveSettings();
        }

        private void TokenTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Notification.AnnouncementSoftwareToken = TokenTextBox.Text.Trim();
            SaveSettings();
        }

        private void ToggleSwitchEnableDynamic_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Notification.IsDynamicNotificationEnabled = CardEnableDynamic.IsOn;
            SaveSettings();
        }

        private void ToggleSwitchEnableWindowsToast_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Notification.IsWindowsToastEnabled = CardEnableWindowsToast.IsOn;
            SaveSettings();
            LoadProviders();
        }

        private void ToggleSwitchDictationDoNotDisturb_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Notification.IsDictationDoNotDisturbEnabled = ToggleSwitchDictationDoNotDisturb.IsOn;
            SaveSettings();
        }

        private void DictationDoNotDisturbMode_CheckChanged(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Notification.IsDictationDoNotDisturbInPptEnabled = CheckBoxDictationDoNotDisturbPpt.IsChecked == true;
            SettingsManager.Settings.Notification.IsDictationDoNotDisturbInWhiteboardEnabled = CheckBoxDictationDoNotDisturbWhiteboard.IsChecked == true;
            SaveSettings();
        }

        private void ComboBoxPlacement_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded || ComboBoxPlacement.SelectedItem is not ComboBoxItem item) return;
            SettingsManager.Settings.Notification.Placement = item.Tag?.ToString() ?? "TopCenter";
            SaveSettings();
        }

        private void ComboBoxAnimationMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded || ComboBoxAnimationMode.SelectedItem is not ComboBoxItem item) return;
            SettingsManager.Settings.Notification.AnimationMode = item.Tag?.ToString() ?? "Standard";
            SaveSettings();
        }

        private void UpdateDurationSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (UpdateDurationText == null) return;
            UpdateDurationTexts();
            if (!_isLoaded) return;
            SettingsManager.Settings.Notification.UpdateDurationSeconds = (int)Math.Round(UpdateDurationSlider.Value);
            SaveSettings();
        }

        private void UrgentDurationSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (UrgentDurationText == null) return;
            UpdateDurationTexts();
            if (!_isLoaded) return;
            SettingsManager.Settings.Notification.UrgentDurationSeconds = (int)Math.Round(UrgentDurationSlider.Value);
            SaveSettings();
        }

        private void ImportantDurationSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ImportantDurationText == null) return;
            UpdateDurationTexts();
            if (!_isLoaded) return;
            SettingsManager.Settings.Notification.ImportantDurationSeconds = (int)Math.Round(ImportantDurationSlider.Value);
            SaveSettings();
        }

        private void ReminderDurationSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ReminderDurationText == null) return;
            UpdateDurationTexts();
            if (!_isLoaded) return;
            SettingsManager.Settings.Notification.ReminderDurationSeconds = (int)Math.Round(ReminderDurationSlider.Value);
            SaveSettings();
        }

        private void OtherDurationSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (OtherDurationText == null) return;
            UpdateDurationTexts();
            if (!_isLoaded) return;
            SettingsManager.Settings.Notification.OtherDurationSeconds = (int)Math.Round(OtherDurationSlider.Value);
            SaveSettings();
        }

        private void RefreshProvidersButton_Click(object sender, RoutedEventArgs e)
        {
            LoadProviders();
        }

        private void TestNotificationButton_Click(object sender, RoutedEventArgs e)
        {
            NotificationCenterService.Enqueue(new NotificationMessage
            {
                Id = "settings-test-" + Guid.NewGuid().ToString("N"),
                Type = NotificationMessageType.Other,
                Level = NotificationMessageLevel.Normal,
                Title = Ink_Canvas.Properties.Strings.GetString("Notification_TestTitle") ?? "通知测试",
                Summary = Ink_Canvas.Properties.Strings.GetString("Notification_TestSummary") ?? "灵动通知与消息队列运行正常。",
                Icon = "Info",
                DisplaySeconds = SettingsManager.Settings.Notification.OtherDurationSeconds,
                Source = "local",
                ProviderId = "local"
            });
        }
    }
}
