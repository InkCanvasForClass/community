using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 插件来源安全检查。
    /// <para>插件安装来源分为 <see cref="PluginTrustLevel"/> 三档：</para>
    /// <list type="bullet">
    /// <item><see cref="PluginTrustLevel.Trusted"/>：官方插件市场索引中的条目，且 SHA256 校验通过</item>
    /// <item><see cref="PluginTrustLevel.Known"/>：市场索引中存在但 SHA256 未提供/未校验</item>
    /// <item><see cref="PluginTrustLevel.Unknown"/>：本地 .icpx、第三方镜像、或 SHA256 校验失败的安装包</item>
    /// </list>
    /// 对 <see cref="PluginTrustLevel.Unknown"/> 的安装，建议弹出安全提示并由用户明确确认。
    /// </summary>
    public class PluginSecurityCheck
    {
        private readonly PluginMarketService _market;

        public PluginSecurityCheck(PluginMarketService market)
        {
            _market = market;
        }

        /// <summary>
        /// 评估一个即将被安装的 .icpx 包的安全等级。如果包尚未提取可传 <paramref name="expectedSha256"/>=<c>null</c>。
        /// </summary>
        public SecurityVerdict EvaluatePackage(
            string packageFilePath,
            string expectedSha256,
            string declaredPluginId)
        {
            var verdict = new SecurityVerdict
            {
                PackagePath = packageFilePath,
                PluginId = declaredPluginId ?? "",
                DetectedAt = DateTime.UtcNow
            };

            if (string.IsNullOrEmpty(packageFilePath) || !File.Exists(packageFilePath))
            {
                verdict.TrustLevel = PluginTrustLevel.Unknown;
                verdict.Reasons.Add("安装包文件不存在。");
                return verdict;
            }

            try
            {
                var info = InspectPackage(packageFilePath);
                verdict.PackageSha256 = info.PackageSha256;
                if (info.Permissions != null)
                {
                    foreach (var p in info.Permissions) verdict.Permissions.Add(p);
                }

                if (string.IsNullOrEmpty(declaredPluginId)) declaredPluginId = info.Manifest?.Id;
                verdict.PluginId = declaredPluginId ?? "";

                // 找不到说明不是 .icpx 标准包
                if (info.Manifest == null)
                {
                    verdict.TrustLevel = PluginTrustLevel.Unknown;
                    verdict.Reasons.Add("无法解析 manifest.json，可能不是 ICC-CE 标准插件包。");
                    return verdict;
                }

                // 是否能在市场找到
                var marketEntry = string.IsNullOrEmpty(declaredPluginId)
                    ? null
                    : _market?.ResolveMarketPlugin(declaredPluginId);

                if (marketEntry != null)
                {
                    verdict.IsOnMarket = true;
                    var hashMatches = !string.IsNullOrEmpty(info.PackageSha256)
                                      && !string.IsNullOrEmpty(marketEntry.DownloadSha256)
                                      && string.Equals(info.PackageSha256, marketEntry.DownloadSha256, StringComparison.OrdinalIgnoreCase);

                    if (hashMatches)
                    {
                        verdict.TrustLevel = PluginTrustLevel.Trusted;
                        return verdict;
                    }

                    if (!string.IsNullOrEmpty(marketEntry.DownloadSha256))
                    {
                        verdict.TrustLevel = PluginTrustLevel.Unknown;
                        verdict.Reasons.Add("文件 SHA256 与市场记录不匹配，包可能被篡改。");
                        return verdict;
                    }

                    verdict.TrustLevel = PluginTrustLevel.Known;
                    verdict.Reasons.Add("市场未提供 SHA256 校验值，无法对比文件完整性。");
                    return verdict;
                }

                // 本地或未知来源
                verdict.TrustLevel = PluginTrustLevel.Unknown;
                if (!string.IsNullOrEmpty(declaredPluginId))
                {
                    verdict.Reasons.Add($"插件 '{declaredPluginId}' 不在官方插件市场索引中，来源未知。");
                }
                else
                {
                    verdict.Reasons.Add("无法从 manifest 中解析插件 id，无法与市场索引对比。");
                }
                if (info.Permissions != null && info.Permissions.Count > 0)
                {
                    verdict.Reasons.Add($"插件声明了较高权限：{string.Join(", ", info.Permissions)}");
                }
                return verdict;
            }
            catch (Exception ex)
            {
                verdict.TrustLevel = PluginTrustLevel.Unknown;
                verdict.Reasons.Add($"解析安装包失败：{ex.Message}");
                return verdict;
            }
        }

        /// <summary>
        /// 默认策略下，安全安装应阻断哪些级别。该层级以上的强制弹出确认。
        /// </summary>
        public bool RequiresUserConfirmation(SecurityVerdict verdict)
        {
            return verdict.TrustLevel == PluginTrustLevel.Unknown;
        }

        /// <summary>
        /// 给 UI 渲染使用的安全摘要文本。
        /// </summary>
        public string FormatVerdict(SecurityVerdict verdict)
        {
            if (verdict == null) return "";
            var lines = new List<string>
            {
                $"信任级别：{verdict.TrustLevel}",
                $"插件 id：{verdict.PluginId}",
                $"SHA256：{verdict.PackageSha256}"
            };
            if (verdict.Permissions != null && verdict.Permissions.Count > 0)
            {
                lines.Add($"权限：{string.Join(", ", verdict.Permissions)}");
            }
            if (verdict.Reasons != null && verdict.Reasons.Count > 0)
            {
                lines.Add("注意事项：");
                lines.AddRange(verdict.Reasons.Select(r => "• " + r));
            }
            return string.Join(Environment.NewLine, lines);
        }

        private static PackageInspection InspectPackage(string packagePath)
        {
            using var stream = File.OpenRead(packagePath);
            using var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Read);

            string manifestText = null;
            foreach (var entry in archive.Entries)
            {
                if (string.Equals(entry.FullName, PluginManager.ManifestFileName, StringComparison.OrdinalIgnoreCase))
                {
                    using var reader = new StreamReader(entry.Open());
                    manifestText = reader.ReadToEnd();
                    break;
                }
            }

            PluginManifest manifest = null;
            if (!string.IsNullOrEmpty(manifestText))
            {
                try { manifest = System.Text.Json.JsonSerializer.Deserialize<PluginManifest>(manifestText); }
                catch { manifest = null; }
            }

            // 文件 SHA256
            stream.Position = 0;
            string sha;
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var hash = sha256.ComputeHash(stream);
                sha = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }

            return new PackageInspection
            {
                Manifest = manifest,
                Permissions = manifest?.Permissions?.ToList() ?? new List<string>(),
                PackageSha256 = sha
            };
        }

        private class PackageInspection
        {
            public PluginManifest Manifest { get; set; }
            public List<string> Permissions { get; set; }
            public string PackageSha256 { get; set; }
        }
    }
}
