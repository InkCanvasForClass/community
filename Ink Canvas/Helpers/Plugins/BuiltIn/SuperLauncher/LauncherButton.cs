using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using iNKORE.UI.WPF.Modern.Controls;

namespace Ink_Canvas.Helpers.Plugins.BuiltIn.SuperLauncher
{
    /// <summary>
    /// 启动台按钮控件
    /// </summary>
    public class LauncherButton
    {
        /// <summary>
        /// 父插件
        /// </summary>
        private readonly SuperLauncherPlugin _plugin;

        /// <summary>
        /// 实际按钮控件
        /// </summary>
        private readonly SimpleStackPanel _panel;

        /// <summary>
        /// 获取按钮UI元素
        /// </summary>
        public UIElement Element => _panel;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="plugin">父插件</param>
        public LauncherButton(SuperLauncherPlugin plugin)
        {
            try
            {
                _plugin = plugin;
                LogHelper.WriteLogToFile("开始创建启动台按钮");

                // 创建SimpleStackPanel
                _panel = new SimpleStackPanel
                {
                    Name = "Launcher_Icon",
                    Orientation = Orientation.Vertical,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Width = 28,
                    Margin = new Thickness(0, -2, 0, 0),
                    Background = Brushes.Transparent
                };

                LogHelper.WriteLogToFile("创建SimpleStackPanel完成");

                // 添加图标
                var image = CreateIconImage();
                _panel.Children.Add(image);

                // 添加文本
                TextBlock textBlock = new TextBlock
                {
                    Text = "启动台",
                    Foreground = Brushes.Black,
                    FontSize = 8,
                    Margin = new Thickness(0, 1, 0, 0),
                    TextAlignment = TextAlignment.Center
                };
                _panel.Children.Add(textBlock);

                // 设置鼠标事件
                _panel.MouseDown += Panel_MouseDown;
                _panel.MouseUp += Panel_MouseUp;
                _panel.MouseLeave += Panel_MouseLeave;

                // 右键菜单支持
                _panel.ContextMenu = CreateContextMenu();

                // 设置工具提示
                _panel.ToolTip = "启动台";

                LogHelper.WriteLogToFile("启动台按钮创建完成");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"创建启动台按钮时出错: {ex.Message}", LogHelper.LogType.Error);
                LogHelper.NewLog(ex);
            }
        }

