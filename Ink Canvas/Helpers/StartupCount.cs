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
            catch { }
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
                catch { }
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
                catch { }
            }
        }
    }
}
