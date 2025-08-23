using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrayNotify;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Shell;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 高性能透明桌面窗口
    /// </summary>
    public partial class PerformanceTransparentWin : Window
    {

        static class BrushCreator
        {
            /// <summary>
            /// 尝试从缓存获取或创建颜色笔刷
            /// </summary>
            /// <param name="color">对应的字符串颜色</param>
            /// <returns>已经被 Freeze 的颜色笔刷</returns>
            public static SolidColorBrush GetOrCreate(string color)
            {
                if (!color.StartsWith("#"))
                {
                    throw new ArgumentException($"输入的{nameof(color)}不是有效颜色，需要使用 # 开始");
                    // 如果不使用 # 开始将会在 ConvertFromString 出现异常
                }

                if (TryGetBrush(color, out var brushValue))
                {
                    return (SolidColorBrush)brushValue;
                }

                object convertColor;

                try
                {
                    convertColor = ColorConverter.ConvertFromString(color);
                }
                catch (FormatException)
                {
                    // 因为在 ConvertFromString 会抛出的是 令牌无效 难以知道是为什么传入的不对
                    throw new ArgumentException($"输入的{nameof(color)}不是有效颜色");
                }

                if (convertColor == null)
                {
                    throw new ArgumentException($"输入的{nameof(color)}不是有效颜色");
                }

                var brush = new SolidColorBrush((Color)convertColor);
                if (TryFreeze(brush))
                {
                    BrushCacheList.Add(color, new WeakReference<Brush>(brush));
                }

                return brush;
            }

            private static Dictionary<string, WeakReference<Brush>> BrushCacheList { get; } =
                new Dictionary<string, WeakReference<Brush>>();

            private static bool TryGetBrush(string key, out Brush brush)
            {
                if (BrushCacheList.TryGetValue(key, out var brushValue))
                {
                    if (brushValue.TryGetTarget(out brush))
                    {
                        return true;
                    }
                    else
                    {
                        // 被回收的资源
                        BrushCacheList.Remove(key);
                    }
                }

                brush = null;
                return false;
            }

            private static bool TryFreeze(Freezable freezable)
            {
                if (freezable.CanFreeze)
                {
                    freezable.Freeze();
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// 创建高性能透明桌面窗口
        /// </summary>
        public PerformanceTransparentWin()
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;

            Stylus.SetIsFlicksEnabled(this, false);
            Stylus.SetIsPressAndHoldEnabled(this, false);
            Stylus.SetIsTapFeedbackEnabled(this, false);
            Stylus.SetIsTouchFeedbackEnabled(this, false);

            WindowChrome.SetWindowChrome(this,
                new WindowChrome { GlassFrameThickness = WindowChrome.GlassFrameCompleteThickness, CaptionHeight = 0, CornerRadius = new CornerRadius(0), ResizeBorderThickness = new Thickness(0)});

            var visualTree = new FrameworkElementFactory(typeof(Border));
            visualTree.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Window.BackgroundProperty));
            var childVisualTree = new FrameworkElementFactory(typeof(ContentPresenter));
            childVisualTree.SetValue(UIElement.ClipToBoundsProperty, true);
            visualTree.AppendChild(childVisualTree);

            Template = new ControlTemplate
            {
                TargetType = typeof(Window),
                VisualTree = visualTree,
            };

            _dwmEnabled = DwmCompositionHelper.DwmIsCompositionEnabled();
            if (_dwmEnabled)
            {
                _hwnd = new WindowInteropHelper(this).EnsureHandle();
                Loaded += PerformanceDesktopTransparentWindow_Loaded;
                Background = Brushes.Transparent;
            }
            else
            {
                AllowsTransparency = true;
                Background = BrushCreator.GetOrCreate("#0100000");
                _hwnd = new WindowInteropHelper(this).EnsureHandle();
            }
        }

        /// <summary>
        /// 设置点击穿透到后面透明的窗口
        /// </summary>
        public void SetTransparentHitThrough()
        {
            if (_dwmEnabled)
            {
                Win32.User32.SetWindowLongPtr(_hwnd, Win32.GetWindowLongFields.GWL_EXSTYLE,
                    (IntPtr)(int)((long)Win32.User32.GetWindowLongPtr(_hwnd, Win32.GetWindowLongFields.GWL_EXSTYLE) | (long)Win32.ExtendedWindowStyles.WS_EX_TRANSPARENT));
            }
            else
            {
                Background = Brushes.Transparent;
            }
        }

        /// <summary>
        /// 设置点击命中，不会穿透到后面的窗口
        /// </summary>
        public void SetTransparentNotHitThrough()
        {
            if (_dwmEnabled)
            {
                Win32.User32.SetWindowLongPtr(_hwnd, Win32.GetWindowLongFields.GWL_EXSTYLE,
                    (IntPtr)(int)((long)Win32.User32.GetWindowLongPtr(_hwnd, Win32.GetWindowLongFields.GWL_EXSTYLE) & ~(long)Win32.ExtendedWindowStyles.WS_EX_TRANSPARENT));
            }
            else
            {
                Background = BrushCreator.GetOrCreate("#0100000");
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct STYLESTRUCT
        {
            public int styleOld;
            public int styleNew;
        }

        private void PerformanceDesktopTransparentWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ((HwndSource)PresentationSource.FromVisual(this)).AddHook((IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
            {
                //想要让窗口透明穿透鼠标和触摸等，需要同时设置 WS_EX_LAYERED 和 WS_EX_TRANSPARENT 样式，
                //确保窗口始终有 WS_EX_LAYERED 这个样式，并在开启穿透时设置 WS_EX_TRANSPARENT 样式
                //但是WPF窗口在未设置 AllowsTransparency = true 时，会自动去掉 WS_EX_LAYERED 样式（在 HwndTarget 类中)，
                //如果设置了 AllowsTransparency = true 将使用WPF内置的低性能的透明实现，
                //所以这里通过 Hook 的方式，在不使用WPF内置的透明实现的情况下，强行保证这个样式存在。
                if (msg == (int)Win32.WM.STYLECHANGING && (long)wParam == (long)Win32.GetWindowLongFields.GWL_EXSTYLE)
                {
                    var styleStruct = (STYLESTRUCT)Marshal.PtrToStructure(lParam, typeof(STYLESTRUCT));
                    styleStruct.styleNew |= (int)Win32.ExtendedWindowStyles.WS_EX_LAYERED;
                    Marshal.StructureToPtr(styleStruct, lParam, false);
                    handled = true;
                }
                return IntPtr.Zero;
            });
        }

        /// <summary>
        /// 是否开启 DWM 了，如果开启了，那么才可以使用高性能的桌面透明窗口
        /// </summary>
        private readonly bool _dwmEnabled;
        private readonly IntPtr _hwnd;
    }
}
