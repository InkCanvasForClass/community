using Ink_Canvas.Helpers;
using Ink_Canvas.Models;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using System.Windows;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class AnnouncementPage : Page
    {
        private bool _isLoaded;

        public AnnouncementPage()
        {
            InitializeComponent();
            Loaded += AnnouncementPage_Loaded;
            Unloaded += AnnouncementPage_Unloaded;
        }

        private void AnnouncementPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();
            _isLoaded = true;
        }

        private void AnnouncementPage_Unloaded(object sender, RoutedEventArgs e)
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
        }

        private void SaveSettings()
        {
            if (!_isLoaded) return;
            SettingsManager.SaveSettingsToFile();
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
            var window = new AnnouncementCenterWindow
            {
                Owner = Window.GetWindow(this)
            };
            window.ShowDialog();
        }

        private void ApiBaseUrlTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Notification.AnnouncementApiBaseUrl = ApiBaseUrlTextBox.Text.Trim();
            SaveSettings();
        }

        private void WebSocketUrlTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Notification.AnnouncementWebSocketUrl = WebSocketUrlTextBox.Text.Trim();
            SaveSettings();
        }

        private void TokenTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Notification.AnnouncementSoftwareToken = TokenTextBox.Text.Trim();
            SaveSettings();
        }
    }
}
