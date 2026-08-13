using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Ink_Canvas.Plugins
{
    internal sealed class PluginWhiteboardStateStore
    {
        private readonly string[] _pageIds;
        private readonly Func<string> _createPageId;
        private readonly Dictionary<string, IWhiteboardPageStateProvider> _providers =
            new Dictionary<string, IWhiteboardPageStateProvider>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, IWhiteboardLegacyStateImporter> _legacyImporters =
            new Dictionary<string, IWhiteboardLegacyStateImporter>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Dictionary<string, string>> _states =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        internal PluginWhiteboardStateStore(int pageCapacity = 101, Func<string> createPageId = null)
        {
            if (pageCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(pageCapacity));
            _pageIds = new string[pageCapacity];
            _createPageId = createPageId ?? (() => Guid.NewGuid().ToString("N"));
        }

        internal string CreatePageId() => _createPageId();

        internal string GetPageId(int pageIndex)
        {
            ValidatePageIndex(pageIndex);
            if (string.IsNullOrEmpty(_pageIds[pageIndex])) _pageIds[pageIndex] = CreatePageId();
            return _pageIds[pageIndex];
        }

        internal void InsertPageId(int pageIndex, string newPageId, int pageCountAfterInsert)
        {
            if (pageIndex < 1) throw new ArgumentOutOfRangeException(nameof(pageIndex));
            ValidatePageIndex(pageIndex);
            ValidatePageIndex(pageCountAfterInsert);

            for (var i = pageCountAfterInsert; i > pageIndex; i--)
                _pageIds[i] = _pageIds[i - 1];
            _pageIds[pageIndex] = string.IsNullOrEmpty(newPageId) ? CreatePageId() : newPageId;
        }

        internal string RemovePageId(int pageIndex, int oldPageCount)
        {
            ValidatePageIndex(pageIndex);
            ValidatePageIndex(oldPageCount);

            var removedPageId = GetPageId(pageIndex);
            for (var i = pageIndex; i < oldPageCount; i++) _pageIds[i] = _pageIds[i + 1];
            _pageIds[oldPageCount] = null;
            foreach (var states in _states.Values) states.Remove(removedPageId);
            return removedPageId;
        }

        internal void RegisterProvider(
            string pluginId,
            IWhiteboardPageStateProvider provider,
            string currentPageId,
            Action<string, Exception> onError)
        {
            ValidateProvider(pluginId, provider);
            if (_providers.ContainsKey(pluginId))
                throw new InvalidOperationException($"插件 {pluginId} 已注册页面状态提供者。");

            _providers.Add(pluginId, provider);
            RestoreProvider(pluginId, provider, currentPageId, onError);
        }

        internal void UnregisterProvider(string pluginId)
        {
            if (string.IsNullOrWhiteSpace(pluginId)) return;
            _providers.Remove(pluginId);
            _legacyImporters.Remove(pluginId);
        }

        internal void RegisterLegacyImporter(string pluginId, IWhiteboardLegacyStateImporter importer)
        {
            if (string.IsNullOrWhiteSpace(pluginId))
                throw new ArgumentException("插件 ID 不能为空。", nameof(pluginId));
            if (importer == null) throw new ArgumentNullException(nameof(importer));
            if (_legacyImporters.ContainsKey(pluginId))
                throw new InvalidOperationException($"插件 {pluginId} 已注册旧页面状态导入器。");
            _legacyImporters.Add(pluginId, importer);
        }

        internal void ImportLegacyPageSidecars(
            string contentFilePath,
            int pageIndex,
            Action<string, Exception> onError)
        {
            ImportLegacyPage(
                pageIndex,
                importer => importer.TryImportPageSidecar(contentFilePath),
                onError);
        }

        internal void ImportLegacyPackagePages(
            string extractedDirectory,
            int firstPageIndex,
            int pageCount,
            Action<string, Exception> onError)
        {
            ValidateDocumentRange(firstPageIndex, pageCount);
            for (var pageIndex = firstPageIndex; pageIndex < firstPageIndex + pageCount; pageIndex++)
            {
                var capturedIndex = pageIndex;
                ImportLegacyPage(
                    pageIndex,
                    importer => importer.TryImportPackagePage(extractedDirectory, capturedIndex),
                    onError);
            }
        }

        internal void Capture(string pageId, Action<string, Exception> onError)
        {
            foreach (var pair in _providers.ToArray())
            {
                try
                {
                    var state = pair.Value.CaptureState();
                    if (!_states.TryGetValue(pair.Key, out var states))
                    {
                        states = new Dictionary<string, string>(StringComparer.Ordinal);
                        _states.Add(pair.Key, states);
                    }

                    if (state == null) states.Remove(pageId);
                    else states[pageId] = state;
                }
                catch (Exception ex)
                {
                    onError?.Invoke(pair.Key, ex);
                }
            }
        }

        internal void Restore(string pageId, Action<string, Exception> onError)
        {
            foreach (var pair in _providers.ToArray()) RestoreProvider(pair.Key, pair.Value, pageId, onError);
        }

        internal string ExportDocumentJson(int firstPageIndex, int pageCount)
        {
            ValidateDocumentRange(firstPageIndex, pageCount);
            var snapshot = new PluginWhiteboardDocumentSnapshot();
            for (var offset = 0; offset < pageCount; offset++)
            {
                var pageIndex = firstPageIndex + offset;
                var pageId = GetPageId(pageIndex);
                var page = new PluginWhiteboardPageSnapshot { Index = pageIndex, Id = pageId };
                foreach (var plugin in _states)
                {
                    if (plugin.Value.TryGetValue(pageId, out var state)) page.States[plugin.Key] = state;
                }
                snapshot.Pages.Add(page);
            }
            return JsonSerializer.Serialize(snapshot, JsonOptions);
        }

        internal void ImportDocumentJson(string json, int firstPageIndex, int pageCount)
        {
            ValidateDocumentRange(firstPageIndex, pageCount);
            var snapshot = JsonSerializer.Deserialize<PluginWhiteboardDocumentSnapshot>(json, JsonOptions)
                ?? throw new InvalidOperationException("Plugin document state is empty.");
            if (snapshot.SchemaVersion != PluginWhiteboardDocumentSnapshot.CurrentSchemaVersion)
                throw new InvalidOperationException("Plugin document state schema is unsupported.");
            if (snapshot.Pages == null || snapshot.Pages.Count != pageCount)
                throw new InvalidOperationException("Plugin document page count does not match the host document.");

            var pageIds = new HashSet<string>(StringComparer.Ordinal);
            var importedStates = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var page in snapshot.Pages)
            {
                if (page == null || page.Index < firstPageIndex || page.Index >= firstPageIndex + pageCount ||
                    string.IsNullOrWhiteSpace(page.Id) || !pageIds.Add(page.Id))
                    throw new InvalidOperationException("Plugin document contains an invalid page identity.");
                if (page.States == null) continue;
                foreach (var plugin in page.States)
                {
                    if (string.IsNullOrWhiteSpace(plugin.Key) || plugin.Value == null)
                        throw new InvalidOperationException("Plugin document contains an invalid plugin state.");
                    if (!importedStates.TryGetValue(plugin.Key, out var states))
                    {
                        states = new Dictionary<string, string>(StringComparer.Ordinal);
                        importedStates.Add(plugin.Key, states);
                    }
                    states[page.Id] = plugin.Value;
                }
            }

            for (var index = firstPageIndex; index < firstPageIndex + pageCount; index++) _pageIds[index] = null;
            foreach (var page in snapshot.Pages) _pageIds[page.Index] = page.Id;
            _states.Clear();
            foreach (var plugin in importedStates) _states.Add(plugin.Key, plugin.Value);
        }

        internal void ImportSinglePageJson(string json, int targetPageIndex)
        {
            ValidatePageIndex(targetPageIndex);
            var snapshot = JsonSerializer.Deserialize<PluginWhiteboardDocumentSnapshot>(json, JsonOptions)
                ?? throw new InvalidOperationException("Plugin document state is empty.");
            if (snapshot.SchemaVersion != PluginWhiteboardDocumentSnapshot.CurrentSchemaVersion ||
                snapshot.Pages == null || snapshot.Pages.Count != 1)
                throw new InvalidOperationException("Plugin single-page document state is invalid.");
            var page = snapshot.Pages[0];
            if (page == null || string.IsNullOrWhiteSpace(page.Id))
                throw new InvalidOperationException("Plugin single-page document identity is invalid.");

            var previousPageId = _pageIds[targetPageIndex];
            if (!string.IsNullOrEmpty(previousPageId))
                foreach (var states in _states.Values) states.Remove(previousPageId);
            _pageIds[targetPageIndex] = page.Id;
            if (page.States == null) return;
            foreach (var plugin in page.States)
            {
                if (string.IsNullOrWhiteSpace(plugin.Key) || plugin.Value == null)
                    throw new InvalidOperationException("Plugin document contains an invalid plugin state.");
                if (!_states.TryGetValue(plugin.Key, out var states))
                {
                    states = new Dictionary<string, string>(StringComparer.Ordinal);
                    _states.Add(plugin.Key, states);
                }
                states[page.Id] = plugin.Value;
            }
        }

        internal void ResetPage(int pageIndex)
        {
            ValidatePageIndex(pageIndex);
            var pageId = _pageIds[pageIndex];
            if (!string.IsNullOrEmpty(pageId))
                foreach (var states in _states.Values) states.Remove(pageId);
            _pageIds[pageIndex] = null;
        }

        internal void ResetDocument(int firstPageIndex, int pageCount)
        {
            ValidateDocumentRange(firstPageIndex, pageCount);
            for (var index = firstPageIndex; index < firstPageIndex + pageCount; index++) _pageIds[index] = null;
            _states.Clear();
        }

        internal bool HasPageState(int pageIndex)
        {
            ValidatePageIndex(pageIndex);
            var pageId = _pageIds[pageIndex];
            return !string.IsNullOrEmpty(pageId) && _states.Values.Any(states => states.ContainsKey(pageId));
        }

        internal IReadOnlyList<PluginWhiteboardInitialHistory> GetInitialHistories(
            int pageIndex,
            Action<string, Exception> onError = null)
        {
            ValidatePageIndex(pageIndex);
            var pageId = GetPageId(pageIndex);
            var result = new List<PluginWhiteboardInitialHistory>();
            foreach (var pair in _providers)
            {
                if (pair.Value is not IWhiteboardInitialHistoryProvider initialProvider ||
                    !_states.TryGetValue(pair.Key, out var states) ||
                    !states.TryGetValue(pageId, out var loadedState))
                    continue;
                try
                {
                    var emptyState = initialProvider.CaptureEmptyState();
                    if (emptyState == null || string.Equals(emptyState, loadedState, StringComparison.Ordinal))
                        continue;
                    result.Add(new PluginWhiteboardInitialHistory
                    {
                        PluginId = pair.Key,
                        EmptyState = emptyState,
                        LoadedState = loadedState
                    });
                }
                catch (Exception ex)
                {
                    onError?.Invoke(pair.Key, ex);
                }
            }
            return result;
        }

        internal IReadOnlyList<PluginWhiteboardCompanionState> GetCompanionStates(
            int pageIndex,
            Action<string, Exception> onError = null)
        {
            ValidatePageIndex(pageIndex);
            var pageId = GetPageId(pageIndex);
            var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<PluginWhiteboardCompanionState>();
            foreach (var pair in _providers)
            {
                if (pair.Value is not IWhiteboardCompanionStateProvider companionProvider ||
                    !_states.TryGetValue(pair.Key, out var states) ||
                    !states.TryGetValue(pageId, out var state))
                    continue;
                try
                {
                    var extension = companionProvider.CompanionFileExtension;
                    ValidateCompanionExtension(extension);
                    if (!extensions.Add(extension))
                        throw new InvalidOperationException($"Companion file extension is already registered: {extension}");
                    var content = companionProvider.ExportCompanionState(state);
                    if (content == null) continue;
                    result.Add(new PluginWhiteboardCompanionState
                    {
                        PluginId = pair.Key,
                        FileExtension = extension,
                        Content = content
                    });
                }
                catch (Exception ex)
                {
                    onError?.Invoke(pair.Key, ex);
                }
            }
            return result;
        }

        private void ImportLegacyPage(
            int pageIndex,
            Func<IWhiteboardLegacyStateImporter, string> import,
            Action<string, Exception> onError)
        {
            ValidatePageIndex(pageIndex);
            var pageId = GetPageId(pageIndex);
            foreach (var pair in _legacyImporters.ToArray())
            {
                if (_states.TryGetValue(pair.Key, out var existing) && existing.ContainsKey(pageId))
                    continue;
                try
                {
                    var state = import(pair.Value);
                    if (state == null) continue;
                    if (!_states.TryGetValue(pair.Key, out var states))
                    {
                        states = new Dictionary<string, string>(StringComparer.Ordinal);
                        _states.Add(pair.Key, states);
                    }
                    states[pageId] = state;
                }
                catch (Exception ex)
                {
                    onError?.Invoke(pair.Key, ex);
                }
            }
        }

        private void RestoreProvider(
            string pluginId,
            IWhiteboardPageStateProvider provider,
            string pageId,
            Action<string, Exception> onError)
        {
            try
            {
                string state = null;
                if (_states.TryGetValue(pluginId, out var states)) states.TryGetValue(pageId, out state);
                provider.RestoreState(state);
            }
            catch (Exception ex)
            {
                onError?.Invoke(pluginId, ex);
            }
        }

        private void ValidatePageIndex(int pageIndex)
        {
            if (pageIndex < 0 || pageIndex >= _pageIds.Length)
                throw new ArgumentOutOfRangeException(nameof(pageIndex));
        }

        private void ValidateDocumentRange(int firstPageIndex, int pageCount)
        {
            if (pageCount <= 0) throw new ArgumentOutOfRangeException(nameof(pageCount));
            ValidatePageIndex(firstPageIndex);
            ValidatePageIndex(firstPageIndex + pageCount - 1);
        }

        private static void ValidateProvider(string pluginId, IWhiteboardPageStateProvider provider)
        {
            if (string.IsNullOrWhiteSpace(pluginId))
                throw new ArgumentException("插件 ID 不能为空。", nameof(pluginId));
            if (provider == null) throw new ArgumentNullException(nameof(provider));
        }

        private static void ValidateCompanionExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension) || extension[0] != '.' ||
                extension.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                extension.Contains("/") || extension.Contains("\\") ||
                !string.Equals(Path.GetFileName(extension), extension, StringComparison.Ordinal))
                throw new InvalidOperationException("Companion file extension is invalid.");
        }
    }

    internal sealed class PluginWhiteboardDocumentSnapshot
    {
        internal const int CurrentSchemaVersion = 1;
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        public List<PluginWhiteboardPageSnapshot> Pages { get; set; } = new List<PluginWhiteboardPageSnapshot>();
    }

    internal sealed class PluginWhiteboardPageSnapshot
    {
        public int Index { get; set; }
        public string Id { get; set; }
        public Dictionary<string, string> States { get; set; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    internal sealed class PluginWhiteboardInitialHistory
    {
        public string PluginId { get; set; }
        public string EmptyState { get; set; }
        public string LoadedState { get; set; }
    }

    internal sealed class PluginWhiteboardCompanionState
    {
        public string PluginId { get; set; }
        public string FileExtension { get; set; }
        public string Content { get; set; }
    }
}
