using Ink_Canvas.Controls;
using Ink_Canvas.Helpers;
using Ink_Canvas.Properties;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Ink_Canvas
{
    public partial class MainWindow : Ink_Canvas.Helpers.PerformanceTransparentWin
    {
        internal static readonly Guid FrozenStrokePropertyGuid = new Guid("12345678-1234-1234-1234-123456789ABC");

        private static readonly TimeSpan DelayedFreezeDelay = TimeSpan.FromMinutes(3);

        private readonly bool[] frozenPages = new bool[101];
        private readonly DateTime[] pageLastUserInkMutationUtc = new DateTime[101];

        private DispatcherTimer delayedFreezeTimer;
        private int? delayedFreezePageIndex;
        private DateTime delayedFreezeReferenceUtc;
        private int? freezeCourseRecordedPageIndex;
        private DateTime freezeCourseRecordedUtc;
        private DateTime lastFreezeBlockNotificationUtc = DateTime.MinValue;

        private Ink_Canvas.Controls.BoardToolbarButton BoardInkFreezeBtn;

        private int GetCurrentFreezePageIndex()
            => currentMode == 0 ? 0 : CurrentWhiteboardIndex;

        private static bool IsValidFreezePageIndex(int pageIndex)
            => pageIndex >= 0 && pageIndex <= 100;

        private bool IsPageFrozen(int pageIndex)
            => IsValidFreezePageIndex(pageIndex) && frozenPages[pageIndex];

        private bool IsCurrentPageFrozen
            => IsPageFrozen(GetCurrentFreezePageIndex());

        private bool IsFreezeMutatingMode(InkCanvasEditingMode mode)
            => mode == InkCanvasEditingMode.Ink
               || mode == InkCanvasEditingMode.EraseByPoint
               || mode == InkCanvasEditingMode.EraseByStroke
               || mode == InkCanvasEditingMode.Select
               || drawingShapeMode != 0;

        private bool IsFreezeEditingToolModeName(string mode)
        {
            switch (mode)
            {
                case "pen":
                case "color":
                case "eraser":
                case "eraserByStrokes":
                case "select":
                case "shape":
                    return true;
                default:
                    return false;
            }
        }

        private string NormalizeToolModeForFreeze(string mode)
        {
            if (string.IsNullOrEmpty(mode)) return mode;
            return IsCurrentPageFrozen && IsFreezeEditingToolModeName(mode) ? "cursor" : mode;
        }

        private void MarkCurrentPageInkChanged()
            => MarkPageInkChanged(GetCurrentFreezePageIndex());

        private void MarkPageInkChanged(int pageIndex)
        {
            if (!IsValidFreezePageIndex(pageIndex)) return;
            pageLastUserInkMutationUtc[pageIndex] = DateTime.UtcNow;
        }

        private void ApplyFreezeStateToCurrentStrokes()
        {
            try
            {
                if (inkCanvas == null) return;

                bool isFrozen = IsCurrentPageFrozen;
                foreach (Stroke stroke in inkCanvas.Strokes)
                {
                    if (isFrozen)
                    {
                        if (!stroke.ContainsPropertyData(FrozenStrokePropertyGuid))
                            stroke.AddPropertyData(FrozenStrokePropertyGuid, true);
                    }
                    else if (stroke.ContainsPropertyData(FrozenStrokePropertyGuid))
                    {
                        stroke.RemovePropertyData(FrozenStrokePropertyGuid);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"应用笔迹冻结状态失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        private void FreezePage(int pageIndex, bool notify = true)
        {
            if (!IsValidFreezePageIndex(pageIndex)) return;
            if (frozenPages[pageIndex])
            {
                if (notify) ShowNotification("该页面已冻结");
                UpdateInkFreezeButtonState();
                return;
            }

            frozenPages[pageIndex] = true;
            if (pageIndex == GetCurrentFreezePageIndex())
            {
                ApplyFreezeStateToCurrentStrokes();
                EnsureCurrentFrozenEditingState();
            }

            UpdateInkFreezeButtonState();
            if (notify) ShowNotification(pageIndex == 0 ? "当前批注页已冻结" : $"白板第 {pageIndex} 页已冻结");
        }

        private async Task<bool> UnfreezePageAsync(int pageIndex, bool skipVerification = false, bool notify = true)
        {
            if (!IsValidFreezePageIndex(pageIndex)) return false;
            if (!frozenPages[pageIndex])
            {
                UpdateInkFreezeButtonState();
                return true;
            }

            if (!skipVerification)
            {
                bool ok = await SecurityManager.PromptAndVerifyPasswordOrTotpAsync(
                    Settings,
                    this,
                    "解冻验证",
                    "请输入安全密码或 TOTP 动态验证码以解冻当前页面。");
                if (!ok)
                {
                    if (notify) ShowNotification("解冻验证未通过");
                    return false;
                }
            }

            frozenPages[pageIndex] = false;
            if (pageIndex == GetCurrentFreezePageIndex())
                ApplyFreezeStateToCurrentStrokes();

            UpdateInkFreezeButtonState();
            if (notify) ShowNotification(pageIndex == 0 ? "当前批注页已解冻" : $"白板第 {pageIndex} 页已解冻");
            return true;
        }

        internal async void ToggleInkFreeze_MouseUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                int pageIndex = GetCurrentFreezePageIndex();
                if (IsPageFrozen(pageIndex))
                {
                    await UnfreezePageAsync(pageIndex);
                }
                else
                {
                    FreezePage(pageIndex);

                    // 直接调用 CursorIcon_Click 来确保完整切换到鼠标模式
                    CursorIcon_Click(null, null);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"浮动栏冻结按钮点击失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        internal void AttachInkFreezeBtn(ToolbarImageButton btn)
        {
            Freeze_Icon = btn;
            UpdateInkFreezeButtonState();
        }

        internal void AttachBoardInkFreezeBtn(BoardToolbarButton btn)
        {
            BoardInkFreezeBtn = btn;
            UpdateInkFreezeButtonState();
        }

        private void UpdateInkFreezeButtonState()
        {
            try
            {
                if (Freeze_Icon != null)
                {
                    bool isFrozen = IsCurrentPageFrozen;
                    Freeze_Icon.Label = Strings.GetString(isFrozen ? "FloatingBar_Unfreeze" : "FloatingBar_Freeze")
                                        ?? (isFrozen ? "解冻" : "冻结");
                    Freeze_Icon.Icon.Geometry = Geometry.Parse(isFrozen
                        ? XamlGraphicsIconGeometries.UnfreezeIconGeometry
                        : XamlGraphicsIconGeometries.FreezeIconGeometry);

                    var foreground = FloatBarForegroundColor;
                    var frozenColor = IsCurrentThemeDark() ? Color.FromRgb(102, 204, 255) : Color.FromRgb(30, 58, 138);
                    Freeze_Icon.Icon.Brush = new SolidColorBrush(isFrozen ? frozenColor : foreground);
                }

                if (BoardInkFreezeBtn != null)
                {
                    int pageIndex = GetCurrentFreezePageIndex();
                    bool isFrozen = IsPageFrozen(pageIndex);
                    BoardInkFreezeBtn.Label = Strings.GetString(isFrozen ? "FloatingBar_Unfreeze" : "FloatingBar_Freeze")
                                              ?? (isFrozen ? "解冻" : "冻结");
                    BoardInkFreezeBtn.IconGeometry = isFrozen
                        ? XamlGraphicsIconGeometries.UnfreezeIconGeometry
                        : XamlGraphicsIconGeometries.FreezeIconGeometry;

                    var frozenColor = IsCurrentThemeDark() ? Color.FromRgb(102, 204, 255) : Color.FromRgb(30, 58, 138);
                    BoardInkFreezeBtn.IconBrush = new SolidColorBrush(isFrozen ? frozenColor : FloatBarForegroundColor);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"更新冻结按钮状态失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        internal async void BoardInkFreeze_MouseUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                int pageIndex = GetCurrentFreezePageIndex();
                if (IsPageFrozen(pageIndex))
                {
                    await UnfreezePageAsync(pageIndex);
                }
                else
                {
                    FreezePage(pageIndex);

                    if (!isFloatingBarFolded)
                    {
                        HideSubPanels("cursor", true);
                        await Task.Delay(50);

                        if (IsInPptPresentationMode)
                            ViewboxFloatingBarMarginAnimation(60);
                        else
                            ViewboxFloatingBarMarginAnimation(100, true);
                    }

                    UpdateInkFreezeButtonState();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"白板冻结按钮点击失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        private void EnsureCurrentFrozenEditingState()
        {
            try
            {
                drawingShapeMode = 0;
                inkCanvas?.Select(new StrokeCollection());
                if (GridInkCanvasSelectionCover != null)
                    GridInkCanvasSelectionCover.Visibility = Visibility.Collapsed;
                if (currentSelectedElement != null)
                {
                    UnselectElement(currentSelectedElement);
                    currentSelectedElement = null;
                }
                DisableEraserOverlay();

                if (inkCanvas != null && inkCanvas.EditingMode != InkCanvasEditingMode.None)
                    inkCanvas.EditingMode = InkCanvasEditingMode.None;
                _globalHotkeyManager?.UpdateHotkeyStateForToolMode(true);
                UpdateCurrentToolMode("cursor");
                SetFloatingBarHighlightPosition("cursor");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"切换冻结编辑状态失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        private bool TryBlockFrozenPageMutation(string action = null)
        {
            if (!IsCurrentPageFrozen) return false;

            EnsureCurrentFrozenEditingState();

            if (DateTime.UtcNow - lastFreezeBlockNotificationUtc > TimeSpan.FromMilliseconds(1500))
            {
                lastFreezeBlockNotificationUtc = DateTime.UtcNow;
                ShowNotification(string.IsNullOrWhiteSpace(action)
                    ? "当前页面已冻结，不能修改内容"
                    : $"当前页面已冻结，不能{action}");
            }

            return true;
        }

        private void ResetInkFreezePageStates()
        {
            for (int i = 0; i < frozenPages.Length; i++)
            {
                frozenPages[i] = false;
                pageLastUserInkMutationUtc[i] = DateTime.MinValue;
            }
            HandleInkFreezeCourseCancel(false);
            UpdateInkFreezeButtonState();
        }

        private void HandleInkFreezeCourseStart(int pageIndex)
        {
            if (!IsValidFreezePageIndex(pageIndex)) pageIndex = GetCurrentFreezePageIndex();
            freezeCourseRecordedPageIndex = pageIndex;
            freezeCourseRecordedUtc = DateTime.UtcNow;
            delayedFreezeTimer?.Stop();
            delayedFreezePageIndex = null;
            ShowNotification(pageIndex == 0 ? "已记录当前批注页，等待课程结束后冻结" : $"已记录白板第 {pageIndex} 页，等待课程结束后冻结");
        }

        private void HandleInkFreezeCourseEnd(int pageIndex)
        {
            if (!IsValidFreezePageIndex(pageIndex))
                pageIndex = freezeCourseRecordedPageIndex ?? GetCurrentFreezePageIndex();

            ScheduleDelayedFreeze(pageIndex, DateTime.UtcNow);
        }

        private void HandleInkFreezeCourseCancel(bool notify = true)
        {
            delayedFreezeTimer?.Stop();
            delayedFreezePageIndex = null;
            freezeCourseRecordedPageIndex = null;
            if (notify) ShowNotification("已取消自动冻结倒计时");
        }

        private void ScheduleDelayedFreeze(int pageIndex, DateTime referenceTimeUtc)
        {
            if (!IsValidFreezePageIndex(pageIndex)) return;

            delayedFreezeTimer?.Stop();
            delayedFreezePageIndex = pageIndex;
            delayedFreezeReferenceUtc = referenceTimeUtc;

            delayedFreezeTimer = new DispatcherTimer
            {
                Interval = DelayedFreezeDelay
            };
            delayedFreezeTimer.Tick += (s, e) =>
            {
                delayedFreezeTimer.Stop();
                var target = delayedFreezePageIndex;
                delayedFreezePageIndex = null;
                freezeCourseRecordedPageIndex = null;

                if (!target.HasValue || !IsValidFreezePageIndex(target.Value)) return;

                if (pageLastUserInkMutationUtc[target.Value] <= delayedFreezeReferenceUtc)
                    FreezePage(target.Value, true);
                else
                    ShowNotification("检测到页面操作，已取消自动冻结");
            };
            delayedFreezeTimer.Start();

            ShowNotification("将在 3 分钟后检查并自动冻结页面");
        }
    }
}
