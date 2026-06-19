using Ink_Canvas.Helpers;
using Ink_Canvas.Models;
using Ink_Canvas.Properties;
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
            SliderTouchHelper.AddTouchSupportToAllSliders(this);
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
            CardEnableDynamic.IsOn = notification.IsDynamicNotificationEnabled;
            CardEnableWindowsToast.IsOn = notification.IsWindowsToastEnabled;
            ToggleSwitchDictationDoNotDisturb.IsOn = notification.IsDictationDoNotDisturbEnabled;
            CheckBoxDictationDoNotDisturbPPT.IsChecked = notification.IsDictationDoNotDisturbInPPTEnabled;
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
                DisplayName = NotificationStrings.Provider_Local,
                Description = NotificationStrings.Provider_LocalDesc,
                IsEnabled = true,
                IsRunning = true,
                Status = NotificationStrings.Provider_Running
            });

            NotificationProviderRegistry.RegisterOrUpdate(new NotificationProviderStatus
            {
                ProviderId = "windows-toast",
                DisplayName = NotificationStrings.Provider_WindowsToast,
                Description = NotificationStrings.Provider_WindowsToastDesc,
                IsEnabled = SettingsManager.Settings.Notification.IsWindowsToastEnabled,
                IsRunning = SettingsManager.Settings.Notification.IsWindowsToastEnabled,
                Status = SettingsManager.Settings.Notification.IsWindowsToastEnabled
                    ? NotificationStrings.Provider_Running
                    : NotificationStrings.Provider_Disabled
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
                DisplayName = NotificationStrings.Provider_Announcement,
                Description = NotificationStrings.Provider_AnnouncementDesc,
                IsEnabled = SettingsManager.Settings.Notification.IsAnnouncementEnabled,
                IsRunning = false,
                Status = SettingsManager.Settings.Notification.IsAnnouncementEnabled
                    ? NotificationStrings.Provider_WaitingRestart
                    : NotificationStrings.Provider_Disabled
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
            SettingsManager.Settings.Notification.IsDictationDoNotDisturbInPPTEnabled = CheckBoxDictationDoNotDisturbPPT.IsChecked == true;
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
                Title = NotificationStrings.TestTitle,
                Summary = NotificationStrings.TestSummary,
                Icon = "Info",
                DisplaySeconds = SettingsManager.Settings.Notification.OtherDurationSeconds,
                Source = "local",
                ProviderId = "local"
            });
        }
    }
}
