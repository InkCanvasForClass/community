using Ink_Canvas.Helpers;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace Ink_Canvas
{
    public partial class MainWindow
    {
        /// <summary>
        /// 媒体穿透区域的触摸扩展边距（像素），方便手指操作。
        /// </summary>
        private const double MediaTouchMargin = 15;

        /// <summary>
        /// 媒体区域在 WPF 窗口坐标系下的矩形列表。
        /// </summary>
        private List<Rect> _mediaPassthroughRects = new List<Rect>();

        /// <summary>
        /// 是否因进入媒体区域而切换到了鼠标模式。
        /// </summary>
        private bool _isMediaRegionMouseMode;

        /// <summary>
        /// 媒体穿透检测定时器（切换到鼠标模式后 OnMouseMove 不再触发，需要定时轮询鼠标位置）。
        /// </summary>
        private System.Windows.Threading.DispatcherTimer _mediaPassthroughTimer;

        #region 媒体区域坐标转换

        /// <summary>
        /// 从 VSTO 获取的原始磅值 + 窗口句柄，通过 Win32 API 计算屏幕像素坐标，
        /// 再转换为 WPF 窗口坐标。必须在 UI 线程调用。
        /// </summary>
        internal void BuildSmartModeRects()
        {
            _mediaPassthroughRects.Clear();

            if (_smartModeRegions == null || _smartModeRegions.Count == 0)
            {
                LogHelper.WriteLogToFile("[SmartMode] BuildRects: 无区域数据", LogHelper.LogType.Info);
                return;
            }

            // 如果 VSTO 未返回 slide 尺寸，尝试多种回退
            if (_smartModeSlideWidth <= 0 || _smartModeSlideHeight <= 0)
            {
                // 回退1：通过 COM 对象
                try
                {
                    var pres = pptApplication?.ActivePresentation;
                    if (pres != null)
                    {
                        _smartModeSlideWidth = pres.PageSetup.SlideWidth;
                        _smartModeSlideHeight = pres.PageSetup.SlideHeight;
                        LogHelper.WriteLogToFile($"[SmartMode] COM 回退: Slide={_smartModeSlideWidth}x{_smartModeSlideHeight}磅", LogHelper.LogType.Info);
                    }
                }
                catch { }

                // 回退2：通过 _pptManager
                if (_smartModeSlideWidth <= 0)
                {
                    try
                    {
                        var pres = _pptManager?.GetCurrentActivePresentation() as Microsoft.Office.Interop.PowerPoint.Presentation;
                        if (pres != null)
                        {
                            _smartModeSlideWidth = pres.PageSetup.SlideWidth;
                            _smartModeSlideHeight = pres.PageSetup.SlideHeight;
                            LogHelper.WriteLogToFile($"[SmartMode] PPTManager 回退: Slide={_smartModeSlideWidth}x{_smartModeSlideHeight}磅", LogHelper.LogType.Info);
                        }
                    }
                    catch { }
                }

                // 回退3：使用标准 16:9 尺寸
                if (_smartModeSlideWidth <= 0)
                {
                    _smartModeSlideWidth = 750;
                    _smartModeSlideHeight = 421.875f;
                    LogHelper.WriteLogToFile($"[SmartMode] 使用默认 16:9: Slide={_smartModeSlideWidth}x{_smartModeSlideHeight}磅", LogHelper.LogType.Info);
                }
            }

            if (_smartModeSlideShowHwnd == IntPtr.Zero)
            {
                _smartModeSlideShowHwnd = FindWindow(PowerPointSlideShowWindowClassName, null);
                LogHelper.WriteLogToFile($"[SmartMode] FindWindow 回退获取 Hwnd=0x{_smartModeSlideShowHwnd.ToInt64():X}", LogHelper.LogType.Info);
            }

            if (_smartModeSlideShowHwnd == IntPtr.Zero || _smartModeSlideWidth <= 0 || _smartModeSlideHeight <= 0)
            {
                LogHelper.WriteLogToFile($"[SmartMode] BuildRects: 参数仍无效 Hwnd=0x{_smartModeSlideShowHwnd.ToInt64():X} Slide={_smartModeSlideWidth}x{_smartModeSlideHeight}", LogHelper.LogType.Info);
                return;
            }

            // 通过 Win32 获取放映窗口的物理像素坐标
            if (!GetWindowRect(_smartModeSlideShowHwnd, out RECT winRect))
            {
                LogHelper.WriteLogToFile("[SmartMode] BuildRects: GetWindowRect 失败", LogHelper.LogType.Warning);
                return;
            }

            uint dpi = 96;
            try { dpi = GetDpiForWindow(_smartModeSlideShowHwnd); } catch { }
            double dpiScale = dpi / 96.0;

            double winLeft = winRect.Left;
            double winTop = winRect.Top;
            double winWidthPx = winRect.Right - winRect.Left;
            double winHeightPx = winRect.Bottom - winRect.Top;

            // 将像素尺寸转为磅，与 slideWidth/slideHeight（磅）计算比例
            double winWidthPt = winWidthPx / dpiScale;
            double winHeightPt = winHeightPx / dpiScale;
            double scale = Math.Min(winWidthPt / _smartModeSlideWidth, winHeightPt / _smartModeSlideHeight);
            double offsetX = (winWidthPt - _smartModeSlideWidth * scale) / 2;
            double offsetY = (winHeightPt - _smartModeSlideHeight * scale) / 2;

            LogHelper.WriteLogToFile($"[SmartMode] BuildRects: WinRect=({winLeft},{winTop}) {winWidthPx}x{winHeightPx}px DPI={dpi} Scale={scale:F4}", LogHelper.LogType.Info);

            foreach (var region in _smartModeRegions)
            {
                try
                {
                    // shape 磅 → 窗口内磅偏移 → 乘 dpiScale → 屏幕像素
                    double screenX = winLeft + (offsetX + region.X * scale) * dpiScale;
                    double screenY = winTop + (offsetY + region.Y * scale) * dpiScale;
                    double screenW = region.Width * scale * dpiScale;
                    double screenH = region.Height * scale * dpiScale;

                    // 屏幕像素 → WPF 窗口坐标
                    var wpfTopLeft = PointFromScreen(new Point(screenX, screenY));
                    var wpfBottomRight = PointFromScreen(new Point(screenX + screenW, screenY + screenH));
                    var rect = new Rect(wpfTopLeft, wpfBottomRight);
                    rect.Inflate(MediaTouchMargin, MediaTouchMargin);
                    _mediaPassthroughRects.Add(rect);

                    LogHelper.WriteLogToFile($"  区域: 磅({region.X:F0},{region.Y:F0}) → 屏幕({screenX:F0},{screenY:F0}) {screenW:F0}x{screenH:F0}px → WPF({rect.X:F0},{rect.Y:F0}) {rect.Width:F0}x{rect.Height:F0}", LogHelper.LogType.Info);
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"  坐标转换失败: {ex.Message}", LogHelper.LogType.Warning);
                }
            }
        }

        #endregion

        #region 鼠标跟踪：进入媒体区域切换鼠标模式，离开恢复批注模式

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (!Settings.PowerPointSettings.EnableSmartMode || _mediaPassthroughRects.Count == 0)
            {
                if (_isMediaRegionMouseMode)
                {
                    StopMediaPassthroughTimer();
                    LogHelper.WriteLogToFile("[SmartMode] 离开媒体区域，恢复批注模式", LogHelper.LogType.Info);
                    PenIcon_Click(null, null);
                    _isMediaRegionMouseMode = false;
                }
                return;
            }

            var pos = e.GetPosition(this);
            bool inRegion = IsPointInMediaRect(pos);

            if (inRegion && !_isMediaRegionMouseMode)
            {
                LogHelper.WriteLogToFile($"[SmartMode] 进入媒体区域 ({pos.X:F0},{pos.Y:F0})，切换鼠标模式", LogHelper.LogType.Info);
                CursorIcon_Click(null, null);
                _isMediaRegionMouseMode = true;
                StartMediaPassthroughTimer();
            }
            else if (!inRegion && _isMediaRegionMouseMode)
            {
                StopMediaPassthroughTimer();
                LogHelper.WriteLogToFile($"[SmartMode] 离开媒体区域 ({pos.X:F0},{pos.Y:F0})，恢复批注模式", LogHelper.LogType.Info);
                PenIcon_Click(null, null);
                _isMediaRegionMouseMode = false;
            }
        }

        /// <summary>
        /// 切换到鼠标模式后，OnMouseMove 不再触发（窗口穿透）。
        /// 用定时器轮询鼠标位置，检测离开媒体区域后恢复批注模式。
        /// </summary>
        private void StartMediaPassthroughTimer()
        {
            if (_mediaPassthroughTimer != null) return;
            _mediaPassthroughTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _mediaPassthroughTimer.Tick += (s, e) =>
            {
                if (!_isMediaRegionMouseMode || _mediaPassthroughRects.Count == 0)
                {
                    StopMediaPassthroughTimer();
                    return;
                }

                // 获取鼠标在窗口内的位置
                var mousePos = System.Windows.Forms.Cursor.Position;
                Point windowPoint;
                try { windowPoint = PointFromScreen(new Point(mousePos.X, mousePos.Y)); }
                catch { return; }

                if (!IsPointInMediaRect(windowPoint))
                {
                    StopMediaPassthroughTimer();
                    LogHelper.WriteLogToFile($"[SmartMode] 定时器: 鼠标离开媒体区域 ({windowPoint.X:F0},{windowPoint.Y:F0})，恢复批注模式", LogHelper.LogType.Info);
                    PenIcon_Click(null, null);
                    _isMediaRegionMouseMode = false;
                }
            };
            _mediaPassthroughTimer.Start();
        }

        private void StopMediaPassthroughTimer()
        {
            _mediaPassthroughTimer?.Stop();
            _mediaPassthroughTimer = null;
        }

        private bool IsPointInMediaRect(Point windowPoint)
        {
            foreach (var rect in _mediaPassthroughRects)
            {
                if (rect.Contains(windowPoint))
                    return true;
            }
            return false;
        }

        #endregion

        protected override bool ShouldHandleWindowChromeHitTest(Point windowPoint)
        {
            return ContainsPoint(ViewboxFloatingBar, windowPoint)
                   || ContainsPoint(LeftSidePanel, windowPoint)
                   || ContainsPoint(RightSidePanel, windowPoint)
                   || ContainsPoint(LeftUnFoldButtonQuickPanel, windowPoint)
                   || ContainsPoint(RightUnFoldButtonQuickPanel, windowPoint)
                   || ContainsPoint(LeftBottomPanelForPPTNavigation, windowPoint)
                   || ContainsPoint(RightBottomPanelForPPTNavigation, windowPoint)
                   || ContainsPoint(LeftSidePanelForPPTNavigation, windowPoint)
                   || ContainsPoint(RightSidePanelForPPTNavigation, windowPoint)
                   || ContainsPoint(ViewboxBlackboardLeftSide, windowPoint)
                   || ContainsPoint(BlackboardCenterSide, windowPoint)
                   || ContainsPoint(ViewboxBlackboardRightSide, windowPoint)
                   || ContainsPoint(BorderStrokeSelectionControl, windowPoint)
                   || ContainsPoint(BorderImageSelectionControl, windowPoint)
                   || ContainsPoint(BorderPdfPageSidebar, windowPoint)
                   || ContainsPoint(ImageSelectionOverlay, windowPoint)
                   || ContainsPoint(QuickDrawFloatingButton, windowPoint)
                   || ContainsPoint(BorderInkReplayToolBox, windowPoint)
                   || ContainsPoint(PPTTimeCapsuleContainer, windowPoint)
                   || ContainsPoint(PPTQuickPanelContainer, windowPoint)
                   || ContainsPoint(VideoPresenterSidebar, windowPoint);
        }

        private bool ContainsPoint(FrameworkElement element, Point windowPoint)
        {
            if (element == null || !element.IsVisible || !element.IsHitTestVisible)
                return false;

            if (element.ActualWidth <= 0 || element.ActualHeight <= 0)
                return false;

            try
            {
                var topLeft = element.TransformToAncestor(this).Transform(new Point(0, 0));
                var bounds = new Rect(topLeft, new Size(element.ActualWidth, element.ActualHeight));
                return bounds.Contains(windowPoint);
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }
}
