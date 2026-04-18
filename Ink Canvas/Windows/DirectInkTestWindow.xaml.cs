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
                Microsoft.Toolkit.Win32.UI.Controls.Interop.WinRT.CoreInputDeviceTypes.Mouse |
                Microsoft.Toolkit.Win32.UI.Controls.Interop.WinRT.CoreInputDeviceTypes.Pen |
                Microsoft.Toolkit.Win32.UI.Controls.Interop.WinRT.CoreInputDeviceTypes.Touch;

            // 设置默认画笔属性
            SetPenColor(global::Windows.UI.Colors.Black);
        }

        private void SetPenColor(global::Windows.UI.Color color)
        {
            var drawingAttributes = new InkDrawingAttributes();
            drawingAttributes.Color = color;
            drawingAttributes.Size = new global::Windows.Foundation.Size(5, 5);
            drawingAttributes.IgnorePressure = false; // 启用压感
            drawingAttributes.FitToCurve = true; // 启用硬件级平滑拟合
            
            DirectInkCanvas.InkPresenter.UpdateDefaultDrawingAttributes(new Microsoft.Toolkit.Win32.UI.Controls.Interop.WinRT.InkDrawingAttributes(drawingAttributes));
        }

        private void BtnPen_Click(object sender, RoutedEventArgs e)
        {
            DirectInkCanvas.InkPresenter.InputProcessingConfiguration.Mode = Microsoft.Toolkit.Win32.UI.Controls.Interop.WinRT.InkInputProcessingMode.Inking;
        }

        private void BtnEraser_Click(object sender, RoutedEventArgs e)
        {
            DirectInkCanvas.InkPresenter.InputProcessingConfiguration.Mode = Microsoft.Toolkit.Win32.UI.Controls.Interop.WinRT.InkInputProcessingMode.Erasing;
        }

        private void BtnColorRed_Click(object sender, RoutedEventArgs e)
        {
            SetPenColor(global::Windows.UI.Colors.Red);
            DirectInkCanvas.InkPresenter.InputProcessingConfiguration.Mode = Microsoft.Toolkit.Win32.UI.Controls.Interop.WinRT.InkInputProcessingMode.Inking;
        }

        private void BtnColorBlack_Click(object sender, RoutedEventArgs e)
        {
            SetPenColor(global::Windows.UI.Colors.Black);
            DirectInkCanvas.InkPresenter.InputProcessingConfiguration.Mode = Microsoft.Toolkit.Win32.UI.Controls.Interop.WinRT.InkInputProcessingMode.Inking;
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            DirectInkCanvas.InkPresenter.StrokeContainer.Clear();
        }
    }
}