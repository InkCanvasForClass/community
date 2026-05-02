using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 渲染保存文件名模板。支持占位符: {date} {time} {datetime} {mode} {page} {count} {type}。
    /// 当模板为空、渲染结果非法或仅含分隔符时，回退到默认时间戳命名。
    /// </summary>
    public static class SaveFileNameHelper
    {
        private const string DefaultDateTime = "yyyy-MM-dd HH-mm-ss-fff";

        // Windows 保留设备名（不区分大小写）。这些名称无论是否带扩展名，CreateFile 都会失败。
        private static readonly HashSet<string> ReservedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
        };

        public static string Render(string template, SaveFileNameContext ctx)
        {
            if (ctx == null) ctx = new SaveFileNameContext();
            var now = ctx.Time ?? DateTime.Now;

            if (string.IsNullOrWhiteSpace(template))
                return now.ToString(DefaultDateTime);

            try
            {
                string result = template
                    .Replace("{date}", now.ToString("yyyy-MM-dd"))
                    .Replace("{time}", now.ToString("HH-mm-ss"))
                    .Replace("{datetime}", now.ToString(DefaultDateTime))
                    .Replace("{mode}", ctx.Mode ?? "")
                    .Replace("{page}", ctx.Page?.ToString() ?? "")
                    .Replace("{count}", ctx.Count?.ToString() ?? "")
                    .Replace("{type}", ctx.Type ?? "");

                result = SanitizeFileName(result);

                if (string.IsNullOrWhiteSpace(result) || Regex.IsMatch(result, @"^[\s\-_]+$"))
                    return now.ToString(DefaultDateTime);

                return result;
            }
            catch
            {
                return now.ToString(DefaultDateTime);
            }
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }

            // Windows 禁止文件名以点号或空格结尾（会被静默截断甚至创建失败）。
            name = name.Trim().TrimEnd('.', ' ');

            if (string.IsNullOrEmpty(name)) return name;

            // 保留设备名：比较时忽略扩展名，命中则加下划线前缀以规避。
            var stem = Path.GetFileNameWithoutExtension(name);
            if (!string.IsNullOrEmpty(stem) && ReservedNames.Contains(stem))
            {
                name = "_" + name;
            }

            return name;
        }
    }

    public class SaveFileNameContext
    {
        public DateTime? Time { get; set; }
        /// <summary>"Annotation" or "BlackBoard" or "Screenshot" etc.</summary>
        public string Mode { get; set; }
        /// <summary>"User" or "Auto"</summary>
        public string Type { get; set; }
        public int? Page { get; set; }
        public int? Count { get; set; }
    }
}