using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.IO.Pipes;
using System.Threading;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Media;

namespace Ink_Canvas.Helpers
{
    public sealed class IpcIACoreClient : IDisposable
    {
        private static IpcIACoreClient _instance;
        private static readonly object _instanceLock = new object();

        public static IpcIACoreClient Instance
        {
            get
            {
                if (_instance == null)
                    lock (_instanceLock)
                        if (_instance == null)
                            _instance = new IpcIACoreClient();
                return _instance;
            }
        }

        private Process _helperProcess;
        private KillOnCloseJob _helperJob;
        private MemoryMappedFile _sharedMemory;
        private int _sharedMemoryCapacity = DefaultSharedMemoryCapacity;
        private int _sharedMemoryGeneration;
        private readonly object _pipeLock = new object();
        // 仅守护"判断 IsAvailable → 启动 helper"这一段，避免与识别请求的 _pipeLock 互锁，
        // 同时根除 App.xaml.cs:1415 + MainWindow.xaml.cs:1522/2588 + InkRecognitionManager:177
        // 并发调 Start() 时起出两条 helper 的竞态（保留旧 PID 被覆盖变孤儿）。
        private readonly object _startGate = new object();
        private bool _disposed;
        private bool _available;

        private static string HelperExePath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "InkCanvas.IACoreHelper.exe");

        public bool IsHelperExecutableAvailable => File.Exists(HelperExePath);

        private int CurrentProcessId => Process.GetCurrentProcess().Id;

        private string PipeName =>
            string.Format(PipeNameFormat, CurrentProcessId);

        private string SharedMemoryName =>
            string.Format(SharedMemoryNameFormat, CurrentProcessId, _sharedMemoryGeneration);

        private IpcIACoreClient() { }

        public bool Start()
        {
            if (_disposed) return false;
            lock (_startGate)
            {
                if (IsAvailable) return true;

                if (!File.Exists(HelperExePath))
                {
                    _available = false;
                    return false;
                }

                return LaunchHelper();
            }
        }

        public bool IsAvailable => _available && _helperProcess != null && !_helperProcess.HasExited;

        public InkShapeRecognitionResult Recognize(StrokeCollection strokes)
        {
            if (strokes == null || strokes.Count == 0)
                return InkShapeRecognitionResult.Empty;

            EnsureHelperAlive();
            if (!IsAvailable)
                return InkShapeRecognitionResult.Empty;

            lock (_pipeLock)
            {
                try
                {
                    return SendRecognizeRequest(strokes);
                }
                catch
                {
                    KillHelper();
                    return InkShapeRecognitionResult.Empty;
                }
            }
        }

        /// <summary>
        /// 通过 IPC 辅助进程执行 IACore 文字识别（IAWinFX InkAnalyzer + AnalysisHintNode）。
        /// 返回分词文本/候选/包围框/笔画索引；辅助进程不可用或失败时返回空结果（调用方据此回落 WinRT）。
        /// </summary>
        /// <param name="hint">上下文提示（Factoid/WordList/WordMode/CoerceToFactoid/Hint 区域）；传 null 表示无提示。</param>
        public HandwritingRecognitionResult RecognizeText(StrokeCollection strokes, IacoreTextHint hint = null)
        {
            if (strokes == null || strokes.Count == 0)
                return HandwritingRecognitionResult.Empty;

            EnsureHelperAlive();
            if (!IsAvailable)
                return HandwritingRecognitionResult.Empty;

            lock (_pipeLock)
            {
                try
                {
                    return SendRecognizeTextRequest(strokes, hint);
                }
                catch
                {
                    KillHelper();
                    return HandwritingRecognitionResult.Empty;
                }
            }
        }

