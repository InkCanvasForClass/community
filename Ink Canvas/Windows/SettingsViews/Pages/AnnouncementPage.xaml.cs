using Ink_Canvas.Properties;
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
                DisplayName = NotificationStrings.Provider_Announcement,
                Description = NotificationStrings.Provider_AnnouncementDesc,
                IsEnabled = SettingsManager.Settings.Notification.IsAnnouncementEnabled,
                IsRunning = false,
                Status = SettingsManager.Settings.Notification.IsAnnouncementEnabled
                    ? NotificationStrings.Provider_WaitingRestart
                    : NotificationStrings.Provider_Disabled
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
    }
}
