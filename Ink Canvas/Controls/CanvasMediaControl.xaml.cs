using Ink_Canvas.Helpers;
using Ink_Canvas.Properties;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas.Controls
{
    public partial class CanvasMediaControl : UserControl
    {
        private const string PlayIconGeometry = "M16.7501 8.41185L41.1672 21.1167C42.7595 21.9452 43.3786 23.9076 42.5501 25.4999C42.2421 26.0919 41.7592 26.5747 41.1672 26.8828L16.7501 39.5876C15.1579 40.4161 13.1954 39.797 12.3669 38.2047C12.1259 37.7414 12 37.2268 12 36.7045V11.2949C12 9.5 13.4551 8.04492 15.25 8.04492C15.6977 8.04492 16.1397 8.13739 16.5486 8.31562L16.7501 8.41185ZM15.5962 10.6296L15.4857 10.5829C15.4099 10.5578 15.3303 10.5449 15.25 10.5449C14.8358 10.5449 14.5 10.8807 14.5 11.2949V36.7045C14.5 36.8251 14.529 36.9438 14.5847 37.0507C14.7759 37.4182 15.2287 37.5611 15.5962 37.3699L40.0132 24.6651C40.1499 24.594 40.2613 24.4825 40.3324 24.3459C40.5236 23.9785 40.3807 23.5256 40.0132 23.3344L15.5962 10.6296Z";
        private const string PauseIconGeometry = "M11.75 6C9.67893 6 8 7.67893 8 9.75V38.25C8 40.3211 9.67893 42 11.75 42H18.25C20.3211 42 22 40.3211 22 38.25V9.75C22 7.67893 20.3211 6 18.25 6H11.75ZM10.5 9.75C10.5 9.05964 11.0596 8.5 11.75 8.5H18.25C18.9404 8.5 19.5 9.05964 19.5 9.75V38.25C19.5 38.9404 18.9404 39.5 18.25 39.5H11.75C11.0596 39.5 10.5 38.9404 10.5 38.25V9.75ZM29.75 6C27.6789 6 26 7.67893 26 9.75V38.25C26 40.3211 27.6789 42 29.75 42H36.25C38.3211 42 40 40.3211 40 38.25V9.75C40 7.67893 38.3211 6 36.25 6H29.75ZM28.5 9.75C28.5 9.05964 29.0596 8.5 29.75 8.5H36.25C36.9404 8.5 37.5 9.05964 37.5 9.75V38.25C37.5 38.9404 36.9404 39.5 36.25 39.5H29.75C29.0596 39.5 28.5 38.9404 28.5 38.25V9.75Z";

        private bool _isDraggingProgress;
        private bool _isMediaOpened;
        private bool _isPlaying;
        private bool _isInternalProgressUpdate;
        private bool _pendingSeekAfterOpen;
        private TimeSpan _pendingPosition = TimeSpan.Zero;
        private double _pendingSpeedRatio = 1.0;
        private bool _suppressNestedSelection;

        // Render-loop hook for lyric/progress updates. We piggy-back on CompositionTarget.Rendering
        // (vsync-aligned) instead of a DispatcherTimer so the per-character karaoke highlight is
        // as smooth as the WPF render pipeline can deliver.
        private bool _renderingHooked;

        // LRC lyrics state
        private LrcData _lrcData;
        private int _currentLrcIndex = -1;

        // Per-line highlight animation state
        // _sungColor:    color of glyphs that have already been sung (full white)
        // _pendingColor: color of glyphs still ahead (dimmer white)
        // _charFadeDuration: how long the active glyph's in-word sweep takes.
        // The active glyph uses a 4-stop LinearGradientBrush in RelativeToBoundingBox mode so
        // the highlight walks from the left edge of the *glyph* to the right edge — that's the
        // "each part of the character lit up in sequence" karaoke feel.
        private static readonly Color _sungColor = Color.FromRgb(0xFF, 0xFF, 0xFF);
        private static readonly Color _pendingColor = Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF);
        private static readonly TimeSpan _charFadeDuration = TimeSpan.FromMilliseconds(220);

        // Width of the soft transition band inside the active glyph, expressed as a fraction of
        // its bounding-box width (0..1). Larger = softer sweep glow; smaller = crisper lit edge.
        private const double ActiveGlyphSoftBand = 0.25;

        public CanvasMediaControl()
        {
            InitializeComponent();
            // Drive progress + per-char sweep from CompositionTarget.Rendering so updates land
            // on each vsync instead of an arbitrary DispatcherTimer cadence — that's what gives
            // the lyric highlight its smooth karaoke feel under real WPF rendering pressure.
            Loaded += CanvasMediaControl_Loaded;
            Unloaded += CanvasMediaControl_Unloaded;
            AudioPlaceholder.Visibility = Visibility.Collapsed;
        }

        public string SourcePath { get; private set; }

        public string DisplayName { get; private set; }

        public bool IsAudioOnly { get; private set; }

        public MediaElement MediaPlayer => Player;

        public void Initialize(string sourcePath, string displayName = null)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
                throw new ArgumentException("Media source path cannot be empty.", nameof(sourcePath));

            if (!File.Exists(sourcePath))
                throw new FileNotFoundException("Media source file was not found.", sourcePath);

            if (!Uri.TryCreate(Path.GetFullPath(sourcePath), UriKind.Absolute, out var sourceUri))
                throw new ArgumentException("Media source path is invalid.", nameof(sourcePath));

            SourcePath = sourcePath;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? Path.GetFileName(sourcePath) : displayName;
            IsAudioOnly = IsAudioFile(sourcePath);
            ToolTip = DisplayName;
            AudioTitleTextBlock.Text = DisplayName;
            AudioPlaceholder.Visibility = IsAudioOnly ? Visibility.Visible : Visibility.Collapsed;
            PreviewRow.Height = IsAudioOnly ? GridLength.Auto : new GridLength(1, GridUnitType.Star);
            LoadLrcFile(sourcePath);
            if (IsAudioOnly)
            {
                Height = _lrcData != null ? 210 : 168;
                Width = Width > 0 ? Width : 520;
            }
            else
            {
                Width = Width > 0 ? Width : 800;
                Height = Height > 0 ? Height : 520;
            }
            Player.Source = sourceUri;
            Player.Volume = VolumeSlider.Value;
            ApplySelectedSpeed();
            UpdateLocalizedTexts();
            UpdatePlayPauseGlyph();
            UpdateTimeText();
            UpdateVolumePercentText();
        }

        public void SetPlaybackPosition(TimeSpan position)
        {
            _pendingPosition = position < TimeSpan.Zero ? TimeSpan.Zero : position;
            if (_isMediaOpened)
            {
                SeekTo(_pendingPosition);
            }
            else
            {
                _pendingSeekAfterOpen = true;
            }
        }

        public void SetPlaybackRate(double speedRatio)
        {
            _pendingSpeedRatio = speedRatio <= 0 ? 1.0 : speedRatio;
            foreach (ComboBoxItem item in SpeedComboBox.Items)
            {
                if (item.Tag == null) continue;
                if (double.TryParse(item.Tag.ToString(), out var value) && Math.Abs(value - _pendingSpeedRatio) < 0.001)
                {
                    SpeedComboBox.SelectedItem = item;
                    break;
                }
            }
            ApplySelectedSpeed();
        }

        public void SetVolumeLevel(double volume)
        {
            var normalized = Math.Max(0, Math.Min(1, volume));
            VolumeSlider.Value = normalized;
            Player.Volume = normalized;
        }

        public TimeSpan? GetPlaybackPositionOrNull()
        {
            if (!_isMediaOpened) return _pendingPosition;
            return Player.Position;
        }

        public double PlaybackRate => GetSelectedSpeedRatio();

        public double VolumeLevel => VolumeSlider.Value;

        public void PausePlayback()
        {
            try
            {
                Player.Pause();
                _isPlaying = false;
                UpdatePlayPauseGlyph();
            }
            catch
            {
            }
        }

        public void StopPlayback()
        {
            try
            {
                StopRenderHook();
                Player.Stop();
                _isPlaying = false;
                UpdatePlayPauseGlyph();
                if (_isMediaOpened)
                {
                    SeekTo(TimeSpan.Zero);
                }
            }
            catch
            {
            }
        }

        public void Shutdown()
        {
            try
            {
                StopRenderHook();
                Player.Stop();
                Player.Source = null;
            }
            catch
            {
            }
        }

        public void RegisterSelectHandler(MouseButtonEventHandler handler)
        {
            if (handler == null) return;
            Root.MouseLeftButtonDown += (_, e) => handler(this, e);
            PreviewHost.MouseLeftButtonDown += (_, e) => handler(this, e);
            AudioPlaceholder.MouseLeftButtonDown += (_, e) => handler(this, e);
        }

        public void RegisterTouchSelectHandler(EventHandler<TouchEventArgs> handler)
        {
            if (handler == null) return;
            Root.TouchDown += (_, e) => handler(this, e);
            PreviewHost.TouchDown += (_, e) => handler(this, e);
            AudioPlaceholder.TouchDown += (_, e) => handler(this, e);
        }

        public static bool IsInteractiveChildTarget(DependencyObject current)
        {
            while (current != null)
            {
                if (current is ButtonBase || current is Slider || current is ComboBox || current is ComboBoxItem || current is Thumb)
                {
                    return true;
                }

                if (current is CanvasMediaControl)
                {
                    return false;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return false;
        }

        public void UpdateLocalizedTexts()
        {
            SpeedLabelTextBlock.Text = FloatingBarStrings.FloatingBar_FadeSpeed;
        }

        private void CanvasMediaControl_Loaded(object sender, RoutedEventArgs e)
        {
            ApplySelectedSpeed();
            Player.Volume = VolumeSlider.Value;
            UpdateVolumePercentText();
        }

        private void CanvasMediaControl_Unloaded(object sender, RoutedEventArgs e)
        {
            StopRenderHook();
        }

        private void OnRendering(object sender, EventArgs e)
        {
            if (!_isMediaOpened || _isDraggingProgress) return;
            UpdateProgressFromPlayer();
            UpdateLyricsHighlight();
        }

        private void StartRenderHook()
        {
            if (_renderingHooked) return;
            // System.Windows.Media.CompositionTarget.Rendering fires once per vsync (typically
            // ~60 fps on a modern display, capped by monitor refresh). That makes the karaoke
            // sweep render in lockstep with the display instead of the DispatcherTimer cadence
            // which can drift and look choppy under heavy UI work.
            System.Windows.Media.CompositionTarget.Rendering += OnRendering;
            _renderingHooked = true;
        }

        private void StopRenderHook()
        {
            if (!_renderingHooked) return;
            System.Windows.Media.CompositionTarget.Rendering -= OnRendering;
            _renderingHooked = false;
        }

        private void Player_MediaOpened(object sender, RoutedEventArgs e)
        {
            _isMediaOpened = true;
            ApplySelectedSpeed();
            Player.Volume = VolumeSlider.Value;
            if (_pendingSeekAfterOpen)
            {
                SeekTo(_pendingPosition);
                _pendingSeekAfterOpen = false;
            }
            UpdateProgressMaximum();
            UpdateProgressFromPlayer();
        }

        private void Player_MediaEnded(object sender, RoutedEventArgs e)
        {
            _isPlaying = false;
            StopRenderHook();
            try
            {
                Player.Stop();
            }
            catch
            {
            }
            SeekTo(TimeSpan.Zero);
            UpdatePlayPauseGlyph();
        }

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            TogglePlayback();
        }

        private void TogglePlayback()
        {
            try
            {
                if (_isPlaying)
                {
                    Player.Pause();
                    _isPlaying = false;
                    StopRenderHook();
                }
                else
                {
                    Player.Play();
                    _isPlaying = true;
                    StartRenderHook();
                }
                UpdatePlayPauseGlyph();
            }
            catch
            {
            }
        }

        private void SpeedComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplySelectedSpeed();
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (Player != null) Player.Volume = e.NewValue;
            UpdateVolumePercentText();
        }

        private void UpdateVolumePercentText()
        {
            if (VolumePercentText != null)
                VolumePercentText.Text = $"{(int)(VolumeSlider.Value * 100)}%";
        }

        private void ProgressSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDraggingProgress = true;
        }

        private void ProgressSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            CommitSeekFromSlider();
        }

        private void ProgressSlider_LostMouseCapture(object sender, MouseEventArgs e)
        {
            if (_isDraggingProgress)
            {
                CommitSeekFromSlider();
            }
        }

        private void ProgressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInternalProgressUpdate) return;
            if (_isDraggingProgress)
            {
                UpdateTimeText(TimeSpan.FromSeconds(e.NewValue));
            }
        }

        private void CommitSeekFromSlider()
        {
            _isDraggingProgress = false;
            var target = TimeSpan.FromSeconds(ProgressSlider.Value);
            SeekTo(target);
            if (_isPlaying)
            {
                StartRenderHook();
            }
        }

        private void SeekTo(TimeSpan position)
        {
            if (position < TimeSpan.Zero) position = TimeSpan.Zero;
            _pendingPosition = position;
            if (_isMediaOpened)
            {
                try
                {
                    Player.Position = position;
                }
                catch
                {
                }
            }
            UpdateTimeText(position);
            if (_isMediaOpened)
            {
                UpdateProgressFromPlayer();
            }
        }

        private void UpdateProgressMaximum()
        {
            var totalSeconds = Player.NaturalDuration.HasTimeSpan ? Player.NaturalDuration.TimeSpan.TotalSeconds : 0;
            if (double.IsNaN(totalSeconds) || double.IsInfinity(totalSeconds) || totalSeconds < 0)
            {
                totalSeconds = 0;
            }
            ProgressSlider.Maximum = Math.Max(1, totalSeconds);
        }

        private void UpdateProgressFromPlayer()
        {
            if (!_isMediaOpened) return;
            _isInternalProgressUpdate = true;
            UpdateProgressMaximum();
            var seconds = Math.Max(0, Player.Position.TotalSeconds);
            if (seconds > ProgressSlider.Maximum)
            {
                ProgressSlider.Maximum = Math.Max(1, seconds);
            }
            ProgressSlider.Value = seconds;
            _isInternalProgressUpdate = false;
            UpdateTimeText(Player.Position);
        }

        private void UpdateTimeText()
        {
            var current = _isMediaOpened ? Player.Position : _pendingPosition;
            UpdateTimeText(current);
        }

        private void UpdateTimeText(TimeSpan current)
        {
            var total = _isMediaOpened && Player.NaturalDuration.HasTimeSpan
                ? Player.NaturalDuration.TimeSpan
                : TimeSpan.Zero;
            TimeTextBlock.Text = $"{FormatTime(current)} / {FormatTime(total)}";
        }

        private void UpdatePlayPauseGlyph()
        {
            PlayPauseIconPath.Data = Geometry.Parse(_isPlaying ? PauseIconGeometry : PlayIconGeometry);
        }

        private void ApplySelectedSpeed()
        {
            var ratio = GetSelectedSpeedRatio();
            _pendingSpeedRatio = ratio;
            try
            {
                Player.SpeedRatio = ratio;
            }
            catch
            {
            }
        }

        private double GetSelectedSpeedRatio()
        {
            if (SpeedComboBox.SelectedItem is ComboBoxItem item && item.Tag != null &&
                double.TryParse(item.Tag.ToString(), out var value))
            {
                return value;
            }
            return _pendingSpeedRatio <= 0 ? 1.0 : _pendingSpeedRatio;
        }

        private static bool IsAudioFile(string path)
        {
            var ext = Path.GetExtension(path);
            return string.Equals(ext, ".mp3", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ext, ".wav", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ext, ".m4a", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ext, ".aac", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ext, ".flac", StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatTime(TimeSpan value)
        {
            if (value.TotalHours >= 1)
            {
                return value.ToString(@"hh\:mm\:ss");
            }
            return value.ToString(@"mm\:ss");
        }

        protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject source)
            {
                if (FindAncestor<Button>(source) != null || FindAncestor<Slider>(source) != null || FindAncestor<ComboBox>(source) != null)
                {
                    _suppressNestedSelection = true;
                    e.Handled = false;
                    base.OnPreviewMouseLeftButtonDown(e);
                    return;
                }
            }
            _suppressNestedSelection = false;
            base.OnPreviewMouseLeftButtonDown(e);
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            if (_suppressNestedSelection)
            {
                _suppressNestedSelection = false;
                base.OnMouseLeftButtonDown(e);
                return;
            }
            base.OnMouseLeftButtonDown(e);
        }

        private void LoadLrcFile(string audioPath)
        {
            // Reset previous lyrics state
            _lrcData = null;
            _currentLrcIndex = -1;
            if (LyricsPanel != null) LyricsPanel.Visibility = Visibility.Collapsed;
            if (LyricTextBlock != null) LyricTextBlock.Text = string.Empty;
            if (LyricPerCharHost != null) LyricPerCharHost.Visibility = Visibility.Collapsed;
            if (LyricSungPrefix != null) LyricSungPrefix.Text = string.Empty;
            if (LyricActiveChar != null) LyricActiveChar.Text = string.Empty;
            if (LyricPendingSuffix != null) LyricPendingSuffix.Text = string.Empty;
            if (LyricTranslationBlock != null)
            {
                LyricTranslationBlock.Text = string.Empty;
                LyricTranslationBlock.Visibility = Visibility.Collapsed;
            }

            // Try to find a matching LRC file
            var lrcPath = Path.ChangeExtension(audioPath, ".lrc");
            var data = LrcParser.ParseFile(lrcPath);
            if (data == null || data.Lines.Count == 0)
                return;

            // Build per-character timings: prefer inline <mm:ss.xx> tags; for any line missing them,
            // distribute evenly between the previous and next line time (or a 4s default).
            FillCharTimings(data.Lines);

            _lrcData = data;
            if (LyricsPanel != null) LyricsPanel.Visibility = Visibility.Visible;
            // Adjust height for bilingual lyrics
            if (IsAudioOnly && data.Lines.Any(l => !string.IsNullOrEmpty(l.Translation)))
            {
                Height = 240;
            }
        }

        /// <summary>
        /// For each line, ensure the Chars list is populated. Lines without inline timestamps
        /// get an even slice of (nextLineStart - lineStart).
        /// </summary>
        private static void FillCharTimings(List<LrcLine> lines)
        {
            if (lines == null) return;
            var defaultDuration = TimeSpan.FromSeconds(4);
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (string.IsNullOrEmpty(line.Text)) continue;
                var nextStart = i + 1 < lines.Count ? (TimeSpan?)lines[i + 1].Time : null;
                LrcParser.EnsureCharTimings(line, nextStart, defaultDuration);
            }
        }

        private void UpdateLyricsHighlight()
        {
            if (_lrcData == null || _lrcData.Lines.Count == 0) return;

            var position = Player.Position;
            var newIndex = LrcParser.GetCurrentLineIndex(_lrcData.Lines, position);

            if (newIndex != _currentLrcIndex)
            {
                _currentLrcIndex = newIndex;
                if (newIndex < 0 || newIndex >= _lrcData.Lines.Count)
                {
                    ClearLyricsDisplay();
                    return;
                }

                var line = _lrcData.Lines[newIndex];
                RebuildLyricTriplets(line);
                UpdateTranslation(line);
                return;
            }

            // Same line still active: refresh the active glyph's blend.
            if (_currentLrcIndex >= 0 && _currentLrcIndex < _lrcData.Lines.Count)
            {
                UpdateActiveGlyph(_lrcData.Lines[_currentLrcIndex], position);
            }
        }

        private void ClearLyricsDisplay()
        {
            if (LyricTextBlock != null) LyricTextBlock.Text = string.Empty;
            if (LyricSungPrefix != null) LyricSungPrefix.Text = string.Empty;
            if (LyricActiveChar != null) LyricActiveChar.Text = string.Empty;
            if (LyricPendingSuffix != null) LyricPendingSuffix.Text = string.Empty;
            if (LyricPerCharHost != null) LyricPerCharHost.Visibility = Visibility.Collapsed;
        }

        private void UpdateTranslation(LrcLine line)
        {
            if (LyricTranslationBlock == null) return;
            if (string.IsNullOrEmpty(line.Translation))
            {
                LyricTranslationBlock.Text = string.Empty;
                LyricTranslationBlock.Visibility = Visibility.Collapsed;
            }
            else
            {
                LyricTranslationBlock.Text = line.Translation;
                LyricTranslationBlock.Visibility = Visibility.Visible;
            }
        }

        // Cached brush reused while the active glyph's progress animates. We rebuild it only when
        // the playhead advances to a different glyph (idx changes) — intra-glyph progress just
        // shifts the two middle GradientStops in place, so we keep the same brush instance.
        private LinearGradientBrush _activeGlyphBrush;
        private System.Windows.Documents.Run _activeGlyphRun;

        /// <summary>
        /// Rehydrates the sung/active/pending triplet for the new lyric line. Each TextBlock's
        /// Text is set once; subsequent frames only adjust the active glyph's gradient stops.
        /// </summary>
        private void RebuildLyricTriplets(LrcLine line)
        {
            if (LyricSungPrefix == null || LyricActiveChar == null || LyricPendingSuffix == null
                || LyricPerCharHost == null) return;

            var chars = line.Chars;
            if (chars == null || chars.Count == 0)
            {
                LyricPerCharHost.Visibility = Visibility.Collapsed;
                if (LyricTextBlock != null) LyricTextBlock.Text = line.Text;
                return;
            }

            if (LyricTextBlock != null) LyricTextBlock.Text = string.Empty;
            LyricPerCharHost.Visibility = Visibility.Visible;

            // Active TextBlock always hosts exactly one Run with the gradient brush; the Run is
            // rebuilt on every glyph change so it owns a fresh unsubscribed brush reference.
            LyricSungPrefix.Text = string.Empty;
            LyricPendingSuffix.Text = ConcatText(chars, 1);
            _activeGlyphRun = new System.Windows.Documents.Run(chars[0].Text);
            _activeGlyphBrush = BuildActiveGlyphBrush(0);
            _activeGlyphRun.Foreground = _activeGlyphBrush;
            LyricActiveChar.Inlines.Clear();
            LyricActiveChar.Inlines.Add(_activeGlyphRun);

            UpdateActiveGlyph(line, Player.Position);
        }

        /// <summary>
        /// Recomputes the sung prefix boundary and slides the in-word sweep across the active
        /// glyph for the current playhead position.
        /// </summary>
        private void UpdateActiveGlyph(LrcLine line, TimeSpan position)
        {
            if (LyricSungPrefix == null || LyricActiveChar == null || LyricPendingSuffix == null) return;

            var chars = line.Chars;
            if (chars == null || chars.Count == 0) return;

            var relative = position - line.Time;
            int activeIdx = -1;
            for (int i = 0; i < chars.Count; i++)
            {
                if (chars[i].StartOffset <= relative) activeIdx = i;
                else break;
            }

            // Build prefix / active / suffix text for the current index.
            string prefix = activeIdx >= 0 ? ConcatText(chars, 0, activeIdx) : string.Empty;
            string suffix = ConcatText(chars, activeIdx + 1);

            if (!string.Equals(LyricSungPrefix.Text, prefix, StringComparison.Ordinal))
            {
                LyricSungPrefix.Text = prefix;
            }
            if (!string.Equals(LyricPendingSuffix.Text, suffix, StringComparison.Ordinal))
            {
                LyricPendingSuffix.Text = suffix;
            }

            // When the active glyph changes, rebuild its Run + brush so a new bounding-box brush
            // is in effect. Otherwise just shift the existing brush's stops.
            if (activeIdx < 0)
            {
                // No active glyph yet — show nothing in the active slot.
                if (!string.IsNullOrEmpty(LyricActiveChar.Text))
                {
                    LyricActiveChar.Inlines.Clear();
                    _activeGlyphRun = null;
                    _activeGlyphBrush = null;
                }
                return;
            }

            var newRunText = chars[activeIdx].Text;
            var progress = ComputeActiveProgress(chars[activeIdx], relative);

            if (_activeGlyphRun == null || !string.Equals(_activeGlyphRun.Text, newRunText, StringComparison.Ordinal))
            {
                _activeGlyphBrush = BuildActiveGlyphBrush(progress);
                _activeGlyphRun = new System.Windows.Documents.Run(newRunText) { Foreground = _activeGlyphBrush };
                LyricActiveChar.Inlines.Clear();
                LyricActiveChar.Inlines.Add(_activeGlyphRun);
            }
            else if (_activeGlyphBrush != null)
            {
                SetActiveGlyphProgress(_activeGlyphBrush, progress);
            }
        }

        private double ComputeActiveProgress(LrcChar ch, TimeSpan relative)
        {
            var intoChar = relative - ch.StartOffset;
            var charDur = ch.Duration ?? _charFadeDuration;
            if (charDur <= TimeSpan.Zero) return 1;
            var progress = intoChar.TotalMilliseconds / charDur.TotalMilliseconds;
            if (progress < 0) progress = 0;
            else if (progress > 1) progress = 1;
            return progress;
        }

        /// <summary>
        /// Builds a 4-stop horizontal LinearGradientBrush in the glyph's own bounding box, with
        /// stops 1/2 acting as the sung/pending boundary swept by the playhead.
        /// </summary>
        private static LinearGradientBrush BuildActiveGlyphBrush(double progress)
        {
            // The painted glyph is structured so that 0..left is fully sung, right..1 is fully
            // pending, and only the band around progress softens the transition. The left/right
            // symmetric soft band collapses to a single point when progress==0 or progress==1,
            // so the brush never leaks pending color into the lit half of the glyph.
            var softBand = Math.Min(ActiveGlyphSoftBand, progress);
            softBand = Math.Min(softBand, 1 - progress);
            var left = progress - softBand;
            var right = progress + softBand;

            var brush = new LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0, 0.5),
                EndPoint = new System.Windows.Point(1, 0.5),
                MappingMode = BrushMappingMode.RelativeToBoundingBox,
                SpreadMethod = GradientSpreadMethod.Pad
            };
            brush.GradientStops.Add(new GradientStop(_sungColor, 0.0));
            brush.GradientStops.Add(new GradientStop(_sungColor, left));
            brush.GradientStops.Add(new GradientStop(_pendingColor, right));
            brush.GradientStops.Add(new GradientStop(_pendingColor, 1.0));
            return brush;
        }

        /// <summary>
        /// Updates the middle GradientStop offsets on an existing active-glyph brush without
        /// rebuilding the brush itself — cheapest possible way to slide the lit edge.
        /// </summary>
        private static void SetActiveGlyphProgress(LinearGradientBrush brush, double progress)
        {
            if (brush == null || brush.GradientStops.Count < 4) return;
            var softBand = Math.Min(ActiveGlyphSoftBand, progress);
            softBand = Math.Min(softBand, 1 - progress);
            brush.GradientStops[1].Offset = progress - softBand;
            brush.GradientStops[2].Offset = progress + softBand;
        }

        private static string ConcatText(List<LrcChar> chars, int from, int to = int.MaxValue)
        {
            if (chars == null || from >= chars.Count) return string.Empty;
            if (to > chars.Count) to = chars.Count;
            if (to <= from) return string.Empty;
            var sb = new System.Text.StringBuilder(to - from);
            for (int i = from; i < to; i++) sb.Append(chars[i].Text);
            return sb.ToString();
        }

        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T matched)
                {
                    return matched;
                }
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }
            return null;
        }
    }
}
