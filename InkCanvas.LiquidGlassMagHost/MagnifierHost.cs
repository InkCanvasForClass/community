using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using static InkCanvas.LiquidGlassMagHost.Native;

namespace InkCanvas.LiquidGlassMagHost
{
    /// <summary>
    /// 独立进程入口：在 STA 上创建 Magnification 宿主，抓取桌面矩形后通过 stdout
    /// 回传 BGRA 字节给主进程。绕过 WPF 主进程的 DComp 上下文，让 DWM 正常合成放大镜内容。
    ///
    /// 协议：
    ///   请求（24 字节，stderr/无）：
    ///     long  excludeHwnd (8)
    ///     int   left, top, width, height (16)
    ///   响应（stdout，二进制）：
    ///     int   status (0=OK, 1=失败, 2=不可用)
    ///     int   width
    ///     int   height
    ///     int   stride
    ///     int   bytesLen
    ///     byte[bytesLen]  BGRA
    /// </summary>
    internal static class Program
    {
        private const string HostClassName = "ICC.LiquidGlass.MagHost";
        private const string MagnifierClassName = "Magnifier";
        private const int HostAnchorRightMargin = 8;
        private const int HostAnchorBottomMargin = 8;
        private const byte HostAlpha = 1;

        // 进程内状态（STA 线程专用）
        private static bool _magInitialized;
        private static bool _classRegistered;
        private static IntPtr _hostHwnd;
        private static IntPtr _magHwnd;
        private static int _hostWidth, _hostHeight, _hostLeft, _hostTop;
        private static WndProcDelegate _wndProcRef;

