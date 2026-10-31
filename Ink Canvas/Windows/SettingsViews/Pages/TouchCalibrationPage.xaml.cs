using Ink_Canvas.Ink;
#if ICC_NATIVE_WET_INK
using Ink_Canvas.Ink.Native;
#endif
using Ink_Canvas.Properties;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Linq;
using System.Windows.Threading;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class TouchCalibrationPage : iNKORE.UI.WPF.Modern.Controls.Page
    {
        private static readonly DependencyProperty PreviewInkBrushProperty = DependencyProperty.Register(
            "PreviewInkBrush", typeof(Brush), typeof(TouchCalibrationPage),
            new PropertyMetadata(null, OnPreviewInkBrushChanged));

        private static void OnPreviewInkBrushChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var window = (TouchCalibrationPage)sender;
            if (window.Surface == null || e.NewValue is not SolidColorBrush brush) return;
            window.Surface.DefaultDrawingAttributes.Color = brush.Color;
            foreach (var stroke in window.Surface.Strokes)
                stroke.DrawingAttributes.Color = brush.Color;
        }

        private readonly TouchCalibrationSampler _palm = new(TouchCalibrationMeasure.Area);
        private readonly TouchCalibrationSampler _finger = new(TouchCalibrationMeasure.Area);
        private readonly TouchCalibrationSampler _nib = new(TouchCalibrationMeasure.Area);
        private readonly HashSet<long> _contacts = new();
        private readonly PalmEraserContactTracker _previewTracker = new();
#if ICC_NATIVE_WET_INK
        private NativePointerInputSource _native;
