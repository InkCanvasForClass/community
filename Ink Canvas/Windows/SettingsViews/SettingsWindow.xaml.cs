using Ink_Canvas.Windows.SettingsViews;
using iNKORE.UI.WPF.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas.Windows
{
    public partial class SettingsWindow : Window
    {
        private MainWindow _mainWindow;

        /// <summary>
        /// 初始化设置窗口的 UI、事件和面板并加载初始状态与主题。
        /// </summary>
        /// <remarks>
        /// 构造函数完成以下工作：获取主窗口引用，注册搜索与菜单事件，构建并绑定侧栏条目，挂接各设置面板的滚动/阴影事件，初始化面板数组、滚动容器、标题与名称映射，设置初始选中项与主题状态，为自定义滑块添加触摸支持，预加载所有面板设置，并多次应用主题以确保视觉元素正确呈现（包含延迟再应用以修正标题栏等）。
        /// </remarks>
        public SettingsWindow()
        {
            InitializeComponent();

            // 获取 MainWindow 实例
            _mainWindow = Application.Current.MainWindow as MainWindow;

            // 初始化搜索面板事件
            SearchPanelControl.NavigateToItem += SearchPanel_NavigateToItem;
            SearchPanelControl.CloseSearch += SearchPanel_CloseSearch;

            // 订阅菜单关闭事件，确保状态同步
            if (MenuButtonContextMenu != null)
            {
                MenuButtonContextMenu.Closed += (s, e) =>
                {
                    // 菜单关闭时的处理
                };
            }

            // 初始化侧边栏项目
            SidebarItemsControl.ItemsSource = SidebarItems;
            SidebarItems.Add(new SidebarItem()
            {
                Type = SidebarItemType.Item,
                Title = "启动时行为",
                Name = "StartupItem",
                IconSource = FindResource("StartupIcon") as DrawingImage,
                Selected = false,
            });
            SidebarItems.Add(new SidebarItem()
            {
                Type = SidebarItemType.Item,
                Title = "画板和墨迹",
                Name = "CanvasAndInkItem",
                IconSource = FindResource("CanvasAndInkIcon") as DrawingImage,
                Selected = false,
            });
            SidebarItems.Add(new SidebarItem()
            {
                Type = SidebarItemType.Item,
                Title = "手势操作",
                Name = "GesturesItem",
                IconSource = FindResource("GesturesIcon") as DrawingImage,
                Selected = false,
            });
            SidebarItems.Add(new SidebarItem()
            {
                Type = SidebarItemType.Item,
                Title = "墨迹纠正",
                Name = "InkRecognitionItem",
                IconSource = FindResource("InkRecognitionIcon") as DrawingImage,
                Selected = false,
            });
            SidebarItems.Add(new SidebarItem()
            {
                Type = SidebarItemType.Separator
            });
            SidebarItems.Add(new SidebarItem()
            {
                Type = SidebarItemType.Item,
                Title = "个性化设置",
                Name = "ThemeItem",
                IconSource = FindResource("AppearanceIcon") as DrawingImage,
                Selected = false,
            });
            SidebarItems.Add(new SidebarItem()
            {
                Type = SidebarItemType.Item,
                Title = "快捷键设置",
                Name = "ShortcutsItem",
                IconSource = FindResource("ShortcutsIcon") as DrawingImage,
                Selected = false,
            });
            SidebarItems.Add(new SidebarItem()
            {
                Type = SidebarItemType.Item,
                Title = "崩溃处理",
                Name = "CrashActionItem",
                IconSource = FindResource("CrashActionIcon") as DrawingImage,
                Selected = false,
            });
            SidebarItems.Add(new SidebarItem()
            {
                Type = SidebarItemType.Separator
            });
            SidebarItems.Add(new SidebarItem()
            {
                Type = SidebarItemType.Item,
                Title = "PowerPoint 支持",
                Name = "PowerPointItem",
                IconSource = FindResource("PowerPointIcon") as DrawingImage,
                Selected = false,
            });

            SidebarItems.Add(new SidebarItem()
            {
                Type = SidebarItemType.Item,
                Title = "自动化行为",
                Name = "AutomationItem",
                IconSource = FindResource("AutomationIcon") as DrawingImage,
                Selected = false,
            });
            SidebarItems.Add(new SidebarItem()
            {
                Type = SidebarItemType.Item,
                Title = "随机点名",
                Name = "LuckyRandomItem",
                IconSource = FindResource("LuckyRandomIcon") as DrawingImage,
                Selected = false,
            });
            SidebarItems.Add(new SidebarItem()
            {
                Type = SidebarItemType.Separator
            });
            SidebarItems.Add(new SidebarItem()
            {
                Type = SidebarItemType.Item,
                Title = "高级选项",
                Name = "AdvancedItem",
                IconSource = FindResource("AdvancedIcon") as DrawingImage,
                Selected = false,
            });
            SidebarItems.Add(new SidebarItem()
            {
                Type = SidebarItemType.Item,
                Title = "截图和屏幕捕捉",
                Name = "SnapshotItem",
                IconSource = FindResource("SnapshotIcon") as DrawingImage,
                Selected = false,
            });
            SidebarItems.Add(new SidebarItem()
            {
                Type = SidebarItemType.Separator
            });
            SidebarItems.Add(new SidebarItem()
            {
                Type = SidebarItemType.Item,
                Title = "安全",
                Name = "SecurityItem",
                IconSource = FindResource("SecurityIcon") as DrawingImage,
                Selected = false,
            });
            SidebarItems.Add(new SidebarItem()
            {
                Type = SidebarItemType.Item,
                Title = "更新中心",
                Name = "UpdateCenterItem",
                IconSource = FindResource("UpdateCenterIcon") as DrawingImage,
                Selected = false,
            });
            SidebarItems.Add(new SidebarItem()
            {
                Type = SidebarItemType.Item,
                Title = "关于 InkCanvasForClass",
                Name = "AboutItem",
                IconSource = FindResource("AboutIcon") as DrawingImage,
                Selected = false,
            });
            SettingsPanes = new Grid[] {
                SecurityPane,
                UpdateCenterPane,
                AboutPane,
                CanvasAndInkPane,
                GesturesPane,
                StartupPane,
                ThemePane,
                ShortcutsPane,
                CrashActionPane,
                InkRecognitionPane,
                AutomationPane,
                PowerPointPane,
                LuckyRandomPane,
                AdvancedPane,
                SnapshotPane
            };

            SettingsPaneScrollViewers = new ScrollViewer[] {
                SecurityPanel.ScrollViewerEx,
                UpdateCenterPanel.UpdateCenterScrollViewerEx,
                SettingsAboutPanel.AboutScrollViewerEx,
                CanvasAndInkPanel.ScrollViewerEx,
                GesturesPanel.ScrollViewerEx,
                StartupPanel.ScrollViewerEx,
                ThemePanel.ScrollViewerEx,
                ShortcutsPanel.ScrollViewerEx,
                CrashActionPanel.ScrollViewerEx,
                InkRecognitionPanel.ScrollViewerEx,
                AutomationPanel.ScrollViewerEx,
                PowerPointPanel.ScrollViewerEx,
                LuckyRandomPanel.ScrollViewerEx,
                AdvancedPanel.ScrollViewerEx,
                SnapshotPanel.ScrollViewerEx
            };

            SettingsPaneTitles = new string[] {
                "安全",
                "更新中心",
                "关于",
                "画板和墨迹",
                "手势操作",
                "启动时行为",
                "个性化设置",
                "快捷键设置",
                "崩溃处理",
                "墨迹识别",
                "自动化",
                "PowerPoint",
                "幸运随机",
                "高级",
                "截图"
            };

            SettingsPaneNames = new string[] {
                "SecurityItem",
                "UpdateCenterItem",
                "AboutItem",
                "CanvasAndInkItem",
                "GesturesItem",
                "StartupItem",
                "ThemeItem",
                "ShortcutsItem",
                "CrashActionItem",
                "InkRecognitionItem",
                "AutomationItem",
                "PowerPointItem",
                "LuckyRandomItem",
                "AdvancedItem",
                "SnapshotItem"
            };

            SettingsAboutPanel.IsTopBarNeedShadowEffect += (o, s) => DropShadowEffectTopBar.Opacity = 0.25;
            SettingsAboutPanel.IsTopBarNeedNoShadowEffect += (o, s) => DropShadowEffectTopBar.Opacity = 0;

            // 订阅所有UserControl的滚动事件
            CanvasAndInkPanel.IsTopBarNeedShadowEffect += (o, s) => DropShadowEffectTopBar.Opacity = 0.25;
            CanvasAndInkPanel.IsTopBarNeedNoShadowEffect += (o, s) => DropShadowEffectTopBar.Opacity = 0;
            GesturesPanel.IsTopBarNeedShadowEffect += (o, s) => DropShadowEffectTopBar.Opacity = 0.25;
            GesturesPanel.IsTopBarNeedNoShadowEffect += (o, s) => DropShadowEffectTopBar.Opacity = 0;
            StartupPanel.IsTopBarNeedShadowEffect += (o, s) => DropShadowEffectTopBar.Opacity = 0.25;
            StartupPanel.IsTopBarNeedNoShadowEffect += (o, s) => DropShadowEffectTopBar.Opacity = 0;
            InkRecognitionPanel.IsTopBarNeedShadowEffect += (o, s) => DropShadowEffectTopBar.Opacity = 0.25;
            InkRecognitionPanel.IsTopBarNeedNoShadowEffect += (o, s) => DropShadowEffectTopBar.Opacity = 0;
            AutomationPanel.IsTopBarNeedShadowEffect += (o, s) => DropShadowEffectTopBar.Opacity = 0.25;
            AutomationPanel.IsTopBarNeedNoShadowEffect += (o, s) => DropShadowEffectTopBar.Opacity = 0;
            PowerPointPanel.IsTopBarNeedShadowEffect += (o, s) => DropShadowEffectTopBar.Opacity = 0.25;
            PowerPointPanel.IsTopBarNeedNoShadowEffect += (o, s) => DropShadowEffectTopBar.Opacity = 0;
            ThemePanel.IsTopBarNeedShadowEffect += (o, s) => DropShadowEffectTopBar.Opacity = 0.25;
            ThemePanel.IsTopBarNeedNoShadowEffect += (o, s) => DropShadowEffectTopBar.Opacity = 0;

            // 监听主题变化
            ThemePanel.ThemeChanged += (o, s) =>
            {
                ApplyTheme();
                ApplyThemeToAllPanels();
            };
            ShortcutsPanel.IsTopBarNeedShadowEffect += (o, s) => DropShadowEffectTopBar.Opacity = 0.25;
            ShortcutsPanel.IsTopBarNeedNoShadowEffect += (o, s) => DropShadowEffectTopBar.Opacity = 0;
            CrashActionPanel.IsTopBarNeedShadowEffect += (o, s) => DropShadowEffectTopBar.Opacity = 0.25;
            CrashActionPanel.IsTopBarNeedNoShadowEffect += (o, s) => DropShadowEffectTopBar.Opacity = 0;
            LuckyRandomPanel.IsTopBarNeedShadowEffect += (o, s) => DropShadowEffectTopBar.Opacity = 0.25;
            LuckyRandomPanel.IsTopBarNeedNoShadowEffect += (o, s) => DropShadowEffectTopBar.Opacity = 0;
            AdvancedPanel.IsTopBarNeedShadowEffect += (o, s) => DropShadowEffectTopBar.Opacity = 0.25;
            AdvancedPanel.IsTopBarNeedNoShadowEffect += (o, s) => DropShadowEffectTopBar.Opacity = 0;
            SnapshotPanel.IsTopBarNeedShadowEffect += (o, s) => DropShadowEffectTopBar.Opacity = 0.25;
            SnapshotPanel.IsTopBarNeedNoShadowEffect += (o, s) => DropShadowEffectTopBar.Opacity = 0;
            SecurityPanel.IsTopBarNeedShadowEffect += (o, s) => DropShadowEffectTopBar.Opacity = 0.25;
            SecurityPanel.IsTopBarNeedNoShadowEffect += (o, s) => DropShadowEffectTopBar.Opacity = 0;
            UpdateCenterPanel.IsTopBarNeedShadowEffect += (o, s) => DropShadowEffectTopBar.Opacity = 0.25;
            UpdateCenterPanel.IsTopBarNeedNoShadowEffect += (o, s) => DropShadowEffectTopBar.Opacity = 0;

            _selectedSidebarItemName = "StartupItem";

            // 初始化侧边栏项目的主题状态
            bool isDarkTheme = MainWindow.Settings?.Appearance != null &&
                              (MainWindow.Settings.Appearance.Theme == 1 ||
                               (MainWindow.Settings.Appearance.Theme == 2 && !IsSystemThemeLight()));
            foreach (var item in SidebarItems)
            {
                item.IsDarkTheme = isDarkTheme;
            }

            UpdateSidebarItemsSelection();

            // 为自定义滑块控件添加触摸支持
            AddTouchSupportToCustomSliders();

            // 先应用主题，确保标题栏等元素正确显示
            ApplyTheme();

            // 加载所有面板的设置
            LoadAllPanelsSettings();

            // 通知所有面板应用主题
            ApplyThemeToAllPanels();

            // 延迟再次应用主题，确保所有元素都正确应用主题（特别是标题栏）
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ApplyTheme();
                ApplyThemeToAllPanels();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>
        /// 通知所有已注册的设置面板应用当前主题配置，使各面板更新其视觉样式以匹配窗口主题。
        /// </summary>
        /// <remarks>
        /// 对每个面板尝试通过反射调用其 `ApplyTheme` 方法；如果某面板不存在该方法则会跳过，调用过程中发生的异常会被捕获并写入调试输出，但不会中断对其它面板的处理。
        /// </remarks>
        private void ApplyThemeToAllPanels()
        {
            try
            {
                bool isDarkTheme = MainWindow.Settings?.Appearance != null &&
                                  (MainWindow.Settings.Appearance.Theme == 1 ||
                                   (MainWindow.Settings.Appearance.Theme == 2 && !IsSystemThemeLight()));

                // 使用反射调用所有面板的 ApplyTheme 方法（如果存在）
                var panels = new UserControl[]
                {
                    SecurityPanel, UpdateCenterPanel, StartupPanel, CanvasAndInkPanel, GesturesPanel, InkRecognitionPanel,
                    ThemePanel, ShortcutsPanel, CrashActionPanel, PowerPointPanel,
                    AutomationPanel, LuckyRandomPanel, AdvancedPanel, SnapshotPanel,
                    SettingsAboutPanel, AppearancePanel, SearchPanelControl
                };

                foreach (var panel in panels)
                {
                    if (panel != null)
                    {
                        var method = panel.GetType().GetMethod("ApplyTheme",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (method != null)
                        {
                            try
                            {
                                method.Invoke(panel, null);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"应用主题到 {panel.GetType().Name} 时出错: {ex.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"通知面板应用主题时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 应用主题
        /// </summary>
        public void ApplyTheme()
        {
            try
            {
                if (MainWindow.Settings?.Appearance == null) return;

                bool isDarkTheme = MainWindow.Settings.Appearance.Theme == 1 ||
                                   (MainWindow.Settings.Appearance.Theme == 2 && !IsSystemThemeLight());

                if (isDarkTheme)
                {
                    if (MainBorder != null)
                    {
                        MainBorder.Background = new SolidColorBrush(Color.FromRgb(43, 43, 43));
                        MainBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 120, 215));
                    }
                    if (SidebarBorder != null)
                    {
                        SidebarBorder.Background = ThemeHelper.GetBackgroundSecondaryBrush(); // Windows 系统次要背景 #191919
                        SidebarBorder.BorderBrush = ThemeHelper.GetBorderPrimaryBrush(); // Windows 系统边框
                    }
                    if (SearchButtonBorder != null)
                    {
                        SearchButtonBorder.Background = ThemeHelper.GetButtonBackgroundBrush(); // Windows 系统按钮背景
                        // 更新搜索按钮图标颜色
                        UpdateButtonIconColor(SearchButtonBorder, true);
                    }
                    if (MenuButtonBorder != null)
                    {
                        MenuButtonBorder.Background = ThemeHelper.GetButtonBackgroundBrush(); // Windows 系统按钮背景
                        // 更新菜单按钮图标颜色
                        UpdateButtonIconColor(MenuButtonBorder, true);
                    }
                    if (TitleTextBlock != null)
                    {
                        TitleTextBlock.Foreground = ThemeHelper.GetTextPrimaryBrush(); // Windows 系统主文字颜色
                    }
                    if (MenuButtonContextMenu != null)
                    {
                        MenuButtonContextMenu.Background = ThemeHelper.GetBackgroundSecondaryBrush(); // Windows 系统菜单背景
                        MenuButtonContextMenu.BorderBrush = ThemeHelper.GetBorderPrimaryBrush(); // Windows 系统边框
                        // 更新上下文菜单中的图标和文字颜色
                        UpdateContextMenuTheme(MenuButtonContextMenu, true);
                    }
                    if (SettingsWindowTitle != null)
                    {
                        SettingsWindowTitle.Foreground = ThemeHelper.GetTextPrimaryBrush(); // Windows 系统主文字颜色
                    }
                    if (TopBarBorder != null)
                    {
                        TopBarBorder.Background = ThemeHelper.GetBackgroundPrimaryBrush(); // Windows 系统主背景
                    }
                    // 更新内部标题栏背景 - 使用直接访问而不是 FindDescendantByName
                    // TopBarBackgroundBorder 是 TopBarBorder 的直接子元素
                    if (TopBarBorder != null && TopBarBorder.Child is Border topBarBackgroundBorder)
                    {
                        topBarBackgroundBorder.Background = ThemeHelper.GetBackgroundPrimaryBrush(); // Windows 系统主背景
                    }
                    // 如果上面的方法找不到，尝试使用 FindDescendantByName 作为备用
                    var topBarBackgroundBorderFallback = this.FindDescendantByName("TopBarBackgroundBorder") as Border;
                    if (topBarBackgroundBorderFallback != null)
                    {
                        topBarBackgroundBorderFallback.Background = ThemeHelper.GetBackgroundPrimaryBrush(); // Windows 系统主背景
                    }

                    // 更新侧边栏项目文本颜色
                    foreach (var item in SidebarItems)
                    {
                        // 通过反射或直接访问来更新文本颜色
                        // 这里需要在 XAML 中绑定或通过其他方式更新
                    }

                    // 更新滚动条样式 - 参考 Windows 系统设置
                    var scrollBarTrack = this.FindDescendantByName("ScrollBarBorderTrackBackground") as Border;
                    if (scrollBarTrack != null)
                    {
                        scrollBarTrack.Background = ThemeHelper.GetScrollBarTrackBrush(); // Windows 系统滚动条轨道
                        scrollBarTrack.Opacity = 0.3;
                    }

                    // 更新侧边栏项目主题
                    foreach (var item in SidebarItems)
                    {
                        item.IsDarkTheme = true;
                    }
                    CollectionViewSource.GetDefaultView(SidebarItems).Refresh();

                    // 更新图标颜色
                    UpdateIconColors(true);
                }
                else
                {
                    if (MainBorder != null)
                    {
                        MainBorder.Background = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                        MainBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(53, 132, 228));
                    }
                    if (SidebarBorder != null)
                    {
                        SidebarBorder.Background = new SolidColorBrush(Color.FromRgb(235, 235, 235));
                        SidebarBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(225, 225, 225));
                    }
                    if (SearchButtonBorder != null)
                    {
                        SearchButtonBorder.Background = new SolidColorBrush(Color.FromRgb(217, 217, 217));
                        // 更新搜索按钮图标颜色
                        UpdateButtonIconColor(SearchButtonBorder, false);
                    }
                    if (MenuButtonBorder != null)
                    {
                        MenuButtonBorder.Background = new SolidColorBrush(Color.FromRgb(217, 217, 217));
                        // 更新菜单按钮图标颜色
                        UpdateButtonIconColor(MenuButtonBorder, false);
                    }
                    if (TitleTextBlock != null)
                    {
                        TitleTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(46, 52, 54));
                    }
                    if (MenuButtonContextMenu != null)
                    {
                        MenuButtonContextMenu.Background = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                        MenuButtonContextMenu.BorderBrush = new SolidColorBrush(Color.FromRgb(211, 211, 211));
                        // 更新上下文菜单中的图标和文字颜色
                        UpdateContextMenuTheme(MenuButtonContextMenu, false);
                    }
                    if (SettingsWindowTitle != null)
                    {
                        SettingsWindowTitle.Foreground = new SolidColorBrush(Color.FromRgb(46, 52, 54));
                    }
                    if (TopBarBorder != null)
                    {
                        TopBarBorder.Background = new SolidColorBrush(Color.FromRgb(250, 250, 250));
                    }
                    // 更新内部标题栏背景 - 使用直接访问而不是 FindDescendantByName
                    // TopBarBackgroundBorder 是 TopBarBorder 的直接子元素
                    if (TopBarBorder != null && TopBarBorder.Child is Border topBarBackgroundBorder)
                    {
                        topBarBackgroundBorder.Background = new SolidColorBrush(Color.FromRgb(250, 250, 250));
                    }
                    // 如果上面的方法找不到，尝试使用 FindDescendantByName 作为备用
                    var topBarBackgroundBorderFallback = this.FindDescendantByName("TopBarBackgroundBorder") as Border;
                    if (topBarBackgroundBorderFallback != null)
                    {
                        topBarBackgroundBorderFallback.Background = new SolidColorBrush(Color.FromRgb(250, 250, 250));
                    }

                    // 更新滚动条样式
                    var scrollBarTrack = this.FindDescendantByName("ScrollBarBorderTrackBackground") as Border;
                    if (scrollBarTrack != null)
                    {
                        scrollBarTrack.Background = ThemeHelper.GetScrollBarTrackBrush(); // Windows 系统滚动条轨道
                        scrollBarTrack.Opacity = 0;
                    }

                    // 更新侧边栏项目主题
                    foreach (var item in SidebarItems)
                    {
                        item.IsDarkTheme = false;
                    }
                    CollectionViewSource.GetDefaultView(SidebarItems).Refresh();

                    // 更新图标颜色
                    UpdateIconColors(false);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"应用主题时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新所有图标颜色以适配主题
        /// </summary>
        private void UpdateIconColors(bool isDarkTheme)
        {
            try
            {
                // 根据主题选择颜色
                Color iconColor = isDarkTheme
                    ? Color.FromRgb(243, 243, 243) // 深色主题使用浅色图标 #F3F3F3
                    : Color.FromRgb(34, 34, 34);   // 浅色主题使用深色图标 #222222

                // 更新每个侧边栏项目的图标
                foreach (var item in SidebarItems)
                {
                    if (item.IconSource is DrawingImage drawingImage &&
                        drawingImage.Drawing is DrawingGroup drawingGroup)
                    {
                        // 克隆并更新图标
                        var clonedDrawing = CloneDrawingGroup(drawingGroup, iconColor);
                        item.IconSource = new DrawingImage { Drawing = clonedDrawing };
                    }
                }

                CollectionViewSource.GetDefaultView(SidebarItems).Refresh();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新图标颜色时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 克隆 DrawingGroup 并更新颜色
        /// </summary>
        private DrawingGroup CloneDrawingGroup(DrawingGroup source, Color newColor)
        {
            var cloned = new DrawingGroup();
            cloned.ClipGeometry = source.ClipGeometry?.Clone();
            cloned.Opacity = source.Opacity;
            cloned.Transform = source.Transform?.Clone();

            foreach (var drawing in source.Children)
            {
                if (drawing is GeometryDrawing geometryDrawing)
                {
                    var clonedGeometry = geometryDrawing.Geometry?.Clone();
                    var clonedBrush = CloneBrush(geometryDrawing.Brush, newColor);
                    var clonedPen = geometryDrawing.Pen != null
                        ? ClonePen(geometryDrawing.Pen, newColor)
                        : null;

                    cloned.Children.Add(new GeometryDrawing(clonedBrush, clonedPen, clonedGeometry));
                }
                else if (drawing is DrawingGroup subGroup)
                {
                    cloned.Children.Add(CloneDrawingGroup(subGroup, newColor));
                }
                else
                {
                    // 对于其他类型的 Drawing，尝试克隆
                    cloned.Children.Add(drawing);
                }
            }

            return cloned;
        }

        /// <summary>
        /// 克隆 Brush 并更新颜色
        /// </summary>
        private Brush CloneBrush(Brush source, Color newColor)
        {
            if (source is SolidColorBrush solidBrush)
            {
                // 检查是否是深色（需要更新的颜色）
                var originalColor = solidBrush.Color;
                if (originalColor.R == 34 && originalColor.G == 34 && originalColor.B == 34) // #222222
                {
                    return new SolidColorBrush(newColor) { Opacity = solidBrush.Opacity };
                }
                else if (originalColor.A > 0 && originalColor != Colors.Transparent &&
                         originalColor.R < 50 && originalColor.G < 50 && originalColor.B < 50) // 深色
                {
                    return new SolidColorBrush(newColor) { Opacity = solidBrush.Opacity };
                }
                // 保持其他颜色不变（如透明色）
                return new SolidColorBrush(originalColor) { Opacity = solidBrush.Opacity };
            }
            return source?.Clone();
        }

        /// <summary>
        /// 克隆 Pen 并更新颜色
        /// </summary>
        private Pen ClonePen(Pen source, Color newColor)
        {
            var clonedBrush = CloneBrush(source.Brush, newColor);
            return new Pen(clonedBrush, source.Thickness)
            {
                StartLineCap = source.StartLineCap,
                EndLineCap = source.EndLineCap,
                LineJoin = source.LineJoin,
                MiterLimit = source.MiterLimit
            };
        }

        /// <summary>
        /// 更新上下文菜单的主题（图标和文字颜色）
        /// </summary>
        private void UpdateContextMenuTheme(ContextMenu contextMenu, bool isDarkTheme)
        {
            try
            {
                Color iconColor = isDarkTheme
                    ? Color.FromRgb(243, 243, 243) // 深色主题使用浅色图标 #F3F3F3
                    : Color.FromRgb(34, 34, 34);   // 浅色主题使用深色图标 #222222

                foreach (var item in contextMenu.Items)
                {
                    if (item is MenuItem menuItem)
                    {
                        // 更新文字颜色
                        menuItem.Foreground = ThemeHelper.GetTextPrimaryBrush();

                        // 更新图标颜色
                        if (menuItem.Icon is Image iconImage && iconImage.Source is DrawingImage drawingImage)
                        {
                            if (drawingImage.Drawing is DrawingGroup drawingGroup)
                            {
                                var clonedDrawing = CloneDrawingGroup(drawingGroup, iconColor);
                                iconImage.Source = new DrawingImage { Drawing = clonedDrawing };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新上下文菜单主题时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新按钮中的图标颜色
        /// </summary>
        private void UpdateButtonIconColor(Border buttonBorder, bool isDarkTheme)
        {
            try
            {
                Color iconColor = isDarkTheme
                    ? Color.FromRgb(243, 243, 243) // 深色主题使用浅色图标 #F3F3F3
                    : Color.FromRgb(34, 34, 34);   // 浅色主题使用深色图标 #222222

                // 查找按钮中的 Image 控件
                var image = FindVisualChild<Image>(buttonBorder);
                if (image != null && image.Source is DrawingImage drawingImage)
                {
                    if (drawingImage.Drawing is DrawingGroup drawingGroup)
                    {
                        var clonedDrawing = CloneDrawingGroup(drawingGroup, iconColor);
                        image.Source = new DrawingImage { Drawing = clonedDrawing };
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新按钮图标颜色时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 在视觉树中查找指定类型的子元素
        /// </summary>
        private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                {
                    return result;
                }
                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                {
                    return childOfChild;
                }
            }
            return null;
        }

        /// <summary>
        /// 检查系统主题是否为浅色
        /// </summary>
        private bool IsSystemThemeLight()
        {
            try
            {
                var registryKey = Microsoft.Win32.Registry.CurrentUser;
                var themeKey = registryKey.OpenSubKey("software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize");
                var keyValue = 0;
                if (themeKey != null) keyValue = (int)themeKey.GetValue("SystemUsesLightTheme");
                return keyValue == 1;
            }
            catch
            {
                return true; // 默认返回浅色主题
            }
        }

        /// <summary>
        /// 预加载并初始化所有设置面板：对每个面板尝试加载设置、启用触摸支持并应用当前主题。
        /// </summary>
        /// <remarks>
        /// 该操作在 UI 线程上以 DispatcherPriority.Loaded 异步调度执行，并在完成后再次触发对所有面板的主题应用以确保视觉状态一致。
        /// 对单个面板的初始化错误会被捕获并处理，不会中断其它面板的预加载流程。
        /// </remarks>
        private void LoadAllPanelsSettings()
        {
            try
            {
                // 预加载所有面板，确保它们在显示前都已初始化
                // 使用 Dispatcher.BeginInvoke 延迟执行，确保所有面板都已创建
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        // 所有设置面板列表
                        var allPanels = new UserControl[]
                        {
                            StartupPanel,
                            CanvasAndInkPanel,
                            GesturesPanel,
                            InkRecognitionPanel,
                            ThemePanel,
                            ShortcutsPanel,
                            CrashActionPanel,
                            PowerPointPanel,
                            AutomationPanel,
                            LuckyRandomPanel,
                            AdvancedPanel,
                            SnapshotPanel,
                            SecurityPanel,
                            UpdateCenterPanel,
                            SettingsAboutPanel,
                            AppearancePanel
                        };

                        // 预加载所有面板：调用 LoadSettings、EnableTouchSupport 和 ApplyTheme
                        foreach (var panel in allPanels)
                        {
                            if (panel != null)
                            {
                                try
                                {
                                    // 直接调用 LoadSettings 确保设置被加载
                                    var loadSettingsMethod = panel.GetType().GetMethod("LoadSettings",
                                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                    if (loadSettingsMethod != null)
                                    {
                                        loadSettingsMethod.Invoke(panel, null);
                                    }

                                    // 调用 EnableTouchSupport 确保触摸支持已启用
                                    var enableTouchSupportMethod = panel.GetType().GetMethod("EnableTouchSupport",
                                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                    if (enableTouchSupportMethod != null)
                                    {
                                        enableTouchSupportMethod.Invoke(panel, null);
                                    }
                                    else
                                    {
                                        // 如果面板没有 EnableTouchSupport 方法，直接使用 MainWindowSettingsHelper
                                        MainWindowSettingsHelper.EnableTouchSupportForControls(panel);
                                    }

                                    // 调用 ApplyTheme 确保主题已应用
                                    var applyThemeMethod = panel.GetType().GetMethod("ApplyTheme",
                                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                    if (applyThemeMethod != null)
                                    {
                                        applyThemeMethod.Invoke(panel, null);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"预加载面板 {panel?.GetType().Name} 时出错: {ex.Message}");
                                }
                            }
                        }

                        // 再次应用主题到所有面板，确保主题完全加载
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            ApplyThemeToAllPanels();
                        }), System.Windows.Threading.DispatcherPriority.Loaded);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"预加载所有面板时出错: {ex.Message}");
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载设置面板设置时出错: {ex.Message}");
            }
        }



        public enum SidebarItemType
        {
            Item,
            Separator
        }

        public class SidebarItem : System.ComponentModel.INotifyPropertyChanged
        {
            public SidebarItemType Type { get; set; }
            public string Title { get; set; }
            public string Name { get; set; }
            public ImageSource IconSource { get; set; }
            private bool _selected = false;
            private bool _isDarkTheme = false;

            public bool Selected
            {
                get => _selected;
                set
                {
                    _selected = value;
                    OnPropertyChanged(nameof(_siBackground));
                }
            }

            public bool IsDarkTheme
            {
                get => _isDarkTheme;
                set
                {
                    _isDarkTheme = value;
                    OnPropertyChanged(nameof(_siForeground));
                    OnPropertyChanged(nameof(_siBackground));
                    OnPropertyChanged(nameof(_spStroke));
                }
            }

            public Visibility _spVisibility
            {
                get => Type == SidebarItemType.Separator ? Visibility.Visible : Visibility.Collapsed;
            }
            public Visibility _siVisibility
            {
                get => Type == SidebarItemType.Item ? Visibility.Visible : Visibility.Collapsed;
            }

            public SolidColorBrush _siBackground
            {
                get
                {
                    if (Selected)
                    {
                        return _isDarkTheme
                            ? ThemeHelper.GetSelectedBackgroundBrush() // Windows 系统选中背景 #3E3E3E
                            : new SolidColorBrush(Color.FromRgb(237, 237, 237));
                    }
                    return new SolidColorBrush(Colors.Transparent);
                }
            }

            public SolidColorBrush _siForeground
            {
                get => _isDarkTheme
                    ? ThemeHelper.GetTextPrimaryBrush() // Windows 系统主文字颜色 #F3F3F3
                    : new SolidColorBrush(Color.FromRgb(0, 0, 0));
            }

            public SolidColorBrush _spStroke
            {
                get => _isDarkTheme
                    ? ThemeHelper.GetSeparatorBrush() // Windows 系统分隔线 #3E3E3E
                    : new SolidColorBrush(Color.FromRgb(237, 237, 237));
            }

            public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
            protected virtual void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
            }
        }

        public string _selectedSidebarItemName = "";
        public ObservableCollection<SidebarItem> SidebarItems = new ObservableCollection<SidebarItem>();

        public Grid[] SettingsPanes;
        public ScrollViewer[] SettingsPaneScrollViewers;
        public string[] SettingsPaneTitles;
        public string[] SettingsPaneNames;

        /// <summary>
        /// 根据当前选中的侧边栏项更新侧边栏条目状态、面板可见性与主题并将视图滚动到顶部。
        /// </summary>
        /// <remarks>
        /// - 将 SidebarItems 中的 Selected 与 SettingsWindowTitle.Text 与字段 _selectedSidebarItemName 同步；
        /// - 根据应用设置计算并同步每个 SidebarItem 的 IsDarkTheme；
        /// - 切换各个面板（Pane）的 Visibility，仅显示与 _selectedSidebarItemName 对应的面板；
        /// - 异步调用所选面板（若存在）的 ApplyTheme 方法以确保新显示面板使用正确主题；
        /// - 将所有已注册的 SettingsPaneScrollViewers 滚动到顶部以重置视图位置。
        /// </remarks>
        public void UpdateSidebarItemsSelection()
        {
            foreach (var si in SidebarItems)
            {
                si.Selected = si.Name == _selectedSidebarItemName;
                if (si.Selected && SettingsWindowTitle != null)
                {
                    SettingsWindowTitle.Text = si.Title;
                }
            }
            CollectionViewSource.GetDefaultView(SidebarItems).Refresh();

            // 确保主题状态同步
            bool isDarkTheme = MainWindow.Settings?.Appearance != null &&
                              (MainWindow.Settings.Appearance.Theme == 1 ||
                               (MainWindow.Settings.Appearance.Theme == 2 && !IsSystemThemeLight()));
            foreach (var si in SidebarItems)
            {
                si.IsDarkTheme = isDarkTheme;
            }

            // 定义面板映射
            var panelMappings = new Dictionary<string, UserControl>
            {
                { "AboutItem", SettingsAboutPanel },
                { "CanvasAndInkItem", CanvasAndInkPanel },
                { "GesturesItem", GesturesPanel },
                { "StartupItem", StartupPanel },
                { "ThemeItem", ThemePanel },
                { "ShortcutsItem", ShortcutsPanel },
                { "CrashActionItem", CrashActionPanel },
                { "InkRecognitionItem", InkRecognitionPanel },
                { "AutomationItem", AutomationPanel },
                { "PowerPointItem", PowerPointPanel },
                { "LuckyRandomItem", LuckyRandomPanel },
                { "AdvancedItem", AdvancedPanel },
                { "SnapshotItem", SnapshotPanel },
                { "SecurityItem", SecurityPanel },
                { "UpdateCenterItem", UpdateCenterPanel },
                { "AppearanceItem", AppearancePanel }
            };

            // 设置面板可见性并应用主题
            if (AboutPane != null) AboutPane.Visibility = _selectedSidebarItemName == "AboutItem" ? Visibility.Visible : Visibility.Collapsed;
            if (CanvasAndInkPane != null) CanvasAndInkPane.Visibility = _selectedSidebarItemName == "CanvasAndInkItem" ? Visibility.Visible : Visibility.Collapsed;
            if (GesturesPane != null) GesturesPane.Visibility = _selectedSidebarItemName == "GesturesItem" ? Visibility.Visible : Visibility.Collapsed;
            if (StartupPane != null) StartupPane.Visibility = _selectedSidebarItemName == "StartupItem" ? Visibility.Visible : Visibility.Collapsed;
            if (ThemePane != null) ThemePane.Visibility = _selectedSidebarItemName == "ThemeItem" ? Visibility.Visible : Visibility.Collapsed;
            if (ShortcutsPane != null) ShortcutsPane.Visibility = _selectedSidebarItemName == "ShortcutsItem" ? Visibility.Visible : Visibility.Collapsed;
            if (CrashActionPane != null) CrashActionPane.Visibility = _selectedSidebarItemName == "CrashActionItem" ? Visibility.Visible : Visibility.Collapsed;
            if (InkRecognitionPane != null) InkRecognitionPane.Visibility = _selectedSidebarItemName == "InkRecognitionItem" ? Visibility.Visible : Visibility.Collapsed;
            if (AutomationPane != null) AutomationPane.Visibility = _selectedSidebarItemName == "AutomationItem" ? Visibility.Visible : Visibility.Collapsed;
            if (PowerPointPane != null) PowerPointPane.Visibility = _selectedSidebarItemName == "PowerPointItem" ? Visibility.Visible : Visibility.Collapsed;
            if (LuckyRandomPane != null) LuckyRandomPane.Visibility = _selectedSidebarItemName == "LuckyRandomItem" ? Visibility.Visible : Visibility.Collapsed;
            if (AdvancedPane != null) AdvancedPane.Visibility = _selectedSidebarItemName == "AdvancedItem" ? Visibility.Visible : Visibility.Collapsed;
            if (SnapshotPane != null) SnapshotPane.Visibility = _selectedSidebarItemName == "SnapshotItem" ? Visibility.Visible : Visibility.Collapsed;
            if (SecurityPane != null) SecurityPane.Visibility = _selectedSidebarItemName == "SecurityItem" ? Visibility.Visible : Visibility.Collapsed;
            if (UpdateCenterPane != null) UpdateCenterPane.Visibility = _selectedSidebarItemName == "UpdateCenterItem" ? Visibility.Visible : Visibility.Collapsed;

            // 为新显示的面板应用主题（延迟执行，确保面板已完全显示）
            if (panelMappings.ContainsKey(_selectedSidebarItemName))
            {
                var selectedPanel = panelMappings[_selectedSidebarItemName];
                if (selectedPanel != null)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            var applyThemeMethod = selectedPanel.GetType().GetMethod("ApplyTheme",
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            if (applyThemeMethod != null)
                            {
                                applyThemeMethod.Invoke(selectedPanel, null);
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"切换面板时应用主题到 {selectedPanel.GetType().Name} 时出错: {ex.Message}");
                        }
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                }
            }
            if (SettingsPaneScrollViewers != null)
            {
                foreach (var sv in SettingsPaneScrollViewers)
                {
                    if (sv != null)
                    {
                        sv.ScrollToTop();
                    }
                }
            }
        }

        private void ScrollBar_Scroll(object sender, RoutedEventArgs e)
        {
            var scrollbar = (ScrollBar)sender;
            var scrollviewer = scrollbar.FindAscendant<ScrollViewer>();
            if (scrollviewer != null) scrollviewer.ScrollToVerticalOffset(scrollbar.Track.Value);
        }

        private void ScrollBarTrack_MouseEnter(object sender, MouseEventArgs e)
        {
            var border = (Border)sender;
            if (border.Child is Track track)
            {
                track.Width = 16;
                track.Margin = new Thickness(0, 0, -2, 0);
                var scrollbar = track.FindAscendant<ScrollBar>();
                if (scrollbar != null) scrollbar.Width = 16;
                var grid = track.FindAscendant<Grid>();
                if (grid.FindDescendantByName("ScrollBarBorderTrackBackground") is Border backgroundBorder)
                {
                    backgroundBorder.Width = 8;
                    backgroundBorder.CornerRadius = new CornerRadius(4);
                    backgroundBorder.Opacity = 1;
                }
                var thumb = track.Thumb.Template.FindName("ScrollbarThumbEx", track.Thumb);
                if (thumb != null)
                {
                    var _thumb = thumb as Border;
                    _thumb.CornerRadius = new CornerRadius(4);
                    _thumb.Width = 8;
                    _thumb.Margin = new Thickness(-0.75, 0, 1, 0);
                    _thumb.Background = new SolidColorBrush(Color.FromRgb(138, 138, 138));
                }
            }
        }

        private void ScrollBarTrack_MouseLeave(object sender, MouseEventArgs e)
        {
            var border = (Border)sender;
            border.Background = new SolidColorBrush(Colors.Transparent);
            border.CornerRadius = new CornerRadius(0);
            if (border.Child is Track track)
            {
                track.Width = 6;
                track.Margin = new Thickness(0, 0, 0, 0);
                var scrollbar = track.FindAscendant<ScrollBar>();
                if (scrollbar != null) scrollbar.Width = 6;
                var grid = track.FindAscendant<Grid>();
                if (grid.FindDescendantByName("ScrollBarBorderTrackBackground") is Border backgroundBorder)
                {
                    backgroundBorder.Width = 3;
                    backgroundBorder.CornerRadius = new CornerRadius(1.5);
                    backgroundBorder.Opacity = 0;
                }
                var thumb = track.Thumb.Template.FindName("ScrollbarThumbEx", track.Thumb);
                if (thumb != null)
                {
                    var _thumb = thumb as Border;
                    _thumb.CornerRadius = new CornerRadius(1.5);
                    _thumb.Width = 3;
                    _thumb.Margin = new Thickness(0);
                    _thumb.Background = new SolidColorBrush(Color.FromRgb(195, 195, 195));
                }
            }
        }

        private void ScrollbarThumb_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var thumb = (Thumb)sender;
            var border = thumb.Template.FindName("ScrollbarThumbEx", thumb);
            ((Border)border).Background = new SolidColorBrush(Color.FromRgb(95, 95, 95));
        }

        private void ScrollbarThumb_MouseUp(object sender, MouseButtonEventArgs e)
        {
            var thumb = (Thumb)sender;
            var border = thumb.Template.FindName("ScrollbarThumbEx", thumb);
            ((Border)border).Background = new SolidColorBrush(Color.FromRgb(138, 138, 138));
        }

        private Border _sidebarItemMouseDownBorder = null;

        private void SidebarItem_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_sidebarItemMouseDownBorder != null || _sidebarItemMouseDownBorder == sender) return;
            _sidebarItemMouseDownBorder = (Border)sender;
            var bd = sender as Border;
            if (bd.FindDescendantByName("MouseFeedbackBorder") is Border feedbackBd) feedbackBd.Opacity = 0.12;
        }

        private void SidebarItem_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_sidebarItemMouseDownBorder == null || _sidebarItemMouseDownBorder != sender) return;
            if (_sidebarItemMouseDownBorder.Tag is SidebarItem data) _selectedSidebarItemName = data.Name;
            SidebarItem_MouseLeave(sender, null);
            UpdateSidebarItemsSelection();
        }

        private void SidebarItem_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_sidebarItemMouseDownBorder == null || _sidebarItemMouseDownBorder != sender) return;
            if (_sidebarItemMouseDownBorder.FindDescendantByName("MouseFeedbackBorder") is Border feedbackBd) feedbackBd.Opacity = 0;
            _sidebarItemMouseDownBorder = null;
        }

        private void TopBarBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            DragMove();
        }

        private void SidebarTopBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;

            var source = e.OriginalSource as DependencyObject;
            if (IsInsideElement(source, SearchButtonBorder) || IsInsideElement(source, MenuButtonBorder))
                return;

            DragMove();
        }

        private static bool IsInsideElement(DependencyObject source, DependencyObject target)
        {
            if (source == null || target == null) return false;
            if (source == target) return true;
            return IsInsideElement(VisualTreeHelper.GetParent(source), target);
        }

        private void CloseButton_Click(object sender, MouseButtonEventArgs e)
        {
            Close();
        }

        private void SearchButton_Click(object sender, MouseButtonEventArgs e)
        {
            // 显示搜索界面
            SearchPane.Visibility = Visibility.Visible;
            SearchPanelControl.FocusSearchBox();
        }

        private void SearchPanel_NavigateToItem(object sender, string itemName)
        {
            // 隐藏搜索界面
            SearchPane.Visibility = Visibility.Collapsed;

            // 导航到对应的设置项
            NavigateToSidebarItem(itemName);
        }

        private void SearchPanel_CloseSearch(object sender, EventArgs e)
        {
            // 隐藏搜索界面
            SearchPane.Visibility = Visibility.Collapsed;
        }

        private void NavigateToSidebarItem(string itemName)
        {
            // 查找对应的侧边栏项并选中
            foreach (var item in SidebarItems)
            {
                if (item.Name == itemName)
                {
                    SelectSidebarItem(item);
                    break;
                }
            }
        }

        private void SelectSidebarItem(SidebarItem item)
        {
            _selectedSidebarItemName = item.Name;
            UpdateSidebarItemsSelection();
        }

        private void MenuButton_Click(object sender, MouseButtonEventArgs e)
        {
            // 切换上下文菜单的显示状态
            if (MenuButtonContextMenu != null)
            {
                MenuButtonContextMenu.PlacementTarget = MenuButtonBorder;
                MenuButtonContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;

                // 如果菜单已打开，则关闭；如果已关闭，则打开
                bool isCurrentlyOpen = MenuButtonContextMenu.IsOpen;

                if (isCurrentlyOpen)
                {
                    // 如果菜单已打开，直接关闭
                    MenuButtonContextMenu.IsOpen = false;
                }
                else
                {
                    // 如果菜单未打开，打开菜单
                    MenuButtonContextMenu.IsOpen = true;
                }

                // 标记事件已处理，防止菜单拦截点击
                e.Handled = true;
            }
        }

        private void MenuItemExit_Click(object sender, RoutedEventArgs e)
        {
            // 关闭设置窗口
            Close();

            // 调用主窗口的退出方法
            if (_mainWindow != null)
            {
                _mainWindow.BtnExit_Click(sender, e);
            }
        }

        private void MenuItemRestart_Click(object sender, RoutedEventArgs e)
        {
            // 关闭设置窗口
            Close();

            // 调用主窗口的重启方法
            if (_mainWindow != null)
            {
                _mainWindow.BtnRestart_Click(sender, e);
            }
        }

        private void MenuItemReset_Click(object sender, RoutedEventArgs e)
        {
            // 调用主窗口的重置配置方法
            if (_mainWindow != null)
            {
                _mainWindow.BtnResetToSuggestion_Click(sender, e);
            }
        }

        private void MenuItemSwitchToOldSettings_Click(object sender, RoutedEventArgs e)
        {
            // 关闭新设置窗口
            Close();
        }

        private void ToggleSwitch_Click(object sender, MouseButtonEventArgs e)
        {
            var border = sender as Border;
            if (border != null)
            {
                // 切换开关状态
                bool isOn = border.Background.ToString() == "#FF3584E4";
                border.Background = isOn ? new SolidColorBrush(Color.FromRgb(225, 225, 225)) : new SolidColorBrush(Color.FromRgb(53, 132, 228));

                // 切换内部圆点的位置
                var innerBorder = border.Child as Border;
                if (innerBorder != null)
                {
                    innerBorder.HorizontalAlignment = isOn ? HorizontalAlignment.Left : HorizontalAlignment.Right;
                }

                // 根据Tag处理不同的设置项
                string tag = border.Tag?.ToString();
                if (!string.IsNullOrEmpty(tag))
                {
                    HandleSettingChange(tag, !isOn);
                }
            }
        }

        private void HandleSettingChange(string settingName, bool value)
        {
            // 根据设置名称处理不同的设置项
            switch (settingName)
            {
                case "UseObviousCursor":
                    // 处理使用更加明显的画笔光标设置
                    break;
                case "HideInkWhenExitAnnotationMode":
                    // 处理退出批注模式后隐藏墨迹设置
                    break;
                case "DisablePenPressure":
                    // 处理禁用模拟笔锋设置
                    break;
                case "ClearInkWithoutHistory":
                    // 处理清空墨迹时不保留时光机历史记录设置
                    break;
                case "UseBlackBackgroundForTransparency":
                    // 处理AllowTransparency使用黑色背景设置
                    break;
                case "KeepHyperbolaAsymptote":
                    // 处理保留双曲线渐近线设置
                    break;
                case "UseInkEraser":
                    // 处理使用墨迹擦设置
                    break;
                case "UseDefaultBackgroundColorForNewPage":
                    // 处理创建新页时始终使用默认背景色设置
                    break;
                case "DebugMode":
                    // 处理调试模式设置
                    break;
                case "PerformanceMonitoring":
                    // 处理性能监控设置
                    break;
                case "AutoRestartOnCrash":
                    // 处理崩溃时自动重启设置
                    break;
                case "SendCrashReport":
                    // 处理发送崩溃报告设置
                    break;
                case "EnableLuckyRandom":
                    // 处理启用幸运随机功能设置
                    break;
                case "EnableInkToShape":
                    // 处理启用墨迹转形状设置
                    break;
                case "InkRecognitionRange":
                    // 处理墨迹识别范围设置
                    break;
                case "RecognizeQuadrilateral":
                    // 处理识别四边形设置
                    break;
                case "RecognizeTriangle":
                    // 处理识别三角形设置
                    break;
                case "RecognizeCircle":
                    // 处理识别圆形设置
                    break;
                case "RecognizeLine":
                    // 处理识别直线设置
                    break;
                case "EnablePressureSimulation":
                    // 处理启用三角形和矩形每边模拟压力值设置
                    break;
                case "ConcentricCircleCorrection":
                    // 处理同心圆识别矫正设置
                    break;
                case "EnableAutoHide":
                    // 处理启用自动收纳设置
                    break;
                case "EnableAutoKill":
                    // 处理启用自动查杀设置
                    break;
                case "EnableWhiteboardKiller":
                    // 处理启用桌面画板悬浮窗杀手设置
                    break;
                case "EnablePowerPointCom":
                    // 处理启用 PowerPoint COM 支持设置
                    break;
                case "EnableWpsCom":
                    // 处理启用 WPS COM 支持设置
                    break;
                case "EnableVsto":
                    // 处理启用 VSTO 支持设置
                    break;
                default:
                    // 未知设置项
                    break;
            }
        }

        private void OptionButton_Click(object sender, MouseButtonEventArgs e)
        {
            var border = sender as Border;
            if (border != null)
            {
                string tag = border.Tag?.ToString();
                if (!string.IsNullOrEmpty(tag))
                {
                    // 清除同组其他按钮的选中状态
                    ClearOtherOptionsInGroup(border, tag);

                    // 设置当前按钮为选中状态
                    border.Background = new SolidColorBrush(Color.FromRgb(225, 225, 225));
                    var textBlock = border.Child as TextBlock;
                    if (textBlock != null)
                    {
                        textBlock.FontWeight = FontWeights.Bold;
                    }

                    // 处理选项变化
                    HandleOptionChange(tag);
                }
            }
        }

        private void ClearOtherOptionsInGroup(Border currentBorder, string currentTag)
        {
            // 获取当前按钮所在的父容器
            var parent = currentBorder.Parent as StackPanel;
            if (parent != null)
            {
                // 获取组名（Tag中下划线前的部分）
                string groupName = currentTag.Split('_')[0];

                // 清除同组其他按钮的选中状态
                foreach (var child in parent.Children)
                {
                    if (child is Border border && border != currentBorder)
                    {
                        string childTag = border.Tag?.ToString();
                        if (!string.IsNullOrEmpty(childTag) && childTag.StartsWith(groupName + "_"))
                        {
                            border.Background = new SolidColorBrush(Colors.Transparent);
                            var textBlock = border.Child as TextBlock;
                            if (textBlock != null)
                            {
                                textBlock.FontWeight = FontWeights.Normal;
                            }
                        }
                    }
                }
            }
        }

        private void HandleOptionChange(string optionTag)
        {
            // 根据选项标签处理不同的选项变化
            string[] parts = optionTag.Split('_');
            if (parts.Length >= 2)
            {
                string group = parts[0];
                string value = parts[1];

                switch (group)
                {
                    case "EraserSize":
                        // 处理板擦橡皮大小设置
                        break;
                    case "DefaultBackgroundColor":
                        // 处理默认背景色设置
                        break;
                    case "DefaultPaperFormat":
                        // 处理默认稿纸格式设置
                        break;
                    case "AutoSaveInterval":
                        // 处理自动保存间隔设置
                        break;
                    case "ScreenshotQuality":
                        // 处理截图质量设置
                        break;
                    case "ScreenshotFormat":
                        // 处理截图格式设置
                        break;
                    case "InkRecognitionBehavior":
                        // 处理墨迹识别后转换行为设置
                        break;
                    case "Theme":
                        // 处理主题设置
                        break;
                    case "Language":
                        // 处理语言设置
                        break;
                    default:
                        // 未知选项组
                        break;
                }
            }
        }

        #region 自定义滑块触摸支持

        /// <summary>
        /// 为自定义滑块控件添加触摸和手写笔事件支持
        /// </summary>
        private void AddTouchSupportToCustomSliders()
        {
            try
            {
                // 延迟执行，确保UI元素已加载
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    // 查找所有自定义滑块控件并添加触摸支持
                    AddTouchSupportToCustomSliderInPane(CanvasAndInkPane);
                    AddTouchSupportToCustomSliderInPane(ThemePane);
                    AddTouchSupportToCustomSliderInPane(PowerPointPane);
                    AddTouchSupportToCustomSliderInPane(AutomationPane);
                    AddTouchSupportToCustomSliderInPane(LuckyRandomPane);
                    AddTouchSupportToCustomSliderInPane(AdvancedPane);
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
            catch (Exception ex)
            {
                // 记录错误但不影响程序运行
                Debug.WriteLine($"添加自定义滑块触摸支持时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 为指定面板中的自定义滑块控件添加触摸支持
        /// </summary>
        /// <param name="pane">面板控件</param>
        private void AddTouchSupportToCustomSliderInPane(Grid pane)
        {
            if (pane == null) return;

            // 查找面板中的所有自定义滑块控件
            var customSliders = FindCustomSlidersInPanel(pane);

            foreach (var slider in customSliders)
            {
                AddTouchSupportToCustomSlider(slider);
            }
        }

        /// <summary>
        /// 在面板中查找自定义滑块控件
        /// </summary>
        /// <param name="panel">面板控件</param>
        /// <returns>自定义滑块控件列表</returns>
        private List<CustomSliderInfo> FindCustomSlidersInPanel(DependencyObject panel)
        {
            var customSliders = new List<CustomSliderInfo>();

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(panel); i++)
            {
                var child = VisualTreeHelper.GetChild(panel, i);

                // 检查是否是自定义滑块控件（包含GnomeSliderThumb图片的Grid）
                if (child is Grid grid)
                {
                    var customSlider = FindCustomSliderInGrid(grid);
                    if (customSlider != null)
                    {
                        customSliders.Add(customSlider);
                    }
                }

                // 递归查找子元素
                customSliders.AddRange(FindCustomSlidersInPanel(child));
            }

            return customSliders;
        }

        /// <summary>
        /// 在Grid中查找自定义滑块控件
        /// </summary>
        /// <param name="grid">Grid控件</param>
        /// <returns>自定义滑块信息</returns>
        private CustomSliderInfo FindCustomSliderInGrid(Grid grid)
        {
            // 查找包含GnomeSliderThumb图片的Grid
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(grid); i++)
            {
                var child = VisualTreeHelper.GetChild(grid, i);

                if (child is Image image && image.Source != null)
                {
                    var sourceName = image.Source.ToString();
                    if (sourceName.Contains("GnomeSliderThumb"))
                    {
                        // 找到滑块控件，创建自定义滑块信息
                        var customSlider = new CustomSliderInfo
                        {
                            Container = grid,
                            ThumbImage = image,
                            TrackBorder = FindTrackBorderInGrid(grid),
                            ValueBorder = FindValueBorderInGrid(grid)
                        };

                        return customSlider;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 在Grid中查找轨道Border
        /// </summary>
        /// <param name="grid">Grid控件</param>
        /// <returns>轨道Border</returns>
        private Border FindTrackBorderInGrid(Grid grid)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(grid); i++)
            {
                var child = VisualTreeHelper.GetChild(grid, i);

                if (child is Border border && border.Background != null)
                {
                    var brush = border.Background as SolidColorBrush;
                    if (brush != null && brush.Color.ToString() == "#FFDEDEDE")
                    {
                        return border;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 在Grid中查找值显示Border
        /// </summary>
        /// <param name="grid">Grid控件</param>
        /// <returns>值显示Border</returns>
        private Border FindValueBorderInGrid(Grid grid)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(grid); i++)
            {
                var child = VisualTreeHelper.GetChild(grid, i);

                if (child is Border border && border.Background != null)
                {
                    var brush = border.Background as SolidColorBrush;
                    if (brush != null && brush.Color.ToString() == "#FF3584E4")
                    {
                        return border;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 为自定义滑块控件添加触摸支持
        /// </summary>
        /// <param name="customSlider">自定义滑块信息</param>
        private void AddTouchSupportToCustomSlider(CustomSliderInfo customSlider)
        {
            if (customSlider?.Container == null) return;

            // 启用触摸和手写笔支持
            customSlider.Container.IsManipulationEnabled = true;

            // 添加触摸事件
            customSlider.Container.TouchDown += (s, e) => CustomSlider_TouchDown(s, e, customSlider);
            customSlider.Container.TouchMove += (s, e) => CustomSlider_TouchMove(s, e, customSlider);
            customSlider.Container.TouchUp += (s, e) => CustomSlider_TouchUp(s, e, customSlider);

            // 添加手写笔事件
            customSlider.Container.StylusDown += (s, e) => CustomSlider_StylusDown(s, e, customSlider);
            customSlider.Container.StylusMove += (s, e) => CustomSlider_StylusMove(s, e, customSlider);
            customSlider.Container.StylusUp += (s, e) => CustomSlider_StylusUp(s, e, customSlider);

            // 添加操作事件
            customSlider.Container.ManipulationStarted += (s, e) => CustomSlider_ManipulationStarted(s, e, customSlider);
            customSlider.Container.ManipulationDelta += (s, e) => CustomSlider_ManipulationDelta(s, e, customSlider);
            customSlider.Container.ManipulationCompleted += (s, e) => CustomSlider_ManipulationCompleted(s, e, customSlider);
        }

        /// <summary>
        /// 自定义滑块触摸按下事件处理
        /// </summary>
        private void CustomSlider_TouchDown(object sender, TouchEventArgs e, CustomSliderInfo customSlider)
        {
            customSlider.Container.CaptureTouch(e.TouchDevice);
            customSlider.IsTouchCaptured = true;
            var touchPoint = e.GetTouchPoint(customSlider.Container);
            UpdateCustomSliderValueFromPosition(customSlider, touchPoint.Position);
            e.Handled = true;
        }

        /// <summary>
        /// 自定义滑块触摸移动事件处理
        /// </summary>
        private void CustomSlider_TouchMove(object sender, TouchEventArgs e, CustomSliderInfo customSlider)
        {
            // 检查是否有触摸捕获
            if (!customSlider.IsTouchCaptured) return;
            var touchPoint = e.GetTouchPoint(customSlider.Container);
            UpdateCustomSliderValueFromPosition(customSlider, touchPoint.Position);
            e.Handled = true;
        }

        /// <summary>
        /// 自定义滑块触摸释放事件处理
        /// </summary>
        private void CustomSlider_TouchUp(object sender, TouchEventArgs e, CustomSliderInfo customSlider)
        {
            customSlider.Container.ReleaseTouchCapture(e.TouchDevice);
            customSlider.IsTouchCaptured = false;
            e.Handled = true;
        }

        /// <summary>
        /// 自定义滑块手写笔按下事件处理
        /// </summary>
        private void CustomSlider_StylusDown(object sender, StylusDownEventArgs e, CustomSliderInfo customSlider)
        {
            customSlider.Container.CaptureStylus();
            var stylusPoint = e.GetStylusPoints(customSlider.Container);
            if (stylusPoint.Count > 0)
            {
                UpdateCustomSliderValueFromPosition(customSlider, stylusPoint[0].ToPoint());
            }
            e.Handled = true;
        }

        /// <summary>
        /// 自定义滑块手写笔移动事件处理
        /// </summary>
        private void CustomSlider_StylusMove(object sender, StylusEventArgs e, CustomSliderInfo customSlider)
        {
            if (!customSlider.Container.IsStylusCaptured) return;
            var stylusPoint = e.GetStylusPoints(customSlider.Container);
            if (stylusPoint.Count > 0)
            {
                UpdateCustomSliderValueFromPosition(customSlider, stylusPoint[0].ToPoint());
            }
            e.Handled = true;
        }

        /// <summary>
        /// 自定义滑块手写笔释放事件处理
        /// </summary>
        private void CustomSlider_StylusUp(object sender, StylusEventArgs e, CustomSliderInfo customSlider)
        {
            customSlider.Container.ReleaseStylusCapture();
            e.Handled = true;
        }

        /// <summary>
        /// 自定义滑块操作开始事件处理
        /// </summary>
        private void CustomSlider_ManipulationStarted(object sender, ManipulationStartedEventArgs e, CustomSliderInfo customSlider)
        {
            e.Handled = true;
        }

        /// <summary>
        /// 自定义滑块操作变化事件处理
        /// </summary>
        private void CustomSlider_ManipulationDelta(object sender, ManipulationDeltaEventArgs e, CustomSliderInfo customSlider)
        {
            var manipulationOrigin = e.ManipulationOrigin;
            UpdateCustomSliderValueFromPosition(customSlider, manipulationOrigin);
            e.Handled = true;
        }

        /// <summary>
        /// 自定义滑块操作完成事件处理
        /// </summary>
        private void CustomSlider_ManipulationCompleted(object sender, ManipulationCompletedEventArgs e, CustomSliderInfo customSlider)
        {
            e.Handled = true;
        }

        /// <summary>
        /// 根据触摸/手写笔位置更新自定义滑块值
        /// </summary>
        /// <param name="customSlider">自定义滑块信息</param>
        /// <param name="position">触摸/手写笔位置</param>
        private void UpdateCustomSliderValueFromPosition(CustomSliderInfo customSlider, Point position)
        {
            if (customSlider?.TrackBorder == null || customSlider.ThumbImage == null) return;

            try
            {
                // 计算滑块轨道的实际位置和长度
                var trackWidth = customSlider.TrackBorder.ActualWidth;
                if (trackWidth <= 0) return;

                // 考虑拇指大小，计算有效轨道长度
                var thumbSize = 21; // 根据XAML中的Width="21"
                var effectiveWidth = trackWidth - thumbSize;

                // 计算相对位置（0-1之间），考虑拇指大小
                var adjustedX = position.X - thumbSize / 2;
                var relativePosition = Math.Max(0, Math.Min(1, adjustedX / effectiveWidth));

                // 更新滑块位置
                var thumbTransform = customSlider.ThumbImage.RenderTransform as TranslateTransform;
                if (thumbTransform == null)
                {
                    thumbTransform = new TranslateTransform();
                    customSlider.ThumbImage.RenderTransform = thumbTransform;
                }

                // 计算新的滑块位置
                var newX = relativePosition * effectiveWidth;
                thumbTransform.X = newX;

                // 更新值显示Border的宽度
                if (customSlider.ValueBorder != null)
                {
                    var valueWidth = relativePosition * trackWidth;
                    customSlider.ValueBorder.Width = Math.Max(0, valueWidth);

                    // 调整值显示Border的位置
                    var valueMargin = customSlider.ValueBorder.Margin;
                    customSlider.ValueBorder.Margin = new Thickness(0, valueMargin.Top, trackWidth - valueWidth, valueMargin.Bottom);
                }

                // 这里可以根据需要添加值变化事件处理
                // 例如：OnCustomSliderValueChanged(customSlider, relativePosition);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"更新自定义滑块值时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 自定义滑块信息类
        /// </summary>
        private class CustomSliderInfo
        {
            public Grid Container { get; set; }
            public Image ThumbImage { get; set; }
            public Border TrackBorder { get; set; }
            public Border ValueBorder { get; set; }
            public bool IsTouchCaptured { get; set; } = false;
        }

        #endregion
    }
}
