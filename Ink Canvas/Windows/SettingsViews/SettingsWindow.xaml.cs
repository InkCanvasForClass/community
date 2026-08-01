using Ink_Canvas.Helpers;
using Ink_Canvas.Properties;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using Ink_Canvas.Windows.SettingsViews.Pages;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;
using Windows.Win32;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;
using Screen = System.Windows.Forms.Screen;

namespace Ink_Canvas.Windows.SettingsViews
{
    public partial class SettingsWindow : Window
    {
        private readonly Dictionary<string, Type> _pageTypes = new Dictionary<string, Type>
        {
            { "HomePage", typeof(HomePage) },
            { "StartupPage", typeof(StartupPage) },
            { "ClockPage", typeof(ClockPage) },
            { "PrivacyPage", typeof(PrivacyPage) },
            { "SecurityPage", typeof(SecurityPage) },
            { "WindowPage", typeof(WindowPage) },
            { "AppearancePage", typeof(AppearancePage) },
            { "SidebarPage", typeof(SidebarPage) },
            { "HotkeyPage", typeof(HotkeyPage) },
            { "ToolbarPage", typeof(ToolbarPage) },
            { "ToolbarAppearancePage", typeof(ToolbarAppearancePage) },
            { "FloatingBarThemePage", typeof(FloatingBarThemePage) },
            { "FloatingBarThemeMarketPage", typeof(FloatingBarThemeMarketPage) },
            { "ToolbarMenuPage", typeof(ToolbarMenuPage) },
            { "BoardToolbarPage", typeof(BoardToolbarPage) },
            { "BoardAppearancePage", typeof(BoardAppearancePage) },
            { "BoardMenuPage", typeof(BoardMenuPage) },
            { "WhiteboardTipsPage", typeof(WhiteboardTipsPage) },
            { "UpdatePage", typeof(UpdatePage) },
            { "NotificationPage", typeof(NotificationPage) },
            { "AnnouncementCenterPage", typeof(AnnouncementCenterPage) },
            { "ExperimentalPage", typeof(ExperimentalPage) },
            { "AdvancedPage", typeof(AdvancedPage) },
            { "StoragePage", typeof(StoragePage) },
            { "BackupPage", typeof(BackupPage) },
            { "CloudStoragePage", typeof(CloudStoragePage) },
            { "AutomationWorkflowPage", typeof(AutomationWorkflowPage) },
            { "PowerPointPage", typeof(PowerPointPage) },
            { "RandomDrawPage", typeof(RandomDrawPage) },
            { "CanvasPage", typeof(CanvasPage) },
            { "InkRecognitionPage", typeof(InkRecognitionPage) },
            { "PerformancePage", typeof(PerformancePage) },
            { "DebugPage", typeof(DebugPage) },
            { "FriendlyLinksPage", typeof(FriendlyLinksPage) },
            { "AboutPage", typeof(AboutPage) },
            { "Settings", typeof(SettingsPage) },
            { "PluginPage", typeof(PluginPage) },
            { "PluginSettingsPage", typeof(PluginSettingsPage) },
            { "PluginMarketplacePage", typeof(PluginMarketplacePage) },
            { "PPTPageFlipPreviewPage", typeof(PPTPageFlipPreviewPage) }
        };
        private readonly Dictionary<string, object> _pages = new Dictionary<string, object>();
        private readonly Dictionary<string, Ink_Canvas.Plugins.PluginInfo> _pluginPages = new Dictionary<string, Ink_Canvas.Plugins.PluginInfo>();

        // 保存窗口原始位置和大小
        private double _originalLeft;
        private double _originalTop;
        private double _originalWidth;
        private double _originalHeight;

        // 标记窗口是否曾经最大化过
        private bool _wasMaximized = false;

        private bool _isNavigating = false;
        private bool _updateBadgeDismissed = false;

        /// <summary>
        /// 若为 true，则跳过 Loaded 中默认导航到 HomePage 的行为。
        /// 用于 URI 打开设置窗口时由调用方在 Show() 之前设置，避免覆盖外部指定的目标页。
        /// </summary>
        public bool SuppressInitialNavigation { get; set; }

        /// <summary>
        /// 待应用的设置项高亮 key。由外部（URI 处理器）设置，在页面 Loaded 完成后触发高亮。
        /// </summary>
        private string _pendingHighlightKey;

        /// <summary>
        /// 由 URI 处理器调用：设置挂起的高亮 key。
        /// 若当前页面已加载，则立即触发；否则推迟到 OnRootFrameNavigated 中的 TryApplyPendingHighlight 处理。
        /// </summary>
        public void SetPendingHighlightKey(string key)
        {
            _pendingHighlightKey = key;
            // 若页面已加载（窗口已打开但用户再次导航），尝试立即触发
            if (rootFrame?.Content is FrameworkElement page && page.IsLoaded)
            {
                TryApplyPendingHighlight();
            }
        }

