using Ink_Canvas.Helpers;
using Ink_Canvas.Ink;
using Ink_Canvas.Properties;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    /// <summary>
    /// 输入来源设置：触摸屏书写、手掌擦、鼠标/笔书写分别选择来源。
    /// 触摸屏与手掌擦可以一个用 WPF、一个用 HID；HID 不可用时触摸书写自动回退 WPF。
    /// </summary>
    public partial class TouchInputPage : iNKORE.UI.WPF.Modern.Controls.Page
    {
        private static readonly string[] TouchSourceNames = { "WPF 触摸", "HID 原始触摸" };
        private static readonly string[] MouseSourceNames = { "WPF 鼠标/笔", "Raw HID / Pointer（鼠标+笔）" };
        private static readonly string[] WidthModeKeys =
        {
            "TouchInput_WidthModeFixed",
            "TouchInput_WidthModeContact",
            "TouchInput_WidthModeContactScaled",
            "TouchInput_WidthModeSimulated"
        };
        private bool _loaded;
        private bool _updating;

        public TouchInputPage()
        {
            InitializeComponent();
            for (int i = 0; i < TouchSourceNames.Length; i++)
            {
                CmbTouchWriting.Items.Add(new ComboBoxItem { Content = TouchSourceNames[i] });
                CmbPalmEraser.Items.Add(new ComboBoxItem { Content = TouchSourceNames[i] });
            }
            for (int i = 0; i < MouseSourceNames.Length; i++)
                CmbMouseWriting.Items.Add(new ComboBoxItem { Content = MouseSourceNames[i] });
            for (int i = 0; i < WidthModeKeys.Length; i++)
                CmbWidthMode.Items.Add(new ComboBoxItem { Content = AdvancedStrings.GetString(WidthModeKeys[i]) });
            Loaded += TouchInputPage_Loaded;
        }

        private static int TouchWritingChannel =>
            Math.Clamp(SettingsManager.Settings.Advanced.TouchWritingChannel, 0, 1);

        private static int PalmEraserChannel =>
            Math.Clamp(SettingsManager.Settings.Advanced.PalmEraserChannel, 0, 1);

        private static int MouseWritingChannel =>
            Math.Clamp(SettingsManager.Settings.Advanced.MouseWritingChannel, 0, 1);

        private static int WidthMode =>
            Math.Clamp(SettingsManager.Settings.Advanced.TouchWidthMode, 0, 3);

        private void TouchInputPage_Loaded(object sender, RoutedEventArgs e)
        {
            _loaded = false;
            _updating = true;
            try
            {
                var advanced = SettingsManager.Settings.Advanced;
                // 旧版只有单一通道；如果它是 HID，迁移为书写和手掌擦都用 HID。
                if (advanced.PalmInputChannel > 0
                    && advanced.TouchWritingChannel == 0 && advanced.PalmEraserChannel == 0)
                {
                    advanced.TouchWritingChannel = 1;
                    advanced.PalmEraserChannel = 1;
                }
                advanced.PalmInputChannel = 0;

                CmbTouchWriting.SelectedIndex = TouchWritingChannel;
                CmbPalmEraser.SelectedIndex = PalmEraserChannel;
                CmbMouseWriting.SelectedIndex = MouseWritingChannel;
                CmbWidthMode.SelectedIndex = WidthMode;
                SliderWidthScale.Value = Math.Clamp(advanced.TouchWidthScale, 0.25, 4.0);
                SliderPressureInfluence.Value = Math.Clamp(advanced.TouchPressureInfluence, 0.0, 1.0);
                ToggleSwitchTouchDebug.IsOn = advanced.ShowTouchDebugOverlay;
                UpdateWidthText();
            }
            finally
            {
                _updating = false;
                _loaded = true;
            }
            UpdateStatus();
        }

        private void CmbTouchWriting_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_loaded || _updating || CmbTouchWriting.SelectedIndex < 0) return;
            SettingsManager.Settings.Advanced.TouchWritingChannel = CmbTouchWriting.SelectedIndex;
            SaveAndReload();
        }

        private void CmbPalmEraser_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_loaded || _updating || CmbPalmEraser.SelectedIndex < 0) return;
            SettingsManager.Settings.Advanced.PalmEraserChannel = CmbPalmEraser.SelectedIndex;
            SaveAndReload();
        }

        private void CmbMouseWriting_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_loaded || _updating || CmbMouseWriting.SelectedIndex < 0) return;
            SettingsManager.Settings.Advanced.MouseWritingChannel = CmbMouseWriting.SelectedIndex;
            SaveAndReload();
        }

        private void CmbWidthMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_loaded || _updating || CmbWidthMode.SelectedIndex < 0) return;
            SettingsManager.Settings.Advanced.TouchWidthMode = CmbWidthMode.SelectedIndex;
            SettingsManager.SaveSettingsToFile();
        }

        private void SliderWidthScale_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_loaded || _updating) return;
            SettingsManager.Settings.Advanced.TouchWidthScale = Math.Round(e.NewValue, 2);
            SettingsManager.SaveSettingsToFile();
            UpdateWidthText();
        }

        private void SliderPressureInfluence_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_loaded || _updating) return;
            SettingsManager.Settings.Advanced.TouchPressureInfluence = Math.Round(e.NewValue, 2);
            SettingsManager.SaveSettingsToFile();
            UpdateWidthText();
        }

        private void ToggleSwitchTouchDebug_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_loaded || _updating) return;
            SettingsManager.Settings.Advanced.ShowTouchDebugOverlay = ToggleSwitchTouchDebug.IsOn;
            SettingsManager.SaveSettingsToFile();
            (Application.Current.MainWindow as MainWindow)?.UpdateTouchDebugOverlayVisibility();
        }

        private void UpdateWidthText()
        {
            if (TextWidthScale != null)
                TextWidthScale.Text = SettingsManager.Settings.Advanced.TouchWidthScale.ToString("0.00") + "×";
            if (TextPressureInfluence != null)
                TextPressureInfluence.Text = SettingsManager.Settings.Advanced.TouchPressureInfluence.ToString("P0");
        }

        private void SaveAndReload()
        {
            SettingsManager.SaveSettingsToFile();
            (Application.Current.MainWindow as MainWindow)?.ReloadPalmInputSource();
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            if (BarStatus == null) return;
            string format = AdvancedStrings.GetString("TouchInput_StatusFormat");
            string message = string.Format(format,
                TouchSourceNames[TouchWritingChannel],
                TouchSourceNames[PalmEraserChannel],
                MouseSourceNames[MouseWritingChannel]);
            bool hidFallback = (TouchWritingChannel == 1 || PalmEraserChannel == 1)
                && (Application.Current.MainWindow as MainWindow)?.IsHidTouchActive != true;
            if (hidFallback)
                message += " " + AdvancedStrings.GetString("TouchInput_HidFallback");
            BarStatus.Message = message;
            BarStatus.Severity = hidFallback
                ? iNKORE.UI.WPF.Modern.Controls.InfoBarSeverity.Warning
                : iNKORE.UI.WPF.Modern.Controls.InfoBarSeverity.Informational;
        }

    }
}
