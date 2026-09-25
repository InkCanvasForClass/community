using Ink_Canvas.Helpers;
using Ink_Canvas.Properties;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

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
                    CardUseWinRTInk.IsOn = settings.Canvas.UseWinRTInk;

                    int curveMode = 0;
                    if (settings.Canvas.UseAdvancedBezierSmoothing) curveMode = 2;
                    else if (settings.Canvas.FitToCurve) curveMode = 1;
                    ComboBoxCurveSmoothingMode.SelectedIndex = curveMode;

                    BrushAutoRestoreTimesTextBox.Text = settings.Canvas.BrushAutoRestoreTimes ?? string.Empty;
                    LoadBrushAutoRestoreColor(settings.Canvas.BrushAutoRestoreColor);

                    // 加载画笔光标类型设置
                    ComboBoxPenCursorType.SelectedIndex = settings.Canvas.PenCursorType;
                    CustomPenCursorPathText.Text = settings.Canvas.CustomPenCursorPath ?? string.Empty;
                    UpdateCustomPenCursorPathVisibility();

                    // 加载扩展画布提示自动隐藏延时（设置存储为毫秒，界面以秒为单位）
                    if (settings.Canvas.EdgeExpandAutoHideMs < 1000 || settings.Canvas.EdgeExpandAutoHideMs > 30000)
                    {
                        settings.Canvas.EdgeExpandAutoHideMs = 5000;
                        SettingsManager.SaveSettingsToFile();
                    }
                    EdgeExpandHintAutoHideDelaySlider.Value = settings.Canvas.EdgeExpandAutoHideMs / 1000.0;
                    EdgeExpandHintAutoHideDelayText.Text =
                        string.Format(CanvasStrings.Canvas_SecondsFormat, EdgeExpandHintAutoHideDelaySlider.Value);

                    // 加载批注状态点提示设置
                    AnnotationDotHintClusterRadiusSlider.Value = settings.Canvas.AnnotationDotHintClusterRadius;
                    AnnotationDotHintClusterRadiusText.Text = $"{settings.Canvas.AnnotationDotHintClusterRadius:F0} px";
                    AnnotationDotHintStrokeLengthSlider.Value = settings.Canvas.AnnotationDotHintStrokeLengthThreshold;
                    AnnotationDotHintStrokeLengthText.Text = $"{settings.Canvas.AnnotationDotHintStrokeLengthThreshold:F0} px";
                    AnnotationDotHintClickCountSlider.Value = settings.Canvas.AnnotationDotHintClickCount;
                    AnnotationDotHintClickCountText.Text = $"{settings.Canvas.AnnotationDotHintClickCount} 次";
                    AnnotationDotHintDisplayDurationSlider.Value = settings.Canvas.AnnotationDotHintDisplayDurationSeconds;
                    AnnotationDotHintDisplayDurationText.Text = $"{settings.Canvas.AnnotationDotHintDisplayDurationSeconds:F0} 秒";

                    // 更新触发区域预览
                    UpdateAnnotationDotHintPreview(settings.Canvas.AnnotationDotHintClusterRadius);
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

        private void ToggleSwitchUseWinRTInk_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Canvas.UseWinRTInk = CardUseWinRTInk.IsOn;
            SettingsManager.SaveSettingsToFile();
            // 切换实验性墨迹管线：让 MainWindow 按当前逻辑工具挂载/卸载系统湿墨。
            (Application.Current.MainWindow as MainWindow)?.SyncWinRTInkPipelineWithLogicalTool();
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

        private void ComboBoxPenCursorType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            UpdateCustomPenCursorPathVisibility();
        }

        private void EdgeExpandHintAutoHideDelaySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isLoaded) return;
            double seconds = Math.Round(e.NewValue);
            EdgeExpandHintAutoHideDelayText.Text = string.Format(CanvasStrings.Canvas_SecondsFormat, seconds);
            SettingsManager.Settings.Canvas.EdgeExpandAutoHideMs = seconds * 1000;
            SettingsManager.SaveSettingsToFile();
        }

        private void AnnotationDotHintClusterRadiusSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isLoaded) return;
            double value = Math.Round(e.NewValue);
            AnnotationDotHintClusterRadiusText.Text = $"{value:F0} px";
            SettingsManager.Settings.Canvas.AnnotationDotHintClusterRadius = value;
            UpdateAnnotationDotHintPreview(value);
            SettingsManager.SaveSettingsToFile();
        }

        private void AnnotationDotHintStrokeLengthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isLoaded) return;
            double value = Math.Round(e.NewValue);
            AnnotationDotHintStrokeLengthText.Text = $"{value:F0} px";
            SettingsManager.Settings.Canvas.AnnotationDotHintStrokeLengthThreshold = value;
            SettingsManager.SaveSettingsToFile();
        }

        private void AnnotationDotHintClickCountSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isLoaded) return;
            int value = (int)Math.Round(e.NewValue);
            AnnotationDotHintClickCountText.Text = $"{value} 次";
            SettingsManager.Settings.Canvas.AnnotationDotHintClickCount = value;
            UpdateAnnotationDotHintPreview(SettingsManager.Settings.Canvas.AnnotationDotHintClusterRadius);
            SettingsManager.SaveSettingsToFile();
        }

        private void AnnotationDotHintDisplayDurationSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isLoaded) return;
            double value = Math.Round(e.NewValue);
            AnnotationDotHintDisplayDurationText.Text = $"{value:F0} 秒";
            SettingsManager.Settings.Canvas.AnnotationDotHintDisplayDurationSeconds = value;
            SettingsManager.SaveSettingsToFile();
        }

        // 预览板：点击画点追踪
        private readonly Queue<System.Windows.Point> _previewDotQueue = new Queue<System.Windows.Point>();
        private System.Windows.Point _previewMouseDownPoint;
        private bool _previewHintShown;

        private void UpdateAnnotationDotHintPreview(double radius)
        {
            if (AnnotationDotHintPreviewZone == null) return;

            // 预览圆圈大小：半径映射到圆圈直径（最小 16px，最大 120px）
            double circleSize = Math.Min(radius * 2, 120);
            circleSize = Math.Max(circleSize, 16);
            AnnotationDotHintPreviewZone.Width = circleSize;
            AnnotationDotHintPreviewZone.Height = circleSize;

            if (AnnotationDotHintPreviewLabel != null)
            {
                int clickCount = SettingsManager.Settings.Canvas.AnnotationDotHintClickCount;
                AnnotationDotHintPreviewLabel.Text = string.Format(Properties.FloatingBarStrings.Canvas_AnnotationDotHint_Settings_PreviewLabel, clickCount);
            }
        }

        private void AnnotationDotHintPreviewBoard_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var board = sender as System.Windows.UIElement;
            if (board == null) return;
            _previewMouseDownPoint = e.GetPosition(board);
        }

        private void AnnotationDotHintPreviewBoard_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var board = sender as System.Windows.UIElement;
            if (board == null) return;

            var upPoint = e.GetPosition(board);
            double dx = upPoint.X - _previewMouseDownPoint.X;
            double dy = upPoint.Y - _previewMouseDownPoint.Y;
            double dist = Math.Sqrt(dx * dx + dy * dy);

            // 视为点击（移动 < 5px）
            if (dist > 5) return;

            DrawPreviewDot(_previewMouseDownPoint);
            TrackPreviewDot(_previewMouseDownPoint);
        }

        private void DrawPreviewDot(System.Windows.Point position)
        {
            if (AnnotationDotHintPreviewCanvas == null) return;

            var dot = new System.Windows.Shapes.Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 37, 99, 235)),
                IsHitTestVisible = false
            };
            System.Windows.Controls.Canvas.SetLeft(dot, position.X - 3);
            System.Windows.Controls.Canvas.SetTop(dot, position.Y - 3);
            AnnotationDotHintPreviewCanvas.Children.Add(dot);

            // 10 秒后渐隐消失
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(10)
            };
            timer.Tick += (s, args) =>
            {
                timer.Stop();
                var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(500));
                fadeOut.Completed += (_, _) =>
                {
                    AnnotationDotHintPreviewCanvas.Children.Remove(dot);
                };
                dot.BeginAnimation(System.Windows.UIElement.OpacityProperty, fadeOut);
            };
            timer.Start();
        }

        private void TrackPreviewDot(System.Windows.Point position)
        {
            double boardW = AnnotationDotHintPreviewBoard?.ActualWidth ?? 300;
            double boardH = AnnotationDotHintPreviewBoard?.ActualHeight ?? 180;
            if (double.IsNaN(boardW) || boardW <= 0) boardW = 300;
            if (double.IsNaN(boardH) || boardH <= 0) boardH = 180;

            // 判断点是否在圆圈内
            double cx = boardW / 2;
            double cy = boardH / 2;
            double circleRadius = (AnnotationDotHintPreviewZone?.Width ?? 60) / 2;

            double distFromCenter = Math.Sqrt((position.X - cx) * (position.X - cx) + (position.Y - cy) * (position.Y - cy));
            if (distFromCenter > circleRadius) return; // 圈外点击不追踪

            _previewDotQueue.Enqueue(position);
            while (_previewDotQueue.Count > 10)
                _previewDotQueue.Dequeue();

            int clickCount = SettingsManager.Settings.Canvas.AnnotationDotHintClickCount;
            if (_previewDotQueue.Count < clickCount) return;

            // 检查最近 clickCount 个点是否都在圆圈内
            var arr = _previewDotQueue.ToArray();
            int start = arr.Length - clickCount;
            bool allInCircle = true;
            for (int i = start; i < arr.Length; i++)
            {
                double d = Math.Sqrt((arr[i].X - cx) * (arr[i].X - cx) + (arr[i].Y - cy) * (arr[i].Y - cy));
                if (d > circleRadius)
                {
                    allInCircle = false;
                    break;
                }
            }

            if (allInCircle && !_previewHintShown)
            {
                _previewHintShown = true;
                ShowPreviewHintFlash();
            }
        }

        private void ShowPreviewHintFlash()
        {
            if (AnnotationDotHintPreviewZone == null) return;

            // 圆圈闪烁表示触发（绿色）
            var flash = new System.Windows.Media.Animation.ColorAnimation
            {
                From = System.Windows.Media.Color.FromArgb(16, 34, 197, 94),
                To = System.Windows.Media.Color.FromArgb(160, 34, 197, 94),
                Duration = TimeSpan.FromMilliseconds(200),
                AutoReverse = true,
                RepeatBehavior = new System.Windows.Media.Animation.RepeatBehavior(3)
            };
            var brush = AnnotationDotHintPreviewZone.Fill as System.Windows.Media.SolidColorBrush;
            if (brush == null)
            {
                brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(16, 34, 197, 94));
                AnnotationDotHintPreviewZone.Fill = brush;
            }
            brush.BeginAnimation(System.Windows.Media.SolidColorBrush.ColorProperty, flash);

            // 圆边框也变绿
            AnnotationDotHintPreviewZone.Stroke = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(160, 34, 197, 94));

            // 在提示文字中显示"已触发"
            if (AnnotationDotHintPreviewLabel != null)
            {
                var origText = AnnotationDotHintPreviewLabel.Text;
                AnnotationDotHintPreviewLabel.Text = Properties.FloatingBarStrings.Canvas_AnnotationDotHint_Settings_PreviewTriggered;
                AnnotationDotHintPreviewLabel.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(200, 37, 99, 235));

                // 3 秒后恢复
                var displaySeconds = SettingsManager.Settings.Canvas.AnnotationDotHintDisplayDurationSeconds;
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(displaySeconds)
                };
                timer.Tick += (s, args) =>
                {
                    timer.Stop();
                    _previewHintShown = false;
                    _previewDotQueue.Clear();
                    AnnotationDotHintPreviewLabel.Text = origText;
                    AnnotationDotHintPreviewLabel.Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromArgb(96, 0, 0, 0));
                    // 恢复圆圈原始颜色
                    AnnotationDotHintPreviewZone.Fill = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromArgb(16, 37, 99, 235));
                    AnnotationDotHintPreviewZone.Stroke = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromArgb(128, 37, 99, 235));
                };
                timer.Start();
            }
        }

        private void UpdateCustomPenCursorPathVisibility()
        {
            if (CardCustomPenCursorPath == null) return;
            CardCustomPenCursorPath.Visibility =
                ComboBoxPenCursorType.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SelectCustomPenCursor_Click(object sender, RoutedEventArgs e)
        {
            var filter = CanvasStrings.Canvas_CustomPenCursorFilter;
            if (string.IsNullOrWhiteSpace(filter))
                filter = "Cursor files|*.cur;*.ani";

            var dialog = new OpenFileDialog
            {
                Filter = filter,
                Title = CanvasStrings.Canvas_SelectCustomPenCursor
            };

            if (dialog.ShowDialog() == true)
            {
                var path = dialog.FileName;
                SettingsManager.Settings.Canvas.CustomPenCursorPath = path;
                CustomPenCursorPathText.Text = path;
                SettingsManager.SaveSettingsToFile();
                SettingsActionHub.OnCustomPenCursorPathChanged();
            }
        }
    }
}
