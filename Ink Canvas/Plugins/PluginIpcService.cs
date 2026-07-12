using Ink_Canvas.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// IPC 服务器，基于 <see cref="IPluginIpcBus"/> 的实现。该实现位于主项目，SDK 仅暴露接口。
    /// 命名管道名称：<c>\\.\pipe\ICC.PluginIpc.<sessionId></c>。
    /// 帧协议：4 字节长度前缀 + UTF-8 JSON 负载，单帧上限 <see cref="MaxMessageBytes"/>。
    /// </summary>
    public class PluginIpcService : IPluginIpcBus, IDisposable
    {
        /// <summary>
        /// 单条消息的最大字节数。
        /// </summary>
        public const int MaxMessageBytes = 1024 * 1024; // 1 MB

        private static readonly JsonSerializerOptions IpcJsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        private readonly object _lock = new();
        private readonly Dictionary<string, List<Func<System.Text.Json.JsonElement?, object>>> _handlers = new(StringComparer.Ordinal);
        private readonly CancellationTokenSource _cts = new();
        private readonly string _pipeName;
        private Task _listenTask;
        private bool _disposed;

        /// <summary>
        /// 收到任何消息时触发。
        /// </summary>
        public event EventHandler<IpcMessage> MessageReceived;

        public string PipeName => _pipeName;

        public PluginIpcService()
        {
            _pipeName = $"ICC.PluginIpc.{GetSessionId()}";
        }

        #region IPluginIpcBus

        /// <summary>
        /// 启动命名管道服务端，循环 accept 客户端连接。
        /// </summary>
        public void Start()
        {
            lock (_lock)
            {
                if (_listenTask != null) return;
                _listenTask = Task.Run(ListenLoopAsync);
            }
        }

        /// <summary>
        /// 注册一个处理函数。同一方法可注册多个，第一个不抛异常的胜出。
        /// </summary>
        public void RegisterHandler(string method, Func<System.Text.Json.JsonElement?, object> handler)
        {
            if (string.IsNullOrEmpty(method)) throw new ArgumentException("method required");
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            lock (_lock)
            {
                if (!_handlers.TryGetValue(method, out var list))
                {
                    list = new List<Func<System.Text.Json.JsonElement?, object>>();
                    _handlers[method] = list;
                }
                list.Add(handler);
            }
        }

        /// <summary>
        /// 主动调用对端，<paramref name="args"/> 是任意 JSON 结构。
        /// 失败时抛出 <see cref="InvalidOperationException"/>。
        /// </summary>
        public async Task<object> InvokeAsync(string method, System.Text.Json.JsonElement? args = null, TimeSpan? timeout = null)
        {
            if (string.IsNullOrEmpty(method)) throw new ArgumentException("method required");

            using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            var connectMs = (int)(timeout ?? TimeSpan.FromSeconds(5)).TotalMilliseconds;
            await client.ConnectAsync(connectMs).ConfigureAwait(false);

            var request = new IpcMessage
            {
                Method = method,
                Params = args,
                From = "host"
            };
            var reqBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request, IpcJsonOptions));
            await WriteFramedAsync(client, reqBytes, _cts.Token).ConfigureAwait(false);

            var respData = await ReadFramedAsync(client, _cts.Token).ConfigureAwait(false);
            if (respData == null || respData.Length == 0) return null;
            var resp = JsonSerializer.Deserialize<IpcMessage>(Encoding.UTF8.GetString(respData), IpcJsonOptions);
            if (resp == null) return null;
            if (resp.Error != null)
                throw new InvalidOperationException($"IPC error ({resp.Error.Code}): {resp.Error.Message}");
            return resp.Result;
        }

        #endregion

        #region 服务端

        private async Task ListenLoopAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                NamedPipeServerStream server = null;
                try
                {
                    server = CreatePipeServer();
                    await server.WaitForConnectionAsync(_cts.Token).ConfigureAwait(false);
                    _ = Task.Run(() => HandleConnectionAsync(server));
                    server = null; // ownership transferred
                }
                catch (OperationCanceledException)
                {
                    server?.Dispose();
                    break;
                }
                catch (Exception ex)
                {
                    server?.Dispose();
                    LogHelper.WriteLogToFile(
                        $"PluginIpc | 服务端异常: {ex.Message}",
                        LogHelper.LogType.Warning);
                    try { await Task.Delay(1000, _cts.Token); }
                    catch (OperationCanceledException) { break; }
                }
            }
        }

        private NamedPipeServerStream CreatePipeServer()
        {
            // 使用 standard ctor。命名管道身份继承自当前进程，对同等 / 更低权限用户受限。
            // 如需更强 ACL，可改为 System.IO.Pipes.AccessControl 的 NamedPipeServerStreamAcl.Create。
            return new NamedPipeServerStream(
                _pipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous,
                inBufferSize: 4096,
                outBufferSize: 4096);
        }

        private async Task HandleConnectionAsync(NamedPipeServerStream stream)
        {
            using (stream)
            {
                try
                {
                    var data = await ReadFramedAsync(stream, _cts.Token).ConfigureAwait(false);
                    if (data == null || data.Length == 0) return;

                    var msg = JsonSerializer.Deserialize<IpcMessage>(Encoding.UTF8.GetString(data), IpcJsonOptions);
                    if (msg == null) return;

                    var response = new IpcMessage
                    {
                        Id = msg.Id,
                        Method = msg.Method,
                        From = "host"
                    };

                    try
                    {
                        var result = Dispatch(msg.Method, msg.Params);
                        if (result != null)
                        {
                            response.Result = result is System.Text.Json.JsonElement je
                                ? je
                                : JsonSerializer.SerializeToElement(result, IpcJsonOptions);
                        }
                    }
                    catch (Exception ex)
                    {
                        response.Error = new IpcError { Code = -32000, Message = ex.Message };
                    }

                    var respBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(response, IpcJsonOptions));
                    await WriteFramedAsync(stream, respBytes, _cts.Token).ConfigureAwait(false);

                    MessageReceived?.Invoke(this, msg);
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile(
                        $"PluginIpc | 客户端消息处理失败: {ex.Message}",
                        LogHelper.LogType.Warning);
                }
            }
        }

        private object Dispatch(string method, System.Text.Json.JsonElement? args)
        {
            Func<System.Text.Json.JsonElement?, object>[] snapshot;
            lock (_lock)
            {
                if (!_handlers.TryGetValue(method, out var list) || list.Count == 0)
                    throw new InvalidOperationException($"Method '{method}' not found");
                snapshot = list.ToArray();
            }

            Exception last = null;
            foreach (var handler in snapshot)
            {
                try { return handler(args); }
                catch (Exception ex) { last = ex; }
            }

            throw last ?? new InvalidOperationException("All handlers failed");
        }

        #endregion

        #region 帧协议

        private static async Task<byte[]> ReadFramedAsync(PipeStream stream, CancellationToken token)
        {
            var lengthBuf = new byte[4];
            var read = await ReadExactAsync(stream, lengthBuf, 4, token).ConfigureAwait(false);
            if (read < 4) return null;

            var length = BitConverter.ToInt32(lengthBuf, 0);
            if (length < 0 || length > MaxMessageBytes)
                throw new InvalidDataException("IPC message too large or invalid");

            var buf = new byte[length];
            read = await ReadExactAsync(stream, buf, length, token).ConfigureAwait(false);
            return read == length ? buf : null;
        }

        private static async Task WriteFramedAsync(PipeStream stream, byte[] payload, CancellationToken token)
        {
            var lengthBuf = BitConverter.GetBytes(payload.Length);
            await stream.WriteAsync(lengthBuf, 0, lengthBuf.Length, token).ConfigureAwait(false);
            await stream.WriteAsync(payload, 0, payload.Length, token).ConfigureAwait(false);
            await stream.FlushAsync(token).ConfigureAwait(false);
        }

        private static async Task<int> ReadExactAsync(PipeStream stream, byte[] buffer, int count, CancellationToken token)
        {
            var total = 0;
            while (total < count)
            {
                var n = await stream.ReadAsync(buffer, total, count - total, token).ConfigureAwait(false);
                if (n <= 0) break;
                total += n;
            }
            return total;
        }

        #endregion

        public static string GetSessionId()
        {
            try
            {
                return System.Diagnostics.Process.GetCurrentProcess().SessionId.ToString();
            }
            catch
            {
                return "0";
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try { _cts.Cancel(); } catch { }
            try { _listenTask?.Wait(TimeSpan.FromSeconds(1)); } catch { }
            try { _cts.Dispose(); } catch { }
        }
    }
}
