using System.Windows;
using Windows.UI.Input.Inking;
using Windows.UI.Core;

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
                global::Windows.UI.Core.CoreInputDeviceTypes.Mouse |
                global::Windows.UI.Core.CoreInputDeviceTypes.Pen |
                global::Windows.UI.Core.CoreInputDeviceTypes.Touch;

            // 设置默认画笔属性
            SetPenColor(global::Windows.UI.Colors.Black);
        }

        private void SetPenColor(global::Windows.UI.Color color)
        {
            var uwpDrawingAttributes = new global::Windows.UI.Input.Inking.InkDrawingAttributes();
            uwpDrawingAttributes.Color = color;
            uwpDrawingAttributes.Size = new global::Windows.Foundation.Size(5, 5);
            uwpDrawingAttributes.IgnorePressure = false; // 启用压感
            uwpDrawingAttributes.FitToCurve = true; // 启用硬件级平滑拟合
            
            DirectInkCanvas.InkPresenter.UpdateDefaultDrawingAttributes(uwpDrawingAttributes);
        }

        private void BtnPen_Click(object sender, RoutedEventArgs e)
        {
            DirectInkCanvas.InkPresenter.InputProcessingConfiguration.Mode = global::Windows.UI.Input.Inking.InkInputProcessingMode.Inking;
        }

        private void BtnEraser_Click(object sender, RoutedEventArgs e)
        {
            DirectInkCanvas.InkPresenter.InputProcessingConfiguration.Mode = global::Windows.UI.Input.Inking.InkInputProcessingMode.Erasing;
        }

        private void BtnColorRed_Click(object sender, RoutedEventArgs e)
        {
            SetPenColor(global::Windows.UI.Colors.Red);
            DirectInkCanvas.InkPresenter.InputProcessingConfiguration.Mode = global::Windows.UI.Input.Inking.InkInputProcessingMode.Inking;
        }

        private void BtnColorBlack_Click(object sender, RoutedEventArgs e)
        {
            SetPenColor(global::Windows.UI.Colors.Black);
            DirectInkCanvas.InkPresenter.InputProcessingConfiguration.Mode = global::Windows.UI.Input.Inking.InkInputProcessingMode.Inking;
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            DirectInkCanvas.InkPresenter.StrokeContainer.Clear();
        }
    }
}