        /// <summary>
        /// 创建右键菜单
        /// </summary>
        private ContextMenu CreateContextMenu()
        {
            try
            {
                // 创建菜单
                ContextMenu menu = new ContextMenu();

                // 创建位置切换菜单项
                MenuItem positionMenuItem = new MenuItem();
                positionMenuItem.Header = _plugin.Config.ButtonPosition == LauncherButtonPosition.Left ?
                    "移至右侧" : "移至左侧";
                positionMenuItem.Click += (s, e) =>
                {
                    // 切换位置
                    _plugin.Config.ButtonPosition = _plugin.Config.ButtonPosition == LauncherButtonPosition.Left ?
                        LauncherButtonPosition.Right : LauncherButtonPosition.Left;

                    // 更新按钮位置
                    _plugin.UpdateButtonPosition();

                    // 保存配置
                    _plugin.SaveConfig();

                    LogHelper.WriteLogToFile($"通过右键菜单切换启动台按钮位置为: {_plugin.Config.ButtonPosition}");
                };
                menu.Items.Add(positionMenuItem);

                // 添加设置菜单项
                MenuItem settingsMenuItem = new MenuItem();
                settingsMenuItem.Header = "打开设置";
                settingsMenuItem.Click += (s, e) =>
                {
                    // 打开插件设置窗口
                    var mainWindow = Application.Current.MainWindow;
                    if (mainWindow != null)
                    {
                        try
                        {
                            // 使用反射调用主窗口的ShowPluginSettings方法
                            var method = mainWindow.GetType().GetMethod("ShowPluginSettings");
                            if (method != null)
                            {
                                method.Invoke(mainWindow, null);
                                LogHelper.WriteLogToFile("已打开插件设置窗口");
                            }
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLogToFile($"打开插件设置窗口失败: {ex.Message}", LogHelper.LogType.Error);
                        }
                    }
                };
                menu.Items.Add(settingsMenuItem);

                return menu;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"创建右键菜单时出错: {ex.Message}", LogHelper.LogType.Error);
                return null;
            }
        }

        /// <summary>
        /// 获取实际的UI元素
        /// </summary>
        [Obsolete("使用Element属性代替")]
        public UIElement GetUIElement()
        {
            return _panel;
        }

        /// <summary>
        /// 创建图标图像
        /// </summary>
        private Image CreateIconImage()
        {
            try
            {
                // 创建图像
                Image image = new Image
                {
                    Height = 17,
                    Margin = new Thickness(0, 3, 0, 0)
                };

                // 设置位图缩放模式
                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);

                // 创建绘图图像
                DrawingImage drawingImage = new DrawingImage();
                DrawingGroup drawingGroup = new DrawingGroup();
                drawingGroup.ClipGeometry = Geometry.Parse("M0,0 V24 H24 V0 H0 Z");

                // 使用提供的应用网格图标
                GeometryDrawing geometryDrawing = new GeometryDrawing
                {
                    Brush = new SolidColorBrush(Color.FromRgb(0x1B, 0x1B, 0x1B)),
                    Geometry = Geometry.Parse("F0 M24,24z M0,0z M4.41721,4.29873C4.35178,4.29873,4.29873,4.35178,4.29873,4.41721L4.29873,9.15646C4.29873,9.22189,4.35178,9.27494,4.41721,9.27494L9.15646,9.27494C9.22189,9.27494,9.27494,9.22189,9.27494,9.15646L9.27494,4.41721C9.27494,4.35178,9.22189,4.29873,9.15646,4.29873L4.41721,4.29873z M2.64,4.41721C2.64,3.43569,3.43569,2.64,4.41721,2.64L9.15646,2.64C10.138,2.64,10.9337,3.43569,10.9337,4.41721L10.9337,9.15646C10.9337,10.138,10.138,10.9337,9.15646,10.9337L4.41721,10.9337C3.43569,10.9337,2.64,10.138,2.64,9.15646L2.64,4.41721z M14.8435,4.29873C14.7781,4.29873,14.7251,4.35178,14.7251,4.41721L14.7251,9.15646C14.7251,9.22189,14.7781,9.27494,14.8435,9.27494L19.5828,9.27494C19.6482,9.27494,19.7013,9.22189,19.7013,9.15646L19.7013,4.41721C19.7013,4.35178,19.6482,4.29873,19.5828,4.29873L14.8435,4.29873z M13.0663,4.41721C13.0663,3.43569,13.862,2.64,14.8435,2.64L19.5828,2.64C20.5643,2.64,21.36,3.43569,21.36,4.41721L21.36,9.15646C21.36,10.138,20.5643,10.9337,19.5828,10.9337L14.8435,10.9337C13.862,10.9337,13.0663,10.138,13.0663,9.15646L13.0663,4.41721z M14.8435,14.7251C14.7781,14.7251,14.7251,14.7781,14.7251,14.8435L14.7251,19.5828C14.7251,19.6482,14.7781,19.7013,14.8435,19.7013L19.5828,19.7013C19.6482,19.7013,19.7013,19.6482,19.7013,19.5828L19.7013,14.8435C19.7013,14.7781,19.6482,14.7251,19.5828,14.7251L14.8435,14.7251z M13.0663,14.8435C13.0663,13.862,13.862,13.0663,14.8435,13.0663L19.5828,13.0663C20.5643,13.0663,21.36,13.862,21.36,14.8435L21.36,19.5828C21.36,20.5643,20.5643,21.36,19.5828,21.36L14.8435,21.36C13.862,21.36,13.0663,20.5643,13.0663,19.5828L13.0663,14.8435z M4.41721,14.7251C4.35178,14.7251,4.29873,14.7781,4.29873,14.8435L4.29873,19.5828C4.29873,19.6482,4.35178,19.7013,4.41721,19.7013L9.15646,19.7013C9.22189,19.7013,9.27494,19.6482,9.27494,19.5828L9.27494,14.8435C9.27494,14.7781,9.22189,14.7251,9.15646,14.7251L4.41721,14.7251z M2.64,14.8435C2.64,13.862,3.43569,13.0663,4.41721,13.0663L9.15646,13.0663C10.138,13.0663,10.9337,13.862,10.9337,14.8435L10.9337,19.5828C10.9337,20.5643,10.138,21.36,9.15646,21.36L4.41721,21.36C3.43569,21.36,2.64,20.5643,2.64,19.5828L2.64,14.8435z")
                };

                drawingGroup.Children.Add(geometryDrawing);

                // 设置图像源
                drawingImage.Drawing = drawingGroup;
                image.Source = drawingImage;

                return image;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"创建图标图像时出错: {ex.Message}", LogHelper.LogType.Error);
                LogHelper.NewLog(ex);

                // 返回一个空图像
                return new Image();
            }
        }

        /// <summary>
        /// 鼠标按下事件
        /// </summary>
        private void Panel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                // 提供反馈
                _panel.Background = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0));
                LogHelper.WriteLogToFile("启动台按钮鼠标按下");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"启动台按钮鼠标按下事件出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 鼠标抬起事件
        /// </summary>
        private void Panel_MouseUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                // 只有左键点击才显示启动台窗口
                if (e.ChangedButton != MouseButton.Left)
                {
                    return;
                }

                // 恢复背景
                _panel.Background = Brushes.Transparent;
                LogHelper.WriteLogToFile("启动台按钮鼠标抬起，准备显示启动台窗口");

                // 获取按钮在屏幕上的位置
                Point buttonPosition = _panel.PointToScreen(new Point(_panel.ActualWidth / 2, 0));

                // 显示启动台窗口
                _plugin.ShowLauncherWindow(buttonPosition);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"启动台按钮鼠标抬起事件出错: {ex.Message}", LogHelper.LogType.Error);
                LogHelper.NewLog(ex);
            }
        }

        /// <summary>
        /// 鼠标离开事件
        /// </summary>
        private void Panel_MouseLeave(object sender, MouseEventArgs e)
        {
            try
            {
                // 恢复背景
                _panel.Background = Brushes.Transparent;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"启动台按钮鼠标离开事件出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }
    }
}