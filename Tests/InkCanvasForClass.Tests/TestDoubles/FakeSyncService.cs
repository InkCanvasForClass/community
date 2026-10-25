using System;
using System.Collections.Generic;
using Ink_Canvas.Services;

namespace InkCanvasForClass.Tests.TestDoubles
{
    /// <summary>
    /// <see cref="ISyncService"/> 的测试双件（手写，零新依赖）。
    /// <see cref="Invoke"/> 直接同步执行；两个 <see cref="BeginInvoke(Action)"/> 重载
    /// 只把回调记入待决队列、不执行，测试可断言"发起了多少次异步调用"，
    /// 并可用 <see cref="RunPending"/> 显式排空。不触碰真实 UI 调度器。
    /// </summary>
    internal sealed class FakeSyncService : ISyncService
    {
        private readonly Queue<Action> _pending = new Queue<Action>();

        /// <summary><see cref="Invoke"/> 被调用的次数。</summary>
        public int InvokeCount { get; private set; }

        /// <summary>两个 BeginInvoke 重载合计被调用的次数。</summary>
        public int BeginInvokeCount { get; private set; }

        /// <summary>已入队、尚未经 <see cref="RunPending"/> 执行的回调数。</summary>
        public int PendingCount => _pending.Count;

        /// <summary>同步执行 <paramref name="action"/>（等价已处于 UI 线程）。</summary>
        public void Invoke(Action action)
        {
            InvokeCount++;
            action();
        }

        /// <summary>记录回调但不执行。</summary>
        public void BeginInvoke(Action action)
        {
            BeginInvokeCount++;
            _pending.Enqueue(action);
        }

        /// <summary>记录回调但不执行；优先级仅记录语义，双件不按优先级排序。</summary>
        public void BeginInvoke(SyncPriority priority, Action action)
        {
            BeginInvokeCount++;
            _pending.Enqueue(action);
        }

        /// <summary>按入队顺序执行全部待决回调，返回执行条数。</summary>
        public int RunPending()
        {
            var ran = 0;
            while (_pending.Count > 0)
            {
                _pending.Dequeue()();
                ran++;
            }
            return ran;
        }
    }
}
