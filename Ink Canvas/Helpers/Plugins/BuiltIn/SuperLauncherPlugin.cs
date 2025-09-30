using Ink_Canvas.Helpers.Plugins.BuiltIn.SuperLauncher;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Ink_Canvas.Helpers.Plugins.BuiltIn
{
    /// <summary>
    /// 超级启动台插件
    /// </summary>
    public class SuperLauncherPlugin : PluginBase
    {
        #region 插件基本信息

        public override string Name => "超级启动台";

        public override string Description => "在浮动栏添加一个启动台按钮，可快速启动常用应用程序。";

        public override Version Version => new Version(1, 0, 1);

        public override string Author => "ICC CE 团队";

        public override bool IsBuiltIn => true;

        #endregion

        #region 插件属性和字段

        /// <summary>
        /// 启动台配置
        /// </summary>
        public LauncherConfig Config { get; private set; }

        /// <summary>
        /// 启动台应用程序列表
        /// </summary>
        public ObservableCollection<LauncherItem> LauncherItems { get; private set; }

        /// <summary>
        /// 启动台按钮
        /// </summary>
        private LauncherButton _launcherButton;

        /// <summary>
        /// 启动台窗口
        /// </summary>
        private LauncherWindow _launcherWindow;

        /// <summary>
        /// 配置文件路径
        /// </summary>
        private readonly string _configPath = Path.Combine(App.RootPath, "PluginConfigs", "SuperLauncher.json");

        /// <summary>
        /// 标记是否已添加到浮动栏
        /// </summary>
        private bool _isAddedToFloatingBar;

        #endregion

        #region 插件生命周期

        public override void Initialize()
        {
            try
            {
                base.Initialize();

                // 创建配置目录
                string configDir = Path.Combine(App.RootPath, "PluginConfigs");
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }

                // 加载配置
                LoadConfig();

                LogHelper.WriteLogToFile("超级启动台插件已初始化");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"初始化超级启动台插件时出错: {ex.Message}", LogHelper.LogType.Error);
                LogHelper.NewLog(ex);
            }
        }

        public override void Enable()
        {
            try
            {
                if (IsEnabled) return; // 防止重复启用

                // 创建启动台按钮
                if (_launcherButton == null)
                {
                    _launcherButton = new LauncherButton(this);
                    LogHelper.WriteLogToFile("超级启动台按钮已创建");
                }

                // 添加启动台按钮到浮动栏
                AddLauncherButtonToFloatingBar();

                // 设置启用状态
                base.Enable();

                // 保存插件配置
                SavePluginSettings();

                LogHelper.WriteLogToFile("超级启动台插件已启用");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"启用超级启动台插件时出错: {ex.Message}", LogHelper.LogType.Error);
                LogHelper.NewLog(ex);
            }
        }

        public override void Disable()
        {
            try
            {
                if (!IsEnabled) return; // 防止重复禁用

                // 从浮动栏移除启动台按钮
                RemoveLauncherButtonFromFloatingBar();

                // 如果启动台窗口打开，则关闭
                if (_launcherWindow != null && _launcherWindow.IsVisible)
                {
                    _launcherWindow.Close();
                    _launcherWindow = null;
                }

                // 设置禁用状态
                base.Disable();

                // 保存插件配置
                SavePluginSettings();

                LogHelper.WriteLogToFile("超级启动台插件已禁用");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"禁用超级启动台插件时出错: {ex.Message}", LogHelper.LogType.Error);
                LogHelper.NewLog(ex);
            }
        }

        public override UserControl GetSettingsView()
        {
            return new LauncherSettingsControl(this);
        }

        public override void Cleanup()
        {
            // 保存配置
            SaveConfig();

            // 从浮动栏移除启动台按钮
            RemoveLauncherButtonFromFloatingBar();

            // 如果启动台窗口打开，则关闭
            if (_launcherWindow != null && _launcherWindow.IsVisible)
            {
                _launcherWindow.Close();
                _launcherWindow = null;
            }

            base.Cleanup();
        }

        /// <summary>
        /// 保存插件设置
        /// </summary>
        public override void SavePluginSettings()
        {
            try
            {
                // 确保配置已加载
                if (Config == null)
                {
                    LoadConfig();
                }

                // 更新其他设置，但不更改插件启用状态

                // 保存配置
                SaveConfig();

                LogHelper.WriteLogToFile("超级启动台插件设置已保存");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"保存超级启动台插件设置时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        #endregion

        #region 配置管理

        /// <summary>
        /// 加载配置
        /// </summary>
        private void LoadConfig()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    string json = File.ReadAllText(_configPath);
                    Config = JsonConvert.DeserializeObject<LauncherConfig>(json) ?? CreateDefaultConfig();
                    LauncherItems = new ObservableCollection<LauncherItem>(Config.Items ?? new List<LauncherItem>());

                    // 注意：不再根据配置更改插件启用状态
                    // 插件状态由PluginManager统一管理
                }
                else
                {
                    Config = CreateDefaultConfig();
                    LauncherItems = new ObservableCollection<LauncherItem>(Config.Items);
                    SaveConfig();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"加载超级启动台配置时出错: {ex.Message}", LogHelper.LogType.Error);
                Config = CreateDefaultConfig();
                LauncherItems = new ObservableCollection<LauncherItem>(Config.Items);
            }
        }

        /// <summary>
        /// 保存配置
        /// </summary>
        public void SaveConfig()
        {
            try
            {
                // 同步LauncherItems到Config
                Config.Items = new List<LauncherItem>(LauncherItems);

                // 序列化并保存配置
                string json = JsonConvert.SerializeObject(Config, Formatting.Indented);
                File.WriteAllText(_configPath, json);

                LogHelper.WriteLogToFile("超级启动台配置已保存");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"保存超级启动台配置时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 创建默认配置
        /// </summary>
        private LauncherConfig CreateDefaultConfig()
        {
            var config = new LauncherConfig
            {
                ButtonPosition = LauncherButtonPosition.Right,
                // 不再使用IsEnabled，插件状态由PluginManager管理
                Items = new List<LauncherItem>
                {
                    new LauncherItem
                    {
                        Name = "资源管理器",
                        Path = @"C:\Windows\explorer.exe",
                        IsVisible = true,
                        Position = 0
                    }
                }
            };

            return config;
        }

        #endregion

        #region 启动台按钮管理

        /// <summary>
        /// 将启动台按钮添加到浮动栏
        /// </summary>
        private void AddLauncherButtonToFloatingBar()
        {
            try
            {
                // 如果已经添加，先移除
                if (_isAddedToFloatingBar)
                {
                    RemoveLauncherButtonFromFloatingBar();
                    _isAddedToFloatingBar = false;
                }

                // 获取主窗口实例
                var mainWindow = Application.Current.MainWindow;
                if (mainWindow == null)
                {
                    LogHelper.WriteLogToFile("未找到主窗口实例，无法添加启动台按钮", LogHelper.LogType.Error);
                    return;
                }

                // 创建启动台按钮
                _launcherButton = new LauncherButton(this);
                var buttonElement = _launcherButton.Element;

                // 查找浮动栏
                var floatingBar = mainWindow.FindName("StackPanelFloatingBar") as Panel;
                if (floatingBar == null)
                {
                    // 如果直接查找失败，则尝试遍历可视树查找
                    Panel floatingBarPanelFromTree = null;
                    FindStackPanelFloatingBar(mainWindow, ref floatingBarPanelFromTree);
                    floatingBar = floatingBarPanelFromTree;
                }

                if (floatingBar == null)
                {
                    LogHelper.WriteLogToFile("未找到浮动栏，无法添加启动台按钮", LogHelper.LogType.Error);
                    return;
                }

                // 添加启动台按钮到浮动栏
                if (Config.ButtonPosition == LauncherButtonPosition.Left)
                {
                    floatingBar.Children.Insert(0, buttonElement);
                    LogHelper.WriteLogToFile("启动台按钮已添加到浮动栏左侧");
                }
                else
                {
                    floatingBar.Children.Add(buttonElement);
                    LogHelper.WriteLogToFile("启动台按钮已添加到浮动栏右侧");
                }

                _isAddedToFloatingBar = true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"添加启动台按钮到浮动栏时出错: {ex.Message}", LogHelper.LogType.Error);
                LogHelper.NewLog(ex);
            }
        }

        /// <summary>
        /// 递归查找StackPanelFloatingBar
        /// </summary>
        private void FindStackPanelFloatingBar(DependencyObject parent, ref Panel result)
        {
            if (parent == null || result != null) return;

            try
            {
                // 检查当前对象是否为我们要找的面板
                if (parent is Panel panel && panel.Name == "StackPanelFloatingBar")
                {
                    result = panel;
                    return;
                }

                // 获取子元素数量
                int childCount = VisualTreeHelper.GetChildrenCount(parent);

                // 遍历所有子元素
                for (int i = 0; i < childCount; i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                    FindStackPanelFloatingBar(child, ref result);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"查找StackPanelFloatingBar时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 从浮动栏移除启动台按钮
        /// </summary>
        private void RemoveLauncherButtonFromFloatingBar()
        {
            try
            {
                if (!_isAddedToFloatingBar || _launcherButton == null)
                {
                    return;
                }

                // 获取主窗口实例
                var mainWindow = Application.Current.MainWindow;
                if (mainWindow == null)
                {
                    LogHelper.WriteLogToFile("未找到主窗口实例，无法移除启动台按钮", LogHelper.LogType.Error);
                    return;
                }

                // 获取按钮元素
                var buttonElement = _launcherButton.Element;

                // 查找浮动栏
                var floatingBar = mainWindow.FindName("StackPanelFloatingBar") as Panel;
                if (floatingBar == null)
                {
                    // 如果直接查找失败，则尝试遍历可视树查找
                    Panel floatingBarPanelFromTree = null;
                    FindStackPanelFloatingBar(mainWindow, ref floatingBarPanelFromTree);
                    floatingBar = floatingBarPanelFromTree;
                }

                if (floatingBar == null)
                {
                    LogHelper.WriteLogToFile("未找到浮动栏，无法移除启动台按钮", LogHelper.LogType.Error);
                    return;
                }

                // 从浮动栏移除启动台按钮
                if (floatingBar.Children.Contains(buttonElement))
                {
                    floatingBar.Children.Remove(buttonElement);
                    LogHelper.WriteLogToFile("启动台按钮已从浮动栏移除");
                }

                _isAddedToFloatingBar = false;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"移除启动台按钮时出错: {ex.Message}", LogHelper.LogType.Error);
                LogHelper.NewLog(ex);
            }
        }

        /// <summary>
        /// 更新启动台按钮位置
        /// </summary>
        public void UpdateButtonPosition()
        {
            try
            {
                // 如果按钮已添加到浮动栏，重新添加以更新位置
                if (_isAddedToFloatingBar)
                {
                    RemoveLauncherButtonFromFloatingBar();
                    AddLauncherButtonToFloatingBar();
                    LogHelper.WriteLogToFile($"启动台按钮位置已更新为: {Config.ButtonPosition}");
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"更新启动台按钮位置时出错: {ex.Message}", LogHelper.LogType.Error);
                LogHelper.NewLog(ex);
            }
        }

        #endregion

        #region 启动台功能

        /// <summary>
        /// 显示启动台窗口
        /// </summary>
        /// <param name="buttonPosition">按钮在屏幕上的位置</param>
        public void ShowLauncherWindow(Point buttonPosition)
        {
            try
            {
                // 如果窗口已存在，关闭它
                if (_launcherWindow != null && _launcherWindow.IsVisible)
                {
                    _launcherWindow.Close();
                    _launcherWindow = null;
                    return;
                }

                // 创建新的启动台窗口
                _launcherWindow = new LauncherWindow(this);

                // 计算窗口位置，使其位于按钮上方
                PositionLauncherWindow(_launcherWindow, buttonPosition);

                // 显示窗口
                _launcherWindow.Show();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"显示启动台窗口时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 设置启动台窗口位置
        /// </summary>
        /// <param name="window">启动台窗口</param>
        /// <param name="buttonPosition">按钮在屏幕上的位置</param>
        private void PositionLauncherWindow(LauncherWindow window, Point buttonPosition)
        {
            // 确保窗口已加载
            if (window.ActualWidth == 0 || window.ActualHeight == 0)
            {
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;

                // 设置窗口加载完成后的位置
                window.Loaded += (s, e) =>
                {
                    // 窗口位于按钮上方居中
                    double left = buttonPosition.X - (window.ActualWidth / 2);
                    double top = buttonPosition.Y - window.ActualHeight - 10; // 在按钮上方留出一些间距

                    // 确保窗口在屏幕内
                    left = Math.Max(0, Math.Min(left, SystemParameters.WorkArea.Width - window.ActualWidth));
                    top = Math.Max(0, Math.Min(top, SystemParameters.WorkArea.Height - window.ActualHeight));

                    window.Left = left;
                    window.Top = top;
                };
            }
            else
            {
                // 窗口位于按钮上方居中
                double left = buttonPosition.X - (window.ActualWidth / 2);
                double top = buttonPosition.Y - window.ActualHeight - 10; // 在按钮上方留出一些间距

                // 确保窗口在屏幕内
                left = Math.Max(0, Math.Min(left, SystemParameters.WorkArea.Width - window.ActualWidth));
                top = Math.Max(0, Math.Min(top, SystemParameters.WorkArea.Height - window.ActualHeight));

                window.Left = left;
                window.Top = top;
            }
        }

        /// <summary>
        /// 添加应用到启动台
        /// </summary>
        /// <param name="item">启动台项</param>
        public void AddLauncherItem(LauncherItem item)
        {
            // 如果项目数量已达上限，则不添加
            if (LauncherItems.Count >= 40)
            {
                MessageBox.Show("启动台项目数量已达上限(40个)！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 寻找合适的位置
            if (item.Position < 0)
            {
                item.Position = FindNextAvailablePosition();
            }

            // 添加项目并保存配置
            LauncherItems.Add(item);
            SaveConfig();
        }

        /// <summary>
        /// 查找下一个可用位置
        /// </summary>
        private int FindNextAvailablePosition()
        {
            // 获取已使用的位置列表
            var usedPositions = new HashSet<int>();
            foreach (var item in LauncherItems)
            {
                usedPositions.Add(item.Position);
            }

            // 查找第一个可用位置
            for (int i = 0; i < 40; i++)
            {
                if (!usedPositions.Contains(i))
                {
                    return i;
                }
            }

            // 如果所有位置都已使用，则返回0
            return 0;
        }

        #endregion
    }
}