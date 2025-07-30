using System;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

// 为了避免命名冲突，使用别名
using WpfCanvas = System.Windows.Controls.Canvas;

namespace Ink_Canvas
{
    public partial class ScreenshotSelectorWindow : Window
    {
        private bool _isSelecting = false;
        private System.Windows.Point _startPoint;
        private System.Windows.Point _currentPoint;
        
        public Rectangle? SelectedArea { get; private set; }

        public ScreenshotSelectorWindow()
        {
            InitializeComponent();
            
            // 设置窗口覆盖所有屏幕
            SetupFullScreenOverlay();
            
            // 隐藏提示文字的定时器
            var timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(3);
            timer.Tick += (s, e) =>
            {
                HintText.Visibility = Visibility.Collapsed;
                timer.Stop();
            };
            timer.Start();
        }

        private void SetupFullScreenOverlay()
        {
            // 获取所有屏幕的虚拟屏幕边界
            var virtualScreen = System.Windows.Forms.SystemInformation.VirtualScreen;

            // 转换为WPF坐标系统
            var dpiScale = GetDpiScale();

            this.Left = virtualScreen.Left / dpiScale;
            this.Top = virtualScreen.Top / dpiScale;
            this.Width = virtualScreen.Width / dpiScale;
            this.Height = virtualScreen.Height / dpiScale;
        }

        private double GetDpiScale()
        {
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                return source.CompositionTarget.TransformToDevice.M11;
            }
            return 1.0; // 默认DPI
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                // 取消截图
                SelectedArea = null;
                DialogResult = false;
                Close();
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isSelecting = true;
            _startPoint = e.GetPosition(this);
            _currentPoint = _startPoint;
            
            // 隐藏提示文字
            HintText.Visibility = Visibility.Collapsed;
            
            // 显示选择矩形
            SelectionRectangle.Visibility = Visibility.Visible;
            SizeInfoBorder.Visibility = Visibility.Visible;
            
            // 捕获鼠标
            CaptureMouse();
            
            UpdateSelection();
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isSelecting)
            {
                _currentPoint = e.GetPosition(this);
                UpdateSelection();
            }
        }

        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isSelecting)
            {
                _isSelecting = false;
                ReleaseMouseCapture();

                // 计算选择区域
                var rect = GetSelectionRectangle();
                if (rect.Width > 5 && rect.Height > 5) // 最小尺寸检查
                {
                    // 转换为屏幕坐标，考虑DPI缩放
                    var dpiScale = GetDpiScale();
                    var virtualScreen = System.Windows.Forms.SystemInformation.VirtualScreen;

                    // 计算实际屏幕坐标
                    int screenX = (int)((rect.X * dpiScale) + virtualScreen.Left);
                    int screenY = (int)((rect.Y * dpiScale) + virtualScreen.Top);
                    int screenWidth = (int)(rect.Width * dpiScale);
                    int screenHeight = (int)(rect.Height * dpiScale);

                    SelectedArea = new Rectangle(screenX, screenY, screenWidth, screenHeight);
                    DialogResult = true;
                }
                else
                {
                    SelectedArea = null;
                    DialogResult = false;
                }

                Close();
            }
        }

        private void UpdateSelection()
        {
            var rect = GetSelectionRectangle();

            // 更新选择矩形
            WpfCanvas.SetLeft(SelectionRectangle, rect.X);
            WpfCanvas.SetTop(SelectionRectangle, rect.Y);
            SelectionRectangle.Width = rect.Width;
            SelectionRectangle.Height = rect.Height;

            // 更新尺寸信息
            SizeInfoText.Text = $"{(int)rect.Width} x {(int)rect.Height}";
            WpfCanvas.SetLeft(SizeInfoBorder, rect.X);
            WpfCanvas.SetTop(SizeInfoBorder, rect.Y - 30);

            // 确保尺寸信息不超出屏幕
            if (WpfCanvas.GetTop(SizeInfoBorder) < 0)
            {
                WpfCanvas.SetTop(SizeInfoBorder, rect.Y + rect.Height + 5);
            }
        }

        private Rect GetSelectionRectangle()
        {
            double x = Math.Min(_startPoint.X, _currentPoint.X);
            double y = Math.Min(_startPoint.Y, _currentPoint.Y);
            double width = Math.Abs(_currentPoint.X - _startPoint.X);
            double height = Math.Abs(_currentPoint.Y - _startPoint.Y);
            
            return new Rect(x, y, width, height);
        }
    }
}
