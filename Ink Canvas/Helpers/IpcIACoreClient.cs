using System;
using System.Diagnostics;
using System.IO;
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
        private readonly object _pipeLock = new object();
        private bool _disposed;
        private bool _available;

        private static string HelperExePath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "InkCanvas.IACoreHelper.exe");

        private string PipeName =>
            string.Format("ICC_IACoreHelper_{0}", Process.GetCurrentProcess().Id);

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

                var psi = new ProcessStartInfo
                {
                    FileName         = HelperExePath,
                    Arguments        = Process.GetCurrentProcess().Id.ToString(),
                    UseShellExecute  = false,
                    CreateNoWindow   = true,
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
                };
                _helperProcess = Process.Start(psi);
                if (_helperProcess == null)
                {
                    _available = false;
                    return false;
                }
                _helperProcess.EnableRaisingEvents = true;
                _helperProcess.Exited += OnHelperExited;

                bool pipeReady = WaitForPipe(3000);
                _available = pipeReady;
                return pipeReady;
            }
            catch
            {
                _available = false;
                return false;
            }
        }

        private bool WaitForPipe(int timeoutMs)
        {
            int elapsed = 0;
            while (elapsed < timeoutMs)
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
                    elapsed += 300;
                }
            }
            return false;
        }

        private InkShapeRecognitionResult SendRecognizeRequest(StrokeCollection strokes)
        {
            using (var client = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut))
            {
                client.Connect(IpcTimeoutMs);

                using (var writer = new BinaryWriter(client, System.Text.Encoding.UTF8, leaveOpen: true))
                using (var reader = new BinaryReader(client, System.Text.Encoding.UTF8, leaveOpen: true))
                {
                    writer.Write(CmdRecognize);
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

                    bool success   = reader.ReadBoolean();
                    string shape   = reader.ReadString();
                    float cx       = reader.ReadSingle();
                    float cy       = reader.ReadSingle();
                    float width    = reader.ReadSingle();
                    float height   = reader.ReadSingle();

                    int hotLen     = reader.ReadInt32();
                    var hotPoints  = new PointCollection();
                    for (int i = 0; i < hotLen; i++)
                        hotPoints.Add(new Point(reader.ReadSingle(), reader.ReadSingle()));

                    int idxLen     = reader.ReadInt32();
                    var indices    = new int[idxLen];
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
        }

        private void EnsureHelperAlive()
        {
            if (!IsAvailable)
                LaunchHelper();
        }

        private void OnHelperExited(object sender, EventArgs e)
        {
            _available = false;
        }

        private void KillHelper()
        {
            if (_helperProcess == null) return;
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
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            KillHelper();
        }

        private const int  IpcTimeoutMs = 5000;
        private const byte CmdRecognize = 0x01;
        private const byte CmdShutdown  = 0xFF;
    }
}