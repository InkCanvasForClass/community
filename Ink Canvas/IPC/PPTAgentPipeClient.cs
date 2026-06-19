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
        private readonly object _sendLock = new object();
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

            return TrySendMessage(message);
        }

        public T SendRequest<T>(string command, object data = null, int timeoutMilliseconds = PipeConstants.RequestTimeoutMilliseconds)
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

            if (!TrySendMessage(message))
            {
                _pendingRequests.TryRemove(requestId, out _);
                return default;
            }

            try
            {
                if (!tcs.Task.Wait(timeoutMilliseconds))
                {
                    _pendingRequests.TryRemove(requestId, out _);
                    LogHelper.WriteLogToFile($"PPT Agent 命令超时: {command}", LogHelper.LogType.Warning);
                    return default;
                }

                var token = tcs.Task.Result;
                if (token == null || token.Type == JTokenType.Null)
                    return default;

                return token.ToObject<T>();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"PPT Agent 请求失败 [{command}]: {ex}", LogHelper.LogType.Warning);
                return default;
            }
            finally
            {
                _pendingRequests.TryRemove(requestId, out _);
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

        private bool TrySendMessage<T>(PPTPipeMessage<T> message)
        {
            try
            {
                var pipe = _pipe;
                if (pipe == null || !pipe.IsConnected)
                    return false;

                var json = JsonConvert.SerializeObject(message);
                lock (_sendLock)
                {
                    if (pipe == null || !pipe.IsConnected)
                        return false;
                    PipeFrame.WriteFrame(pipe, json);
                }
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"发送 PPT Agent Pipe 消息失败: {ex.Message}", LogHelper.LogType.Warning);
                ClosePipe();
                return false;
            }
        }

        private void SetConnected(bool connected)
        {
            if (_isConnected == connected) return;
            _isConnected = connected;
            ConnectionChanged?.Invoke(connected);
        }

        private void ClosePipe()
        {
            try { _pipe?.Dispose(); } catch { }
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
        }
    }
}
