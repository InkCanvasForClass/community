using Ink_Canvas.Controls;
using Ink_Canvas.Controls.Toolbar.FloatingToolbar;
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
                if (notify) ShowNotification(MainWindowStrings.Main_Freeze_PageFrozen);
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
            if (notify) ShowNotification(pageIndex == 0 ? MainWindowStrings.Main_Freeze_AnnotationPageFrozen : string.Format(MainWindowStrings.Main_Freeze_WhiteboardPageFrozen, pageIndex));
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
                    MainWindowStrings.Main_Freeze_VerifyTitle,
                    MainWindowStrings.Main_Freeze_VerifyMessage);
                if (!ok)
                {
                    if (notify) ShowNotification(MainWindowStrings.Main_Freeze_VerifyFailed);
                    return false;
                }
            }

            frozenPages[pageIndex] = false;
            if (pageIndex == GetCurrentFreezePageIndex())
                ApplyFreezeStateToCurrentStrokes();

            UpdateInkFreezeButtonState();
            if (notify) ShowNotification(pageIndex == 0 ? MainWindowStrings.Main_Freeze_PageUnfrozen : string.Format(MainWindowStrings.Main_Freeze_WhiteboardPageUnfrozen, pageIndex));
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
                    Freeze_Icon.Label = isFrozen ? FloatingBarStrings.FloatingBar_Unfreeze : FloatingBarStrings.FloatingBar_Freeze;
                    Freeze_Icon.Icon.Geometry = Geometry.Parse(isFrozen
                        ? XamlGraphicsIconGeometries.UnfreezeIconGeometry
                        : XamlGraphicsIconGeometries.FreezeIconGeometry);

                    var foreground = FloatBarForegroundColor;
                    var frozenColor = IsCurrentThemeDark() ? Color.FromRgb(102, 204, 255) : Color.FromRgb(30, 58, 138);
                    if (!ToolbarRegistry.GetUseRedStyle(Freeze_Icon))
                        Freeze_Icon.Icon.Brush = new SolidColorBrush(isFrozen ? frozenColor : foreground);
                }

                if (BoardInkFreezeBtn != null)
                {
                    int pageIndex = GetCurrentFreezePageIndex();
                    bool isFrozen = IsPageFrozen(pageIndex);
                    BoardInkFreezeBtn.Label = isFrozen ? FloatingBarStrings.FloatingBar_Unfreeze : FloatingBarStrings.FloatingBar_Freeze;
                    BoardInkFreezeBtn.IconGeometry = isFrozen
                        ? XamlGraphicsIconGeometries.UnfreezeIconGeometry
                        : XamlGraphicsIconGeometries.FreezeIconGeometry;

                    if (isFrozen)
                    {
                        BoardInkFreezeBtn.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                        BoardInkFreezeBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                        BoardInkFreezeBtn.IconBrush = new SolidColorBrush(Colors.GhostWhite);
                        BoardInkFreezeBtn.Foreground = new SolidColorBrush(Colors.GhostWhite);
                    }
                    else
                    {
                        bool isDark = Settings.Appearance.Theme == 1 ||
                            (Settings.Appearance.Theme == 2 && !ThemeHelper.IsSystemThemeLight());
                        BoardInkFreezeBtn.Background = new SolidColorBrush(isDark
                            ? Color.FromRgb(42, 42, 42)
                            : Color.FromRgb(244, 244, 245));
                        BoardInkFreezeBtn.BorderBrush = new SolidColorBrush(isDark
                            ? Color.FromRgb(85, 85, 85)
                            : Color.FromRgb(161, 161, 170));
                        BoardInkFreezeBtn.IconBrush = new SolidColorBrush(FloatBarForegroundColor);
                        BoardInkFreezeBtn.Foreground = new SolidColorBrush(FloatBarForegroundColor);
                    }
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
                    PenIcon_Click(null, null);
                }
                else
                {
                    FreezePage(pageIndex);

                    if (!isFloatingBarFolded)
                    {
                        HideSubPanels("cursor", true);
                        await Task.Delay(50);

                        if (IsInPPTPresentationMode)
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
                    ? MainWindowStrings.Main_Freeze_FrozenNoEdit
                    : string.Format(MainWindowStrings.Main_Freeze_FrozenNoAction, action));
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
            ShowNotification(pageIndex == 0 ? MainWindowStrings.Main_Freeze_Recorded : string.Format(MainWindowStrings.Main_Freeze_RecordedWhiteboard, pageIndex));
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
            if (notify) ShowNotification(MainWindowStrings.Main_Freeze_CancelCountdown);
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
                    ShowNotification(MainWindowStrings.Main_Freeze_CancelByOperation);
            };
            delayedFreezeTimer.Start();

            ShowNotification(MainWindowStrings.Main_Freeze_AutoFreezeIn3Min);
        }
    }
}