#endif
        private TouchCalibrationEnvironment _environment;
        private TouchCalibrationRecord _draft;
        private int _step;
        private long? _pressId;
        private long _lastNativeTimestamp = -1;
        private bool _invalidPress;
        private bool _endingTouch;
        private StylusPointCollection _previewPoints;
        private Point? _lastErase;
        private Stroke _previewStroke;
        private TouchCalibrationSampler Sampler => _step == 1 ? _palm : _step == 2 ? _finger : _nib;

        private Window _host;
        private bool _initialized;
        private bool _pointOnly;
        private readonly PalmContactGroupTracker _groupTracker = new();
        private readonly Dictionary<long, Stroke> _groupStrokes = new();
        private readonly DispatcherTimer _groupTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
        private long? _groupAnchor;
        private bool UsesPointGrouping => _step == 4 && (_pointOnly || SimulateErase.IsChecked == true);
        private double _simulationWidth;
        private double _simulationHeight;
        private iNKORE.UI.WPF.Modern.Controls.NavigationViewPaneDisplayMode _previousPaneMode;

        public TouchCalibrationPage()
        {
            InitializeComponent();
            _initialized = true;
            SetResourceReference(PreviewInkBrushProperty, iNKORE.UI.WPF.Modern.ThemeKeys.TextFillColorPrimaryBrushKey);
            Loaded += PageLoaded;
            Unloaded += PageUnloaded;
            ReturnButton.Content = Text("CalibrationReturnSettings");
            SimulateButton.Content = Text("CalibrationSimulate");
            SimulationHint.Text = Text("CalibrationSimulateHint");
            SimulateErase.Content = Text("CalibrationSimulateErase");
            GroupCountLabel.Text = Text("CalibrationGroupCount");
            GroupWindowLabel.Text = Text("CalibrationGroupWindow");
            GroupToleranceLabel.Text = Text("CalibrationGroupTolerance");
            _groupTimer.Tick += (_, _) => EvaluatePointGroup();
        }

        private void PageLoaded(object sender, RoutedEventArgs e)
        {
            if (_host != null) return;
            _host = Window.GetWindow(this);
            if (_host == null) return;
            _host.LocationChanged += HostEnvironmentChanged;
            _host.DpiChanged += HostDpiChanged;
            _host.Deactivated += HostDeactivated;
            if (_host is SettingsWindow settings)
            {
                _previousPaneMode = settings.NavigationViewControl.PaneDisplayMode;
                settings.NavigationViewControl.PaneDisplayMode = iNKORE.UI.WPF.Modern.Controls.NavigationViewPaneDisplayMode.LeftMinimal;
            }
#if ICC_NATIVE_WET_INK
            var dpi = VisualTreeHelper.GetDpi(_host);
            _native = new NativePointerInputSource((HwndSource)PresentationSource.FromVisual(_host),
                NativeSample, dpi.DpiScaleX, dpi.DpiScaleY, registerRawMouse: false);
#endif
            _step = 0;
            _pointOnly = false;
            _environment = null;
            _draft = null;
            _palm.Reset(); _finger.Reset(); _nib.Reset();
            OutlineWidth.Clear(); OutlineHeight.Clear();
            SimulateErase.IsChecked = false;
            SizeAdjustment.Value = 1;
            ShowStep();
        }

        private void PageUnloaded(object sender, RoutedEventArgs e)
        {
            CancelPress();
#if ICC_NATIVE_WET_INK
            _native?.Dispose();
            _native = null;
#endif
            if (_host == null) return;
            _host.LocationChanged -= HostEnvironmentChanged;
            _host.DpiChanged -= HostDpiChanged;
            _host.Deactivated -= HostDeactivated;
            if (_host is SettingsWindow settings)
                settings.NavigationViewControl.PaneDisplayMode = _previousPaneMode;
            _host = null;
        }

        private void HostEnvironmentChanged(object sender, EventArgs e) => CheckEnvironment();
        private void HostDpiChanged(object sender, DpiChangedEventArgs e) => CheckEnvironment();
        private void HostDeactivated(object sender, EventArgs e) => CancelPress();

        private void ReturnToSettings(object sender, RoutedEventArgs e)
        {
            CancelPress();
            if (Window.GetWindow(this) is SettingsWindow settings) settings.NavigateToPage("AdvancedPage");
        }

        private void ContentSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_initialized) return;
            bool narrow = e.NewSize.Width < 700;
            OptionsColumn.Width = narrow ? new GridLength(0) : new GridLength(300);
            OptionsRow.Height = narrow ? new GridLength(Math.Min(220, e.NewSize.Height * 0.45)) : new GridLength(0);
            Grid.SetColumn(OptionsPanel, narrow ? 0 : 1);
            Grid.SetRow(OptionsPanel, narrow ? 1 : 0);
            OptionsPanel.Margin = narrow ? new Thickness(0, 12, 0, 0) : new Thickness(20, 0, 0, 0);
        }

        private string Text(string key) => AdvancedStrings.GetString(key);
        private void ShowStep()
        {
            CancelPress();
            Surface.Strokes.Clear();
            Surface.EditingMode = _step == 0 || _step == 4 ? InkCanvasEditingMode.Ink : InkCanvasEditingMode.None;
            StepTitle.Text = string.Format(Text("CalibrationStep"), _step + 1, 5)
                + "  " + Text(new[] { "CalibrationOutline", "CalibrationPalm", "CalibrationFinger", "CalibrationNib", "CalibrationPreview" }[_step]);
            Hint.Text = Text(new[] { "CalibrationOutlineHint", "CalibrationPressHint", "CalibrationPressHint", "CalibrationNibHint", "CalibrationPreviewHint" }[_step]);
            OutlinePanel.Visibility = _step == 0 ? Visibility.Visible : Visibility.Collapsed;
            PreviewPanel.Visibility = _step == 4 ? Visibility.Visible : Visibility.Collapsed;
            SkipButton.Visibility = _step == 3 ? Visibility.Visible : Visibility.Collapsed;
            BackButton.IsEnabled = _step > 0;
            if (_pointOnly)
            {
                StepTitle.Text = Text("CalibrationSimulate");
                Hint.Text = Text("CalibrationSimulateHint");
            }
            NextButton.Content = Text(_step == 0 ? "CalibrationAreaRoute" : "CalibrationNext");
            NextButton.Visibility = _step == 4 ? Visibility.Collapsed : Visibility.Visible;
            SaveButton.Visibility = _step == 4 ? Visibility.Visible : Visibility.Collapsed;
            SimulateErase.IsEnabled = !_pointOnly;
            Status.Text = Text("CalibrationReady");
            UpdatePreviewPolicy();
            UpdateSimulationMode();
        }

        private bool CheckEnvironment()
        {
            if (!IsLoaded || _host == null) return true;
#if ICC_NATIVE_WET_INK
            var dpi = VisualTreeHelper.GetDpi(this);
            _native?.UpdateDpi(dpi.DpiScaleX, dpi.DpiScaleY);
#endif
            if (_environment == null) return true;
            if (_environment.Matches(TouchCalibrationDisplay.Capture(_host, _environment.Source))) return true;
            _environment = null;
            _draft = null;
            _palm.Reset(); _finger.Reset(); _nib.Reset();
            _step = 0;
            _pointOnly = false;
            SimulateErase.IsChecked = false;
            OutlineWidth.Clear();
            OutlineHeight.Clear();
            ShowStep();
            Status.Text = Text("CalibrationEnvironmentChanged");
            return false;
        }

        private void OutlineCollected(object sender, InkCanvasStrokeCollectedEventArgs e)
        {
            if (_step != 0) return;
            var bounds = Surface.Strokes.GetBounds();
            OutlineWidth.Text = bounds.Width.ToString("F1", CultureInfo.CurrentCulture);
            OutlineHeight.Text = bounds.Height.ToString("F1", CultureInfo.CurrentCulture);
        }

        private bool Inside(Point position) => position.X >= 0 && position.Y >= 0
            && position.X <= Surface.ActualWidth && position.Y <= Surface.ActualHeight;

        private bool AcceptSource(string source)
        {
            if (!CheckEnvironment()) return false;
            if (_environment == null) _environment = TouchCalibrationDisplay.Capture(_host, source);
            return _environment.Source == source;
        }

        private void Begin(long id, string source, Point position, double width, double height)
        {
            if (_step == 0 || !Inside(position) || !AcceptSource(source)) return;
            if (!_contacts.Add(id)) return;
            if (UsesPointGrouping) { BeginPointGroup(id, position); return; }
            if (_contacts.Count > 1)
            {
                _invalidPress = true;
                Sampler.CancelPress();
                Status.Text = Text("CalibrationMultipleContacts");
                return;
            }
            _pressId = id;
            _invalidPress = false;
            _lastNativeTimestamp = -1;
            _previewTracker.Reset();
            _lastErase = null;
            _previewPoints = new StylusPointCollection();
            _previewStroke = null;
            if (_step != 4) Sampler.BeginPress(true, 1, source);
            Move(id, source, position, width, height);
        }

        private void Move(long id, string source, Point position, double width, double height)
        {
            if (UsesPointGrouping)
            {
                if (_contacts.Contains(id) && _environment?.Source == source && CheckEnvironment())
                    MovePointGroup(id, position);
                return;
            }
            if (_pressId != id || _invalidPress || _environment?.Source != source || !CheckEnvironment()) return;
            if (!Inside(position))
            {
                _invalidPress = true;
                Sampler.CancelPress();
                return;
            }
            bool valid = double.IsFinite(width) && width >= 0.5 && double.IsFinite(height) && height >= 0.5;
            if (valid)
            {
                ContactBox.Visibility = Visibility.Visible;
                ContactBox.Width = Math.Min(width, 4096); ContactBox.Height = Math.Min(height, 4096);
                System.Windows.Controls.Canvas.SetLeft(ContactBox, position.X - width / 2);
                System.Windows.Controls.Canvas.SetTop(ContactBox, position.Y - height / 2);
            }
            if (_step == 4)
            {
                Preview(position, width, height);
                return;
            }
            Sampler.AddSample(width, height, 0, _contacts.Count, source);
            Status.Text = valid ? string.Format(Text("CalibrationLive"), width, height, width * height,
                Sampler.CompletedPresses, source) : Text("CalibrationNoArea");
        }

        private void End(long id, bool canceled)
        {
            if (UsesPointGrouping) { EndPointGroup(id, canceled); return; }
            if (!_contacts.Remove(id)) return;
            if (_pressId == id)
            {
                if (_step != 4)
                {
                    if (canceled || _invalidPress) Sampler.CancelPress();
                    else if (!Sampler.EndPress()) Status.Text = Text("CalibrationNoArea");
                    else Status.Text = string.Format(Text("CalibrationRecorded"), Sampler.CompletedPresses, Sampler.MedianPeak());
                }
                _pressId = null;
                ContactBox.Visibility = Visibility.Collapsed;
                _previewStroke = null;
            }
        }

        private void CancelPress()
        {
            _groupTimer.Stop();
            foreach (var stroke in _groupStrokes.Values) Surface.Strokes.Remove(stroke);
            _groupStrokes.Clear();
            _groupTracker.Reset();
            _groupAnchor = null;
            _palm.CancelPress(); _finger.CancelPress(); _nib.CancelPress();
            _contacts.Clear(); _pressId = null;
            _invalidPress = false;
            _lastNativeTimestamp = -1;
            _previewTracker.Reset();
            _lastErase = null;
            if (_previewStroke != null) Surface.Strokes.Remove(_previewStroke);
            _previewStroke = null;
            _endingTouch = true;
            try { Surface.ReleaseAllTouchCaptures(); }
            finally { _endingTouch = false; }
            if (ContactBox != null) ContactBox.Visibility = Visibility.Collapsed;
        }

