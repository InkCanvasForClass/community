using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace Ink_Canvas
{
    /// <summary>
    /// AddPickNameBackgroundWindow.xaml 的交互逻辑
    /// </summary>
    public partial class AddPickNameBackgroundWindow : Window
    {
        private MainWindow mainWindow;
        private string selectedFilePath;
        public bool IsSuccess { get; private set; }

        public AddPickNameBackgroundWindow(MainWindow owner)
        {
            InitializeComponent();
            mainWindow = owner;
            IsSuccess = false;
            
            // 添加TextBox内容变化事件以检查是否可以保存
            BackgroundNameTextBox.TextChanged += (s, e) => ValidateSaveButton();
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "图像文件|*.png;*.jpg;*.jpeg;*.bmp;*.gif",
                Title = "选择一个背景图片"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                selectedFilePath = openFileDialog.FileName;
                BackgroundPathTextBox.Text = selectedFilePath;
                
                // 显示预览
                try
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(selectedFilePath);
                    bitmap.EndInit();
                    BackgroundPreviewImage.Source = bitmap;
                    NoImageText.Visibility = Visibility.Collapsed;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"无法加载图像: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                
                // 自动填充名称建议（文件名，不包括扩展名）
                string suggestedName = Path.GetFileNameWithoutExtension(selectedFilePath);
                BackgroundNameTextBox.Text = suggestedName;
                
                ValidateSaveButton();
            }
        }
        
        private void ValidateSaveButton()
        {
            SaveButton.IsEnabled = !string.IsNullOrWhiteSpace(BackgroundNameTextBox.Text) && !string.IsNullOrEmpty(selectedFilePath);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 创建pictures/picknamebackgrounds文件夹结构（如果不存在）
                string picturesFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pictures");
                string backgroundsFolder = Path.Combine(picturesFolder, "picknamebackgrounds");
                
                if (!Directory.Exists(picturesFolder))
                {
                    Directory.CreateDirectory(picturesFolder);
                }
                
                if (!Directory.Exists(backgroundsFolder))
                {
                    Directory.CreateDirectory(backgroundsFolder);
                }
                
                // 生成一个唯一的文件名（使用GUID）
                string extension = Path.GetExtension(selectedFilePath);
                string newFileName = $"{Guid.NewGuid()}{extension}";
                string destPath = Path.Combine(backgroundsFolder, newFileName);
                
                // 复制文件到pictures/picknamebackgrounds文件夹
                File.Copy(selectedFilePath, destPath);
                
                // 创建新的自定义背景对象
                var customBackground = new CustomPickNameBackground(BackgroundNameTextBox.Text, destPath);
                
                // 添加到主窗口的设置中
                MainWindow.Settings.RandSettings.CustomPickNameBackgrounds.Add(customBackground);
                
                // 更新ComboBox
                mainWindow.UpdatePickNameBackgroundsInComboBox();
                
                // 保存设置
                MainWindow.SaveSettingsToFile();
                
                IsSuccess = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存背景时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
} 