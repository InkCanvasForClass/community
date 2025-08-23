using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml.Linq;

namespace Ink_Canvas {
    
    public partial class MW_Toast : UserControl {

        private Tuple<Color, Color>[] gradientTuples = new Tuple<Color, Color>[] {
            new Tuple<Color, Color>(Color.FromRgb(246, 116, 62),Color.FromRgb(212, 37, 37)),
            new Tuple<Color, Color>(Color.FromRgb(248, 184, 6), Color.FromRgb(255, 140, 4)),
            new Tuple<Color, Color>(Color.FromRgb(45, 130, 178), Color.FromRgb(50, 154, 187)),
            new Tuple<Color, Color>(Color.FromRgb(50, 187, 113), Color.FromRgb(42, 157, 143))
        };

        private Color[] borderColors = new Color[] {
            Color.FromRgb(240, 134, 58),
            Color.FromRgb(255, 223, 141),
            Color.FromRgb(123, 207, 237),
            Color.FromRgb(67, 213, 144),
        };

        public enum ToastType {
            Error,
            Warning,
            Informative,
            Success
        }

        public DrawingImage[] tipIconDrawingImages;

        private void UpdateToastStyle(ToastType type) {
            ToastBorder.BorderBrush = new SolidColorBrush(borderColors[(int)type]);
            GradientStop1.Color = gradientTuples[(int)type].Item1;
            GradientStop2.Color = gradientTuples[(int)type].Item2;
            ToastIconImage.Source = tipIconDrawingImages[(int)type];
        }

        private string _toastText = "InkCanvasForClass";
        public string ToastText {
            get => _toastText;
            set {
                _toastText = value;
                ToastTextBlock.Text = _toastText;
            }
        }

        private ToastType _toastType = ToastType.Error;

        public ToastType Type {
            get => _toastType;
            set {
                _toastType = value;
                UpdateToastStyle(value);
            }
        }

        private void Animate(double opacityFrom, double opacityTo, int durationMs, Action complete,  EasingFunctionBase easing = null) {
            var sb = new Storyboard();

            var fadeInAnimation = new DoubleAnimation {
                From = opacityFrom,
                To = opacityTo,
                Duration = TimeSpan.FromMilliseconds(durationMs)
            };
            Storyboard.SetTargetProperty(fadeInAnimation, new PropertyPath(UIElement.OpacityProperty));
            if (easing != null) fadeInAnimation.EasingFunction = easing;
            sb.Children.Add(fadeInAnimation);
            sb.Completed += (sender, args) => {
                if (complete != null) complete();
            };

            ToastGrid.Opacity = opacityFrom;
            sb.Begin(ToastGrid);
        }

        public void ShowImmediately() {
            ToastGrid.Opacity = 1;
        }

        public void ShowAnimated() {
            HideImmediately();
            Animate(0, 1, 200, null ,new CubicEase());
        }

        public void ShowAnimatedWithAutoDispose(int autoCloseMs=3000) {
            var t = new Thread(() => {
                _isDisplay = true;
                Dispatcher.InvokeAsync(() => { ShowAnimated(); });
                Thread.Sleep(autoCloseMs);
                Dispatcher.InvokeAsync(() => {
                    Animate(1, 0, 200, (() => {
                        ShouldDisposeAction(this);
                    }), new CubicEase());
                });
                _isDisplay = false;
            });
            t.Start();
        }

        public void ShowImmediatelyWithAutoDispose(int autoCloseMs=3000) {
            var t = new Thread(() => {
                _isDisplay = true;
                Dispatcher.InvokeAsync(() => { ToastGrid.Opacity = 1; });
                Thread.Sleep(autoCloseMs);
                Dispatcher.InvokeAsync(() => {
                    Animate(1, 0, 200, (() => {
                        ShouldDisposeAction(this);
                    }), new CubicEase());
                });
                _isDisplay = false;
            });
            t.Start();
        }

        public void HideImmediately() {
            ToastGrid.Opacity = 0;
        }

        public void HideAnimated() {
            Animate(1, 0, 200, null, new CubicEase());
        }

        public Action<MW_Toast> ShouldDisposeAction;

        private bool _isDisplay = false;
        public bool IsDisplay {
            get => _isDisplay;
        }

        public MW_Toast(ToastType type, string text, Action<MW_Toast> shouldDisposeAction) {
            InitializeComponent();

            tipIconDrawingImages = new DrawingImage[] {
                this.FindResource("ErrorIcon") as DrawingImage,
                this.FindResource("WarningIcon") as DrawingImage,
                this.FindResource("InfoIcon") as DrawingImage,
                this.FindResource("SuccessIcon") as DrawingImage,
            };

            Type = type;
            ToastText = text;

            ToastGrid.Opacity = 0;
            ShouldDisposeAction = shouldDisposeAction;
        }
    }
}
