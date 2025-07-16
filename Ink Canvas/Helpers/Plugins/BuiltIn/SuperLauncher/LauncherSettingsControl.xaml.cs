using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Ink_Canvas.Windows;

namespace Ink_Canvas.Helpers.Plugins.BuiltIn.SuperLauncher
{
    /// <summary>
    /// LauncherSettingsControl.xaml 的交互逻辑
    /// </summary>
    public partial class LauncherSettingsControl : UserControl
    {
        /// <summary>
        /// 父插件
        /// </summary>
        private readonly SuperLauncherPlugin _plugin;
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="plugin">父插件</param>
        public LauncherSettingsControl(SuperLauncherPlugin plugin)
        {
            InitializeComponent();
            
            _plugin = plugin;
            
            // 设置按钮位置
            RbtnLeft.IsChecked = _plugin.Config.ButtonPosition == LauncherButtonPosition.Left;
            RbtnRight.IsChecked = _plugin.Config.ButtonPosition == LauncherButtonPosition.Right;
            
            // 绑定应用列表
            DgApps.ItemsSource = _plugin.LauncherItems;
            
            // 初始化按钮状态
            UpdateButtonStates();
        }
        
        /// <summary>
        /// 更新按钮状态
        /// </summary>
        private void UpdateButtonStates()
        {
            bool hasSelection = DgApps.SelectedItem != null;
            BtnEdit.IsEnabled = hasSelection;
            BtnDelete.IsEnabled = hasSelection;
        }
        
        /// <summary>
        /// 位置单选按钮选择事件
        /// </summary>
        private void RbtnPosition_Checked(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            
            LauncherButtonPosition oldPosition = _plugin.Config.ButtonPosition;
            
            if (sender == RbtnLeft)
            {
                _plugin.Config.ButtonPosition = LauncherButtonPosition.Left;
            }
            else if (sender == RbtnRight)
            {
                _plugin.Config.ButtonPosition = LauncherButtonPosition.Right;
            }
            
            // 如果位置发生变化，更新按钮位置
            if (oldPosition != _plugin.Config.ButtonPosition)
            {
                try
                {
                    // 更新按钮位置
                    _plugin.UpdateButtonPosition();
                    
                    // 保存配置
                    _plugin.SaveConfig();
                    
                    LogHelper.WriteLogToFile($"启动台按钮位置已更改为: {_plugin.Config.ButtonPosition}", LogHelper.LogType.Info);
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"更新启动台按钮位置时出错: {ex.Message}", LogHelper.LogType.Error);
                    MessageBox.Show($"更新启动台按钮位置时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        
        /// <summary>
        /// 添加应用按钮点击事件
        /// </summary>
        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 弹出文件选择对话框
                OpenFileDialog dialog = new OpenFileDialog
                {
                    Title = "选择应用程序",
                    Filter = "应用程序 (*.exe)|*.exe|所有文件 (*.*)|*.*",
                    Multiselect = false
                };
                
                if (dialog.ShowDialog() == true)
                {
                    // 创建新的启动项
                    LauncherItem item = new LauncherItem
                    {
                        Name = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName),
                        Path = dialog.FileName,
                        IsVisible = true,
                        Position = -1 // 让插件管理器分配位置
                    };
                    
                    // 显示编辑对话框
                    EditLauncherItem(item, true);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"添加启动项时出错: {ex.Message}", LogHelper.LogType.Error);
                MessageBox.Show($"添加启动项时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 编辑应用按钮点击事件
        /// </summary>
        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (DgApps.SelectedItem is LauncherItem item)
            {
                EditLauncherItem(item, false);
            }
        }
        
        /// <summary>
        /// 删除应用按钮点击事件
        /// </summary>
        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (DgApps.SelectedItem is LauncherItem item)
            {
                // 确认删除
                MessageBoxResult result = MessageBox.Show(
                    $"确定要删除 {item.Name} 吗？", 
                    "删除确认", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    // 从集合中移除
                    _plugin.LauncherItems.Remove(item);
                    
                    // 保存配置
                    _plugin.SaveConfig();
                }
            }
        }
        
        /// <summary>
        /// 保存设置按钮点击事件
        /// </summary>
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 保存配置
                _plugin.SaveConfig();
                
                // 如果插件已启用，重新加载启动台按钮
                if (_plugin.IsEnabled)
                {
                    _plugin.Disable();
                    _plugin.Enable();
                }
                else
                {
                    // 如果插件未启用，则启用它
                    _plugin.Enable();
                    
                    // 通知PluginSettingsWindow刷新插件列表
                    var window = Window.GetWindow(this);
                    if (window is PluginSettingsWindow pluginSettingsWindow)
                    {
                        // 触发刷新
                        pluginSettingsWindow.RefreshPluginList();
                    }
                }
                
                MessageBox.Show("设置已保存并应用！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"保存设置时出错: {ex.Message}", LogHelper.LogType.Error);
                MessageBox.Show($"保存设置时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 应用项选择变更事件
        /// </summary>
        private void DgApps_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateButtonStates();
        }
        
        /// <summary>
        /// 编辑启动项
        /// </summary>
        /// <param name="item">启动项</param>
        /// <param name="isNew">是否为新建</param>
        private void EditLauncherItem(LauncherItem item, bool isNew)
        {
            // 创建简单的编辑窗口
            Window editWindow = new Window
            {
                Title = isNew ? "添加应用" : "编辑应用",
                Width = 400,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize
            };
            
            // 创建编辑表单
            Grid grid = new Grid
            {
                Margin = new Thickness(20)
            };
            
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            
            // 名称输入框
            TextBlock nameLabel = new TextBlock 
            { 
                Text = "名称:", 
                VerticalAlignment = VerticalAlignment.Center 
            };
            TextBox nameTextBox = new TextBox 
            { 
                Text = item.Name, 
                Margin = new Thickness(0, 5, 0, 5) 
            };
            
            Grid.SetRow(nameLabel, 0);
            Grid.SetColumn(nameLabel, 0);
            Grid.SetRow(nameTextBox, 0);
            Grid.SetColumn(nameTextBox, 1);
            
            grid.Children.Add(nameLabel);
            grid.Children.Add(nameTextBox);
            
            // 路径输入框
            TextBlock pathLabel = new TextBlock 
            { 
                Text = "路径:", 
                VerticalAlignment = VerticalAlignment.Center 
            };
            Grid pathGrid = new Grid();
            pathGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            pathGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength() });
            
            TextBox pathTextBox = new TextBox 
            { 
                Text = item.Path, 
                Margin = new Thickness(0, 5, 5, 5) 
            };
            Button browseButton = new Button 
            { 
                Content = "浏览", 
                Padding = new Thickness(5, 0, 5, 0),
                Margin = new Thickness(0, 5, 0, 5)
            };
            
            browseButton.Click += (s, e) => 
            {
                OpenFileDialog dialog = new OpenFileDialog
                {
                    Title = "选择应用程序",
                    Filter = "应用程序 (*.exe)|*.exe|所有文件 (*.*)|*.*",
                    Multiselect = false,
                    FileName = pathTextBox.Text
                };
                
                if (dialog.ShowDialog() == true)
                {
                    pathTextBox.Text = dialog.FileName;
                }
            };
            
            Grid.SetColumn(pathTextBox, 0);
            Grid.SetColumn(browseButton, 1);
            pathGrid.Children.Add(pathTextBox);
            pathGrid.Children.Add(browseButton);
            
            Grid.SetRow(pathLabel, 1);
            Grid.SetColumn(pathLabel, 0);
            Grid.SetRow(pathGrid, 1);
            Grid.SetColumn(pathGrid, 1);
            
            grid.Children.Add(pathLabel);
            grid.Children.Add(pathGrid);
            
            // 确认和取消按钮
            StackPanel buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };
            
