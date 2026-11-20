using System;
using System.IO;

namespace Ink_Canvas.Helpers
{
    public static class StartupCount
    {
        private static readonly string CountFilePath = Path.Combine(App.RootPath, "startup-count");
        private static readonly object fileLock = new object();

        // 崩溃重启计数仅在同一故障时间窗内有效：历史残留计数（如数天前的连续崩溃）
        // 会让后续偶发的一次崩溃直接误触"重启次数已达上限"弹窗并拒绝重启。
        private static readonly TimeSpan CountTimeWindow = TimeSpan.FromMinutes(10);

        public static int GetCount()
        {
            try
            {
                if (File.Exists(CountFilePath))
                {
                    // 超过时间窗的计数文件视为过期，按 0 处理
                    var lastWrite = File.GetLastWriteTime(CountFilePath);
                    if (DateTime.Now - lastWrite > CountTimeWindow)
                    {
                        return 0;
                    }

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
                        File.Delete(CountFilePath);
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
            }
        }
    }
}
