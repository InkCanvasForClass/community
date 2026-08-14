using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Ink_Canvas.Controls
{
    /// <summary>
    /// 视频展台弹窗菜单内容：使用 PopupShellContent 作为外壳，承载摄像头设备选择、
    /// 分辨率选择以及矫正/拍照/旋转/关闭 4 个操作按钮。
    /// </summary>
    /// <remarks>
    /// 该控件仅作为 UI 容器，所有事件由 MainWindow 通过订阅内部控件事件处理。
    /// </remarks>
    public partial class BoothPopupContent : UserControl
    {
        public ComboBox CameraDevicesComboBoxControl => CameraDevicesComboBoxCtrl;
        public ComboBox BoothResolutionComboBoxControl => BoothResolutionComboBoxCtrl;
        public ComboBox PhotoCorrectionAccelerationComboBox => PhotoCorrectionAccelerationComboBoxCtrl;
        public Button CapturePhotoButton => CapturePhotoButtonCtrl;
        public Button RotateImageButton => RotateImageButtonCtrl;
        public Button ExitVideoPresenterButton => ExitVideoPresenterButtonCtrl;
        public ToggleButton PhotoCorrectionToggle => PhotoCorrectionToggleCtrl;
        public Slider BrightnessSlider => BrightnessSliderCtrl;
        public TextBlock BrightnessValueText => BrightnessValueTextCtrl;
        public Expander ProModeExpander => ProModeExpanderCtrl;
        public Slider ContrastSlider => ContrastSliderCtrl;
        public TextBlock ContrastValueText => ContrastValueTextCtrl;
        public Slider SaturationSlider => SaturationSliderCtrl;
        public TextBlock SaturationValueText => SaturationValueTextCtrl;
        public Slider WhiteBalanceSlider => WhiteBalanceSliderCtrl;
        public TextBlock WhiteBalanceValueText => WhiteBalanceValueTextCtrl;
        public Slider GainSlider => GainSliderCtrl;
        public TextBlock GainValueText => GainValueTextCtrl;
        public Slider FocusSlider => FocusSliderCtrl;
        public TextBlock FocusValueText => FocusValueTextCtrl;
        public Slider ExposureSlider => ExposureSliderCtrl;
        public TextBlock ExposureValueText => ExposureValueTextCtrl;

        public Button CloseButtonControl => Shell?.CloseButtonControl;

        public BoothPopupContent()
        {
            InitializeComponent();
            Shell.InnerContent = InnerContentHost.Content;
        }
    }
}
