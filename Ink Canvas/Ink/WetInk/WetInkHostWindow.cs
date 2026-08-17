using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Windows = global::Windows;

namespace Ink_Canvas.Ink.WetInk
{
    /// <summary>
    /// 墨迹覆盖宿主窗口：位于主窗口上方的顶层透明窗口，承载 WinRT InkPresenter 的
    /// DirectComposition 表面。
    ///
    /// - 视觉：WS_EX_NOREDIRECTIONBITMAP（无重定向位图，除合成内容外全透明）。
    /// - 输入：SetWindowRgn 按排除矩形裁剪出真正可写的画布区域；chrome 区域不在窗口内，
    ///   指针自然落到主窗口 —— 结构性排除，不再有旧系统的白名单穿透问题。
    /// - 手掌：WM_POINTER 只读接触尺寸（GetPointerTouchInfo.rcContact），交给分类器。
    /// - 常驻可见、停靠离屏（永不 Show/Hide，避免 DWM 合成重排闪屏）。
    ///
    /// 注：本文件直接用 P/Invoke + DllImport 处理 SetWindowRgn / 区域 / WM_POINTER。
    /// CsWin32 生成的 HRGN（SafeHandle 派生）与 SetWindowRgn 的 HRGN 重载类型不匹配，
    /// 而 WM_POINTER 接口（GetPointerId/GetPointerType）CsWin32 未生成在 PInvoke 中，
    /// 故直接走 user32 DllImport 最稳。
    /// </summary>
    internal sealed class WetInkHostWindow : IDisposable
    {
        private const int WsPopup = unchecked((int)0x80000000);
        private const int WsVisible = 0x10000000;
        private const int WsClipSiblings = 0x04000000;
        private const int WsClipChildren = 0x02000000;

        private const int WsExNoActivate = 0x08000000;
        private const int WsExToolWindow = 0x00000080;
        private const int WsExNoRedirectionBitmap = 0x00200000;

        private const int WmPointerDown = 0x0246;
        private const int WmPointerUpdate = 0x0245;
        private const int WmPointerUp = 0x0247;
        private const int WmPointerCaptureChanged = 0x024C;
        private const int WmNcHitTest = 0x0084;
        private const int HtTransparent = -1;
        private const int HtClient = 1;

        private const int PointerTypeTouch = 2; // PT_TOUCH
        private const int PointerTypePen = 3;   // PT_PEN

        private const int RgnOr = 2;
        private const int RgnDiff = 4;

        private const int HiddenPosition = -100000;

        private const uint SwpNoActivate = 0x0010;
        private const uint SwpShowWindow = 0x0040;
        private const uint SwpNoMove = 0x0002;
        private const uint SwpNoSize = 0x0001;

        private readonly IntPtr _mainWindowHwnd;
        private readonly HwndSource _source;
        private double _dpiScale = 1.0;
        private IReadOnlyList<Rect> _exclusionRectsDip;
        private double _clientOriginXDip;
        private double _clientOriginYDip;
        private bool _disposed;

        public Action<uint, bool, double, double, double, double> ContactSample;
        public Action<uint, double, double, float, long> PenSample;
        public Action<uint> ContactUp;

        public IntPtr Hwnd => _source.Handle;
        public double DpiScale => _dpiScale;

        public WetInkHostWindow(IntPtr mainWindowHwnd)
        {
            _mainWindowHwnd = mainWindowHwnd;

            var parameters = new HwndSourceParameters("InkCanvasForClass.WetInkOverlay.v2")
            {
                Width = 1,
                Height = 1,
                PositionX = HiddenPosition,
                PositionY = HiddenPosition,
                WindowStyle = WsPopup | WsVisible | WsClipSiblings | WsClipChildren,
                ExtendedWindowStyle = WsExNoActivate | WsExToolWindow | WsExNoRedirectionBitmap,
                HwndSourceHook = WndProc
            };

            _source = new HwndSource(parameters);
        }

