using System;
using System.Collections.Generic;
using System.IO;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 跨进程、跨软件重启复用 PPT 幻灯片缩略图。
    /// 缓存保存在对应 PPT 的自动墨迹保存目录下，与 PPTInkManager 使用相同的文档目录标识；
    /// 使用文件长度和最后写入时间检测文档版本，不读取整个 PPT 文件计算内容哈希。
    /// </summary>
    internal static class PPTThumbnailDiskCache
    {
        public static bool TryLoad(string presentationPath, string presentationName, int width, int height, int slideCount,
            out List<PPTSlideThumbnail> thumbnails)
        {
            thumbnails = null;
            if (!TryGetCacheDirectory(presentationPath, presentationName, width, height, slideCount, out var directory))
                return false;

            try
            {
                var loaded = new List<PPTSlideThumbnail>(slideCount);
                for (int i = 1; i <= slideCount; i++)
                {
                    var file = Path.Combine(directory, "slide_" + i.ToString("D4") + ".png");
                    if (!File.Exists(file)) return false;
                    var bytes = File.ReadAllBytes(file);
                    if (bytes.Length == 0) return false;
                    loaded.Add(new PPTSlideThumbnail { SlideNumber = i, PngBytes = bytes });
                }

                thumbnails = loaded;
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"读取 PPT 磁盘缩略图缓存失败: {ex.Message}", LogHelper.LogType.Trace);
                thumbnails = null;
                return false;
            }
        }

        public static void Save(string presentationPath, string presentationName, int width, int height, int slideCount,
            IList<PPTSlideThumbnail> thumbnails)
        {
            if (thumbnails == null || thumbnails.Count != slideCount ||
                !TryGetCacheDirectory(presentationPath, presentationName, width, height, slideCount, out var directory))
                return;

            string parent = Path.GetDirectoryName(directory);
            string tempDirectory = Path.Combine(parent, ".tmp-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(tempDirectory);
                foreach (var thumbnail in thumbnails)
                {
                    if (thumbnail == null || thumbnail.SlideNumber < 1 ||
                        thumbnail.SlideNumber > slideCount || thumbnail.PngBytes == null ||
                        thumbnail.PngBytes.Length == 0)
                        return;

                    File.WriteAllBytes(
                        Path.Combine(tempDirectory, "slide_" + thumbnail.SlideNumber.ToString("D4") + ".png"),
                        thumbnail.PngBytes);
                }

                Directory.CreateDirectory(parent);
                if (!Directory.Exists(directory))
                {
                    try { Directory.Move(tempDirectory, directory); }
                    catch (IOException) when (Directory.Exists(directory)) { }
                }

                // 同一文档只保留当前版本，避免反复编辑 PPT 后缓存无限增长。
                foreach (var oldDirectory in Directory.GetDirectories(parent))
                {
                    if (string.Equals(oldDirectory, directory, StringComparison.OrdinalIgnoreCase) ||
                        Path.GetFileName(oldDirectory).StartsWith(".tmp-", StringComparison.OrdinalIgnoreCase))
                        continue;
                    try { Directory.Delete(oldDirectory, true); } catch { }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"写入 PPT 磁盘缩略图缓存失败: {ex.Message}", LogHelper.LogType.Trace);
            }
            finally
            {
                try { if (Directory.Exists(tempDirectory)) Directory.Delete(tempDirectory, true); } catch { }
            }
        }

        private static bool TryGetCacheDirectory(string presentationPath, string presentationName, int width, int height,
            int slideCount, out string directory)
        {
            directory = null;
            if (string.IsNullOrWhiteSpace(presentationPath)) return false;

            try
            {
                var fullPath = Path.GetFullPath(presentationPath);
                var file = new FileInfo(fullPath);
                if (!file.Exists || width <= 0 || height <= 0 || slideCount <= 0) return false;

                string autoSaveLocation = MainWindow.Settings?.Automation?.AutoSavedStrokesLocation;
                if (string.IsNullOrWhiteSpace(autoSaveLocation) || string.IsNullOrWhiteSpace(presentationName)) return false;

                // 与 PPTInkManager 使用相同的演示文稿目录标识。
                string presentationId = presentationName + "_" + slideCount.ToString() + "_" + HashHelper.GetFileHash(fullPath);
                string presentationInkDirectory = Path.Combine(autoSaveLocation, "Auto Saved - Presentations", presentationId);
                string version = file.Length.ToString() + "-" + file.LastWriteTimeUtc.Ticks.ToString() +
                    "-" + slideCount.ToString() + "-" + width.ToString() + "x" + height.ToString();
                directory = Path.Combine(presentationInkDirectory, "Thumbnails", version);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
