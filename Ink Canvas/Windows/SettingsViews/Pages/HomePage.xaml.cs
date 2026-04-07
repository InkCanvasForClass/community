using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    /// <summary>
    /// HomePage.xaml 的交互逻辑
    /// </summary>
    public partial class HomePage : Page
    {
        public HomePage()
        {
            InitializeComponent();
        }

        private void SettingsCard_Basic_Click(object sender, RoutedEventArgs e)
        {
            // 找到SettingsWindow窗口
            SettingsWindow settingsWindow = Window.GetWindow(this) as SettingsWindow;
            if (settingsWindow != null)
            {
                // 调用NavigateToPage方法导航到基本设置页面
                settingsWindow.NavigateToPage("BasicPage");
            }
        }

        private void SettingsCard_Page2_Click(object sender, RoutedEventArgs e)
        {
            // 找到SettingsWindow窗口
            SettingsWindow settingsWindow = Window.GetWindow(this) as SettingsWindow;
            if (settingsWindow != null)
            {
                // 调用NavigateToPage方法导航到页面2
                settingsWindow.NavigateToPage("Page2Page");
            }
        }

        private void SettingsCard_Design_Click(object sender, RoutedEventArgs e)
        {
            // 找到SettingsWindow窗口
            SettingsWindow settingsWindow = Window.GetWindow(this) as SettingsWindow;
            if (settingsWindow != null)
            {
                // 调用NavigateToPage方法导航到设计设置页面
                settingsWindow.NavigateToPage("DesignPage");
            }
        }

        private void SettingsCard_Appearance_Click(object sender, RoutedEventArgs e)
        {
            // 找到SettingsWindow窗口
            SettingsWindow settingsWindow = Window.GetWindow(this) as SettingsWindow;
            if (settingsWindow != null)
            {
                // 调用NavigateToPage方法导航到外观设置页面
                settingsWindow.NavigateToPage("AppearancePage");
            }
        }
    }
}
