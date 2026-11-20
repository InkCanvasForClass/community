using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Ink_Canvas.Helpers;
using Ink_Canvas.Properties;
using Ink_Canvas.Windows;
using Newtonsoft.Json;

namespace Ink_Canvas
{
    public partial class MainWindow
    {
        private readonly Queue<string> pendingStrokeFiles = new Queue<string>();
        private bool isOpeningStrokeFile;

        // 冷启动与 IPC 共用此入口；模态窗口的消息循环仍会收到 IPC，按顺序处理，避免叠放弹窗。
        public async void OpenStrokeFileFromShell(string filePath)
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
                        if (dialog.ShowDialog() == true && await ImportStrokeFile(path, dialog.SelectedTarget))
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

        internal async Task<bool> ImportStrokeFile(string filePath, StrokeFileOpenTarget? target)
        {
            if (!target.HasValue) return false;
            int targetMode = target == StrokeFileOpenTarget.Screen ? 0 : 1;
            int targetPageIndex = targetMode == 0 ? 0 : CurrentWhiteboardIndex;
            string targetPageId = _pluginWhiteboardStateStore.GetPageId(targetPageIndex);
            bool wasInBooth = _isVideoPresenterSpecialMode;
            int boothPhotoIndex = _boothCurrentPhotoIndex;

            // 在切换模式之前读完并校验，损坏、空文件不会改变当前画布。
            StrokeCollection strokes;
            using (var stream = File.OpenRead(filePath)) strokes = new StrokeCollection(stream);
            var elements = new List<FrameworkElement>();
            var sceneElements = new List<FrameworkElement>();
            var elementsPath = Path.ChangeExtension(filePath, ".elements.json");
            if (File.Exists(elementsPath))
            {
                var infos = JsonConvert.DeserializeObject<List<CanvasElementInfo>>(File.ReadAllText(elementsPath))
                    ?? throw new InvalidDataException(MainWindowStrings.Main_StrokesFileOpenFailed);
                foreach (var info in infos)
                {
                    if (info == null) throw new InvalidDataException(MainWindowStrings.Main_StrokesFileOpenFailed);
                    FrameworkElement element;
                    switch (info.Type?.ToLowerInvariant())
                    {
                        case "image":
                            if (!File.Exists(info.SourcePath))
                                throw new FileNotFoundException(MainWindowStrings.Main_OpenInk_ElementFailed, info.SourcePath);
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.UriSource = new Uri(info.SourcePath, UriKind.Absolute);
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.EndInit();
                            bitmap.Freeze();
                            element = new Image { Source = bitmap, Width = info.Width, Height = info.Height,
                                Stretch = Enum.TryParse<Stretch>(info.Stretch, out var stretch) ? stretch : Stretch.Fill };
                            InkCanvas.SetLeft(element, info.Left);
                            InkCanvas.SetTop(element, info.Top);
                            break;
                        case "pdf":
                            element = await CreatePdfFromElementInfoAsync(info);
                            break;
                        case "media":
                            element = CreateMediaFromElementInfo(info);
                            break;
                        case "svgscene":
                        case "svgscenegroup":
                            element = CreateSecAgentSceneElement(info);
                            if (element != null) sceneElements.Add(element);
                            break;
                        default:
                            throw new InvalidDataException(MainWindowStrings.Main_OpenInk_ElementFailed);
                    }
                    if (element == null) throw new InvalidDataException(MainWindowStrings.Main_OpenInk_ElementFailed);
                    elements.Add(element);
                }
            }
            var pluginStates = _pluginWhiteboardStateStore.ReadPageSidecars(filePath);
            if (strokes.Count == 0 && elements.Count == 0 && pluginStates.Count == 0)
                throw new InvalidDataException(MainWindowStrings.Main_OpenInk_Empty);
            // PDF 准备期间会让出 UI 线程，用户可能切页、删除页或切换展台照片。
            if ((targetMode == 1 && CurrentWhiteboardIndex != targetPageIndex) ||
                _pluginWhiteboardStateStore.GetPageId(targetPageIndex) != targetPageId ||
                (target == StrokeFileOpenTarget.Booth && (wasInBooth != _isVideoPresenterSpecialMode ||
                    (wasInBooth && boothPhotoIndex != _boothCurrentPhotoIndex))))
                throw new InvalidOperationException(MainWindowStrings.Main_OpenInk_PageChanged);
            if (IsPageFrozen(targetPageIndex))
                throw new InvalidOperationException(MainWindowStrings.Main_Freeze_FrozenNoEdit);
            if (isDisplayingOrHidingBlackboard)
                throw new InvalidOperationException(MainWindowStrings.Main_OpenInk_ModeBusy);
            if (pluginStates.Count > 0) CaptureCurrentPluginPageStates();
            var pluginChanges = _pluginWhiteboardStateStore.PreparePageAppend(pluginStates, targetPageIndex);

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

            // 数据和元素已就绪；同步追加并将整份文件的历史组成一组，避免 PDF 异步加载到其他页面。
            var history = new List<TimeMachineHistory>();
            var previousCommitType = _currentCommitType;
            _currentCommitType = CommitReason.CodeInput;
            try
            {
                if (strokes.Count > 0)
                {
                    history.Add(new TimeMachineHistory(strokes, TimeMachineHistoryType.UserInput, false));
                    inkCanvas.Strokes.Add(strokes);
                }
                foreach (var element in elements)
                {
                    history.Add(new TimeMachineHistory(element, TimeMachineHistoryType.ElementInsert));
                    inkCanvas.Children.Add(element);
                }
                foreach (var state in pluginChanges)
                {
                    history.Add(new TimeMachineHistory(state.PluginId, state.EmptyState, state.LoadedState));
                    _pluginWhiteboardStateStore.SetPageState(GetPluginWhiteboardPageIndex(), state.PluginId, state.LoadedState, true);
                }
            }
            catch
            {
                for (int i = history.Count - 1; i >= 0; i--)
                {
                    history[i].StrokeHasBeenCleared = true;
                    ApplyHistoryToCanvas(history[i]);
                }
                throw;
            }
            finally
            {
                _currentCommitType = previousCommitType;
            }
            timeMachine.CommitHistoryGroup(history);
            foreach (var element in sceneElements) RefreshRestoredSecAgentElementLayout(element);
            if (elements.Exists(element => element is Controls.PdfEmbeddedView))
            {
                _pdfSidebarNextPositionUseHostTransform = true;
                SyncPdfPageSidebarWithCanvas();
            }
            MarkCurrentPageInkChanged();
            return true;
        }
    }
}
