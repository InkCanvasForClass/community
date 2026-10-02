using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;

namespace Ink_Canvas.Helpers
{
    public static class WindowBackdropHelper
    {
        /// <summary>
        /// 扩展标题栏高度（对应原 iNKORE <c>ui:TitleBar.Height</c>）：窗口内容里有自定义标题栏时，
        /// 用它作为 WindowChrome 的拖拽区高度；未设置（NaN）时按窗口是否带原生标题栏自动取系统标题栏高度。
        /// </summary>
        public static readonly DependencyProperty ExtendedTitleBarHeightProperty =
            DependencyProperty.RegisterAttached(
                "ExtendedTitleBarHeight",
                typeof(double),
                typeof(WindowBackdropHelper),
                new PropertyMetadata(double.NaN));

        public static void SetExtendedTitleBarHeight(Window window, double value)
        {
            window?.SetValue(ExtendedTitleBarHeightProperty, value);
        }

        public static double GetExtendedTitleBarHeight(Window window)
        {
            return window == null ? double.NaN : (double)window.GetValue(ExtendedTitleBarHeightProperty);
        }
        /// <summary>
        /// 记录每个窗口的背景应用状态与首次显示后重应用所需的回调，避免重复挂载 Loaded 处理器。
        /// </summary>
        private static readonly ConditionalWeakTable<Window, BackdropRequest> Requests =
            new ConditionalWeakTable<Window, BackdropRequest>();

        private sealed class BackdropRequest
        {
            public string Name;
            public bool IsAcrylic10;
            public System.Windows.Media.Color Acrylic10Tint;
            public bool HasApplied;
            public bool Hooked;
            public bool ReappliedAfterShow;
        }

        public static void Apply(Window window, Settings settings = null)
        {
            if (window == null) return;

            var backdropName = settings?.Appearance?.WindowBackdrop
                ?? MainWindow.Settings?.Appearance?.WindowBackdrop
                ?? "None";

            Apply(window, backdropName);
        }

