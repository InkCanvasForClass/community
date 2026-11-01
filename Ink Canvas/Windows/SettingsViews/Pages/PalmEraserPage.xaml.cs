using Ink_Canvas.Ink;
using Ink_Canvas.Properties;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using System.Windows;
using System.Windows.Controls;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    /// <summary>
    /// 手掌擦的唯一入口：启用开关 + 校准状态/向导放在同一页。
    /// 当前版本必须先校准；未校准或环境不匹配时运行时直接拒绝手掌擦。
    /// </summary>
    public partial class PalmEraserPage : iNKORE.UI.WPF.Modern.Controls.Page
    {
        private bool _isLoaded;

        public PalmEraserPage()
        {
            InitializeComponent();
            Loaded += PalmEraserPage_Loaded;
            IsVisibleChanged += (_, e) =>
            {
                if (e.NewValue is true && _isLoaded) UpdateCalibrationStatus();
            };
        }

        private void PalmEraserPage_Loaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = false;
            try
            {
                ToggleSwitchEnablePalmEraser.IsOn = SettingsManager.Settings.Canvas.EnablePalmEraser;
                SliderPalmSize.Value = System.Math.Clamp(
                    SettingsManager.Settings.Advanced.PalmSizeMultiplier, 0.5, 2.0);
                UpdatePalmSizeText();
                UpdateCalibrationStatus();
            }
            finally
            {
                _isLoaded = true;
            }
        }

        private void ToggleSwitchEnablePalmEraser_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            (Application.Current.MainWindow as MainWindow)?.ReloadPalmInputSource();
            UpdateCalibrationStatus();
        }

        private void SliderPalmSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            double value = System.Math.Round(System.Math.Clamp(e.NewValue, 0.5, 2.0), 2);
            UpdatePalmSizeText();
            if (!_isLoaded) return;
            SettingsManager.Settings.Advanced.PalmSizeMultiplier = value;
            SettingsManager.SaveSettingsToFile();
        }

        private void UpdatePalmSizeText()
        {
            if (TextPalmSize != null)
                TextPalmSize.Text = System.Math.Round(SliderPalmSize.Value, 2).ToString("0.00") + "×";
        }

        private void BtnStartCalibrate_Click(object sender, RoutedEventArgs e)
        {
            (Application.Current.MainWindow as MainWindow)?.ResetTouchCalibrationInput();
            if (Window.GetWindow(this) is SettingsWindow settingsWindow)
                settingsWindow.NavigateToPage("TouchCalibrationPage");
        }

        private void ClearCalibration_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show(AdvancedStrings.GetString("CalibrationClearConfirm"),
                    AdvancedStrings.CalibrationTitle, MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            if (!SettingsManager.TrySaveCalibration(null))
                TextCalibrateStatus.Text = AdvancedStrings.GetString("CalibrationSaveFailed");
            else
                UpdateCalibrationStatus();
        }

        private void UpdateCalibrationStatus()
        {
            var record = SettingsManager.Settings.Advanced.TouchCalibration;
            bool valid = record?.IsValid() == true;
            bool matched = false;
            if (valid)
            {
                Window window = Application.Current.MainWindow ?? Window.GetWindow(this);
                matched = window != null
                    && record.Environment.Matches(TouchCalibrationDisplay.Capture(window, record.Environment.Source));
            }

            bool hidFallback = matched && SettingsManager.Settings.Advanced.PalmEraserChannel == 1
                && record?.HasHidAreas != true;
            string key = !valid ? "PalmEraser_StatusNone"
                : matched ? (hidFallback ? "PalmEraser_StatusHidFallback" : "PalmEraser_StatusReady")
                : "PalmEraser_StatusMismatch";
            TextCalibrateStatus.Text = AdvancedStrings.GetString(key);
            // The warning is only relevant while the feature is switched on.
            BarCalibrationRequired.IsOpen = ToggleSwitchEnablePalmEraser.IsOn && !(valid && matched);
            BtnClearCalibration.IsEnabled = valid;
        }
    }
}
