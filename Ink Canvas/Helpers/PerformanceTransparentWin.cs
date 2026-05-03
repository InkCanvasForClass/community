using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// Main-window base that can use WindowChrome for DWM-backed transparent rendering.
    /// </summary>
    public partial class PerformanceTransparentWin : Window
    {
        private readonly bool _useWindowChromeRendering;
        private readonly bool _dwmEnabled;
        private IntPtr _hwnd;
        private bool _transparentHitThrough;

        public bool IsUsingWindowChromeRendering => _useWindowChromeRendering && _dwmEnabled;

        public PerformanceTransparentWin()
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;

            _useWindowChromeRendering = SettingsManager.ReadEnableWindowChromeRendering();
            _dwmEnabled = DwmCompositionHelper.IsCompositionEnabled();

            if (IsUsingWindowChromeRendering)
            {
                ConfigureWindowChromeRendering();
            }
            else
            {
                AllowsTransparency = true;
                Background = Brushes.Transparent;
            }
        }

        private void ConfigureWindowChromeRendering()
        {
            WindowChrome.SetWindowChrome(this, new WindowChrome
            {
                GlassFrameThickness = WindowChrome.GlassFrameCompleteThickness,
                CaptionHeight = 0,
                CornerRadius = new CornerRadius(0),
                ResizeBorderThickness = new Thickness(0)
            });

            var root = new FrameworkElementFactory(typeof(Border));
            root.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(BackgroundProperty));

            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(UIElement.ClipToBoundsProperty, true);
            root.AppendChild(presenter);

            Template = new ControlTemplate
            {
                TargetType = typeof(Window),
                VisualTree = root
            };

            Background = Brushes.Transparent;
            SourceInitialized += PerformanceTransparentWin_SourceInitialized;
        }

        private void PerformanceTransparentWin_SourceInitialized(object sender, EventArgs e)
        {
            if (!IsUsingWindowChromeRendering) return;

            _hwnd = new WindowInteropHelper(this).Handle;
            EnsureLayeredWindowStyle();
            if (HwndSource.FromHwnd(_hwnd) is HwndSource source)
            {
                source.AddHook(WindowChromeStyleHook);
            }
        }

        private IntPtr WindowChromeStyleHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == (int)WindowMessage.StyleChanging && wParam.ToInt32() == GwlExStyle)
            {
                var styleStruct = Marshal.PtrToStructure<StyleStruct>(lParam);
                styleStruct.StyleNew |= (int)WsExLayered;
                Marshal.StructureToPtr(styleStruct, lParam, false);
                handled = true;
            }
            else if (msg == (int)WindowMessage.NcHitTest && _transparentHitThrough)
            {
                var point = PointFromScreen(GetPointFromLParam(lParam));
                if (!ShouldHandleWindowChromeHitTest(point))
                {
                    handled = true;
                    return new IntPtr(HtTransparent);
                }
            }

            return IntPtr.Zero;
        }

        public void SetTransparentHitThrough()
        {
            if (!IsUsingWindowChromeRendering) return;
            _transparentHitThrough = true;
            EnsureLayeredWindowStyle();
        }

        public void SetTransparentNotHitThrough()
        {
            if (!IsUsingWindowChromeRendering) return;
            _transparentHitThrough = false;
            EnsureLayeredWindowStyle();
        }

        protected virtual bool ShouldHandleWindowChromeHitTest(Point windowPoint)
        {
            return InputHitTest(windowPoint) != null;
        }

        private const int GwlExStyle = -20;
        private const long WsExLayered = 0x00080000L;
        private const int HtTransparent = -1;

        private enum WindowMessage
        {
            NcHitTest = 0x0084,
            StyleChanging = 0x007C
        }

        private static Point GetPointFromLParam(IntPtr lParam)
        {
            var value = lParam.ToInt64();
            var x = unchecked((short)(value & 0xffff));
            var y = unchecked((short)((value >> 16) & 0xffff));
            return new Point(x, y);
        }

        private void EnsureLayeredWindowStyle()
        {
            if (!IsUsingWindowChromeRendering || _hwnd == IntPtr.Zero) return;

            var exStyle = GetWindowLongPtr(_hwnd, GwlExStyle).ToInt64();
            if ((exStyle & WsExLayered) == 0)
                SetWindowLongPtr(_hwnd, GwlExStyle, new IntPtr(exStyle | WsExLayered));
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct StyleStruct
        {
            public int StyleOld;
            public int StyleNew;
        }

        private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        {
            return IntPtr.Size == 8
                ? GetWindowLongPtr64(hWnd, nIndex)
                : new IntPtr(GetWindowLong32(hWnd, nIndex));
        }

        private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            return IntPtr.Size == 8
                ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
                : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }

        [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
    }
}
