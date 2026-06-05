using Ink_Canvas.Helpers;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class CanvasPage : iNKORE.UI.WPF.Modern.Controls.Page
    {
        private bool _isLoaded = false;

        public CanvasPage()
        {
            InitializeComponent();
            Loaded += CanvasPage_Loaded;
        }

        private void CanvasPage_Loaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = false;

            try
            {
                var settings = SettingsManager.Settings;

                if (settings.Canvas != null)
                {
                    CardEnablePressureTouchMode.IsOn = settings.Canvas.EnablePressureTouchMode;
                    CardDisablePressure.IsOn = settings.Canvas.DisablePressure;

                    int curveMode = 0;
                    if (settings.Canvas.UseAdvancedBezierSmoothing) curveMode = 2;
                    else if (settings.Canvas.FitToCurve) curveMode = 1;
                    ComboBoxCurveSmoothingMode.SelectedIndex = curveMode;

                    BrushAutoRestoreTimesTextBox.Text = settings.Canvas.BrushAutoRestoreTimes ?? string.Empty;
                    LoadBrushAutoRestoreColor(settings.Canvas.BrushAutoRestoreColor);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载画板设置时出错: {ex.Message}");
            }

            _isLoaded = true;
            SliderTouchHelper.AddTouchSupportToAllSliders(this);
        }

        private void LoadBrushAutoRestoreColor(string hex)
        {
            try
            {
                foreach (var item in ComboBoxBrushAutoRestoreColor.Items)
                {
                    if (item is ComboBoxItem cbi && cbi.Tag != null &&
                        string.Equals(cbi.Tag.ToString(), hex, StringComparison.OrdinalIgnoreCase))
                    {
                        ComboBoxBrushAutoRestoreColor.SelectedItem = cbi;
                        return;
                    }
                }
                ComboBoxBrushAutoRestoreColor.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载画笔恢复颜色时出错: {ex.Message}");
            }
        }

        private void ToggleSwitchEnablePressureTouchMode_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Canvas.EnablePressureTouchMode = CardEnablePressureTouchMode.IsOn;
            SettingsActionHub.OnEnablePressureTouchModeChanged(CardEnablePressureTouchMode.IsOn);
            if (!CardEnablePressureTouchMode.IsOn || !SettingsManager.Settings.Canvas.DisablePressure)
                CardDisablePressure.IsOn = SettingsManager.Settings.Canvas.DisablePressure;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchDisablePressure_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Canvas.DisablePressure = CardDisablePressure.IsOn;
            SettingsActionHub.OnDisablePressureChanged(CardDisablePressure.IsOn);
            if (!CardDisablePressure.IsOn || !SettingsManager.Settings.Canvas.EnablePressureTouchMode)
                CardEnablePressureTouchMode.IsOn = SettingsManager.Settings.Canvas.EnablePressureTouchMode;
            SettingsManager.SaveSettingsToFile();
        }

        private void ComboBoxCurveSmoothingMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            var item = ComboBoxCurveSmoothingMode?.SelectedItem as ComboBoxItem;
            if (item == null) return;
            var tag = item.Tag?.ToString() ?? "0";
            switch (tag)
            {
                case "1":
                    SettingsManager.Settings.Canvas.FitToCurve = true;
                    SettingsManager.Settings.Canvas.UseAdvancedBezierSmoothing = false;
                    break;
                case "2":
                    SettingsManager.Settings.Canvas.FitToCurve = false;
                    SettingsManager.Settings.Canvas.UseAdvancedBezierSmoothing = true;
                    break;
                default:
                    SettingsManager.Settings.Canvas.FitToCurve = false;
                    SettingsManager.Settings.Canvas.UseAdvancedBezierSmoothing = false;
                    break;
            }
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnCurveSmoothingModeChanged(
                SettingsManager.Settings.Canvas.FitToCurve,
                SettingsManager.Settings.Canvas.UseAdvancedBezierSmoothing);
        }

        private void BrushAutoRestoreTimesTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Canvas.BrushAutoRestoreTimes = BrushAutoRestoreTimesTextBox.Text ?? string.Empty;
            SettingsManager.SaveSettingsToFile();
        }

        private void ComboBoxBrushAutoRestoreColor_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            if (ComboBoxBrushAutoRestoreColor.SelectedItem is ComboBoxItem item)
            {
                string hex = item.Tag as string ?? string.Empty;
                SettingsManager.Settings.Canvas.BrushAutoRestoreColor = hex;
                SettingsManager.SaveSettingsToFile();
            }
        }
    }
}
