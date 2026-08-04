using System;
using System.Collections.Generic;
using System.Threading;

namespace Ink_Canvas.Ink.Native
{
    /// <summary>
    /// 把 NativeInkController 的 Update+Prediction 调用统一收口。
    ///
    /// 早期版本用独立 worker 线程做异步 Update；但 NativePointerInputSource.WndProc
    /// 本身就在 UI 线程同步回调，worker 只是把同一线程的调用再搬去后台线程，
    /// 额外引入 1 次线程切换 + 2 次锁 + 跨线程 mailbox 隔离，对端到端延迟是净负。
    /// 现在改为同步执行：Enqueue 直接在调用线程跑 TryUpdateSessionWithPrediction，
    /// FlushPointer/DiscardPointer/DiscardAll 退化为 no-op（没有在途异步更新可冲刷）。
    /// </summary>
    internal sealed class NativePointerUpdatePump : IDisposable
    {
        private readonly NativeInkController _controller;
        private readonly Action _signalWork;
        private bool _disposed;

        public NativePointerUpdatePump(NativeInkController controller, Action signalWork)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _signalWork = signalWork ?? throw new ArgumentNullException(nameof(signalWork));
        }

        public void Enqueue(
            uint pointerId,
            long sessionId,
            RawInkSample[] samplesNewestFirst,
            bool predictionEnabled)
        {
            if (samplesNewestFirst == null)
                throw new ArgumentNullException(nameof(samplesNewestFirst));
            if (samplesNewestFirst.Length == 0)
                return;

            ThrowIfDisposed();
            // 同步执行：预测 + snapshot 构造 + mailbox publish 全在当前（UI）线程完成，
            // 省去 worker 线程切换与跨线程 mailbox 隔离。
            if (_controller.TryUpdateSessionWithPrediction(
                    pointerId,
                    sessionId,
                    samplesNewestFirst,
                    predictionEnabled))
            {
                _signalWork();
            }
        }

        /// <summary>
        /// 同步路径下没有在途异步更新，保留该方法只为兼容既有调用方（抬笔/取消前冲刷）。
        /// </summary>
        public void FlushPointer(uint pointerId)
        {
            ThrowIfDisposed();
            // no-op：无在途异步更新可冲刷。
        }

        /// <summary>
        /// 同步路径下没有在途异步更新，保留该方法只为兼容既有调用方。
        /// </summary>
        public void DiscardPointer(uint pointerId)
        {
            ThrowIfDisposed();
            // no-op：无在途异步更新可丢弃。
        }

        /// <summary>
        /// 同步路径下没有在途异步更新，保留该方法只为兼容既有调用方。
        /// </summary>
        public void DiscardAll()
        {
            ThrowIfDisposed();
            // no-op：无在途异步更新可丢弃。
        }

        /// <summary>
        /// 同步路径下没有在途异步更新，保留该方法只为兼容既有调用方。
        /// </summary>
        public void FlushAll()
        {
            ThrowIfDisposed();
            // no-op：无在途异步更新可冲刷。
        }

        public void Dispose()
        {
            _disposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(NativePointerUpdatePump));
        }
    }
}
