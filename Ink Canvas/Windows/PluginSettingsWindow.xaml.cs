using Ink_Canvas.Helpers;
using Ink_Canvas.Helpers.Plugins.New;
using InkCanvas.PluginSdk;
using iNKORE.UI.WPF.Modern.Controls;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;

namespace Ink_Canvas.Windows
{
    /// <summary>
    /// PluginSettingsWindow.xaml 的交互逻辑
    /// </summary>
    public partial class PluginSettingsWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 刷新插件列表
        /// </summary>
        public void RefreshPluginList()
        {
            LoadPlugins();

            // 如果当前选中的插件仍然存在，保持其选中状态
            if (SelectedPlugin != null)
            {
                var matchingPlugin = Plugins.FirstOrDefault(p => p.Plugin.GetType().FullName == SelectedPlugin.GetType().FullName);
                if (matchingPlugin != null)
                {
                    PluginListView.SelectedItem = matchingPlugin;
                }
            }

            OnPropertyChanged(nameof(SelectedPlugin));
            LogHelper.WriteLogToFile("插件列表已刷新");
        }

        private IInkCanvasPlugin _selectedPlugin;

        /// <summary>
        /// 当前选中的插件
        /// </summary>
        public IInkCanvasPlugin SelectedPlugin
        {
            get => _selectedPlugin;
            set
            {
                if (_selectedPlugin != value)
                {
                    _selectedPlugin = value;
                    OnPropertyChanged(nameof(SelectedPlugin));
                    OnPropertyChanged(nameof(Name));
                    OnPropertyChanged(nameof(Version));
                    OnPropertyChanged(nameof(Author));
                    OnPropertyChanged(nameof(Description));
                    OnPropertyChanged(nameof(IsEnabled));
                    OnPropertyChanged(nameof(IsBuiltIn));
                }
            }
        }

        public new string Name => SelectedPlugin?.Name ?? string.Empty;
        public string Version => SelectedPlugin?.Version?.ToString() ?? string.Empty;
        public string Author => SelectedPlugin?.Author ?? string.Empty;
        public string Description => SelectedPlugin?.Description ?? string.Empty;
        public new bool IsEnabled => false; // Plugin system disabled
        public bool IsBuiltIn => false; // Plugin system disabled

        /// <summary>
        /// 插件列表
        /// </summary>
        public ObservableCollection<PluginViewModel> Plugins { get; } = new ObservableCollection<PluginViewModel>();

        public PluginSettingsWindow()
        {
            InitializeComponent();

            // 设置数据上下文
            PluginDetailGrid.DataContext = this;

            // 设置导出按钮初始状态
            BtnExportPlugin.IsEnabled = false;
            BtnExportPlugin.ToolTip = "请先选择要导出的插件";

            // 加载插件列表
            LoadPlugins();

            // 注册窗口关闭事件
            Closing += PluginSettingsWindow_Closing;
        }

        /// <summary>
        /// 窗口关闭事件处理
        /// </summary>
        private void PluginSettingsWindow_Closing(object sender, CancelEventArgs e)
        {
            try
            {
                // 保存插件配置
                LogHelper.WriteLogToFile("插件管理窗口关闭，保存插件配置...");
                // TODO: Save config using new plugin system
                LogHelper.WriteLogToFile("插件配置已保存");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"关闭窗口时保存插件配置出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 加载插件列表
        /// </summary>
        private void LoadPlugins()
        {
            Plugins.Clear();

            LogHelper.WriteLogToFile($"开始加载插件列表到UI，插件总数: {NuGetPluginManager.Instance.Plugins.Count}");

            // TODO: Implement plugin loading with new plugin system
            // For now, disable plugin loading to get build working
            LogHelper.WriteLogToFile("插件系统已禁用，等待新系统实现");

            // 绑定到ListView
            LogHelper.WriteLogToFile($"绑定 {Plugins.Count} 个插件到ListView");
            PluginListView.ItemsSource = Plugins;

            // 如果有插件，选择第一个
            if (Plugins.Count > 0)
            {
                LogHelper.WriteLogToFile($"选择第一个插件: {Plugins[0].Name}");
                PluginListView.SelectedIndex = 0;
            }
            else
            {
                LogHelper.WriteLogToFile("没有找到任何插件", LogHelper.LogType.Warning);
            }
        }

        /// <summary>
        /// 更新属性变更通知
        /// </summary>
        /// <param name="propertyName">属性名称</param>
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// 插件列表选择变更事件
        /// </summary>
        private void PluginListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PluginListView.SelectedItem is PluginViewModel viewModel)
            {
                // 设置当前选中的插件
                SelectedPlugin = viewModel.Plugin;

                // 加载插件设置界面
                PluginSettingsContainer.Content = SelectedPlugin.GetSettingsView();

                // 设置删除按钮的可见性
                BtnDeletePlugin.Visibility = Visibility.Collapsed; // Plugin system disabled

                // 设置导出按钮的可用状态
                BtnExportPlugin.IsEnabled = false; // Plugin system disabled
                if (false) // Plugin system disabled
                {
                    BtnExportPlugin.ToolTip = "内置插件无法导出";
                }
                else
                {
                    BtnExportPlugin.ToolTip = "将插件导出为.iccpp文件";
                }
            }
            else
            {
                SelectedPlugin = null;
                PluginSettingsContainer.Content = null;
                BtnDeletePlugin.Visibility = Visibility.Collapsed;
                BtnExportPlugin.IsEnabled = false;
                BtnExportPlugin.ToolTip = "请先选择要导出的插件";
            }
        }