        public void UpdateTarget(
            double dpiScale,
            double clientOriginXDip,
            double clientOriginYDip,
            double clientWidthDip,
            double clientHeightDip,
            IReadOnlyList<Rect> exclusionRectsDip)
        {
            if (_disposed) return;
            _dpiScale = dpiScale;
            // 排除矩形为屏幕 DIP；NCHITTEST 的 lParam 是屏幕像素。
            _exclusionRectsDip = exclusionRectsDip;
            _clientOriginXDip = clientOriginXDip;
            _clientOriginYDip = clientOriginYDip;

            var originXPx = (int)(clientOriginXDip * dpiScale);
            var originYPx = (int)(clientOriginYDip * dpiScale);
            var widthPx = Math.Max(1, (int)Math.Round(clientWidthDip * dpiScale));
            var heightPx = Math.Max(1, (int)Math.Round(clientHeightDip * dpiScale));

            SetWindowPos(_source.Handle, GetZOrderTargetHwnd(), originXPx, originYPx, widthPx, heightPx,
                SwpNoActivate | SwpShowWindow);

            SetExclusionRegion(widthPx, heightPx, exclusionRectsDip);
        }

        public void ParkOffscreen()
        {
            if (_disposed) return;
            SetWindowPos(_source.Handle, GetZOrderTargetHwnd(), HiddenPosition, HiddenPosition, 1, 1,
                SwpNoActivate | SwpShowWindow);
            SetWindowRgn(_source.Handle, IntPtr.Zero, true);
        }

        public void BringToFront()
        {
            if (_disposed) return;
            SetWindowPos(_source.Handle, GetZOrderTargetHwnd(), 0, 0, 0, 0,
                SwpNoActivate | SwpNoMove | SwpNoSize | SwpShowWindow);
        }

        private IntPtr GetZOrderTargetHwnd()
        {
            try
            {
                var exStyle = GetWindowLong(_mainWindowHwnd, GwlExStyle);
                return (exStyle & WsExTopmost) != 0 ? HwndTopmost : HwndTop;
            }
            catch
            {
                return HwndTop;
            }
        }

