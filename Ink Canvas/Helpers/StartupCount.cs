using System;
using System.IO;

namespace Ink_Canvas.Helpers
{
    public static class StartupCount
    {
        private static readonly string CountFilePath = Path.Combine(App.RootPath, "startup-count");
        private static readonly object fileLock = new object();

        public static int GetCount()
        {
            try
            {
                if (File.Exists(CountFilePath))
                {
                    var text = File.ReadAllText(CountFilePath).Trim();
                    if (int.TryParse(text, out int count))
                        return count;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
            return 0;
        }

        public static void Increment()
        {
            lock (fileLock)
            {
                int count = GetCount() + 1;
                try
                {
                    File.WriteAllText(CountFilePath, count.ToString());
                    LogHelper.WriteLogToFile($"[Crash] 熔断计数已递增: {count}", LogHelper.LogType.Info);
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
            }
        }

        public static void Reset()
        {
            lock (fileLock)
            {
                try
                {
                    if (File.Exists(CountFilePath))
                    {
                        File.Delete(CountFilePath);
                        LogHelper.WriteLogToFile("[Crash] 熔断计数已重置（已稳定运行）", LogHelper.LogType.Info);
                    }
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
            }
        }
    }
}
