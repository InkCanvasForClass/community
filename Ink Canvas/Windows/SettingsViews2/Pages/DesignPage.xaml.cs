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

namespace Ink_Canvas.Windows.SettingsViews2.Pages
{
    public partial class DesignPage : Page
    {
        public DesignPage()
        {
            InitializeComponent();
        }

        private void SettingsCard_Click(object sender, RoutedEventArgs e)
        {
            SettingsWindow2 settingsWindow = Window.GetWindow(this) as SettingsWindow2;
            if (settingsWindow != null)
            {
                settingsWindow.NavigateToPage("IconographyPage");
            }
        }

        private void SettingsCard_Click_1(object sender, RoutedEventArgs e)
        {
            SettingsWindow2 settingsWindow = Window.GetWindow(this) as SettingsWindow2;
            if (settingsWindow != null)
            {
                settingsWindow.NavigateToPage("TypographyPage");
            }
        }
    }
}
