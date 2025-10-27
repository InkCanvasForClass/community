using Ink_Canvas.Helpers;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace Ink_Canvas
{
    /// <summary>
    /// 快抽悬浮按钮
    /// </summary>
    public partial class QuickDrawFloatingButton : Window
    {
        // 定期维护置顶的定时器
        private DispatcherTimer _topmostTimer;

        public QuickDrawFloatingButton()
        {
            InitializeComponent();
            
            // 设置无焦点状态
            this.Focusable = false;
            this.ShowInTaskbar = false;

            // 停止定时器并清理
            this.Closed += QuickDrawFloatingButton_Closed;
            this.Unloaded += QuickDrawFloatingButton_Unloaded;
        }


        private void FloatingButton_Loaded(object sender, RoutedEventArgs e)
        {
            // 设置位置到屏幕右下角稍微靠近中部
            SetPositionToBottomRight();
            
            // 启动定时维护置顶
            StartTopmostTimer();

            // 保留立即应用置顶以避免短时间内失效
            ApplyFloatingButtonTopmost();
        }

        private void SetPositionToBottomRight()
        {
            try
            {
                // 获取主屏幕的工作区域
                var workingArea = SystemParameters.WorkArea;
                this.Left = workingArea.Right - this.Width - 0;
                this.Top = workingArea.Bottom - this.Height - 200;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"设置悬浮按钮位置失败: {ex.Message}", LogHelper.LogType.Error);
                // 如果计算失败，使用默认位置
                this.Left = 720;
                this.Top = 400;
            }
        }

        private void FloatingButton_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                // 打开快抽窗口
                var quickDrawWindow = new QuickDrawWindow();
                quickDrawWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"打开快抽窗口失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void QuickDrawFloatingButton_Unloaded(object sender, RoutedEventArgs e)
        {
            StopTopmostTimer();
        }

        private void QuickDrawFloatingButton_Closed(object sender, EventArgs e)
        {
            StopTopmostTimer();
        }

        private void StartTopmostTimer()
        {
            try
            {
                if (_topmostTimer != null) return;

                _topmostTimer = new DispatcherTimer(DispatcherPriority.Normal)
                {
                    Interval = TimeSpan.FromSeconds(1.5)
                };
                _topmostTimer.Tick += TopmostTimer_Tick;
                _topmostTimer.Start();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"启动置顶维护定时器失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void StopTopmostTimer()
        {
            try
            {
                if (_topmostTimer == null) return;
                _topmostTimer.Stop();
                _topmostTimer.Tick -= TopmostTimer_Tick;
                _topmostTimer = null;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"停止置顶维护定时器失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void TopmostTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                var main = Application.Current?.MainWindow as Window;
                if (main == null) return;

                // 保持与主窗口一致的 Topmost 状态
                if (this.Topmost != main.Topmost)
                {
                    this.Topmost = main.Topmost;

                    // 若需要强制置顶，尝试使用 Win32
                    var hwnd = new WindowInteropHelper(this).Handle;
                    if (hwnd != IntPtr.Zero && this.Topmost)
                    {
                        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0,
                            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW | SWP_NOOWNERZORDER);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"置顶维护Tick失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }


        #region Win32 API 声明和置顶管理
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOPMOST = 0x00000008;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_NOOWNERZORDER = 0x0200;

        /// <summary>
        /// 应用悬浮按钮置顶
        /// </summary>
        private void ApplyFloatingButtonTopmost()
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;

                // 强制激活窗口
                Activate();
                Focus();

                // 设置WPF的Topmost属性
                Topmost = true;

                // 使用Win32 API强制置顶
                // 1. 设置窗口样式为置顶
                int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOPMOST);

                // 2. 使用SetWindowPos确保窗口在最顶层
                SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW | SWP_NOOWNERZORDER);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"应用快抽悬浮按钮置顶失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }
        #endregion
    }
}
