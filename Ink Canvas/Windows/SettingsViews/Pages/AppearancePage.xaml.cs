using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class AppearancePage : Page
    {
        public AppearancePage()
        {
            InitializeComponent();
        }

        private void SettingsCard_Click(object sender, RoutedEventArgs e)
        {
            SettingsWindow settingsWindow = Window.GetWindow(this) as SettingsWindow;
            if (settingsWindow != null)
            {
                settingsWindow.NavigateToPage("ThemePage");
            }
        }

        private void SettingsCard_Click_1(object sender, RoutedEventArgs e)
        {
            SettingsWindow settingsWindow = Window.GetWindow(this) as SettingsWindow;
            if (settingsWindow != null)
            {
                settingsWindow.NavigateToPage("ColorsPage");
            }
        }

        private void SettingsCard_Click_2(object sender, RoutedEventArgs e)
        {
            SettingsWindow settingsWindow = Window.GetWindow(this) as SettingsWindow;
            if (settingsWindow != null)
            {
                settingsWindow.NavigateToPage("FontsPage");
            }
        }
    }
}
