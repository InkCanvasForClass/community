using System;
using System.Windows.Threading;

namespace Ink_Canvas.Services
{
    /// <summary>
    /// <see cref="ISyncService"/> 的 WPF 实现：构造时捕获当前 UI 线程的 Dispatcher。
    /// 本类是 Services/ 内唯一允许引用 Dispatcher 的类型。
    /// </summary>
    internal sealed class WpfSyncService : ISyncService
    {
        private readonly Dispatcher _dispatcher;

        /// <summary>必须在 UI 线程上构造（捕获 Dispatcher.CurrentDispatcher）。</summary>
        public WpfSyncService()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
        }

        public void Invoke(Action action) => _dispatcher.Invoke(action);

        public void BeginInvoke(Action action) => _dispatcher.BeginInvoke(action);

        public void BeginInvoke(SyncPriority priority, Action action)
            => _dispatcher.BeginInvoke(ToDispatcherPriority(priority), action);

        private static DispatcherPriority ToDispatcherPriority(SyncPriority priority) => priority switch
        {
            SyncPriority.ContextIdle => DispatcherPriority.ContextIdle,
            _ => throw new ArgumentOutOfRangeException(nameof(priority), priority, null)
        };
    }
}
