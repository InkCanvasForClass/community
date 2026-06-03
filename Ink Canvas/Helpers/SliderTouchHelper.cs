using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 为 WPF Slider 控件提供触屏/手写笔支持。
    /// 处理轨道上的点击定位和通过触摸拖动滑块。
    /// </summary>
    public static class SliderTouchHelper
    {
        #region IsEnabled

        public static bool GetIsEnabled(Slider slider)
        {
            return (bool)slider.GetValue(IsEnabledProperty);
        }

        public static void SetIsEnabled(Slider slider, bool value)
        {
            slider.SetValue(IsEnabledProperty, value);
        }

        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled",
                typeof(bool),
                typeof(SliderTouchHelper),
                new PropertyMetadata(false, OnIsEnabledChanged));

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var slider = d as Slider;
            if (slider == null) return;

            if ((bool)e.NewValue)
            {
                slider.PreviewStylusSystemGesture += OnPreviewStylusSystemGesture;
                slider.Loaded += OnSliderLoaded;
            }
            else
            {
                slider.PreviewStylusSystemGesture -= OnPreviewStylusSystemGesture;
                slider.Loaded -= OnSliderLoaded;
                UnhookThumb(slider);
            }
        }

        #endregion

        private static void OnSliderLoaded(object sender, RoutedEventArgs e)
        {
            var slider = (Slider)sender;
            HookThumb(slider);
        }

        private static void HookThumb(Slider slider)
        {
            var thumb = FindVisualChild<Thumb>(slider);
            if (thumb != null)
            {
                thumb.StylusDown += OnThumbStylusDown;
                thumb.StylusUp += OnThumbStylusUp;
                SetThumb(slider, thumb);
            }
        }

        private static void UnhookThumb(Slider slider)
        {
            var thumb = GetThumb(slider);
            if (thumb != null)
            {
                thumb.StylusDown -= OnThumbStylusDown;
                thumb.StylusUp -= OnThumbStylusUp;
            }
            slider.ClearValue(ThumbPropertyKey);
        }

        private static void OnThumbStylusDown(object sender, StylusDownEventArgs e)
        {
            // 阻止系统将触摸解释为点击手势，否则会取消滑块的拖动操作
            e.Handled = true;
        }

        private static void OnThumbStylusUp(object sender, StylusEventArgs e)
        {
            e.Handled = true;
        }

        private static void OnPreviewStylusSystemGesture(object sender, StylusSystemGestureEventArgs e)
        {
            var slider = (Slider)sender;

            switch (e.SystemGesture)
            {
                case SystemGesture.Tap:
                    // 点击轨道时，将滑块移动到点击位置
                    var track = FindVisualChild<Track>(slider);
                    if (track != null)
                    {
                        var point = e.GetPosition(track);
                        slider.Value = track.ValueFromPoint(point);
                    }
                    e.Handled = true;
                    break;

                case SystemGesture.HoldEnter:
                case SystemGesture.HoldLeave:
                case SystemGesture.RightTap:
                    // 阻止这些手势干扰滑块交互
                    e.Handled = true;
                    break;
            }
        }

        #region Thumb storage

        private static readonly DependencyPropertyKey ThumbPropertyKey =
            DependencyProperty.RegisterAttachedReadOnly(
                "Thumb",
                typeof(Thumb),
                typeof(SliderTouchHelper),
                null);

        private static Thumb GetThumb(Slider slider)
        {
            return (Thumb)slider.GetValue(ThumbPropertyKey.DependencyProperty);
        }

        private static void SetThumb(Slider slider, Thumb value)
        {
            slider.SetValue(ThumbPropertyKey, value);
        }

        #endregion

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result) return result;
                var found = FindVisualChild<T>(child);
                if (found != null) return found;
            }
            return null;
        }
    }
}
