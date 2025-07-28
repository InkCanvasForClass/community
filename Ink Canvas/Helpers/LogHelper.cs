using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Ink_Canvas.Helpers
{
    class LogHelper
    {
        public static string LogFile = "Log.txt";
        private static string LogsFolder = "Logs";
        private static string AppStartTime = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
        private static readonly long MaxLogsFolderSizeBytes = 5 * 1024 * 1024; // 5MB

        public static void NewLog(string str)
        {
            WriteLogToFile(str);
        }

        public static void NewLog(Exception ex)
        {
            if (ex == null) return;
            var stackTrace = ex.StackTrace ?? "<no stack trace>";
            var msg = $"[Exception] Type: {ex.GetType().FullName}\nMessage: {ex.Message}\nStackTrace: {stackTrace}";
            if (ex.InnerException != null)
            {
                msg += $"\nInnerException: {ex.InnerException.GetType().FullName} - {ex.InnerException.Message}\n{ex.InnerException.StackTrace}";
            }
            WriteLogToFile(msg, LogType.Error);
        }

        public static void WriteLogToFile(string str, LogType logType = LogType.Info)
        {
            // 检查日志是否启用
            if (MainWindow.Settings != null && MainWindow.Settings.Advanced != null && !MainWindow.Settings.Advanced.IsLogEnabled) return;
            
            string strLogType = logType.ToString();
            try
            {
                string file;
                
                // 检查是否启用了日期保存功能
                if (MainWindow.Settings != null && MainWindow.Settings.Advanced != null && MainWindow.Settings.Advanced.IsSaveLogByDate)
                {
                    // 确保Logs文件夹存在
                    string logsPath = Path.Combine(App.RootPath, LogsFolder);
                    if (!Directory.Exists(logsPath))
                    {
                        Directory.CreateDirectory(logsPath);
                    }
                    
                    // 检查Logs文件夹大小，如果超过5MB则清空
                    CheckAndCleanLogsFolder(logsPath);
                    
                    // 使用软件启动时间作为日志文件名
                    file = Path.Combine(logsPath, $"Log_{AppStartTime}.txt");
                }
                else
                {
                    file = App.RootPath + LogFile;
                }
                
                if (!Directory.Exists(App.RootPath))
                {
                    Directory.CreateDirectory(App.RootPath);
                }
                
                var threadId = Thread.CurrentThread.ManagedThreadId;
                var callingMethod = new StackTrace(2, true).GetFrame(0);
                string callerInfo = "<unknown>";
                if (callingMethod != null)
                {
                    var method = callingMethod.GetMethod();
                    if (method != null)
                    {
                        var className = method.DeclaringType != null ? method.DeclaringType.FullName : "<no class>";
                        callerInfo = $"{className}.{method.Name}";
                    }
                }
                string logLine = string.Format("{0} [T{1}] [{2}] [{3}] {4}", DateTime.Now.ToString("O"), threadId, strLogType, callerInfo, str);
                using (StreamWriter sw = new StreamWriter(file, true))
                {
                    sw.WriteLine(logLine);
                }
            }
            catch { }
        }

        private static void CheckAndCleanLogsFolder(string logsPath)
        {
            try
            {
                long totalSize = 0;
                DirectoryInfo dirInfo = new DirectoryInfo(logsPath);
                
                // 如果目录不存在，直接返回
                if (!dirInfo.Exists) return;
                
                // 计算文件夹大小
                foreach (FileInfo file in dirInfo.GetFiles())
                {
                    totalSize += file.Length;
                }
                
                // 如果超过5MB，清空文件夹
                if (totalSize > MaxLogsFolderSizeBytes)
                {
                    foreach (FileInfo file in dirInfo.GetFiles())
                    {
                        try
                        {
                            file.Delete();
                        }
                        catch { }
                    }
                    
                    // 记录清理操作
                    string cleanupMessage = $"Logs folder exceeded size limit ({totalSize / 1024.0 / 1024.0:F2} MB > {MaxLogsFolderSizeBytes / 1024.0 / 1024.0:F2} MB). Folder cleaned.";
                    using (StreamWriter sw = new StreamWriter(Path.Combine(logsPath, $"Log_{AppStartTime}.txt"), true))
                    {
                        sw.WriteLine($"{DateTime.Now:O} [Cleanup] {cleanupMessage}");
                    }
                }
            }
            catch { }
        }

        internal static void WriteLogToFile(string v, object warning)
        {
            WriteLogToFile($"[Warning] {v}", LogType.Warning);
        }

        public enum LogType
        {
            Info,
            Trace,
            Error,
            Event,
            Warning
        }
    }
}
