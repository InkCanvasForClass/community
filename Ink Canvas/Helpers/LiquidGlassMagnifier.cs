using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 用独立进程 InkCanvas.LiquidGlassMagHost 抓取"某窗口正下方"的桌面像素。
    ///
    /// 为什么走独立进程：WPF 主进程的 DirectComposition 树会干扰同进程创建的放大镜
    /// 控件的合成，导致 PrintWindow 读回纯黑（同进程 STA 线程方案也无效，实测）。
    /// 独立进程拥有自己的 DComp 上下文，DWM 能正常合成放大镜内容并回传像素。
    ///
    /// 协议（与 InkCanvas.LiquidGlassMagHost 对齐）：
    ///   请求：long excludeHwnd (8) + int left,top,w,h (16) = 24 字节
    ///   响应：int status(4) + int w(4) + int h(4) + int stride(4) + int bytesLen(4)
    ///         + byte[bytesLen] BGRA
    /// status: 0=OK, 1=失败, 2=永久不可用
    /// </summary>
    internal static class LiquidGlassMagnifier
    {
        private const string HostExeName = "InkCanvas.LiquidGlassMagHost.exe";
        private const string HostExe86Name = "InkCanvas.LiquidGlassMagHost.exe"; // 单一名称，路径区分 x86/x64
        private const int RequestBytes = 24;
        private const int ResponseHeaderBytes = 20;

        private static readonly object Sync = new object();
        private static bool _available = true;
        private static Process _host;
        private static Stream _stdin;
        private static Stream _stdout;
        private static int _hostPid;

        internal static bool IsAvailable
        {
            get { lock (Sync) return _available; }
        }

        /// <summary>
        /// 抓取源矩形（桌面坐标，物理像素）下、排除 excludeHwnd 后的画面。
        /// 必须在 UI 线程调用。失败返回 null（调用方走 Hide/Show 回退）。
        /// </summary>
        internal static BitmapSource CaptureRegion(IntPtr excludeHwnd, int left, int top, int width, int height)
        {
            if (width <= 0 || height <= 0) return null;
            if (excludeHwnd == IntPtr.Zero) return null;

            lock (Sync)
            {
                if (!_available) return null;
            }

            try
            {
                if (!EnsureHost()) return null;

                var req = new byte[RequestBytes];
                BitConverter.GetBytes(excludeHwnd.ToInt64()).CopyTo(req, 0);
                BitConverter.GetBytes(left).CopyTo(req, 8);
                BitConverter.GetBytes(top).CopyTo(req, 12);
                BitConverter.GetBytes(width).CopyTo(req, 16);
                BitConverter.GetBytes(height).CopyTo(req, 20);

                Stream stdin, stdout;
                lock (Sync)
                {
                    if (!_available || _stdin == null || _stdout == null) return null;
                    stdin = _stdin;
                    stdout = _stdout;
                }

                try
                {
                    stdin.Write(req, 0, RequestBytes);
                    stdin.Flush();
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"液态玻璃放大镜写入请求失败: {ex.Message}",
                        LogHelper.LogType.Warning);
                    MarkHostDead();
                    return null;
                }

                var header = new byte[ResponseHeaderBytes];
                int got = ReadExact(stdout, header, ResponseHeaderBytes);
                if (got < ResponseHeaderBytes)
                {
                    // EOF（got=0）或读截断：通常意味着子进程已退出。
                    Process procRef;
                    lock (Sync) { procRef = _host; }
                    bool exited = procRef?.HasExited ?? true;
                    int exitCode = (exited && procRef != null) ? procRef.ExitCode : -1;
                    LogHelper.WriteLogToFile(
                        $"液态玻璃放大镜读取响应头失败: got={got} 子进程已退出={exited} ExitCode={exitCode}",
                        LogHelper.LogType.Warning);
                    MarkHostDead();
                    return null;
                }

                int status = BitConverter.ToInt32(header, 0);
                int outW = BitConverter.ToInt32(header, 4);
                int outH = BitConverter.ToInt32(header, 8);
                int outStride = BitConverter.ToInt32(header, 12);
                int bytesLen = BitConverter.ToInt32(header, 16);

                if (status != 0 || bytesLen <= 0 || outW <= 0 || outH <= 0)
                {
                    if (status == 2)
                    {
                        // 子进程报告 Magnification 永久不可用
                        lock (Sync) { _available = false; }
                        LogHelper.WriteLogToFile("液态玻璃放大镜子进程报告永久不可用，回退到 Hide/Show 抓屏",
                            LogHelper.LogType.Warning);
                    }
                    else
                    {
                        LogHelper.WriteLogToFile(
                            $"液态玻璃放大镜抓取失败: status={status} bytes={bytesLen} 源=({left},{top},{width}x{height})",
                            LogHelper.LogType.Warning);
                    }
                    return null;
                }

                var pixels = new byte[bytesLen];
                got = ReadExact(stdout, pixels, bytesLen);
                if (got < bytesLen)
                {
                    LogHelper.WriteLogToFile($"液态玻璃放大镜读取像素失败: got={got} expected={bytesLen}",
                        LogHelper.LogType.Warning);
                    MarkHostDead();
                    return null;
                }

                // BGRA 字节 → BitmapSource（跨线程安全：Freeze 后跨进程/线程访问）
                var bitmap = BitmapSource.Create(
                    outW, outH, 96, 96,
                    PixelFormats.Bgr32, null,
                    pixels, outStride);
                bitmap.Freeze();
                return bitmap;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"液态玻璃放大镜异常: {ex.Message}", LogHelper.LogType.Warning);
                return null;
            }
        }

        internal static void Shutdown()
        {
            Process host;
            lock (Sync)
            {
                host = _host;
                _host = null;
                _stdin = null;
                _stdout = null;
            }
            if (host == null) return;
            try
            {
                if (!host.HasExited)
                {
                    try { host.StandardInput.Close(); } catch { }
                    try { host.Kill(); } catch { }
                    host.WaitForExit(1000);
                }
            }
            catch { /* 退出期 */ }
            try { host.Dispose(); } catch { }
        }

        private static bool EnsureHost()
        {
            lock (Sync)
            {
                if (!_available) return false;
                if (_host != null && !_host.HasExited && _stdin != null && _stdout != null)
                    return true;
            }

            // 找到子进程 exe：与主 exe 同目录
            string hostPath = LocateHostExe();
            if (hostPath == null)
            {
                LogHelper.WriteLogToFile(
                    "液态玻璃放大镜子进程未找到：" + HostExeName + "，回退到 Hide/Show 抓屏",
                    LogHelper.LogType.Warning);
                lock (Sync) { _available = false; }
                return false;
            }

            var psi = new ProcessStartInfo
            {
                FileName = hostPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            Process proc;
            try
            {
                proc = Process.Start(psi);
                if (proc != null)
                {
                    // 把子进程 stderr 异步转发到主进程日志，崩溃信息不会丢
                    proc.ErrorDataReceived += (s, e) =>
                    {
                        if (e.Data != null)
                            LogHelper.WriteLogToFile($"[MagHost] {e.Data}", LogHelper.LogType.Warning);
                    };
                    proc.BeginErrorReadLine();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"液态玻璃放大镜子进程启动失败: {ex.Message}", LogHelper.LogType.Warning);
                lock (Sync) { _available = false; }
                return false;
            }

            lock (Sync)
            {
                if (!_available)
                {
                    try { proc.Kill(); } catch { }
                    return false;
                }
                _host = proc;
                _stdin = proc.StandardInput.BaseStream;
                _stdout = proc.StandardOutput.BaseStream;
                _hostPid = proc.Id;
            }
            LogHelper.WriteLogToFile($"液态玻璃放大镜子进程已启动: pid={proc.Id}", LogHelper.LogType.Warning);
            return true;
        }

        private static string LocateHostExe()
        {
            string mainExe = Process.GetCurrentProcess().MainModule.FileName;
            string dir = Path.GetDirectoryName(mainExe);
            string direct = Path.Combine(dir, HostExeName);
            if (File.Exists(direct)) return direct;

            // 备用：向上找一级（开发期 obj 输出可能不在同一目录）
            string parent = Path.GetDirectoryName(dir);
            if (parent != null)
            {
                string p = Path.Combine(parent, HostExeName);
                if (File.Exists(p)) return p;
            }
            return null;
        }

        private static int ReadExact(Stream s, byte[] buf, int n)
        {
            int total = 0;
            while (total < n)
            {
                int r;
                try { r = s.Read(buf, total, n - total); }
                catch { return total; }
                if (r == 0) return total;
                total += r;
            }
            return total;
        }

        private static void MarkHostDead()
        {
            lock (Sync)
            {
                try { _host?.Kill(); } catch { }
                _host = null;
                _stdin = null;
                _stdout = null;
            }
        }
    }
}