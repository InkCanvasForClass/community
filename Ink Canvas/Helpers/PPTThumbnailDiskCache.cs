using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 跨进程、跨软件重启复用 PPT 幻灯片缩略图。
    /// 使用文档路径作为稳定身份，使用文件长度和最后写入时间检测文档版本；
    /// 不读取整个 PPT 文件计算内容哈希，避免大文件带来的额外 I/O。
    /// </summary>
    internal static class PPTThumbnailDiskCache
    {
        private static readonly string CacheRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "InkCanvasForClass", "Cache", "PPTThumbnails");

        public static bool TryLoad(string presentationPath, int width, int height, int slideCount,
            out List<PPTSlideThumbnail> thumbnails)
        {
            thumbnails = null;
            if (!TryGetCacheDirectory(presentationPath, width, height, slideCount, out var directory))
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

        public static void Save(string presentationPath, int width, int height, int slideCount,
            IList<PPTSlideThumbnail> thumbnails)
        {
            if (thumbnails == null || thumbnails.Count != slideCount ||
                !TryGetCacheDirectory(presentationPath, width, height, slideCount, out var directory))
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

        private static bool TryGetCacheDirectory(string presentationPath, int width, int height,
            int slideCount, out string directory)
        {
            directory = null;
            if (string.IsNullOrWhiteSpace(presentationPath)) return false;

            try
            {
                var fullPath = Path.GetFullPath(presentationPath);
                var file = new FileInfo(fullPath);
                if (!file.Exists || width <= 0 || height <= 0 || slideCount <= 0) return false;

                // 仅对路径做哈希以生成安全、长度固定的目录名；不对 PPT 内容做哈希。
                string identity;
                using (var sha = SHA256.Create())
                {
                    var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(fullPath.ToUpperInvariant()));
                    identity = BitConverter.ToString(bytes).Replace("-", string.Empty);
                }

                string version = file.Length.ToString() + "-" + file.LastWriteTimeUtc.Ticks.ToString() +
                    "-" + slideCount.ToString() + "-" + width.ToString() + "x" + height.ToString();
                directory = Path.Combine(CacheRoot, identity, version);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
