using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 重构期吞异常治理用最小日志设施。
    /// 写入 %AppData%/InkCanvasForClass/logs/refactor.log，单文件上限 1MB，滚动保留 3 个。
    /// </summary>
    internal static class LogService
    {
        private const long MaxFileSizeBytes = 1024 * 1024;
        private const int RetainedFiles = 3;
        private static readonly object WriteLock = new object();

        private static string LogDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "InkCanvasForClass", "logs");

        public static void LogException(Exception ex, [CallerMemberName] string member = "")
        {
            LogExceptionCore(ex, member, LogDirectory, MaxFileSizeBytes);
        }

        /// <summary>
        /// 写入核心（可测试性参数化）。生产路径 <see cref="LogException"/> 以默认值调用本方法，
        /// 行为逐字节不变；测试可注入临时目录与小阈值验证写入与滚动。
        /// </summary>
        internal static void LogExceptionCore(Exception ex, string member, string logDirectory, long maxFileSizeBytes)
        {
            try
            {
                lock (WriteLock)
                {
                    Directory.CreateDirectory(logDirectory);
                    var logFile = Path.Combine(logDirectory, "refactor.log");
                    RollIfNeeded(logFile, maxFileSizeBytes);
                    File.AppendAllText(logFile,
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{member}] {ex}{Environment.NewLine}");
                }
            }
            catch
            {
                // 日志设施自身失败时静默放弃，绝不影响主流程。
            }
        }

        private static void RollIfNeeded(string logFile, long maxFileSizeBytes)
        {
            if (!File.Exists(logFile) || new FileInfo(logFile).Length < maxFileSizeBytes) return;
            for (var i = RetainedFiles - 1; i >= 1; i--)
            {
                var older = $"{logFile}.{i}";
                var newer = $"{logFile}.{i + 1}";
                if (File.Exists(newer)) File.Delete(newer);
                if (File.Exists(older)) File.Move(older, newer);
            }
            File.Move(logFile, $"{logFile}.1");
        }
    }
}
