using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Ink_Canvas.Controls
{
    public partial class CopyButton : UserControl
    {
        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
            nameof(Text), typeof(string), typeof(CopyButton), new PropertyMetadata(string.Empty));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public event EventHandler Click;

        public CopyButton()
        {
            InitializeComponent();
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(Text))
                {
                    Clipboard.SetText(Text);
                }

                ShowSuccessAnimation();
                Click?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Unable to Perform Copy", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ShowSuccessAnimation()
        {
            var copyScaleAnim = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromMilliseconds(150)
            };
            ScaleTransform_Copy.BeginAnimation(ScaleTransform.ScaleXProperty, copyScaleAnim);

            var copyOpacityAnim = new DoubleAnimation
            {
                To = 0,
                BeginTime = TimeSpan.FromMilliseconds(100),
                Duration = TimeSpan.FromMilliseconds(10)
            };
            FontIcon_Copy.BeginAnimation(UIElement.OpacityProperty, copyOpacityAnim);

            await Task.Delay(150);
            var successScaleAnim = new DoubleAnimation
            {
                To = 1,
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.2 }
            };
            ScaleTransform_Success.BeginAnimation(ScaleTransform.ScaleXProperty, successScaleAnim);

            var successOpacityAnim = new DoubleAnimation
            {
                To = 1,
                Duration = TimeSpan.FromMilliseconds(15)
            };
            FontIcon_Success.BeginAnimation(UIElement.OpacityProperty, successOpacityAnim);

            await Task.Delay(1000);
            ShowCopyAnimation();
        }

        private async void ShowCopyAnimation()
        {
            var successOpacityAnim = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromMilliseconds(150)
            };
            FontIcon_Success.BeginAnimation(UIElement.OpacityProperty, successOpacityAnim);

            await Task.Delay(150);
            var copyScaleAnim = new DoubleAnimation
            {
                To = 1,
                Duration = TimeSpan.Zero
            };
            ScaleTransform_Copy.BeginAnimation(ScaleTransform.ScaleXProperty, copyScaleAnim);

            var copyOpacityAnim = new DoubleAnimation
            {
                To = 1,
                Duration = TimeSpan.FromMilliseconds(150)
            };
            FontIcon_Copy.BeginAnimation(UIElement.OpacityProperty, copyOpacityAnim);

            var successScaleAnim = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.Zero
            };
            ScaleTransform_Success.BeginAnimation(ScaleTransform.ScaleXProperty, successScaleAnim);
        }
    }
}
