using System;
using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas.Windows.SettingsViews.Helpers
{
    /// <summary>
    /// 让 WrapPanel 根据容器宽度自动计算 ItemWidth，使元素均匀撑满。
    /// 用法：WrapPanelAutoResize.TargetWidth="225"
    /// </summary>
    public static class WrapPanelAutoResize
    {
        public static readonly DependencyProperty TargetWidthProperty =
            DependencyProperty.RegisterAttached(
                "TargetWidth",
                typeof(double),
                typeof(WrapPanelAutoResize),
                new PropertyMetadata(0d, OnTargetWidthChanged));

        public static double GetTargetWidth(DependencyObject obj) => (double)obj.GetValue(TargetWidthProperty);
        public static void SetTargetWidth(DependencyObject obj, double value) => obj.SetValue(TargetWidthProperty, value);

        private static void OnTargetWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WrapPanel panel)
            {
                panel.SizeChanged -= PanelOnSizeChanged;
                if ((double)e.NewValue > 0)
                {
                    panel.SizeChanged += PanelOnSizeChanged;
                    UpdateItemWidth(panel);
                }
            }
        }

        private static void PanelOnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is WrapPanel panel)
                UpdateItemWidth(panel);
        }

        private static void UpdateItemWidth(WrapPanel panel)
        {
            var targetWidth = GetTargetWidth(panel);
            if (targetWidth <= 0) return;

            var availableWidth = panel.ActualWidth;
            if (availableWidth <= 0) availableWidth = panel.RenderSize.Width;
            if (availableWidth <= 0) return;

            var cols = Math.Round(availableWidth / Math.Max(1, targetWidth));
            if (cols < 1) cols = 1;

            panel.ItemWidth = availableWidth / cols;
        }
    }
}
