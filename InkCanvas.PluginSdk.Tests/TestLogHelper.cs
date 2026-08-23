using System;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// Lightweight LogHelper stub for the SDK test harness. The test project links
    /// host plugin files such as PluginCompatibility, which only need a logging sink;
    /// this avoids pulling the full host logging pipeline into the test project.
    /// </summary>
    public static class LogHelper
    {
        public enum LogType
        {
            Trace,
            Info,
            Warning,
            Error
        }

        public static void WriteLogToFile(string message, LogType type = LogType.Info)
        {
            // Test harness intentionally ignores host logging.
        }
    }
}
