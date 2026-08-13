using System;

namespace Ink_Canvas.Plugins
{
    public sealed class WhiteboardPageInfo
    {
        public string Id { get; set; }
        public int Index { get; set; }
        public int Count { get; set; }
        public bool IsFrozen { get; set; }
    }

    public sealed class WhiteboardPageChangingEventArgs : EventArgs
    {
        public WhiteboardPageInfo CurrentPage { get; set; }
        public WhiteboardPageInfo TargetPage { get; set; }
    }

    public sealed class WhiteboardPageChangedEventArgs : EventArgs
    {
        public WhiteboardPageInfo PreviousPage { get; set; }
        public WhiteboardPageInfo CurrentPage { get; set; }
    }

    public sealed class WhiteboardPageRemovedEventArgs : EventArgs
    {
        public WhiteboardPageInfo RemovedPage { get; set; }
        public WhiteboardPageInfo CurrentPage { get; set; }
    }

    public interface IWhiteboardPageStateProvider
    {
        string CaptureState();
        void RestoreState(string state);
    }

    /// <summary>
    /// 可选契约：文档载入已有插件状态时，为宿主时间机器提供空白基线。
    /// </summary>
    public interface IWhiteboardInitialHistoryProvider
    {
        string CaptureEmptyState();
    }

    /// <summary>
    /// 可选契约：把某页已捕获的插件状态导出为与宿主文档并列的兼容文件。
    /// 扩展名必须是无目录部分的复合扩展名，例如 ".feature.json"。
    /// </summary>
    public interface IWhiteboardCompanionStateProvider
    {
        string CompanionFileExtension { get; }
        string ExportCompanionState(string state);
    }

    /// <summary>
    /// Optional importer for page state written by a feature before it became a plugin.
    /// Returning null means that no compatible legacy state exists at the supplied location.
    /// </summary>
    public interface IWhiteboardLegacyStateImporter
    {
        string TryImportPageSidecar(string contentFilePath);
        string TryImportPackagePage(string extractedDirectory, int pageIndex);
    }

    public interface IWhiteboardDocumentService
    {
        WhiteboardPageInfo CurrentPage { get; }

        event EventHandler<WhiteboardPageChangingEventArgs> PageChanging;
        event EventHandler<WhiteboardPageChangedEventArgs> PageChanged;
        event EventHandler<WhiteboardPageRemovedEventArgs> PageRemoved;

        /// <summary>用户确认清空当前白板页时触发；代码驱动的切页和加载清理不触发。</summary>
        event EventHandler PageClearing;

        /// <summary>
        /// 请求修改当前普通白板页。非白板或冻结页返回 false；冻结页提示由宿主统一处理。
        /// </summary>
        bool TryBeginMutation(string action);

        void RegisterPageStateProvider(string pluginId, IWhiteboardPageStateProvider provider);
        void RegisterLegacyStateImporter(string pluginId, IWhiteboardLegacyStateImporter importer);
        void UnregisterPageStateProvider(string pluginId);
    }
}
