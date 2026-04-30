using Ink_Canvas.Helpers;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using System.Windows;
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
    }
}