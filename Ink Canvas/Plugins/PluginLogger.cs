using Ink_Canvas.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 插件独立日志器。将每个插件的输出写入独立目录下的按日轮转文件中。
    /// <para>文件路径：<c>PluginLogs/<plugin-id>/<yyyy-MM-dd>.log</c>。</para>
    /// </summary>
    public class PluginLogger
    {
        private readonly string _logsRoot;
        private readonly string _pluginId;
        private readonly object _writeLock = new object();

        /// <summary>
        /// 单文件最大字节数（默认 1MB），超过后会重命名为 <c>.1.log</c> 并新建。
        /// </summary>
        public long MaxFileSizeBytes { get; set; } = 1L * 1024 * 1024;

        /// <summary>
        /// 同目录下允许保留的轮转文件数。
        /// </summary>
        public int RetainedFiles { get; set; } = 7;

        public PluginLogger(string logsRoot, string pluginId)
        {
            _logsRoot = logsRoot ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PluginLogs");
            _pluginId = SanitizeId(pluginId);
        }

        /// <summary>
        /// 写入 Info 级别日志。
        /// </summary>
        public void Info(string source, string message) => Write("INFO", source, message, null);

        /// <summary>
        /// 写入 Warn 级别日志。
        /// </summary>
        public void Warn(string source, string message, Exception ex = null) => Write("WARN", source, message, ex);

        /// <summary>
        /// 写入 Error 级别日志。
        /// </summary>
        public void Error(string source, string message, Exception ex = null) => Write("ERROR", source, message, ex);

        /// <summary>
        /// 写入 Debug 级别日志。
        /// </summary>
        public void Debug(string source, string message) => Write("DEBUG", source, message, null);

        /// <summary>
        /// 获取今日日志文件路径。
        /// </summary>
        public string GetCurrentLogFilePath()
        {
            return Path.Combine(GetDayDirectory(), DateTime.Now.ToString("yyyy-MM-dd") + ".log");
        }

        private void Write(string level, string source, string message, Exception ex)
        {
            try
            {
                var line = FormatLine(level, source, message, ex);
                var path = GetCurrentLogFilePath();
                lock (_writeLock)
                {
                    if (ShouldRotate(path))
                    {
                        Rotate();
                    }
                    File.AppendAllText(path, line);
                }
            }
            catch (Exception logEx)
            {
                // 兜底：避免日志失败影响主流程
                LogHelper.WriteLogToFile(
                    $"PluginLogger | 写入失败 [{_pluginId}] [{level}] {logEx.Message}",
                    LogHelper.LogType.Warning);
            }
        }

        private static string FormatLine(string level, string source, string message, Exception ex)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append('[').Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")).Append("] ");
            sb.Append('[').Append(level).Append("] ");
            if (!string.IsNullOrEmpty(source)) sb.Append('[').Append(source).Append("] ");
            sb.Append(message ?? "");
            if (ex != null)
            {
                sb.Append(" | ").Append(ex.GetType().Name).Append(": ").Append(ex.Message);
                if (!string.IsNullOrEmpty(ex.StackTrace))
                {
                    sb.AppendLine();
                    sb.Append(ex.StackTrace);
                }
            }
            sb.Append(Environment.NewLine);
            return sb.ToString();
        }

        private bool ShouldRotate(string path)
        {
            try
            {
                if (!File.Exists(path)) return false;
                var len = new FileInfo(path).Length;
                return len >= MaxFileSizeBytes;
            }
            catch
            {
                return false;
            }
        }

        private void Rotate()
        {
            try
            {
                var dir = GetDayDirectory();
                var fileName = DateTime.Now.ToString("yyyy-MM-dd") + ".log";
                var current = Path.Combine(dir, fileName);

                // 删除最旧
                var oldest = Path.Combine(dir, $"{fileName}.{RetainedFiles}.log");
                if (File.Exists(oldest)) File.Delete(oldest);

                // 滚动编号
                for (var i = RetainedFiles - 1; i >= 1; i--)
                {
                    var from = Path.Combine(dir, $"{fileName}.{i}.log");
                    var to = Path.Combine(dir, $"{fileName}.{i + 1}.log");
                    if (File.Exists(from))
                    {
                        if (File.Exists(to)) File.Delete(to);
                        File.Move(from, to);
                    }
                }

                if (File.Exists(current))
                {
                    var to = Path.Combine(dir, $"{fileName}.1.log");
                    if (File.Exists(to)) File.Delete(to);
                    File.Move(current, to);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile(
                    $"PluginLogger | 轮转失败 [{_pluginId}] {ex.Message}",
                    LogHelper.LogType.Warning);
            }
        }

        private string GetDayDirectory()
        {
            var dir = Path.Combine(_logsRoot, _pluginId);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return dir;
        }

        /// <summary>
        /// 取出当日全部插件日志大小（字节），便于 UI 显示。
        /// </summary>
        public long GetCurrentLogSize()
        {
            try
            {
                var path = GetCurrentLogFilePath();
                return File.Exists(path) ? new FileInfo(path).Length : 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 列出该插件对应的所有日志文件（含轮转备份）。
        /// </summary>
        public IEnumerable<string> EnumerateLogFiles()
        {
            var dir = GetDayDirectory();
            if (!Directory.Exists(dir)) yield break;
            foreach (var f in Directory.EnumerateFiles(dir, "*.log", SearchOption.TopDirectoryOnly))
            {
                yield return f;
            }
        }

        /// <summary>
        /// 读取今日日志文件全部内容（用于 UI 调试器）。
        /// </summary>
        public string ReadAll()
        {
            var path = GetCurrentLogFilePath();
            if (!File.Exists(path)) return "";
            try
            {
                return File.ReadAllText(path);
            }
            catch
            {
                return "";
            }
        }

        private static string SanitizeId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "unknown";
            var invalid = Path.GetInvalidFileNameChars();
            var chars = id.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (Array.IndexOf(invalid, chars[i]) >= 0) chars[i] = '_';
            }
            return new string(chars);
        }
    }
}
