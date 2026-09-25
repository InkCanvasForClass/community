using System;
using System.IO;
using System.IO.Compression;

namespace Ink_Canvas.Helpers
{
    public static class SafeZipExtractor
    {
        /// <param name="zipFilePath">ZIP 文件路径</param>
        /// <param name="extractPath">解压目标目录</param>
        /// <param name="overwrite">是否覆盖已存在文件</param>
        public static void ExtractZipSafely(string zipFilePath, string extractPath, bool overwrite = true)
        {
            if (string.IsNullOrWhiteSpace(zipFilePath))
                throw new ArgumentNullException(nameof(zipFilePath));
            if (string.IsNullOrWhiteSpace(extractPath))
                throw new ArgumentNullException(nameof(extractPath));

            var fullExtractPath = Path.GetFullPath(extractPath);
            Directory.CreateDirectory(fullExtractPath);

            int written = 0;
            int skipped = 0;
            using (var zip = ZipFile.OpenRead(zipFilePath))
            {
                foreach (var entry in zip.Entries)
                {
                    // 跳过空条目
                    if (string.IsNullOrEmpty(entry.FullName))
                        continue;

                    // 防止绝对路径和盘符前缀
                    if (Path.IsPathRooted(entry.FullName))
                    {
                        skipped++;
                        continue;
                    }

                    // 统一路径分隔符
                    var normalized = entry.FullName.Replace('/', Path.DirectorySeparatorChar);

                    // 拒绝包含 .. 的路径，防止目录穿越
                    if (normalized.Contains(".." + Path.DirectorySeparatorChar) ||
                        normalized.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                    {
                        skipped++;
                        continue;
                    }

                    var destinationPath = Path.GetFullPath(
                        Path.Combine(fullExtractPath, normalized));

                    // 再次确认仍然在目标目录下
                    if (!destinationPath.StartsWith(fullExtractPath, StringComparison.OrdinalIgnoreCase))
                    {
                        skipped++;
                        continue;
                    }

                    // 目录条目
                    if (entry.FullName.EndsWith("/", StringComparison.Ordinal) ||
                        entry.FullName.EndsWith("\\", StringComparison.Ordinal))
                    {
                        Directory.CreateDirectory(destinationPath);
                        continue;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? fullExtractPath);

                    if (!overwrite && File.Exists(destinationPath))
                        continue;

                    using (var input = entry.Open())
                    using (var output = File.Create(destinationPath))
                    {
                        input.CopyTo(output);
                    }
                    written++;
                }
            }

            if (skipped > 0)
            {
                LogHelper.WriteLogToFile(
                    $"[Zip] 解压 {Path.GetFileName(zipFilePath)} 时跳过 {skipped} 个不安全条目（绝对路径/目录穿越/越界）",
                    LogHelper.LogType.Warning);
            }
            LogHelper.WriteLogToFile(
                $"[Zip] 解压完成: {Path.GetFileName(zipFilePath)} -> {fullExtractPath}, 写入 {written} 个条目",
                LogHelper.LogType.Info);
        }
    }
}


