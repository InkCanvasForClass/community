using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Shell;

namespace WpfUiCompat.Helpers
{
    /// <summary>
    /// 窗口标题栏附加属性（兼容 iNKORE TitleBar）：ExtendViewIntoTitleBar、Height、SystemOverlayRightInset 等。
    /// 通过 WindowChrome 将标题区域扩展到客户区，同时保留系统标题按钮。
    /// </summary>
    public static class TitleBar
    {
        internal const double DefaultHeight = 48d;

        #region Height

        public static readonly DependencyProperty HeightProperty =
            DependencyProperty.RegisterAttached(
                "Height",
                typeof(double),
                typeof(TitleBar),
                new PropertyMetadata(DefaultHeight, OnTitleBarPropertyChanged));

        public static double GetHeight(Window window)
        {
            return (double)window.GetValue(HeightProperty);
        }

        public static void SetHeight(Window window, double value)
        {
            window.SetValue(HeightProperty, value);
        }

        #endregion

        #region ExtendViewIntoTitleBar

        public static readonly DependencyProperty ExtendViewIntoTitleBarProperty =
            DependencyProperty.RegisterAttached(
                "ExtendViewIntoTitleBar",
                typeof(bool),
                typeof(TitleBar),
                new PropertyMetadata(false, OnTitleBarPropertyChanged));

        public static bool GetExtendViewIntoTitleBar(Window window)
        {
            return (bool)window.GetValue(ExtendViewIntoTitleBarProperty);
        }

        public static void SetExtendViewIntoTitleBar(Window window, bool value)
        {
            window.SetValue(ExtendViewIntoTitleBarProperty, value);
        }

        #endregion

        #region SystemOverlayLeftInset

        public static readonly DependencyProperty SystemOverlayLeftInsetProperty =
            DependencyProperty.RegisterAttached(
                "SystemOverlayLeftInset",
                typeof(double),
                typeof(TitleBar),
                new PropertyMetadata(12d));

        public static double GetSystemOverlayLeftInset(Window window)
        {
            return (double)window.GetValue(SystemOverlayLeftInsetProperty);
        }

        #endregion

        #region SystemOverlayRightInset

        public static readonly DependencyProperty SystemOverlayRightInsetProperty =
            DependencyProperty.RegisterAttached(
                "SystemOverlayRightInset",
                typeof(double),
                typeof(TitleBar),
                new PropertyMetadata(138d));

        public static double GetSystemOverlayRightInset(Window window)
        {
            return (double)window.GetValue(SystemOverlayRightInsetProperty);
        }

        internal static void SetSystemOverlayRightInset(Window window, double value)
        {
            window.SetValue(SystemOverlayRightInsetProperty, value);
        }

        #endregion

