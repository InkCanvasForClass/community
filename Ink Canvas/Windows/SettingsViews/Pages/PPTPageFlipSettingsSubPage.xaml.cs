using Ink_Canvas.Helpers;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Windows;
using System.Windows.Controls;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class PPTPageFlipSettingsSubPage : Page
    {
        private readonly int _selectedIndex;
        private bool _isLoaded = false;
        private DelayAction _sliderDelayAction = new DelayAction();

        public PPTPageFlipSettingsSubPage(int selectedIndex)
        {
            InitializeComponent();
            _selectedIndex = selectedIndex;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = false;
            LoadPositionSettings();
            _isLoaded = true;
            SliderTouchHelper.AddTouchSupportToAllSliders(this);
        }

        private void LoadPositionSettings()
        {
            var ppt = SettingsManager.Settings.PowerPointSettings;
            string posName = GetPositionName(_selectedIndex);
            CardPositionEnabled.Header = "启用" + posName + "按钮";

            // 1. Position enabled ToggleSwitch
            string displayOptionStr = ppt.PPTButtonsDisplayOption.ToString("D4");
            if (displayOptionStr.Length < 4) displayOptionStr = "2222";
            int displayIndex = MapComboIndexToDisplayOptionIndex(_selectedIndex);
            ToggleSwitchPositionEnabled.IsOn = displayOptionStr[displayIndex] == '2';

            // 2. Show Page Number ToggleSwitch
            ToggleSwitchShowPageNumber.IsOn = GetPositionShowPageNumber(_selectedIndex, ppt);

            // 3. Black Background ToggleSwitch
            ToggleSwitchBlackBackground.IsOn = GetPositionBlackBackground(_selectedIndex, ppt);

            // 4. Offset Slider (Adjust range dynamically: Side = -500 to 500, Bottom = -100 to 500)
            if (_selectedIndex == 0 || _selectedIndex == 1) // Side (左侧 / 右侧)
            {
                SliderOffset.Minimum = -500;
            }
            else // Bottom (左下 / 右下)
            {
                SliderOffset.Minimum = -100;
            }
            SliderOffset.Value = GetPositionOffset(_selectedIndex, ppt);
            UpdateSliderText(SliderOffset, TextBlockOffsetValue, "{0:F0}");

            // 5. Opacity Slider
            SliderOpacity.Value = GetPositionOpacity(_selectedIndex, ppt);
            UpdateSliderText(SliderOpacity, TextBlockOpacityValue, "{0:P0}");
        }

        private string GetPositionName(int index)
        {
            switch (index)
            {
                case 0: return "左侧";
                case 1: return "右侧";
                case 2: return "左下";
                case 3: return "右下";
                default: return "自定义";
            }
        }

        private int MapComboIndexToDisplayOptionIndex(int comboIndex)
        {
            switch (comboIndex)
            {
                case 0: return 2; // 左侧 (Left Side) -> display option index 2
                case 1: return 3; // 右侧 (Right Side) -> display option index 3
                case 2: return 0; // 左下 (Left Bottom) -> display option index 0
                case 3: return 1; // 右下 (Right Bottom) -> display option index 1
                default: return 0;
            }
        }

        private bool GetPositionShowPageNumber(int index, PowerPointSettings ppt)
        {
            switch (index)
            {
                case 0: return ppt.PPTLSShowPageNumber;
                case 1: return ppt.PPTRSShowPageNumber;
                case 2: return ppt.PPTLBShowPageNumber;
                case 3: return ppt.PPTRBShowPageNumber;
                default: return true;
            }
        }

        private void SetPositionShowPageNumber(int index, PowerPointSettings ppt, bool val)
        {
            switch (index)
            {
                case 0: ppt.PPTLSShowPageNumber = val; break;
                case 1: ppt.PPTRSShowPageNumber = val; break;
                case 2: ppt.PPTLBShowPageNumber = val; break;
                case 3: ppt.PPTRBShowPageNumber = val; break;
            }
        }

        private bool GetPositionBlackBackground(int index, PowerPointSettings ppt)
        {
            switch (index)
            {
                case 0: return ppt.PPTLSBlackBackground;
                case 1: return ppt.PPTRSBlackBackground;
                case 2: return ppt.PPTLBBlackBackground;
                case 3: return ppt.PPTRBBlackBackground;
                default: return false;
            }
        }

        private void SetPositionBlackBackground(int index, PowerPointSettings ppt, bool val)
        {
            switch (index)
            {
                case 0: ppt.PPTLSBlackBackground = val; break;
                case 1: ppt.PPTRSBlackBackground = val; break;
                case 2: ppt.PPTLBBlackBackground = val; break;
                case 3: ppt.PPTRBBlackBackground = val; break;
            }
        }

        private int GetPositionOffset(int index, PowerPointSettings ppt)
        {
            switch (index)
            {
                case 0: return ppt.PPTLSButtonPosition;
                case 1: return ppt.PPTRSButtonPosition;
                case 2: return ppt.PPTLBButtonPosition;
                case 3: return ppt.PPTRBButtonPosition;
                default: return 0;
            }
        }

        private void SetPositionOffset(int index, PowerPointSettings ppt, int val)
        {
            switch (index)
            {
                case 0: ppt.PPTLSButtonPosition = val; break;
                case 1: ppt.PPTRSButtonPosition = val; break;
                case 2: ppt.PPTLBButtonPosition = val; break;
                case 3: ppt.PPTRBButtonPosition = val; break;
            }
        }

        private double GetPositionOpacity(int index, PowerPointSettings ppt)
        {
            switch (index)
            {
                case 0: return ppt.PPTLSButtonOpacity;
                case 1: return ppt.PPTRSButtonOpacity;
                case 2: return ppt.PPTLBButtonOpacity;
                case 3: return ppt.PPTRBButtonOpacity;
                default: return 0.5;
            }
        }

        private void SetPositionOpacity(int index, PowerPointSettings ppt, double val)
        {
            switch (index)
            {
                case 0: ppt.PPTLSButtonOpacity = val; break;
                case 1: ppt.PPTRSButtonOpacity = val; break;
                case 2: ppt.PPTLBButtonOpacity = val; break;
                case 3: ppt.PPTRBButtonOpacity = val; break;
            }
        }

        private void UpdateSliderText(Slider slider, TextBlock textBlock, string format)
        {
            if (slider == null || textBlock == null) return;
            textBlock.Text = string.Format(format, slider.Value);
        }

        private void ToggleSwitchPositionEnabled_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            
            var ppt = SettingsManager.Settings.PowerPointSettings;
            int displayIndex = MapComboIndexToDisplayOptionIndex(_selectedIndex);
            
            string str = ppt.PPTButtonsDisplayOption.ToString("D4");
            char[] c = str.ToCharArray();
            c[displayIndex] = ToggleSwitchPositionEnabled.IsOn ? '2' : '1';
            
            ppt.PPTButtonsDisplayOption = int.Parse(new string(c));
            SettingsManager.SaveSettingsToFile();
            
            // Notify other managers and preview window
            SettingsActionHub.OnPPTButtonsDisplayOptionChanged();
            PPTPageFlipPreviewWindow.ActiveInstance?.UpdatePreview();
        }

        private void ToggleSwitchShowPageNumber_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            var ppt = SettingsManager.Settings.PowerPointSettings;
            bool isOn = ToggleSwitchShowPageNumber.IsOn;
            
            SetPositionShowPageNumber(_selectedIndex, ppt, isOn);
            SettingsManager.SaveSettingsToFile();
            
            // Trigger UI and preview refresh
            SettingsActionHub.OnPPTButtonPositionChanged();
            PPTPageFlipPreviewWindow.ActiveInstance?.UpdatePreview();
        }

        private void ToggleSwitchBlackBackground_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            var ppt = SettingsManager.Settings.PowerPointSettings;
            bool isOn = ToggleSwitchBlackBackground.IsOn;
            
            SetPositionBlackBackground(_selectedIndex, ppt, isOn);
            SettingsManager.SaveSettingsToFile();
            
            // Trigger UI and preview refresh
            SettingsActionHub.OnPPTButtonPositionChanged();
            PPTPageFlipPreviewWindow.ActiveInstance?.UpdatePreview();
        }

        private void SliderOffset_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateSliderText(SliderOffset, TextBlockOffsetValue, "{0:F0}");
            if (!_isLoaded) return;

            var ppt = SettingsManager.Settings.PowerPointSettings;
            int offsetVal = (int)SliderOffset.Value;
            
            SetPositionOffset(_selectedIndex, ppt, offsetVal);
            SettingsActionHub.OnPPTButtonPositionChanged();
            PPTPageFlipPreviewWindow.ActiveInstance?.UpdatePreview();
            
            _sliderDelayAction.DebounceAction(2000, null, () => SettingsManager.SaveSettingsToFile());
        }

        private void ButtonResetOffset_Click(object sender, RoutedEventArgs e)
        {
            SliderOffset.Value = 0;
        }

        private void SliderOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateSliderText(SliderOpacity, TextBlockOpacityValue, "{0:P0}");
            if (!_isLoaded) return;

            var ppt = SettingsManager.Settings.PowerPointSettings;
            double roundedValue = Math.Round(SliderOpacity.Value, 1);
            
            SliderOpacity.ValueChanged -= SliderOpacity_ValueChanged;
            SliderOpacity.Value = roundedValue;
            SliderOpacity.ValueChanged += SliderOpacity_ValueChanged;
            
            SetPositionOpacity(_selectedIndex, ppt, roundedValue);
            
            string buttonKey = "";
            switch (_selectedIndex)
            {
                case 0: buttonKey = "LS"; break;
                case 1: buttonKey = "RS"; break;
                case 2: buttonKey = "LB"; break;
                case 3: buttonKey = "RB"; break;
            }
            
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnPPTButtonOpacityChanged(buttonKey, roundedValue);
            PPTPageFlipPreviewWindow.ActiveInstance?.UpdatePreview();
        }

        private void ButtonResetOpacity_Click(object sender, RoutedEventArgs e)
        {
            SliderOpacity.Value = 0.5;
        }
    }
}
