using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas
{
    /// <summary>
    /// ManagePickNameBackgroundsWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ManagePickNameBackgroundsWindow : Window
    {
        private MainWindow mainWindow;
        public ObservableCollection<CustomPickNameBackground> Backgrounds { get; set; }

        public ManagePickNameBackgroundsWindow(MainWindow owner)
        {
            InitializeComponent();
            mainWindow = owner;

            // 从主窗口的设置获取自定义背景列表
            Backgrounds = new ObservableCollection<CustomPickNameBackground>(MainWindow.Settings.RandSettings.CustomPickNameBackgrounds);
            BackgroundsListView.ItemsSource = Backgrounds;
        }

        private void SetAsCurrentButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is CustomPickNameBackground background)
            {
                // 找到背景在列表中的索引（加8，因为前8个是默认值）
                int index = Backgrounds.IndexOf(background) + 1; // 增加1因为索引0将是CanvasStrings.Canvas_Color_Default

                // 更新设置
                MainWindow.Settings.RandSettings.SelectedBackgroundIndex = index;

                // 更新UI
                mainWindow.UpdatePickNameBackgroundDisplay();

                // 保存设置
                MainWindow.SaveSettingsToFile();

                MessageBox.Show(string.Format(Properties.RandomStrings.Random_ManageBg_SetSuccessFormat, background.Name), Properties.RandomStrings.Random_ManageBg_SetSuccessTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void DeleteBackgroundButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is CustomPickNameBackground background)
            {
                if (MessageBox.Show(string.Format(Properties.RandomStrings.Random_ManageBg_DeleteConfirmFormat, background.Name), Properties.RandomStrings.Random_ManageBg_DeleteConfirmTitle, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    try
                    {
                        // 尝试删除文件
                        if (File.Exists(background.FilePath))
                        {
                            File.Delete(background.FilePath);
                        }

                        // 从列表中移除背景
                        Backgrounds.Remove(background);

                        // 更新主窗口的设置
                        MainWindow.Settings.RandSettings.CustomPickNameBackgrounds.Clear();
                        foreach (var bg in Backgrounds)
                        {
                            MainWindow.Settings.RandSettings.CustomPickNameBackgrounds.Add(bg);
                        }

                        // 如果当前选中的是被删除的背景，重置为默认背景
                        int selectedIndex = MainWindow.Settings.RandSettings.SelectedBackgroundIndex;
                        if (selectedIndex > 0 && selectedIndex - 1 >= MainWindow.Settings.RandSettings.CustomPickNameBackgrounds.Count)
                        {
                            MainWindow.Settings.RandSettings.SelectedBackgroundIndex = 0;
                            mainWindow.UpdatePickNameBackgroundDisplay();
                        }

                        // 更新ComboBox
                        mainWindow.UpdatePickNameBackgroundsInComboBox();

                        // 保存设置
                        MainWindow.SaveSettingsToFile();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(string.Format(Properties.RandomStrings.Random_ManageBg_DeleteFailedFormat, ex.Message), Properties.RandomStrings.Random_Error, MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}