        private bool LaunchHelper()
        {
            try
            {
                KillHelper();
                EnsureSharedMemory(DefaultSharedMemoryCapacity);

                var psi = new ProcessStartInfo
                {
                    FileName = HelperExePath,
                    Arguments = CurrentProcessId.ToString(),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
                };
                _helperProcess = Process.Start(psi);
                if (_helperProcess == null)
                {
                    _available = false;
                    ReleaseSharedMemory();
                    return false;
                }
                _helperProcess.EnableRaisingEvents = true;
                _helperProcess.Exited += OnHelperExited;
                // 把 helper 关联进 Job Object：父进程任何原因消失（崩溃/TaskKill/Environment.Exit）
                // 关闭 job handle 时，Windows 内核会强制结束 helper，防止变成孤儿锁住主程序 exe。
                // 与 helper 内部的父进程守护线程形成双保险。
                _helperJob?.Dispose();
                _helperJob = KillOnCloseJob.TryCreateAssociated(_helperProcess);

                bool pipeReady = WaitForPipe(3000);
                _available = pipeReady;
                if (!pipeReady)
                    ReleaseSharedMemory();
                return pipeReady;
            }
            catch
            {
                _available = false;
                ReleaseSharedMemory();
                return false;
            }
        }

        private bool WaitForPipe(int timeoutMs)
        {
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.ElapsedMilliseconds < timeoutMs)
            {
                if (_helperProcess == null || _helperProcess.HasExited)
                    return false;

                try
                {
                    using (var probe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut))
                    {
                        probe.Connect(200);
                        return true;
                    }
                }
                catch
                {
                    Thread.Sleep(100);
                }
            }
            return false;
        }

        private InkShapeRecognitionResult SendRecognizeRequest(StrokeCollection strokes)
        {
            int requestLength = WriteRequestToSharedMemory(strokes);

            using (var client = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut))
            {
                client.Connect(IpcTimeoutMs);

                using (var writer = new BinaryWriter(client, System.Text.Encoding.UTF8, leaveOpen: true))
                using (var reader = new BinaryReader(client, System.Text.Encoding.UTF8, leaveOpen: true))
                {
                    writer.Write(CmdRecognizeSharedMemory);
                    writer.Write(requestLength);
                    writer.Write(_sharedMemoryCapacity);
                    writer.Write(_sharedMemoryGeneration);
                    writer.Flush();

                    int status = reader.ReadInt32();
                    int responseLength = reader.ReadInt32();
                    // 共享内存容量不足时，仅放大一次并重发请求；helper 仍返回 TooLarge 则放弃，
                    // 而不是让 GrowSharedMemory 在更大尺寸上抛 InvalidOperationException 被吞掉。
                    if (status == StatusResponseTooLarge)
                    {
                        try { GrowSharedMemory(_sharedMemoryCapacity * 2); }
                        catch { return InkShapeRecognitionResult.Empty; }
                        return SendRecognizeRequest(strokes);
                    }
                    if (status != StatusOk || responseLength <= 0)
                        return InkShapeRecognitionResult.Empty;

                    return ReadResponseFromSharedMemory(strokes, responseLength);
                }
            }
        }

        private HandwritingRecognitionResult SendRecognizeTextRequest(StrokeCollection strokes, IacoreTextHint hint)
        {
            int requestLength = WriteTextRequestToSharedMemory(strokes, hint);

            using (var client = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut))
            {
                client.Connect(IpcTimeoutMs);

                using (var writer = new BinaryWriter(client, System.Text.Encoding.UTF8, leaveOpen: true))
                using (var reader = new BinaryReader(client, System.Text.Encoding.UTF8, leaveOpen: true))
                {
                    writer.Write(CmdRecognizeTextSharedMemory);
                    writer.Write(requestLength);
                    writer.Write(_sharedMemoryCapacity);
                    writer.Write(_sharedMemoryGeneration);
                    writer.Flush();

                    int status = reader.ReadInt32();
                    int responseLength = reader.ReadInt32();
                    if (status == StatusResponseTooLarge)
                    {
                        try { GrowSharedMemory(_sharedMemoryCapacity * 2); }
                        catch { return HandwritingRecognitionResult.Empty; }
                        return SendRecognizeTextRequest(strokes, hint);
                    }
                    if (status != StatusOk || responseLength <= 0)
                        return HandwritingRecognitionResult.Empty;

                    return ReadTextResponseFromSharedMemory(strokes, responseLength);
                }
            }
        }

        private int WriteRequestToSharedMemory(StrokeCollection strokes)
        {
            int requestLength = GetRequestLength(strokes);
            int requiredCapacity = SharedMemoryHeaderSize + requestLength + MinResponseCapacity;
            EnsureSharedMemory(requiredCapacity);

            using (var accessor = _sharedMemory.CreateViewAccessor(0, SharedMemoryHeaderSize))
            {
                accessor.Write(HeaderMagicOffset, SharedMemoryMagic);
                accessor.Write(HeaderVersionOffset, ProtocolVersion);
                accessor.Write(HeaderRequestLengthOffset, requestLength);
                accessor.Write(HeaderResponseOffsetOffset, 0);
                accessor.Write(HeaderResponseLengthOffset, 0);
                accessor.Write(HeaderStatusOffset, StatusOk);
            }

            using (var stream = _sharedMemory.CreateViewStream(
                SharedMemoryHeaderSize,
                requestLength,
                MemoryMappedFileAccess.Write))
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8))
            {
                writer.Write(strokes.Count);
                foreach (var stroke in strokes)
                {
                    var pts = stroke.StylusPoints;
                    writer.Write(pts.Count);
                    foreach (var pt in pts)
                    {
                        writer.Write((float)pt.X);
                        writer.Write((float)pt.Y);
                        writer.Write(pt.PressureFactor);
                    }
                }
                writer.Flush();
            }

            return requestLength;
        }

        private int WriteTextRequestToSharedMemory(StrokeCollection strokes, IacoreTextHint hint)
        {
            int requestLength = GetTextRequestLength(strokes, hint);
            int requiredCapacity = SharedMemoryHeaderSize + requestLength + MinResponseCapacity;
            EnsureSharedMemory(requiredCapacity);

            using (var accessor = _sharedMemory.CreateViewAccessor(0, SharedMemoryHeaderSize))
            {
                accessor.Write(HeaderMagicOffset, SharedMemoryMagic);
                accessor.Write(HeaderVersionOffset, ProtocolVersion);
                accessor.Write(HeaderRequestLengthOffset, requestLength);
                accessor.Write(HeaderResponseOffsetOffset, 0);
                accessor.Write(HeaderResponseLengthOffset, 0);
                accessor.Write(HeaderStatusOffset, StatusOk);
            }

            using (var stream = _sharedMemory.CreateViewStream(
                SharedMemoryHeaderSize,
                requestLength,
                MemoryMappedFileAccess.Write))
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8))
            {
                // 提示头：HintLeft/Top/Width/Height + Factoid + WordList[] + WordMode + CoerceToFactoid
                writer.Write(hint?.HintLeft ?? 0f);
                writer.Write(hint?.HintTop ?? 0f);
                writer.Write(hint?.HintWidth ?? 0f);
                writer.Write(hint?.HintHeight ?? 0f);
                writer.Write(hint?.Factoid ?? string.Empty);

                var wl = hint?.WordList;
                int wlLen = wl != null ? wl.Length : 0;
                writer.Write(wlLen);
                for (int i = 0; i < wlLen; i++)
                    writer.Write(wl[i] ?? string.Empty);

                writer.Write(hint?.WordMode ?? false);
                writer.Write(hint?.CoerceToFactoid ?? false);

                // 笔画载荷：与形状请求同编码（strokeCount, 每 stroke 点数 + X/Y/Pressure）。
                writer.Write(strokes.Count);
                foreach (var stroke in strokes)
                {
                    var pts = stroke.StylusPoints;
                    writer.Write(pts.Count);
                    foreach (var pt in pts)
                    {
                        writer.Write((float)pt.X);
                        writer.Write((float)pt.Y);
                        writer.Write(pt.PressureFactor);
                    }
                }
                writer.Flush();
            }

            return requestLength;
        }

        private static int GetTextRequestLength(StrokeCollection strokes, IacoreTextHint hint)
        {
            checked
            {
                // 4×float(Hint rect) + string(Factoid: 4+len) + int(wlLen) + Σ string(4+len) + bool×2 + int(strokeCount) + per stroke
                int length = sizeof(float) * 4;
                var factoid = hint?.Factoid ?? string.Empty;
                length += sizeof(int) + System.Text.Encoding.UTF8.GetByteCount(factoid);

                var wl = hint?.WordList;
                int wlLen = wl != null ? wl.Length : 0;
                length += sizeof(int);
                for (int i = 0; i < wlLen; i++)
                    length += sizeof(int) + System.Text.Encoding.UTF8.GetByteCount(wl[i] ?? string.Empty);

                length += 2; // bool WordMode + bool CoerceToFactoid

                length += sizeof(int);
                foreach (var stroke in strokes)
                    length += sizeof(int) + stroke.StylusPoints.Count * sizeof(float) * 3;
                return length;
            }
        }

        private InkShapeRecognitionResult ReadResponseFromSharedMemory(StrokeCollection strokes, int responseLength)
        {
            int responseOffset;
            using (var accessor = _sharedMemory.CreateViewAccessor(0, SharedMemoryHeaderSize))
            {
                if (accessor.ReadInt32(HeaderMagicOffset) != SharedMemoryMagic ||
                    accessor.ReadInt32(HeaderVersionOffset) != ProtocolVersion ||
                    accessor.ReadInt32(HeaderStatusOffset) != StatusOk)
                    return InkShapeRecognitionResult.Empty;

                responseOffset = accessor.ReadInt32(HeaderResponseOffsetOffset);
                int headerResponseLength = accessor.ReadInt32(HeaderResponseLengthOffset);
                if (headerResponseLength > 0)
                    responseLength = headerResponseLength;
            }

            if (responseOffset < SharedMemoryHeaderSize || responseLength <= 0 || responseOffset + responseLength > _sharedMemoryCapacity)
                return InkShapeRecognitionResult.Empty;

            using (var stream = _sharedMemory.CreateViewStream(responseOffset, responseLength, MemoryMappedFileAccess.Read))
            using (var reader = new BinaryReader(stream, System.Text.Encoding.UTF8))
            {
                bool success = reader.ReadBoolean();
                string shape = reader.ReadString();
                float cx = reader.ReadSingle();
                float cy = reader.ReadSingle();
                float width = reader.ReadSingle();
                float height = reader.ReadSingle();

                int hotLen = reader.ReadInt32();
                var hotPoints = new PointCollection();
                for (int i = 0; i < hotLen; i++)
                    hotPoints.Add(new Point(reader.ReadSingle(), reader.ReadSingle()));

                int idxLen = reader.ReadInt32();
                var indices = new int[idxLen];
                for (int i = 0; i < idxLen; i++)
                    indices[i] = reader.ReadInt32();

                if (!success || string.IsNullOrEmpty(shape))
                    return InkShapeRecognitionResult.Empty;

                var recognized = new StrokeCollection();
                foreach (int idx in indices)
                    if (idx >= 0 && idx < strokes.Count)
                        recognized.Add(strokes[idx]);

                return new InkShapeRecognitionResult(
                    shape,
                    new Point(cx, cy),
                    hotPoints,
                    width,
                    height,
                    recognized);
            }
        }

        private HandwritingRecognitionResult ReadTextResponseFromSharedMemory(StrokeCollection strokes, int responseLength)
        {
            int responseOffset;
            using (var accessor = _sharedMemory.CreateViewAccessor(0, SharedMemoryHeaderSize))
            {
                if (accessor.ReadInt32(HeaderMagicOffset) != SharedMemoryMagic ||
                    accessor.ReadInt32(HeaderVersionOffset) != ProtocolVersion ||
                    accessor.ReadInt32(HeaderStatusOffset) != StatusOk)
                    return HandwritingRecognitionResult.Empty;

                responseOffset = accessor.ReadInt32(HeaderResponseOffsetOffset);
                int headerResponseLength = accessor.ReadInt32(HeaderResponseLengthOffset);
                if (headerResponseLength > 0)
                    responseLength = headerResponseLength;
            }

            if (responseOffset < SharedMemoryHeaderSize || responseLength <= 0 || responseOffset + responseLength > _sharedMemoryCapacity)
                return HandwritingRecognitionResult.Empty;

            using (var stream = _sharedMemory.CreateViewStream(responseOffset, responseLength, MemoryMappedFileAccess.Read))
            using (var reader = new BinaryReader(stream, System.Text.Encoding.UTF8))
            {
                bool success = reader.ReadBoolean();
                string combined = reader.ReadString();

                int wLen = reader.ReadInt32();
                var segments = new List<HandwritingWordSegment>(wLen);
                for (int i = 0; i < wLen; i++)
                {
                    string text = reader.ReadString();

                    int cLen = reader.ReadInt32();
                    var candidates = new List<string>(cLen);
                    for (int j = 0; j < cLen; j++)
                        candidates.Add(reader.ReadString());

                    float left = reader.ReadSingle();
                    float top = reader.ReadSingle();
                    float width = reader.ReadSingle();
                    float height = reader.ReadSingle();

                    int sLen = reader.ReadInt32();
                    var segStrokes = new List<Stroke>(sLen);
                    for (int j = 0; j < sLen; j++)
                    {
                        int idx = reader.ReadInt32();
                        if (idx >= 0 && idx < strokes.Count)
                            segStrokes.Add(strokes[idx]);
                    }

                    segments.Add(new HandwritingWordSegment(
                        text,
                        candidates,
                        new Rect(left, top, width, height),
                        segStrokes));
                }

                if (!success && segments.Count == 0)
                    return HandwritingRecognitionResult.Empty;

                return new HandwritingRecognitionResult(segments);
            }
        }

        private void EnsureSharedMemory(int requiredCapacity)
        {
            if (requiredCapacity > MaxSharedMemoryCapacity)
                throw new InvalidOperationException("IACore shared memory request is too large.");

            if (_sharedMemory != null && _sharedMemoryCapacity >= requiredCapacity)
                return;

            int capacity = DefaultSharedMemoryCapacity;
            while (capacity < requiredCapacity)
                capacity *= 2;

            GrowSharedMemory(capacity);
        }

        private void GrowSharedMemory(int requiredCapacity)
        {
            if (requiredCapacity > MaxSharedMemoryCapacity)
                throw new InvalidOperationException("IACore shared memory response is too large.");

            int capacity = DefaultSharedMemoryCapacity;
            while (capacity < requiredCapacity)
                capacity *= 2;

            ReleaseSharedMemory();
            _sharedMemoryGeneration++;
            _sharedMemory = MemoryMappedFile.CreateOrOpen(SharedMemoryName, capacity, MemoryMappedFileAccess.ReadWrite);
            _sharedMemoryCapacity = capacity;
            // 让 helper 立即按新 generation 重开共享内存，避免下次共享内存命令仍走老句柄抛 FileNotFoundException。
            try { PingSharedMemoryGeneration(); }
            catch (Exception ex) { Debug.WriteLine("PingSharedMemoryGeneration failed: " + ex.Message); }
        }

        private void PingSharedMemoryGeneration()
        {
            if (!IsAvailable) return;
            lock (_pipeLock)
            {
                try
                {
                    using (var client = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut))
                    {
                        client.Connect(IpcTimeoutMs);
                        using (var w = new BinaryWriter(client, System.Text.Encoding.UTF8, leaveOpen: true))
                        using (var r = new BinaryReader(client, System.Text.Encoding.UTF8, leaveOpen: true))
                        {
                            w.Write(CmdPingSharedMemoryGeneration);
                            w.Write(_sharedMemoryGeneration);
                            w.Flush();
                            r.ReadInt32(); // status：失败也只意味着下次再 OpenExisting 重试
                        }
                    }
                }
                catch
                {
                    // 同步唤起失败无影响，下次真实请求时 helper 仍会按 generation 不匹配重开。
                }
            }
        }

        private static int GetRequestLength(StrokeCollection strokes)
        {
            checked
            {
                int length = sizeof(int);
                foreach (var stroke in strokes)
                    length += sizeof(int) + stroke.StylusPoints.Count * sizeof(float) * 3;
                return length;
            }
        }

        private void ReleaseSharedMemory()
        {
            _sharedMemory?.Dispose();
            _sharedMemory = null;
            _sharedMemoryCapacity = DefaultSharedMemoryCapacity;
        }

        private void EnsureHelperAlive()
        {
            // Start() 自带 _startGate 互斥，这里直接复用同一个 gate。
            Start();
        }

        private void OnHelperExited(object sender, EventArgs e)
        {
            _available = false;
            ReleaseSharedMemory();
        }

        private void KillHelper()
        {
            if (_helperProcess == null)
            {
                ReleaseSharedMemory();
                return;
            }
            try
            {
                try { _helperProcess.Exited -= OnHelperExited; } catch { }

                if (!_helperProcess.HasExited)
                {
                    try
                    {
                        using (var client = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut))
                        {
                            client.Connect(500);
                            using (var w = new BinaryWriter(client))
                                w.Write(CmdShutdown);
                        }
                    }
                    catch { }

                    if (!_helperProcess.WaitForExit(800))
                        _helperProcess.Kill();
                }
            }
            catch { }
            finally
            {
                _helperProcess?.Dispose();
                _helperProcess = null;
                _available = false;
                _helperJob?.Dispose();
                _helperJob = null;
                ReleaseSharedMemory();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            KillHelper();
        }

        private const string PipeNameFormat = "ICC_IACoreHelper_{0}";
        private const string SharedMemoryNameFormat = "ICC_IACoreHelper_Shared_{0}_{1}";
        private const int IpcTimeoutMs = 5000;
        private const int ProtocolVersion = 2;
        private const int SharedMemoryHeaderSize = 24;
        private const int DefaultSharedMemoryCapacity = 4 * 1024 * 1024;
        private const int MaxSharedMemoryCapacity = 32 * 1024 * 1024;
        private const int MinResponseCapacity = 4096;
        private const int SharedMemoryMagic = 0x49414348;
        private const int HeaderMagicOffset = 0;
        private const int HeaderVersionOffset = 4;
        private const int HeaderRequestLengthOffset = 8;
        private const int HeaderResponseOffsetOffset = 12;
        private const int HeaderResponseLengthOffset = 16;
        private const int HeaderStatusOffset = 20;
        private const int StatusOk = 0;
        private const int StatusResponseTooLarge = 2;
        private const byte CmdRecognizeSharedMemory = 0x02;
        private const byte CmdRecognizeTextSharedMemory = 0x03;
        private const byte CmdPingSharedMemoryGeneration = 0x04;
        private const byte CmdShutdown = 0xFF;
    }

    /// <summary>
    /// IACore 文字识别的上下文提示（对应 IAWinFX AnalysisHintNode 的属性层）。
    /// UWP WinRT InkAnalyzer 无法访问这些层；只有走 IPC 辅助进程才能注入 Factoid/WordList/WordMode/Coerce。
    /// HintLeft/Top/Width/Height 全 0 表示无限区域（属性作用于全部笔画）。
    /// </summary>
    public sealed class IacoreTextHint
    {
        public float HintLeft;
        public float HintTop;
        public float HintWidth;
        public float HintHeight;
        public string Factoid;
        public string[] WordList;
        public bool WordMode;
        public bool CoerceToFactoid;
    }
}