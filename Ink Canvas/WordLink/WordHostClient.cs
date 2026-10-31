using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using InkCanvasWordLink;

namespace Ink_Canvas.WordLink
{
    internal interface IWordFrameSource : IDisposable
    {
        Task<WordFrame> ExchangeAsync(WordRequest request, CancellationToken cancellationToken);
        Task WarmAsync(CancellationToken token) => Task.CompletedTask;
        void Suspend() => Stop();
        void Stop();
    }

    internal sealed class WordHostClient : IWordFrameSource
    {
        private Process _process;
        private readonly SemaphoreSlim _exchange = new(1, 1);
        private bool _disposed;
        private long _retryAfter;
        private string _retryStatus = "Unavailable";
        private readonly string _helperPath;
        private readonly TimeSpan? _timeoutOverride;
        private int _failures;
        internal string LastError { get; private set; }
        private string _hostStage;
        private int _generation;
        private int _activity;
        private long _readyWindow;
        private Task _detaching = Task.CompletedTask;
        internal int Starts { get; private set; }
        internal WordHostClient(string helperPath = null, TimeSpan? timeout = null)
        {
            _helperPath = helperPath ?? Path.Combine(AppContext.BaseDirectory, "word-link", "InkCanvas.WordLinkHost.exe");
            _timeoutOverride = timeout;
        }

