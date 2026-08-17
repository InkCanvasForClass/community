using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace Ink_Canvas.Ink.WetInk
{
    /// <summary>
    /// chrome 排除矩形收集（**屏幕 DIP** 坐标）。
    ///
    /// 实测教训：
    /// 1. 浮动栏/工具栏是动态构建的（FloatingToolbar/BoardToolbar），必须遍历视觉树收集。
    /// 2. 必须**排除全屏容器**（Main_Grid / GridBackgroundCoverHolder 等），否则整个画布被
    ///    排除，InkPresenter 收不到输入 → 写不了字。
    /// 3. BlackboardUIGridForInkReplay / FloatingbarUIForInkReplay 只是“容器”名字，实际装着
    ///    白板工具栏和浮动栏，**不能整棵跳过**；只跳过真正覆盖全屏的画布容器。
    /// 3. PointToScreen / TranslatePoint 均返回 DIP，**不能再乘 dpiScale**（否则坐标放大漂移）。
    ///    覆盖窗口 NCHITTEST 的 lParam 是物理像素，除 dpiScale 后即为屏幕 DIP。
    /// </summary>
    internal sealed class WetInkRouter
    {
        /// <summary>矩形面积超过窗口面积此比例即视为容器，不排除。</summary>
        private const double MaxChromeAreaRatio = 0.5;

        private static readonly HashSet<string> SkipSubtreeNames = new HashSet<string>(
            new[]
            {
                "GridBackgroundCoverHolder",
                "InkCanvasGridForInkReplay"
            },
            StringComparer.Ordinal);

        // 这些全屏覆盖层是“交互层”而不是画布，可见时必须整体排除，避免拖拽/选择命中被覆盖窗口吃掉。
        private static readonly HashSet<string> AlwaysChromeNames = new HashSet<string>(
            new[]
            {
                "GridForFloatingBarDraging",
                "GridInkCanvasSelectionCover"
            },
            StringComparer.Ordinal);

        /// <summary>
        /// 遍历主窗口视觉树收集 chrome 排除矩形（屏幕 DIP）。
        /// skipRoots = 画布容器（其子树全部跳过）；additionalRectsDip 通常来自可见 Popup/其它窗口。
        /// </summary>
        public List<Rect> BuildAllChromeRects(
            Window mainWindow,
            Point screenOriginDip,
            IReadOnlyList<DependencyObject> skipRoots,
            IReadOnlyList<Rect> additionalRectsDip = null)
        {
            var rects = new List<Rect>();
            if (mainWindow == null) return rects;

            var windowW = mainWindow.ActualWidth;
            var windowH = mainWindow.ActualHeight;
            var maxArea = windowW * windowH * MaxChromeAreaRatio;

            var skip = new HashSet<DependencyObject>();
            if (skipRoots != null)
            {
                foreach (var root in skipRoots)
                {
                    if (root != null) skip.Add(root);
                }
            }

            CollectChrome(mainWindow, mainWindow, screenOriginDip, skip,
                windowW, windowH, maxArea, rects);

            if (additionalRectsDip != null)
            {
                foreach (var rect in additionalRectsDip)
                {
                    if (rect.Width > 0 && rect.Height > 0 && IsWithinWindow(rect, screenOriginDip, windowW, windowH))
                        rects.Add(rect);
                }
            }
            return rects;
        }

        private void CollectChrome(
            DependencyObject parent, Window mainWindow, Point screenOriginDip,
            HashSet<DependencyObject> skipRoots, double windowW, double windowH, double maxArea, List<Rect> rects)
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (skipRoots.Contains(child)) continue; // 跳过画布容器子树

                var fe = child as FrameworkElement;
                if (fe != null && ShouldSkipByName(fe.Name)) continue;

                if (fe != null && fe.Visibility == Visibility.Visible && fe.IsHitTestVisible
                    && fe.ActualWidth > 0 && fe.ActualHeight > 0)
                {
                    try
                    {
                        // TransformBounds 返回 DIP，且正确处理 Viewbox/缩放；再叠加窗口屏幕原点。
                        var local = fe.TransformToAncestor(mainWindow)
                                      .TransformBounds(new Rect(0, 0, fe.ActualWidth, fe.ActualHeight));
                        var x = screenOriginDip.X + local.X;
                        var y = screenOriginDip.Y + local.Y;
                        var w = local.Width;
                        var h = local.Height;

                        // 只收「真 chrome」：必须在窗口范围内，且面积不能接近整窗（那是容器）。
                        var rect = new Rect(x, y, w, h);
                        var inWindow = IsWithinWindow(rect, screenOriginDip, windowW, windowH);
                        var isContainer = w * h > maxArea
                            && !IsAlwaysChromeName(fe.Name);

                        if (w > 0 && h > 0 && inWindow && !isContainer)
                            rects.Add(rect);
                    }
                    catch
                    {
                        // 元素尚未参与布局，跳过。
                    }
                }

                CollectChrome(child, mainWindow, screenOriginDip, skipRoots,
                    windowW, windowH, maxArea, rects);
            }
        }

        private static bool ShouldSkipByName(string name)
        {
            return ShouldSkipChromeName(name);
        }

        /// <summary>画布容器名判定（全屏覆盖容器不能当 chrome 排除）。</summary>
        internal static bool ShouldSkipChromeName(string name)
        {
            return !string.IsNullOrEmpty(name) && SkipSubtreeNames.Contains(name);
        }

        /// <summary>交互层名判定（必须整体排除，避免覆盖窗口吃掉拖拽/选择命中）。</summary>
        internal static bool IsAlwaysChromeName(string name)
        {
            return !string.IsNullOrEmpty(name) && AlwaysChromeNames.Contains(name);
        }

        private static bool IsWithinWindow(Rect rect, Point screenOriginDip, double windowW, double windowH)
        {
            return rect.Right > screenOriginDip.X - 1
                && rect.Bottom > screenOriginDip.Y - 1
                && rect.X < screenOriginDip.X + windowW + 1
                && rect.Y < screenOriginDip.Y + windowH + 1;
        }
    }
}
