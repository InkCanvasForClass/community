using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas.Controls
{
    public partial class SidePanelToggle : UserControl
    {
        // 点击事件
        public event RoutedEventHandler Click;

        public static readonly DependencyProperty IsRightSideProperty = DependencyProperty.Register(
            nameof(IsRightSide), typeof(bool), typeof(SidePanelToggle),
            new PropertyMetadata(false, OnIsRightSideChanged));

        private double _startOffset = 0;
        private bool _isDragging = false;
        private double _startY = 0;
        private double _startX = 0;
        private double _totalDragDistance = 0;
        private bool _allowDrag = true;

        private static void OnIsRightSideChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (SidePanelToggle)d;
            control.UpdateLayoutState((bool)e.NewValue);
        }

        public bool IsRightSide
        {
            get => (bool)GetValue(IsRightSideProperty);
            set => SetValue(IsRightSideProperty, value);
        }

        public Image ChevronIcon
        {
            get
            {
                var settings = MainWindow.Settings?.Appearance;
                bool useMinimalist = settings?.UnFoldButtonImageType == 2;
                return useMinimalist ? ChevronImage : ClassicChevronImage;
            }
        }

        public SidePanelToggle()
        {
            InitializeComponent();
            
            Loaded += (s, e) => {
                ApplySettings();
            };
        }

        protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseDown(e);
            
            if (e.ChangedButton != MouseButton.Left) return;
            
            var settings = MainWindow.Settings?.Appearance;
            _allowDrag = settings == null || settings.AllowDragSidePanel;
            
            // 检查是否在正确的按钮区域内
            var targetElement = (settings?.UnFoldButtonImageType == 2) ? (FrameworkElement)PanelBorder : ClassicViewbox;
            var pos = e.GetPosition(targetElement);
            if (pos.X < 0 || pos.X > targetElement.ActualWidth || pos.Y < 0 || pos.Y > targetElement.ActualHeight)
            {
                return;
            }
            
            _isDragging = true;
            _totalDragDistance = 0;
            _startOffset = MainWindow.Settings.Appearance.QuickPanelBottomOffset;
            var startPos = e.GetPosition(this);
            _startY = startPos.Y;
            _startX = startPos.X;
            CaptureMouse();
        }

        protected override void OnPreviewMouseMove(MouseEventArgs e)
        {
            base.OnPreviewMouseMove(e);
            
            if (!_isDragging) return;
            
            var currentPos = e.GetPosition(this);
            var deltaY = _startY - currentPos.Y;
            var deltaX = _startX - currentPos.X;
            _totalDragDistance += Math.Abs(deltaX) + Math.Abs(deltaY);
            
            // 只有当拖动距离足够大时，才开始真正的拖动
            if (_totalDragDistance < 5) return;
            
            double newOffset = _startOffset + deltaY * 2;
            newOffset = Math.Max(-600, Math.Min(600, newOffset));
            
            MainWindow.Settings.Appearance.QuickPanelBottomOffset = newOffset;
            var mw = Application.Current.MainWindow as MainWindow;
            mw?.ApplyQuickPanelBottomOffset(newOffset);
            Ink_Canvas.Windows.SettingsViews.Pages.AppearancePage.NotifyBottomOffsetChanged(newOffset);
        }

        protected override void OnPreviewMouseUp(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseUp(e);

            if (e.ChangedButton != MouseButton.Left) return;

            // 如果拖动距离很小，认为是点击，触发 Click 事件
            if (_totalDragDistance < 5)
            {
                _isDragging = false;
                ReleaseMouseCapture();
                e.Handled = true;
                // 触发 Click 事件
                Click?.Invoke(this, new RoutedEventArgs());
                return;
            }

            if (_isDragging)
            {
                Ink_Canvas.Windows.SettingsViews.Helpers.SettingsManager.SaveSettingsToFile();
                e.Handled = true;
            }

            _isDragging = false;
            ReleaseMouseCapture();
        }

        protected override void OnLostMouseCapture(MouseEventArgs e)
        {
            base.OnLostMouseCapture(e);
            
            if (_isDragging)
            {
                Ink_Canvas.Windows.SettingsViews.Helpers.SettingsManager.SaveSettingsToFile();
            }
            
            _isDragging = false;
        }

        public void ApplySettings()
        {
            var settings = MainWindow.Settings?.Appearance;
            if (settings == null) return;

            bool useMinimalist = settings.UnFoldButtonImageType == 2;

            if (PanelBorder != null)
                PanelBorder.Visibility = useMinimalist ? Visibility.Visible : Visibility.Collapsed;

            if (ClassicViewbox != null)
                ClassicViewbox.Visibility = useMinimalist ? Visibility.Collapsed : Visibility.Visible;

            if (ChevronImage != null)
                ChevronImage.Visibility = Visibility.Collapsed;

            UpdateLayoutState(IsRightSide);
        }

        private void UpdateLayoutState(bool isRightSide)
        {
            var settings = MainWindow.Settings?.Appearance;
            bool useMinimalist = settings?.UnFoldButtonImageType == 2;

            if (useMinimalist)
            {
                if (PanelBorder == null || InnerStripe == null || ChevronImage == null) return;

                if (isRightSide)
                {
                    PanelBorder.HorizontalAlignment = HorizontalAlignment.Right;
                    PanelBorder.Margin = new Thickness(0, 0, 15, 0);

                    InnerStripe.HorizontalAlignment = HorizontalAlignment.Right;
                    InnerStripe.Margin = new Thickness(0, 0, 3, 0);

                    ChevronImage.RenderTransformOrigin = new Point(0.5, 0.5);
                    ChevronImage.RenderTransform = new RotateTransform(180);
                }
                else
                {
                    PanelBorder.HorizontalAlignment = HorizontalAlignment.Left;
                    PanelBorder.Margin = new Thickness(15, 0, 0, 0);

                    InnerStripe.HorizontalAlignment = HorizontalAlignment.Left;
                    InnerStripe.Margin = new Thickness(3, 0, 0, 0);

                    ChevronImage.RenderTransform = null;
                }
            }
            else
            {
                if (ClassicViewbox == null || ClassicPanelBorder == null || ClassicChevronImage == null) return;

                if (isRightSide)
                {
                    ClassicViewbox.HorizontalAlignment = HorizontalAlignment.Right;
                    ClassicPanelBorder.CornerRadius = new CornerRadius(25, 0, 0, 25);
                    ClassicChevronImage.Margin = new Thickness(0, 0, 10, 0);
                    ClassicChevronImage.RenderTransformOrigin = new Point(0.5, 0.5);
                    ClassicChevronImage.RenderTransform = new RotateTransform(180);
                }
                else
                {
                    ClassicViewbox.HorizontalAlignment = HorizontalAlignment.Left;
                    ClassicPanelBorder.CornerRadius = new CornerRadius(0, 25, 25, 0);
                    ClassicChevronImage.Margin = new Thickness(10, 0, 0, 0);
                    ClassicChevronImage.RenderTransform = null;
                }
            }
        }
    }
}
