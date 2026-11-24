using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ink_Canvas.Helpers
{
    /// <summary>PPT 缩略图持久化缓存。目录身份与 PPTInkManager 共用，页文件独立放在 Thumbnails 中。</summary>
    internal static class PPTThumbnailDiskCache
    {
        private const long MaxCacheBytes = 512L * 1024 * 1024;
        private static readonly TimeSpan MaxCacheAge = TimeSpan.FromDays(30);
        private static readonly TimeSpan TempDirectoryMaxAge = TimeSpan.FromDays(1);

        public static bool TryLoad(string presentationPath, string presentationName, int width, int height,
            int slideCount, out List<PPTSlideThumbnail> thumbnails)
        {
            thumbnails = null;
            if (!TryGetCacheDirectory(presentationPath, presentationName, width, height, slideCount, out var directory))
                return false;

            try
            {
                var loaded = new List<PPTSlideThumbnail>(slideCount);
                for (int i = 1; i <= slideCount; i++)
                {
                    string file = Path.Combine(directory, i.ToString("0000") + ".png");
                    if (!File.Exists(file)) return false;
                    byte[] bytes = File.ReadAllBytes(file);
                    if (!IsPng(bytes)) return false;
                    loaded.Add(new PPTSlideThumbnail { SlideNumber = i, PngBytes = bytes });
                }

                thumbnails = loaded;
                try { Directory.SetLastWriteTimeUtc(directory, DateTime.UtcNow); } catch { }
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"读取 PPT 磁盘缩略图缓存失败: {ex.Message}", LogHelper.LogType.Trace);
                thumbnails = null;
                return false;
            }
        }

        public static void Save(string presentationPath, string presentationName, int width, int height,
            int slideCount, IList<PPTSlideThumbnail> thumbnails)
        {
            if (thumbnails == null || thumbnails.Count != slideCount ||
                !TryGetCacheDirectory(presentationPath, presentationName, width, height, slideCount, out var directory))
                return;

            string sizeDirectory = Path.GetDirectoryName(directory);
            string tempDirectory = Path.Combine(sizeDirectory, ".tmp-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(tempDirectory);
                var writtenSlides = new HashSet<int>();
                foreach (var thumbnail in thumbnails)
                {
                    if (thumbnail == null || thumbnail.SlideNumber < 1 || thumbnail.SlideNumber > slideCount ||
                        thumbnail.PngBytes == null || !IsPng(thumbnail.PngBytes) ||
                        !writtenSlides.Add(thumbnail.SlideNumber))
                        return;

                    File.WriteAllBytes(Path.Combine(tempDirectory, thumbnail.SlideNumber.ToString("0000") + ".png"),
                        thumbnail.PngBytes);
                }

                if (writtenSlides.Count != slideCount) return;

                Directory.CreateDirectory(sizeDirectory);
                ReplaceCacheDirectory(tempDirectory, directory);

                // 仅在 Thumbnails 子树内清理，绝不删除墨迹文件或 Position。
                string presentationsRoot = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(sizeDirectory)));
                CleanupCacheRoot(presentationsRoot);
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

        private static void ReplaceCacheDirectory(string tempDirectory, string directory)
        {
            if (!Directory.Exists(directory))
            {
                Directory.Move(tempDirectory, directory);
                return;
            }

            // A cache miss can mean that an existing revision directory is incomplete or corrupt.
            // Move it aside, publish the complete replacement, and restore it if publication fails.
            string backupDirectory = directory + ".old-" + Guid.NewGuid().ToString("N");
            Directory.Move(directory, backupDirectory);
            try
            {
                Directory.Move(tempDirectory, directory);
                try { Directory.Delete(backupDirectory, true); } catch { }
            }
            catch
            {
                try
                {
                    if (!Directory.Exists(directory) && Directory.Exists(backupDirectory))
                        Directory.Move(backupDirectory, directory);
                }
                catch { }
                throw;
            }
        }

        private static bool TryGetCacheDirectory(string presentationPath, string presentationName, int width,
            int height, int slideCount, out string directory)
        {
            directory = null;
            if (string.IsNullOrWhiteSpace(presentationPath)) return false;

            try
            {
                var file = new FileInfo(Path.GetFullPath(presentationPath));
                if (!file.Exists || width <= 0 || height <= 0 || slideCount <= 0) return false;

                string autoSaveLocation = MainWindow.Settings?.Automation?.AutoSavedStrokesLocation;
                if (string.IsNullOrWhiteSpace(autoSaveLocation)) return false;

                string presentationDirectory = PPTPresentationStorage.GetPresentationFolderPath(
                    autoSaveLocation, presentationName, slideCount, presentationPath);
                string revision = file.Length.ToString() + "-" + file.LastWriteTimeUtc.Ticks.ToString();
                directory = Path.Combine(presentationDirectory, "Thumbnails",
                    width.ToString() + "x" + height.ToString(), revision);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsPng(byte[] bytes)
        {
            return bytes != null && bytes.Length >= 8 &&
                   bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
                   bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A;
        }

        private static void CleanupCacheRoot(string presentationsRoot)
        {
            if (string.IsNullOrWhiteSpace(presentationsRoot) || !Directory.Exists(presentationsRoot)) return;

            try
            {
                var entries = new List<Tuple<string, DateTime, long>>();
                DateTime now = DateTime.UtcNow;
                long totalBytes = 0;

                foreach (string presentationDirectory in Directory.GetDirectories(presentationsRoot))
                {
                    string thumbnailsRoot = Path.Combine(presentationDirectory, "Thumbnails");
                    if (!Directory.Exists(thumbnailsRoot)) continue;

                    foreach (string sizeDirectory in Directory.GetDirectories(thumbnailsRoot))
                    {
                        foreach (string cacheDirectory in Directory.GetDirectories(sizeDirectory))
                        {
                            try
                            {
                                DateTime lastWrite = Directory.GetLastWriteTimeUtc(cacheDirectory);
                                if (Path.GetFileName(cacheDirectory).StartsWith(".tmp-", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (now - lastWrite > TempDirectoryMaxAge)
                                        Directory.Delete(cacheDirectory, true);
                                    continue;
                                }

                                long size = Directory.GetFiles(cacheDirectory, "*", SearchOption.AllDirectories)
                                    .Sum(file => new FileInfo(file).Length);
                                if (now - lastWrite > MaxCacheAge)
                                {
                                    Directory.Delete(cacheDirectory, true);
                                    continue;
                                }

                                totalBytes += size;
                                entries.Add(Tuple.Create(cacheDirectory, lastWrite, size));
                            }
                            catch { }
                        }
                    }
                }

                foreach (var entry in entries.OrderBy(item => item.Item2))
                {
                    if (totalBytes <= MaxCacheBytes) break;
                    try
                    {
                        if (Directory.Exists(entry.Item1))
                        {
                            Directory.Delete(entry.Item1, true);
                            totalBytes -= entry.Item3;
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"清理 PPT 缩略图缓存失败: {ex.Message}", LogHelper.LogType.Trace);
            }
        }
    }
}
