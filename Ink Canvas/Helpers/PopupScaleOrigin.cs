using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 计算菜单缩放的基准点，让它"从触发图标长出来、缩回触发图标"。
    /// </summary>
    /// <remarks>
    /// 为什么需要专门算：
    /// <see cref="UIElement.RenderTransformOrigin"/> 是 0..1 的相对坐标，
    /// 而触发按钮通常在菜单**外面**（菜单弹在按钮上方）。直接把按钮中心换算进去
    /// 会得到负数或大于 1 的值。
    ///
    /// 但原点落在元素外会导致缩放时整体位移过大——之前正是这个原因造成"定位跑偏"，
    /// 并被 Popup 窗口边界裁切。所以这里做**夹取**：
    /// 把原点限制在 [0,1] 内，取的是"菜单靠近按钮的那一侧的目标位置"，
    /// 既保留"从按钮方向长出来"的方向感，又不会越界。
    ///
    /// 换算失败（触发器不在同一视觉树、菜单尚未布局）时退回中心点。
    /// </remarks>
    internal static class PopupScaleOrigin
    {
        /// <summary>允许的原点范围：留在元素内部，避免缩放越界被裁。</summary>
        private const double MinOrigin = 0.0;
        private const double MaxOrigin = 1.0;

        /// <summary>
        /// 取得菜单相对其触发按钮的缩放原点。
        /// </summary>
        /// <param name="popup">目标菜单。</param>
        /// <param name="menu">菜单内容（用于换算坐标系）。</param>
        /// <returns>0..1 范围内的相对原点；无法计算时返回中心 (0.5, 0.5)。</returns>
        public static Point Resolve(Popup popup, FrameworkElement menu)
        {
            var center = new Point(0.5, 0.5);

            if (popup?.PlacementTarget is not FrameworkElement target) return center;
            if (menu == null || !menu.IsLoaded) return center;

            double width = menu.ActualWidth;
            double height = menu.ActualHeight;
            if (width <= 0 || height <= 0) return center;

            try
            {
                // 按钮中心换算到菜单坐标系（可能为负：菜单在按钮上方，按钮在菜单之外）
                var targetCenter = target.TransformToAncestor(menu)
                    .Transform(new Point(target.ActualWidth / 2, target.ActualHeight / 2));
                if (double.IsNaN(targetCenter.X) || double.IsNaN(targetCenter.Y)) return center;

                double x = Clamp(targetCenter.X / width, MinOrigin, MaxOrigin);
                double y = Clamp(targetCenter.Y / height, MinOrigin, MaxOrigin);
                return new Point(x, y);
            }
            catch
            {
                // 不在同一视觉树 / 尚未完成布局
                return center;
            }
        }

        private static double Clamp(double value, double min, double max)
        {
            if (double.IsNaN(value)) return (min + max) / 2;
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
