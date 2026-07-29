using Ink_Canvas.Helpers;
using System;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
namespace Ink_Canvas
{
    public partial class MainWindow : Ink_Canvas.Helpers.PerformanceTransparentWin
    {
        /// <summary>
        /// 鼠标滚轮事件处理，用于PPT翻页
        /// 在批注/绘制模式下，若开启滚轮穿透，则把滚轮事件转发到下方窗口
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">鼠标滚轮事件参数</param>
        private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            // 视频展台特殊模式：滚轮用于缩放预览图。
            // 必须在这里拦截，因为 VideoPresenterSpecialModeContainer 在 Z 顺序最底层，
            // 鼠标事件被上层 inkCanvas 拦截，冒泡到 Window 后由这里转发到缩放处理器。
            if (_isVideoPresenterSpecialMode)
            {
                VideoPresenterSpecialMode_MouseWheel(sender, e);
                return;
            }

            // 滚轮事件来源：
            //   - IsAnnotating=true（画笔/橡皮/选择工具）→ 绘制模式，应用穿透（issue #572）
            //   - IsAnnotating=false（鼠标模式）→ 直接 return，保留原 PPT 翻页逻辑
            bool passthrough = IsAnnotating
                               && Settings.Appearance.PassThroughMouseWheelInDrawingMode;

            try
            {
                LogHelper.WriteLogToFile(
                    $"[MouseWheel] enter delta={e.Delta} isPPT={IsInPPTPresentationMode} currentMode={currentMode} isAnnotating={IsAnnotating} passSetting={Settings?.Appearance?.PassThroughMouseWheelInDrawingMode} -> passthrough={passthrough}",
                    LogHelper.LogType.Trace);
            }
            catch { }

            if (passthrough)
            {
                ForwardMouseWheelToUnderlyingWindow(e);
                e.Handled = true;
                return;
            }

            // PPT 放映：滚轮始终翻页（与工具模式无关，恢复旧行为）
            if (IsInPPTPresentationMode)
            {
                if (e.Delta >= 120) BtnPPTSlidesUp_Click(null, null);
                else if (e.Delta <= -120) BtnPPTSlidesDown_Click(null, null);
            }
        }

        #region 滚轮穿透：用 SendInput 重新注入一次滚轮事件
        // 关键点：批注模式下我们窗口的浮在最上层，必须用 SendInput 注入同方向滚轮，
        // 系统会按 Z 序把它路由到鼠标下方的目标窗口（PPT/Word 等）。
        // 但 PPT/Office 等"焦点窗口才会滚"的应用，必须先把焦点切到下层目标，
        // 再发送滚轮，否则注入的滚轮仍会被当前焦点（可能是我们的透明窗口）吞掉。
        // 因此这里采用：临时把自己切到"鼠标点击穿透"（WS_EX_TRANSPARENT）+ 自身置底，
        // 然后用 SendInput 注入滚轮 —— 系统命中测试会绕过我们，找到真实目标。

        private const uint MOUSEEVENTF_WHEEL = 0x0800;
        private const int WHEEL_DELTA = 120;
        private const int GWL_EXSTYLE = -20;
        private const long WS_EX_TRANSPARENT = 0x00000020L;
        private const long WS_EX_TOPMOST = 0x00000008L;
        private const long WS_EX_LAYERED = 0x00080000L;
        private const long WS_EX_TOOLWINDOW = 0x00000080L;
        private const int HWND_BOTTOM = 1;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;

        private long _lastForwardedWheelTick; // 防止回环触发
        private bool _wheelPassthroughInProgress;

