using Ink_Canvas.Helpers;
using Ink_Canvas.Plugins;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace Ink_Canvas
{
    public partial class MainWindow
    {
        internal bool TryBeginPluginWhiteboardMutation(string action)
        {
            var allowed = false;
            RunOnUiThread(() => allowed = IsWhiteboardMode && !TryBlockFrozenPageMutation(action));
            return allowed;
        }

        private readonly PluginWhiteboardStateStore _pluginWhiteboardStateStore =
            new PluginWhiteboardStateStore();
        private const string PluginDocumentStateFileName = "plugins.json";

        internal event EventHandler<WhiteboardPageChangingEventArgs> PluginWhiteboardPageChanging;
        internal event EventHandler<WhiteboardPageChangedEventArgs> PluginWhiteboardDocumentChanged;
        internal event EventHandler<WhiteboardPageRemovedEventArgs> PluginWhiteboardPageRemoved;
        internal event EventHandler PluginWhiteboardPageClearing;

        internal WhiteboardPageInfo GetCurrentPluginWhiteboardPage()
        {
            WhiteboardPageInfo page = null;
            RunOnUiThread(() => page = CreatePluginWhiteboardPageInfo(GetPluginWhiteboardPageIndex()));
            return page;
        }

        internal void RegisterPluginPageStateProvider(string pluginId, IWhiteboardPageStateProvider provider)
        {
            RunOnUiThread(() =>
            {
                _pluginWhiteboardStateStore.RegisterProvider(
                    pluginId,
                    provider,
                    GetPluginWhiteboardPageId(GetPluginWhiteboardPageIndex()),
                    LogPluginPageStateError);
            });
        }

        internal void UnregisterPluginPageStateProvider(string pluginId)
        {
            if (string.IsNullOrWhiteSpace(pluginId)) return;
            RunOnUiThread(() =>
            {
                CaptureCurrentPluginPageStates();
                _pluginWhiteboardStateStore.UnregisterProvider(pluginId);
            });
        }

        internal void RegisterPluginLegacyStateImporter(
            string pluginId,
            IWhiteboardLegacyStateImporter importer)
        {
            RunOnUiThread(() => _pluginWhiteboardStateStore.RegisterLegacyImporter(pluginId, importer));
        }

        private void BeginPluginWhiteboardPageChange(int targetIndex, string targetPageId = null)
        {
            var currentIndex = GetPluginWhiteboardPageIndex();
            CaptureCurrentPluginPageStates();
            PluginWhiteboardPageChanging?.Invoke(this, new WhiteboardPageChangingEventArgs
            {
                CurrentPage = CreatePluginWhiteboardPageInfo(currentIndex),
                TargetPage = CreatePluginWhiteboardPageInfo(targetIndex, targetPageId)
            });
        }

        private void CompletePluginWhiteboardPageChange(int previousIndex)
        {
            var currentIndex = GetPluginWhiteboardPageIndex();
            RestoreCurrentPluginPageStates();
            PluginWhiteboardDocumentChanged?.Invoke(this, new WhiteboardPageChangedEventArgs
            {
                PreviousPage = CreatePluginWhiteboardPageInfo(previousIndex),
                CurrentPage = CreatePluginWhiteboardPageInfo(currentIndex)
            });
        }

        private string CreatePluginWhiteboardPageId() => _pluginWhiteboardStateStore.CreatePageId();

        private string GetPluginWhiteboardPageId(int pageIndex)
        {
            return _pluginWhiteboardStateStore.GetPageId(pageIndex);
        }

        private void InsertPluginWhiteboardPageId(int pageIndex, string newPageId)
        {
            _pluginWhiteboardStateStore.InsertPageId(pageIndex, newPageId, WhiteboardTotalCount);
        }

        private WhiteboardPageInfo RemovePluginWhiteboardPageId(int pageIndex, int oldPageCount)
        {
            var removed = CreatePluginWhiteboardPageInfo(pageIndex);
            _pluginWhiteboardStateStore.RemovePageId(pageIndex, oldPageCount);
            return removed;
        }

        private void NotifyPluginWhiteboardPageRemoved(WhiteboardPageInfo removedPage)
        {
            PluginWhiteboardPageRemoved?.Invoke(this, new WhiteboardPageRemovedEventArgs
            {
                RemovedPage = removedPage,
                CurrentPage = CreatePluginWhiteboardPageInfo(GetPluginWhiteboardPageIndex())
            });
        }

        private void NotifyPluginWhiteboardPageClearing()
        {
            if (!IsWhiteboardMode) return;
            PluginWhiteboardPageClearing?.Invoke(this, EventArgs.Empty);
        }

        private int GetPluginWhiteboardPageIndex() => currentMode == 0 ? 0 : CurrentWhiteboardIndex;

        private WhiteboardPageInfo CreatePluginWhiteboardPageInfo(int pageIndex, string explicitPageId = null)
        {
            return new WhiteboardPageInfo
            {
                Id = explicitPageId ?? GetPluginWhiteboardPageId(pageIndex),
                Index = pageIndex,
                Count = currentMode == 0 ? 1 : WhiteboardTotalCount,
                IsFrozen = IsPageFrozen(pageIndex)
            };
        }

        private void CaptureCurrentPluginPageStates()
        {
            _pluginWhiteboardStateStore.Capture(
                GetPluginWhiteboardPageId(GetPluginWhiteboardPageIndex()),
                LogPluginPageStateError);
        }

        private void RestoreCurrentPluginPageStates()
        {
            _pluginWhiteboardStateStore.Restore(
                GetPluginWhiteboardPageId(GetPluginWhiteboardPageIndex()),
                LogPluginPageStateError);
        }

        private static void LogPluginPageStateError(string pluginId, Exception ex)
        {
            LogHelper.WriteLogToFile(
                $"插件 {pluginId} 捕获或恢复页面状态失败: {ex}",
                LogHelper.LogType.Error);
        }

        private void SavePluginPageDocumentSidecar(string documentPath, int pageIndex)
        {
            if (currentMode == 0 || string.IsNullOrWhiteSpace(documentPath)) return;
            if (pageIndex == CurrentWhiteboardIndex) CaptureCurrentPluginPageStates();
            WritePluginDocumentState(
                Path.ChangeExtension(documentPath, ".plugins.json"),
                _pluginWhiteboardStateStore.ExportDocumentJson(pageIndex, 1));
            WritePluginCompanionStates(documentPath, pageIndex);
        }

        private bool HasPluginPageState(int pageIndex)
        {
            if (currentMode == 0) return false;
            if (pageIndex == CurrentWhiteboardIndex) CaptureCurrentPluginPageStates();
            return _pluginWhiteboardStateStore.HasPageState(pageIndex);
        }

        private void LoadPluginPageDocumentSidecar(string documentPath)
        {
            if (currentMode == 0 || string.IsNullOrWhiteSpace(documentPath)) return;
            var statePath = Path.ChangeExtension(documentPath, ".plugins.json");
            try
            {
                if (File.Exists(statePath))
                    _pluginWhiteboardStateStore.ImportSinglePageJson(
                        File.ReadAllText(statePath, Encoding.UTF8),
                        GetPluginWhiteboardPageIndex());
                else
                    _pluginWhiteboardStateStore.ResetPage(GetPluginWhiteboardPageIndex());
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"插件页面状态加载失败: {ex}", LogHelper.LogType.Error);
                _pluginWhiteboardStateStore.ResetPage(GetPluginWhiteboardPageIndex());
            }
            _pluginWhiteboardStateStore.ImportLegacyPageSidecars(
                documentPath,
                GetPluginWhiteboardPageIndex(),
                LogPluginPageStateError);
            RestoreCurrentPluginPageStates();
            CommitLoadedPluginStateToCurrentHistory();
        }

        private void SavePluginDocumentStateToDirectory(string directory)
        {
            if (currentMode == 0 || string.IsNullOrWhiteSpace(directory)) return;
            CaptureCurrentPluginPageStates();
            WritePluginDocumentState(
                Path.Combine(directory, PluginDocumentStateFileName),
                _pluginWhiteboardStateStore.ExportDocumentJson(1, WhiteboardTotalCount));
            for (var pageIndex = 1; pageIndex <= WhiteboardTotalCount; pageIndex++)
                WritePluginCompanionStates(
                    Path.Combine(directory, $"page_{pageIndex:D4}"),
                    pageIndex);
        }

        private void LoadPluginDocumentStateFromDirectory(string directory)
        {
            if (currentMode == 0 || string.IsNullOrWhiteSpace(directory)) return;
            var statePath = Path.Combine(directory, PluginDocumentStateFileName);
            try
            {
                if (File.Exists(statePath))
                    _pluginWhiteboardStateStore.ImportDocumentJson(
                        File.ReadAllText(statePath, Encoding.UTF8),
                        1,
                        WhiteboardTotalCount);
                else
                    _pluginWhiteboardStateStore.ResetDocument(1, WhiteboardTotalCount);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"插件文档状态加载失败: {ex}", LogHelper.LogType.Error);
                _pluginWhiteboardStateStore.ResetDocument(1, WhiteboardTotalCount);
            }
            _pluginWhiteboardStateStore.ImportLegacyPackagePages(
                directory,
                1,
                WhiteboardTotalCount,
                LogPluginPageStateError);
            AppendLoadedPluginStatesToPageHistories(1, WhiteboardTotalCount);
            RestoreCurrentPluginPageStates();
        }

        private static void WritePluginDocumentState(string path, string json)
        {
            var temporaryPath = path + ".tmp";
            try
            {
                File.WriteAllText(temporaryPath, json, new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temporaryPath, path, null);
                else File.Move(temporaryPath, path);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }

        private void WritePluginCompanionStates(string contentFilePath, int pageIndex)
        {
            foreach (var state in _pluginWhiteboardStateStore.GetCompanionStates(
                         pageIndex,
                         LogPluginPageStateError))
                WritePluginDocumentState(
                    Path.ChangeExtension(contentFilePath, state.FileExtension),
                    state.Content);
        }

        private void CommitLoadedPluginStateToCurrentHistory()
        {
            foreach (var state in _pluginWhiteboardStateStore.GetInitialHistories(
                         GetPluginWhiteboardPageIndex(),
                         LogPluginPageStateError))
                timeMachine.CommitPluginStateHistory(
                    state.PluginId,
                    state.EmptyState,
                    state.LoadedState);
        }

        private void AppendLoadedPluginStatesToPageHistories(int firstPageIndex, int pageCount)
        {
            for (var pageIndex = firstPageIndex;
                 pageIndex < firstPageIndex + pageCount;
                 pageIndex++)
            {
                var states = _pluginWhiteboardStateStore.GetInitialHistories(
                    pageIndex,
                    LogPluginPageStateError);
                if (states.Count == 0) continue;
                var history = TimeMachineHistories[pageIndex]?.ToList()
                              ?? new System.Collections.Generic.List<TimeMachineHistory>();
                foreach (var state in states)
                    history.Add(new TimeMachineHistory(
                        state.PluginId,
                        state.EmptyState,
                        state.LoadedState));
                TimeMachineHistories[pageIndex] = history.ToArray();
            }
        }
    }
}
