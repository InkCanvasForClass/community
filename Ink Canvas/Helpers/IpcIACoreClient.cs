using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Media;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 通过 Named Pipe IPC 调用 32 位辅助进程 InkCanvasForClass.IACoreHelper.exe 进行 IACore 形状识别。
    /// 单例，线程安全，辅助进程崩溃后自动重启。
    /// </summary>
    public sealed class IpcIACoreClient : IDisposable
    {
        // ── 单例 ──────────────────────────────────────────────────────────────
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

        // ── 状态 ──────────────────────────────────────────────────────────────
        private Process _helperProcess;
        private readonly object _pipeLock = new object();
        private bool _disposed;
        private bool _available;

        // 辅助进程 EXE 的路径（与主 EXE 同目录）
        private static string HelperExePath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "InkCanvasForClass.IACoreHelper.exe");

        // Named Pipe 名称（含主进程 PID，保证唯一）
        private string PipeName =>
            string.Format("ICC_IACoreHelper_{0}", Process.GetCurrentProcess().Id);

        private IpcIACoreClient() { }

        // ── 公开 API ──────────────────────────────────────────────────────────

        /// <summary>启动辅助进程，返回是否成功。</summary>
        public bool Start()
        {
            if (_disposed) return false;

            // 已运行则无需重启，避免 Warmup + App 两处调用导致重复启动
            if (IsAvailable) return true;

            if (!File.Exists(HelperExePath))
            {
                LogHelper.WriteLogToFile($"[IACoreIPC] 辅助进程不存在: {HelperExePath}", LogHelper.LogType.Warning);
                _available = false;
                return false;
            }

            return LaunchHelper();
        }

        /// <summary>是否就绪（辅助进程运行中）。</summary>
        public bool IsAvailable => _available && _helperProcess != null && !_helperProcess.HasExited;

        /// <summary>
        /// 发送笔画到辅助进程进行 IACore 形状识别。
        /// 超时或失败时返回 <see cref="InkShapeRecognitionResult.Empty"/>。
        /// </summary>
        public InkShapeRecognitionResult Recognize(StrokeCollection strokes)
        {
            if (strokes == null || strokes.Count == 0)
                return InkShapeRecognitionResult.Empty;

            EnsureHelperAlive();
            if (!IsAvailable)
            {
                LogHelper.WriteLogToFile($"[IACoreIPC] Recognize 跳过：辅助进程不可用（strokes={strokes.Count}）", LogHelper.LogType.Warning);
                return InkShapeRecognitionResult.Empty;
            }

            lock (_pipeLock)
            {
                try
                {
                    LogHelper.WriteLogToFile($"[IACoreIPC] Recognize 请求：strokes={strokes.Count}");
                    var result = SendRecognizeRequest(strokes);
                    LogHelper.WriteLogToFile(
                        $"[IACoreIPC] Recognize 响应：IsSuccess={result.IsSuccess}, Shape={result.ShapeName}, StrokesToRemove={result.StrokesToRemove?.Count ?? 0}");
                    return result;
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"[IACoreIPC] 识别失败: {ex.GetType().Name}: {ex.Message}", LogHelper.LogType.Error);
                    KillHelper();
                    return InkShapeRecognitionResult.Empty;
                }
            }
        }

        // ── 内部实现 ──────────────────────────────────────────────────────────

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

                // 等待 Pipe 就绪（最多 3 s）
                bool pipeReady = WaitForPipe(3000);
                _available = pipeReady;

                LogHelper.WriteLogToFile($"[IACoreIPC] 辅助进程启动{(pipeReady ? "成功" : "失败（Pipe 未就绪）")}，PID={_helperProcess?.Id}");
                return pipeReady;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[IACoreIPC] 启动辅助进程失败: {ex.Message}", LogHelper.LogType.Error);
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
                        // 连接成功后立即断开，下次 Recognize 会重新连接
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
                    // 序列化请求
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

                    // 读取响应
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

                    // 根据下标还原参与识别的笔画子集
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
            {
                LogHelper.WriteLogToFile("[IACoreIPC] 辅助进程不可用，尝试重启...", LogHelper.LogType.Warning);
                LaunchHelper();
            }
        }

        private void OnHelperExited(object sender, EventArgs e)
        {
            _available = false;
            LogHelper.WriteLogToFile("[IACoreIPC] 辅助进程意外退出", LogHelper.LogType.Warning);
        }

        private void KillHelper()
        {
            if (_helperProcess == null) return;
            try
            {
                // 先解除事件订阅，避免主动 Kill 时触发 OnHelperExited 误报
                try { _helperProcess.Exited -= OnHelperExited; } catch { }

                if (!_helperProcess.HasExited)
                {
                    // 发送优雅关闭命令
                    try
                    {
                        using (var client = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut))
                        {
                            client.Connect(500);
                            using (var w = new BinaryWriter(client))
                                w.Write(CmdShutdown);
                        }
                    }
                    catch { /* 忽略，下面直接 Kill */ }

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

        // ── 协议常量（与 IACoreHelper/IpcProtocol.cs 保持一致） ─────────────
        private const int  IpcTimeoutMs = 5000;
        private const byte CmdRecognize = 0x01;
        private const byte CmdShutdown  = 0xFF;
    }
}