        public SettingsWindow()
        {
            InitializeComponent();

            ApplyCurrentTheme();
            global::Ink_Canvas.Helpers.WindowBackdropHelper.Apply(this, Helpers.SettingsManager.Settings);

            // 初始化内置页面映射
            _pageTypes = new Dictionary<string, Type>
            {
                { "HomePage", typeof(HomePage) },
                { "StartupPage", typeof(StartupPage) },
                { "ClockPage", typeof(ClockPage) },
                { "PrivacyPage", typeof(PrivacyPage) },
                { "SecurityPage", typeof(SecurityPage) },
                { "WindowPage", typeof(WindowPage) },
                { "AppearancePage", typeof(AppearancePage) },
                { "SidebarPage", typeof(SidebarPage) },
                { "HotkeyPage", typeof(HotkeyPage) },
                { "ToolbarPage", typeof(ToolbarPage) },
                { "ToolbarAppearancePage", typeof(ToolbarAppearancePage) },
            { "FloatingBarThemePage", typeof(FloatingBarThemePage) },
            { "FloatingBarThemeMarketPage", typeof(FloatingBarThemeMarketPage) },
                { "ToolbarMenuPage", typeof(ToolbarMenuPage) },
                { "BoardToolbarPage", typeof(BoardToolbarPage) },
                { "BoardAppearancePage", typeof(BoardAppearancePage) },
                { "BoardMenuPage", typeof(BoardMenuPage) },
                { "WhiteboardTipsPage", typeof(WhiteboardTipsPage) },
                { "UpdatePage", typeof(UpdatePage) },
                { "NotificationPage", typeof(NotificationPage) },
                { "AnnouncementCenterPage", typeof(AnnouncementCenterPage) },
                { "ExperimentalPage", typeof(ExperimentalPage) },
                { "AdvancedPage", typeof(AdvancedPage) },
                { "StoragePage", typeof(StoragePage) },
                { "BackupPage", typeof(BackupPage) },
                { "CloudStoragePage", typeof(CloudStoragePage) },
                { "AutomationWorkflowPage", typeof(AutomationWorkflowPage) },
                { "PowerPointPage", typeof(PowerPointPage) },
                { "RandomDrawPage", typeof(RandomDrawPage) },
                { "CanvasPage", typeof(CanvasPage) },
                { "InkRecognitionPage", typeof(InkRecognitionPage) },
                { "PerformancePage", typeof(PerformancePage) },
                { "DebugPage", typeof(DebugPage) },
                { "FriendlyLinksPage", typeof(FriendlyLinksPage) },
                { "AboutPage", typeof(AboutPage) },
                { "Settings", typeof(SettingsPage) },
                { "PluginPage", typeof(PluginPage) },
                { "PluginSettingsPage", typeof(PluginSettingsPage) },
                { "PluginMarketplacePage", typeof(PluginMarketplacePage) },
                { "PPTPageFlipPreviewPage", typeof(PPTPageFlipPreviewPage) }
            };

            // 初始页面统一在 Loaded 阶段导航，避免构造阶段与深链接导航互相覆盖。
            UpdateAppTitleBarMargin();

            this.Loaded += (sender, e) =>
            {
                SetMaxSizeAndCenter();
                RegisterDpiChangedListener();

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!SuppressInitialNavigation)
                    {
                        NavigateToPage("HomePage");
                        NavigationViewControl.SelectedItem = NavigationViewControl.MenuItems[0];
                        NavigationViewControl.Header = NavStrings.Nav_Home;
                    }

                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        LoadPluginSettingsPages();
                        UpdateUpdateBadgeVisibility();
                        UpdateAnnouncementUnreadBadge();
                        // 绑定设置窗口中的 ToggleSwitch 本地化文本
                        Ink_Canvas.Helpers.LocalizationHelper.BindToggleSwitchesInWindow(this);
                    }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                }), System.Windows.Threading.DispatcherPriority.Normal);

                _ = PreloadAllPagesAsync();
            };

            AnnouncementService.UnreadCountChanged += UpdateAnnouncementUnreadBadge;

            this.Closed += (sender, e) =>
            {
                AnnouncementService.UnreadCountChanged -= UpdateAnnouncementUnreadBadge;
                UnregisterDpiChangedListener();
                _pages.Clear();
                _pageTypes.Clear();
            };

            this.TouchUp += (s, e) => PInvoke.ShowCursor(true);
            this.MouseEnter += (s, e) => PInvoke.ShowCursor(true);
            this.Activated += (s, e) => PInvoke.ShowCursor(true);

            this.StateChanged += (sender, e) =>
            {
                if (this.WindowState == WindowState.Maximized)
                {
                    _originalLeft = this.Left;
                    _originalTop = this.Top;
                    _originalWidth = this.Width;
                    _originalHeight = this.Height;
                    _wasMaximized = true;
                    this.MaxWidth = double.PositiveInfinity;
                    this.MaxHeight = double.PositiveInfinity;
                }
                else if (this.WindowState == WindowState.Normal && _wasMaximized)
                {
                    this.Left = _originalLeft;
                    this.Top = _originalTop;
                    this.Width = _originalWidth;
                    this.Height = _originalHeight;
                    _wasMaximized = false;
                    SetMaxSizeOnly();
                }
                else if (this.WindowState == WindowState.Normal)
                {
                    SetMaxSizeOnly();
                }
                UpdateAppTitleBarMargin();
            };

            this.SizeChanged += (sender, e) =>
            {
                if (NavigationViewControl.DisplayMode == NavigationViewDisplayMode.Minimal)
                {
                    UpdateAppTitleBarMargin();
                }
            };
        }

        public void RefreshTheme()
        {
            ApplyCurrentTheme();
            global::Ink_Canvas.Helpers.WindowBackdropHelper.Apply(this, Helpers.SettingsManager.Settings);
        }

        public void ApplyWindowBackdrop(string backdropName)
        {
            global::Ink_Canvas.Helpers.WindowBackdropHelper.Apply(this, backdropName);
        }

        private void ApplyCurrentTheme()
        {
            try
            {
                int themeIndex = Helpers.SettingsManager.Settings.Appearance.Theme;
                var elementTheme = themeIndex switch
                {
                    0 => iNKORE.UI.WPF.Modern.ElementTheme.Light,
                    1 => iNKORE.UI.WPF.Modern.ElementTheme.Dark,
                    _ => IsSystemThemeLight() ? iNKORE.UI.WPF.Modern.ElementTheme.Light : iNKORE.UI.WPF.Modern.ElementTheme.Dark,
                };
                iNKORE.UI.WPF.Modern.ThemeManager.SetRequestedTheme(this, elementTheme);
            }
            catch { }
        }

        private static bool IsSystemThemeLight()
        {
            try
            {
                using (var themeKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (themeKey?.GetValue("AppsUseLightTheme") is int v) return v == 1;
                }
            }
            catch { }
            return false;
        }

        #region 修复触摸屏鼠标指针消失问题

        //[System.Runtime.InteropServices.DllImport("user32.dll")]
        //private static extern int ShowCursor(bool bShow);
        #endregion

        #region 高DPI/多屏自适应窗口控制

        /// <summary>
        /// 获取当前窗口所在屏幕的工作区尺寸（DIP单位）
        /// </summary>
        private void GetWorkAreaSize(out double workAreaWidthDip, out double workAreaHeightDip, out double screenLeftDip, out double screenTopDip)
        {
            // 1. 获取窗口当前所在屏幕
            var windowHandle = new WindowInteropHelper(this).Handle;
            var currentScreen = Screen.FromHandle(windowHandle);
            var workingArea = currentScreen.WorkingArea;
            var screenBounds = currentScreen.Bounds;

            // 2. 获取当前窗口的DPI缩放因子
            var source = PresentationSource.FromVisual(this);
            double dpiScaleX = 1.0;
            double dpiScaleY = 1.0;

            if (source?.CompositionTarget != null)
            {
                dpiScaleX = source.CompositionTarget.TransformToDevice.M11;
                dpiScaleY = source.CompositionTarget.TransformToDevice.M22;
            }

            // 3. 物理像素 → WPF设备无关像素(DIP)转换
            workAreaWidthDip = workingArea.Width / dpiScaleX;
            workAreaHeightDip = workingArea.Height / dpiScaleY;
            screenLeftDip = screenBounds.Left / dpiScaleX;
            screenTopDip = screenBounds.Top / dpiScaleY;
        }

        private void SetMaxSizeAndCenter()
        {
            if (!this.IsLoaded) return;

            GetWorkAreaSize(out double workAreaWidthDip, out double workAreaHeightDip, out double screenLeftDip, out double screenTopDip);

            // 设置窗口最大尺寸
            this.MaxWidth = workAreaWidthDip;
            this.MaxHeight = workAreaHeightDip;

            // 窗口在当前屏幕居中（解决副屏居中跑偏问题）
            this.Left = screenLeftDip + (workAreaWidthDip - this.ActualWidth) / 2;
            this.Top = screenTopDip + (workAreaHeightDip - this.ActualHeight) / 2;
        }

        private void SetMaxSizeOnly()
        {
            if (!this.IsLoaded) return;

            GetWorkAreaSize(out double workAreaWidthDip, out double workAreaHeightDip, out _, out _);

            // 只设置窗口最大尺寸，不改变窗口位置
            this.MaxWidth = workAreaWidthDip;
            this.MaxHeight = workAreaHeightDip;
        }

        #region DPI/系统缩放变化监听
        private HwndSource _hwndSource;
        private void RegisterDpiChangedListener()
        {
            _hwndSource = PresentationSource.FromVisual(this) as HwndSource;
            _hwndSource?.AddHook(DpiChangedWndProc);
        }

        private void UnregisterDpiChangedListener()
        {
            _hwndSource?.RemoveHook(DpiChangedWndProc);
            _hwndSource = null;
        }

        private IntPtr DpiChangedWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_DPICHANGED = 0x02E0;
            // 系统DPI/缩放变化时自动重新计算窗口参数
            if (msg == WM_DPICHANGED)
            {
                SetMaxSizeAndCenter();
                handled = true;
            }
            return IntPtr.Zero;
        }
        #endregion
        #endregion

        #region 导航逻辑优化（含页面缓存）
        private void OnNavigationViewSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (_isNavigating)
            {
                return;
            }

            if (args.IsSettingsSelected)
            {
                NavigateToPage("Settings");
                NavigationViewControl.Header = NavStrings.Settings_Title;
                return;
            }

            // 处理普通导航项
            if (args.SelectedItem is NavigationViewItem selectedItem)
            {
                string tag = selectedItem.Tag as string;
                if (!string.IsNullOrEmpty(tag) && _pageTypes.ContainsKey(tag))
                {
                    Ink_Canvas.Plugins.PluginInfo pluginInfo = null;
                    _pluginPages.TryGetValue(tag, out pluginInfo);

                    object cachedPage = null;
                    _pages.TryGetValue(tag, out cachedPage);

                    if (cachedPage == null || rootFrame.Content != cachedPage)
                    {
                        NavigateToPage(tag, pluginInfo);
                    }
                    else if (cachedPage is PluginSettingsPage pluginSettingsPage && pluginInfo != null)
                    {
                        pluginSettingsPage.CurrentPlugin = pluginInfo;
                    }
                    NavigationViewControl.Header = selectedItem.Content;

                    if (tag == "UpdatePage")
                    {
                        _updateBadgeDismissed = true;
                        UpdateUpdateBadgeVisibility();
                    }
                }
            }
        }

        public void NavigateToPage(string pageTag, Ink_Canvas.Plugins.PluginInfo pluginInfo = null)
        {
            if (!_pageTypes.TryGetValue(pageTag, out Type pageType))
            {
                LogHelper.WriteLogToFile($"SettingsWindow: NavigateToPage 找不到页面类型 [{pageTag}]，已注册: [{string.Join(", ", _pageTypes.Keys)}]", LogHelper.LogType.Warning);
                return;
            }

            try
            {
                _isNavigating = true;

                if (!_pages.TryGetValue(pageTag, out var cachedPage))
                {
                    cachedPage = Activator.CreateInstance(pageType);
                    _pages.Add(pageTag, cachedPage);
                }

                if (cachedPage is PluginSettingsPage pluginSettingsPage && pluginInfo != null)
                {
                    pluginSettingsPage.CurrentPlugin = pluginInfo;
                }



                rootFrame.NavigationUIVisibility = NavigationUIVisibility.Hidden;
                rootFrame.RemoveBackEntry();
                rootFrame.Navigate(cachedPage);
                rootFrame.RemoveBackEntry();
            }
            catch (Exception ex)
            {
                var detail = ex.ToString();
                if (ex.InnerException != null)
                {
                    detail += "\nInnerException:\n" + ex.InnerException;
                }

                Ink_Canvas.Helpers.LogHelper.WriteLogToFile($"SettingsWindow: 导航到 {pageTag} 异常: {detail}", Ink_Canvas.Helpers.LogHelper.LogType.Error);
                MessageBox.Show(string.Format(NavStrings.Nav_NavigateError, ex.InnerException?.Message ?? ex.Message), NavStrings.Nav_Error, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isNavigating = false;
            }
        }


        private void OnNavigationViewBackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
        {
            if (rootFrame.CanGoBack) rootFrame.GoBack();
        }
        private void OnRootFrameNavigated(object sender, NavigationEventArgs e)
        {
            Type currentPageType = rootFrame.SourcePageType;
            if (currentPageType == typeof(PPTPageFlipPreviewPage))
            {
                NavigationViewControl.PaneDisplayMode = iNKORE.UI.WPF.Modern.Controls.NavigationViewPaneDisplayMode.LeftMinimal;
            }
            else
            {
                NavigationViewControl.PaneDisplayMode = iNKORE.UI.WPF.Modern.Controls.NavigationViewPaneDisplayMode.Auto;
            }

            if (_isNavigating)
            {
                return;
            }

            // 处理设置项的选中状态
            if (currentPageType == typeof(SettingsPage))
            {
                NavigationViewControl.SelectedItem = NavigationViewControl.SettingsItem;
                NavigationViewControl.Header = NavStrings.Settings_Title;
                return;
            }

            // 同步其他页面的选中状态
            foreach (var kvp in _pageTypes)
            {
                if (kvp.Value == currentPageType)
                {
                    var targetItem = FindNavigationViewItemByTag(kvp.Key);
                    if (targetItem != null && NavigationViewControl.SelectedItem != targetItem)
                    {
                        NavigationViewControl.SelectedItem = targetItem;
                        NavigationViewControl.Header = targetItem.Content;
                    }
                    break;
                }
            }

            // 重置当前页面的选中设置项（页面可在 Loaded 中再设置）

            ApplySmoothScrollingToPage(e.Content as FrameworkElement);
            HookSettingsCardInputHandlers(e.Content as FrameworkElement);

            // 应用 URI 处理器留下的待处理高亮 key（等待页面 Loaded 完成，确保可视树已构建）
            TryApplyPendingHighlight();

            // 如果导航到了浮动栏主题管理页，确保刷新主题列表（比如从主题市场安装后返回能立即看到）
            try
            {
                if (currentPageType == typeof(FloatingBarThemePage))
                {
                    (rootFrame.Content as FloatingBarThemePage)?.RefreshThemes();
                }
            }
            catch { }
        }

        /// <summary>
        /// 允许外部调用以刷新设置窗口中的浮动栏主题管理页（如果当前正在显示）
        /// </summary>
        public void RefreshFloatingBarThemePage()
        {
            try
            {
                (rootFrame.Content as FloatingBarThemePage)?.RefreshThemes();
            }
            catch { }
        }

        /// <summary>
        /// 如果有挂起的高亮 key，等待设置窗口 + 页面都加载并渲染完成后才触发高亮。
        /// </summary>
        private void TryApplyPendingHighlight()
        {
            if (string.IsNullOrEmpty(_pendingHighlightKey)) return;
            if (rootFrame?.Content is not FrameworkElement page) return;

            var pendingKey = _pendingHighlightKey;
            _pendingHighlightKey = null;

            // 等窗口与页面都 Loaded 后，再依次延迟到 ContextIdle（模板应用完）+ Background（渲染完）才触发高亮
            void TriggerHighlight()
            {
                // 第一段延迟：等模板/子元素生成完
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    // 第二段延迟：等渲染稳定
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        // 第三段延迟：再让出一帧，保证滚动条 BringIntoView 已生效
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            HighlightSetting(pendingKey);
                        }), DispatcherPriority.Background);
                    }), DispatcherPriority.ContextIdle);
                }), DispatcherPriority.ContextIdle);
            }

            void OnPageLoaded(object s, RoutedEventArgs e)
            {
                page.Loaded -= OnPageLoaded;
                TriggerHighlight();
            }

            if (page.IsLoaded && this.IsLoaded)
            {
                TriggerHighlight();
            }
            else
            {
                page.Loaded += OnPageLoaded;
                // 兜底：若页面已 Loaded 但窗口未 Loaded，等待窗口 Loaded
                if (!page.IsLoaded)
                {
                    // nothing — page.Loaded 会触发
                }
                else if (!this.IsLoaded)
                {
                    RoutedEventHandler onWindowLoaded = null;
                    onWindowLoaded = (ws, we) =>
                    {
                        this.Loaded -= onWindowLoaded;
                        TriggerHighlight();
                    };
                    this.Loaded += onWindowLoaded;
                }
            }
        }

        private void ApplySmoothScrollingToPage(FrameworkElement root)
        {
            if (root == null) return;

            var queue = new Queue<DependencyObject>();
            if (root is DependencyObject rootDep) queue.Enqueue(rootDep);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                if (current is ScrollViewer sv)
                {
                    sv.PanningMode = PanningMode.VerticalOnly;
                    sv.PanningDeceleration = 0.001;
                    sv.PanningRatio = 1;
                    sv.ManipulationBoundaryFeedback += (s, e) => e.Handled = true;
                }

                var children = LogicalTreeHelper.GetChildren(current);
                foreach (var child in children)
                {
                    if (child is DependencyObject childDep)
                        queue.Enqueue(childDep);
                }
            }
        }

        private void NavigationViewControl_DisplayModeChanged(NavigationView sender, NavigationViewDisplayModeChangedEventArgs args)
        {
            UpdateAppTitleBarMargin(sender);
        }

        private void UpdateAppTitleBarMargin()
        {
            UpdateAppTitleBarMargin(NavigationViewControl);
        }

        private void UpdateAppTitleBarMargin(NavigationView sender)
        {
            Thickness currMargin = AppTitleBar.Margin;
            if (sender.DisplayMode == NavigationViewDisplayMode.Minimal)
            {
                AppTitleBar.Margin = new Thickness((sender.CompactPaneLength * 2), currMargin.Top, currMargin.Right, currMargin.Bottom);

                // 当窗口宽度非常小时，隐藏图标和应用设置文字
                if (this.ActualWidth < 400)
                {
                    AppTitle.Visibility = Visibility.Collapsed;
                }
                else
                {
                    AppTitle.Visibility = Visibility.Visible;
                }
            }
            else
            {
                AppTitleBar.Margin = new Thickness(sender.CompactPaneLength, currMargin.Top, currMargin.Right, currMargin.Bottom);
                AppTitle.Visibility = Visibility.Visible;
            }
            AppTitleBar.Visibility = sender.PaneDisplayMode == NavigationViewPaneDisplayMode.Top ? Visibility.Collapsed : Visibility.Visible;
        }

        private NavigationViewItem FindNavigationViewItemByTag(string tag)
        {
            // 遍历主菜单
            foreach (var item in NavigationViewControl.MenuItems)
            {
                if (item is NavigationViewItem navItem)
                {
                    if (navItem.Tag as string == tag)
                        return navItem;

                    // 遍历子菜单，自动展开父项
                    foreach (var childItem in navItem.MenuItems)
                    {
                        if (childItem is NavigationViewItem childNavItem && childNavItem.Tag as string == tag)
                        {
                            navItem.IsExpanded = true;
                            return childNavItem;
                        }
                    }
                }
            }

            // 遍历底部菜单
            foreach (var item in NavigationViewControl.FooterMenuItems)
            {
                if (item is NavigationViewItem navItem && navItem.Tag as string == tag)
                {
                    return navItem;
                }
            }

            return null;
        }
        #endregion

        #region 搜索框逻辑优化

        private sealed class SearchEntry
        {
            public string Text;
            public string PageTag;
            public string SettingKey;
            public WeakReference<FrameworkElement> Target;
        }

        private List<SearchEntry> _searchIndex;
        private bool _indexBuilt;

        private void EnsureSearchIndexBuilt()
        {
            if (_indexBuilt && _searchIndex != null) return;
            _searchIndex = new List<SearchEntry>(256);

            foreach (var item in GetAllNavigationItems())
            {
                var text = item.Content?.ToString();
                var tag = item.Tag as string;
                if (!string.IsNullOrWhiteSpace(text) && !string.IsNullOrEmpty(tag))
                {
                    _searchIndex.Add(new SearchEntry { Text = text.Trim(), PageTag = tag });
                }
            }

            foreach (var kv in _pageTypes.ToList())
            {
                var tag = kv.Key;
                if (tag == "Settings") continue;
                if (kv.Value == typeof(PluginSettingsPage)) continue;

                try
                {
                    if (!_pages.TryGetValue(tag, out var page))
                    {
                        page = Activator.CreateInstance(kv.Value);
                        _pages[tag] = page;
                    }
                    if (page is FrameworkElement feRoot)
                    {
                        if (!feRoot.IsLoaded)
                        {
                            try { feRoot.ApplyTemplate(); } catch { }
                        }
                        CollectEntriesFromPage(feRoot, tag);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(string.Format(NavStrings.Nav_IndexBuildFailed, tag, ex.Message));
                }
            }

            foreach (var kv in _pluginPages)
            {
                var pageTag = kv.Key;
                var info = kv.Value;
                var name = info?.Name;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    _searchIndex.Add(new SearchEntry { Text = string.Format(NavStrings.Nav_PluginSettingsFormat, name), PageTag = pageTag });
                }
            }

            _indexBuilt = true;
        }

        private void CollectEntriesFromPage(DependencyObject root, string pageTag)
        {
            foreach (var node in EnumerateLogicalDescendants(root))
            {
                string header = null;
                FrameworkElement target = node as FrameworkElement;

                if (node is Ink_Canvas.Controls.LabeledSettingsCard lsc)
                {
                    header = lsc.Header;
                }
                else if (node is iNKORE.UI.WPF.Modern.Controls.SettingsCard sc)
                {
                    header = sc.Header?.ToString();
                }
                else if (node is iNKORE.UI.WPF.Modern.Controls.SettingsExpander se)
                {
                    header = se.Header?.ToString();
                }

                if (!string.IsNullOrWhiteSpace(header) && target != null)
                {
                    var key = SettingsNavigator.GetSettingsKey(target);
                    _searchIndex.Add(new SearchEntry
                    {
                        Text = header.Trim(),
                        PageTag = pageTag,
                        SettingKey = string.IsNullOrEmpty(key) ? null : key,
                        Target = new WeakReference<FrameworkElement>(target)
                    });
                }
            }
        }

        private static IEnumerable<DependencyObject> EnumerateLogicalDescendants(DependencyObject root)
        {
            if (root == null) yield break;
            var stack = new Stack<DependencyObject>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var node = stack.Pop();
                yield return node;
                foreach (var child in LogicalTreeHelper.GetChildren(node))
                {
                    if (child is DependencyObject d) stack.Push(d);
                }
            }
        }

        private void NavigateToSearchEntry(SearchEntry entry)
        {
            if (entry == null) return;

            NavigateToPage(entry.PageTag);
            var navItem = FindNavigationViewItemByTag(entry.PageTag);
            if (navItem != null && NavigationViewControl.SelectedItem != navItem)
            {
                NavigationViewControl.SelectedItem = navItem;
                NavigationViewControl.Header = navItem.Content;
            }

            if (entry.Target != null && entry.Target.TryGetTarget(out var fe))
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try { fe.BringIntoView(); } catch { }
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        private void OnControlsSearchBoxQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            EnsureSearchIndexBuilt();

            string raw = (args.ChosenSuggestion as string) ?? args.QueryText;
            if (string.IsNullOrWhiteSpace(raw)) return;

            string query = raw.Trim();

            var entry = _searchIndex.FirstOrDefault(e => e.Text.Equals(query, StringComparison.OrdinalIgnoreCase))
                        ?? _searchIndex.FirstOrDefault(e => e.Text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);

            NavigateToSearchEntry(entry);
        }

        private void OnControlsSearchBoxTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;

            EnsureSearchIndexBuilt();

            string query = sender.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(query))
            {
                sender.ItemsSource = null;
                return;
            }

            var suggestions = _searchIndex
                .Where(e => e.Text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(e => e.Text)
                .Distinct()
                .Take(50)
                .ToList();

            sender.ItemsSource = suggestions;
        }

        // 统一获取所有导航项（主菜单+子菜单+底部菜单）
        private List<NavigationViewItem> GetAllNavigationItems()
        {
            var items = new List<NavigationViewItem>();

            // 主菜单+子菜单
            foreach (var item in NavigationViewControl.MenuItems)
            {
                if (item is NavigationViewItem navItem)
                {
                    items.Add(navItem);
                    foreach (var child in navItem.MenuItems)
                    {
                        if (child is NavigationViewItem childNavItem)
                            items.Add(childNavItem);
                    }
                }
            }

            // 底部菜单
            foreach (var item in NavigationViewControl.FooterMenuItems)
            {
                if (item is NavigationViewItem navItem)
                    items.Add(navItem);
            }

            return items;
        }

        private void LoadPluginSettingsPages()
        {
            var pluginManager = Ink_Canvas.Plugins.PluginManager.Instance;
            var plugins = pluginManager.Plugins;

            foreach (var plugin in plugins)
            {
                // \u5355\u4E2A\u63D2\u4EF6\u7684\u8BBE\u7F6E\u9875\u5931\u8D25\uFF08\u7F3A\u4F9D\u8D56\u3001XAML \u89E3\u6790\u5F02\u5E38\u7B49\uFF09\u4E0D\u5E94\u4E2D\u6B62\u5176\u5B83\u63D2\u4EF6\u7684\u8BBE\u7F6E\u9875\u52A0\u8F7D\u3002
                // \u4E4B\u524D\u7684\u6574\u4F53 try/catch \u4F1A\u8BA9\u6392\u5728\u5931\u8D25\u63D2\u4EF6\u4E4B\u540E\u7684\u6240\u6709\u63D2\u4EF6\u8BBE\u7F6E\u9875\u90FD\u51FA\u4E0D\u6765\u3002
                try
                {
                    if (plugin.Instance == null) continue;

                    var settingsView = plugin.Instance.GetSettingsView();
                    if (settingsView != null)
                    {
                        var pageTag = string.Format("PluginSettings_{0}", plugin.Id);

                        _pageTypes[pageTag] = typeof(PluginSettingsPage);
                        _pluginPages[pageTag] = plugin;

                        var navItem = new NavigationViewItem
                        {
                            Content = string.Format(NavStrings.Nav_PluginSettingsFormat, plugin.Name),
                            Tag = pageTag
                        };

                        navItem.Icon = new FontIcon
                        {
                            Glyph = "\uE713"
                        };

                        NavigationViewControl.MenuItems.Add(navItem);
                    }
                }
                catch (Exception ex)
                {
                    pluginManager.LogError(string.Format(
                        NavStrings.Nav_LoadPluginSettingsFailed, plugin.Name + ": " + ex.Message), ex);
                }
            }
        }
        #endregion

        public NavigationView GetNavigationView()
        {
            return NavigationViewControl;
        }

        /// <summary>
        /// 构造当前页面（或指定页面）的设置导航 URL。
        /// </summary>
        public string BuildSettingsUri(string pageTag = null, string settingKey = null)
        {
            pageTag = string.IsNullOrEmpty(pageTag) ? GetCurrentPageTag() : pageTag;
            if (string.IsNullOrEmpty(pageTag)) pageTag = "HomePage";

            string url = "icc://settings/" + Uri.EscapeDataString(pageTag);
            if (!string.IsNullOrEmpty(settingKey))
            {
                url += "?key=" + Uri.EscapeDataString(settingKey);
            }
            return url;
        }

        /// <summary>
        /// 获取当前 Frame 显示的页面 tag（与 _pageTypes 一致）。
        /// </summary>
        private string GetCurrentPageTag()
        {
            var t = rootFrame?.SourcePageType;
            if (t == null) return null;
            foreach (var kv in _pageTypes)
            {
                if (kv.Value == t) return kv.Key;
            }
            return null;
        }

        /// <summary>
        /// 滚动到目标设置项并临时高亮。优先按 SettingsNavigator.SettingsKey 查找；若未找到则按 Header 文本匹配。
        /// </summary>
        public void HighlightSetting(string settingKey)
        {
            if (string.IsNullOrEmpty(settingKey) || rootFrame?.Content is not FrameworkElement root)
                return;

            try
            {
                EnsureSearchIndexBuilt();

                var entry = _searchIndex?.FirstOrDefault(e =>
                    string.Equals(e.PageTag, GetCurrentPageTag(), StringComparison.OrdinalIgnoreCase)
                    && string.Equals(e.SettingKey, settingKey, StringComparison.OrdinalIgnoreCase));

                if (entry != null && entry.Target != null && entry.Target.TryGetTarget(out var fe))
                {
                    FlashHighlight(fe);
                    return;
                }

                // 退路 1：手动遍历当前页面逻辑树，按 SettingsKey 匹配
                FrameworkElement match = null;
                foreach (var node in EnumerateLogicalDescendants(root))
                {
                    if (node is FrameworkElement fe2)
                    {
                        var key = SettingsNavigator.GetSettingsKey(fe2);
                        if (string.Equals(key, settingKey, StringComparison.OrdinalIgnoreCase))
                        {
                            match = fe2;
                            break;
                        }
                    }
                }

                if (match != null)
                {
                    FlashHighlight(match);
                    return;
                }

                // 退路 2：按 Header 文本匹配（用于无 SettingsKey 的设置项）
                foreach (var node in EnumerateLogicalDescendants(root))
                {
                    if (node is FrameworkElement fe3)
                    {
                        var header = GetSettingsHeaderText(fe3);
                        if (!string.IsNullOrEmpty(header) &&
                            string.Equals(header, settingKey, StringComparison.OrdinalIgnoreCase))
                        {
                            FlashHighlight(fe3);
                            return;
                        }
                    }
                }

                LogHelper.WriteLogToFile($"HighlightSetting: 未在当前页找到设置项 [{settingKey}]", LogHelper.LogType.Warning);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"HighlightSetting 异常: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        private void FlashHighlight(FrameworkElement target)
        {
            try { target.BringIntoView(); } catch { }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    var layer = AdornerLayer.GetAdornerLayer(target);
                    if (layer == null)
                    {
                        // 退路：无 AdornerLayer 时使用 Effect 闪两次
                        FlashWithEffect(target);
                        return;
                    }

                    var adorner = new HighlightAdorner(target);
                    layer.Add(adorner);

                    int count = 0;
                    var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
                    timer.Tick += (s, e) =>
                    {
                        try
                        {
                            count++;
                            // 切换可见性：1=隐藏 2=显示 3=隐藏 4=移除（共闪两次）
                            if (count == 1 || count == 3)
                            {
                                adorner.Visibility = Visibility.Hidden;
                            }
                            else if (count == 2)
                            {
                                adorner.Visibility = Visibility.Visible;
                            }
                            else if (count >= 4)
                            {
                                timer.Stop();
                                layer.Remove(adorner);
                            }
                        }
                        catch { }
                    };
                    timer.Start();
                }
                catch { }
            }), DispatcherPriority.Background);
        }

        /// <summary>
        /// 退路：用 DropShadowEffect 闪烁两次。适用于无 AdornerLayer 的元素。
        /// </summary>
        private void FlashWithEffect(FrameworkElement target)
        {
            try
            {
                var originalEffect = target.Effect;
                System.Windows.Media.Effects.DropShadowEffect MakeGlow() => new()
                {
                    Color = Colors.OrangeRed,
                    BlurRadius = 30,
                    ShadowDepth = 0,
                    Opacity = 1
                };

                target.Effect = MakeGlow();
                int count = 0;
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
                timer.Tick += (s, e) =>
                {
                    count++;
                    if (count == 1 || count == 3)
                        target.Effect = originalEffect;
                    else if (count == 2)
                        target.Effect = MakeGlow();
                    else
                    {
                        timer.Stop();
                        target.Effect = originalEffect;
                    }
                };
                timer.Start();
            }
            catch { }
        }

        /// <summary>
        /// Adorner：在目标元素上绘制橙色高亮边框。
        /// </summary>
        private sealed class HighlightAdorner : System.Windows.Documents.Adorner
        {
            private readonly Pen _pen;
            private readonly Brush _fill;

            public HighlightAdorner(UIElement adornedElement) : base(adornedElement)
            {
                _pen = new Pen(new SolidColorBrush(Color.FromRgb(255, 140, 0)), 3);
                _pen.Freeze();
                _fill = new SolidColorBrush(Color.FromArgb(40, 255, 140, 0));
                _fill.Freeze();
                IsHitTestVisible = false;
            }

            protected override void OnRender(System.Windows.Media.DrawingContext drawingContext)
            {
                var rect = new Rect(new Point(0, 0), AdornedElement.RenderSize);
                drawingContext.DrawRectangle(_fill, _pen, rect);
            }
        }

        #region 右键/长按复制设置项 URL

        // 长按计时器与判定阈值
        private const int LongPressThresholdMs = 600;
        private const double LongPressMoveTolerance = 10.0;
        private DispatcherTimer _longPressTimer;
        private TouchPoint _longPressStartPoint;
        private bool _longPressFired;

        /// <summary>
        /// 为已导航页面挂载 SettingsCard / SettingsExpander / LabeledSettingsCard 的右键和触摸长按事件。
        /// </summary>
        private void HookSettingsCardInputHandlers(FrameworkElement root)
        {
            if (root == null) return;
            try
            {
                root.PreviewMouseRightButtonUp -= SettingsCard_RightButtonUp;
                root.PreviewMouseRightButtonUp += SettingsCard_RightButtonUp;

                root.PreviewTouchDown -= SettingsCard_TouchDown;
                root.PreviewTouchDown += SettingsCard_TouchDown;

                root.PreviewTouchMove -= SettingsCard_TouchMove;
                root.PreviewTouchMove += SettingsCard_TouchMove;

                root.PreviewTouchUp -= SettingsCard_TouchUp;
                root.PreviewTouchUp += SettingsCard_TouchUp;
            }
            catch { }
        }

        private void SettingsCard_RightButtonUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var source = e.OriginalSource as DependencyObject;
                var target = FindSettingsContainer(source);
                if (target == null) return;

                // 不阻止默认右键菜单行为完全（如未来需要保留右键菜单），仅触发复制动作
                CopySettingUriFromElement(target);
                e.Handled = true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"右键复制设置 URL 失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        private void SettingsCard_TouchDown(object sender, TouchEventArgs e)
        {
            try
            {
                var source = e.OriginalSource as DependencyObject;
                var target = FindSettingsContainer(source);
                if (target == null) return;

                _longPressFired = false;
                _longPressStartPoint = e.GetTouchPoint(null);

                _longPressTimer?.Stop();
                _longPressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(LongPressThresholdMs) };
                _longPressTimer.Tick += (s, args) =>
                {
                    _longPressTimer?.Stop();
                    if (_longPressFired) return;
                    _longPressFired = true;

                    try
                    {
                        // 长按触发后取消后续的触摸提升（避免立即触发点击）
                        e.TouchDevice.Capture(null);
                    }
                    catch { }

                    CopySettingUriFromElement(target);
                };
                _longPressTimer.Start();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"触摸长按起始处理失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        private void SettingsCard_TouchMove(object sender, TouchEventArgs e)
        {
            try
            {
                if (_longPressTimer == null || !_longPressTimer.IsEnabled) return;
                var current = e.GetTouchPoint(null);
                if (_longPressStartPoint == null) return;

                double dx = current.Position.X - _longPressStartPoint.Position.X;
                double dy = current.Position.Y - _longPressStartPoint.Position.Y;
                if (Math.Sqrt(dx * dx + dy * dy) > LongPressMoveTolerance)
                {
                    _longPressTimer.Stop();
                }
            }
            catch { }
        }

        private void SettingsCard_TouchUp(object sender, TouchEventArgs e)
        {
            try
            {
                _longPressTimer?.Stop();
                _longPressTimer = null;
            }
            catch { }
        }

        /// <summary>
        /// 沿可视树向上查找最近的 SettingsCard / SettingsExpander / LabeledSettingsCard。
        /// </summary>
        private FrameworkElement FindSettingsContainer(DependencyObject source)
        {
            var current = source;
            while (current != null)
            {
                if (current is Ink_Canvas.Controls.LabeledSettingsCard lsc)
                    return lsc;
                if (current is iNKORE.UI.WPF.Modern.Controls.SettingsCard sc)
                    return sc;
                if (current is iNKORE.UI.WPF.Modern.Controls.SettingsExpander se)
                    return se;

                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        /// <summary>
        /// 提取 Header 文本（或 SettingsKey），构造 URL 并复制到剪贴板，弹出通知。
        /// </summary>
        private void CopySettingUriFromElement(FrameworkElement target)
        {
            try
            {
                string key = SettingsNavigator.GetSettingsKey(target);
                if (string.IsNullOrEmpty(key))
                {
                    key = GetSettingsHeaderText(target);
                }

                string pageTag = GetCurrentPageTag();
                string uri = BuildSettingsUri(pageTag, key);

                try { Clipboard.SetText(uri); } catch { }

                ShowCopyUriInfoBar();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"复制设置项 URL 失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        private static string GetSettingsHeaderText(FrameworkElement target)
        {
            try
            {
                if (target is Ink_Canvas.Controls.LabeledSettingsCard lsc)
                    return lsc.Header?.Trim();
                if (target is iNKORE.UI.WPF.Modern.Controls.SettingsCard sc)
                    return (sc.Header as string)?.Trim() ?? sc.Header?.ToString()?.Trim();
                if (target is iNKORE.UI.WPF.Modern.Controls.SettingsExpander se)
                    return (se.Header as string)?.Trim() ?? se.Header?.ToString()?.Trim();
            }
            catch { }
            return null;
        }

        private DispatcherTimer _copyUriInfoBarTimer;

        private void ShowCopyUriInfoBar()
        {
            try
            {
                if (CopyUriInfoBar == null) return;

                CopyUriInfoBar.Message = NavStrings.Nav_CopySettingsUri_Copied;
                CopyUriInfoBar.IsOpen = true;
                CopyUriInfoBar.Visibility = Visibility.Visible;

                _copyUriInfoBarTimer?.Stop();
                _copyUriInfoBarTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
                _copyUriInfoBarTimer.Tick += (s, e) =>
                {
                    _copyUriInfoBarTimer.Stop();
                    try
                    {
                        CopyUriInfoBar.IsOpen = false;
                        CopyUriInfoBar.Visibility = Visibility.Collapsed;
                    }
                    catch { }
                };
                _copyUriInfoBarTimer.Start();
            }
            catch { }
        }

        #endregion

        private async System.Threading.Tasks.Task PreloadAllPagesAsync()
        {
            await System.Threading.Tasks.Task.Delay(1000);

            try
            {
                var tags = _pageTypes.Keys.ToList();
                int count = 0;
                foreach (var tag in tags)
                {
                    if (_pages.ContainsKey(tag))
                        continue;
                    if (!_pageTypes.TryGetValue(tag, out var type))
                        continue;
                    if (type == typeof(PluginSettingsPage))
                        continue;

                    try
                    {
                        if (!_pages.ContainsKey(tag))
                        {
                            var page = Activator.CreateInstance(type);
                            _pages[tag] = page;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(string.Format(NavStrings.Nav_PreloadPageFailed, tag, ex.Message));
                    }

                    // 每加载一页后让出 UI 线程，防止阻塞心跳定时器
                    if (++count % 3 == 0)
                    {
                        await System.Threading.Tasks.Task.Delay(50);
                    }
                    else
                    {
                        await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format(NavStrings.Nav_PreloadPagesFailed, ex.Message));
            }
        }

        public void UpdateUpdateBadgeVisibility()
        {
            try
            {
                var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                bool hasUpdate = mainWindow != null && !string.IsNullOrEmpty(mainWindow.AvailableLatestVersion);
                var item = FindNavigationViewItemByTag("UpdatePage");
                var badge = item?.InfoBadge;
                if (badge != null)
                {
                    badge.Visibility = (hasUpdate && !_updateBadgeDismissed) ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            catch { }
        }

        public void UpdateAnnouncementUnreadBadge()
        {
            Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    var count = AnnouncementService.GetUnreadCount(Helpers.SettingsManager.Settings);
                    if (AnnouncementUnreadInfoBadge != null)
                    {
                        AnnouncementUnreadInfoBadge.Value = count;
                        AnnouncementUnreadInfoBadge.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
                    }
                }
                catch { }
            });
        }
    }
}
