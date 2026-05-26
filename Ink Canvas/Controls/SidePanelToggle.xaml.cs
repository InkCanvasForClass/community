using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas.Controls
{
    public partial class SidePanelToggle : UserControl
    {
        public static readonly DependencyProperty IsRightSideProperty = DependencyProperty.Register(
            nameof(IsRightSide), typeof(bool), typeof(SidePanelToggle),
            new PropertyMetadata(false, OnIsRightSideChanged));

        private bool _isPressed = false;
        private bool _isDragging = false;
        private double _startY = 0;
        private double _startOffset = 0;
        private bool _justFinishedTouchDragging = false;

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
            if (e.ChangedButton == MouseButton.Left)
            {
                var settings = MainWindow.Settings?.Appearance;
                bool allowDrag = settings == null || settings.AllowDragSidePanel;
                if (!allowDrag) return;

                var mw = Application.Current.MainWindow as MainWindow;
                if (mw != null)
                {
                    _isPressed = true;
                    _isDragging = false;
                    _startY = e.GetPosition(mw).Y;
                    _startOffset = MainWindow.Settings.Appearance.QuickPanelBottomOffset;
                    CaptureMouse();
                }
            }
        }

        protected override void OnPreviewMouseMove(MouseEventArgs e)
        {
            base.OnPreviewMouseMove(e);
            if (_isPressed)
            {
                var mw = Application.Current.MainWindow as MainWindow;
                if (mw != null)
                {
                    double currentY = e.GetPosition(mw).Y;
                    double deltaY = currentY - _startY;
                    if (!_isDragging && Math.Abs(deltaY) > 4)
                    {
                        _isDragging = true;
                    }

                    if (_isDragging)
                    {
                        double newOffset = _startOffset - deltaY * 2;
                        // Clamp the offset to a reasonable range, e.g. -600 to 600
                        newOffset = Math.Max(-600, Math.Min(600, newOffset));
                        
                        // Update settings in memory
                        MainWindow.Settings.Appearance.QuickPanelBottomOffset = newOffset;
                        
                        // Apply layout change in real-time
                        mw.ApplyQuickPanelBottomOffset(newOffset);
                        
                        // Notify Settings slider to update in real-time if open
                        Ink_Canvas.Windows.SettingsViews.Pages.AppearancePage.NotifyBottomOffsetChanged(newOffset);
                    }
                }
            }
        }

        protected override void OnPreviewMouseUp(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseUp(e);
            if (_justFinishedTouchDragging)
            {
                _justFinishedTouchDragging = false;
                e.Handled = true;
                _isPressed = false;
                _isDragging = false;
                if (IsMouseCaptured)
                {
                    ReleaseMouseCapture();
                }
                return;
            }

            if (_isPressed)
            {
                ReleaseMouseCapture();
                _isPressed = false;
                if (_isDragging)
                {
                    _isDragging = false;
                    
                    // Save settings permanently
                    Ink_Canvas.Windows.SettingsViews.Helpers.SettingsManager.SaveSettingsToFile();
                    
                    // Prevent this mouse up from triggering the click handler in MainWindow
                    e.Handled = true;
                }
            }
        }

        protected override void OnPreviewTouchDown(TouchEventArgs e)
        {
            base.OnPreviewTouchDown(e);
            var settings = MainWindow.Settings?.Appearance;
            bool allowDrag = settings == null || settings.AllowDragSidePanel;
            if (!allowDrag) return;

            var mw = Application.Current.MainWindow as MainWindow;
            if (mw != null)
            {
                _isPressed = true;
                _isDragging = false;
                _startY = e.GetTouchPoint(mw).Position.Y;
                _startOffset = MainWindow.Settings.Appearance.QuickPanelBottomOffset;
                CaptureTouch(e.TouchDevice);
            }
        }

        protected override void OnPreviewTouchMove(TouchEventArgs e)
        {
            base.OnPreviewTouchMove(e);
            if (_isPressed)
            {
                var mw = Application.Current.MainWindow as MainWindow;
                if (mw != null)
                {
                    double currentY = e.GetTouchPoint(mw).Position.Y;
                    double deltaY = currentY - _startY;
                    if (!_isDragging && Math.Abs(deltaY) > 4)
                    {
                        _isDragging = true;
                    }

                    if (_isDragging)
                    {
                        double newOffset = _startOffset - deltaY * 2;
                        newOffset = Math.Max(-600, Math.Min(600, newOffset));
                        
                        // Update settings in memory
                        MainWindow.Settings.Appearance.QuickPanelBottomOffset = newOffset;
                        
                        // Apply layout change in real-time
                        mw.ApplyQuickPanelBottomOffset(newOffset);
                        
                        // Notify Settings slider to update in real-time if open
                        Ink_Canvas.Windows.SettingsViews.Pages.AppearancePage.NotifyBottomOffsetChanged(newOffset);
                    }
                }
            }
        }

        protected override void OnPreviewTouchUp(TouchEventArgs e)
        {
            base.OnPreviewTouchUp(e);
            if (_isPressed)
            {
                ReleaseTouchCapture(e.TouchDevice);
                _isPressed = false;
                if (_isDragging)
                {
                    _isDragging = false;
                    _justFinishedTouchDragging = true;
                    
                    // Save settings permanently
                    Ink_Canvas.Windows.SettingsViews.Helpers.SettingsManager.SaveSettingsToFile();
                    
                    // Prevent touch up from triggering the click handler in MainWindow
                    e.Handled = true;
                }
            }
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
                    // Align Right inside the container so it is near the right edge of the screen
                    PanelBorder.HorizontalAlignment = HorizontalAlignment.Right;
                    // Since container right is offscreen by 10px, 15px margin makes it exactly 5px away from the right edge
                    PanelBorder.Margin = new Thickness(0, 0, 15, 0);

                    // Align inner stripe to the right edge of the outer track (tucked towards screen boundary)
                    InnerStripe.HorizontalAlignment = HorizontalAlignment.Right;
                    InnerStripe.Margin = new Thickness(0, 0, 3, 0);

                    ChevronImage.RenderTransformOrigin = new Point(0.5, 0.5);
                    ChevronImage.RenderTransform = new RotateTransform(180);
                }
                else
                {
                    // Align Left inside the container so it is near the left edge of the screen
                    PanelBorder.HorizontalAlignment = HorizontalAlignment.Left;
                    // Since container left is offscreen by 10px, 15px margin makes it exactly 5px away from the left edge
                    PanelBorder.Margin = new Thickness(15, 0, 0, 0);

                    // Align inner stripe to the left edge of the outer track (tucked towards screen boundary)
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
