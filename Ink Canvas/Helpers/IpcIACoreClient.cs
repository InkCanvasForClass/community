using System;
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
        private MemoryMappedFile _sharedMemory;
        private int _sharedMemoryCapacity = DefaultSharedMemoryCapacity;
        private int _sharedMemoryGeneration;
        private readonly object _pipeLock = new object();
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
            if (IsAvailable) return true;

            if (!File.Exists(HelperExePath))
            {
                _available = false;
                return false;
            }

            return LaunchHelper();
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

        private bool LaunchHelper()
        {
            try
            {
                KillHelper();
                EnsureSharedMemory(DefaultSharedMemoryCapacity);

                var psi = new ProcessStartInfo
                {
                    FileName         = HelperExePath,
                    Arguments        = CurrentProcessId.ToString(),
                    UseShellExecute  = false,
                    CreateNoWindow   = true,
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
                    if (status == StatusResponseTooLarge)
                    {
                        GrowSharedMemory(_sharedMemoryCapacity * 2);
                        return SendRecognizeRequest(strokes);
                    }
                    if (status != StatusOk || responseLength <= 0)
                        return InkShapeRecognitionResult.Empty;

                    return ReadResponseFromSharedMemory(strokes, responseLength);
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
            if (!IsAvailable)
                LaunchHelper();
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
                _available     = false;
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
        private const byte CmdShutdown = 0xFF;
    }
}