        public static void Apply(Window window, string backdropName)
        {
            if (window == null) return;

            var normalizedName = string.IsNullOrWhiteSpace(backdropName) ? "None" : backdropName;

            // FluentWindow（如设置窗口）由 WPF-UI 自行管理 WindowChrome 与 DWM 背景：它配合 ui:TitleBar
            // 把 CaptionHeight 设为 0 并自己画玻璃帧。这里若再按老方案配置 WindowChrome，会覆盖其拖拽区
            // 与系统按钮布局，因此只把设置映射到它自己的 WindowBackdropType，其余交给库处理。
            if (window is Wpf.Ui.Controls.FluentWindow fluentWindow)
            {
                fluentWindow.WindowBackdropType =
                    string.Equals(normalizedName, "Acrylic10", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(normalizedName, "Acrylic11", StringComparison.OrdinalIgnoreCase)
                        ? WindowBackdropType.Acrylic
                        : Enum.TryParse(normalizedName, true, out WindowBackdropType parsed)
                            ? parsed
                            : WindowBackdropType.None;
                return;
            }

            try
            {
                // 设置页提供三种亚克力方案：Acrylic（系统背景）/ Acrylic10（Win10 的
                // SetWindowCompositionAttribute 方案，自实现）/ Acrylic11（系统背景，Win11）。
                // 后两者不是 WPF-UI WindowBackdropType 的枚举成员，需要先归一化。
                var isAcrylic10 = string.Equals(normalizedName, "Acrylic10", StringComparison.OrdinalIgnoreCase);

                WindowBackdropType backdropType;
                if (isAcrylic10 || string.Equals(normalizedName, "Acrylic11", StringComparison.OrdinalIgnoreCase))
                {
                    backdropType = WindowBackdropType.Acrylic;
                }
                else if (!Enum.TryParse(normalizedName, true, out backdropType))
                {
                    backdropType = WindowBackdropType.None;
                }

                var request = Requests.GetValue(window, _ => new BackdropRequest());

                // 目标效果与窗口当前效果一致时必须走这条分支，不能再执行下面的“先移除再设置”：
                // RemoveBackdrop 会清掉 DWM 系统背景并移除窗口深色模式，而随后重复应用相同值
                // 不会触发库内部重建，于是窗口会停留在“背景已被移除”的状态。Windows 11 上系统
                // 背景生效时窗口自身背景是透明的，此时整页会直接透出桌面与 DWM 回退色，表现为发白；
                // Win10 没有系统背景所以不复现。
                if (request.HasApplied &&
                    string.Equals(request.Name, normalizedName, StringComparison.OrdinalIgnoreCase))
                {
                    ApplyNativeBackdrop(window, request);
                    return;
                }

                RemoveBackdrop(window);

                if (isAcrylic10)
                {
                    // 亚克力色调需在 WPF 层透明之前取色；并记录在请求里供显示后重应用使用
                    request.Acrylic10Tint = (window.Background as SolidColorBrush)?.Color ?? Colors.Transparent;
                    ConfigureWindowChrome(window, backdropType, true);
                    WindowBackdrop.RemoveBackground(window);
                    ApplyAcrylic10(window, request.Acrylic10Tint);
                }
                else if (backdropType == WindowBackdropType.None)
                {
                    SyncWindowDarkMode(window);
                }
                else if (WindowBackdrop.IsSupported(backdropType))
                {
                    // DWM 系统背景要被看见，WPF 层必须透明（与 WPF-UI WindowBackgroundManager 的顺序一致），
                    // 同时补上 WindowChrome 玻璃帧，否则普通窗口的客户区不会被 DWM 绘制系统背景（会变成纯黑）
                    ConfigureWindowChrome(window, backdropType, false);
                    WindowBackdrop.RemoveBackground(window);
                    WindowBackdrop.ApplyBackdrop(window, backdropType);
                }

                request.Name = normalizedName;
                request.IsAcrylic10 = isAcrylic10;
                request.HasApplied = true;

                AttachReapplyAfterShow(window, request);
            }
            catch
            {
                // Unsupported systems simply fall back to the normal window background.
            }
        }

        /// <summary>
        /// 按背景类型配置 WindowChrome 玻璃帧（取值与 WPF-UI FluentWindow 对齐）：
        /// Mica 族用 -1（全窗玻璃帧，DWM 把系统背景铺到客户区）；Acrylic10 用 1px 上边；
        /// None 不改动（此时窗口保持不透明应用背景，不会黑屏）。
        /// </summary>
        private static void ConfigureWindowChrome(Window window, WindowBackdropType backdropType, bool isAcrylic10)
        {
            try
            {
                var chrome = WindowChrome.GetWindowChrome(window);
                if (chrome == null)
                {
                    chrome = new WindowChrome
                    {
                        UseAeroCaptionButtons = true,
                        CornerRadius = new CornerRadius(0),
                    };
                }

                var isResizable = window.ResizeMode == ResizeMode.CanResize || window.ResizeMode == ResizeMode.CanResizeWithGrip;
                chrome.ResizeBorderThickness = isResizable ? new Thickness(4) : new Thickness(0);

                if (chrome.CaptionHeight <= 0)
                {
                    var extendedTitleBarHeight = GetExtendedTitleBarHeight(window);
                    chrome.CaptionHeight = !double.IsNaN(extendedTitleBarHeight) && extendedTitleBarHeight > 0
                        ? extendedTitleBarHeight
                        : window.WindowStyle != WindowStyle.None ? SystemParameters.CaptionHeight : 0;
                }

                chrome.GlassFrameThickness = isAcrylic10
                    ? new Thickness(0, 1, 0, 0)
                    : backdropType == WindowBackdropType.None ? new Thickness(0.00001) : new Thickness(-1);

                WindowChrome.SetWindowChrome(window, chrome);
            }
            catch
            {
            }
        }

        /// <summary>
        /// 只刷新 DWM 侧的系统背景与深色模式，不重建窗口样式。
        /// </summary>
        private static void ApplyNativeBackdrop(Window window, BackdropRequest request)
        {
            try
            {
                if (request.IsAcrylic10)
                {
                    ApplyAcrylic10(window, request.Acrylic10Tint);
                }
                else
                {
                    var backdropType = ParseBackdropType(request.Name);
                    if (WindowBackdrop.IsSupported(backdropType))
                    {
                        WindowBackdrop.ApplyBackdrop(window, backdropType);
                    }
                }

                SyncWindowDarkMode(window);
            }
            catch
            {
            }
        }

        /// <summary>
        /// 把设置里的背景名解析为 WPF-UI 的背景类型（Acrylic10/Acrylic11 归一化到 Acrylic）。
        /// </summary>
        private static WindowBackdropType ParseBackdropType(string name)
        {
            if (string.Equals(name, "Acrylic10", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "Acrylic11", StringComparison.OrdinalIgnoreCase))
            {
                return WindowBackdropType.Acrylic;
            }

            return Enum.TryParse(name, true, out WindowBackdropType type) ? type : WindowBackdropType.None;
        }

        /// <summary>
        /// 同步所有已登记窗口的 DWM 深色模式。
        ///
        /// DWMWA_USE_IMMERSIVE_DARK_MODE 是写进 DWM 的窗口属性，不会随 WPF-UI 换主题字典而更新，
        /// 必须在主题切换后对每个仍打开的窗口重写一次，否则浅色主题下仍留着深色标题栏。
        /// 窗口用 WeakReference 登记，关闭后自动回收，不必反注册。
        /// </summary>
        public static void SyncAllWindowsDarkMode()
        {
            try
            {
                Window[] alive;
                int count;

                lock (RegisteredWindows)
                {
                    alive = new Window[RegisteredWindows.Count];
                    count = 0;

                    foreach (var weak in RegisteredWindows)
                    {
                        if (weak.TryGetTarget(out var window) && window.IsLoaded)
                        {
                            alive[count++] = window;
                        }
                    }
                }

                for (int i = 0; i < count; i++)
                {
                    SyncWindowDarkMode(alive[i]);
                }
            }
            catch
            {
            }
        }

        private static readonly List<WeakReference<Window>> RegisteredWindows = new();

        /// <summary>
        /// 登记窗口以参与主题切换时的深色模式同步。重复登记同一窗口是安全的。
        /// </summary>
        public static void RegisterForThemeSync(Window window)
        {
            if (window == null) return;

            lock (RegisteredWindows)
            {
                for (int i = RegisteredWindows.Count - 1; i >= 0; i--)
                {
                    if (!RegisteredWindows[i].TryGetTarget(out var existing))
                    {
                        RegisteredWindows.RemoveAt(i);
                    }
                    else if (ReferenceEquals(existing, window))
                    {
                        return;
                    }
                }

                RegisteredWindows.Add(new WeakReference<Window>(window));
            }
        }

        /// <summary>
        /// 让窗口的 DWM 深色模式与当前实际主题保持一致（RemoveBackdrop 会把深色模式一并清除）。
        /// </summary>
        private static void SyncWindowDarkMode(Window window)
        {
            try
            {
                var hwnd = new WindowInteropHelper(window).EnsureHandle();
                if (hwnd == IntPtr.Zero) return;

                int useDarkMode = ApplicationThemeManager.GetAppTheme() == ApplicationTheme.Dark ? 1 : 0;

                // Win10 1809~1903 只认旧的属性号 19，1903+ 与 Win11 使用 20
                if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int)) != 0)
                {
                    DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref useDarkMode, sizeof(int));
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// 窗口显示完成后再应用一次系统背景：构造阶段窗口尚未显示时设置的 DWM 系统背景
        /// 在部分系统上不会被采纳，首帧会退回纯色回退背景，需要窗口可见后重新应用。
        /// </summary>
        private static void AttachReapplyAfterShow(Window window, BackdropRequest request)
        {
            try
            {
                if (request.Hooked || request.ReappliedAfterShow || window.IsLoaded) return;
                request.Hooked = true;

                RoutedEventHandler onLoaded = null;
                onLoaded = (sender, e) =>
                {
                    window.Loaded -= onLoaded;
                    request.ReappliedAfterShow = true;

                    if (!window.IsVisible) return;

                    ApplyNativeBackdrop(window, request);
                };
                window.Loaded += onLoaded;
            }
            catch
            {
            }
        }

