using System;
using System.Windows;
using System.Windows.Threading;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class WordPage : Page
    {
        private bool _ready, _pendingSave;
        private readonly DispatcherTimer _saveTimer;
        public WordPage()
        {
            InitializeComponent();
            PreviewRight.Mirror(PreviewLeft);
            PreviewLeft.SetState("Ready", "", 3, 10, false, 100);
            _saveTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(250) };
            _saveTimer.Tick += (_, _) => SavePending();
            Loaded += (_, _) => LoadControls();
            Unloaded += (_, _) => { _ready = false; SavePending(); };
        }

        private void LoadControls()
        {
            _ready = false;
            SettingsManager.MigrateWordLinkSettings();
            var settings = SettingsManager.Settings.WordLink;
            settings.Normalize();
            EnableWord.IsOn = settings.Enabled;
            AutoEnterFocusMode.IsOn = settings.AutoEnterFocusMode;
            FocusToolbarOpacity.Value = settings.FocusToolbarOpacity;
            ShowNavigation.IsOn = settings.ShowNavigation;
            ShowZoom.IsOn = settings.ShowZoom;
            FollowWindow.IsOn = settings.FollowWordWindow;
            UsePptAppearance.IsOn = settings.UsePptAppearance;
            ShowPageNumber.IsOn = settings.ShowPageNumber;
            DarkBackground.IsOn = settings.DarkBackground;
            NavScale.Value = settings.NavigationScale;
            NavOpacity.Value = settings.NavigationOpacity;
            NavShadow.IsOn = settings.NavigationShadow;
            ShadowSize.Value = settings.ShadowSize;
            HorizontalInset.Value = settings.HorizontalInset;
            BottomInset.Value = settings.BottomInset;
            _ready = true;
            RefreshPreview();
        }
        private void SettingsChanged(object sender, RoutedEventArgs e)
        {
            if (!_ready) return;
            var settings = SettingsManager.Settings.WordLink;
            settings.Enabled = EnableWord.IsOn;
            settings.AutoEnterFocusMode = AutoEnterFocusMode.IsOn;
            settings.FocusToolbarOpacity = FocusToolbarOpacity.Value;
            settings.ShowNavigation = ShowNavigation.IsOn;
            settings.ShowZoom = ShowZoom.IsOn;
            settings.FollowWordWindow = FollowWindow.IsOn;
            settings.UsePptAppearance = UsePptAppearance.IsOn;
            settings.ShowPageNumber = ShowPageNumber.IsOn;
            settings.DarkBackground = DarkBackground.IsOn;
            settings.NavigationScale = NavScale.Value;
            settings.NavigationOpacity = NavOpacity.Value;
            settings.NavigationShadow = NavShadow.IsOn;
            settings.ShadowSize = ShadowSize.Value;
            settings.HorizontalInset = HorizontalInset.Value;
            settings.BottomInset = BottomInset.Value;
            ApplyChanges();
        }
        private void RefreshPreview()
        {
            var settings = SettingsManager.Settings.WordLink;
            CustomAppearanceControls.IsEnabled = !settings.UsePptAppearance;
            PreviewLeft.ApplySettings(settings, SettingsManager.Settings.PowerPointSettings, false);
            PreviewRight.ApplySettings(settings, SettingsManager.Settings.PowerPointSettings, true);
        }
        private void ApplyChanges()
        {
            RefreshPreview();
            (Application.Current.MainWindow as MainWindow)?.ApplyWordLinkSettings();
            _pendingSave = true;
            _saveTimer.Stop(); _saveTimer.Start();
        }
        private void SavePending()
        {
            _saveTimer.Stop();
            if (!_pendingSave) return;
            _pendingSave = false;
            SettingsManager.SaveSettingsToFile();
        }
        private void ResetNavigationClick(object sender, RoutedEventArgs e)
        {
            if (!_ready) return;
            bool enabled = SettingsManager.Settings.WordLink.Enabled;
            var previous = SettingsManager.Settings.WordLink;
            SettingsManager.Settings.WordLink = new WordLinkSettings { Enabled = enabled,
                AutoEnterFocusMode = previous.AutoEnterFocusMode, FocusToolbarOpacity = previous.FocusToolbarOpacity };
            LoadControls();
            ApplyChanges();
        }
    }
}
