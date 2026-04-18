using System.Windows;
using Windows.UI.Input.Inking;

namespace Ink_Canvas.Windows
{
    public partial class DirectInkTestWindow : Window
    {
        public DirectInkTestWindow()
        {
            InitializeComponent();
            
            // 初始化 DirectInkCanvas
            DirectInkCanvas.Loaded += DirectInkCanvas_Loaded;
        }

        private void DirectInkCanvas_Loaded(object sender, RoutedEventArgs e)
        {
            // 启用鼠标、触摸和触控笔输入 (WPF 默认全支持，但 UWP DirectInk 需要显式开启)
            DirectInkCanvas.InkPresenter.InputDeviceTypes =
                Windows.UI.Core.CoreInputDeviceTypes.Mouse |
                Windows.UI.Core.CoreInputDeviceTypes.Pen |
                Windows.UI.Core.CoreInputDeviceTypes.Touch;

            // 设置默认画笔属性
            SetPenColor(Windows.UI.Colors.Black);
        }

        private void SetPenColor(Windows.UI.Color color)
        {
            var drawingAttributes = new InkDrawingAttributes();
            drawingAttributes.Color = color;
            drawingAttributes.Size = new Windows.Foundation.Size(5, 5);
            drawingAttributes.IgnorePressure = false; // 启用压感
            drawingAttributes.FitToCurve = true; // 启用硬件级平滑拟合
            
            DirectInkCanvas.InkPresenter.UpdateDefaultDrawingAttributes(drawingAttributes);
        }

        private void BtnPen_Click(object sender, RoutedEventArgs e)
        {
            DirectInkCanvas.InkPresenter.InputProcessingConfiguration.Mode = InkInputProcessingMode.Inking;
        }

        private void BtnEraser_Click(object sender, RoutedEventArgs e)
        {
            DirectInkCanvas.InkPresenter.InputProcessingConfiguration.Mode = InkInputProcessingMode.Erasing;
        }

        private void BtnColorRed_Click(object sender, RoutedEventArgs e)
        {
            SetPenColor(Windows.UI.Colors.Red);
            DirectInkCanvas.InkPresenter.InputProcessingConfiguration.Mode = InkInputProcessingMode.Inking;
        }

        private void BtnColorBlack_Click(object sender, RoutedEventArgs e)
        {
            SetPenColor(Windows.UI.Colors.Black);
            DirectInkCanvas.InkPresenter.InputProcessingConfiguration.Mode = InkInputProcessingMode.Inking;
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            DirectInkCanvas.InkPresenter.StrokeContainer.Clear();
        }
    }
}