        [STAThread]
        private static int Main(string[] args)
        {
            Console.Error.WriteLine($"[MagHost] starting, pid={Environment.ProcessId}, threadApt={Thread.CurrentThread.GetApartmentState()}");

            // STA + 消息泵：Magnification API 依赖创建线程的消息队列。
            // 消息泵由 PeekMessage/DispatchMessage 在抓取间隙手动抽干即可，
            // 不需要 Dispatcher.Run()。
            try
            {
                var stdin = Console.OpenStandardInput();
                var stdout = Console.OpenStandardOutput();
                Console.Error.WriteLine("[MagHost] stdin/stdout opened, waiting for first request...");

                while (true)
                {
                    // 读 24 字节请求（阻塞）。EOF 退出。
                    var header = new byte[24];
                    int got = ReadExact(stdin, header, 24);
                    if (got == 0) { Console.Error.WriteLine("[MagHost] stdin EOF, exiting"); break; } // EOF
                    if (got < 24) { Console.Error.WriteLine($"[MagHost] partial header: {got}"); return 3; }

                    long excludeHwnd = BitConverter.ToInt64(header, 0);
                    int left = BitConverter.ToInt32(header, 8);
                    int top = BitConverter.ToInt32(header, 12);
                    int width = BitConverter.ToInt32(header, 16);
                    int height = BitConverter.ToInt32(header, 20);

                    Console.Error.WriteLine($"[MagHost] request: left={left} top={top} {width}x{height} exclude=0x{excludeHwnd:X}");

                    int status = 0;
                    int outW = 0, outH = 0, outStride = 0;
                    byte[] pixels = null;

                    if (width <= 0 || height <= 0 || excludeHwnd == 0)
                    {
                        status = 1;
                    }
                    else
                    {
                        try
                        {
                            if (!CaptureOne((IntPtr)excludeHwnd, left, top, width, height,
                                out outW, out outH, out outStride, out pixels))
                            {
                                status = 1;
                            }
                        }
                        catch (DllNotFoundException ex)
                        {
                            Console.Error.WriteLine($"[MagHost] DllNotFound: {ex.Message}");
                            status = 2; // 不可用
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine($"[MagHost] capture ex: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                            status = 1;
                        }
                    }

                    Console.Error.WriteLine($"[MagHost] response: status={status} {outW}x{outH} bytes={pixels?.Length ?? 0}");

                    int bytesLen = pixels?.Length ?? 0;
                    var head = new byte[20];
                    BitConverter.GetBytes(status).CopyTo(head, 0);
                    BitConverter.GetBytes(outW).CopyTo(head, 4);
                    BitConverter.GetBytes(outH).CopyTo(head, 8);
                    BitConverter.GetBytes(outStride).CopyTo(head, 12);
                    BitConverter.GetBytes(bytesLen).CopyTo(head, 16);
                    stdout.Write(head, 0, 20);
                    if (bytesLen > 0) stdout.Write(pixels, 0, bytesLen);
                    stdout.Flush();

                    if (status == 2) { Console.Error.WriteLine("[MagHost] permanent unavailable, exiting"); break; }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[MagHost] fatal: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                return 1;
            }

            ShutdownMag();
            Console.Error.WriteLine("[MagHost] clean exit");
            return 0;
        }

        private static int ReadExact(Stream s, byte[] buf, int n)
        {
            int total = 0;
            while (total < n)
            {
                int r = s.Read(buf, total, n - total);
                if (r == 0) return total;
                total += r;
            }
            return total;
        }

        private static bool CaptureOne(IntPtr excludeHwnd, int left, int top, int width, int height,
            out int outW, out int outH, out int outStride, out byte[] pixels)
        {
            outW = outH = outStride = 0;
            pixels = null;

            if (!EnsureHostSize(width, height)) return false;

            var tr = new MAGTRANSFORM { M00 = 1f, M11 = 1f, M22 = 1f };
            if (!MagSetWindowTransform(_magHwnd, ref tr)) return false;

            var filter = new[] { excludeHwnd };
            if (!MagSetWindowFilterList(_magHwnd, MW_FILTERMODE_EXCLUDE, filter.Length, filter)) return false;

            var src = new RECT { Left = left, Top = top, Right = left + width, Bottom = top + height };
            if (!MagSetWindowSource(_magHwnd, ref src)) return false;

            InvalidateRect(_magHwnd, IntPtr.Zero, true);
            UpdateWindow(_magHwnd);
            Pump();

            return PrintHostToBgra(width, height, out outW, out outH, out outStride, out pixels);
        }

        private static bool EnsureHostSize(int width, int height)
        {
            int screenCx = GetSystemMetrics(SM_CXSCREEN);
            int screenCy = GetSystemMetrics(SM_CYSCREEN);
            int left = Math.Max(0, screenCx - width - HostAnchorRightMargin);
            int top = Math.Max(0, screenCy - height - HostAnchorBottomMargin);

            if (_hostHwnd != IntPtr.Zero && _magHwnd != IntPtr.Zero
                && _hostWidth == width && _hostHeight == height)
            {
                if (_hostLeft != left || _hostTop != top)
                {
                    _hostLeft = left;
                    _hostTop = top;
                    SetWindowPos(_hostHwnd, IntPtr.Zero, left, top, width, height,
                        SWP_NOZORDER | SWP_NOACTIVATE);
                }
                return true;
            }

            DestroyHost();
            if (!_magInitialized)
            {
                if (!MagInitialize()) return false;
                _magInitialized = true;
            }
            if (!_classRegistered)
            {
                _wndProcRef = (h, m, w, l) => DefWindowProc(h, m, w, l);
                var wc = new WNDCLASSEX
                {
                    CbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
                    LpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcRef),
                    HInstance = GetModuleHandle(null),
                    LpszClassName = HostClassName
                };
                if (RegisterClassEx(ref wc) == 0 && Marshal.GetLastWin32Error() != 1410)
                    return false;
                _classRegistered = true;
            }

            int exStyle = WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT;
            int style = WS_POPUP | WS_CLIPCHILDREN;

            _hostHwnd = CreateWindowEx(
                exStyle, HostClassName, "ICC MagHost",
                style, left, top, width, height,
                IntPtr.Zero, IntPtr.Zero, GetModuleHandle(null), IntPtr.Zero);
            if (_hostHwnd == IntPtr.Zero) return false;
            SetLayeredWindowAttributes(_hostHwnd, 0, HostAlpha, LWA_ALPHA);

            _magHwnd = CreateWindowEx(
                0, MagnifierClassName, "ICC Mag",
                WS_CHILD | WS_VISIBLE, 0, 0, width, height,
                _hostHwnd, IntPtr.Zero, GetModuleHandle(null), IntPtr.Zero);
            if (_magHwnd == IntPtr.Zero) { DestroyHost(); return false; }

            ShowWindow(_hostHwnd, SW_SHOWNOACTIVATE);
            SetWindowPos(_hostHwnd, IntPtr.Zero, left, top, width, height,
                SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            SetWindowPos(_magHwnd, IntPtr.Zero, 0, 0, width, height,
                SWP_NOZORDER | SWP_NOACTIVATE);

            _hostLeft = left;
            _hostTop = top;
            _hostWidth = width;
            _hostHeight = height;
            return true;
        }

        private static void DestroyHost()
        {
            if (_magHwnd != IntPtr.Zero) { DestroyWindow(_magHwnd); _magHwnd = IntPtr.Zero; }
            if (_hostHwnd != IntPtr.Zero) { DestroyWindow(_hostHwnd); _hostHwnd = IntPtr.Zero; }
            _hostLeft = _hostTop = _hostWidth = _hostHeight = 0;
        }

        private static void ShutdownMag()
        {
            DestroyHost();
            if (_magInitialized)
            {
                try { MagUninitialize(); } catch { }
                _magInitialized = false;
            }
        }

        private static void Pump()
        {
            var msg = new MSG();
            while (PeekMessage(ref msg, IntPtr.Zero, 0, 0, PM_REMOVE))
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
        }

        /// <summary>
        /// PrintWindow + 把 host DC 内容转成 BGRA 字节。
        /// BGRA 是 GDI GetDIBits 默认格式，跨进程传输最稳。
        /// </summary>
        private static bool PrintHostToBgra(int width, int height, out int outW, out int outH, out int outStride, out byte[] pixels)
        {
            outW = width; outH = height; outStride = width * 4; pixels = null;
            IntPtr windowDc = IntPtr.Zero, memDc = IntPtr.Zero, hBitmap = IntPtr.Zero, oldBmp = IntPtr.Zero;
            try
            {
                windowDc = GetDC(_hostHwnd);
                if (windowDc == IntPtr.Zero) return false;
                memDc = CreateCompatibleDC(windowDc);
                if (memDc == IntPtr.Zero) return false;
                hBitmap = CreateCompatibleBitmap(windowDc, width, height);
                if (hBitmap == IntPtr.Zero) return false;
                oldBmp = SelectObject(memDc, hBitmap);

                bool ok = PrintWindow(_hostHwnd, memDc, PW_CLIENTONLY | PW_RENDERFULLCONTENT);
                if (!ok) ok = PrintWindow(_hostHwnd, memDc, PW_CLIENTONLY);
                if (!ok) BitBlt(memDc, 0, 0, width, height, windowDc, 0, 0, SRCCOPY);

                // 读全部 BGRA
                var bi = new BITMAPINFOHEADER
                {
                    BiSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                    BiWidth = width,
                    BiHeight = -height, // top-down
                    BiPlanes = 1,
                    BiBitCount = 32,
                    BiCompression = BI_RGB
                };
                int stride = width * 4;
                var buffer = new byte[stride * height];
                int got = GetDIBits(memDc, hBitmap, 0, (uint)height, buffer, ref bi, DIB_RGB_COLORS);
                if (got == 0) return false;

                // 全黑快速拒绝（GetDIBits 之前必须换出位图——我们这里 BitBlt 已结束，再换也不影响）
                SelectObject(memDc, oldBmp);
                oldBmp = IntPtr.Zero;

                if (IsMostlyBlack(buffer, stride, width, height))
                {
                    Console.Error.WriteLine("MagHost: readback all-black");
                    return false;
                }

                pixels = buffer;
                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"MagHost print ex: {ex.Message}");
                return false;
            }
            finally
            {
                if (oldBmp != IntPtr.Zero && memDc != IntPtr.Zero) SelectObject(memDc, oldBmp);
                if (hBitmap != IntPtr.Zero) DeleteObject(hBitmap);
                if (memDc != IntPtr.Zero) DeleteDC(memDc);
                if (windowDc != IntPtr.Zero) ReleaseDC(_hostHwnd, windowDc);
            }
        }

        private static bool IsMostlyBlack(byte[] buf, int stride, int w, int h)
        {
            int nonBlack = 0;
            int samples = 0;
            for (int y = 0; y < h; y += Math.Max(1, h / 8))
            {
                int row = y * stride;
                for (int x = 0; x < w; x += Math.Max(1, w / 16))
                {
                    int i = row + x * 4;
                    if (i + 2 >= buf.Length) break;
                    byte b = buf[i], g = buf[i + 1], r = buf[i + 2];
                    samples++;
                    if (b > 8 || g > 8 || r > 8) nonBlack++;
                }
            }
            return samples > 0 && (nonBlack * 50) < samples;
        }

        private delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
    }
}