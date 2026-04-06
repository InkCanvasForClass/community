using Ink_Canvas.Windows.SettingsViews.Pages;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Navigation;
using System.Windows.Interop;
using System.Windows.Input;
using System.Linq;
using MessageBox = System.Windows.MessageBox;
using Screen = System.Windows.Forms.Screen;

namespace Ink_Canvas.Windows.SettingsViews
{
    public partial class SettingsWindow : Window
    {
        private readonly Dictionary<string, Type> _pageTypes;
        private readonly Dictionary<string, object> _pages = new Dictionary<string, object>();
        
        // 保存窗口原始位置和大小
        private double _originalLeft;
        private double _originalTop;
        private double _originalWidth;
        private double _originalHeight;
        
        // 标记窗口是否曾经最大化过
        private bool _wasMaximized = false;

        public SettingsWindow()
        {
            InitializeComponent();

            // 初始化内置页面映射
            _pageTypes = new Dictionary<string, Type>
            {
                { "HomePage", typeof(HomePage) },
                { "BasicPage", typeof(BasicPage) },
                { "Page2Page", typeof(Page2Page) },
                { "DesignPage", typeof(DesignPage) },
                { "AppearancePage", typeof(AppearancePage) },
                { "IconographyPage", typeof(IconographyPage) },
                { "TypographyPage", typeof(TypographyPage) },
                { "ThemePage", typeof(ThemePage) },
                { "ColorsPage", typeof(ColorsPage) },
                { "FontsPage", typeof(FontsPage) },
                { "StartupPage", typeof(StartupPage) },
                { "AboutPage", typeof(AboutPage) },
                { "Settings", typeof(SettingsPage) }
            };

            // 默认选中首页
            if (NavigationViewControl.MenuItems.Count > 0)
            {
                // 首先导航到首页
                NavigateToPage("HomePage");
                // 然后选中首页菜单项
                NavigationViewControl.SelectedItem = NavigationViewControl.MenuItems[0];
                NavigationViewControl.Header = "首页";
            }

            // 初始化标题栏边距
            UpdateAppTitleBarMargin();

            // 窗口生命周期事件注册
            this.Loaded += (sender, e) =>
            {
                SetMaxSizeAndCenter();
                RegisterDpiChangedListener();
            };

            // 窗口关闭时释放资源
            this.Closed += (sender, e) =>
            {
                UnregisterDpiChangedListener();
                _pages.Clear();
                _pageTypes.Clear();
            };

            // 修复触摸屏操作后鼠标指针消失的问题
            FixTouchScreenCursorIssue();

            // 窗口状态改变时调整大小限制
            this.StateChanged += (sender, e) =>
            {
                if (this.WindowState == WindowState.Maximized)
                {
                    // 保存窗口原始位置和大小
                    _originalLeft = this.Left;
                    _originalTop = this.Top;
                    _originalWidth = this.Width;
                    _originalHeight = this.Height;
                    
                    // 标记窗口曾经最大化过
                    _wasMaximized = true;
                    
                    // 最大化时清除最大尺寸限制
                    this.MaxWidth = double.PositiveInfinity;
                    this.MaxHeight = double.PositiveInfinity;
                }
                else if (this.WindowState == WindowState.Normal && _wasMaximized)
                {
                    // 从最大化恢复到正常状态时，恢复窗口原始位置和大小
                    this.Left = _originalLeft;
                    this.Top = _originalTop;
                    this.Width = _originalWidth;
                    this.Height = _originalHeight;
                    
                    // 重置标记
                    _wasMaximized = false;
                    
                    // 只设置最大尺寸，不改变窗口位置
                    SetMaxSizeOnly();
                }
                else if (this.WindowState == WindowState.Normal)
                {
                    // 正常状态下只设置最大尺寸限制
                    SetMaxSizeOnly();
                }
                
                // 窗口状态改变时更新标题栏显示
                UpdateAppTitleBarMargin();
            };
            
            // 窗口大小改变时更新标题栏显示
            this.SizeChanged += (sender, e) =>
            {
                if (NavigationViewControl.DisplayMode == NavigationViewDisplayMode.Minimal)
                {
                    UpdateAppTitleBarMargin();
                }
            };
        }

        #region 修复触摸屏鼠标指针消失问题
        private void FixTouchScreenCursorIssue()
        {
            // 触摸结束时强制显示鼠标指针
            this.TouchUp += (s, e) =>
            {
                ShowCursor(true);
            };

            // 鼠标进入窗口时确保指针可见
            this.MouseEnter += (s, e) =>
            {
                ShowCursor(true);
            };

            // 窗口激活时确保指针可见
            this.Activated += (s, e) =>
            {
                ShowCursor(true);
            };
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int ShowCursor(bool bShow);
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
            // 处理自带的设置项导航
            if (args.IsSettingsSelected)
            {
                NavigateToPage("Settings");
                NavigationViewControl.Header = "设置";
                return;
            }

            // 处理普通导航项
            if (args.SelectedItem is NavigationViewItem selectedItem)
            {
                string tag = selectedItem.Tag as string;
                if (!string.IsNullOrEmpty(tag) && _pageTypes.ContainsKey(tag))
                {
                    // 避免重复导航到当前页面
                    if (rootFrame.SourcePageType != _pageTypes[tag])
                    {
                        NavigateToPage(tag);
                    }
                    NavigationViewControl.Header = selectedItem.Content;
                }
            }
        }

        public void NavigateToPage(string pageTag)
        {
            if (!_pageTypes.TryGetValue(pageTag, out Type pageType)) return;

            try
            {
                // 页面缓存：已创建的页面直接复用，保留状态，避免重复初始化
                if (!_pages.TryGetValue(pageTag, out var cachedPage))
                {
                    cachedPage = Activator.CreateInstance(pageType);
                    _pages.Add(pageTag, cachedPage);
                }

                // 导航到缓存页面
                rootFrame.Navigate(cachedPage.GetType(), cachedPage);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导航到页面时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnNavigationViewBackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
        {
            if (rootFrame.CanGoBack) rootFrame.GoBack();
        }

        private void OnRootFrameNavigated(object sender, NavigationEventArgs e)
        {
            // 导航后同步导航项选中状态
            Type currentPageType = rootFrame.SourcePageType;

            // 处理设置项的选中状态
            if (currentPageType == typeof(SettingsPage))
            {
                NavigationViewControl.SelectedItem = NavigationViewControl.SettingsItem;
                NavigationViewControl.Header = "设置";
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
        private void OnControlsSearchBoxQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(args.QueryText)) return;

            string query = args.QueryText.Trim().ToLower();
            var allNavItems = GetAllNavigationItems();

            var targetItem = allNavItems.FirstOrDefault(item =>
                item.Content?.ToString().ToLower().Contains(query) == true);

            if (targetItem != null)
            {
                NavigationViewControl.SelectedItem = targetItem;
            }
        }

        private void OnControlsSearchBoxTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;

            string query = sender.Text.Trim().ToLower();
            var suggestions = new List<string>();

            if (!string.IsNullOrEmpty(query))
            {
                var allNavItems = GetAllNavigationItems();
                suggestions = allNavItems
                    .Where(item => item.Content?.ToString().ToLower().Contains(query) == true)
                    .Select(item => item.Content.ToString())
                    .ToList();
            }

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
        #endregion
    }
}
