using System;
using System.IO;
using System.Security.AccessControl;
using System.Threading;
using System.Threading.Tasks;
using InkCanvasPptAgent.Contracts;
using Newtonsoft.Json;

namespace PptAgent.PowerPointAddIn.IPC
{
    public sealed class PipeHost : IDisposable
    {
        private readonly Func<string, string> _dispatch;
        private readonly object _sendLock = new object();
        private CancellationTokenSource _cts;
        private bool _disposed;

        public bool IsEnabled { get; private set; }

        public PipeHost(Func<string, string> dispatch)
        {
            _dispatch = dispatch;
        }

        public void Start()
        {
            if (_cts != null) return;

            _cts = new CancellationTokenSource();
            Task.Run(() => AcceptLoop(_cts.Token));
        }

        public void SendFrame(string json)
        {
            // TODO: store latest stream for write when host implements single client accept.
            _ = json;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try { _cts?.Cancel(); } catch { }
            _cts?.Dispose();
            _cts = null;
            IsEnabled = false;
        }

        private async Task AcceptLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                System.IO.Pipes.NamedPipeServerStream pipe = null;
                try
                {
                    pipe = new System.IO.Pipes.NamedPipeServerStream(
                        PipeConstants.PipeName,
                        System.IO.Pipes.PipeDirection.InOut,
                        1,
                        System.IO.Pipes.PipeTransmissionMode.Byte,
                        System.IO.Pipes.PipeOptions.Asynchronous,
                        4096,
                        4096,
                        CreatePipeSecurity(),
                        System.IO.Pipes.HandleInheritability.None);

                    await pipe.WaitForConnectionAsync(token).ConfigureAwait(false);
                    IsEnabled = true;

                    await HandleClient(pipe, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"PPT Agent Pipe host error: {ex.Message}");
                }
                finally
                {
                    try { pipe?.Dispose(); } catch { }
                    IsEnabled = false;
                }
            }
        }

        private async Task HandleClient(System.IO.Pipes.NamedPipeServerStream pipe, CancellationToken token)
        {
            while (!token.IsCancellationRequested && pipe.IsConnected)
            {
                var requestJson = PipeFrame.ReadFrame(pipe);
                var responseJson = _dispatch.Invoke(requestJson);
                if (!string.IsNullOrEmpty(responseJson))
                {
                    lock (_sendLock)
                    {
                        PipeFrame.WriteFrame(pipe, responseJson);
                    }
                }
            }
        }

        private static PipeSecurity CreatePipeSecurity()
        {
            var security = new PipeSecurity();
            security.SetSecurityDescriptorSddlForm("D:(A;;FA;;;WD)S:(ML;;NW;;;LW)", AccessControlSections.All);
            return security;
        }
    }
}
