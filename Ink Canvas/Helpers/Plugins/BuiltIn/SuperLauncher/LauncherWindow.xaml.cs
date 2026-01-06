using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Ink_Canvas.Helpers.Plugins.BuiltIn.SuperLauncher
{
    /// <summary>
    /// LauncherWindow.xaml 的交互逻辑（精简、安全版本）
    /// </summary>
    public partial class LauncherWindow : Window
    {
        private readonly SuperLauncherPlugin _plugin;
        private bool _isFixMode;
        private readonly Dictionary<Button, LauncherItem> _appButtons = new Dictionary<Button, LauncherItem>();
        private Button _draggingButton;
        private Point _dragStartPoint;
        private bool _isClosing;

        public LauncherWindow(SuperLauncherPlugin plugin)
        {
            InitializeComponent();
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
            Loaded += LauncherWindow_Loaded;
        }

        private void LauncherWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                LoadLauncherItems();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"加载启动项失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void LoadLauncherItems()
        {
            try
            {
                _appButtons.Clear();
                AppPanel.Children.Clear();

                var items = _plugin?.LauncherItems?.Where(i => i.IsVisible).OrderBy(i => i.Position).ToList();
                if (items == null) return;

                foreach (var item in items)
                {
                    var appButton = new Button { Content = item.Name, Width = 80, Height = 80, Margin = new Thickness(5) };
                    appButton.Click += AppButton_Click;
                    appButton.PreviewMouseDown += AppButton_PreviewMouseDown;
                    appButton.PreviewMouseMove += AppButton_PreviewMouseMove;
                    appButton.PreviewMouseUp += AppButton_PreviewMouseUp;
                    _appButtons.Add(appButton, item);
                    AppPanel.Children.Add(appButton);
                }

                AdjustWindowSize();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"LoadLauncherItems 错误: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void AdjustWindowSize()
        {
            try
            {
                const int appsPerRow = 4;
                int visibleCount = _appButtons.Count;
                int rowCount = (int)Math.Ceiling(visibleCount / (double)appsPerRow);
                Width = Math.Min(appsPerRow * 90 + 40, 400);
                Height = Math.Min(Math.Max(1, rowCount) * 90 + 60, 600);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"调整启动台窗口大小时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void AppButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_isFixMode) return;
                if (sender is Button button && _appButtons.TryGetValue(button, out LauncherItem item))
                {
                    string appPath = item.Path;
                    string appName = item.Name;
                    LogHelper.WriteLogToFile($"点击启动应用: {appName}, 路径: {appPath}");
                    _isClosing = true;

                    Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(200).ConfigureAwait(false);
                            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                try
                                {
                                    if (File.Exists(appPath) || !appPath.Contains(":\\"))
                                    {
                                        var psi = new ProcessStartInfo { FileName = appPath, UseShellExecute = true };
                                        Process.Start(psi);
                                        LogHelper.WriteLogToFile($"应用程序 {appName} 已启动");
                                    }
                                    else
                                    {
                                        LogHelper.WriteLogToFile($"应用路径不存在: {appPath}", LogHelper.LogType.Error);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogHelper.WriteLogToFile($"启动应用程序失败: {ex.Message}", LogHelper.LogType.Error);
                                }
                            }));
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLogToFile($"应用启动任务出错: {ex.Message}", LogHelper.LogType.Error);
                        }
                    });

                    try { Dispatcher.BeginInvoke(new Action(() => Close()), DispatcherPriority.Background); } catch { }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"应用按钮点击事件出错: {ex.Message}", LogHelper.LogType.Error);
                try { _isClosing = true; Close(); } catch { }
            }
        }

        private void AppButton_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!(sender is Button btn) || !_appButtons.ContainsKey(btn)) return;
            _dragStartPoint = e.GetPosition(AppPanel);
            _draggingButton = btn;
            btn.CaptureMouse();
            Panel.SetZIndex(btn, 1000);
            btn.Opacity = 0.7;
            e.Handled = true;
        }

        private void AppButton_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_draggingButton == null || e.LeftButton != MouseButtonState.Pressed) return;
            Point current = e.GetPosition(AppPanel);
            var diff = current - _dragStartPoint;
            if (Math.Abs(diff.X) < 5 && Math.Abs(diff.Y) < 5) return;
            // 简化：不做移动预览，只在释放时重排
            e.Handled = true;
        }

        private void AppButton_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (!_isFixMode || _draggingButton == null)
                {
                    if (_draggingButton != null)
                    {
                        _draggingButton.ReleaseMouseCapture();
                        _draggingButton.Opacity = 1;
                        Panel.SetZIndex(_draggingButton, 0);
                        _draggingButton = null;
                    }
                    return;
                }

                _draggingButton.ReleaseMouseCapture();
                Point releasePoint = e.GetPosition(AppPanel);
                int newPosition = CalculateGridPosition(releasePoint);
                LauncherItem currentItem = _appButtons[_draggingButton];
                ReorderItems(currentItem, newPosition);
                LoadLauncherItems();
                _plugin.SaveConfig();
                _draggingButton.Opacity = 1;
                Panel.SetZIndex(_draggingButton, 0);
                _draggingButton = null;
                e.Handled = true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"拖拽释放处理出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private int CalculateGridPosition(Point point)
        {
            int columnCount = 4;
            int columnWidth = 90;
            int rowHeight = 90;
            int column = (int)(point.X / columnWidth);
            int row = (int)(point.Y / rowHeight);
            column = Math.Max(0, Math.Min(column, columnCount - 1));
            row = Math.Max(0, row);
            return row * columnCount + column;
        }

        private void ReorderItems(LauncherItem item, int newPosition)
        {
            try
            {
                item.IsPositionFixed = true;
                if (item.Position == newPosition) return;
                var visibleItems = _plugin.LauncherItems.Where(i => i.IsVisible).OrderBy(i => i.Position).ToList();
                visibleItems.Remove(item);
                int insertIndex = Math.Max(0, Math.Min(newPosition, visibleItems.Count));
                visibleItems.Insert(insertIndex, item);
                for (int i = 0; i < visibleItems.Count; i++) visibleItems[i].Position = i;

                // 不直接赋值给属性（set 访问器不可访问），而是更新现有集合的内容
                var coll = _plugin.LauncherItems;
                coll.Clear();
                foreach (var it in visibleItems) coll.Add(it);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"重新排序应用项时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }
    }
}