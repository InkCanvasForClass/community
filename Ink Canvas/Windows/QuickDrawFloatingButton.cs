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
        private bool isDragging = false;
        private Point dragStartPoint;
        private Point windowStartPoint;

        public QuickDrawFloatingButton()
        {
            InitializeComponent();
            
            // 设置无焦点状态
            this.Focusable = false;
            this.ShowInTaskbar = false;
            
            // 窗口句柄创建后应用无焦点模式
            this.SourceInitialized += QuickDrawFloatingButton_SourceInitialized;
        }

        private void QuickDrawFloatingButton_SourceInitialized(object sender, EventArgs e)
        {
            ApplyNoFocusMode();
        }


        private void FloatingButton_Loaded(object sender, RoutedEventArgs e)
        {
            // 设置位置到屏幕右下角稍微靠近中部
            SetPositionToBottomRight();
            
            // 应用无焦点模式
            ApplyNoFocusMode();
            
            // 应用置顶
            ApplyFloatingButtonTopmost();
            
            if (MainWindow.Settings?.Advanced?.EnableUIAccessTopMost != true)
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
                // 如果正在拖动，不触发点击事件
                if (isDragging) return;
                
                // 打开快抽窗口
                var quickDrawWindow = new QuickDrawWindow();
                quickDrawWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"打开快抽窗口失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void DragArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            isDragging = false;
            // 记录鼠标在屏幕上的初始位置
            dragStartPoint = this.PointToScreen(e.GetPosition(this));
            // 记录窗口的初始位置
            windowStartPoint = new Point(this.Left, this.Top);
            ((UIElement)sender).CaptureMouse();
            e.Handled = true;
        }

        private void DragArea_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && ((UIElement)sender).IsMouseCaptured)
            {
                // 获取鼠标在屏幕上的当前位置
                Point currentScreenPoint = this.PointToScreen(e.GetPosition(this));
                Vector diff = currentScreenPoint - dragStartPoint;
                
                if (!isDragging && (Math.Abs(diff.X) > 3 || Math.Abs(diff.Y) > 3))
                {
                    isDragging = true;
                }
                
                if (isDragging)
                {
                    // 使用窗口初始位置加上鼠标移动的距离
                    double newLeft = windowStartPoint.X + diff.X;
                    double newTop = windowStartPoint.Y + diff.Y;
                    
                    // 限制在屏幕范围内
                    var workingArea = SystemParameters.WorkArea;
                    newLeft = Math.Max(workingArea.Left, Math.Min(newLeft, workingArea.Right - this.Width));
                    newTop = Math.Max(workingArea.Top, Math.Min(newTop, workingArea.Bottom - this.Height));
                    
                    this.Left = newLeft;
                    this.Top = newTop;
                }
            }
        }

        private void DragArea_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (((UIElement)sender).IsMouseCaptured)
            {
                ((UIElement)sender).ReleaseMouseCapture();
            }
            
            // 延迟重置拖动状态，避免触发点击事件
            if (isDragging)
            {
                Dispatcher.BeginInvoke(new Action(() => { isDragging = false; }), 
                    DispatcherPriority.Background);
            }
            else
            {
                isDragging = false;
            }
            
            e.Handled = true;
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
        private const int WS_EX_NOACTIVATE = 0x08000000;
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
        /// 应用无焦点模式
        /// </summary>
        private void ApplyNoFocusMode()
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;
                
                int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                
                // 悬浮快抽窗口始终启用无焦点模式
                SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE);
            }
            catch (Exception)
            {
            }
        }

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

                // 悬浮快抽窗口始终启用无焦点模式，不需要检查主窗口设置

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
                    
                    // 确保无焦点模式样式正确
                    if ((exStyle & WS_EX_NOACTIVATE) == 0)
                    {
                        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE);
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
