using Ink_Canvas.Windows;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace Ink_Canvas.Controls
{
    /// <summary>
    /// 快抽悬浮按钮控件
    /// </summary>
    public partial class QuickDrawFloatingButtonControl : UserControl
    {
        private bool _isDragging = false;
        private Point _dragStartPoint;
        private Point _controlStartPoint;

        public QuickDrawFloatingButtonControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 快抽按钮点击事件
        /// </summary>
        private void FloatingButton_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                // 如果正在拖动，不触发点击事件
                if (_isDragging) return;
                
                // 打开快抽窗口
                var quickDrawWindow = new QuickDrawWindow();
                quickDrawWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"打开快抽窗口失败: {ex.Message}", Helpers.LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 拖动区域鼠标按下事件
        /// </summary>
        private void DragArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            
            // 记录鼠标在屏幕上的初始位置
            _dragStartPoint = this.PointToScreen(e.GetPosition(this));
            
            // 记录控件的初始位置
            var parent = this.Parent as FrameworkElement;
            if (parent != null)
            {
                var transform = this.TransformToVisual(parent);
                var currentPos = transform.Transform(new Point(0, 0));
                _controlStartPoint = currentPos;
            }
            else
            {
                var currentMargin = this.Margin;
                _controlStartPoint = new Point(
                    double.IsNaN(currentMargin.Left) ? 0 : currentMargin.Left,
                    double.IsNaN(currentMargin.Top) ? 0 : currentMargin.Top);
            }
            
            ((UIElement)sender).CaptureMouse();
            e.Handled = true;
        }

        /// <summary>
        /// 拖动区域鼠标移动事件
        /// </summary>
        private void DragArea_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && ((UIElement)sender).IsMouseCaptured)
            {
                // 获取鼠标在屏幕上的当前位置
                Point currentScreenPoint = this.PointToScreen(e.GetPosition(this));
                Vector diff = currentScreenPoint - _dragStartPoint;
                
                if (!_isDragging && (Math.Abs(diff.X) > 3 || Math.Abs(diff.Y) > 3))
                {
                    _isDragging = true;
                    // 切换到绝对定位模式
                    this.HorizontalAlignment = HorizontalAlignment.Left;
                    this.VerticalAlignment = VerticalAlignment.Top;
                }
                
                if (_isDragging)
                {
                    // 计算新位置
                    var parent = this.Parent as FrameworkElement;
                    if (parent != null)
                    {
                        // 计算屏幕坐标相对于父容器的位置
                        var parentPoint = parent.PointFromScreen(currentScreenPoint);
                        var startParentPoint = parent.PointFromScreen(_dragStartPoint);
                        
                        // 计算相对于初始位置的偏移
                        double offsetX = parentPoint.X - startParentPoint.X;
                        double offsetY = parentPoint.Y - startParentPoint.Y;
                        
                        // 新位置 = 初始位置 + 偏移
                        double newLeft = _controlStartPoint.X + offsetX;
                        double newTop = _controlStartPoint.Y + offsetY;
                        
                        // 限制在父容器范围内
                        newLeft = Math.Max(0, Math.Min(newLeft, parent.ActualWidth - this.ActualWidth));
                        newTop = Math.Max(0, Math.Min(newTop, parent.ActualHeight - this.ActualHeight));
                        
                        // 更新Margin
                        this.Margin = new Thickness(newLeft, newTop, 0, 0);
                    }
                }
            }
        }

        /// <summary>
        /// 拖动区域鼠标释放事件
        /// </summary>
        private void DragArea_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (((UIElement)sender).IsMouseCaptured)
            {
                ((UIElement)sender).ReleaseMouseCapture();
            }
            
            if (_isDragging)
            {
                Dispatcher.BeginInvoke(new Action(() => { _isDragging = false; }), 
                    DispatcherPriority.Background);
            }
            else
            {
                _isDragging = false;
            }
            
            e.Handled = true;
        }
    }
}

