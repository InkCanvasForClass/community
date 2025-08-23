using OSVersionExtension;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Ink_Canvas.Helpers;
using Vanara.PInvoke;

namespace Ink_Canvas {
    public partial class MainWindow : PerformanceTransparentWin {

        public IntPtr MagnificationWinHandle;
        public IntPtr MagnificationHostWindowHandle;

        public bool isFreezeFrameLoaded = false;

        [DllImport("user32.dll")]
        static extern bool UpdateWindow(IntPtr hWnd);

        /// <summary>
        /// 初始化画面定格窗口
        /// </summary>
        /// <param name="hwndsList"></param>
        public void InitFreezeWindow(HWND[] hwndsList) {
            isFreezeFrameLoaded = false;
            if (OSVersion.GetOperatingSystem() < OSVersionExtension.OperatingSystem.Windows81) return;
            if (!Magnification.MagInitialize()) return;
            // 註冊宿主窗體類名
            var wndClassEx = new WNDCLASSEX {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(), style = CS_HREDRAW | CS_VREDRAW,
                lpfnWndProc = StaticWndProcDelegate, hInstance = IntPtr.Zero,
                hCursor = LoadCursor(IntPtr.Zero, IDC_ARROW), hbrBackground = (IntPtr)(1 + COLOR_BTNFACE),
                lpszClassName = "ICCMagnifierWindowHostForFreezeFrame",
                hIcon = IntPtr.Zero, hIconSm = IntPtr.Zero
            };
            RegisterClassEx(ref wndClassEx);
            // 創建宿主窗體
            var windowHostHandle = CreateWindowEx(
                WS_EX_TOPMOST | WS_EX_LAYERED, "ICCMagnifierWindowHostForFreezeFrame", "ICCMagnifierWindowHostWindowForFreezeFrame",
                WS_SIZEBOX | WS_SYSMENU | WS_CLIPCHILDREN | WS_CAPTION | WS_MAXIMIZEBOX, 0, 0,
                System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width,
                System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                IntPtr.Zero);
            // 設定分層窗體
            SetLayeredWindowAttributes(windowHostHandle, 0, 0, LWA_ALPHA);
            // 創建放大鏡窗體
            var hwndMag = CreateWindowEx(
                0, Magnification.WC_MAGNIFIER, "ICCMagnifierWindowForFreezeFrame", WS_CHILD | WS_VISIBLE | MS_CLIPAROUNDCURSOR, 0, 0,
                System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width,
                System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height, windowHostHandle,
                IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            // 設定窗體樣式和排布
            int style = GetWindowLong(windowHostHandle, GWL_STYLE);
            style &= ~WS_CAPTION; // 隐藏标题栏
            style &= ~WS_THICKFRAME; // 禁止窗口拉伸
            SetWindowLong(windowHostHandle, GWL_STYLE, style);
            SetWindowPos(windowHostHandle, IntPtr.Zero, 0, 0, 0, 0, SWP_NOSIZE | SWP_FRAMECHANGED);
            // 設定額外樣式
            int exStyle = GetWindowLong(windowHostHandle, GWL_EXSTYLE);
            exStyle |= WS_EX_TOOLWINDOW; /* <- 隐藏任务栏图标 */
            exStyle &= ~WS_EX_APPWINDOW;
            SetWindowLong(windowHostHandle, GWL_EXSTYLE, exStyle);
            // 导出成员
            MagnificationWinHandle = hwndMag;
            MagnificationHostWindowHandle = windowHostHandle;
            // 設定放大鏡工廠
            Magnification.MAGTRANSFORM matrix = new Magnification.MAGTRANSFORM();
            matrix[0, 0] = 1.0f;
            matrix[0, 1] = 0.0f;
            matrix[0, 2] = 0.0f;
            matrix[1, 0] = 0.0f;
            matrix[1, 1] = 1.0f;
            matrix[1, 2] = 0.0f;
            matrix[2, 0] = 1.0f;
            matrix[2, 1] = 0.0f;
            matrix[2, 2] = 0.0f;
            if (!Magnification.MagSetWindowTransform(hwndMag, matrix)) return;
            // 設定放大鏡轉化矩乘陣列
            Magnification.MAGCOLOREFFECT magEffect = new Magnification.MAGCOLOREFFECT();
            magEffect[0, 0] = 1.0f;
            magEffect[0, 1] = 0.0f;
            magEffect[0, 2] = 0.0f;
            magEffect[0, 3] = 0.0f;
            magEffect[0, 4] = 0.0f;
            magEffect[1, 0] = 0.0f;
            magEffect[1, 1] = 1.0f;
            magEffect[1, 2] = 0.0f;
            magEffect[1, 3] = 0.0f;
            magEffect[1, 4] = 0.0f;
            magEffect[2, 0] = 0.0f;
            magEffect[2, 1] = 0.0f;
            magEffect[2, 2] = 1.0f;
            magEffect[2, 3] = 0.0f;
            magEffect[2, 4] = 0.0f;
            magEffect[3, 0] = 0.0f;
            magEffect[3, 1] = 0.0f;
            magEffect[3, 2] = 0.0f;
            magEffect[3, 3] = 1.0f;
            magEffect[3, 4] = 0.0f;
            magEffect[4, 0] = 0.0f;
            magEffect[4, 1] = 0.0f;
            magEffect[4, 2] = 0.0f;
            magEffect[4, 3] = 0.0f;
            magEffect[4, 4] = 1.0f;
            if (!Magnification.MagSetColorEffect(hwndMag, magEffect)) return;
            // 顯示窗體
            ShowWindow(windowHostHandle, SW_SHOW);
            // 过滤窗口
            var hwnds = new List<HWND> { hwndMag };
            hwnds.AddRange(hwndsList);
            if (!Magnification.MagSetWindowFilterList(hwndMag, Magnification.MW_FILTERMODE.MW_FILTERMODE_EXCLUDE,
                    hwnds.Count, hwnds.ToArray())) return;
            // 设置窗口 Source
            if (!Magnification.MagSetWindowSource(hwndMag, new RECT(0, 0,
                    System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width,
                    System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height))) return;
            InvalidateRect(hwndMag, IntPtr.Zero, true);
            isFreezeFrameLoaded = true;
        }

        public async Task<bool> InitFreezeWindowAsync(HWND[] hwndsList) {
            return await Task.Run(() => {
                InitFreezeWindow(hwndsList);
                return true;
            });
        }

        public void DisposeFreezeFrame() {
            // 反注册宿主窗口
            UnregisterClass("ICCMagnifierWindowHostForFreezeFrame", IntPtr.Zero);
            // 销毁宿主窗口
            Magnification.MagUninitialize();
            DestroyWindow(MagnificationHostWindowHandle);
        }

        public void SetFreezeFrameWindowsFilterList(HWND[] hwndsList) {
            if (!isFreezeFrameLoaded) return;
            var hwnds = new List<HWND> { MagnificationWinHandle };
            hwnds.AddRange(hwndsList);
            if (!Magnification.MagSetWindowFilterList(MagnificationWinHandle, Magnification.MW_FILTERMODE.MW_FILTERMODE_EXCLUDE,
                    hwnds.Count, hwnds.ToArray())) return;
        }

        public Bitmap GetFreezedFrame() {
            if (!isFreezeFrameLoaded) return new Bitmap(1,1);
            if (!Magnification.MagSetWindowSource(MagnificationWinHandle, new RECT(0, 0,
                    System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width,
                    System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height))) return new Bitmap(1,1);
            InvalidateRect(MagnificationWinHandle, IntPtr.Zero, true);
            UpdateWindow(MagnificationHostWindowHandle);
            RECT rect;
            GetWindowRect(MagnificationWinHandle, out rect);
            Bitmap bmp = new Bitmap(rect.Width, rect.Height);
            Graphics memoryGraphics = Graphics.FromImage(bmp);
            PrintWindow(MagnificationWinHandle, memoryGraphics.GetHdc(), PW_RENDERFULLCONTENT);
            memoryGraphics.ReleaseHdc();
            return bmp;
        }

        public async Task<Bitmap> GetFreezedFrameAsync() {
            if (!isFreezeFrameLoaded) return null;
            var result = await Task.Run(GetFreezedFrame);
            return result;
        }
    }
}