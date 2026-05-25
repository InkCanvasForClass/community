using Ink_Canvas.Windows.SettingsViews.Helpers;
using System.Windows;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class HomePage
    {
        public HomePage()
        {
            InitializeComponent();
        }

        private void QuickNavCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is string pageTag)
            {
                var settingsWindow = Window.GetWindow(this) as SettingsWindow;
                settingsWindow?.NavigateToPage(pageTag);
            }
        }

        private void BtnRestart_Click(object sender, RoutedEventArgs e)
        {
            SettingsActionHub.OnRestartApplication(sender, e);
        }

        private void BtnResetToSuggestion_Click(object sender, RoutedEventArgs e)
        {
            SettingsActionHub.OnResetToSuggestion(sender, e);
        }

        private void BtnExit_Click(object sender, RoutedEventArgs e)
        {
            SettingsActionHub.OnExitApplication(sender, e);
        }
    }
}