        /// <summary>
        /// 清掉此前应用过的所有背景效果（DWM 系统背景 + Win10 亚克力 + 深色模式）。
        /// </summary>
        private static void RemoveBackdrop(Window window)
        {
            try
            {
                WindowBackdrop.RemoveBackdrop(window);
                RemoveAcrylic10(window);
            }
            catch
            {
            }
        }

        #region Acrylic10（SetWindowCompositionAttribute，移植自 iNKORE.UI.WPF.Modern，MIT License）

        private static void ApplyAcrylic10(Window window, Color tintColor)
        {
            var hwnd = new WindowInteropHelper(window).EnsureHandle();
            if (hwnd == IntPtr.Zero) return;

            SetAccentPolicy(hwnd, ACCENT_ENABLE_ACRYLICBLURBEHIND, ColorToAbgr(tintColor, 0.8));
        }

        private static void RemoveAcrylic10(Window window)
        {
            var hwnd = new WindowInteropHelper(window).EnsureHandle();
            if (hwnd == IntPtr.Zero) return;

            SetAccentPolicy(hwnd, ACCENT_DISABLED, 0);
        }

        private static void SetAccentPolicy(IntPtr hwnd, int accentState, int gradientColor)
        {
            var accentPolicy = new ACCENT_POLICY
            {
                AccentState = accentState,
                GradientColor = gradientColor
            };

            int accentStructSize = Marshal.SizeOf(accentPolicy);
            IntPtr accentPtr = Marshal.AllocHGlobal(accentStructSize);
            try
            {
                Marshal.StructureToPtr(accentPolicy, accentPtr, false);
                var data = new WINCOMPATTRDATA
                {
                    Attribute = WCA_ACCENT_POLICY,
                    SizeOfData = accentStructSize,
                    Data = accentPtr
                };
                SetWindowCompositionAttribute(hwnd, ref data);
            }
            finally
            {
                Marshal.FreeHGlobal(accentPtr);
            }
        }

        private static int ColorToAbgr(Color value, double alphaScale)
        {
            return value.R << 0 | value.G << 8 | value.B << 16 | (int)(value.A * alphaScale) << 24;
        }

        #endregion

        #region Win32 互操作

        private const int WCA_ACCENT_POLICY = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;
        private const int ACCENT_DISABLED = 0;
        private const int ACCENT_ENABLE_ACRYLICBLURBEHIND = 4;

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        [DllImport("user32.dll")]
        private static extern bool SetWindowCompositionAttribute(IntPtr hwnd, ref WINCOMPATTRDATA data);

        [StructLayout(LayoutKind.Sequential)]
        private struct WINCOMPATTRDATA
        {
            public int Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ACCENT_POLICY
        {
            public int AccentState;
            public int AccentFlags;
            public int GradientColor;
            public int AnimationId;
        }

        #endregion
    }
}