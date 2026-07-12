using System;
using System.Text.RegularExpressions;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 插件版本兼容性检查。支持以下格式：
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

            // 1. 最低宿主版本
            if (!string.IsNullOrWhiteSpace(manifest.MinHostVersion))
            {
                if (!IsVersionAtLeast(HostApiRequirement.MinSupportedHostVersion, manifest.MinHostVersion))
                {
                    return CompatibilityResult.Fail(
                        $"插件要求宿主版本 ≥ {manifest.MinHostVersion}，当前宿主为 {HostApiRequirement.MinSupportedHostVersion}");
                }
            }

            // 2. API 版本（主版本相同即兼容）
            if (!string.IsNullOrWhiteSpace(manifest.ApiVersion))
            {
                if (!IsApiVersionCompatible(manifest.ApiVersion))
                {
                    return CompatibilityResult.Fail(
                        $"插件要求 API 版本 {manifest.ApiVersion}，当前宿主 API 为 {HostApiRequirement.CurrentApiVersion}");
                }
            }

            // 3. 版本范围（可选，仅当插件同时使用旧依赖检查时启用）
            if (!string.IsNullOrWhiteSpace(manifest.VersionRange))
            {
                if (!IsVersionInRange(manifest.Version, manifest.VersionRange))
                {
                    return CompatibilityResult.Fail(
                        $"插件版本 {manifest.Version} 不在宿主允许的版本范围 {manifest.VersionRange} 内");
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
            return true; // 无法解析时放行，避免阻塞启动
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
            return true;
        }

        /// <summary>
        /// 判断 <paramref name="version"/> 是否满足 <paramref name="range"/>（npm 风格）。
        /// </summary>
        public static bool IsVersionInRange(string version, string range)
        {
            if (string.IsNullOrWhiteSpace(range)) return true;
            if (Version.TryParse(NormalizeVersion(version), out var ver) != true) return true;

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
            return Version.TryParse(NormalizeVersion(raw), out target);
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