        public async Task<WordFrame> ExchangeAsync(WordRequest request, CancellationToken cancellationToken)
        {
            await _detaching.WaitAsync(cancellationToken);
            if (_disposed || !await _exchange.WaitAsync(0, cancellationToken)) return null;
            Interlocked.Increment(ref _activity);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            int generation = Volatile.Read(ref _generation);
            string stage = "startup";
            try
            {
                var process = _process;
                bool connecting = process == null || process.HasExited;
                if (connecting)
                {
                    if (Environment.TickCount64 < _retryAfter) return new WordFrame { Status = _retryStatus };
                    StartHost();
                    process = _process;
                    using var startup = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    startup.CancelAfter(TimeSpan.FromSeconds(5));
                    await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new WordRequest { Handshake = true }).AsMemory(), startup.Token);
                    await process.StandardInput.FlushAsync(startup.Token);
                    var greeting = await ReadFrameAsync(process, startup.Token);
                    if (greeting.Status != "HostReady") throw new NotSupportedException("Word helper handshake mismatch; update the word-link folder.");
                }
                stage = "Word request";
                var requestWatch = Stopwatch.StartNew();
                deadline.CancelAfter(RequestTimeout(connecting || _readyWindow == 0 || _readyWindow != request.WindowHandle,
                    request.DeferContentCheck));
                await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request).AsMemory(), deadline.Token);
                await process.StandardInput.FlushAsync(deadline.Token);
                var frame = await ReadFrameAsync(process, deadline.Token);
                if (frame.Status == "Ready" && generation == Volatile.Read(ref _generation)
                    && ReferenceEquals(_process, process)) _readyWindow = frame.WindowHandle;
                if (requestWatch.ElapsedMilliseconds >= 250)
                    Helpers.LogHelper.WriteLogToFile($"[WordLink] Slow query: {requestWatch.ElapsedMilliseconds}ms, status={frame.Status}, phase={_hostStage}",
                        Helpers.LogHelper.LogType.Warning);
                if (frame.Status is "Ready" or "Waiting" or "Busy") { LastError = null; _failures = 0; }
                return frame;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                if (_disposed || generation != Volatile.Read(ref _generation)) return null;
                LastError = $"{stage} timed out. {_hostStage}";
                Helpers.LogHelper.WriteLogToFile($"[WordLink] {LastError}", Helpers.LogHelper.LogType.Warning);
                ResetHost();
                _retryStatus = "Timeout";
                return new WordFrame { Status = "Timeout" };
            }
            catch (OperationCanceledException) { ResetHost(false); return null; }
            catch (Exception ex)
            {
                if (_disposed || generation != Volatile.Read(ref _generation)) return null;
                LastError = $"{ex.GetType().Name}: {ex.Message}";
                Helpers.LogHelper.WriteLogToFile($"[WordLink] Helper unavailable: {ex.GetType().Name}", Helpers.LogHelper.LogType.Info);
                ResetHost();
                _retryStatus = ex is FileNotFoundException ? "HelperMissing"
                    : ex is NotSupportedException ? "HelperMismatch" : "Unavailable";
                return new WordFrame { Status = _retryStatus };
            }
            finally { _exchange.Release(); }
        }

        public async Task WarmAsync(CancellationToken token)
        {
            if (_disposed || _process != null) return;
            await ExchangeAsync(new WordRequest { Handshake = true }, token);
        }

        public void Suspend()
        {
            var process = _process;
            if (process != null) _detaching = ReleaseDocumentAsync(process, Volatile.Read(ref _activity));
        }

        private async Task ReleaseDocumentAsync(Process process, int activity)
        {
            await _exchange.WaitAsync().ConfigureAwait(false);
            try
            {
                // A newer request may already own another document. Never
                // detach that session on behalf of an earlier suspension.
                if (_disposed || !ReferenceEquals(_process, process) || activity != Volatile.Read(ref _activity)) return;
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new WordRequest { ReleaseDocument = true }).AsMemory(), deadline.Token).ConfigureAwait(false);
                await process.StandardInput.FlushAsync(deadline.Token).ConfigureAwait(false);
                var frame = await ReadFrameAsync(process, deadline.Token).ConfigureAwait(false);
                if (frame.Status != "Released") throw new InvalidDataException("Word helper did not release its document.");
                _readyWindow = 0;
            }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"[WordLink] Detach failed: {ex.GetType().Name}", Helpers.LogHelper.LogType.Warning);
                if (ReferenceEquals(_process, process)) ResetHost(false);
            }
            finally { _exchange.Release(); }
        }

        internal TimeSpan RequestTimeout(bool connecting, bool deferContentCheck)
            // The first document bind and full-document revision/pagination
            // need more time on slower PCs. Scroll-only queries remain bounded.
            => _timeoutOverride ?? TimeSpan.FromSeconds(connecting || !deferContentCheck ? 8 : 3);

        internal static int FailureDelay(int failures) => 500 * (1 << Math.Clamp(failures - 1, 0, 3));

        private static async Task<WordFrame> ReadFrameAsync(Process process, CancellationToken token)
        {
            // Bounded line framing; use the process captured for this exchange so
            // disabling the feature cannot redirect an old read to a new process.
            var line = new StringBuilder();
            var buffer = new char[2048];
            while (line.Length <= 131072)
            {
                int count = await process.StandardOutput.ReadAsync(buffer.AsMemory(), token);
                if (count == 0) throw new EndOfStreamException();
                int newline = Array.IndexOf(buffer, '\n', 0, count);
                line.Append(buffer, 0, newline >= 0 ? newline : count);
                if (newline >= 0)
                {
                    if (newline != count - 1) throw new InvalidDataException("Unexpected trailing helper data.");
                    break;
                }
            }
            if (line.Length > 131072) throw new InvalidDataException("Word helper response is too large.");
            var frame = JsonSerializer.Deserialize<WordFrame>(line.ToString());
            if (frame?.IsValid() != true) throw new InvalidDataException("Invalid Word helper snapshot.");
            return frame;
        }

        private void StartHost()
        {
            ResetHost(false);
            string path = _helperPath;
            if (!File.Exists(path)) throw new FileNotFoundException("Word helper not deployed.");
            var start = new ProcessStartInfo(path)
            {
                UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(path)
            };
            start.ArgumentList.Add("--parent"); start.ArgumentList.Add(Environment.ProcessId.ToString());
            var process = new Process { StartInfo = start };
            _hostStage = null;
            // Drain stderr so a failed Office call can never fill the helper's pipe.
            process.ErrorDataReceived += (_, e) =>
            {
                if (ReferenceEquals(_process, process) && !string.IsNullOrEmpty(e.Data))
                {
                    if (e.Data.StartsWith("[WordLinkHost] slow-phase=", StringComparison.Ordinal))
                    {
                        Helpers.LogHelper.WriteLogToFile(e.Data, Helpers.LogHelper.LogType.Warning);
                        return;
                    }
                    _hostStage = e.Data.Length <= 512 ? e.Data : e.Data[..512];
                    if (!e.Data.StartsWith("[WordLinkHost] phase=", StringComparison.Ordinal)) LastError = _hostStage;
                }
            };
            _process = process;
            if (!process.Start()) throw new IOException("Cannot start Word helper.");
            Starts++;
            Helpers.LogHelper.WriteLogToFile($"[WordLink] Helper started: pid={process.Id}", Helpers.LogHelper.LogType.Info);
            process.BeginErrorReadLine();
        }

        private void ResetHost(bool backoff = true)
        {
            _readyWindow = 0;
            var process = Interlocked.Exchange(ref _process, null);
            if (process != null)
            {
                if (backoff)
                {
                    try { if (!process.HasExited) process.Kill(); } catch (InvalidOperationException) { }
                    finally { process.Dispose(); }
                }
                else
                {
                    // Let an idle helper release its OLE apartment cleanly. Do
                    // not make the UI wait for Word; a busy helper is still killed.
                    try { process.StandardInput.Close(); }
                    catch (Exception ex) when (ex is InvalidOperationException or IOException) { }
                    _ = RetireAsync(process);
                }
            }
            _retryAfter = backoff ? Environment.TickCount64 + FailureDelay(++_failures) : 0;
            if (!backoff) _retryStatus = "Unavailable";
        }

        private static async Task RetireAsync(Process process)
        {
            try
            {
                using var deadline = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
                try { await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { if (!process.HasExited) process.Kill(); }
            }
            catch (Exception ex)
            { Helpers.LogHelper.WriteLogToFile($"[WordLink] Helper cleanup: {ex.GetType().Name}", Helpers.LogHelper.LogType.Info); }
            finally { process.Dispose(); }
        }

        public void Stop() { Interlocked.Increment(ref _generation); _failures = 0; ResetHost(false); }
        public void Dispose() { _disposed = true; Stop(); }
    }
}
