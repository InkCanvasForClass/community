using Ink_Canvas.Helpers;
using InkCanvasPPTAgent.Contracts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace Ink_Canvas.IPC
{
    public sealed class PPTAgentPipeClient : IDisposable
    {
        private readonly SemaphoreSlim _sendGate = new SemaphoreSlim(1, 1);
        private int _queuedCommands;
        private readonly ConcurrentDictionary<string, TaskCompletionSource<JToken>> _pendingRequests = new ConcurrentDictionary<string, TaskCompletionSource<JToken>>();

        private CancellationTokenSource _cts;
        private NamedPipeClientStream _pipe;
        private Task _connectLoopTask;
        private volatile bool _isConnected;
        private bool _disposed;

        public event Action<bool> ConnectionChanged;
        public event Action<PPTState> StateReceived;
        public event Action<string, PPTState> EventReceived;

        public bool IsConnected => _isConnected && _pipe?.IsConnected == true;

        public void Start()
        {
            if (_disposed) return;
            if (_cts != null) return;

            _cts = new CancellationTokenSource();
            _connectLoopTask = Task.Run(() => ConnectLoop(_cts.Token));
            LogHelper.WriteLogToFile("[PPT-Agent] Pipe 客户端连接循环已启动", LogHelper.LogType.Info);
        }

        public void Stop()
        {
            try
            {
                var cts = _cts;
                _cts = null;
                cts?.Cancel();
                ClosePipe();
                FailPendingRequests(new IOException("PPT Agent pipe stopped."));
                cts?.Dispose();
                LogHelper.WriteLogToFile("[PPT-Agent] Pipe 客户端已停止", LogHelper.LogType.Info);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"停止 PPT Agent Pipe 客户端失败: {ex}", LogHelper.LogType.Warning);
            }
        }

        public bool SendCommand(string command, object data = null)
        {
            var message = new PPTPipeMessage<object>
            {
                Type = PPTMessageTypes.Command,
                Cmd = command,
                Data = data,
                RequestId = Guid.NewGuid().ToString("N")
            };

            // Commands are accepted into a bounded async sender; never wait for a pipe
            // consumer on a WPF click/event thread.
            if (!IsConnected) return false;
            if (Interlocked.Increment(ref _queuedCommands) > 64)
            {
                Interlocked.Decrement(ref _queuedCommands);
                return false;
            }
            _ = SendQueuedCommandAsync(message);
            var sent = true;
            LogHelper.WriteLogToFile(
                $"[PPT-Agent] 命令{(sent ? "已发送" : "发送失败")}: {command}",
                sent ? LogHelper.LogType.Info : LogHelper.LogType.Warning);
            return sent;
        }

        private async Task SendQueuedCommandAsync(PPTPipeMessage<object> message)
        {
            try { await TrySendMessageAsync(message, CancellationToken.None).ConfigureAwait(false); }
            finally { Interlocked.Decrement(ref _queuedCommands); }
        }

        public async Task<T> SendRequestAsync<T>(string command, object data = null, int timeoutMilliseconds = PipeConstants.RequestTimeoutMilliseconds)
        {
            var requestId = Guid.NewGuid().ToString("N");
            var tcs = new TaskCompletionSource<JToken>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingRequests[requestId] = tcs;

            var message = new PPTPipeMessage<object>
            {
                Type = PPTMessageTypes.Command,
                Cmd = command,
                Data = data,
                RequestId = requestId
            };

            using var timeout = new CancellationTokenSource(timeoutMilliseconds);
            var connection = _pipe;
            try
            {
                if (!await TrySendMessageAsync(message, timeout.Token).ConfigureAwait(false)) return default;
                var completed = await Task.WhenAny(tcs.Task, Task.Delay(Timeout.Infinite, timeout.Token))
                    .ConfigureAwait(false);

                if (completed != tcs.Task)
                {
                    _pendingRequests.TryRemove(requestId, out _);
                    if (connection != null && ReferenceEquals(Interlocked.CompareExchange(ref _pipe, null, connection), connection))
                    {
                        connection.Dispose();
                        SetConnected(false);
                    }
                    LogHelper.WriteLogToFile($"PPT Agent 命令超时: {command}", LogHelper.LogType.Warning);
                    return default;
                }

                var token = await tcs.Task.ConfigureAwait(false);
                if (token == null || token.Type == JTokenType.Null)
                    return default;

                return token.ToObject<T>();
            }
            catch (Exception ex)
            {
                _pendingRequests.TryRemove(requestId, out _);
                LogHelper.WriteLogToFile($"PPT Agent 请求失败 [{command}]: {ex}", LogHelper.LogType.Warning);
                return default;
            }
            finally
            {
                _pendingRequests.TryRemove(requestId, out _);
            }
        }

        /// <summary>
        /// Synchronous request. Prefer <see cref="SendRequestAsync{T}"/> to avoid sync-over-async deadlocks.
        /// </summary>
        public T SendRequest<T>(string command, object data = null, int timeoutMilliseconds = PipeConstants.RequestTimeoutMilliseconds)
        {
            try
            {
                return SendRequestAsync<T>(command, data, timeoutMilliseconds).ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"PPT Agent 请求失败 [{command}]: {ex}", LogHelper.LogType.Warning);
                return default;
            }
        }

        private async Task ConnectLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                NamedPipeClientStream pipe = null;
                try
                {
                    pipe = new NamedPipeClientStream(".", PipeConstants.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                    await pipe.ConnectAsync(PipeConstants.ConnectTimeoutMilliseconds, token).ConfigureAwait(false);

                    _pipe = pipe;
                    SetConnected(true);
                    LogHelper.WriteLogToFile("[PPT-Agent] Pipe 已连接", LogHelper.LogType.Info);
                    SendCommand(PPTCommands.State);

                    Listen(pipe, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (TimeoutException)
                {
                    pipe?.Dispose();
                }
                catch (IOException)
                {
                    pipe?.Dispose();
                }
                catch (Exception ex)
                {
                    pipe?.Dispose();
                    LogHelper.WriteLogToFile($"PPT Agent Pipe 连接循环异常: {ex.Message}", LogHelper.LogType.Trace);
                }
                finally
                {
                    if (ReferenceEquals(_pipe, pipe))
                        _pipe = null;
                    SetConnected(false);
                    FailPendingRequests(new IOException("PPT Agent pipe disconnected."));
                }

                if (!token.IsCancellationRequested)
                {
                    try { await Task.Delay(1000, token).ConfigureAwait(false); }
                    catch (OperationCanceledException) { break; }
                }
            }
        }

        private void Listen(NamedPipeClientStream pipe, CancellationToken token)
        {
            while (!token.IsCancellationRequested && pipe.IsConnected)
            {
                var json = PipeFrame.ReadFrame(pipe);
                DispatchMessage(json);
            }
        }

        private void DispatchMessage(string json)
        {
            try
            {
                var obj = JObject.Parse(json);
                var type = obj.Value<string>(nameof(PPTPipeMessage<object>.Type));
                var command = obj.Value<string>(nameof(PPTPipeMessage<object>.Cmd));
                var requestId = obj.Value<string>(nameof(PPTPipeMessage<object>.RequestId));
                var data = obj[nameof(PPTPipeMessage<object>.Data)];

                if ((type == PPTMessageTypes.Response || type == PPTMessageTypes.Error) && !string.IsNullOrEmpty(requestId))
                {
                    if (_pendingRequests.TryRemove(requestId, out var tcs))
                    {
                        if (type == PPTMessageTypes.Error)
                        {
                            var error = obj.Value<string>(nameof(PPTPipeMessage<object>.Error)) ?? "PPT Agent command failed.";
                            tcs.TrySetException(new InvalidOperationException(error));
                        }
                        else
                        {
                            tcs.TrySetResult(data);
                        }
                    }
                    return;
                }

                if (type == PPTMessageTypes.State)
                {
                    var state = data?.ToObject<PPTState>();
                    if (state != null)
                        StateReceived?.Invoke(state);
                    return;
                }

                if (type == PPTMessageTypes.Event)
                {
                    var state = data?.ToObject<PPTState>();
                    EventReceived?.Invoke(command, state);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"解析 PPT Agent Pipe 消息失败: {ex}", LogHelper.LogType.Warning);
            }
        }

        private async Task<bool> TrySendMessageAsync<T>(PPTPipeMessage<T> message, CancellationToken cancellationToken)
        {
            var pipe = _pipe;
            bool entered = false;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(PipeConstants.RequestTimeoutMilliseconds);
            try
            {
                if (pipe == null || !pipe.IsConnected)
                    return false;

                var json = JsonConvert.SerializeObject(message);
                await _sendGate.WaitAsync(timeout.Token).ConfigureAwait(false);
                entered = true;
                // A canceled/partial frame cannot be reused by the next sender.
                using (timeout.Token.Register(() => { try { pipe.Dispose(); } catch { } }))
                {
                    if (!ReferenceEquals(pipe, _pipe) || !pipe.IsConnected)
                        return false;
                    await PipeFrame.WriteFrameAsync(pipe, json, timeout.Token).ConfigureAwait(false);
                }
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"发送 PPT Agent Pipe 消息失败: {ex.Message}", LogHelper.LogType.Warning);
                if (entered && ReferenceEquals(Interlocked.CompareExchange(ref _pipe, null, pipe), pipe))
                {
                    pipe?.Dispose();
                    SetConnected(false);
                }
                return false;
            }
            finally { if (entered) _sendGate.Release(); }
        }

        private void SetConnected(bool connected)
        {
            if (_isConnected == connected) return;
            _isConnected = connected;
            LogHelper.WriteLogToFile($"[PPT-Agent] Pipe 状态变化: connected={connected}", LogHelper.LogType.Info);
            ConnectionChanged?.Invoke(connected);
        }

        private void ClosePipe()
        {
            try { _pipe?.Dispose(); }
            catch (Exception ex) { LogHelper.WriteLogToFile($"[PPT-Agent] 关闭并释放命名管道失败: {ex.Message}", LogHelper.LogType.Info); }
            _pipe = null;
            SetConnected(false);
        }

        private void FailPendingRequests(Exception ex)
        {
            foreach (var item in _pendingRequests)
            {
                if (_pendingRequests.TryRemove(item.Key, out var tcs))
                    tcs.TrySetException(ex);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
            _cts?.Dispose();
            LogHelper.WriteLogToFile("[PPT-Agent] Pipe 客户端已释放", LogHelper.LogType.Info);
        }
    }
}
