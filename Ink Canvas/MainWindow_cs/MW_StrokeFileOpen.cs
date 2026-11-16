using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Media;
using Ink_Canvas.Helpers;
using Ink_Canvas.Properties;
using Ink_Canvas.Windows;

namespace Ink_Canvas
{
    public partial class MainWindow
    {
        private readonly Queue<string> pendingStrokeFiles = new Queue<string>();
        private bool isOpeningStrokeFile;

        // 冷启动与 IPC 共用此入口；模态窗口的消息循环仍会收到 IPC，按顺序处理，避免叠放弹窗。
        public void OpenStrokeFileFromShell(string filePath)
        {
            pendingStrokeFiles.Enqueue(filePath);
            if (isOpeningStrokeFile) return;
            isOpeningStrokeFile = true;
            try
            {
                while (pendingStrokeFiles.Count > 0)
                {
                    var path = pendingStrokeFiles.Dequeue();
                    try
                    {
                        var dialog = new StrokeFileOpenWindow(path) { Owner = MessageBoxHelper.GetDefaultOwner(this) };
                        if (dialog.Owner == null) dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                        if (dialog.ShowDialog() == true && ImportStrokeFile(path, dialog.SelectedTarget))
                            ShowNotification(string.Format(MainWindowStrings.Main_StrokesFileLoaded, Path.GetFileName(path)));
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"打开外部墨迹文件失败: {ex}", LogHelper.LogType.Error);
                        ShowNotification(ex is InvalidDataException || ex is InvalidOperationException
                            ? ex.Message : MainWindowStrings.Main_StrokesFileOpenFailed);
                    }
                }
            }
            finally
            {
                isOpeningStrokeFile = false;
            }
        }

        internal bool ImportStrokeFile(string filePath, StrokeFileOpenTarget? target)
        {
            if (!target.HasValue) return false;

            // 在切换模式之前读完并校验，损坏、空文件不会改变当前画布。
            StrokeCollection strokes;
            using (var stream = File.OpenRead(filePath)) strokes = new StrokeCollection(stream);
            if (strokes.Count == 0) throw new InvalidDataException(MainWindowStrings.Main_OpenInk_Empty);
            int targetMode = target == StrokeFileOpenTarget.Screen ? 0 : 1;
            if (IsPageFrozen(targetMode == 0 ? 0 : CurrentWhiteboardIndex))
                throw new InvalidOperationException(MainWindowStrings.Main_Freeze_FrozenNoEdit);
            if (isDisplayingOrHidingBlackboard)
                throw new InvalidOperationException(MainWindowStrings.Main_OpenInk_ModeBusy);

            if (target == StrokeFileOpenTarget.Booth)
            {
                // 已在照片页时不重新进入展台，保留当前照片及其变换。
                if (!_isVideoPresenterSpecialMode) ToggleVideoBooth();
            }
            else
            {
                if (_isVideoPresenterSpecialMode)
                {
                    // 先保存展台状态并恢复白板，再向目标页面追加文件。
                    ExitVideoPresenterSpecialMode();
                    CloseVideoPresenterSidebarAndReleaseResources();
                    GridBackgroundCover.Visibility = Visibility.Visible;
                }
                if (currentMode != targetMode) ImageBlackboard_MouseUp(null, null);
                if (target == StrokeFileOpenTarget.Screen && GridTransparencyFakeBackground.Background == Brushes.Transparent)
                    BtnHideInkCanvas_Click(null, null);
            }

            if (currentMode != targetMode || (target == StrokeFileOpenTarget.Booth && !_isVideoPresenterSpecialMode))
                throw new InvalidOperationException(MainWindowStrings.Main_OpenInk_ModeBusy);

            // 复用批量插入：保留已有元素和历史，即使当前是点橡皮也只生成一条撤销记录。
            if (!TryAddPluginStrokes(strokes, null)) return false;
            MarkCurrentPageInkChanged();
            return true;
        }
    }
}