        [DllImport("user32.dll", EntryPoint = "GetForegroundWindow")]
        private static extern IntPtr ForwardWheelGetForegroundWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", EntryPoint = "GetWindow")]
        private static extern IntPtr ForwardWheelGetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        {
            return IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : new IntPtr(GetWindowLong32(hWnd, nIndex));
        }

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            return IntPtr.Size == 8
                ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
                : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public MOUSEINPUT mi;
        }

        private const uint INPUT_MOUSE = 0;

        /// <summary>
        /// 通过 Win32 SendInput 重新注入一次同方向的滚轮事件。
        /// 注入前临时给自身加 WS_EX_TRANSPARENT 并用 SetWindowPos(HWND_BOTTOM) 让自己绕过命中测试，
        /// 然后注入滚轮，注入完成后再恢复原来的扩展样式和位置。
        /// </summary>
        private void ForwardMouseWheelToUnderlyingWindow(MouseWheelEventArgs e)
        {
            if (_wheelPassthroughInProgress) { e.Handled = true; return; }
            long now = Environment.TickCount64;
            if (now - _lastForwardedWheelTick < 16) { e.Handled = true; return; }

            IntPtr handle = new WindowInteropHelper(this).Handle;
            if (handle == IntPtr.Zero) return;

            _wheelPassthroughInProgress = true;
            _lastForwardedWheelTick = now;
            IntPtr prevExStyle = IntPtr.Zero;
            bool exStyleChanged = false;

            try
            {
                // 记录原始 ExStyle
                prevExStyle = GetWindowLongPtr(handle, GWL_EXSTYLE);
                long originalStyle = prevExStyle.ToInt64();
                try { LogHelper.WriteLogToFile($"[MouseWheel] origExStyle=0x{originalStyle:X}", LogHelper.LogType.Trace); } catch { }

                // 1) 加 WS_EX_TRANSPARENT，让命中测试能临时"看穿"我们到下层
                long newStyle = originalStyle | WS_EX_TRANSPARENT;
                if (newStyle != originalStyle)
                {
                    SetWindowLongPtr(handle, GWL_EXSTYLE, new IntPtr(newStyle));
                    exStyleChanged = true;
                    long after = GetWindowLongPtr(handle, GWL_EXSTYLE).ToInt64();
                    try { LogHelper.WriteLogToFile($"[MouseWheel] afterExStyle=0x{after:X}", LogHelper.LogType.Trace); } catch { }
                }

                // 2) 注入滚轮事件（系统会按 Z 序派发到下层，因为我们已 WS_EX_TRANSPARENT）
                // 注意：不再切 Z 序到 HWND_BOTTOM，那是闪烁的最大来源。
                var input = new INPUT
                {
                    type = INPUT_MOUSE,
                    mi = new MOUSEINPUT
                    {
                        dx = 0,
                        dy = 0,
                        mouseData = unchecked((uint)e.Delta),
                        dwFlags = MOUSEEVENTF_WHEEL,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                };
                bool ok = SendInput(1, new[] { input }, Marshal.SizeOf(typeof(INPUT)));
                try { LogHelper.WriteLogToFile($"[MouseWheel] SendInput ret={ok} delta={e.Delta}", LogHelper.LogType.Trace); } catch { }

                e.Handled = true;
            }
            catch (Exception ex)
            {
                try { LogHelper.WriteLogToFile($"[MouseWheel] 注入滚轮失败: {ex.Message}", LogHelper.LogType.Error); } catch { }
            }
            finally
            {
                // 3) 恢复扩展样式（延迟一两帧，确保系统已完成滚轮派发）
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (exStyleChanged)
                        {
                            SetWindowLongPtr(handle, GWL_EXSTYLE, prevExStyle);
                        }
                    }
                    catch (Exception ex)
                    {
                        try { LogHelper.WriteLogToFile($"[MouseWheel] 恢复窗口样式失败: {ex.Message}", LogHelper.LogType.Error); } catch { }
                    }
                    finally
                    {
                        _wheelPassthroughInProgress = false;
                    }
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        #endregion

        /// <summary>
        /// 键盘按键预览事件处理，用于PPT翻页
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">键盘事件参数</param>
        private void Main_Grid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!IsInPPTPresentationMode || currentMode != 0) return;

            if (e.Key == Key.Down || e.Key == Key.PageDown || e.Key == Key.Right || e.Key == Key.N || e.Key == Key.Space)
            {
                BtnPPTSlidesDown_Click(null, null);
            }
            if (e.Key == Key.Up || e.Key == Key.PageUp || e.Key == Key.Left || e.Key == Key.P)
            {
                BtnPPTSlidesUp_Click(null, null);
            }
        }


        /// <summary>
        /// 撤销操作热键处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">执行路由事件参数</param>
        private void HotKey_Undo(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                SymbolIconUndo_MouseUp(lastBorderMouseDownObject, null);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
        }

        /// <summary>
        /// 重做操作热键处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">执行路由事件参数</param>
        private void HotKey_Redo(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                SymbolIconRedo_MouseUp(lastBorderMouseDownObject, null);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
        }

        /// <summary>
        /// 清空画布热键处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">执行路由事件参数</param>
        private void HotKey_Clear(object sender, ExecutedRoutedEventArgs e)
        {
            SymbolIconDelete_MouseUp(lastBorderMouseDownObject, null);
        }


        /// <summary>
        /// 退出PPT放映热键处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">执行路由事件参数</param>
        internal async void KeyExit(object sender, ExecutedRoutedEventArgs e)
        {
            if (currentMode != 0)
            {
                ImageBlackboard_MouseUp(lastBorderMouseDownObject, null);
                return;
            }

            if (IsInPPTPresentationMode) await ExitPPTPresentation();
        }

        /// <summary>
        /// 切换到绘图工具热键处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">执行路由事件参数</param>
        private async void KeyChangeToDrawTool(object sender, ExecutedRoutedEventArgs e)
        {
            if (isFloatingBarFolded)
            {
                await UnFoldFloatingBar(new object());
            }
            PenIcon_Click(lastBorderMouseDownObject, null);
        }

        /// <summary>
        /// 退出绘图工具热键处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">执行路由事件参数</param>
        /// <remarks>
        /// 在白板模式下，alt+q 退出白板模式
        /// 在非白板模式下，alt+q 切换到鼠标模式
        /// </remarks>
        internal void KeyChangeToQuitDrawTool(object sender, ExecutedRoutedEventArgs e)
        {
            if (currentMode != 0)
            {
                // 在白板模式下，alt+q 退出白板模式
                ImageBlackboard_MouseUp(lastBorderMouseDownObject, null);
            }
            else
            {
                // 在非白板模式下，alt+q 切换到鼠标模式
                CursorIcon_Click(lastBorderMouseDownObject, null);
            }
        }

        /// <summary>
        /// 切换到选择工具热键处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">执行路由事件参数</param>
        /// <remarks>仅当画布控件面板可见时生效</remarks>
        private void KeyChangeToSelect(object sender, ExecutedRoutedEventArgs e)
        {
            if (!IsAnnotating)
            {
                PenIcon_Click(lastBorderMouseDownObject, null);
            }
            SymbolIconSelect_MouseUp(lastBorderMouseDownObject, null);
        }

        /// <summary>
        /// 切换到橡皮擦工具热键处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">执行路由事件参数</param>
        /// <remarks>仅当画布控件面板可见时生效，根据当前橡皮擦状态选择相应的橡皮擦模式</remarks>
        private async void KeyChangeToEraser(object sender, ExecutedRoutedEventArgs e)
        {
            if (isFloatingBarFolded)
            {
                await UnFoldFloatingBar(new object());
            }

            if (!IsAnnotating)
            {
                PenIcon_Click(lastBorderMouseDownObject, null);
            }

            if (Eraser_Icon.Background != null)
                EraserIconByStrokes_Click(lastBorderMouseDownObject, null);
            else
                EraserIcon_Click(lastBorderMouseDownObject, null);
        }

        /// <summary>
        /// 切换到白板模式热键处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">执行路由事件参数</param>
        private void KeyChangeToBoard(object sender, ExecutedRoutedEventArgs e)
        {
            ImageBlackboard_MouseUp(lastBorderMouseDownObject, null);
        }

        /// <summary>
        /// 屏幕截图热键处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">执行路由事件参数</param>
        private void KeyCapture(object sender, ExecutedRoutedEventArgs e)
        {
            SaveScreenShotToDesktop();
        }

        /// <summary>
        /// 绘制直线热键处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">执行路由事件参数</param>
        /// <remarks>仅当画布控件面板可见时生效</remarks>
        private void KeyDrawLine(object sender, ExecutedRoutedEventArgs e)
        {
            DrawLineFromHotkey();
        }

        internal async void DrawLineFromHotkey()
        {
            if (isFloatingBarFolded)
            {
                await UnFoldFloatingBar(new object());
            }

            if (!IsAnnotating)
            {
                PenIcon_Click(lastBorderMouseDownObject, null);
            }

            BtnDrawLine_Click(lastMouseDownSender, null);
        }

        internal async void SwitchToEraserFromHotkey()
        {
            if (isFloatingBarFolded)
            {
                await UnFoldFloatingBar(new object());
            }

            if (!IsAnnotating)
            {
                PenIcon_Click(lastBorderMouseDownObject, null);
            }

            EraserIcon_Click(lastBorderMouseDownObject, null);
        }

        internal async void SwitchToSelectFromHotkey()
        {
            if (isFloatingBarFolded)
            {
                await UnFoldFloatingBar(new object());
            }

            if (!IsAnnotating)
            {
                PenIcon_Click(lastBorderMouseDownObject, null);
            }

            SymbolIconSelect_MouseUp(lastBorderMouseDownObject, null);
        }

        /// <summary>
        /// 隐藏工具栏热键处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">执行路由事件参数</param>
        private void KeyHide(object sender, ExecutedRoutedEventArgs e)
        {
            SymbolIconEmoji_MouseUp(null, null);
        }
    }
}