#if ICC_NATIVE_WET_INK
        private bool NativeSample(NativePointerInputBatch batch)
        {
            if (batch.InputKind != NativeInkInputKind.Touch) return false;
            if (_environment?.Source == "WPF") return false;
            long id = batch.PointerId;
            if (batch.MessageKind == NativePointerMessageKind.CaptureLost)
            {
                bool tracked = _contacts.Contains(id);
                End(id, true);
                return tracked;
            }
            if (batch.SamplesNewestFirst.Count == 0) return _contacts.Contains(id);
            if (!CheckEnvironment()) return true;
            var sample = batch.SamplesNewestFirst[0];
            var position = _host.TranslatePoint(new Point(sample.X, sample.Y), Surface);
            if (_step == 0) return Inside(position); // Never trace the palm itself.
            if (batch.MessageKind == NativePointerMessageKind.Down)
            {
                if (!Inside(position)) return false; // Keep buttons and other chrome interactive.
                var dpi = VisualTreeHelper.GetDpi(this);
                Begin(id, "Pointer", position, TouchCalibrationPolicy.PixelsToDip(sample.ContactWidthPixels, dpi.DpiScaleX),
                    TouchCalibrationPolicy.PixelsToDip(sample.ContactHeightPixels, dpi.DpiScaleY));
            }
            if (!_contacts.Contains(id)) return false;
            bool canceled = (sample.Flags & NativeInkSampleFlags.Canceled) != 0;
            if (canceled) { End(id, true); return true; }
            var scale = VisualTreeHelper.GetDpi(this);
            for (int i = batch.SamplesNewestFirst.Count - 1; i >= 0; i--)
            {
                var point = batch.SamplesNewestFirst[i];
                if (point.TimestampMicroseconds <= _lastNativeTimestamp) continue;
                _lastNativeTimestamp = point.TimestampMicroseconds;
                Move(id, "Pointer", TranslatePoint(new Point(point.X, point.Y), Surface),
                    TouchCalibrationPolicy.PixelsToDip(point.ContactWidthPixels, scale.DpiScaleX),
                    TouchCalibrationPolicy.PixelsToDip(point.ContactHeightPixels, scale.DpiScaleY));
            }
            if (batch.MessageKind == NativePointerMessageKind.Up) End(id, false);
            return true;
        }

