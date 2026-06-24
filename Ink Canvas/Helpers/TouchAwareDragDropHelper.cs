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
    /// <para>- 一旦检测到触屏输入，grip handle 将持续显示直到应用重启（不因鼠标输入而恢复隐藏）</para>
    /// <para>用法：</para>
    /// <para>1. 在 ItemsControl 上设置 touch:TouchAwareDragDropHelper.IsEnabled="True"</para>
    /// <para>2. 在 ItemTemplate 中的拖动图标上设置 touch:TouchAwareDragDropHelper.IsGripHandle="True"</para>
    /// </summary>
    public static class TouchAwareDragDropHelper
    {
        // 全局触屏模式标记：一旦检测到触屏输入，所有注册的 ItemsControl 都进入触屏模式且不恢复
        private static bool _globalTouchModeActivated;

        // 跟踪所有已注册的 ItemsControl
        private static readonly List<WeakReference<ItemsControl>> _registeredControls = new();
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
            itemsControl.ItemContainerGenerator.StatusChanged += (s, args) =>
            {
                if (itemsControl.ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
                {
                    UpdateGripHandleVisualState(itemsControl, GetIsTouchMode(itemsControl));
                }
            };
            itemsControl.Unloaded += ItemsControl_Unloaded;

            // 注册到全局列表
            _registeredControls.Add(new WeakReference<ItemsControl>(itemsControl));

            // 如果全局触屏模式已激活，立即显示 grip handle
            if (_globalTouchModeActivated)
            {
                SetIsTouchMode(itemsControl, true);
                UpdateGripHandleVisualState(itemsControl, true);
            }

            // 订阅窗口级 StylusDown 事件，实现"点击窗口任意位置即显示"
            itemsControl.Loaded += ItemsControl_Loaded;
        }

        private static void UnsubscribeItemsControlEvents(ItemsControl itemsControl)
        {
            itemsControl.PreviewStylusDown -= ItemsControl_PreviewStylusDown;
            itemsControl.PreviewStylusUp -= ItemsControl_PreviewStylusUp;
            itemsControl.Unloaded -= ItemsControl_Unloaded;
            itemsControl.Loaded -= ItemsControl_Loaded;

            // 从全局列表移除
            _registeredControls.RemoveAll(wr => !wr.TryGetTarget(out var target) || target == itemsControl);
        }

        private static void ItemsControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (!(sender is ItemsControl itemsControl)) return;
            var window = Window.GetWindow(itemsControl);
            if (window != null)
            {
                window.PreviewStylusDown -= Window_PreviewStylusDown;
                window.PreviewStylusDown += Window_PreviewStylusDown;
            }
        }

        private static void ItemsControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (sender is ItemsControl itemsControl)
            {
                UnsubscribeItemsControlEvents(itemsControl);
            }
        }

        /// <summary>
        /// 窗口级触屏按下处理：一旦检测到触屏输入，激活所有已注册 ItemsControl 的触屏模式且不恢复。
        /// </summary>
        private static void Window_PreviewStylusDown(object sender, StylusEventArgs e)
        {
            if (_globalTouchModeActivated) return;

            _globalTouchModeActivated = true;

            // 激活所有已注册的 ItemsControl
            foreach (var wr in _registeredControls)
            {
                if (wr.TryGetTarget(out var itemsControl) && itemsControl != null)
                {
                    if (!GetIsTouchMode(itemsControl))
                    {
                        if (!GetOriginalIsDragSource(itemsControl).HasValue)
                        {
                            SetOriginalIsDragSource(itemsControl, GongDragDrop.GetIsDragSource(itemsControl));
                        }
                        SetIsTouchMode(itemsControl, true);
                        UpdateGripHandleVisualState(itemsControl, true);
                    }
                }
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

        /// <summary>
        /// 是否强制始终显示 grip handle（不依赖触摸模式）。
        /// 对应 ClassIsland 2.0 TouchDragThumb 的 IsExplicitVisible 属性。
        /// </summary>
        public static readonly DependencyProperty IsExplicitVisibleProperty =
            DependencyProperty.RegisterAttached(
                "IsExplicitVisible",
                typeof(bool),
                typeof(TouchAwareDragDropHelper),
                new PropertyMetadata(false, OnIsExplicitVisibleChanged));

        public static bool GetIsExplicitVisible(DependencyObject obj)
            => (bool)obj.GetValue(IsExplicitVisibleProperty);

        public static void SetIsExplicitVisible(DependencyObject obj, bool value)
            => obj.SetValue(IsExplicitVisibleProperty, value);

        private static void OnIsExplicitVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is FrameworkElement element)) return;
            if (!GetIsGripHandle(element)) return;
            // 重新计算可见性
            var itemsControl = FindParent<ItemsControl>(element);
            bool isTouchMode = itemsControl != null && GetIsTouchMode(itemsControl);
            UpdateGripHandleElementVisualState(element, isTouchMode);
        }

        private static void OnIsGripHandleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is FrameworkElement element)) return;

            if ((bool)e.NewValue)
            {
                element.PreviewStylusDown += GripHandle_PreviewStylusDown;
                element.PreviewStylusUp += GripHandle_PreviewStylusUp;
                // 默认鼠标模式：IsExplicitVisible=false 时不参与 hit testing 且隐藏
                bool explicitVisible = GetIsExplicitVisible(element);
                element.IsHitTestVisible = explicitVisible;
                element.Visibility = explicitVisible ? Visibility.Visible : Visibility.Collapsed;
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
                element.IsHitTestVisible = isTouchMode || GetIsExplicitVisible(element);
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
        /// 一旦激活触屏模式，不会因鼠标输入而恢复（重启软件可恢复）。
        /// </summary>
        private static void ItemsControl_PreviewStylusDown(object sender, StylusEventArgs e)
        {
            if (!(sender is ItemsControl itemsControl)) return;

            if (!GetIsTouchMode(itemsControl))
            {
                if (!GetOriginalIsDragSource(itemsControl).HasValue)
                {
                    SetOriginalIsDragSource(itemsControl, GongDragDrop.GetIsDragSource(itemsControl));
                }
                SetIsTouchMode(itemsControl, true);
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
                RestoreScrollViewerManipulation(itemsControl);
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
                    bool explicitVisible = GetIsExplicitVisible(element);
                    element.IsHitTestVisible = isTouchMode || explicitVisible;
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
        /// 触屏模式下显示 grip handle（Visibility=Visible），鼠标模式下隐藏（Visibility=Collapsed）。
        /// 但 IsExplicitVisible=True 时始终显示（对应 ClassIsland 2.0 TouchDragThumb 的 IsExplicitVisible）。
        /// </summary>
        private static void UpdateGripHandleElementVisualState(UIElement element, bool isTouchMode)
        {
            bool explicitVisible = GetIsExplicitVisible(element);
            element.Visibility = (isTouchMode || explicitVisible) ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion

        #region Grip Handle 事件处理

        /// <summary>
        /// 按住 grip handle 时：
        /// 1. 临时启用 IsDragSource，让即将提升的 MouseDown 触发 gong-wpf-dragdrop 拖动
        /// 2. 禁用所有父级 ScrollViewer 的 IsManipulationEnabled，防止触屏移动被处理为 panning
        /// 不捕获 Stylus，让触笔事件自然提升为鼠标事件，gong-wpf-dragdrop 才能正常启动拖拽。
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

            // 禁用所有父级 ScrollViewer（包括 ListView 内部的和外层包裹的）
            var parent = VisualTreeHelper.GetParent(gripHandle);
            while (parent != null)
            {
                if (parent is ScrollViewer sv)
                {
                    SetOriginalIsManipulationEnabled(sv, sv.IsManipulationEnabled);
                    sv.IsManipulationEnabled = false;
                }
                parent = VisualTreeHelper.GetParent(parent);
            }
        }

        /// <summary>
        /// 释放 grip handle 时恢复所有父级 ScrollViewer 状态。
        /// </summary>
        private static void GripHandle_PreviewStylusUp(object sender, StylusEventArgs e)
        {
            if (!(sender is FrameworkElement gripHandle)) return;

            var parent = VisualTreeHelper.GetParent(gripHandle);
            while (parent != null)
            {
                if (parent is ScrollViewer sv)
                {
                    var original = GetOriginalIsManipulationEnabled(sv);
                    if (original.HasValue)
                    {
                        sv.IsManipulationEnabled = original.Value;
                        SetOriginalIsManipulationEnabled(sv, null);
                    }
                }
                parent = VisualTreeHelper.GetParent(parent);
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 恢复 ItemsControl 及其父级中所有被禁用的 ScrollViewer 的 IsManipulationEnabled。
        /// 在没有 Stylus capture 的情况下，GripHandle_PreviewStylusUp 可能不会触发，
        /// 所以在 ItemsControl_PreviewStylusUp 中统一恢复。
        /// </summary>
        private static void RestoreScrollViewerManipulation(ItemsControl itemsControl)
        {
            // 检查 ItemsControl 的父级 ScrollViewer（外层 ScrollViewer）
            var parent = VisualTreeHelper.GetParent(itemsControl);
            while (parent != null)
            {
                if (parent is ScrollViewer sv)
                {
                    var original = GetOriginalIsManipulationEnabled(sv);
                    if (original.HasValue)
                    {
                        sv.IsManipulationEnabled = original.Value;
                        SetOriginalIsManipulationEnabled(sv, null);
                    }
                }
                parent = VisualTreeHelper.GetParent(parent);
            }

            // 检查 ItemsControl 内部的 ScrollViewer（ListView 模板内的）
            var queue = new Queue<DependencyObject>();
            queue.Enqueue(itemsControl);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current is ScrollViewer sv2)
                {
                    var original = GetOriginalIsManipulationEnabled(sv2);
                    if (original.HasValue)
                    {
                        sv2.IsManipulationEnabled = original.Value;
                        SetOriginalIsManipulationEnabled(sv2, null);
                    }
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
