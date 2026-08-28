using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace WpfUiCompat.Helpers
{
    /// <summary>
    /// 背景效果类型（兼容 iNKORE BackdropType）。移植自 iNKORE.UI.WPF.Modern（MIT License）。
    /// </summary>
    public enum BackdropType
    {
        None = 1,
        Mica = 2,
        Acrylic = 3,
        Tabbed = 4,

        Acrylic10,
        Acrylic11,
    }

    /// <summary>
    /// Windows 10 (1903+) 亚克力效果助手（SetWindowCompositionAttribute 方案）。
    /// 移植自 iNKORE.UI.WPF.Modern（MIT License）。
    /// </summary>
    public static class Acrylic10Helper
    {
        public static bool IsAcrylicSupported()
        {
            return NativeMethods.OSVersion >= new Version(10, 0, 17063);
        }

        public static bool Apply(Window window, bool force = false)
        {
            var windowHandle = new WindowInteropHelper(window).EnsureHandle();
            if (windowHandle == IntPtr.Zero) return false;

            if (window.Background is SolidColorBrush brush)
            {
                return Apply(windowHandle, brush.Color, force);
            }
            return Apply(windowHandle, Colors.Transparent, force);
        }

        public static bool Apply(IntPtr handle, Color color, bool force = false)
        {
            if (handle == IntPtr.Zero) return false;
            if (!force && !IsAcrylicSupported()) return false;
            return TryApplyAcrylic(handle, color);
        }

        public static void Remove(Window window)
        {
            var windowHandle = new WindowInteropHelper(window).EnsureHandle();
            if (windowHandle == IntPtr.Zero) return;
            Remove(windowHandle);
        }

        public static void Remove(IntPtr handle)
        {
            if (handle == IntPtr.Zero) return;

            var accentPolicy = new NativeMethods.ACCENT_POLICY
            {
                AccentState = (uint)NativeMethods.ACCENT_STATE.ACCENT_DISABLED,
            };

            int accentStructSize = Marshal.SizeOf(accentPolicy);
            IntPtr accentPtr = Marshal.AllocHGlobal(accentStructSize);
            try
            {
                Marshal.StructureToPtr(accentPolicy, accentPtr, false);
                var data = new NativeMethods.WINCOMPATTRDATA
                {
                    Attribute = 19, // WCA_ACCENT_POLICY
                    SizeOfData = accentStructSize,
                    Data = accentPtr
                };
                NativeMethods.SetWindowCompositionAttribute(handle, ref data);
            }
            finally
            {
                Marshal.FreeHGlobal(accentPtr);
            }
        }

        public static bool TryApplyAcrylic(IntPtr handle, Color backcolor)
        {
            var accentPolicy = new NativeMethods.ACCENT_POLICY
            {
                AccentState = (uint)NativeMethods.ACCENT_STATE.ACCENT_ENABLE_ACRYLICBLURBEHIND,
                GradientColor = unchecked((uint)NativeMethods.ColorToAbgr(backcolor, 0.8))
            };

            int accentStructSize = Marshal.SizeOf(accentPolicy);
            IntPtr accentPtr = Marshal.AllocHGlobal(accentStructSize);
            try
            {
                Marshal.StructureToPtr(accentPolicy, accentPtr, false);
                var data = new NativeMethods.WINCOMPATTRDATA
                {
                    Attribute = 19, // WCA_ACCENT_POLICY
                    SizeOfData = accentStructSize,
                    Data = accentPtr
                };
                NativeMethods.SetWindowCompositionAttribute(handle, ref data);
            }
            finally
            {
                Marshal.FreeHGlobal(accentPtr);
            }
            return true;
        }
    }

    /// <summary>
    /// Windows 11 系统背景效果助手（DWMWA_SYSTEMBACKDROP_TYPE 方案）。
    /// 移植自 iNKORE.UI.WPF.Modern（MIT License）。
    /// </summary>
    public static class BackdropHelper
    {
        public static bool IsSupported(BackdropType type)
        {
            return type switch
            {
                BackdropType.None => true,
                BackdropType.Tabbed => NativeMethods.OSVersion >= new Version(10, 0, 22523),
                BackdropType.Mica => NativeMethods.IsWindows11OrGreater,
                BackdropType.Acrylic11 => NativeMethods.OSVersion >= new Version(10, 0, 22523),
                BackdropType.Acrylic10 => Acrylic10Helper.IsAcrylicSupported(),
                BackdropType.Acrylic => Acrylic10Helper.IsAcrylicSupported(),
                _ => false
            };
        }

        public static BackdropType GetActualBackdropType(BackdropType type)
        {
            if (type == BackdropType.Acrylic)
            {
                return IsSupported(BackdropType.Acrylic11) ? BackdropType.Acrylic11 : BackdropType.Acrylic10;
            }
            return type;
        }

        // 运行时已应用的背景类型（Window 级）。UpdateWindowChrome 据此决定玻璃帧，
        // 避免加载后按 XAML 附加属性（常为 None）重置、破坏已应用的系统背景。
        private static readonly System.Collections.Generic.Dictionary<Window, BackdropType> _applied
            = new System.Collections.Generic.Dictionary<Window, BackdropType>();

        internal static BackdropType GetApplied(Window window)
        {
            return window != null && _applied.TryGetValue(window, out var t) ? t : BackdropType.None;
        }

        /// <summary>
        /// 设置窗口的玻璃帧厚度。Mica/Tabbed/Acrylic11 需要 -1（全窗玻璃帧），
        /// DWM 才会把系统背景绘制到客户区；Acrylic10 保留 1px 上边；None 为 0。
        /// </summary>
        internal static void SetGlassFrame(Window window, Thickness thickness)
        {
            if (window == null) return;
            var chrome = System.Windows.Shell.WindowChrome.GetWindowChrome(window);
            if (chrome == null)
            {
                chrome = new System.Windows.Shell.WindowChrome
                {
                    CornerRadius = new CornerRadius(0),
                    NonClientFrameEdges = System.Windows.Shell.NonClientFrameEdges.None,
                    UseAeroCaptionButtons = false,
                    CaptionHeight = 0
                };
                System.Windows.Shell.WindowChrome.SetWindowChrome(window, chrome);
            }
            if (chrome.GlassFrameThickness != thickness)
            {
                chrome.GlassFrameThickness = thickness;
            }
        }

        private static bool IsSystemBackdrop(BackdropType t)
            => t is BackdropType.Mica or BackdropType.Tabbed or BackdropType.Acrylic11;

        private static void RestoreBackground(Window window)
        {
            // 仅当透明值由本助手设置时清除：ClearValue 回退到隐式样式的不透明主题背景
            if (ReferenceEquals(window.Background, System.Windows.Media.Brushes.Transparent))
            {
                window.ClearValue(Window.BackgroundProperty);
            }
        }

        public static bool Apply(Window window, BackdropType type, bool force = false)
        {
            if (window == null) return false;

            var actual = GetActualBackdropType(type);

            // 系统级背景：DWM 才会绘制；
            // 操作系统不支持时降级为 None，避免透明窗口无绘制导致纯黑。
            if (IsSystemBackdrop(actual) && !IsSupported(actual))
            {
                actual = BackdropType.None;
            }

            var previous = GetApplied(window);

            if (actual == BackdropType.None)
            {
                _applied[window] = BackdropType.None;
                if (IsSystemBackdrop(previous))
                {
                    RestoreBackground(window);
                    RestoreCompositionBackground(window);
                }
                // 官方值：0.00001 —— 不绘制玻璃帧但保留系统窗口边框
                SetGlassFrame(window, new Thickness(0.00001));
            }
            else if (actual == BackdropType.Acrylic10)
            {
                _applied[window] = BackdropType.Acrylic10;
                SetGlassFrame(window, new Thickness(0, 1, 0, 0));
            }
            else
            {
                _applied[window] = actual;
                // 对齐官方 WindowBackdrop.RemoveBackground：
                // 1) WPF 窗口背景透明（SetCurrentValue 局部值覆盖隐式样式）
                window.SetCurrentValue(Window.BackgroundProperty, System.Windows.Media.Brushes.Transparent);
                // 2) 合成目标背景透明 —— DWM 系统背景（云母/亚克力）能否透出的决定性一步
                var handle0 = new WindowInteropHelper(window).EnsureHandle();
                if (handle0 != IntPtr.Zero)
                {
                    var source = System.Windows.Interop.HwndSource.FromHwnd(handle0);
                    if (source?.CompositionTarget != null)
                    {
                        source.CompositionTarget.BackgroundColor = System.Windows.Media.Colors.Transparent;
                    }
                }
                SetGlassFrame(window, new Thickness(-1));
            }

            var windowHandle = new WindowInteropHelper(window).EnsureHandle();
            if (windowHandle == IntPtr.Zero) return false;

            return Apply(windowHandle, actual, force);
        }

        /// <summary>恢复合成目标背景（对齐官方 RestoreContentBackground）。</summary>
        private static void RestoreCompositionBackground(Window window)
        {
            try
            {
                var handle = new WindowInteropHelper(window).EnsureHandle();
                if (handle == IntPtr.Zero) return;
                var source = System.Windows.Interop.HwndSource.FromHwnd(handle);
                if (source?.CompositionTarget != null)
                {
                    source.CompositionTarget.BackgroundColor = System.Windows.SystemColors.WindowColor;
                }
            }
            catch { }
        }

        public static bool Apply(IntPtr handle, BackdropType type, bool force = false, Color? acrylic10Color = null)
        {
            if (handle == IntPtr.Zero) return false;
            if (!force && !IsSupported(type)) return false;

            // DWMWA_COLOR_NONE：让 DWM 不绘制标题栏底色，避免与背景效果冲突
            int captionColor = -2;
            NativeMethods.DwmSetWindowAttribute(handle, NativeMethods.DWMWA_CAPTION_COLOR, ref captionColor, sizeof(int));

            return type switch
            {
                BackdropType.None => TryApplyNone(handle),
                BackdropType.Mica => TryApplyMica(handle),
                BackdropType.Acrylic11 => TryApplyAcrylic(handle),
                BackdropType.Acrylic10 => Acrylic10Helper.TryApplyAcrylic(handle, acrylic10Color ?? Colors.Transparent),
                BackdropType.Acrylic => Apply(handle, GetActualBackdropType(type), force, acrylic10Color),
                BackdropType.Tabbed => TryApplyTabbed(handle),
                _ => false
            };
        }

        public static void Remove(Window window)
        {
            if (window == null) return;

            if (_applied.TryGetValue(window, out var prev) && IsSystemBackdrop(prev))
            {
                RestoreBackground(window);
            }
            _applied.Remove(window);
            RestoreCompositionBackground(window);
            SetGlassFrame(window, new Thickness(0.00001));

            var windowHandle = new WindowInteropHelper(window).EnsureHandle();
            if (windowHandle == IntPtr.Zero) return;
            Remove(windowHandle);
        }

        public static void Remove(IntPtr handle)
        {
            if (handle == IntPtr.Zero) return;

            Acrylic10Helper.Remove(handle);

            int pvAttribute = 0;
            int backdropPvAttribute = NativeMethods.DWMSBT_NONE;

            NativeMethods.DwmSetWindowAttribute(handle, NativeMethods.DWMWA_MICA_EFFECT, ref pvAttribute, sizeof(int));
            NativeMethods.DwmSetWindowAttribute(handle, NativeMethods.DWMWA_SYSTEMBACKDROP_TYPE, ref backdropPvAttribute, sizeof(int));

            int captionColor = -1; // DWMWA_COLOR_DEFAULT
            NativeMethods.DwmSetWindowAttribute(handle, NativeMethods.DWMWA_CAPTION_COLOR, ref captionColor, sizeof(int));
        }

        public static void ApplyDarkMode(Window window)
        {
            if (window == null) return;
            try
            {
                var windowHandle = new WindowInteropHelper(window).EnsureHandle();
                if (windowHandle == IntPtr.Zero) return;
                ApplyDarkMode(windowHandle);
            }
            catch { }
        }

        public static void ApplyDarkMode(IntPtr handle)
        {
            if (handle == IntPtr.Zero) return;
            int pvAttribute = 1;
            int dwAttribute = NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE;
            if (NativeMethods.OSVersion < new Version(10, 0, 18985))
            {
                dwAttribute = NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE_OLD;
            }
            NativeMethods.DwmSetWindowAttribute(handle, dwAttribute, ref pvAttribute, sizeof(int));
        }

        public static void RemoveDarkMode(Window window)
        {
            if (window == null) return;
            try
            {
                var windowHandle = new WindowInteropHelper(window).EnsureHandle();
                if (windowHandle == IntPtr.Zero) return;
                RemoveDarkMode(windowHandle);
            }
            catch { }
        }

        public static void RemoveDarkMode(IntPtr handle)
        {
            if (handle == IntPtr.Zero) return;
            int pvAttribute = 0;
            int dwAttribute = NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE;
            if (NativeMethods.OSVersion < new Version(10, 0, 18985))
            {
                dwAttribute = NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE_OLD;
            }
            NativeMethods.DwmSetWindowAttribute(handle, dwAttribute, ref pvAttribute, sizeof(int));
        }

        private static void RemoveTitleBar(IntPtr handle)
        {
            try
            {
                NativeMethods.SetWindowLong(handle, -16, NativeMethods.GetWindowLong(handle, -16) & ~0x80000);
            }
            catch
            {
            }
        }

        private static bool TryApplyNone(IntPtr handle)
        {
            if (NativeMethods.OSVersion >= new Version(10, 0, 22523))
            {
                int backdropPvAttribute = NativeMethods.DWMSBT_AUTO;
                NativeMethods.DwmSetWindowAttribute(handle, NativeMethods.DWMWA_SYSTEMBACKDROP_TYPE, ref backdropPvAttribute, sizeof(int));
                return true;
            }
            Remove(handle);
            return true;
        }

        private static bool TryApplyTabbed(IntPtr handle)
        {
            int backdropPvAttribute = NativeMethods.DWMSBT_TABBEDWINDOW;
            NativeMethods.DwmSetWindowAttribute(handle, NativeMethods.DWMWA_SYSTEMBACKDROP_TYPE, ref backdropPvAttribute, sizeof(int));
            return true;
        }

        private static bool TryApplyMica(IntPtr handle)
        {
            if (NativeMethods.OSVersion >= new Version(10, 0, 22523))
            {
                int backdropPvAttribute = NativeMethods.DWMSBT_MAINWINDOW;
                NativeMethods.DwmSetWindowAttribute(handle, NativeMethods.DWMWA_SYSTEMBACKDROP_TYPE, ref backdropPvAttribute, sizeof(int));
                return true;
            }

            RemoveTitleBar(handle);
            int pvAttribute = 1;
            NativeMethods.DwmSetWindowAttribute(handle, NativeMethods.DWMWA_MICA_EFFECT, ref pvAttribute, sizeof(int));
            return true;
        }

        private static bool TryApplyAcrylic(IntPtr handle)
        {
            int backdropPvAttribute = NativeMethods.DWMSBT_TRANSIENTWINDOW;
            NativeMethods.DwmSetWindowAttribute(handle, NativeMethods.DWMWA_SYSTEMBACKDROP_TYPE, ref backdropPvAttribute, sizeof(int));
            return true;
        }
    }
}