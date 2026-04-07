using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    /// <summary>
    /// Basic.xaml 的交互逻辑
    /// </summary>
    public partial class BasicPage : Page
    {
        public BasicPage()
        {
            InitializeComponent();
        }

        private void SettingsCard_Click(object sender, RoutedEventArgs e)
        {
            // 找到SettingsWindow窗口
            SettingsWindow settingsWindow = Window.GetWindow(this) as SettingsWindow;
            if (settingsWindow != null)
            {
                // 调用NavigateToPage方法导航到启动页面
                settingsWindow.NavigateToPage("StartupPage");
            }
        }
    }
}
