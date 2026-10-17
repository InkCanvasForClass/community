using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace InkCanvas.IACoreHelper
{
    /// <summary>
    /// 重构期吞异常治理用最小日志设施。
    /// 写入 %AppData%/InkCanvasForClass/logs/refactor.log，单文件上限 1MB，滚动保留 3 个。
    /// 与主程序集 Ink_Canvas.Helpers.LogService 同逻辑；本程序集未引用主程序集，故保留本地副本。
    /// </summary>
    internal static class LogService
    {
        private const long MaxFileSizeBytes = 1024 * 1024;
        private const int RetainedFiles = 3;
        private static readonly object WriteLock = new object();

        private static string LogDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "InkCanvasForClass", "logs");

        private static string LogFile => Path.Combine(LogDirectory, "refactor.log");

        public static void LogException(Exception ex, [CallerMemberName] string member = "")
        {
            try
            {
                lock (WriteLock)
                {
                    Directory.CreateDirectory(LogDirectory);
                    RollIfNeeded();
                    File.AppendAllText(LogFile,
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{member}] {ex}{Environment.NewLine}");
                }
            }
            catch
            {
                // 日志设施自身失败时静默放弃，绝不影响主流程。
            }
        }

        private static void RollIfNeeded()
        {
            if (!File.Exists(LogFile) || new FileInfo(LogFile).Length < MaxFileSizeBytes) return;
            for (var i = RetainedFiles - 1; i >= 1; i--)
            {
                var older = $"{LogFile}.{i}";
                var newer = $"{LogFile}.{i + 1}";
                if (File.Exists(newer)) File.Delete(newer);
                if (File.Exists(older)) File.Move(older, newer);
            }
            File.Move(LogFile, $"{LogFile}.1");
        }
    }
}
