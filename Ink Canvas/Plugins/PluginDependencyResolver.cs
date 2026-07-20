using System.Collections.Generic;
using System.Linq;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 插件依赖冲突检测器。在加载前对一组 <see cref="PluginInfo"/> 做以下检查：
    /// <list type="number">
    /// <item>重复 id（同一目录扫描两次或市场提供重复条目）</item>
    /// <item>循环依赖（<see cref="LoadStatus"/> 已被 <see cref="PluginManager.ResolveLoadOrder"/> 检测，本类只做静态补充检查）</item>
    /// <item>版本冲突：插件 A 要求 dep 1.0.0，插件 B 要求 dep 1.5.0 但不可同时满足</item>
    /// <item>缺失的必需依赖</item>
    /// <item>缺失的可选依赖（仅告警，不阻塞加载）</item>
    /// </list>
    /// </summary>
    public class PluginDependencyResolver
    {
        /// <summary>
        /// 在一组候选插件之间检测冲突并返回信息。
        /// </summary>
        public DependencyAnalysis Analyze(IEnumerable<PluginInfo> candidates)
        {
            var report = new DependencyAnalysis();
            if (candidates == null) return report;

            var pluginList = candidates.Where(p => p != null).ToList();

            // 1. 检测重复 id
            var dupGroups = pluginList
                .Where(p => !string.IsNullOrEmpty(p.Id))
                .GroupBy(p => p.Id, System.StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .ToList();

            foreach (var group in dupGroups)
            {
                report.Issues.Add(new DependencyIssue
                {
                    Severity = IssueSeverity.Error,
                    Code = DependencyIssueCode.DuplicatePluginId,
                    PluginId = group.Key,
                    Message = $"检测到重复的插件 id '{group.Key}'：{string.Join(", ", group.Select(p => p.Name))}"
                });
            }

            // 2. 检测缺失必需依赖 + 版本冲突
            var byId = pluginList
                .Where(p => !string.IsNullOrEmpty(p.Id))
                .GroupBy(p => p.Id, System.StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), System.StringComparer.OrdinalIgnoreCase);

            foreach (var plugin in pluginList)
            {
                var deps = plugin.Manifest?.Dependencies;
                if (deps == null || deps.Count == 0) continue;

                foreach (var dep in deps)
                {
                    if (string.IsNullOrEmpty(dep.Id)) continue;

                    // 缺失依赖
                    if (!byId.TryGetValue(dep.Id, out var provider))
                    {
                        if (dep.IsRequired)
                        {
                            report.Issues.Add(new DependencyIssue
                            {
                                Severity = IssueSeverity.Error,
                                Code = DependencyIssueCode.MissingRequiredDependency,
                                PluginId = plugin.Id,
                                Message = $"插件 '{plugin.Name}' 缺少必需依赖 '{dep.Id}'{(string.IsNullOrEmpty(dep.Version) ? "" : $" (>= {dep.Version})")}"
                            });
                        }
                        else
                        {
                            report.Issues.Add(new DependencyIssue
                            {
                                Severity = IssueSeverity.Warning,
                                Code = DependencyIssueCode.MissingOptionalDependency,
                                PluginId = plugin.Id,
                                Message = $"插件 '{plugin.Name}' 推荐依赖 '{dep.Id}' 未安装"
                            });
                        }
                        continue;
                    }

                    // 版本不满足
                    if (!string.IsNullOrEmpty(dep.Version) &&
                        !PluginCompatibility.IsVersionInRange(provider.Version, ">=" + dep.Version))
                    {
                        report.Issues.Add(new DependencyIssue
                        {
                            Severity = dep.IsRequired ? IssueSeverity.Error : IssueSeverity.Warning,
                            Code = DependencyIssueCode.DependencyVersionTooLow,
                            PluginId = plugin.Id,
                            Message = $"插件 '{plugin.Name}' 依赖 '{dep.Id} >= {dep.Version}'，实际为 {provider.Version}"
                        });
                    }
                }
            }

            // 3. 检测不同插件要求互不兼容的依赖版本
            // 例如：A 要求 dep 1.x，B 要求 dep 2.x
            var depRequirementGroups = new Dictionary<string, List<(string pluginId, string dependencyId, string required, bool isRequired)>>();

            foreach (var plugin in pluginList)
            {
                var deps = plugin.Manifest?.Dependencies;
                if (deps == null) continue;

                foreach (var dep in deps)
                {
                    if (string.IsNullOrEmpty(dep.Id) || string.IsNullOrEmpty(dep.Version)) continue;
                    if (!byId.TryGetValue(dep.Id, out _)) continue; // 仅当依赖实际存在时才有意义

                    if (!depRequirementGroups.TryGetValue(dep.Id, out var list))
                    {
                        list = new List<(string, string, string, bool)>();
                        depRequirementGroups[dep.Id] = list;
                    }
                    list.Add((plugin.Id, dep.Id, dep.Version, dep.IsRequired));
                }
            }

            foreach (var (depId, requirements) in depRequirementGroups)
            {
                // 取至少一个硬性要求，且至少两个不同插件要求不同主版本
                var requiredVersions = requirements.Where(r => r.isRequired).Select(r => r.required).Distinct().ToList();
                if (requiredVersions.Count < 2) continue;

                var majors = requiredVersions
                    .Select(v => PluginCompatibility.NormalizeVersion(v))
                    .Select(v => int.TryParse(v.Split('.')[0], out var i) ? i : 0)
                    .Distinct()
                    .ToList();

                if (majors.Count > 1)
                {
                    foreach (var r in requirements.Where(r => r.isRequired))
                    {
                        report.Issues.Add(new DependencyIssue
                        {
                            Severity = IssueSeverity.Error,
                            Code = DependencyIssueCode.ConflictingDependency,
                            PluginId = r.pluginId,
                            Message = $"插件 '{r.pluginId}' 与其它插件对依赖 '{r.dependencyId}' 要求的版本不兼容（至少需要 {r.required}）"
                        });
                    }
                }
            }

            report.HasErrors = report.Issues.Any(i => i.Severity == IssueSeverity.Error);
            report.HasWarnings = report.Issues.Any(i => i.Severity == IssueSeverity.Warning);
            return report;
        }
    }

    /// <summary>
    /// 一组 <see cref="DependencyIssue"/> 的归纳报告。
    /// </summary>
    public class DependencyAnalysis
    {
        public List<DependencyIssue> Issues { get; } = new List<DependencyIssue>();
        public bool HasErrors { get; set; }
        public bool HasWarnings { get; set; }

        public IEnumerable<DependencyIssue> Errors => Issues.Where(i => i.Severity == IssueSeverity.Error);
        public IEnumerable<DependencyIssue> Warnings => Issues.Where(i => i.Severity == IssueSeverity.Warning);
    }

    public enum IssueSeverity
    {
        Warning = 0,
        Error = 1
    }

    public enum DependencyIssueCode
    {
        MissingRequiredDependency,
        MissingOptionalDependency,
        DependencyVersionTooLow,
        ConflictingDependency,
        DuplicatePluginId
    }

    public class DependencyIssue
    {
        public IssueSeverity Severity { get; set; }
        public DependencyIssueCode Code { get; set; }
        public string PluginId { get; set; } = "";
        public string Message { get; set; } = "";
    }
}
