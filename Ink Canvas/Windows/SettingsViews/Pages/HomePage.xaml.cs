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
    }
}