#endif

        private void TouchDownSample(object sender, TouchEventArgs e)
        {
            e.Handled = true;
            if (_step == 0 || _environment?.Source == "Pointer") return;
            var point = e.GetTouchPoint(Surface);
            Begin(e.TouchDevice.Id, "WPF", point.Position, point.Bounds.Width, point.Bounds.Height);
            Surface.CaptureTouch(e.TouchDevice);
        }
        private void TouchMoveSample(object sender, TouchEventArgs e)
        {
            e.Handled = true;
            var point = e.GetTouchPoint(Surface);
            Move(e.TouchDevice.Id, "WPF", point.Position, point.Bounds.Width, point.Bounds.Height);
        }
        private void TouchUpSample(object sender, TouchEventArgs e)
        {
            e.Handled = true;
            End(e.TouchDevice.Id, false);
            _endingTouch = true;
            Surface.ReleaseTouchCapture(e.TouchDevice);
            _endingTouch = false;
        }
        private void TouchCaptureLost(object sender, TouchEventArgs e)
        {
            if (!_endingTouch) End(e.TouchDevice.Id, true);
        }

        private void Preview(Point position, double width, double height)
        {
            if (!TouchCalibrationPolicy.TryResolve(_draft, _environment,
                SettingsManager.Settings.Canvas.PalmEraserSensitivity, out var threshold, out var factor)) return;
            bool ellipse = SettingsManager.Settings.Canvas.EraserShapeType == 0;
            if (_previewTracker.Update(width, height, threshold, factor, ellipse, out var w, out var h))
            {
                if (_previewStroke != null) Surface.Strokes.Remove(_previewStroke);
                _previewStroke = null;
                StylusShape shape = ellipse ? new EllipseStylusShape(w, h) : new RectangleStylusShape(w, h);
                Surface.Strokes.Erase(new[] { _lastErase ?? position, position }, shape);
                _lastErase = position;
                ContactBox.Width = w; ContactBox.Height = h;
                System.Windows.Controls.Canvas.SetLeft(ContactBox, position.X - w / 2); System.Windows.Controls.Canvas.SetTop(ContactBox, position.Y - h / 2);
            }
            else
            {
                _previewPoints.Add(new StylusPoint(position.X, position.Y));
                if (_previewStroke == null)
                {
                    _previewStroke = new Stroke(_previewPoints, Surface.DefaultDrawingAttributes.Clone());
                    Surface.Strokes.Add(_previewStroke);
                }
            }
        }

        private void StartSimulation(object sender, RoutedEventArgs e)
        {
            if (!TryOutline(out _simulationWidth, out _simulationHeight))
            { Status.Text = Text("CalibrationInvalidOutline"); return; }
            _pointOnly = true;
            _environment = TouchCalibrationDisplay.Capture(_host, "WPF");
            _draft = new TouchCalibrationRecord
            {
                UsePointGrouping = true,
                OutlineWidth = _simulationWidth,
                OutlineHeight = _simulationHeight,
                Environment = _environment
            };
            _step = 4;
            ShowStep();
            SimulateErase.IsChecked = true;
        }

        private void SimulationModeChanged(object sender, RoutedEventArgs e)
        {
            if (!_initialized) return;
            CancelPress();
            UpdateSimulationMode();
        }

        private void UpdateSimulationMode()
        {
            if (_step == 4) Surface.EditingMode = InkCanvasEditingMode.Ink;
        }

        private void GroupOptionsChanged(object sender, RoutedEventArgs e)
        {
            if (!_initialized) return;
            CancelPress();
        }

        private void BeginPointGroup(long id, Point position)
        {
            _groupTracker.MinimumContacts = GroupCount.SelectedIndex + 2;
            _groupTracker.WindowMilliseconds = (int)GroupWindow.Value;
            _groupTracker.Tolerance = GroupTolerance.Value;
            _groupTracker.Width = _pointOnly ? _simulationWidth : _draft?.OutlineWidth ?? 0;
            _groupTracker.Height = _pointOnly ? _simulationHeight : _draft?.OutlineHeight ?? 0;
            _groupTracker.Down(id, position.X, position.Y, Environment.TickCount64);
            var points = new StylusPointCollection { new StylusPoint(position.X, position.Y) };
            var stroke = new Stroke(points, Surface.DefaultDrawingAttributes.Clone());
            _groupStrokes[id] = stroke;
            Surface.Strokes.Add(stroke);
            _groupTimer.Start();
            EvaluatePointGroup();
        }

        private void MovePointGroup(long id, Point position)
        {
            if (!Inside(position)) { EndPointGroup(id, true); return; }
            _groupTracker.Move(id, position.X, position.Y);
            if (!_groupTracker.IsPalm(id) && _groupStrokes.TryGetValue(id, out var stroke))
                stroke.StylusPoints.Add(new StylusPoint(position.X, position.Y));
            EvaluatePointGroup();
        }

        private void EvaluatePointGroup()
        {
            if (!UsesPointGrouping || !CheckEnvironment()) return;
            _groupTracker.Evaluate(Environment.TickCount64);
            foreach (long id in _groupTracker.PalmIds.ToArray())
                if (_groupStrokes.Remove(id, out var stroke)) Surface.Strokes.Remove(stroke);
            var ids = _groupTracker.PalmIds.ToArray();
            long? anchor = ids.Length == 0 ? null : ids.Min();
            if (anchor != _groupAnchor) _lastErase = null;
            _groupAnchor = anchor;
            if (_groupTracker.TryGetCenter(out var x, out var y)) SimulateAt(new Point(x, y));
            else { _lastErase = null; ContactBox.Visibility = Visibility.Collapsed; }
            Status.Text = string.Format(Text("CalibrationGroupStatus"), _groupTracker.Count,
                _groupTracker.MinimumContacts, anchor.HasValue ? Text("CalibrationGroupPalm") : Text("CalibrationGroupWriting"));
        }

        private void EndPointGroup(long id, bool canceled)
        {
            if (_groupStrokes.Remove(id, out var stroke) && canceled) Surface.Strokes.Remove(stroke);
            _groupTracker.Up(id);
            _contacts.Remove(id);
            _lastErase = null; // Lifting a contact changes the centroid; do not erase the jump.
            if (_groupTracker.Count == 0) _groupTimer.Stop();
            EvaluatePointGroup();
        }

        private void SimulateAt(Point point)
        {
            if (!Inside(point)) { _lastErase = null; ContactBox.Visibility = Visibility.Collapsed; return; }
            double width = _pointOnly ? _simulationWidth : _draft?.OutlineWidth ?? 0;
            double height = _pointOnly ? _simulationHeight : _draft?.OutlineHeight ?? 0;
            bool ellipse = SettingsManager.Settings.Canvas.EraserShapeType == 0;
            if (!PalmEraserContactTracker.TryGetEraserSize(width, height, 0.01,
                Math.Sqrt(SizeAdjustment.Value), ellipse, out var w, out var h)) return;
            StylusShape shape = ellipse ? new EllipseStylusShape(w, h) : new RectangleStylusShape(w, h);
            Surface.Strokes.Erase(new[] { _lastErase ?? point, point }, shape);
            _lastErase = point;
            ContactBox.Width = w; ContactBox.Height = h;
            System.Windows.Controls.Canvas.SetLeft(ContactBox, point.X - w / 2);
            System.Windows.Controls.Canvas.SetTop(ContactBox, point.Y - h / 2);
            ContactBox.Visibility = Visibility.Visible;
        }

        private void UpdatePreviewPolicy()
        {
            if (PreviewPolicy == null) return;
            if (_pointOnly) { PreviewPolicy.Text = Text("CalibrationSimulationOnly"); return; }
            if (_draft == null) return;
            TouchCalibrationPolicy.TryResolve(_draft, _environment, SettingsManager.Settings.Canvas.PalmEraserSensitivity,
                out var threshold, out var factor);
            PreviewPolicy.Text = string.Format(Text("CalibrationPolicy"), threshold * threshold, factor * factor,
                SettingsManager.Settings.Canvas.PalmEraserSensitivity);
        }
        private void SizeAdjustmentChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_initialized) return;
            CancelPress();
            if (_draft != null) _draft.SizeAdjustment = e.NewValue;
            UpdatePreviewPolicy();
        }
        private void Retry(object sender, RoutedEventArgs e)
        {
            CancelPress();
            if (_step >= 1 && _step <= 3) Sampler.Reset();
            if (_step == 0) { OutlineWidth.Clear(); OutlineHeight.Clear(); }
            ShowStep();
        }
        private void Back(object sender, RoutedEventArgs e)
        {
            if (_contacts.Count != 0) { Status.Text = Text("CalibrationLift"); return; }
            if (_pointOnly)
            {
                _pointOnly = false;
                _step = 0;
                SimulateErase.IsChecked = false;
                ShowStep();
                return;
            }
            if (_step > 0) { _step--; ShowStep(); }
        }
        private void Skip(object sender, RoutedEventArgs e)
        {
            if (_contacts.Count != 0) { Status.Text = Text("CalibrationLift"); return; }
            _nib.Reset();
            _step = 4; ShowStep();
        }
        private void Next(object sender, RoutedEventArgs e)
        {
            if (_step >= 4) return;
            if (_contacts.Count != 0) { Status.Text = Text("CalibrationLift"); return; }
            if (!CheckEnvironment()) return;
            if (_step == 0 && (!TryOutline(out _, out _)))
            { Status.Text = Text("CalibrationInvalidOutline"); return; }
            if (_step >= 1 && _step <= 3 && !Sampler.IsComplete)
            { Status.Text = Text("CalibrationNeedThree"); return; }
            if (_step == 2)
            {
                TryOutline(out var width, out var height);
                var error = TouchCalibrationCalculator.TryBuild(_finger, _palm, width, height, _environment, out _draft);
                if (error != TouchCalibrationBuildError.None)
                { Status.Text = Text("CalibrationOverlap"); return; }
                _draft.SizeAdjustment = SizeAdjustment.Value;
            }
            _step++; ShowStep();
        }

        private void SaveCalibration(object sender, RoutedEventArgs e)
        {
            if (_step != 4) return;
            if (_contacts.Count != 0) { Status.Text = Text("CalibrationLift"); return; }
            if (!CheckEnvironment()) return;
            if (_draft == null) { Status.Text = Text("CalibrationOverlap"); return; }
            _draft.UsePointGrouping = UsesPointGrouping;
            _draft.MinimumContacts = GroupCount.SelectedIndex + 2;
            _draft.WindowMilliseconds = (int)GroupWindow.Value;
            _draft.GroupTolerance = GroupTolerance.Value;
            _draft.SizeAdjustment = SizeAdjustment.Value;
            if (!_draft.IsValid()) { Status.Text = Text("CalibrationOverlap"); return; }
            int? nib = !_pointOnly && _nib.IsComplete
                ? (int)Math.Clamp(Math.Round(Math.Sqrt(_nib.MedianPeak()) * 1.5), 1, 50) : null;
            if (!SettingsManager.TrySaveCalibration(_draft, nib))
            { Status.Text = Text("CalibrationSaveFailed"); return; }
            Status.Text = Text("CalibrationSaved");
            ReturnToSettings(sender, e);
        }

        private bool TryOutline(out double width, out double height)
        {
            bool w = double.TryParse(OutlineWidth.Text, out width);
            bool h = double.TryParse(OutlineHeight.Text, out height);
            return w && h && TouchCalibrationRecord.Positive(width) && width <= 4096
                && TouchCalibrationRecord.Positive(height) && height <= 4096;
        }
    }
}
