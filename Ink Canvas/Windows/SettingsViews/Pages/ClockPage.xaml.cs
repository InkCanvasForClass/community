using Ink_Canvas.Helpers;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class ClockPage : Page
    {
        private bool _isLoaded = false;
        private DispatcherTimer _clockTimer;

        public ClockPage()
        {
            InitializeComponent();
            Loaded += Page_Loaded;
            Unloaded += Page_Unloaded;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();
            _isLoaded = true;
            StartClock();
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = false;
            StopClock();
        }

        private void LoadSettings()
        {
            var settings = SettingsManager.Settings;
            if (settings?.Appearance == null) return;

            ComboBoxTimeFormat.SelectedIndex = settings.Appearance.Use24HourTimeFormat ? 1 : 0;
            UpdateTimeDisplay();
        }

        private void StartClock()
        {
            _clockTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _clockTimer.Tick += ClockTimer_Tick;
            _clockTimer.Start();
            UpdateTimeDisplay();
        }

        private void StopClock()
        {
            if (_clockTimer != null)
            {
                _clockTimer.Stop();
                _clockTimer.Tick -= ClockTimer_Tick;
                _clockTimer = null;
            }
        }

        private void ClockTimer_Tick(object sender, EventArgs e)
        {
            UpdateTimeDisplay();
        }

        private void UpdateTimeDisplay()
        {
            var now = DateTime.Now;
            bool use24Hour = SettingsManager.Settings?.Appearance?.Use24HourTimeFormat ?? false;

            if (use24Hour)
                TextBlockCurrentTime.Text = now.ToString("HH:mm:ss");
            else
                TextBlockCurrentTime.Text = now.ToString("hh:mm:ss");

            string[] dayNames = { "星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六" };
            TextBlockCurrentDate.Text = $"{now:yyyy年MM月dd日} {dayNames[(int)now.DayOfWeek]}";
        }

        private void ComboBoxTimeFormat_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Appearance.Use24HourTimeFormat = ComboBoxTimeFormat.SelectedIndex == 1;
            SettingsManager.SaveSettingsToFile();
            UpdateTimeDisplay();
        }
    }
}
