using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Ink_Canvas.UInk
{
    /// <summary>
    /// `.uink.extra` 资源包（ZIP）读写。规范约束（uink_media / uink_conf）：
    ///  - Media.path 写入前必须 Unicode NFC、`/` 分隔，不得以 `/` 开头、含 `\`、NUL、控制字符、空路径段、`.` 或 `..`；
    ///  - ZIP 条目路径用相同 NFC 规范化形式，且不得重复；
    ///  - 读取不受信任 ZIP 前必须做资源预算检查（条目数、单条目大小、总解压大小、压缩比），禁止直接解压到工作目录。
    /// 路径穿越校验思路与 SafeZipExtractor 一致（拒绝绝对路径/`..`，Combine 后二次确认在目标目录内）。
    /// </summary>
    public static class UInkExtraArchive
    {
        // 资源预算上限（防 zip 炸弹）
        public const int MaxEntries = 10_000;
        public const long MaxSingleEntryBytes = 512L * 1024 * 1024;   // 512 MiB
        public const long MaxTotalBytes = 1L * 1024 * 1024 * 1024;    // 1 GiB
        public const double MaxCompressionRatio = 1000.0;

        /// <summary>规范化 ZIP 条目/Media 相对路径。返回 null 表示非法。</summary>
        public static string NormalizeEntryPath(string rawPath)
        {
            if (string.IsNullOrEmpty(rawPath)) return null;
            string nfc;
            try { nfc = rawPath.Normalize(NormalizationForm.FormC); }
            catch (ArgumentException) { return null; }

            if (nfc.IndexOf('\\') >= 0) return null;                 // 禁止 \（规范要求 / 分隔）
            if (nfc.IndexOf('\0') >= 0) return null;                 // NUL
            if (Path.IsPathRooted(nfc)) return null;                 // 绝对路径/盘符
            foreach (char c in nfc)
                if (char.IsControl(c)) return null;                  // 控制字符

            var segments = nfc.Split('/');
            foreach (var seg in segments)
            {
                if (seg.Length == 0) return null;                    // 空路径段（含前导/尾随 /）
                if (seg == "." || seg == "..") return null;          // . / ..
            }
            return nfc;
        }

        /// <summary>创建 `.uink.extra` ZIP。重复条目、非法条目跳过；入口路径已 NFC 规范化。</summary>
        public static void WriteArchive(string zipPath, IReadOnlyList<(string entryPath, string sourceFile)> resources)
        {
            if (resources == null) return;
            if (File.Exists(zipPath)) File.Delete(zipPath);
            using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (entryPath, sourceFile) in resources)
            {
                if (string.IsNullOrEmpty(sourceFile) || !File.Exists(sourceFile)) continue;
                var safe = NormalizeEntryPath(entryPath);
                if (safe == null || !seen.Add(safe)) continue;
                archive.CreateEntryFromFile(sourceFile, safe, CompressionLevel.Optimal);
            }
        }

        /// <summary>
        /// 预算检查 + 路径安全解压 `.uink.extra`，返回「入口路径(NFC) → 本地文件」映射。
        /// 任何预算/路径违规立即返回 null（不部分解压）。目录条目跳过。
        /// </summary>
        public static Dictionary<string, string> ExtractWithBudget(string zipPath, string extractDir)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            long totalBytes = 0;
            int entryCount = 0;
            var rootFull = Path.GetFullPath(extractDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

            using var archive = ZipFile.OpenRead(zipPath);
            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name) || entry.FullName.EndsWith("/")) continue; // 目录条目
                entryCount++;
                if (entryCount > MaxEntries) return null;
                if (entry.Length > MaxSingleEntryBytes) return null;
                totalBytes += entry.Length;
                if (totalBytes > MaxTotalBytes) return null;
                if (entry.CompressedLength > 0 && entry.Length / (double)entry.CompressedLength > MaxCompressionRatio) return null;

                var safe = NormalizeEntryPath(entry.FullName);
                if (safe == null) return null;

                var full = Path.GetFullPath(Path.Combine(extractDir, safe));
                if (!full.StartsWith(rootFull, StringComparison.Ordinal)) return null;

                Directory.CreateDirectory(Path.GetDirectoryName(full));
                entry.ExtractToFile(full, overwrite: true);
                result[safe] = full;
            }
            return result;
        }

        /// <summary>按文件扩展名推断 MIME（UInk Media.mimeType）。未知回退 application/octet-stream。</summary>
        public static string MimeForPath(string path)
        {
            switch (Path.GetExtension(path)?.ToLowerInvariant())
            {
                case ".png": return "image/png";
                case ".jpg": case ".jpeg": return "image/jpeg";
                case ".gif": return "image/gif";
                case ".bmp": return "image/bmp";
                case ".svg": return "image/svg+xml";
                case ".webp": return "image/webp";
                case ".tif": case ".tiff": return "image/tiff";
                case ".mp3": return "audio/mpeg";
                case ".wav": return "audio/wav";
                case ".m4a": return "audio/mp4";
                case ".aac": return "audio/aac";
                case ".flac": return "audio/flac";
                case ".mp4": return "video/mp4";
                case ".mov": return "video/quicktime";
                case ".webm": return "video/webm";
                case ".avi": return "video/x-msvideo";
                case ".mkv": return "video/x-matroska";
                case ".pdf": return "application/pdf";
                default: return "application/octet-stream";
            }
        }
    }
}
