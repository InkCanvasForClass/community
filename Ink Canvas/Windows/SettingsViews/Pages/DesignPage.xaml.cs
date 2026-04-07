using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class DesignPage : Page
    {
        public DesignPage()
        {
            InitializeComponent();
        }

        private void SettingsCard_Click(object sender, RoutedEventArgs e)
        {
            SettingsWindow settingsWindow = Window.GetWindow(this) as SettingsWindow;
            if (settingsWindow != null)
            {
                settingsWindow.NavigateToPage("IconographyPage");
            }
        }

        private void SettingsCard_Click_1(object sender, RoutedEventArgs e)
        {
            SettingsWindow settingsWindow = Window.GetWindow(this) as SettingsWindow;
            if (settingsWindow != null)
            {
                settingsWindow.NavigateToPage("TypographyPage");
            }
        }
    }
}