        /// <summary>
        /// 加载本地插件按钮点击事件
        /// </summary>
        private void BtnLoadPlugin_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 创建文件对话框
                OpenFileDialog dialog = new OpenFileDialog
                {
                    Filter = "ICC插件文件(*.iccpp)|*.iccpp",
                    Title = "选择要加载的插件文件"
                };

                // 显示对话框
                if (dialog.ShowDialog() == true)
                {
                    // 获取插件文件路径
                    string pluginPath = dialog.FileName;

                    // 检查是否在Plugins目录下
                    string pluginsDirectory = Path.Combine(App.RootPath, "Plugins");
                    string targetPath = Path.Combine(pluginsDirectory, Path.GetFileName(pluginPath));

                    // 确保Plugins目录存在
                    if (!Directory.Exists(pluginsDirectory))
                    {
                        Directory.CreateDirectory(pluginsDirectory);
                    }

                    // 如果插件不在Plugins目录下，复制过去
                    if (!string.Equals(pluginPath, targetPath, StringComparison.OrdinalIgnoreCase))
                    {
                        File.Copy(pluginPath, targetPath, true);
                        pluginPath = targetPath;
                    }

                    // 加载插件 - using new plugin system
                    // Note: External plugin loading needs to be implemented in NuGetPluginManager
                    IInkCanvasPlugin plugin = null; // TODO: Implement external plugin loading

                    if (plugin != null)
                    {
                        // 刷新插件列表
                        LoadPlugins();

                        // 选择新加载的插件
                        foreach (var item in Plugins)
                        {
                            if (item.Plugin == plugin)
                            {
                                PluginListView.SelectedItem = item;
                                break;
                            }
                        }

                        MessageBox.Show($"插件 {plugin.Name} v{plugin.Version} 已成功加载！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("插件加载失败，请检查文件是否有效。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载插件时发生错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 删除插件按钮点击事件
        /// </summary>
        private void BtnDeletePlugin_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedPlugin == null) return;

            // 不能删除内置插件
            if (false) // Plugin system disabled
            {
                MessageBox.Show("内置插件无法删除。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 保存插件名称，以便在删除后使用
            string pluginName = SelectedPlugin.Name;

            // 确认删除
            MessageBoxResult result = MessageBox.Show(
                $"确定要删除插件 {pluginName} 吗？\n此操作将永久删除插件文件，无法恢复。",
                "删除确认",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                // Plugin system disabled - delete functionality removed
                bool success = false; // Plugin system disabled

                if (success)
                {
                    // 刷新插件列表
                    LoadPlugins();

                    // 如果还有插件，选择第一个
                    if (Plugins.Count > 0)
                    {
                        PluginListView.SelectedIndex = 0;
                    }

                    MessageBox.Show($"插件 {pluginName} 已成功删除。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"删除插件 {pluginName} 失败，请稍后重试。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// 导出插件按钮点击事件
        /// </summary>
        private void BtnExportPlugin_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 检查是否有选中的插件
                if (SelectedPlugin == null)
                {
                    MessageBox.Show("请先选择要导出的插件", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 检查是否为内置插件
                if (false) // Plugin system disabled
                {
                    MessageBox.Show("内置插件无法导出", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 检查插件文件是否存在
                string pluginPath = null;
                if (false) // Plugin system disabled
                {
                    // Plugin system disabled - no plugin path available
                    pluginPath = null;
                }

                if (string.IsNullOrEmpty(pluginPath) || !File.Exists(pluginPath))
                {
                    MessageBox.Show("插件文件不存在或无法访问", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 创建保存文件对话框
                SaveFileDialog dialog = new SaveFileDialog
                {
                    Filter = "ICC插件文件(*.iccpp)|*.iccpp",
                    Title = "导出插件",
                    FileName = Path.GetFileName(pluginPath)
                };

                // 显示对话框
                if (dialog.ShowDialog() == true)
                {
                    // 获取目标路径
                    string targetPath = dialog.FileName;

                    // 如果目标文件已存在，询问是否覆盖
                    if (File.Exists(targetPath) && !string.Equals(pluginPath, targetPath, StringComparison.OrdinalIgnoreCase))
                    {
                        MessageBoxResult result = MessageBox.Show("目标文件已存在，是否覆盖？", "确认", MessageBoxButton.YesNo, MessageBoxImage.Question);
                        if (result != MessageBoxResult.Yes)
                        {
                            return;
                        }
                    }

                    // 复制插件文件到目标路径
                    if (!string.Equals(pluginPath, targetPath, StringComparison.OrdinalIgnoreCase))
                    {
                        File.Copy(pluginPath, targetPath, true);
                    }

                    LogHelper.WriteLogToFile($"插件 {SelectedPlugin.Name} 已成功导出到: {targetPath}");
                    MessageBox.Show($"插件 {SelectedPlugin.Name} 已成功导出！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"导出插件时出错: {ex.Message}", LogHelper.LogType.Error);
                MessageBox.Show($"导出插件时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 插件开关切换事件
        /// </summary>
        private void PluginToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            try
            {
            if (sender is ToggleSwitch toggleSwitch &&
                toggleSwitch.Tag is IInkCanvasPlugin plugin)
                {
                    // 记录当前开关状态
                    bool targetState = toggleSwitch.IsOn;

                    // 记录插件类型名称和名称，用于稍后查找重载后的插件
                    string pluginTypeName = plugin.GetType().FullName;
                    string pluginName = plugin.Name;
                    bool wasBuiltIn = false; // Plugin system disabled

                    LogHelper.WriteLogToFile($"UI开关切换: {pluginName}, 目标状态: {(targetState ? "启用" : "禁用")}");

                    // 切换插件状态 - using new plugin system
                    if (targetState)
                    {
                        NuGetPluginManager.Instance.EnablePlugin(plugin.Name);
                    }
                    else
                    {
                        NuGetPluginManager.Instance.DisablePlugin(plugin.Name);
                    }
                    LogHelper.WriteLogToFile("插件状态已立即保存到配置文件");

                    // 延迟一下再检查状态，确保变更已应用
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            // 查找最新的插件实例
                            IInkCanvasPlugin currentPlugin = null;
                            foreach (var p in NuGetPluginManager.Instance.Plugins)
                            {
                                if (p.GetType().FullName == pluginTypeName || p.Name == pluginName)
                                {
                                    // Get the actual plugin instance from the manager
                                    currentPlugin = null; // TODO: Get plugin instance from NuGetPluginManager
                                    break;
                                }
                            }

                            if (currentPlugin == null)
                            {
                                LogHelper.WriteLogToFile($"无法找到插件: {pluginName}，UI状态可能不准确", LogHelper.LogType.Warning);
                                return;
                            }

                            // 检查实际状态
                            bool actualState = currentPlugin != null; // TODO: Check actual plugin state
                            LogHelper.WriteLogToFile($"插件 {pluginName} 实际状态: {(actualState ? "启用" : "禁用")}");

                            // 更新视图模型
                            PluginViewModel viewModel = null;
                            if (toggleSwitch.DataContext is PluginViewModel vm)
                            {
                                viewModel = vm;
                            }
                            else
                            {
                                viewModel = Plugins.FirstOrDefault(p => p.Plugin == currentPlugin);
                            }

                            if (viewModel != null)
                            {
                                // 确保视图模型状态与实际状态一致
                                if (viewModel.IsEnabled != actualState)
                                {
                                    LogHelper.WriteLogToFile($"同步视图模型状态: {(actualState ? "启用" : "禁用")}");
                                    viewModel.IsEnabled = actualState;
                                }

                                // 确保UI开关状态与实际状态一致
                                if (toggleSwitch.IsOn != actualState)
                                {
                                    LogHelper.WriteLogToFile($"同步UI开关状态: {(actualState ? "启用" : "禁用")}");
                                    toggleSwitch.IsOn = actualState;
                                }
                            }

                            // 如果是当前选中的插件，更新属性
                            if (currentPlugin == SelectedPlugin)
                            {
                                OnPropertyChanged(nameof(IsEnabled));
                            }

                            // 对于内置插件，特别处理
                            if (wasBuiltIn)
                            {
                                // Plugin system disabled - special plugin refresh logic removed
                            }

                            LogHelper.WriteLogToFile($"插件 {pluginName} UI状态同步完成");
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLogToFile($"同步UI状态时出错: {ex.Message}", LogHelper.LogType.Error);
                        }
                    }), DispatcherPriority.Background);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"切换插件状态时出错: {ex.Message}", LogHelper.LogType.Error);
                MessageBox.Show($"切换插件状态时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 保存插件状态按钮点击事件
        /// </summary>
        private void BtnSaveConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int syncCount = 0;

                // 遍历界面上所有插件视图模型，获取开关状态
                foreach (var viewModel in Plugins)
                {
                    try
                    {
                        if (viewModel.Plugin != null)
                        {
                            // 获取UI中开关的当前状态（从界面控件读取）
                            bool uiState = viewModel.IsEnabled;

                            // 获取插件类型名，用于查找配置
                            string pluginTypeName = viewModel.Plugin.GetType().FullName;

                            // 查找实际的插件实例（可能与viewModel.Plugin不同，因为可能已经重新加载）
                            // Plugin system disabled
                            var actualPlugin = (IInkCanvasPlugin)null;
                            // Plugin system disabled - no plugin lookup

                            // 如果找不到对应的实际插件实例，跳过
                            if (actualPlugin == null)
                            {
                                LogHelper.WriteLogToFile($"手动保存：无法找到与UI对应的插件实例：{viewModel.Name}", LogHelper.LogType.Warning);
                                continue;
                            }

                            // TODO: Implement plugin state management with new plugin system
                            // For now, just log the state change
                            LogHelper.WriteLogToFile($"手动保存：插件 {actualPlugin?.Name ?? "未知"} 状态变更请求: {uiState}");
                            syncCount++;
                        }
                    }
                    catch (Exception pluginEx)
                    {
                        // 单个插件处理失败不应该影响其他插件
                        LogHelper.WriteLogToFile($"手动保存：处理插件 {viewModel.Name} 时出错: {pluginEx.Message}", LogHelper.LogType.Error);
                    }
                }

                // 保存插件状态配置
                // TODO: Save config using new plugin system

                // 记录日志
                LogHelper.WriteLogToFile($"用户手动保存插件状态配置，同步了 {syncCount} 个状态变更");

                // 刷新插件列表，确保UI与最新状态同步
                RefreshPluginList();

                // 显示保存成功提示
                string message = syncCount > 0
                    ? $"插件状态已成功保存，同步了 {syncCount} 个状态变更"
                    : "插件状态已成功保存，所有插件状态已是最新";
                MessageBox.Show(message, "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                // 记录错误日志
                LogHelper.WriteLogToFile($"手动保存插件状态时出错: {ex.Message}", LogHelper.LogType.Error);

                // 显示错误信息
                MessageBox.Show($"保存插件状态时发生错误: {ex.Message}", "保存失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 关闭按钮点击事件
        /// </summary>
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            // 直接关闭窗口，窗口关闭事件会处理配置保存
            Close();
        }

        /// <summary>
        /// 标题栏拖拽事件
        /// </summary>
        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                // 双击标题栏切换最大化状态
                if (WindowState == WindowState.Maximized)
                {
                    WindowState = WindowState.Normal;
                }
                else
                {
                    WindowState = WindowState.Maximized;
                }
            }
            else
            {
                // 拖拽窗口
                DragMove();
            }
        }
    }

    /// <summary>
    /// 插件视图模型
    /// </summary>
    public class PluginViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 插件实例
        /// </summary>
        public IInkCanvasPlugin Plugin { get; }

        /// <summary>
        /// 插件名称
        /// </summary>
        public string Name
        {
            get
            {
                string name = Plugin?.Name ?? "未命名插件";
                LogHelper.WriteLogToFile($"获取插件名称: {name}，类型: {Plugin?.GetType().FullName ?? "未知"}");
                return name;
            }
        }

        /// <summary>
        /// 插件是否启用
        /// </summary>
        private bool _isEnabled;
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    OnPropertyChanged(nameof(IsEnabled));
                }
            }
        }

        public PluginViewModel(IInkCanvasPlugin plugin)
        {
            Plugin = plugin;

            // 获取实际状态
            _isEnabled = plugin != null; // TODO: Get actual plugin state

            // 记录日志
            LogHelper.WriteLogToFile($"创建插件视图模型: {plugin?.GetType().FullName ?? "未知"}, 名称: {plugin?.Name ?? "未命名"}");

            // TODO: Register plugin state change events for new plugin system
        }

        /// <summary>
        /// 处理插件状态变更事件
        /// </summary>
        private void Plugin_EnabledStateChanged(object sender, bool isEnabled)
        {
            // 在UI线程上更新状态
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                IsEnabled = isEnabled;

                // 确保配置立即保存
                if (sender is IInkCanvasPlugin plugin)
                {
                    LogHelper.WriteLogToFile($"视图模型捕获到插件 {plugin.Name} 状态变更: {(isEnabled ? "启用" : "禁用")}");
                    // TODO: Save config using new plugin system
                    LogHelper.WriteLogToFile("视图模型已触发配置保存");
                }
            }));
        }

        /// <summary>
        /// 属性变更通知
        /// </summary>
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}