            Button okButton = new Button
            {
                Content = "确定",
                Padding = new Thickness(15, 5, 15, 5),
                Margin = new Thickness(0, 0, 10, 0),
                IsDefault = true
            };
            
            Button cancelButton = new Button
            {
                Content = "取消",
                Padding = new Thickness(15, 5, 15, 5),
                IsCancel = true
            };
            
            okButton.Click += (s, e) => 
            {
                // 验证输入
                if (string.IsNullOrWhiteSpace(nameTextBox.Text))
                {
                    MessageBox.Show("请输入应用名称！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                if (string.IsNullOrWhiteSpace(pathTextBox.Text))
                {
                    MessageBox.Show("请输入应用路径！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // 更新项目
                item.Name = nameTextBox.Text;
                item.Path = pathTextBox.Text;
                
                // 如果是新建，添加到集合
                if (isNew)
                {
                    _plugin.AddLauncherItem(item);
                }
                else
                {
                    // 触发属性变更通知，刷新DataGrid
                    if (DgApps.ItemsSource is ICollectionView view)
                    {
                        view.Refresh();
                    }
                    
                    // 保存配置
                    _plugin.SaveConfig();
                }
                
                editWindow.DialogResult = true;
                editWindow.Close();
            };
            
            cancelButton.Click += (s, e) => 
            {
                editWindow.DialogResult = false;
                editWindow.Close();
            };
            
            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            
            Grid.SetRow(buttonPanel, 2);
            Grid.SetColumnSpan(buttonPanel, 2);
            
            grid.Children.Add(buttonPanel);
            
            // 设置窗口内容
            editWindow.Content = grid;
            
            // 显示窗口
            editWindow.ShowDialog();
        }
    }
} 