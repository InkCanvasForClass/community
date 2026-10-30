using System;

namespace Ink_Canvas.Services
{
    /// <summary>
    /// UI 线程调度抽象。
    /// 业务服务经本接口封送 UI 线程调用，禁止直接引用 UI 线程调度器；
    /// 唯一实现 <see cref="WpfSyncService"/> 是 Services/ 内唯一持有调度器的类型。
    /// </summary>
    internal interface ISyncService
    {
        /// <summary>在 UI 线程上同步执行委托（等价 UI 调度器 Invoke）。</summary>
        void Invoke(Action action);

        /// <summary>在 UI 线程上以默认优先级异步执行委托（等价 UI 调度器 BeginInvoke）。</summary>
        void BeginInvoke(Action action);

        /// <summary>在 UI 线程上以指定优先级异步执行委托（等价带优先级的 BeginInvoke）。</summary>
        void BeginInvoke(SyncPriority priority, Action action);
    }

    /// <summary>
    /// UI 调度优先级。与 WPF 调度优先级类型解耦（Services/ 内不得出现调度器字样），
    /// 仅收录已使用的取值，后续按需扩充（YAGNI）。
    /// </summary>
    internal enum SyncPriority
    {
        /// <summary>UI 线程空闲时执行（等价 UI 调度器空闲优先级）。</summary>
        ContextIdle
    }
}
