using Ink_Canvas.Properties;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Ink_Canvas.Controls
{
    public partial class CanvasMediaControl : UserControl
    {
        private const string PlayIconGeometry = "M16.7501 8.41185L41.1672 21.1167C42.7595 21.9452 43.3786 23.9076 42.5501 25.4999C42.2421 26.0919 41.7592 26.5747 41.1672 26.8828L16.7501 39.5876C15.1579 40.4161 13.1954 39.797 12.3669 38.2047C12.1259 37.7414 12 37.2268 12 36.7045V11.2949C12 9.5 13.4551 8.04492 15.25 8.04492C15.6977 8.04492 16.1397 8.13739 16.5486 8.31562L16.7501 8.41185ZM15.5962 10.6296L15.4857 10.5829C15.4099 10.5578 15.3303 10.5449 15.25 10.5449C14.8358 10.5449 14.5 10.8807 14.5 11.2949V36.7045C14.5 36.8251 14.529 36.9438 14.5847 37.0507C14.7759 37.4182 15.2287 37.5611 15.5962 37.3699L40.0132 24.6651C40.1499 24.594 40.2613 24.4825 40.3324 24.3459C40.5236 23.9785 40.3807 23.5256 40.0132 23.3344L15.5962 10.6296Z";
        private const string PauseIconGeometry = "M11.75 6C9.67893 6 8 7.67893 8 9.75V38.25C8 40.3211 9.67893 42 11.75 42H18.25C20.3211 42 22 40.3211 22 38.25V9.75C22 7.67893 20.3211 6 18.25 6H11.75ZM10.5 9.75C10.5 9.05964 11.0596 8.5 11.75 8.5H18.25C18.9404 8.5 19.5 9.05964 19.5 9.75V38.25C19.5 38.9404 18.9404 39.5 18.25 39.5H11.75C11.0596 39.5 10.5 38.9404 10.5 38.25V9.75ZM29.75 6C27.6789 6 26 7.67893 26 9.75V38.25C26 40.3211 27.6789 42 29.75 42H36.25C38.3211 42 40 40.3211 40 38.25V9.75C40 7.67893 38.3211 6 36.25 6H29.75ZM28.5 9.75C28.5 9.05964 29.0596 8.5 29.75 8.5H36.25C36.9404 8.5 37.5 9.05964 37.5 9.75V38.25C37.5 38.9404 36.9404 39.5 36.25 39.5H29.75C29.0596 39.5 28.5 38.9404 28.5 38.25V9.75Z";

        private readonly DispatcherTimer _positionTimer;
        private bool _isDraggingProgress;
        private bool _isMediaOpened;
        private bool _isPlaying;
        private bool _isInternalProgressUpdate;
        private bool _pendingSeekAfterOpen;
        private TimeSpan _pendingPosition = TimeSpan.Zero;
        private double _pendingSpeedRatio = 1.0;
        private bool _suppressNestedSelection;

        public CanvasMediaControl()
        {
            InitializeComponent();
            _positionTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _positionTimer.Tick += PositionTimer_Tick;
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
            SourcePath = sourcePath;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? Path.GetFileName(sourcePath) : displayName;
            IsAudioOnly = IsAudioFile(sourcePath);
            ToolTip = DisplayName;
            AudioTitleTextBlock.Text = DisplayName;
            AudioPlaceholder.Visibility = IsAudioOnly ? Visibility.Visible : Visibility.Collapsed;
            PreviewRow.Height = IsAudioOnly ? GridLength.Auto : new GridLength(1, GridUnitType.Star);
            if (IsAudioOnly)
            {
                Height = 168;
                Width = Width > 0 ? Width : 520;
            }
            else
            {
                Width = Width > 0 ? Width : 800;
                Height = Height > 0 ? Height : 520;
            }
            Player.Source = new Uri(sourcePath);
            Player.Volume = VolumeSlider.Value;
            ApplySelectedSpeed();
            UpdateLocalizedTexts();
            UpdatePlayPauseGlyph();
            UpdateTimeText();
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
                _positionTimer.Stop();
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
                _positionTimer.Stop();
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
            PreviewHost.MouseLeftButtonDown += (_, e) => handler(this, e);
            AudioPlaceholder.MouseLeftButtonDown += (_, e) => handler(this, e);
        }

        public void RegisterTouchSelectHandler(EventHandler<TouchEventArgs> handler)
        {
            if (handler == null) return;
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
            VolumeLabelTextBlock.Text = TimerStrings.Timer_Volume;
        }

        private void CanvasMediaControl_Loaded(object sender, RoutedEventArgs e)
        {
            ApplySelectedSpeed();
            Player.Volume = VolumeSlider.Value;
        }

        private void CanvasMediaControl_Unloaded(object sender, RoutedEventArgs e)
        {
            _positionTimer.Stop();
        }

        private void PositionTimer_Tick(object sender, EventArgs e)
        {
            if (!_isMediaOpened || _isDraggingProgress) return;
            UpdateProgressFromPlayer();
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
            _positionTimer.Stop();
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
                    _positionTimer.Stop();
                }
                else
                {
                    Player.Play();
                    _isPlaying = true;
                    _positionTimer.Start();
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
            Player.Volume = e.NewValue;
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
                _positionTimer.Start();
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
