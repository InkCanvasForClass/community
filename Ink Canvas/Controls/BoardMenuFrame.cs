using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas.Controls
{
    [TemplatePart(Name = PartCloseImage, Type = typeof(UIElement))]
    [TemplatePart(Name = PartAnimationRoot, Type = typeof(UIElement))]
    public class BoardMenuFrame : ContentControl
    {
        private const string PartCloseImage = "PART_CloseImage";
        private const string PartAnimationRoot = "PART_AnimationRoot";

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(object), typeof(BoardMenuFrame), new PropertyMetadata(null));

        public static readonly DependencyProperty TitleFontSizeProperty =
            DependencyProperty.Register(nameof(TitleFontSize), typeof(double), typeof(BoardMenuFrame), new PropertyMetadata(11d));

        public static readonly DependencyProperty HeaderHeightProperty =
            DependencyProperty.Register(nameof(HeaderHeight), typeof(double), typeof(BoardMenuFrame), new PropertyMetadata(48d));

        public static readonly DependencyProperty PanelCornerRadiusProperty =
            DependencyProperty.Register(nameof(PanelCornerRadius), typeof(CornerRadius), typeof(BoardMenuFrame), new PropertyMetadata(new CornerRadius(5)));

        public static readonly DependencyProperty HeaderCornerRadiusProperty =
            DependencyProperty.Register(nameof(HeaderCornerRadius), typeof(CornerRadius), typeof(BoardMenuFrame), new PropertyMetadata(new CornerRadius(6, 6, 0, 0)));

        public static readonly DependencyProperty PanelBackgroundProperty =
            DependencyProperty.Register(nameof(PanelBackground), typeof(Brush), typeof(BoardMenuFrame), new PropertyMetadata(null));

        public static readonly DependencyProperty HeaderBackgroundProperty =
            DependencyProperty.Register(nameof(HeaderBackground), typeof(Brush), typeof(BoardMenuFrame),
                new PropertyMetadata(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563eb"))));

        public static readonly DependencyProperty HeaderBorderBrushProperty =
            DependencyProperty.Register(nameof(HeaderBorderBrush), typeof(Brush), typeof(BoardMenuFrame),
                new PropertyMetadata(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1e3a8a"))));

        public static readonly DependencyProperty IsOpenProperty =
            DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(BoardMenuFrame), new PropertyMetadata(false));

        public static readonly DependencyProperty PlacementTargetProperty =
            DependencyProperty.Register(nameof(PlacementTarget), typeof(UIElement), typeof(BoardMenuFrame), new PropertyMetadata(null));

        public static readonly DependencyProperty PlacementProperty =
            DependencyProperty.Register(nameof(Placement), typeof(PlacementMode), typeof(BoardMenuFrame), new PropertyMetadata(PlacementMode.Custom));

        public static readonly DependencyProperty CustomPopupPlacementCallbackProperty =
            DependencyProperty.Register(nameof(CustomPopupPlacementCallback), typeof(CustomPopupPlacementCallback), typeof(BoardMenuFrame),
                new PropertyMetadata((CustomPopupPlacementCallback)PlaceCenteredAbove));

        private static CustomPopupPlacement[] PlaceCenteredAbove(Size popupSize, Size targetSize, Point offset)
        {
            return new[]
            {
                new CustomPopupPlacement(
                    new Point((targetSize.Width - popupSize.Width) / 2 + offset.X,
                              -popupSize.Height + offset.Y),
                    PopupPrimaryAxis.Horizontal),
                new CustomPopupPlacement(
                    new Point((targetSize.Width - popupSize.Width) / 2 + offset.X,
                              targetSize.Height - offset.Y),
                    PopupPrimaryAxis.Horizontal)
            };
        }

        public static readonly DependencyProperty PopupHorizontalOffsetProperty =
            DependencyProperty.Register(nameof(PopupHorizontalOffset), typeof(double), typeof(BoardMenuFrame), new PropertyMetadata(0d));

        public static readonly DependencyProperty PopupVerticalOffsetProperty =
            DependencyProperty.Register(nameof(PopupVerticalOffset), typeof(double), typeof(BoardMenuFrame), new PropertyMetadata(-4d));

        public object Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
        public double TitleFontSize { get => (double)GetValue(TitleFontSizeProperty); set => SetValue(TitleFontSizeProperty, value); }
        public double HeaderHeight { get => (double)GetValue(HeaderHeightProperty); set => SetValue(HeaderHeightProperty, value); }
        public CornerRadius PanelCornerRadius { get => (CornerRadius)GetValue(PanelCornerRadiusProperty); set => SetValue(PanelCornerRadiusProperty, value); }
        public CornerRadius HeaderCornerRadius { get => (CornerRadius)GetValue(HeaderCornerRadiusProperty); set => SetValue(HeaderCornerRadiusProperty, value); }
        public Brush PanelBackground { get => (Brush)GetValue(PanelBackgroundProperty); set => SetValue(PanelBackgroundProperty, value); }
        public Brush HeaderBackground { get => (Brush)GetValue(HeaderBackgroundProperty); set => SetValue(HeaderBackgroundProperty, value); }
        public Brush HeaderBorderBrush { get => (Brush)GetValue(HeaderBorderBrushProperty); set => SetValue(HeaderBorderBrushProperty, value); }
        public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
        public UIElement PlacementTarget { get => (UIElement)GetValue(PlacementTargetProperty); set => SetValue(PlacementTargetProperty, value); }
        public PlacementMode Placement { get => (PlacementMode)GetValue(PlacementProperty); set => SetValue(PlacementProperty, value); }
        public CustomPopupPlacementCallback CustomPopupPlacementCallback
        {
            get => (CustomPopupPlacementCallback)GetValue(CustomPopupPlacementCallbackProperty);
            set => SetValue(CustomPopupPlacementCallbackProperty, value);
        }
        public double PopupHorizontalOffset { get => (double)GetValue(PopupHorizontalOffsetProperty); set => SetValue(PopupHorizontalOffsetProperty, value); }
        public double PopupVerticalOffset { get => (double)GetValue(PopupVerticalOffsetProperty); set => SetValue(PopupVerticalOffsetProperty, value); }

        public event MouseButtonEventHandler CloseMouseDown;
        public event MouseButtonEventHandler CloseMouseUp;

        public UIElement AnimationTarget { get; private set; }

        private UIElement _closeImage;

        static BoardMenuFrame()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(BoardMenuFrame), new FrameworkPropertyMetadata(typeof(BoardMenuFrame)));
            VisibilityProperty.OverrideMetadata(typeof(BoardMenuFrame),
                new FrameworkPropertyMetadata(Visibility.Collapsed, OnVisibilityChanged));
        }

        private static void OnVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((BoardMenuFrame)d).IsOpen = (Visibility)e.NewValue == Visibility.Visible;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            if (_closeImage != null)
            {
                _closeImage.MouseDown -= CloseImage_MouseDown;
                _closeImage.MouseUp -= CloseImage_MouseUp;
            }
            _closeImage = GetTemplateChild(PartCloseImage) as UIElement;
            if (_closeImage != null)
            {
                _closeImage.MouseDown += CloseImage_MouseDown;
                _closeImage.MouseUp += CloseImage_MouseUp;
            }
            AnimationTarget = GetTemplateChild(PartAnimationRoot) as UIElement;
        }

        private void CloseImage_MouseDown(object sender, MouseButtonEventArgs e)
        {
            CloseMouseDown?.Invoke(sender, e);
        }

        private void CloseImage_MouseUp(object sender, MouseButtonEventArgs e)
        {
            CloseMouseUp?.Invoke(sender, e);
        }
    }
}