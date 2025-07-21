using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas
{
    /// <summary>
    /// CustomIconWindow.xaml 的交互逻辑
    /// </summary>
    public partial class CustomIconWindow : Window
    {
        private MainWindow mainWindow;
        public ObservableCollection<CustomFloatingBarIcon> CustomIcons { get; set; }

        public CustomIconWindow(MainWindow owner)
        {
            InitializeComponent();
            mainWindow = owner;
            
            // 从主窗口的设置获取自定义图标列表
            CustomIcons = new ObservableCollection<CustomFloatingBarIcon>(MainWindow.Settings.Appearance.CustomFloatingBarImgs);
            CustomIconsListView.ItemsSource = CustomIcons;
        }

        private void DeleteCustomIcon_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is CustomFloatingBarIcon icon)
            {
                // 从列表中移除图标
                CustomIcons.Remove(icon);
                
                // 更新主窗口的设置
                MainWindow.Settings.Appearance.CustomFloatingBarImgs.Clear();
                foreach (var customIcon in CustomIcons)
                {
                    MainWindow.Settings.Appearance.CustomFloatingBarImgs.Add(customIcon);
                }
                
                // 如果当前选中的是被删除的图标，重置为默认图标
                if (MainWindow.Settings.Appearance.FloatingBarImg >= 8 && 
                    MainWindow.Settings.Appearance.FloatingBarImg - 8 >= MainWindow.Settings.Appearance.CustomFloatingBarImgs.Count)
                {
                    MainWindow.Settings.Appearance.FloatingBarImg = 0;
                    mainWindow.ComboBoxFloatingBarImg.SelectedIndex = 0;
                    mainWindow.UpdateFloatingBarIcon();
                }
                
                // 更新ComboBox
                mainWindow.UpdateCustomIconsInComboBox();
                
                // 保存设置
                MainWindow.SaveSettingsToFile();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
} 