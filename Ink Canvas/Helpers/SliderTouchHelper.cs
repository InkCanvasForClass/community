using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 为 Slider 控件提供触摸和手写笔事件支持的辅助类
    /// </summary>
    public static class SliderTouchHelper
    {
        // 内部标记：是否已初始化触摸支持
        private static readonly DependencyProperty TouchSupportInitializedProperty =
            DependencyProperty.RegisterAttached(
                "TouchSupportInitialized",
                typeof(bool),
                typeof(SliderTouchHelper),
                new PropertyMetadata(false));

        private static bool GetTouchSupportInitialized(DependencyObject obj)
        {
            return (bool)obj.GetValue(TouchSupportInitializedProperty);
        }

        private static void SetTouchSupportInitialized(DependencyObject obj, bool value)
        {
            obj.SetValue(TouchSupportInitializedProperty, value);
        }

        /// <summary>
        /// 为单个滑块控件添加触摸和手写笔事件支持
        /// </summary>
        public static void AddTouchSupport(Slider slider)
        {
            if (slider == null) return;

            // 避免重复添加
            if (GetTouchSupportInitialized(slider)) return;
            SetTouchSupportInitialized(slider, true);

            slider.IsManipulationEnabled = true;

            slider.TouchDown += (s, e) => HandleSliderTouch(s, e, slider);
            slider.TouchMove += (s, e) => HandleSliderTouch(s, e, slider);
            slider.TouchUp += (s, e) => HandleSliderTouchEnd(s, e, slider);

            slider.StylusDown += (s, e) => HandleSliderStylus(s, e, slider);
            slider.StylusMove += (s, e) => HandleSliderStylus(s, e, slider);
            slider.StylusUp += (s, e) => HandleSliderStylusEnd(s, e, slider);
        }

        /// <summary>
        /// 为指定逻辑树中的所有 Slider 控件添加触摸支持
        /// </summary>
        public static void AddTouchSupportToAllSliders(DependencyObject root)
        {
            if (root == null) return;

            var visited = new System.Collections.Generic.HashSet<DependencyObject>();
            var queue = new System.Collections.Generic.Queue<DependencyObject>();
            queue.Enqueue(root);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current == null || !visited.Add(current)) continue;

                if (current is Slider slider && !GetTouchSupportInitialized(slider))
                {
                    AddTouchSupport(slider);
                }

                // 遍历视觉树（仅对 Visual 类型）
                if (current is System.Windows.Media.Visual)
                {
                    int childCount = VisualTreeHelper.GetChildrenCount(current);
                    for (int i = 0; i < childCount; i++)
                    {
                        var child = VisualTreeHelper.GetChild(current, i);
                        queue.Enqueue(child);
                    }
                }

                // 也遍历逻辑树（处理尚未加载到视觉树的元素）
                foreach (var child in LogicalTreeHelper.GetChildren(current))
                {
                    if (child is DependencyObject childDep)
                    {
                        queue.Enqueue(childDep);
                    }
                }
            }
        }

        private static void HandleSliderTouch(object sender, TouchEventArgs e, Slider slider)
        {
            if (slider == null) return;

            if (e.RoutedEvent == UIElement.TouchDownEvent)
            {
                slider.CaptureTouch(e.TouchDevice);
            }

            var touchPoint = e.GetTouchPoint(slider);
            UpdateSliderValueFromPosition(slider, touchPoint.Position);

            e.Handled = true;
        }

        private static void HandleSliderTouchEnd(object sender, TouchEventArgs e, Slider slider)
        {
            if (slider == null) return;

            slider.ReleaseTouchCapture(e.TouchDevice);
            e.Handled = true;
        }

        private static void HandleSliderStylus(object sender, StylusEventArgs e, Slider slider)
        {
            if (slider == null) return;

            if (e.RoutedEvent == UIElement.StylusDownEvent)
            {
                slider.CaptureStylus();
            }

            var stylusPoint = e.GetStylusPoints(slider);
            if (stylusPoint.Count > 0)
            {
                UpdateSliderValueFromPosition(slider, stylusPoint[0].ToPoint());
            }

            e.Handled = true;
        }

        private static void HandleSliderStylusEnd(object sender, StylusEventArgs e, Slider slider)
        {
            if (slider == null) return;

            slider.ReleaseStylusCapture();
            e.Handled = true;
        }

        /// <summary>
        /// 根据触摸/手写笔位置更新滑块值
        /// </summary>
        private static void UpdateSliderValueFromPosition(Slider slider, Point position)
        {
            if (slider == null) return;

            try
            {
                var track = slider.Template?.FindName("PART_Track", slider) as Track;
                if (track != null)
                {
                    var trackBounds = track.TransformToAncestor(slider).TransformBounds(
                        new Rect(0, 0, track.ActualWidth, track.ActualHeight));

                    double relativePosition = 0;

                    if (slider.Orientation == Orientation.Horizontal)
                    {
                        if (trackBounds.Width > 0)
                        {
                            var relativeX = position.X - trackBounds.X;
                            relativePosition = Math.Max(0, Math.Min(1, relativeX / trackBounds.Width));
                        }
                    }
                    else
                    {
                        if (trackBounds.Height > 0)
                        {
                            var relativeY = position.Y - trackBounds.Y;
                            relativePosition = Math.Max(0, Math.Min(1, relativeY / trackBounds.Height));
                        }
                    }

                    var newValue = slider.Minimum + relativePosition * (slider.Maximum - slider.Minimum);

                    if (slider.IsSnapToTickEnabled && slider.TickFrequency > 0)
                    {
                        var tickCount = (int)((slider.Maximum - slider.Minimum) / slider.TickFrequency);
                        var tickIndex = (int)Math.Round(relativePosition * tickCount);
                        newValue = slider.Minimum + tickIndex * slider.TickFrequency;
                    }

                    slider.Value = Math.Max(slider.Minimum, Math.Min(slider.Maximum, newValue));
                }
                else
                {
                    UpdateSliderValueFromPositionFallback(slider, position);
                }
            }
            catch (Exception)
            {
                UpdateSliderValueFromPositionFallback(slider, position);
            }
        }

        /// <summary>
        /// 根据触摸/手写笔位置更新滑块值（回退方法）
        /// </summary>
        private static void UpdateSliderValueFromPositionFallback(Slider slider, Point position)
        {
            if (slider == null) return;

            try
            {
                double relativePosition = 0;

                if (slider.Orientation == Orientation.Horizontal)
                {
                    var sliderWidth = slider.ActualWidth;
                    if (sliderWidth > 0)
                    {
                        var thumbSize = 20;
                        var effectiveWidth = sliderWidth - thumbSize;
                        var adjustedX = position.X - thumbSize / 2;
                        relativePosition = Math.Max(0, Math.Min(1, adjustedX / effectiveWidth));
                    }
                }
                else
                {
                    var sliderHeight = slider.ActualHeight;
                    if (sliderHeight > 0)
                    {
                        var thumbSize = 20;
                        var effectiveHeight = sliderHeight - thumbSize;
                        var adjustedY = position.Y - thumbSize / 2;
                        relativePosition = Math.Max(0, Math.Min(1, adjustedY / effectiveHeight));
                    }
                }

                var newValue = slider.Minimum + relativePosition * (slider.Maximum - slider.Minimum);

                if (slider.IsSnapToTickEnabled && slider.TickFrequency > 0)
                {
                    var tickCount = (int)((slider.Maximum - slider.Minimum) / slider.TickFrequency);
                    var tickIndex = (int)Math.Round(relativePosition * tickCount);
                    newValue = slider.Minimum + tickIndex * slider.TickFrequency;
                }

                slider.Value = Math.Max(slider.Minimum, Math.Min(slider.Maximum, newValue));
            }
            catch { }
        }
    }
}
