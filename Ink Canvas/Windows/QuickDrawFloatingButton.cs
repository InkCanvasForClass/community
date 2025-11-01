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
        public QuickDrawFloatingButton()
        {
            InitializeComponent();
            
            // 设置无焦点状态
            this.Focusable = false;
            this.ShowInTaskbar = false;
        }


        private void FloatingButton_Loaded(object sender, RoutedEventArgs e)
        {
            // 设置位置到屏幕右下角稍微靠近中部
            SetPositionToBottomRight();
            
            // 应用置顶
            ApplyFloatingButtonTopmost();
            
            // 如果主窗口在无焦点模式下，启动置顶维护
            if (MainWindow.Settings?.Advanced?.IsNoFocusMode == true && 
                MainWindow.Settings?.Advanced?.EnableUIAccessTopMost != true)
            {
                StartTopmostMaintenance();
            }
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




        #region Win32 API 声明和置顶管理
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentProcessId();

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOPMOST = 0x00000008;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_NOOWNERZORDER = 0x0200;

        // 添加定时器来维护置顶状态
        private DispatcherTimer topmostMaintenanceTimer;
        private bool isTopmostMaintenanceEnabled;

        /// <summary>
        /// 应用悬浮按钮置顶
        /// </summary>
        private void ApplyFloatingButtonTopmost()
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;
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

        /// <summary>
        /// 启动置顶维护定时器
        /// </summary>
        private void StartTopmostMaintenance()
        {
            if (MainWindow.Settings?.Advanced?.EnableUIAccessTopMost == true)
            {
                return;
            }

            if (isTopmostMaintenanceEnabled) return;

            if (topmostMaintenanceTimer == null)
            {
                topmostMaintenanceTimer = new DispatcherTimer();
                topmostMaintenanceTimer.Interval = TimeSpan.FromMilliseconds(500); // 每500ms检查一次
                topmostMaintenanceTimer.Tick += TopmostMaintenanceTimer_Tick;
            }

            topmostMaintenanceTimer.Start();
            isTopmostMaintenanceEnabled = true;
        }

        /// <summary>
        /// 停止置顶维护定时器
        /// </summary>
        private void StopTopmostMaintenance()
        {
            if (topmostMaintenanceTimer != null && isTopmostMaintenanceEnabled)
            {
                topmostMaintenanceTimer.Stop();
                isTopmostMaintenanceEnabled = false;
            }
        }

        /// <summary>
        /// 置顶维护定时器事件
        /// </summary>
        private void TopmostMaintenanceTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                if (MainWindow.Settings?.Advanced?.EnableUIAccessTopMost == true)
                {
                    StopTopmostMaintenance();
                    return;
                }

                if (MainWindow.Settings?.Advanced?.IsNoFocusMode != true)
                {
                    StopTopmostMaintenance();
                    return;
                }

                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;

                // 检查窗口是否仍然可见且不是最小化状态
                if (!IsWindow(hwnd) || !IsWindowVisible(hwnd) || IsIconic(hwnd))
                {
                    return;
                }

                // 检查是否有子窗口在前景
                var foregroundWindow = GetForegroundWindow();
                if (foregroundWindow != hwnd)
                {
                    // 检查前景窗口是否是当前应用程序的子窗口
                    var foregroundWindowProcessId = GetWindowThreadProcessId(foregroundWindow, out uint processId);
                    var currentProcessId = GetCurrentProcessId();

                    if (processId == currentProcessId)
                    {
                        // 如果有子窗口在前景，暂停置顶维护
                        return;
                    }

                    // 如果窗口不在最顶层且没有子窗口，重新设置置顶
                    SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0,
                        SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW | SWP_NOOWNERZORDER);

                    // 确保窗口样式正确
                    int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                    if ((exStyle & WS_EX_TOPMOST) == 0)
                    {
                        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOPMOST);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"快抽悬浮按钮置顶维护定时器出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 窗口关闭时停止置顶维护定时器
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            StopTopmostMaintenance();
            base.OnClosed(e);
        }
        #endregion
    }
}
