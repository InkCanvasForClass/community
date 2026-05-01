using System;
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
            return name.Trim();
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