        private void SetExclusionRegion(int widthPx, int heightPx, IReadOnlyList<Rect> exclusionRectsDip)
        {
            var hwnd = _source.Handle;
            if (exclusionRectsDip == null || exclusionRectsDip.Count == 0)
            {
                SetWindowRgn(hwnd, IntPtr.Zero, true);
                return;
            }

            var whole = CreateRectRgn(0, 0, widthPx, heightPx);
            var combined = CreateRectRgn(0, 0, 0, 0);

            try
            {
                foreach (var rect in exclusionRectsDip)
                {
                    // 矩形是屏幕 DIP，区域是客户端像素：减客户端原点再乘 dpi。
                    var clientX = (rect.X - _clientOriginXDip) * _dpiScale;
                    var clientY = (rect.Y - _clientOriginYDip) * _dpiScale;
                    var r = CreateRectRgn(
                        (int)Math.Round(clientX),
                        (int)Math.Round(clientY),
                        (int)Math.Round(rect.Right * _dpiScale - _clientOriginXDip * _dpiScale),
                        (int)Math.Round(rect.Bottom * _dpiScale - _clientOriginYDip * _dpiScale));
                    try
                    {
                        CombineRgn(combined, combined, r, RgnOr);
                    }
                    finally
                    {
                        DeleteObject(r);
                    }
                }

                CombineRgn(whole, whole, combined, RgnDiff);
            }
            finally
            {
                DeleteObject(combined);
            }

            // SetWindowRgn 成功后系统接管 whole。
            SetWindowRgn(hwnd, whole, true);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            try
            {
                if (msg == WmNcHitTest)
                {
                    // chrome 排除区域内的输入穿透到主窗口（浮动栏/PPT 导航等必须可点）。
                    // lParam = 屏幕像素；排除矩形 = 屏幕 DIP。
                    var xPx = (short)(lParam.ToInt64() & 0xFFFF);
                    var yPx = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
                    var rects = _exclusionRectsDip;
                    if (rects != null)
                    {
                        var xd = xPx / _dpiScale;
                        var yd = yPx / _dpiScale;
                        foreach (var r in rects)
                        {
                            if (xd >= r.X && xd <= r.Right && yd >= r.Y && yd <= r.Bottom)
                            {
                                handled = true;
                                return new IntPtr(HtTransparent);
                            }
                        }
                    }
                    return new IntPtr(HtClient);
                }

                if (msg == WmPointerDown || msg == WmPointerUpdate)
                {
                    var pointerId = GetPointerIdFromWParam(wParam);
                    OnPointerContact(pointerId);
                }
                else if (msg == WmPointerUp || msg == WmPointerCaptureChanged)
                {
                    var pointerId = GetPointerIdFromWParam(wParam);
                    ContactUp?.Invoke(pointerId);
                }
            }
            catch (Exception ex)
            {
                // WndProc 异常绝不能抛出（会淹没 WPF Dispatcher 导致 UI 无响应/点不了）。
                Helpers.LogHelper.WriteLogToFile(
                    $"WetInkHostWindow WndProc 异常: {ex.Message}", Helpers.LogHelper.LogType.Warning);
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// GetPointerId 不是 user32.dll 导出函数，而是 winuser.h 的宏
        /// （GET_POINTERID_WPARAM = LOWORD(wParam)）。必须内联提取，否则
        /// EntryPointNotFoundException 每次 WM_POINTER 抛一次，淹没 UI 线程。
        /// </summary>
        private static uint GetPointerIdFromWParam(IntPtr wParam)
        {
            return (uint)(wParam.ToInt64() & 0xFFFF);
        }

        private void OnPointerContact(uint pointerId)
        {
            if (!GetPointerType(pointerId, out var pointerType))
                return;

            if (pointerType == PointerTypeTouch)
            {
                if (GetPointerTouchInfo(pointerId, out var info))
                {
                    var widthPx = info.rcContact.Right - info.rcContact.Left;
                    var heightPx = info.rcContact.Bottom - info.rcContact.Top;
                    double contactWidthDip = widthPx > 0 ? widthPx / _dpiScale : 0;
                    double contactHeightDip = heightPx > 0 ? heightPx / _dpiScale : 0;
                    double xDip = info.pointerInfo.ptPixelLocation.X / _dpiScale - _clientOriginXDip;
                    double yDip = info.pointerInfo.ptPixelLocation.Y / _dpiScale - _clientOriginYDip;
                    ContactSample?.Invoke(pointerId, true, contactWidthDip, contactHeightDip, xDip, yDip);
                }
            }
            else if (pointerType == PointerTypePen)
            {
                if (GetPointerPenInfo(pointerId, out var info))
                {
                    double xDip = info.pointerInfo.ptPixelLocation.X / _dpiScale - _clientOriginXDip;
                    double yDip = info.pointerInfo.ptPixelLocation.Y / _dpiScale - _clientOriginYDip;
                    float pressure = info.pressure > 0 ? Math.Min(1f, info.pressure / 1024f) : 0f;
                    long timestampMicroseconds = info.pointerInfo.performanceCount > 0
                        ? (long)(info.pointerInfo.performanceCount / 10UL)
                        : info.pointerInfo.dwTime * 1000L;
                    PenSample?.Invoke(pointerId, xDip, yDip, pressure, timestampMicroseconds);
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _source.Dispose();
        }

        private static readonly IntPtr HwndTop = IntPtr.Zero;
        private static readonly IntPtr HwndTopmost = new IntPtr(-1);
        private const int GwlExStyle = -20;
        private const int WsExTopmost = 0x00000008;

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowRgn(IntPtr hWnd, IntPtr hRgn, [MarshalAs(UnmanagedType.Bool)] bool bRedraw);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern int CombineRgn(IntPtr dest, IntPtr src1, IntPtr src2, int fnCombineMode);

        // GetPointerId 是宏（GET_POINTERID_WPARAM=LOWORD(wParam)），无 DllImport 导出；已内联为
        // GetPointerIdFromWParam。GetPointerType / GetPointerTouchInfo 是 user32.dll 真实导出。

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetPointerType(uint pointerId, out int pointerType);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetPointerTouchInfo(uint pointerId, out POINTER_TOUCH_INFO pointerInfo);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetPointerPenInfo(uint pointerId, out POINTER_PEN_INFO pointerInfo);

        // ---- WM_POINTER 结构 ----

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINTER_INFO
        {
            public int pointerType;
            public uint pointerId;
            public uint frameId;
            public uint pointerFlags;
            public IntPtr sourceDevice;
            public IntPtr hwndTarget;
            public POINT ptPixelLocation;
            public POINT ptHimetricLocation;
            public POINT ptPixelLocationRaw;
            public POINT ptHimetricLocationRaw;
            public uint dwTime;
            public uint historyCount;
            public int inputData;
            public uint dwKeyStates;
            public ulong performanceCount;
            public int buttonChangeType;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINTER_TOUCH_INFO
        {
            public POINTER_INFO pointerInfo;
            public RECT rcContact;
            public RECT rcContactRaw;
            public uint orientation;
            public uint pressure;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINTER_PEN_INFO
        {
            public POINTER_INFO pointerInfo;
            public int penFlags;
            public int penMask;
            public uint pressure;
            public uint rotation;
            public int tiltX;
            public int tiltY;
        }
    }
}
