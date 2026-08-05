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

                    var accent = Application.Current.TryFindResource("FloatingBarAccentBrush") as Brush
                        ?? new SolidColorBrush(Color.FromRgb(37, 99, 235));
                    var normalBackground = Application.Current.TryFindResource("FloatingBarBackgroundBrush") as Brush
                        ?? Application.Current.TryFindResource("BoardFloatBarBackground") as Brush
                        ?? new SolidColorBrush(Color.FromRgb(42, 42, 42));
                    var normalForeground = Application.Current.TryFindResource("FloatingBarForegroundBrush") as Brush
                        ?? new SolidColorBrush(FloatBarForegroundColor);

                    if (isFrozen)
                    {
                        BoardInkFreezeBtn.Background = accent;
                        BoardInkFreezeBtn.IconBrush = Brushes.White;
                        BoardInkFreezeBtn.Foreground = Brushes.White;
                    }
                    else
                    {
                        BoardInkFreezeBtn.Background = Brushes.Transparent;
                        BoardInkFreezeBtn.IconBrush = normalForeground;
                        BoardInkFreezeBtn.Foreground = normalForeground;
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

        private static bool ContainsCjkCharacters(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;

            foreach (char ch in text)
            {
                if (ch >= 0x3400 && ch <= 0x9FFF)
                    return true;
            }

            return false;
        }

        private string LocalizeFrozenPageAction(string action)
        {
            if (string.IsNullOrWhiteSpace(action)) return null;

            string resourceKey = action switch
            {
                "修改冻结页面" => "Main_Freeze_Action_EditPage",
                "切换到编辑工具" => "Main_Freeze_Action_SwitchToEditTool",
                "切换到选择工具" => "Main_Freeze_Action_SwitchToSelectionTool",
                "清除冻结页面内容" => "Main_Freeze_Action_ClearPageContent",
                "粘贴图片" => "Main_Freeze_Action_PasteImage",
                "扩展画布" => "Main_Freeze_Action_ExpandCanvas",
                "擦除冻结页面" => "Main_Freeze_Action_ErasePageContent",
                "移动图片" => "Main_Freeze_Action_MoveImage",
                "缩放图片" => "Main_Freeze_Action_ResizeImage",
                "移动或缩放图片" => "Main_Freeze_Action_MoveOrResizeImage",
                "克隆图片" => "Main_Freeze_Action_CloneImage",
                "克隆图片到新页面" => "Main_Freeze_Action_CloneImageToNewPage",
                "旋转图片" => "Main_Freeze_Action_RotateImage",
                "切换 PDF 页" => "Main_Freeze_Action_SwitchPdfPage",
                "删除图片" => "Main_Freeze_Action_DeleteImage",
                "撤销冻结页面内容" => "Main_Freeze_Action_UndoPageChanges",
                "重做冻结页面内容" => "Main_Freeze_Action_RedoPageChanges",
                "重播冻结页面内容" => "Main_Freeze_Action_ReplayPageChanges",
                "切换到画笔" => "Main_Freeze_Action_SwitchToPen",
                "切换到橡皮擦" => "Main_Freeze_Action_SwitchToEraser",
                "切换到线擦" => "Main_Freeze_Action_SwitchToStrokeEraser",
                "清空冻结页面内容" => "Main_Freeze_Action_ClearPage",
                "插入截图" => "Main_Freeze_Action_InsertScreenshot",
                "插入图片" => "Main_Freeze_Action_InsertImage",
                "插入控件" => "Main_Freeze_Action_InsertElement",
                "删除控件" => "Main_Freeze_Action_DeleteElement",
                "书写" => "Main_Freeze_Action_Write",
                "打开墨迹文件" => "Main_Freeze_Action_OpenInkFile",
                "恢复墨迹文件" => "Main_Freeze_Action_RestoreInkFile",
                "克隆墨迹" => "Main_Freeze_Action_CloneInk",
                "克隆墨迹到新页面" => "Main_Freeze_Action_CloneInkToNewPage",
                "插入墨迹到白板" => "Main_Freeze_Action_InsertInkToWhiteboard",
                "修改墨迹粗细" => "Main_Freeze_Action_ChangeInkThickness",
                "翻转墨迹" => "Main_Freeze_Action_FlipInk",
                "旋转墨迹" => "Main_Freeze_Action_RotateInk",
                "移动墨迹" => "Main_Freeze_Action_MoveInk",
                "移动或缩放墨迹" => "Main_Freeze_Action_MoveOrResizeInk",
                "调整墨迹大小" => "Main_Freeze_Action_ResizeInk",
                "打开几何工具" => "Main_Freeze_Action_OpenShapeTool",
                "绘制几何图形" => "Main_Freeze_Action_DrawShape",
                "书写或擦除" => "Main_Freeze_Action_WriteOrErase",
                "移动或缩放内容" => "Main_Freeze_Action_MoveOrResizeContent",
                _ when action == FloatingBarStrings.Board_InsertImage => "Main_Freeze_Action_InsertFile",
                _ => null
            };

            if (!string.IsNullOrWhiteSpace(resourceKey))
                return MainWindowStrings.GetString(resourceKey) ?? action;

            if (!System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase)
                && ContainsCjkCharacters(action))
                return null;

            return action;
        }

        private bool TryBlockFrozenPageMutation(string action = null)
        {
            if (!IsCurrentPageFrozen) return false;

            EnsureCurrentFrozenEditingState();

            if (DateTime.UtcNow - lastFreezeBlockNotificationUtc > TimeSpan.FromMilliseconds(1500))
            {
                lastFreezeBlockNotificationUtc = DateTime.UtcNow;
                string localizedAction = LocalizeFrozenPageAction(action);
                ShowNotification(string.IsNullOrWhiteSpace(localizedAction)
                    ? MainWindowStrings.Main_Freeze_FrozenNoEdit
                    : string.Format(MainWindowStrings.Main_Freeze_FrozenNoAction, localizedAction));
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
