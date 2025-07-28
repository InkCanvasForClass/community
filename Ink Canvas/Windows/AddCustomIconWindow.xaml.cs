using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace Ink_Canvas
{
    /// <summary>
    /// AddCustomIconWindow.xaml 的交互逻辑
    /// </summary>
    public partial class AddCustomIconWindow : Window
    {
        private MainWindow mainWindow;
        private string selectedFilePath;
        public bool IsSuccess { get; private set; }

        public AddCustomIconWindow(MainWindow owner)
        {
            InitializeComponent();
            mainWindow = owner;
            IsSuccess = false;
            
            // 添加TextBox内容变化事件以检查是否可以保存
            IconNameTextBox.TextChanged += (s, e) => ValidateSaveButton();
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "图像文件|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.ico",
                Title = "选择一个图标文件"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                selectedFilePath = openFileDialog.FileName;
                IconPathTextBox.Text = selectedFilePath;
                
                // 显示预览
                try
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(selectedFilePath);
                    bitmap.EndInit();
                    IconPreviewImage.Source = bitmap;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"无法加载图像: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                
                // 自动填充名称建议（文件名，不包括扩展名）
                string suggestedName = Path.GetFileNameWithoutExtension(selectedFilePath);
                IconNameTextBox.Text = suggestedName;
                
                ValidateSaveButton();
            }
        }
        
        private void ValidateSaveButton()
        {
            SaveButton.IsEnabled = !string.IsNullOrWhiteSpace(IconNameTextBox.Text) && !string.IsNullOrEmpty(selectedFilePath);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 创建pictures/icons文件夹结构（如果不存在）
                string picturesFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pictures");
                string iconsFolder = Path.Combine(picturesFolder, "icons");
                
                if (!Directory.Exists(picturesFolder))
                {
                    Directory.CreateDirectory(picturesFolder);
                }
                
                if (!Directory.Exists(iconsFolder))
                {
                    Directory.CreateDirectory(iconsFolder);
                }
                
                // 生成一个唯一的文件名（使用GUID）
                string extension = Path.GetExtension(selectedFilePath);
                string newFileName = $"{Guid.NewGuid()}{extension}";
                string destPath = Path.Combine(iconsFolder, newFileName);
                
                // 复制文件到pictures/icons文件夹
                File.Copy(selectedFilePath, destPath);
                
                // 创建新的自定义图标对象
                var customIcon = new CustomFloatingBarIcon(IconNameTextBox.Text, destPath);
                
                // 添加到主窗口的设置中
                MainWindow.Settings.Appearance.CustomFloatingBarImgs.Add(customIcon);
                
                // 更新ComboBox
                mainWindow.UpdateCustomIconsInComboBox();
                
                // 保存设置
                MainWindow.SaveSettingsToFile();
                
                IsSuccess = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存图标时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
} 