using Ink_Canvas.Helpers;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Ink_Canvas.Plugins.Services
{
    /// <summary>
    /// 外部演示源服务实现：允许插件接管放映模式，把自己的文档（PDF、图片集等）
    /// 接入宿主的翻页 UI 与放映布局。
    /// </summary>
    internal class PresentationSourceService : IPresentationSourceService
    {
        private readonly MainWindow _mainWindow;
        private readonly Dispatcher _dispatcher;
        private PresentationSourceDescriptor _activeSource;
        private readonly object _lock = new();

        public bool IsActive
        {
            get { lock (_lock) return _activeSource != null; }
        }

        public int PageCount
        {
            get { lock (_lock) return _activeSource?.PageCount ?? 0; }
        }

        public int CurrentPage { get; private set; }

        public event Action<string> Ended;

        public PresentationSourceService(MainWindow mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _dispatcher = mainWindow.Dispatcher;
        }

        public Task<bool> BeginAsync(PresentationSourceDescriptor descriptor,
            CancellationToken cancellationToken = default)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            if (descriptor.PageCount <= 0)
                throw new ArgumentException("PageCount must be greater than 0.", nameof(descriptor));
            if (descriptor.NavigateAsync == null)
                throw new ArgumentException("NavigateAsync callback is required.", nameof(descriptor));

            return _dispatcher.InvokeAsync(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                // 真实 PPT 放映中拒绝外部演示源，避免 UI 冲突
                if (_mainWindow.PPTManager?.IsInSlideShow == true)
                {
                    LogHelper.WriteLogToFile(
                        $"拒绝外部演示源 [{descriptor.Id}]：PowerPoint 正在放映中。",
                        LogHelper.LogType.Warning);
                    return false;
                }

                // 结束之前的外部演示源（若存在）
                if (_activeSource != null)
                {
                    EndAsyncCore(_activeSource.Id);
                }

                lock (_lock)
                {
                    _activeSource = descriptor;
                    CurrentPage = Math.Max(1, Math.Min(descriptor.CurrentPage, descriptor.PageCount));
                }

                // 触发宿主放映模式：设置标志 + 显示翻页条 + 工具栏布局切换
                _mainWindow.IsInPPTPresentationMode = true;
                _mainWindow.ArePPTControlsVisible = true;
                _mainWindow.UpdateToolbarComponentVisibility();

                // 浮动栏像真实 PPT 放映一样重新定位（居中、缩进）。
                _mainWindow.UpdateToolbarPosition();

                _mainWindow.PPTUIManager?.UpdateSlideShowStatus(
                    isInSlideShow: true,
                    currentSlide: CurrentPage,
                    totalSlides: descriptor.PageCount);

                // 抑制 PPT 时间胶囊与快捷面板（它们依赖 PPTManager 数据）
                _mainWindow.UpdatePPTTimeCapsuleVisibility();
                _mainWindow.UpdatePPTQuickPanelVisibility();

                LogHelper.WriteLogToFile(
                    $"外部演示源 [{descriptor.Id}] 已激活：{CurrentPage}/{descriptor.PageCount} 页。",
                    LogHelper.LogType.Info);

                return true;
            }).Task;
        }

        public Task EndAsync(string sourceId = null, CancellationToken cancellationToken = default)
        {
            return _dispatcher.InvokeAsync(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                string activeId;
                lock (_lock)
                {
                    if (_activeSource == null) return;
                    activeId = _activeSource.Id;

                    // sourceId 校验：防止插件误关掉别人的放映
                    if (!string.IsNullOrEmpty(sourceId) && sourceId != activeId)
                    {
                        LogHelper.WriteLogToFile(
                            $"外部演示源 [{sourceId}] 尝试结束，但当前激活的是 [{activeId}]，已忽略。",
                            LogHelper.LogType.Warning);
                        return;
                    }
                }

                EndAsyncCore(activeId);
            }).Task;
        }

        /// <summary>内部结束逻辑，必须在 UI 线程调用。</summary>
        private void EndAsyncCore(string activeId)
        {
            lock (_lock)
            {
                if (_activeSource == null || _activeSource.Id != activeId) return;
                _activeSource = null;
                CurrentPage = 0;
            }

            _mainWindow.IsInPPTPresentationMode = false;
            _mainWindow.ArePPTControlsVisible = false;
            _mainWindow.UpdateToolbarComponentVisibility();
            _mainWindow.PPTUIManager?.UpdateSlideShowStatus(isInSlideShow: false);
            _mainWindow.PPTUIManager?.HideAllNavigationPanels();
            _mainWindow.UpdatePPTTimeCapsuleVisibility();
            _mainWindow.UpdatePPTQuickPanelVisibility();

            // 浮动栏恢复到桌面模式的定位。
            _mainWindow.UpdateToolbarPosition();

            LogHelper.WriteLogToFile(
                $"外部演示源 [{activeId}] 已结束。",
                LogHelper.LogType.Info);

            try
            {
                Ended?.Invoke(activeId);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile(
                    $"外部演示源 Ended 事件触发异常: {ex}",
                    LogHelper.LogType.Error);
            }
        }

        public Task UpdatePageAsync(int currentPage, int pageCount = 0,
            CancellationToken cancellationToken = default)
        {
            return _dispatcher.InvokeAsync(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                lock (_lock)
                {
                    if (_activeSource == null) return;

                    if (pageCount > 0)
                        _activeSource.PageCount = pageCount;

                    CurrentPage = Math.Max(1, Math.Min(currentPage, _activeSource.PageCount));
                }

                _mainWindow.PPTUIManager?.UpdateCurrentSlideNumber(CurrentPage, PageCount);
            }).Task;
        }

        /// <summary>
        /// 宿主翻页条被点击时调用（由 MW_PPT 路由）。
        /// 回调插件，成功后读取新页码并同步 UI。
        /// </summary>
        internal async Task<bool> HandleNavigationAsync(PresentationNavigation direction,
            CancellationToken cancellationToken = default)
        {
            PresentationSourceDescriptor descriptor;
            lock (_lock)
            {
                if (_activeSource == null) return false;
                descriptor = _activeSource;
            }

            try
            {
                // 回调返回新页码（1-based）；<=0 表示已到边界或失败。
                int newPage = await descriptor.NavigateAsync(direction, cancellationToken)
                    .ConfigureAwait(false);
                if (newPage <= 0) return false;

                await UpdatePageAsync(newPage, 0, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile(
                    $"外部演示源 [{descriptor.Id}] 翻页回调异常: {ex}",
                    LogHelper.LogType.Error);
                return false;
            }
        }

        /// <summary>
        /// 宿主需要强制结束外部演示源时调用（例如真实 PPT 开始放映、程序退出）。
        /// </summary>
        internal void ForceEnd(string reason)
        {
            string activeId;
            lock (_lock)
            {
                if (_activeSource == null) return;
                activeId = _activeSource.Id;
            }

            LogHelper.WriteLogToFile(
                $"外部演示源 [{activeId}] 被强制结束：{reason}",
                LogHelper.LogType.Warning);

            EndAsyncCore(activeId);
        }

        /// <summary>当前外部演示源是否禁用页码点击。</summary>
        internal bool IsPageNumberClickDisabled()
        {
            lock (_lock)
            {
                return _activeSource != null && !_activeSource.AllowPageNumberClick;
            }
        }
    }
}
