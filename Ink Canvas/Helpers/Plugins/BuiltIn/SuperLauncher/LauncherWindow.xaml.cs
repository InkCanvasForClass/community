using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Ink_Canvas.Helpers.Plugins.BuiltIn.SuperLauncher
{
    /// <summary>
    /// LauncherWindow.xaml 的交互逻辑
    /// </summary>
    public partial class LauncherWindow : Window
    {
        /// <summary>
        /// 父插件
        /// </summary>
        private readonly SuperLauncherPlugin _plugin;

        /// <summary>
        /// 是否处于固定模式
        /// </summary>
        private bool _isFixMode;

        /// <summary>
        /// 应用项按钮列表
        /// </summary>
        private readonly Dictionary<Button, LauncherItem> _appButtons = new Dictionary<Button, LauncherItem>();

        /// <summary>
        /// 拖拽中的按钮
        /// </summary>
        private Button _draggingButton;

        /// <summary>
        /// 拖拽开始位置
        /// </summary>
        private Point _dragStartPoint;

        /// <summary>
        /// 构造函数
        /// </summary>
        public LauncherWindow(SuperLauncherPlugin plugin)
        {
            InitializeComponent();

            _plugin = plugin;

            // 加载应用项
            LoadLauncherItems();

            // 添加鼠标按下事件（用于拖动窗口）
            MouseDown += (s, e) =>
            {
                if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed)
                {
                    DragMove();
                }
            };

            // 根据应用数量调整窗口大小
            AdjustWindowSize();
        }

        /// <summary>
        /// 加载启动台应用项
        /// </summary>
        private void LoadLauncherItems()
        {
            // 清空现有应用项
            AppPanel.Children.Clear();
            _appButtons.Clear();

            // 获取显示的应用项
            var visibleItems = _plugin.LauncherItems
                .Where(item => item.IsVisible)
                .OrderBy(item => item.Position)
                .ToList();

            foreach (var item in visibleItems)
            {
                // 创建应用按钮
                Button appButton = new Button
                {
                    Style = (Style)FindResource("LauncherItemStyle"),
                    DataContext = item,
                    Tag = item.Position
                };

                // 添加点击事件
                appButton.Click += AppButton_Click;

                // 在固定模式下，添加拖拽事件
                appButton.PreviewMouseDown += AppButton_PreviewMouseDown;
                appButton.PreviewMouseMove += AppButton_PreviewMouseMove;
                appButton.PreviewMouseUp += AppButton_PreviewMouseUp;

                // 记录按钮和项目的对应关系
                _appButtons.Add(appButton, item);

                // 添加到面板
                AppPanel.Children.Add(appButton);
            }
        }

        /// <summary>
        /// 根据应用数量调整窗口大小
        /// </summary>
        private void AdjustWindowSize()
        {
            try
            {
                // 每行最多显示4个应用
                const int appsPerRow = 4;

                // 计算行数
                int visibleCount = _appButtons.Count;
                int rowCount = (int)Math.Ceiling(visibleCount / (double)appsPerRow);

                // 设置窗口宽度（每个应用90像素宽 = 80 + 5*2）
                Width = Math.Min(appsPerRow * 90 + 40, 400); // 最大宽度400

                // 设置窗口高度（每个应用90像素高 = 80 + 5*2）
                Height = Math.Min(rowCount * 90 + 60, 600); // 最大高度600，标题栏40 + 边距20
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"调整启动台窗口大小时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 应用按钮点击事件
        /// </summary>
        private void AppButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_isFixMode) return; // 在固定模式下，不响应点击事件

                if (sender is Button button && _appButtons.TryGetValue(button, out LauncherItem item))
                {
                    // 获取应用路径和名称，用于后续启动
                    string appPath = item.Path;
                    string appName = item.Name;

                    LogHelper.WriteLogToFile($"点击启动应用: {appName}, 路径: {appPath}");

                    // 首先标记窗口正在关闭
                    IsClosing = true;

                    // 创建一个应用启动任务
                    var launchTask = new Task(() =>
                    {
                        try
                        {
                            // 等待一段时间，确保窗口关闭流程已经开始
                            Thread.Sleep(200);

                            // 使用UI线程启动应用
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                try
                                {
                                    // 检查应用路径是否存在
                                    if (File.Exists(appPath) || !appPath.Contains(":\\"))
                                    {
                                        // 创建进程启动信息
                                        var psi = new ProcessStartInfo
                                        {
                                            FileName = appPath,
                                            UseShellExecute = true,
                                        };

                                        // 启动应用程序
                                        var process = Process.Start(psi);
                                        LogHelper.WriteLogToFile($"应用程序 {appName} 已启动");
                                    }
                                    else
                                    {
                                        LogHelper.WriteLogToFile($"应用路径不存在: {appPath}", LogHelper.LogType.Error);
                                        MessageBox.Show($"找不到应用程序: {appPath}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogHelper.WriteLogToFile($"启动应用程序失败: {ex.Message}", LogHelper.LogType.Error);
                                    MessageBox.Show($"启动应用程序失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLogToFile($"应用启动任务出错: {ex.Message}", LogHelper.LogType.Error);
                        }
                    });

                    // 关闭窗口
                    try
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            try { Close(); } catch { }

                            // 启动应用程序任务
                            launchTask.Start();
                        }), DispatcherPriority.Background);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"关闭窗口或启动任务时出错: {ex.Message}", LogHelper.LogType.Error);
                        // 如果无法通过UI关闭窗口，直接启动任务
                        launchTask.Start();
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"应用按钮点击事件出错: {ex.Message}", LogHelper.LogType.Error);
                try { IsClosing = true; Close(); } catch { }
            }
        }

        #region 固定模式拖拽事件

        /// <summary>
        /// 应用按钮鼠标按下事件
        /// </summary>
        private void AppButton_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isFixMode) return;

            if (e.ChangedButton == MouseButton.Left && sender is Button button)
            {
                _draggingButton = button;
                _dragStartPoint = e.GetPosition(AppPanel);
                button.CaptureMouse();
                button.Opacity = 0.7;

                // 阻止事件冒泡，以避免触发按钮点击
                e.Handled = true;
            }
        }

        /// <summary>
        /// 应用按钮鼠标移动事件
        /// </summary>
        private void AppButton_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isFixMode || _draggingButton == null) return;

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point currentPosition = e.GetPosition(AppPanel);

                // 移动按钮
                System.Windows.Controls.Canvas.SetLeft(_draggingButton, currentPosition.X - _draggingButton.ActualWidth / 2);
                System.Windows.Controls.Canvas.SetTop(_draggingButton, currentPosition.Y - _draggingButton.ActualHeight / 2);

                // 将按钮移到最上层
                Panel.SetZIndex(_draggingButton, 100);

                // 阻止事件冒泡
                e.Handled = true;
            }
        }

        /// <summary>
        /// 应用按钮鼠标释放事件
        /// </summary>
        private void AppButton_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isFixMode || _draggingButton == null) return;

            // 释放鼠标捕获
            _draggingButton.ReleaseMouseCapture();

            // 计算新位置
            Point releasePoint = e.GetPosition(AppPanel);
            int newPosition = CalculateGridPosition(releasePoint);

            // 获取当前项目
            LauncherItem currentItem = _appButtons[_draggingButton];

            // 重新排序
            ReorderItems(currentItem, newPosition);

            // 重新加载应用项
            LoadLauncherItems();

            // 保存配置
            _plugin.SaveConfig();

            // 清除拖拽状态
            _draggingButton.Opacity = 1;
            Panel.SetZIndex(_draggingButton, 0);
            _draggingButton = null;

            // 阻止事件冒泡
            e.Handled = true;
        }

        /// <summary>
        /// 计算网格位置
        /// </summary>
        private int CalculateGridPosition(Point point)
        {
            // 计算行和列
            int columnCount = 4; // 每行最多4个应用
            int columnWidth = 90; // 应用宽度（包括边距）
            int rowHeight = 90; // 应用高度（包括边距）

            int column = (int)(point.X / columnWidth);
            int row = (int)(point.Y / rowHeight);

            // 确保在有效范围内
            column = Math.Max(0, Math.Min(column, columnCount - 1));
            row = Math.Max(0, row);

            // 计算位置索引
            return row * columnCount + column;
        }

        /// <summary>
        /// 重新排序应用项
        /// </summary>
        private void ReorderItems(LauncherItem item, int newPosition)
        {
            try
            {
                // 设置项目为固定位置
                item.IsPositionFixed = true;

                // 如果位置相同，无需调整
                if (item.Position == newPosition)
                {
                    return;
                }

                // 获取所有可见项目
                var visibleItems = _plugin.LauncherItems
                    .Where(i => i.IsVisible)
                    .OrderBy(i => i.Position)
                    .ToList();

                // 移除当前项目
                visibleItems.Remove(item);

                // 查找插入位置
                int insertIndex = 0;
                for (int i = 0; i < visibleItems.Count; i++)
                {
                    if (visibleItems[i].Position >= newPosition)
                    {
                        insertIndex = i;
                        break;
                    }
                    insertIndex = i + 1;
                }

                // 插入项目
                visibleItems.Insert(insertIndex, item);

                // 重新分配位置
                for (int i = 0; i < visibleItems.Count; i++)
                {
                    visibleItems[i].Position = i;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"重新排序应用项时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        #endregion

        #region 窗口事件处理

        /// <summary>
        /// 窗口失去焦点事件
        /// </summary>
        private void Window_Deactivated(object sender, EventArgs e)
        {
            try
            {
                // 只有在非固定模式、窗口已加载、未处于关闭状态且IsLoaded=true时关闭窗口
                if (!_isFixMode && IsLoaded && !IsClosing)
                {
                    // 标记为正在关闭
                    IsClosing = true;

                    // 使用Dispatcher.BeginInvoke而不是直接调用Close，避免冲突
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            // 再次检查窗口状态
                            if (IsLoaded && !IsClosing)
                            {
                                Close();
                            }
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLogToFile($"延迟关闭窗口时出错: {ex.Message}", LogHelper.LogType.Error);
                        }
                    }), DispatcherPriority.Background);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"窗口失去焦点关闭时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 窗口是否正在关闭
        /// </summary>
        private bool IsClosing { get; set; }

        /// <summary>
        /// 重写OnClosing方法，标记窗口正在关闭
        /// </summary>
        protected override void OnClosing(CancelEventArgs e)
        {
            IsClosing = true;
            base.OnClosing(e);
        }

        /// <summary>
        /// 关闭按钮点击事件
        /// </summary>
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// 固定模式按钮点击事件
        /// </summary>
        private void BtnFixMode_Click(object sender, RoutedEventArgs e)
        {
            // 切换固定模式
            _isFixMode = !_isFixMode;

            // 更新固定模式按钮图标颜色
            FixModeIcon.Fill = _isFixMode ? Brushes.Yellow : Brushes.White;

            // 显示提示
            if (_isFixMode)
            {
                MessageBox.Show("已进入固定模式，您可以拖动应用图标调整位置。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        #endregion
    }
}