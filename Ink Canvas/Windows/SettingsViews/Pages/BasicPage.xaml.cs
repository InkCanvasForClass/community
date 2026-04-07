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
