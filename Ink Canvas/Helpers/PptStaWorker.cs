using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// Bounded Office COM worker. A hung COM call occupies one background thread,
    /// never the UI and never a new thread for every subsequent button press.
    /// </summary>
    internal static class PptStaWorker
    {
        private static readonly TaskCompletionSource<Dispatcher> Ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private static int _pending;
        static PptStaWorker()
        {
            var thread = new Thread(() =>
            {
                // Office connection points and COM reentrancy require an STA message pump.
                Ready.SetResult(Dispatcher.CurrentDispatcher);
                Dispatcher.Run();
            }) { IsBackground = true, Name = "ICC Office COM worker" };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        internal static async Task<T> RunAsync<T>(Func<T> action, CancellationToken cancellationToken = default)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            var token = timeout.Token;
            if (Interlocked.Increment(ref _pending) > 8)
            {
                Interlocked.Decrement(ref _pending);
                throw new InvalidOperationException("Office worker queue is full.");
            }
            try
            {
                var dispatcher = await Ready.Task.WaitAsync(token).ConfigureAwait(false);
                _ = dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                {
                    try
                    {
                        if (token.IsCancellationRequested) { completion.TrySetCanceled(token); return; }
                        completion.TrySetResult(action());
                    }
                    catch (Exception ex) { completion.TrySetException(ex); }
                    finally { Interlocked.Decrement(ref _pending); }
                }));
            }
            catch { Interlocked.Decrement(ref _pending); throw; }
            // Observe eventual errors even when the caller has already timed out.
            _ = completion.Task.ContinueWith(t => { _ = t.Exception; },
                CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
            return await completion.Task.WaitAsync(token).ConfigureAwait(false);
        }
    }
}