        private static void OnTitleBarPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Window window)
            {
                WindowHelper.UpdateWindowChrome(window);
            }
        }
    }

    /// <summary>
    /// 窗口附加属性（兼容 iNKORE WindowHelper）：UseModernWindowStyle、SystemBackdropType、CornerStyle。
    /// </summary>
    public static class WindowHelper
    {
        #region UseModernWindowStyle

        public static readonly DependencyProperty UseModernWindowStyleProperty =
            DependencyProperty.RegisterAttached(
                "UseModernWindowStyle",
                typeof(bool),
                typeof(WindowHelper),
                new PropertyMetadata(false, OnUseModernWindowStyleChanged));

        public static void SetUseModernWindowStyle(Window window, bool value)
        {
            window?.SetValue(UseModernWindowStyleProperty, value);
        }

        public static bool GetUseModernWindowStyle(Window window)
        {
            return window != null && (bool)window.GetValue(UseModernWindowStyleProperty);
        }

        private static void OnUseModernWindowStyleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not Window window) return;

            void ApplyDarkMode()
            {
                var theme = ThemeManager.GetActualTheme(window);
                if (theme == ElementTheme.Dark)
                {
                    BackdropHelper.ApplyDarkMode(window);
                }
                else
                {
                    BackdropHelper.RemoveDarkMode(window);
                }
            }

            if ((bool)e.NewValue)
            {
                ApplyDarkMode();

                void OnLoaded(object sender, RoutedEventArgs args)
                {
                    UpdateWindowChrome(window);
                    // 移除系统默认标题栏图标区（保留系统按钮）
                    try
                    {
                        var handle = new WindowInteropHelper(window).Handle;
                        if (handle != IntPtr.Zero)
                        {
                            NativeMethods.SetWindowLong(handle, -16, NativeMethods.GetWindowLong(handle, -16) & ~0x80000);
                        }
                    }
                    catch { }
                }

                if (window.IsLoaded)
                {
                    OnLoaded(null, null);
                }
                else
                {
                    window.Loaded -= OnLoaded;
                    window.Loaded += OnLoaded;
                }

                ThemeManager.ActualThemeChanged += (s, args) => window.Dispatcher.BeginInvoke(ApplyDarkMode);
            }
        }

        #endregion

        #region SystemBackdropType

        public static readonly DependencyProperty SystemBackdropTypeProperty =
            DependencyProperty.RegisterAttached(
                "SystemBackdropType",
                typeof(BackdropType),
                typeof(WindowHelper),
                new PropertyMetadata(BackdropType.None, OnSystemBackdropTypeChanged));

        public static void SetSystemBackdropType(Window window, BackdropType value)
        {
            window?.SetValue(SystemBackdropTypeProperty, value);
        }

        public static BackdropType GetSystemBackdropType(Window window)
        {
            return window == null ? BackdropType.None : (BackdropType)window.GetValue(SystemBackdropTypeProperty);
        }

        private static void OnSystemBackdropTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Window window)
            {
                window.Loaded -= OnWindowLoadedForBackdrop;
                window.Loaded += OnWindowLoadedForBackdrop;
                if (window.IsLoaded)
                {
                    OnWindowLoadedForBackdrop(window, null);
                }
                UpdateWindowChrome(window);
            }
        }

        private static void OnWindowLoadedForBackdrop(object sender, RoutedEventArgs e)
        {
            if (sender is Window window)
            {
                BackdropHelper.Apply(window, GetSystemBackdropType(window), true);
            }
        }

        #endregion

        #region CornerStyle

        public static readonly DependencyProperty CornerStyleProperty =
            DependencyProperty.RegisterAttached(
                "CornerStyle",
                typeof(WindowCornerStyle),
                typeof(WindowHelper),
                new PropertyMetadata(WindowCornerStyle.DoNotApply, OnCornerStyleChanged));

        public static void SetCornerStyle(Window window, WindowCornerStyle value)
        {
            window?.SetValue(CornerStyleProperty, value);
        }

        public static WindowCornerStyle GetCornerStyle(Window window)
        {
            return window == null ? WindowCornerStyle.DoNotApply : (WindowCornerStyle)window.GetValue(CornerStyleProperty);
        }

        private static void OnCornerStyleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not Window window) return;

            void Apply()
            {
                try
                {
                    var handle = new WindowInteropHelper(window).Handle;
                    if (handle == IntPtr.Zero) return;

                    int preference = (WindowCornerStyle)e.NewValue switch
                    {
                        WindowCornerStyle.Round => 2,      // DWMWCP_ROUND
                        WindowCornerStyle.RoundSmall => 3, // DWMWCP_ROUNDSMALL
                        _ => 1                             // DWMWCP_DEFAULT
                    };
                    NativeMethods.DwmSetWindowAttribute(handle, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
                }
                catch { }
            }

            window.Loaded -= OnCornerLoaded;
            window.Loaded += OnCornerLoaded;
            void OnCornerLoaded(object s, RoutedEventArgs args) => Apply();

            if (window.IsLoaded)
            {
                Apply();
            }
        }

        #endregion

        #region Acrylic10Color

        public static readonly DependencyProperty Acrylic10ColorProperty =
            DependencyProperty.RegisterAttached(
                "Acrylic10Color",
                typeof(System.Windows.Media.Color),
                typeof(WindowHelper),
                new PropertyMetadata(System.Windows.Media.Colors.Transparent));

        public static void SetAcrylic10Color(Window window, System.Windows.Media.Color value)
        {
            window?.SetValue(Acrylic10ColorProperty, value);
        }

        public static System.Windows.Media.Color GetAcrylic10Color(Window window)
        {
            return window == null ? System.Windows.Media.Colors.Transparent : (System.Windows.Media.Color)window.GetValue(Acrylic10ColorProperty);
        }

        #endregion

        /// <summary>
        /// 根据当前主题为窗口应用/移除沉浸式深色标题栏。
        /// </summary>
        public static void ApplyImmersiveDarkMode(Window window, bool dark)
        {
            if (window == null) return;
            try
            {
                var handle = new WindowInteropHelper(window).Handle;
                if (handle == IntPtr.Zero)
                {
                    window.SourceInitialized -= OnApplyDarkWhenReady;
                    window.SourceInitialized += OnApplyDarkWhenReady;
                    void OnApplyDarkWhenReady(object s, EventArgs a)
                    {
                        window.SourceInitialized -= OnApplyDarkWhenReady;
                        if (dark) BackdropHelper.ApplyDarkMode(window); else BackdropHelper.RemoveDarkMode(window);
                    }
                    return;
                }
                if (dark) BackdropHelper.ApplyDarkMode(window); else BackdropHelper.RemoveDarkMode(window);
            }
            catch { }
        }

        /// <summary>
        /// 更新 WindowChrome：标题栏高度、拖拽与缩放边框。
        /// </summary>
        public static WindowChrome UpdateWindowChrome(Window window)
        {
            if (window == null) return null;

            var chrome = WindowChrome.GetWindowChrome(window);

            if (GetUseModernWindowStyle(window) || TitleBar.GetExtendViewIntoTitleBar(window))
            {
                if (chrome == null)
                {
                    chrome = new WindowChrome
                    {
                        CornerRadius = new CornerRadius(0),
                        NonClientFrameEdges = NonClientFrameEdges.None,
                        UseAeroCaptionButtons = false,
                        CaptionHeight = 0
                    };
                }

                var isResizable = window.ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip;
                chrome.ResizeBorderThickness = isResizable ? new Thickness(4) : new Thickness(0);

                // 扩展标题栏进入客户区时，把拖拽区高度交给自定义标题栏
                if (TitleBar.GetExtendViewIntoTitleBar(window))
                {
                    chrome.CaptionHeight = 0;
                    // 自定义标题栏元素需要可交互：内容默认就在客户区内
                }
                else
                {
                    chrome.CaptionHeight = TitleBar.GetHeight(window);
                }

                // 玻璃帧按“实际生效”的背景类型决定（优先运行时 BackdropHelper 应用值，其次 XAML 附加属性），
                // 对齐 WPF-UI FluentWindow.SetWindowChrome：
                // - Mica/Tabbed/Acrylic11：-1 全窗玻璃帧（DWM 绘制系统背景到客户区，窗口背景已设为透明）
                // - Acrylic10：1px 上边
                // - None：0.00001 —— 不绘制玻璃帧，但系统仍绘制窗口边框（官方 FluentWindow 同款取值）
                var requestedBackdrop = BackdropHelper.GetApplied(window);
                if (requestedBackdrop == BackdropType.None)
                {
                    requestedBackdrop = GetSystemBackdropType(window);
                }
                var actualBackdrop = BackdropHelper.GetActualBackdropType(requestedBackdrop);
                chrome.GlassFrameThickness = actualBackdrop switch
                {
                    BackdropType.Mica or BackdropType.Tabbed or BackdropType.Acrylic11 => new Thickness(-1),
                    BackdropType.Acrylic10 => new Thickness(0, 1, 0, 0),
                    _ => new Thickness(0.00001)
                };

                WindowChrome.SetWindowChrome(window, chrome);
            }

            return chrome;
        }
    }
}