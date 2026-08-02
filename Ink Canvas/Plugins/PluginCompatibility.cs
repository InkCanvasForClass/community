using System;
using Ink_Canvas.Helpers;
using Ink_Canvas.Properties;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 插件版本兼容性检查。三项检查各自独立，任一不通过即拒绝加载：
    /// <list type="number">
    /// <item><c>MinHostVersion</c>：宿主编译版本不得低于该值</item>
    /// <item><c>ApiVersion</c>：与宿主 API 主版本相同且不高于宿主</item>
    /// <item><c>VersionRange</c>：宿主编译版本须落在该范围内（可表达上界）</item>
    /// </list>
    /// 版本号无法解析时一律放行以免阻塞启动，但会记录警告，见 <c>LogUnparsable</c>。
    /// <para>范围（<c>VersionRange</c>）支持以下格式：</para>
    /// <list type="bullet">
    /// <item>精确版本：<c>1.2.3</c></item>
    /// <item><c>^1.2.3</c>：同一主版本且大于等于给定版本</item>
    /// <item><c>~1.2.3</c>：同一主.次版本且大于等于给定版本</item>
    /// <item><c>>=1.0.0</c>、<c>>1.0.0</c>、<c><=2.0.0</c>、<c><2.0.0</c></item>
    /// <item><c>>=1.0.0,<2.0.0</c>（逗号或空格分隔）</item>
    /// </list>
    /// </summary>
    public static class PluginCompatibility
    {
        /// <summary>
        /// 返回符合结果，包含 <c>IsCompatible</c> 与可读 <c>Reason</c>。
        /// </summary>
        public static CompatibilityResult Check(PluginManifest manifest)
        {
            if (manifest == null) return CompatibilityResult.Ok();

            // 1. 最低宿主版本：与宿主实际编译版本比较
            if (!string.IsNullOrWhiteSpace(manifest.MinHostVersion))
            {
                if (!IsVersionAtLeast(HostApiRequirement.HostVersion, manifest.MinHostVersion))
                {
                    return CompatibilityResult.Fail(
                        string.Format(PluginStrings.Compat_HostVersionTooLow,
                            manifest.MinHostVersion, HostApiRequirement.HostVersion));
                }
            }

            // 2. API 版本（主版本相同即兼容）
            if (!string.IsNullOrWhiteSpace(manifest.ApiVersion))
            {
                if (!IsApiVersionCompatible(manifest.ApiVersion))
                {
                    return CompatibilityResult.Fail(
                        string.Format(PluginStrings.Compat_ApiVersionMismatch,
                            manifest.ApiVersion, HostApiRequirement.CurrentApiVersion));
                }
            }

            // 3. 宿主版本范围（可选）：插件声明自己能工作的宿主区间，可同时表达上界
            if (!string.IsNullOrWhiteSpace(manifest.VersionRange))
            {
                if (!IsVersionInRange(HostApiRequirement.HostVersion, manifest.VersionRange))
                {
                    return CompatibilityResult.Fail(
                        string.Format(PluginStrings.Compat_HostVersionOutOfRange,
                            HostApiRequirement.HostVersion, manifest.VersionRange));
                }
            }

            return CompatibilityResult.Ok();
        }

        /// <summary>
        /// 判断指定版本字符串 <paramref name="required"/> 是否满足主版本相同 + 次版本不超过当前。
        /// </summary>
        public static bool IsApiVersionCompatible(string required)
        {
            if (Version.TryParse(NormalizeVersion(required), out var req)
                && Version.TryParse(NormalizeVersion(HostApiRequirement.CurrentApiVersion), out var cur))
            {
                return req.Major == cur.Major && req <= cur;
            }

            // 无法解析时放行，避免阻塞启动；但必须留痕，否则插件会在不兼容的宿主上静默加载后崩溃
            LogUnparsable("ApiVersion", required);
            return true;
        }

        /// <summary>
        /// 判断 <paramref name="hostVersion"/> 是否 ≥ <paramref name="requiredMinVersion"/>。
        /// </summary>
        public static bool IsVersionAtLeast(string hostVersion, string requiredMinVersion)
        {
            if (Version.TryParse(NormalizeVersion(hostVersion), out var cur)
                && Version.TryParse(NormalizeVersion(requiredMinVersion), out var req))
            {
                return cur >= req;
            }

            LogUnparsable("MinHostVersion", requiredMinVersion);
            return true;
        }

        /// <summary>
        /// 判断 <paramref name="version"/> 是否满足 <paramref name="range"/>（npm 风格）。
        /// </summary>
        public static bool IsVersionInRange(string version, string range)
        {
            if (string.IsNullOrWhiteSpace(range)) return true;
            if (Version.TryParse(NormalizeVersion(version), out var ver) != true)
            {
                LogUnparsable("VersionRange.version", version);
                return true;
            }

            foreach (var raw in range.Split(new[] { ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var part = raw.Trim();
                if (part.Length == 0) continue;

                if (part.StartsWith(">="))
                {
                    if (!TryParse(part.Substring(2), out var target)) continue;
                    if (ver < target) return false;
                }
                else if (part.StartsWith(">"))
                {
                    if (!TryParse(part.Substring(1), out var target)) continue;
                    if (ver <= target) return false;
                }
                else if (part.StartsWith("<="))
                {
                    if (!TryParse(part.Substring(2), out var target)) continue;
                    if (ver > target) return false;
                }
                else if (part.StartsWith("<"))
                {
                    if (!TryParse(part.Substring(1), out var target)) continue;
                    if (ver >= target) return false;
                }
                else if (part.StartsWith("^"))
                {
                    if (!TryParse(part.Substring(1), out var target)) continue;
                    if (ver < target) return false;
                    if (ver.Major != target.Major) return false;
                }
                else if (part.StartsWith("~"))
                {
                    if (!TryParse(part.Substring(1), out var target)) continue;
                    if (ver < target) return false;
                    if (ver.Major != target.Major || ver.Minor != target.Minor) return false;
                }
                else
                {
                    // 精确匹配
                    if (!TryParse(part, out var target)) continue;
                    if (ver != target) return false;
                }
            }

            return true;
        }

        private static bool TryParse(string raw, out Version target)
        {
            if (Version.TryParse(NormalizeVersion(raw), out target)) return true;

            // 调用方会 continue 跳过该比较项，等于放宽了范围限制，同样需要留痕
            LogUnparsable("VersionRange.comparator", raw);
            return false;
        }

        /// <summary>
        /// 版本号无法被 <see cref="Version.TryParse"/> 解析时记一条警告。
        /// 解析失败一律按放行处理以免阻塞启动，因此这条日志是排查
        /// “插件本该被拒却仍加载”的唯一线索。带预发布后缀（如 <c>1.7.19-beta</c>）是常见成因。
        /// </summary>
        private static void LogUnparsable(string field, string value)
        {
            LogHelper.WriteLogToFile(
                $"[PluginCompatibility] 无法解析 {field} \"{value}\"，已跳过该项兼容性检查并放行",
                LogHelper.LogType.Warning);
        }

        /// <summary>
        /// 将 "v1.2"、"1.2" 这种短写法补齐为 "x.y.z" 以便 <see cref="Version.TryParse"/> 解析。
        /// </summary>
        public static string NormalizeVersion(string v)
        {
            if (string.IsNullOrWhiteSpace(v)) return "0.0.0";
            v = v.Trim().TrimStart('v', 'V');
            var parts = v.Split('.');
            if (parts.Length == 1) v += ".0.0";
            else if (parts.Length == 2) v += ".0";
            return v;
        }
    }

    /// <summary>
    /// 兼容性检查结果。
    /// </summary>
    public class CompatibilityResult
    {
        public bool IsCompatible { get; set; }
        public string Reason { get; set; } = "";

        public static CompatibilityResult Ok() => new CompatibilityResult { IsCompatible = true };
        public static CompatibilityResult Fail(string reason) => new CompatibilityResult
        {
            IsCompatible = false,
            Reason = reason ?? ""
        };
    }
}
