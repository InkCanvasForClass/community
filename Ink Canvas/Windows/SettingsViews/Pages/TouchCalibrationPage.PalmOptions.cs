using Ink_Canvas.Ink;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class TouchCalibrationPage
    {
        private bool _loadingPalmOptions = true;
        private bool _palmOptionsValid = true;
        private readonly List<Action<PalmEraserOptions>> _loadOptions = new();
        private readonly List<Func<PalmEraserOptions, PalmEraserOptions>> _readOptions = new();

        private void BuildPalmOptions()
        {
            AddChoice(SourceOptionsPanel, "PalmSource", new[] { "PalmAuto", "PalmRaw", "PalmSoftware" },
                p => (int)p.Source, (p, n) => p with { Source = (PalmSourceMode)n });
            AddText(SourceOptionsPanel, "PalmDevice", p => p.HidDevice, (p, text) => p with { HidDevice = text.Trim() });
            AddChoice(SourceOptionsPanel, "PalmHidScale", new[] { "PalmStretch", "PalmIsotropic" },
                p => (int)p.HidScale, (p, n) => p with { HidScale = (PalmHidScale)n });
            AddToggle("PalmFollow", p => p.FollowSize, (p, v) => p with { FollowSize = v });
            AddToggle("PalmLock", p => p.LockSize, (p, v) => p with { LockSize = v });
            AddToggle("PalmSmooth", p => p.Smooth, (p, v) => p with { Smooth = v });
            AddChoice(PalmOptionsPanel, "PalmLimit", new[] { "PalmUnlimited", "PalmFloor", "PalmCap" },
                p => (int)p.Limit, (p, n) => p with { Limit = (PalmSizeLimit)n });
            AddToggle("PalmPressure", p => p.PressureEnabled, (p, v) => p with { PressureEnabled = v });
            AddSlider("PalmPressureThreshold", 0, 1, p => p.PressureThreshold, (p, v) => p with { PressureThreshold = v });
            AddSlider("PalmPressureMaximum", 1, 3, p => p.PressureMaximum, (p, v) => p with { PressureMaximum = v });
            AddToggle("PalmArea", p => p.AreaEnabled, (p, v) => p with { AreaEnabled = v });
            AddText(PalmOptionsPanel, "PalmAreaThreshold", p => p.AreaThresholdPx2?.ToString(CultureInfo.CurrentCulture) ?? "",
                (p, text) => p with { AreaThresholdPx2 = string.IsNullOrWhiteSpace(text) ? null : Parse(text, true) });
            AddChoice(PalmOptionsPanel, "PalmAspect", new[] { "PalmContactAspect", "PalmTraceAspect", "PalmCustomAspect" },
                p => (int)p.Aspect, (p, n) => p with { Aspect = (PalmAspectSource)n });
            AddText(PalmOptionsPanel, "PalmCustomRatio", p => p.CustomAspect.ToString(CultureInfo.CurrentCulture),
                (p, text) => p with { CustomAspect = Parse(text, false) });
        }

        private void LoadPalmOptions()
        {
            _loadingPalmOptions = true;
            try
            {
                _options = (SettingsManager.Settings.Advanced.PalmEraser ?? new()).Sanitize();
                foreach (var load in _loadOptions) load(_options);
                GroupCount.SelectedIndex = Math.Clamp(_options.MinimumContacts - 2, 0, 3);
                GroupWindow.Value = _options.GroupWindowMs;
                GroupTolerance.Value = _options.GroupTolerance;
                SimulateErase.IsChecked = _options.MultiTouch;
                _rawDevice = "";
                _palmOptionsValid = true;
            }
            finally { _loadingPalmOptions = false; }
        }

        private void PalmOptionChanged()
        {
            if (_loadingPalmOptions || Surface == null) return;
            var previous = _options;
            try
            {
                var result = _options;
                foreach (var read in _readOptions) result = read(result);
                _options = result.Sanitize();
                _palmOptionsValid = true;
                CancelPress();
                if (previous.Source != _options.Source || previous.HidDevice != _options.HidDevice)
                {
                    _rawDevice = "";
                    _environment = null;
                    _sourceChooser.Clear();
                    _draft = null;
                    _palm.Reset(); _finger.Reset(); _nib.Reset();
                    _step = 0;
                    ShowStep();
                }
                UpdatePreviewPolicy();
            }
            catch (FormatException)
            {
                _palmOptionsValid = false;
                Status.Text = Text("PalmInvalidNumber");
            }
        }

        private static double Parse(string text, bool zeroAllowed)
        {
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out double value)
                || !double.IsFinite(value) || value < 0 || (!zeroAllowed && value == 0)) throw new FormatException();
            return value;
        }

        private void AddChoice(StackPanel panel, string label, string[] choices,
            Func<PalmEraserOptions, int> get, Func<PalmEraserOptions, int, PalmEraserOptions> set)
        {
            panel.Children.Add(new TextBlock { Text = Text(label), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 4) });
            var control = new ComboBox();
            foreach (string choice in choices) control.Items.Add(Text(choice));
            panel.Children.Add(control);
            _loadOptions.Add(p => control.SelectedIndex = get(p));
            _readOptions.Add(p => set(p, Math.Max(0, control.SelectedIndex)));
            control.SelectionChanged += (_, _) => PalmOptionChanged();
        }

        private void AddToggle(string label, Func<PalmEraserOptions, bool> get, Func<PalmEraserOptions, bool, PalmEraserOptions> set)
        {
            var control = new CheckBox { Content = Text(label), Margin = new Thickness(0, 8, 0, 0) };
            PalmOptionsPanel.Children.Add(control);
            _loadOptions.Add(p => control.IsChecked = get(p));
            _readOptions.Add(p => set(p, control.IsChecked == true));
            control.Checked += (_, _) => PalmOptionChanged();
            control.Unchecked += (_, _) => PalmOptionChanged();
        }

        private void AddSlider(string label, double min, double max, Func<PalmEraserOptions, double> get,
            Func<PalmEraserOptions, double, PalmEraserOptions> set)
        {
            var caption = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 4) };
            var control = new Slider { Minimum = min, Maximum = max, TickFrequency = 0.05, IsSnapToTickEnabled = true };
            PalmOptionsPanel.Children.Add(caption); PalmOptionsPanel.Children.Add(control);
            _loadOptions.Add(p => { control.Value = get(p); caption.Text = Text(label) + " " + control.Value.ToString("F2"); });
            _readOptions.Add(p => set(p, control.Value));
            control.ValueChanged += (_, _) => { caption.Text = Text(label) + " " + control.Value.ToString("F2"); PalmOptionChanged(); };
        }

        private void AddText(StackPanel panel, string label, Func<PalmEraserOptions, string> get,
            Func<PalmEraserOptions, string, PalmEraserOptions> set)
        {
            panel.Children.Add(new TextBlock { Text = Text(label), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 4) });
            var control = new TextBox(); panel.Children.Add(control);
            _loadOptions.Add(p => control.Text = get(p));
            _readOptions.Add(p => set(p, control.Text));
            control.TextChanged += (_, _) => PalmOptionChanged();
        }

        private void RawSample(RawPalmContact sample)
        {
            if (_host == null || !_host.IsActive || _step == 0 || _environment?.Source is "WPF" or "Pointer"
                || _options.Source == PalmSourceMode.Software) return;
            if (_options.HidDevice.Length > 0 && !string.Equals(_options.HidDevice, sample.Device, StringComparison.OrdinalIgnoreCase)) return;
            if (_rawDevice.Length == 0) _rawDevice = sample.Device;
            if (!string.Equals(_rawDevice, sample.Device, StringComparison.OrdinalIgnoreCase)) return;
            if (sample.Released) { End(sample.Id, false); return; }
            var environment = TouchCalibrationDisplay.Capture(_host, "RawHID");
            var size = PalmEraserEngine.HidDimensions(sample.Width, sample.Height, sample.WidthMaximum,
                sample.HeightMaximum, environment.PixelWidth, environment.PixelHeight, _options.HidScale);
            var desktop = System.Windows.Forms.SystemInformation.VirtualScreen;
            Point position = Surface.PointFromScreen(new Point(desktop.Left + sample.X * desktop.Width, desktop.Top + sample.Y * desktop.Height));
            if (!double.IsFinite(position.X) || !double.IsFinite(position.Y)) return;
            if (_step == 4) _previewEngine.Configure(PreviewProfile());
            _previewEngine.NotePressure(sample.Pressure);
            if (!_contacts.Contains(sample.Id)) Begin(sample.Id, "RawHID", position, size.Width / environment.DpiX, size.Height / environment.DpiY);
            else Move(sample.Id, "RawHID", position, size.Width / environment.DpiX, size.Height / environment.DpiY);
            if (_step != 4 && _environment?.Source == "RawHID") Status.Text += " · " + _rawDevice;
        }
    }
}
