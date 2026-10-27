using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using InkCanvasPPTAgent.Contracts;

namespace InkCanvas.PowerPointAddIn.IPC
{
    public sealed class PipeHost : IDisposable
    {
        private readonly Func<string, string> _dispatch;
        private readonly SemaphoreSlim _sendGate = new SemaphoreSlim(1, 1);
        private int _queuedFrames;
        private CancellationTokenSource _cts;
        private NamedPipeServerStream _currentPipe;
        private volatile bool _clientConnected;
        private bool _disposed;

        public bool IsEnabled => _clientConnected;

        public PipeHost(Func<string, string> dispatch)
        {
            _dispatch = dispatch;
        }

        /// <summary>
        /// 向当前连接的客户端主动推送消息（状态/事件）。
        /// 线程安全，可从任意线程调用。
        /// </summary>
        public void SendFrame(string json)
        {
            var pipe = _currentPipe;
            if (pipe == null || !pipe.IsConnected || string.IsNullOrEmpty(json)) return;
            if (Interlocked.Increment(ref _queuedFrames) > 64)
            {
                Interlocked.Decrement(ref _queuedFrames);
                return;
            }
            _ = SendFrameAsync(pipe, json);
        }

        private async Task SendFrameAsync(NamedPipeServerStream pipe, string json)
        {
            bool entered = false;
            using (var timeout = new CancellationTokenSource(PipeConstants.RequestTimeoutMilliseconds))
            {
                try
                {
                    await _sendGate.WaitAsync(timeout.Token).ConfigureAwait(false);
                    entered = true;
                    using (timeout.Token.Register(() => { try { pipe.Dispose(); } catch { } }))
                    {
                        if (!ReferenceEquals(pipe, _currentPipe)) return;
                        await InkCanvasPPTAgent.Contracts.PipeFrame.WriteFrameAsync(pipe, json, timeout.Token).ConfigureAwait(false);
                    }
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
                finally
                {
                    if (entered) _sendGate.Release();
                    Interlocked.Decrement(ref _queuedFrames);
                }
            }
        }

        public void Start()
        {
            if (_cts != null) return;
            _cts = new CancellationTokenSource();
            Task.Run(() => AcceptLoop(_cts.Token));
        }

        public void Stop()
        {
            try { _cts?.Cancel(); } catch { }
            try { _currentPipe?.Dispose(); } catch { }
            _cts?.Dispose();
            _cts = null;
            _clientConnected = false;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
        }

        private async Task AcceptLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                NamedPipeServerStream pipe = null;
                try
                {
                    pipe = new NamedPipeServerStream(
                        PipeConstants.PipeName,
                        PipeDirection.InOut,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await pipe.WaitForConnectionAsync(token).ConfigureAwait(false);
                    _currentPipe = pipe;
                    _clientConnected = true;
                    System.Diagnostics.Debug.WriteLine("ICC PPT Agent: client connected");

                    HandleClient(pipe, token);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ICC PPT Agent pipe error: {ex.Message}");
                }
                finally
                {
                    _clientConnected = false;
                    _currentPipe = null;
                    try { pipe?.Dispose(); } catch { }
                }
            }
        }

        private void HandleClient(NamedPipeServerStream pipe, CancellationToken token)
        {
            while (!token.IsCancellationRequested && pipe.IsConnected)
            {
                try
                {
                    string requestJson = PipeFrame.ReadFrame(pipe);
                    string responseJson = _dispatch.Invoke(requestJson);
                    if (!string.IsNullOrEmpty(responseJson))
                    {
                        SendFrame(responseJson);
                    }
                }
                catch (IOException) { break; }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ICC PPT Agent handle error: {ex.Message}");
                    break;
                }
            }
        }
    }
}
