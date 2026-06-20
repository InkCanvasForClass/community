using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using GongDragDrop = GongSolutions.Wpf.DragDrop.DragDrop;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 基于 ClassIsland 2.0 AVA 拖动思路的触屏感知拖拽辅助类。
    /// <para>参考 ClassIsland 2.0 的 PointerStateAssist + TouchDragThumb + AdvancedItemDragBehavior 架构：</para>
    /// <para>- 窗口/控件级检测输入设备类型（鼠标/触屏）</para>
    /// <para>- 触屏模式下显示拖动按钮（grip handle），鼠标模式下隐藏</para>
    /// <para>- 触屏模式下只有从 grip handle 发起的按下才能触发拖动，否则事件交给 ScrollViewer 处理滑动</para>
    /// <para>用法：</para>
    /// <para>1. 在 ItemsControl 上设置 touch:TouchAwareDragDropHelper.IsEnabled="True"</para>
    /// <para>2. 在 ItemTemplate 中的拖动图标上设置 touch:TouchAwareDragDropHelper.IsGripHandle="True"</para>
    /// </summary>
    public static class TouchAwareDragDropHelper
    {
        #region IsEnabled 附加属性

        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled",
                typeof(bool),
                typeof(TouchAwareDragDropHelper),
                new PropertyMetadata(false, OnIsEnabledChanged));

        public static bool GetIsEnabled(DependencyObject obj)
            => (bool)obj.GetValue(IsEnabledProperty);

        public static void SetIsEnabled(DependencyObject obj, bool value)
            => obj.SetValue(IsEnabledProperty, value);

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is ItemsControl itemsControl)) return;

            if ((bool)e.NewValue)
            {
                SubscribeItemsControlEvents(itemsControl);
            }
            else
            {
                UnsubscribeItemsControlEvents(itemsControl);
            }
        }

        private static void SubscribeItemsControlEvents(ItemsControl itemsControl)
        {
            itemsControl.PreviewStylusDown += ItemsControl_PreviewStylusDown;
            itemsControl.PreviewStylusUp += ItemsControl_PreviewStylusUp;
            itemsControl.PreviewMouseLeftButtonDown += ItemsControl_PreviewMouseLeftButtonDown;
            itemsControl.ItemContainerGenerator.StatusChanged += (s, args) =>
            {
                if (itemsControl.ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
                {
                    UpdateGripHandleVisualState(itemsControl, GetIsTouchMode(itemsControl));
                }
            };
            itemsControl.Unloaded += ItemsControl_Unloaded;
        }

        private static void UnsubscribeItemsControlEvents(ItemsControl itemsControl)
        {
            itemsControl.PreviewStylusDown -= ItemsControl_PreviewStylusDown;
            itemsControl.PreviewStylusUp -= ItemsControl_PreviewStylusUp;
            itemsControl.PreviewMouseLeftButtonDown -= ItemsControl_PreviewMouseLeftButtonDown;
            itemsControl.Unloaded -= ItemsControl_Unloaded;
        }

        private static void ItemsControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (sender is ItemsControl itemsControl)
            {
                UnsubscribeItemsControlEvents(itemsControl);
            }
        }

        #endregion

        #region IsGripHandle 附加属性

        public static readonly DependencyProperty IsGripHandleProperty =
            DependencyProperty.RegisterAttached(
                "IsGripHandle",
                typeof(bool),
                typeof(TouchAwareDragDropHelper),
                new PropertyMetadata(false, OnIsGripHandleChanged));

        public static bool GetIsGripHandle(DependencyObject obj)
            => (bool)obj.GetValue(IsGripHandleProperty);

        public static void SetIsGripHandle(DependencyObject obj, bool value)
            => obj.SetValue(IsGripHandleProperty, value);

        private static void OnIsGripHandleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is FrameworkElement element)) return;

            if ((bool)e.NewValue)
            {
                element.PreviewStylusDown += GripHandle_PreviewStylusDown;
                element.PreviewStylusUp += GripHandle_PreviewStylusUp;
                // 默认鼠标模式，不参与 hit testing（鼠标事件穿透到 ListBoxItem，gong-wpf-dragdrop 正常处理）
                element.IsHitTestVisible = false;
                element.Loaded += GripHandle_Loaded;
            }
            else
            {
                element.PreviewStylusDown -= GripHandle_PreviewStylusDown;
                element.PreviewStylusUp -= GripHandle_PreviewStylusUp;
                element.IsHitTestVisible = true;
                element.Loaded -= GripHandle_Loaded;
            }
        }

        private static void GripHandle_Loaded(object sender, RoutedEventArgs e)
        {
            if (!(sender is FrameworkElement element)) return;
            var itemsControl = FindParent<ItemsControl>(element);
            if (itemsControl != null)
            {
                bool isTouchMode = GetIsTouchMode(itemsControl);
                element.IsHitTestVisible = isTouchMode;
                UpdateGripHandleElementVisualState(element, isTouchMode);
            }
        }

        #endregion

        #region IsTouchMode 附加属性（只读，可继承模拟）

        private static readonly DependencyPropertyKey IsTouchModePropertyKey =
            DependencyProperty.RegisterAttachedReadOnly(
                "IsTouchMode",
                typeof(bool),
                typeof(TouchAwareDragDropHelper),
                new PropertyMetadata(false));

        public static readonly DependencyProperty IsTouchModeProperty =
            IsTouchModePropertyKey.DependencyProperty;

        public static bool GetIsTouchMode(DependencyObject obj)
            => (bool)obj.GetValue(IsTouchModeProperty);

        private static void SetIsTouchMode(DependencyObject obj, bool value)
            => obj.SetValue(IsTouchModePropertyKey, value);

        #endregion

        #region 私有状态属性

        // 保存 ScrollViewer 原始的 IsManipulationEnabled 值，用于恢复
        private static readonly DependencyProperty OriginalIsManipulationEnabledProperty =
            DependencyProperty.RegisterAttached(
                "OriginalIsManipulationEnabled",
                typeof(bool?),
                typeof(TouchAwareDragDropHelper),
                new PropertyMetadata(null));

        private static bool? GetOriginalIsManipulationEnabled(DependencyObject obj)
            => (bool?)obj.GetValue(OriginalIsManipulationEnabledProperty);

        private static void SetOriginalIsManipulationEnabled(DependencyObject obj, bool? value)
            => obj.SetValue(OriginalIsManipulationEnabledProperty, value);

        // 保存 ItemsControl 原始的 IsDragSource 值，用于恢复
        private static readonly DependencyProperty OriginalIsDragSourceProperty =
            DependencyProperty.RegisterAttached(
                "OriginalIsDragSource",
                typeof(bool?),
                typeof(TouchAwareDragDropHelper),
                new PropertyMetadata(null));

        private static bool? GetOriginalIsDragSource(DependencyObject obj)
            => (bool?)obj.GetValue(OriginalIsDragSourceProperty);

        private static void SetOriginalIsDragSource(DependencyObject obj, bool? value)
            => obj.SetValue(OriginalIsDragSourceProperty, value);

        #endregion

        #region 输入设备检测与模式切换

        /// <summary>
        /// 触屏按下时：切换到触屏模式，禁用 IsDragSource（阻止非 grip handle 区域触发拖动）。
        /// 这对应 ClassIsland 2.0 AdvancedItemDragBehavior.PointerPressed 中的判定：
        /// 触屏模式下如果不是从 TouchDragThumb 发起则 return。
        /// </summary>
        private static void ItemsControl_PreviewStylusDown(object sender, StylusEventArgs e)
        {
            if (!(sender is ItemsControl itemsControl)) return;

            if (!GetIsTouchMode(itemsControl))
            {
                SetIsTouchMode(itemsControl, true);
                if (!GetOriginalIsDragSource(itemsControl).HasValue)
                {
                    SetOriginalIsDragSource(itemsControl, GongDragDrop.GetIsDragSource(itemsControl));
                }
                UpdateGripHandleVisualState(itemsControl, true);
            }

            // 触屏模式下禁用 IsDragSource，只有 grip handle 按下时才临时启用
            GongDragDrop.SetIsDragSource(itemsControl, false);
        }

        private static void ItemsControl_PreviewStylusUp(object sender, StylusEventArgs e)
        {
            if (!(sender is ItemsControl itemsControl)) return;
            if (GetIsTouchMode(itemsControl))
            {
                GongDragDrop.SetIsDragSource(itemsControl, false);
            }
        }

        /// <summary>
        /// 纯鼠标输入时：切换回鼠标模式，恢复 IsDragSource。
        /// </summary>
        private static void ItemsControl_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!(sender is ItemsControl itemsControl)) return;
            if (e.StylusDevice == null && GetIsTouchMode(itemsControl))
            {
                SetIsTouchMode(itemsControl, false);
                UpdateGripHandleVisualState(itemsControl, false);
                var original = GetOriginalIsDragSource(itemsControl);
                GongDragDrop.SetIsDragSource(itemsControl, original ?? true);
            }
        }

        #endregion

        #region Grip Handle 视觉状态更新

        private static void UpdateGripHandleVisualState(ItemsControl itemsControl, bool isTouchMode)
        {
            for (int i = 0; i < itemsControl.Items.Count; i++)
            {
                var container = itemsControl.ItemContainerGenerator.ContainerFromIndex(i);
                if (container != null)
                {
                    UpdateGripHandleVisualStateInContainer(container, isTouchMode);
                }
            }
        }

        private static void UpdateGripHandleVisualStateInContainer(DependencyObject root, bool isTouchMode)
        {
            var queue = new Queue<DependencyObject>();
            queue.Enqueue(root);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current == null) continue;

                if (GetIsGripHandle(current) && current is UIElement element)
                {
                    element.IsHitTestVisible = isTouchMode;
                    UpdateGripHandleElementVisualState(element, isTouchMode);
                }

                if (current is Visual)
                {
                    int childCount = VisualTreeHelper.GetChildrenCount(current);
                    for (int i = 0; i < childCount; i++)
                    {
                        queue.Enqueue(VisualTreeHelper.GetChild(current, i));
                    }
                }
            }
        }

        /// <summary>
        /// 触屏模式下给 grip handle 添加半透明背景，让用户看到拖动按钮。
        /// 对应 ClassIsland 2.0 TouchDragThumb 在触屏模式下 Opacity=1。
        /// </summary>
        private static void UpdateGripHandleElementVisualState(UIElement element, bool isTouchMode)
        {
            if (element is Border border)
            {
                if (isTouchMode)
                {
                    border.Background = new SolidColorBrush(Color.FromArgb(0x33, 0x25, 0x63, 0xEB));
                    border.BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, 0x25, 0x63, 0xEB));
                    border.BorderThickness = new Thickness(1);
                }
                else
                {
                    border.Background = Brushes.Transparent;
                    border.BorderBrush = Brushes.Transparent;
                    border.BorderThickness = new Thickness(0);
                }
            }
        }

        #endregion

        #region Grip Handle 事件处理

        /// <summary>
        /// 按住 grip handle 时：
        /// 1. 临时启用 IsDragSource，让即将提升的 MouseDown 触发 gong-wpf-dragdrop 拖动
        /// 2. 禁用父级 ScrollViewer 的 IsManipulationEnabled，防止触屏移动被处理为 panning
        /// 对应 ClassIsland 2.0 中 TouchDragThumb（继承自 Thumb）按下时捕获指针的行为。
        /// </summary>
        private static void GripHandle_PreviewStylusDown(object sender, StylusEventArgs e)
        {
            if (!(sender is FrameworkElement gripHandle)) return;

            var itemsControl = FindParent<ItemsControl>(gripHandle);
            if (itemsControl != null)
            {
                var original = GetOriginalIsDragSource(itemsControl) ?? true;
                GongDragDrop.SetIsDragSource(itemsControl, original);
            }

            var scrollViewer = FindParent<ScrollViewer>(gripHandle);
            if (scrollViewer != null)
            {
                SetOriginalIsManipulationEnabled(scrollViewer, scrollViewer.IsManipulationEnabled);
                scrollViewer.IsManipulationEnabled = false;
            }

            gripHandle.CaptureStylus();
        }

        /// <summary>
        /// 释放 grip handle 时恢复状态。
        /// </summary>
        private static void GripHandle_PreviewStylusUp(object sender, StylusEventArgs e)
        {
            if (!(sender is FrameworkElement gripHandle)) return;

            var scrollViewer = FindParent<ScrollViewer>(gripHandle);
            if (scrollViewer != null)
            {
                var original = GetOriginalIsManipulationEnabled(scrollViewer);
                if (original.HasValue)
                {
                    scrollViewer.IsManipulationEnabled = original.Value;
                    SetOriginalIsManipulationEnabled(scrollViewer, null);
                }
            }

            var itemsControl = FindParent<ItemsControl>(gripHandle);
            if (itemsControl != null && GetIsTouchMode(itemsControl))
            {
                GongDragDrop.SetIsDragSource(itemsControl, false);
            }

            gripHandle.ReleaseStylusCapture();
        }

        #endregion

        #region 辅助方法

        private static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parent = VisualTreeHelper.GetParent(child);
            while (parent != null && !(parent is T))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }
            return parent as T;
        }

        #endregion
    }
}
