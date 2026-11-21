using Ink_Canvas.Helpers;
using Ink_Canvas.Plugins;
using Ink_Canvas.Properties;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using iNKORE.UI.WPF.Modern.Common.IconKeys;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas.Controls.Toolbar.FloatingToolbar
{
    public static class ToolbarRegistry
    {
        /// <summary>
        /// 是否给工具栏组件显隐加过渡动画。
        /// 尊重「禁止工具栏动画」设置：关掉时一律瞬间切换，保证低端设备与
        /// 用户明确要求"不要动画"时没有额外开销。
        /// </summary>
        private static bool _animateVisibilityChange = true;

        /// <summary>设置组件显隐是否播放过渡动画（对应「禁止工具栏动画」的取反）。</summary>
        public static void SetVisibilityAnimationEnabled(bool enabled)
        {
            _animateVisibilityChange = enabled;
        }

        private static List<IToolbarItem> _items;
        private static readonly List<PluginToolbarItemInfo> _pluginItems = new List<PluginToolbarItemInfo>();
        internal const string InjectedTag = "ToolbarRegistryInjected";
        internal const string ContentBorderTag = "ToolbarContentBorder";
        internal const string SelectionCanvasTag = "ToolbarSelectionCanvas";
        internal const string SelectionBGTag = "ToolbarSelectionBG";
        internal const string IndicatorBarTag = "ToolbarIndicatorBar";
        internal const string ContentPanelTag = "ToolbarContentPanel";

        private static readonly string ConfigSubDir = Path.Combine("Configs", "ToolbarConfigs");

        public static readonly DependencyProperty HidingRulesetProperty =
            DependencyProperty.RegisterAttached("HidingRuleset", typeof(ToolbarRuleset), typeof(ToolbarRegistry),
                new PropertyMetadata(null));

        public static void SetHidingRuleset(FrameworkElement element, ToolbarRuleset value)
            => element.SetValue(HidingRulesetProperty, value);

        public static ToolbarRuleset GetHidingRuleset(FrameworkElement element)
            => (ToolbarRuleset)element.GetValue(HidingRulesetProperty);

        public static readonly DependencyProperty PreventHideOnDragClickProperty =
            DependencyProperty.RegisterAttached("PreventHideOnDragClick", typeof(bool), typeof(ToolbarRegistry),
                new PropertyMetadata(false));

        public static void SetPreventHideOnDragClick(FrameworkElement element, bool value)
            => element.SetValue(PreventHideOnDragClickProperty, value);

        public static bool GetPreventHideOnDragClick(FrameworkElement element)
            => (bool)element.GetValue(PreventHideOnDragClickProperty);

        public static readonly DependencyProperty IsContentCollapsedByUserProperty =
            DependencyProperty.RegisterAttached("IsContentCollapsedByUser", typeof(bool), typeof(ToolbarRegistry),
                new PropertyMetadata(false));

        public static void SetIsContentCollapsedByUser(FrameworkElement element, bool value)
            => element.SetValue(IsContentCollapsedByUserProperty, value);

        public static bool GetIsContentCollapsedByUser(FrameworkElement element)
            => (bool)element.GetValue(IsContentCollapsedByUserProperty);

        public static readonly DependencyProperty UseRedStyleProperty =
            DependencyProperty.RegisterAttached("UseRedStyle", typeof(bool), typeof(ToolbarRegistry),
                new PropertyMetadata(false));

        public static void SetUseRedStyle(FrameworkElement element, bool value)
            => element.SetValue(UseRedStyleProperty, value);

        public static bool GetUseRedStyle(FrameworkElement element)
            => (bool)element.GetValue(UseRedStyleProperty);

        public static List<KeyValuePair<string, string>> AvailableConditions => new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("isAnnotating", Strings.GetString("ToolbarCondition_Annotating") ?? "Annotation mode"),
            new KeyValuePair<string, string>("isPPTMode", Strings.GetString("ToolbarCondition_PPTMode") ?? "PPT mode"),
            new KeyValuePair<string, string>("isContentCollapsedByUser", Strings.GetString("ToolbarCondition_Collapsed") ?? "Toolbar collapsed")
        };

        private static bool _isContentCollapsedByUser = false;

        public static bool IsContentCollapsedByUser
        {
            get => _isContentCollapsedByUser;
            set => _isContentCollapsedByUser = value;
        }

        #region Ruleset evaluation

        public static bool EvaluateRuleset(ToolbarRuleset ruleset, Dictionary<string, bool> context)
        {
            if (ruleset == null)
                return false;

            if (ruleset.Groups == null || ruleset.Groups.Count == 0)
            {
                ruleset.State = BoolToState(false);
                return false;
            }

            bool result = ruleset.Mode == ToolbarLogicalMode.And;

            foreach (var group in ruleset.Groups)
            {
                if (!group.IsEnabled)
                {
                    group.State = 0;
                    continue;
                }

                bool? groupResult = EvaluateGroup(group, context);
                group.State = BoolToState(groupResult);

                if (groupResult == null)
                    continue;

                bool gVal = groupResult.Value;
                if (!gVal && ruleset.Mode == ToolbarLogicalMode.And)
                {
                    result = false;
                    break;
                }
                if (gVal && ruleset.Mode == ToolbarLogicalMode.Or)
                {
                    result = true;
                    break;
                }
            }

            result ^= ruleset.IsReversed;
            ruleset.State = BoolToState(result);
            return result;
        }

        private static bool? EvaluateGroup(ToolbarRuleGroup group, Dictionary<string, bool> context)
        {
            if (group.Rules == null || group.Rules.Count == 0)
            {
                bool emptyResult = group.Mode == ToolbarLogicalMode.And;
                emptyResult ^= group.IsReversed;
                return emptyResult;
            }

            bool result = group.Mode == ToolbarLogicalMode.And;

            foreach (var rule in group.Rules)
            {
                if (string.IsNullOrEmpty(rule.ConditionId))
                {
                    rule.State = 0;
                    continue;
                }

                bool conditionMet = context.TryGetValue(rule.ConditionId, out var val) && val;
                bool ruleResult = conditionMet ^ rule.IsReversed;
                rule.State = BoolToState(ruleResult);

                if (!ruleResult && group.Mode == ToolbarLogicalMode.And)
                {
                    result = false;
                    break;
                }
                if (ruleResult && group.Mode == ToolbarLogicalMode.Or)
                {
                    result = true;
                    break;
                }
            }

            result ^= group.IsReversed;
            return result;
        }

        private static int BoolToState(bool? v) => v switch
        {
            true => 2,
            false => 1,
            null => 0
        };

        internal static ToolbarRuleset MigrateHidingRule(ToolbarHidingRule rule)
        {
            return rule switch
            {
                ToolbarHidingRule.AlwaysShow => ToolbarRuleset.AlwaysShow(),
                ToolbarHidingRule.AnnotationOnly => ToolbarRuleset.AnnotationOnly(),
                ToolbarHidingRule.PPTOnly => ToolbarRuleset.PPTOnly(),
                ToolbarHidingRule.PPTAnnotationOnly => ToolbarRuleset.PPTAnnotationOnly(),
                ToolbarHidingRule.AnnotationOrPPTGesture => ToolbarRuleset.AnnotationOnly(),
                _ => ToolbarRuleset.AlwaysShow()
            };
        }

        internal static ToolbarRuleset GetEffectiveRuleset(ToolbarComponentEntry entry)
        {
            if (entry.HidingRuleset != null)
                return entry.HidingRuleset;
            return MigrateHidingRule(entry.HidingRule);
        }

        #endregion

        public static IReadOnlyList<IToolbarItem> Discover()
        {
            if (_items != null) return _items;

            var itemType = typeof(IToolbarItem);
            _items = Assembly.GetExecutingAssembly()
                .GetTypes()
                // 只选择可实例化且有公共无参构造函数的类型，避免 Activator.CreateInstance 抛出 MissingMethodException
                .Where(t => !t.IsAbstract && !t.IsInterface && itemType.IsAssignableFrom(t)
                            && t.GetConstructor(Type.EmptyTypes) != null)
                .Select(t =>
                {
                    try { return (IToolbarItem)Activator.CreateInstance(t); }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"ToolbarRegistry: 实例化 {t.FullName} 失败: {ex.Message}", LogHelper.LogType.Warning);
                        return null;
                    }
                })
                .Where(i => i != null)
                .ToList();

            // 添加插件注册的工具栏项
            foreach (var pluginItem in _pluginItems)
            {
                _items.Add(new PluginToolbarItemWrapper(pluginItem));
            }

            return _items;
        }

        public static void RegisterPluginItem(PluginToolbarItemInfo itemInfo, bool autoAddToActiveConfig = true)
        {
            if (itemInfo == null || string.IsNullOrEmpty(itemInfo.Id)) return;
            if (_pluginItems.Any(item => string.Equals(item.Id, itemInfo.Id, StringComparison.OrdinalIgnoreCase))) return;

            _pluginItems.Add(itemInfo);
            LogHelper.WriteLogToFile($"ToolbarRegistry: 插件注册工具栏项 [{itemInfo.Id}] (autoAddToActiveConfig={autoAddToActiveConfig})", LogHelper.LogType.Info);

            if (autoAddToActiveConfig)
            {
                EnsurePluginItemInActiveConfig(itemInfo.Id);
            }

            if (_items != null)
            {
                _items.Add(new PluginToolbarItemWrapper(itemInfo));
            }
        }

        private static void EnsurePluginItemInActiveConfig(string itemId)
        {
            EnsureDefaultConfigExists();

            var configName = SettingsManager.Settings?.ToolbarConfigName;
            if (string.IsNullOrWhiteSpace(configName))
                configName = "default";

            var layout = LoadActiveConfig() ?? CreateDefaultLayout();
            layout.Components ??= new List<ToolbarComponentEntry>();
            if (ContainsComponent(layout.Components, itemId)) return;

            layout.Components.Add(new ToolbarComponentEntry
            {
                Id = itemId,
                HidingRuleset = ToolbarRuleset.AlwaysShow().WithHideOnCollapsed()
            });
            SaveConfigFile(configName, layout);
            LogHelper.WriteLogToFile(
                $"ToolbarRegistry: 已将插件组件 [{itemId}] 加入当前配置 [{configName}]",
                LogHelper.LogType.Info);
        }

        private static bool ContainsComponent(IEnumerable<ToolbarComponentEntry> entries, string itemId)
        {
            foreach (var entry in entries ?? Enumerable.Empty<ToolbarComponentEntry>())
            {
                if (string.Equals(entry.Id, itemId, StringComparison.OrdinalIgnoreCase))
                    return true;
                if (entry.Children != null && ContainsComponent(entry.Children, itemId))
                    return true;
            }
            return false;
        }

        public static IReadOnlyList<PluginToolbarItemInfo> GetPluginItems() => _pluginItems.AsReadOnly();

        /// <summary>
        /// 注销插件注册的工具栏组件，断开对插件程序集中委托（ViewFactory 等）的引用。
        /// 热重载必需：这些委托只要还留在静态表里，插件 ALC 就永远卸载不掉。
        /// 只清注册表与已构建的 <see cref="_items"/> 缓存，不动用户的布局配置文件——
        /// 重载后同 Id 组件会重新注册，用户摆好的位置得以保留。
        /// </summary>
        public static bool UnregisterPluginItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;

            var removed = _pluginItems.RemoveAll(
                item => string.Equals(item.Id, itemId, StringComparison.OrdinalIgnoreCase)) > 0;

            // _items 里存的是包着 PluginToolbarItemInfo 的 wrapper，同样持有插件委托，必须一并移除。
            _items?.RemoveAll(item => item is PluginToolbarItemWrapper
                                      && string.Equals(item.Id, itemId, StringComparison.OrdinalIgnoreCase));

            if (removed)
                LogHelper.WriteLogToFile($"ToolbarRegistry: 已注销插件工具栏项 [{itemId}]", LogHelper.LogType.Info);

            return removed;
        }

        /// <summary>
        /// 从所有浮动工具栏配置文件里移除指定 Id 的组件条目（递归处理组合子项）。
        /// 插件卸载时调用：用户把插件组件拖进了工具栏，卸载后该组件已不存在，
        /// 不清理会导致 Populate 反复刷 "未找到条目" 警告，且工具栏持续保留空白位。
        /// </summary>
        /// <returns>被修改的配置文件数量。</returns>
        public static int RemovePluginEntryFromAllConfigs(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return 0;

            var modified = 0;
            foreach (var configName in ListConfigFiles())
            {
                try
                {
                    var layout = LoadConfigFile(configName);
                    if (layout?.Components == null) continue;

                    if (StripPluginEntry(layout.Components, itemId) > 0)
                    {
                        SaveConfigFile(configName, layout);
                        modified++;
                        LogHelper.WriteLogToFile(
                            $"ToolbarRegistry: 已从配置 [{configName}] 移除插件组件条目 [{itemId}]",
                            LogHelper.LogType.Info);
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile(
                        $"ToolbarRegistry: 清理配置 [{configName}] 中的插件组件失败: {ex.Message}",
                        LogHelper.LogType.Warning);
                }
            }

            return modified;
        }

        /// <summary>
        /// 递归剔除 <see cref="ToolbarComponentEntry"/> 集合中 Id 等于 <paramref name="itemId"/> 的条目。
        /// </summary>
        private static int StripPluginEntry(List<ToolbarComponentEntry> entries, string itemId)
        {
            if (entries == null || entries.Count == 0) return 0;

            var removed = 0;
            for (var i = entries.Count - 1; i >= 0; i--)
            {
                var entry = entries[i];
                if (entry == null) continue;

                if (string.Equals(entry.Id, itemId, StringComparison.OrdinalIgnoreCase))
                {
                    entries.RemoveAt(i);
                    removed++;
                    continue;
                }

                if (entry.Children != null && entry.Children.Count > 0)
                    removed += StripPluginEntry(entry.Children, itemId);
            }

            return removed;
        }


        #region Config file system

        public static string GetConfigDirectory()
        {
            return Path.Combine(App.RootPath, ConfigSubDir);
        }

        public static string GetConfigFilePath(string name)
        {
            return Path.Combine(GetConfigDirectory(), name + ".json");
        }

        private static string GetBackupFilePath(string name)
        {
            return Path.Combine(GetConfigDirectory(), name + ".json.bak");
        }

        public static List<string> ListConfigFiles()
        {
            var dir = GetConfigDirectory();
            if (!Directory.Exists(dir)) return new List<string>();
            return Directory.GetFiles(dir, "*.json")
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static ToolbarLayoutSettings LoadConfigFile(string name)
        {
            var path = GetConfigFilePath(name);
            if (!File.Exists(path))
            {
                var bakPath = GetBackupFilePath(name);
                if (File.Exists(bakPath))
                {
                    LogHelper.WriteLogToFile($"ToolbarRegistry: 主配置文件不存在，尝试加载备份 [{bakPath}]", LogHelper.LogType.Warning);
                    var bakResult = TryDeserializeConfig(bakPath, name);
                    if (bakResult != null)
                    {
                        MigrateConditionIdCasing(bakResult, name);
                        SaveConfigFile(name, bakResult);
                        LogHelper.WriteLogToFile($"ToolbarRegistry: 从备份恢复配置 [{name}] 成功", LogHelper.LogType.Info);
                    }
                    return bakResult;
                }
                LogHelper.WriteLogToFile($"ToolbarRegistry: 配置文件不存在 [{path}]", LogHelper.LogType.Warning);
                return null;
            }
            var result = TryDeserializeConfig(path, name);
            if (result != null)
            {
                MigrateConditionIdCasing(result, name);
                return result;
            }

            var backupPath = GetBackupFilePath(name);
            if (File.Exists(backupPath))
            {
                LogHelper.WriteLogToFile($"ToolbarRegistry: 主配置文件损坏，尝试加载备份 [{backupPath}]", LogHelper.LogType.Warning);
                var bakResult = TryDeserializeConfig(backupPath, name);
                if (bakResult != null)
                {
                    MigrateConditionIdCasing(bakResult, name);
                    SaveConfigFile(name, bakResult);
                    LogHelper.WriteLogToFile($"ToolbarRegistry: 从备份恢复配置 [{name}] 成功", LogHelper.LogType.Info);
                }
                return bakResult;
            }

            LogHelper.WriteLogToFile($"ToolbarRegistry: 配置 [{name}] 和备份均不可用", LogHelper.LogType.Error);
            return null;
        }

        private static ToolbarLayoutSettings TryDeserializeConfig(string path, string name)
        {
            try
            {
                var json = File.ReadAllText(path);
                var layout = JsonConvert.DeserializeObject<ToolbarLayoutSettings>(json);
                if (layout?.Components == null || layout.Components.Count == 0)
                {
                    LogHelper.WriteLogToFile($"ToolbarRegistry: 配置 [{name}] 内容为空或无效", LogHelper.LogType.Warning);
                    return null;
                }
                return layout;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"ToolbarRegistry: 加载配置 [{name}] 失败: {ex.Message}", LogHelper.LogType.Error);
                return null;
            }
        }

        public static void SaveConfigFile(string name, ToolbarLayoutSettings layout)
        {
            try
            {
                var dir = GetConfigDirectory();
                if (!Directory.Exists(dir))
                    ProcessProtectionManager.WithWriteAccess(dir, () => Directory.CreateDirectory(dir));

                var path = GetConfigFilePath(name);
                var bakPath = GetBackupFilePath(name);

                if (File.Exists(path))
                {
                    try
                    {
                        ProcessProtectionManager.WithWriteAccess(bakPath, () => File.Copy(path, bakPath, true));
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"ToolbarRegistry: 备份配置 [{name}] 失败: {ex.Message}", LogHelper.LogType.Warning);
                    }
                }

                var json = JsonConvert.SerializeObject(layout, Formatting.Indented);
                ProcessProtectionManager.WithWriteAccess(path, () => File.WriteAllText(path, json));
                LogHelper.WriteLogToFile($"ToolbarRegistry: 保存配置 [{name}] 成功", LogHelper.LogType.Info);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"ToolbarRegistry: 保存配置 [{name}] 失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        public static void DeleteConfigFile(string name)
        {
            try
            {
                var path = GetConfigFilePath(name);
                if (File.Exists(path))
                    ProcessProtectionManager.WithWriteAccess(path, () => File.Delete(path));
                var bakPath = GetBackupFilePath(name);
                if (File.Exists(bakPath))
                    ProcessProtectionManager.WithWriteAccess(bakPath, () => File.Delete(bakPath));
                LogHelper.WriteLogToFile($"ToolbarRegistry: 删除配置 [{name}]", LogHelper.LogType.Info);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"ToolbarRegistry: 删除配置 [{name}] 失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private static volatile bool _defaultConfigEnsured;

        public static void EnsureDefaultConfigExists()
        {
            if (_defaultConfigEnsured) return;

            var dir = GetConfigDirectory();
            if (!Directory.Exists(dir))
                ProcessProtectionManager.WithWriteAccess(dir, () => Directory.CreateDirectory(dir));

            var defaultPath = GetConfigFilePath("default");
            if (!File.Exists(defaultPath))
            {
                var layout = CreateDefaultLayout();
                SaveConfigFile("default", layout);
                LogHelper.WriteLogToFile("ToolbarRegistry: 首次启动，创建 default.json", LogHelper.LogType.Info);
            }

            _defaultConfigEnsured = true;
        }

        public static ToolbarLayoutSettings LoadActiveConfig()
        {
            var configName = SettingsManager.Settings?.ToolbarConfigName;
            if (string.IsNullOrWhiteSpace(configName))
                configName = "default";

            var layout = LoadConfigFile(configName);
            if (layout != null && layout.Components != null && layout.Components.Count > 0)
                return layout;

            var files = ListConfigFiles();
            if (files.Count > 0 && files[0] != configName)
            {
                layout = LoadConfigFile(files[0]);
                if (layout != null && layout.Components != null && layout.Components.Count > 0)
                    return layout;
            }

            return CreateDefaultLayout();
        }

        /// <summary>
        /// 修正旧版配置文件中 ConditionId 大小写不一致的问题。
        /// 例如旧版使用 "isPptMode"，新版代码使用 "isPPTMode"，
        /// 导致条件匹配失败，退出按钮在 PPT 模式下不显示。
        /// 修正后会自动将更新后的配置写回文件。
        /// </summary>
        private static readonly Dictionary<string, string> ConditionIdRenames = new(StringComparer.OrdinalIgnoreCase)
        {
            ["isPptMode"] = "isPPTMode",
            ["isPptAnnotationOnly"] = "isPPTAnnotationOnly",
            ["isAnnotatingOrPptGesture"] = "isAnnotatingOrPPTGesture"
        };

        private static void MigrateConditionIdCasing(ToolbarLayoutSettings layout, string configName)
        {
            if (layout?.Components == null) return;

            bool changed = false;
            foreach (var component in layout.Components)
            {
                changed |= MigrateEntryConditionIds(component);
                changed |= MigrateEntryRedStyle(component);
            }

            if (changed)
            {
                SaveConfigFile(configName, layout);
                LogHelper.WriteLogToFile(
                    $"ToolbarRegistry: 已迁移配置 [{configName}] 中的旧版字段（ConditionId 大小写 / 红色强调）",
                    LogHelper.LogType.Info);
            }
        }

        /// <summary>
        /// 迁移旧的“红色强调”设置：
        /// 旧版单一开关 useRedStyle → 新版 redStyleIcon / redStyleText 两个独立开关；
        /// 旧版 builtin.clear（清除/删除）的红色是硬编码的、配置里没有对应设置，
        /// 迁移时为其补上默认强调，避免升级后失去红色外观。
        /// </summary>
        private static bool MigrateEntryRedStyle(ToolbarComponentEntry entry)
        {
            if (entry == null) return false;
            bool changed = false;

            bool hasLegacy = entry.Settings != null && entry.Settings.ContainsKey(ComponentSettingKeys.UseRedStyle);
            if (hasLegacy)
            {
                bool legacy = entry.GetSettingBool(ComponentSettingKeys.UseRedStyle);
                entry.Settings.Remove(ComponentSettingKeys.UseRedStyle);
                if (legacy)
                {
                    entry.Settings[ComponentSettingKeys.RedStyleIcon] = true;
                    entry.Settings[ComponentSettingKeys.RedStyleText] = true;
                }
                else
                {
                    entry.Settings.Remove(ComponentSettingKeys.RedStyleIcon);
                    entry.Settings.Remove(ComponentSettingKeys.RedStyleText);
                }
                changed = true;
            }
            else if (entry.Id == "builtin.clear"
                && (entry.Settings == null || !entry.Settings.ContainsKey(ComponentSettingKeys.RedStyleIcon))
                && (entry.Settings == null || !entry.Settings.ContainsKey(ComponentSettingKeys.RedStyleText)))
            {
                entry.SetSetting(ComponentSettingKeys.RedStyleIcon, true);
                entry.SetSetting(ComponentSettingKeys.RedStyleText, true);
                changed = true;
            }

            if (entry.Children != null)
            {
                foreach (var child in entry.Children)
                {
                    changed |= MigrateEntryRedStyle(child);
                }
            }

            return changed;
        }

        private static bool MigrateEntryConditionIds(ToolbarComponentEntry entry)
        {
            bool changed = false;

            if (entry.HidingRuleset?.Groups != null)
            {
                foreach (var group in entry.HidingRuleset.Groups)
                {
                    if (group.Rules == null) continue;
                    foreach (var rule in group.Rules)
                    {
                        if (rule.ConditionId != null && ConditionIdRenames.TryGetValue(rule.ConditionId, out var newName)
                            && !string.Equals(rule.ConditionId, newName, StringComparison.Ordinal))
                        {
                            LogHelper.WriteLogToFile(
                                $"ToolbarRegistry: 修正 ConditionId [{rule.ConditionId}] -> [{newName}]",
                                LogHelper.LogType.Info);
                            rule.ConditionId = newName;
                            changed = true;
                        }
                    }
                }
            }

            if (entry.Children != null)
            {
                foreach (var child in entry.Children)
                {
                    changed |= MigrateEntryConditionIds(child);
                }
            }

            return changed;
        }

        #endregion

        public static void ClearInjected(Panel container)
        {
            if (container == null) return;
            var toRemove = container.Children.OfType<FrameworkElement>()
                .Where(e => e.Tag as string == InjectedTag || e.Tag as string == ContentBorderTag)
                .ToList();
            foreach (var element in toRemove)
                container.Children.Remove(element);
        }

        #region Display items and segments

        private class DisplayItem
        {
            public FrameworkElement View { get; set; }
            public ToolbarRuleset Ruleset { get; set; }
            public bool IsSeparateBorder { get; set; }
            public bool IsToolbarButton { get; set; }
        }

        private class Segment
        {
            public bool IsSeparateBorder { get; set; }
            public List<DisplayItem> Items { get; set; } = new();
        }

        private static List<DisplayItem> FlattenEntries(IToolbarHost host, List<ToolbarComponentEntry> entries, Dictionary<string, IToolbarItem> itemMap, Orientation orientation = Orientation.Horizontal, bool inheritedRedStyleIcon = false, bool inheritedRedStyleText = false)
        {
            var result = new List<DisplayItem>();
            foreach (var entry in entries)
            {
                if (entry.IsGroup)
                {
                    var groupRuleset = GetEffectiveRuleset(entry);
                    var groupRedStyleIcon = inheritedRedStyleIcon || entry.GetSettingBool(ComponentSettingKeys.RedStyleIcon);
                    var groupRedStyleText = inheritedRedStyleText || entry.GetSettingBool(ComponentSettingKeys.RedStyleText);
                    var groupContentItems = new List<DisplayItem>();

                    foreach (var childEntry in entry.Children)
                    {
                        if (childEntry.IsGroup)
                        {
                            if (groupContentItems.Count > 0)
                            {
                                FlushGroupContentItems(result, groupContentItems, groupRuleset, entry.ShowSeparateBorder, orientation);
                                groupContentItems.Clear();
                            }
                            var nestedItems = FlattenEntries(host, new List<ToolbarComponentEntry> { childEntry }, itemMap, orientation, groupRedStyleIcon, groupRedStyleText);
                            foreach (var nestedItem in nestedItems)
                            {
                                result.Add(nestedItem);
                            }
                            continue;
                        }

                        if (!itemMap.TryGetValue(childEntry.Id, out var item)) continue;
                        var view = BuildAndRegister(host, item, orientation);
                        if (view == null) continue;
                        view.Tag = InjectedTag;
                        ApplyComponentSettings(view, childEntry);
                        if ((groupRedStyleIcon || groupRedStyleText) && view is ToolbarImageButton groupButton)
                        {
                            ApplyRedStyle(groupButton, groupRedStyleIcon, groupRedStyleText);
                        }
                        var childRuleset = GetEffectiveRuleset(childEntry);
                        SetHidingRuleset(view, childRuleset);

                        if (childEntry.ShowSeparateBorder)
                        {
                            if (groupContentItems.Count > 0)
                            {
                                FlushGroupContentItems(result, groupContentItems, groupRuleset, entry.ShowSeparateBorder, orientation);
                                groupContentItems.Clear();
                            }
                            result.Add(new DisplayItem
                            {
                                View = view,
                                Ruleset = childRuleset,
                                IsSeparateBorder = true,
                                IsToolbarButton = view is ToolbarImageButton
                            });
                        }
                        else
                        {
                            groupContentItems.Add(new DisplayItem
                            {
                                View = view,
                                Ruleset = childRuleset,
                                IsSeparateBorder = false,
                                IsToolbarButton = view is ToolbarImageButton
                            });
                        }
                    }

                    if (groupContentItems.Count > 0)
                    {
                        FlushGroupContentItems(result, groupContentItems, groupRuleset, entry.ShowSeparateBorder, orientation);
                    }
                }
                else
                {
                    if (!itemMap.TryGetValue(entry.Id, out var item))
                    {
                        LogHelper.WriteLogToFile($"ToolbarRegistry: 未找到条目 [{entry.Id}]", LogHelper.LogType.Warning);
                        continue;
                    }
                    var view = BuildAndRegister(host, item, orientation);
                    if (view == null) continue;
                    view.Tag = InjectedTag;
                    ApplyComponentSettings(view, entry);
                    if ((inheritedRedStyleIcon || inheritedRedStyleText) && view is ToolbarImageButton inheritedButton)
                    {
                        ApplyRedStyle(inheritedButton, inheritedRedStyleIcon, inheritedRedStyleText);
                    }
                    var ruleset = GetEffectiveRuleset(entry);
                    SetHidingRuleset(view, ruleset);
                    if (entry.GetSettingBool(ComponentSettingKeys.RedStyleIcon) || entry.GetSettingBool(ComponentSettingKeys.RedStyleText))
                        SetUseRedStyle(view, true);
                    result.Add(new DisplayItem
                    {
                        View = view,
                        Ruleset = ruleset,
                        IsSeparateBorder = entry.ShowSeparateBorder,
                        IsToolbarButton = view is ToolbarImageButton
                    });
                }
            }
            return result;
        }

        private static void FlushGroupContentItems(List<DisplayItem> result, List<DisplayItem> groupContentItems, ToolbarRuleset groupRuleset, bool groupShowSeparateBorder, Orientation orientation = Orientation.Horizontal)
        {
            if (groupContentItems.Count == 0) return;

            if (groupShowSeparateBorder)
            {
                var innerPanel = new StackPanel { Orientation = orientation };
                foreach (var item in groupContentItems)
                {
                    item.View.Margin = new Thickness(0);
                    innerPanel.Children.Add(item.View);
                }
                innerPanel.Tag = InjectedTag;
                SetHidingRuleset(innerPanel, groupRuleset);
                result.Add(new DisplayItem
                {
                    View = innerPanel,
                    Ruleset = groupRuleset,
                    IsSeparateBorder = true,
                    IsToolbarButton = false
                });
            }
            else
            {
                result.Add(CreateGroupContentDisplayItem(groupContentItems, groupRuleset, orientation));
            }
        }

        private static DisplayItem CreateGroupContentDisplayItem(List<DisplayItem> groupContentItems, ToolbarRuleset groupRuleset, Orientation orientation = Orientation.Horizontal)
        {
            var innerPanel = new StackPanel { Orientation = orientation };
            foreach (var item in groupContentItems)
            {
                item.View.Margin = new Thickness(0);
                innerPanel.Children.Add(item.View);
            }
            innerPanel.Tag = InjectedTag;
            SetHidingRuleset(innerPanel, groupRuleset);
            return new DisplayItem
            {
                View = innerPanel,
                Ruleset = groupRuleset,
                IsSeparateBorder = false,
                IsToolbarButton = false
            };
        }

        private static List<Segment> GroupIntoSegments(List<DisplayItem> displayItems)
        {
            var segments = new List<Segment>();
            var currentContentItems = new List<DisplayItem>();

            foreach (var item in displayItems)
            {
                if (item.IsSeparateBorder)
                {
                    if (currentContentItems.Count > 0)
                    {
                        segments.Add(new Segment { IsSeparateBorder = false, Items = new List<DisplayItem>(currentContentItems) });
                        currentContentItems.Clear();
                    }
                    segments.Add(new Segment { IsSeparateBorder = true, Items = new List<DisplayItem> { item } });
                }
                else
                {
                    currentContentItems.Add(item);
                }
            }

            if (currentContentItems.Count > 0)
            {
                segments.Add(new Segment { IsSeparateBorder = false, Items = new List<DisplayItem>(currentContentItems) });
            }

            return segments;
        }

        #endregion

        public static void Populate(IToolbarHost host, Panel rootPanel, ToolbarLayoutSettings layout, Orientation orientation = Orientation.Horizontal)
        {
            if (host == null || rootPanel == null)
            {
                LogHelper.WriteLogToFile("ToolbarRegistry: Populate host/rootPanel 为空", LogHelper.LogType.Warning);
                return;
            }

            layout = layout ?? CreateDefaultLayout();
            if (layout.Components == null || layout.Components.Count == 0)
            {
                layout = CreateDefaultLayout();
            }

            var discovered = Discover();
            var itemMap = discovered.ToDictionary(i => i.Id, i => i);

            ClearInjected(rootPanel);

            var displayItems = FlattenEntries(host, layout.Components, itemMap, orientation);
            var segments = GroupIntoSegments(displayItems);

            bool hasExistingChildren = rootPanel.Children.Count > 0;
            bool isFirst = true;
            foreach (var segment in segments)
            {
                if (segment.IsSeparateBorder)
                {
                    var item = segment.Items[0];
                    var elementToAdd = WrapInSeparateBorder(item.View, item.Ruleset, item.IsToolbarButton, orientation);
                    elementToAdd.Margin = (isFirst && !hasExistingChildren) ? new Thickness(0) :
                        orientation == Orientation.Horizontal ? new Thickness(3, 0, 0, 0) : new Thickness(0, 3, 0, 0);
                    ApplyInitialVisibility(elementToAdd, item.Ruleset);
                    rootPanel.Children.Add(elementToAdd);
                }
                else
                {
                    var contentBorder = CreateContentBorder(segment.Items, orientation);
                    contentBorder.Margin = (isFirst && !hasExistingChildren) ? new Thickness(0) :
                        orientation == Orientation.Horizontal ? new Thickness(3, 0, 0, 0) : new Thickness(0, 3, 0, 0);
                    rootPanel.Children.Add(contentBorder);
                }
                isFirst = false;
            }
        }

        private static Border CreateContentBorder(List<DisplayItem> items, Orientation orientation = Orientation.Horizontal)
        {
            var contentPanel = new StackPanel
            {
                Orientation = orientation,
                Margin = orientation == Orientation.Horizontal ? new Thickness(2, 2, 2, 0) : new Thickness(2, 2, 0, 2),
                Cursor = Cursors.Arrow,
                Tag = ContentPanelTag
            };

            foreach (var item in items)
            {
                ApplyInitialVisibility(item.View, item.Ruleset);
                contentPanel.Children.Add(item.View);
                if (GetUseRedStyle(item.View))
                    SetUseRedStyle(contentPanel, true);
            }

            var border = new Border
            {
                Padding = orientation == Orientation.Horizontal ? new Thickness(2, 0, 2, 0) : new Thickness(0, 2, 0, 2),
                Visibility = Visibility.Visible,
                Height = orientation == Orientation.Horizontal ? 58 : double.NaN,
                Width = orientation == Orientation.Vertical ? 58 : double.NaN,
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(2),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = contentPanel,
                Tag = ContentBorderTag
            };
            border.SetResourceReference(Border.BackgroundProperty, "FloatingBarBackgroundBrush");
            border.SetResourceReference(Border.BorderBrushProperty, "FloatingBarBorderBrush");

            return border;
        }

        private static Border WrapInSeparateBorder(FrameworkElement view, ToolbarRuleset ruleset, bool isToolbarButton, Orientation orientation = Orientation.Horizontal)
        {
            Border wrapper;

            if (isToolbarButton)
            {
                var contentPanel = new StackPanel
                {
                    Orientation = orientation,
                    Margin = orientation == Orientation.Horizontal ? new Thickness(2, 2, 2, 0) : new Thickness(2, 2, 0, 2),
                    Cursor = Cursors.Arrow,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Tag = ContentPanelTag
                };
                ApplyInitialVisibility(view, ruleset);
                contentPanel.Children.Add(view);

                wrapper = new Border
                {
                    Margin = new Thickness(0),
                    Padding = new Thickness(0),
                    MinWidth = orientation == Orientation.Horizontal ? 58 : 0,
                    MinHeight = orientation == Orientation.Vertical ? 58 : 0,
                    Height = orientation == Orientation.Horizontal ? 58 : double.NaN,
                    Width = orientation == Orientation.Vertical ? 58 : double.NaN,
                    CornerRadius = new CornerRadius(8),
                    BorderThickness = new Thickness(2),
                    Child = contentPanel,
                    Tag = ContentBorderTag
                };
                wrapper.SetResourceReference(Border.BackgroundProperty, "FloatingBarBackgroundBrush");
                wrapper.SetResourceReference(Border.BorderBrushProperty, "FloatingBarBorderBrush");

                view.HorizontalAlignment = HorizontalAlignment.Center;
                view.VerticalAlignment = VerticalAlignment.Center;
            }
            else
            {
                var contentPanel = new StackPanel
                {
                    Orientation = orientation,
                    Margin = orientation == Orientation.Horizontal ? new Thickness(2, 2, 2, 0) : new Thickness(2, 2, 0, 2),
                    Cursor = Cursors.Arrow,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Tag = ContentPanelTag
                };
                ApplyInitialVisibility(view, ruleset);
                contentPanel.Children.Add(view);

                wrapper = new Border
                {
                    Margin = new Thickness(0),
                    Padding = orientation == Orientation.Horizontal ? new Thickness(2, 0, 2, 0) : new Thickness(0, 2, 0, 2),
                    MinWidth = 0,
                    Height = orientation == Orientation.Horizontal ? 58 : double.NaN,
                    Width = orientation == Orientation.Vertical ? 58 : double.NaN,
                    CornerRadius = new CornerRadius(8),
                    BorderThickness = new Thickness(2),
                    Child = contentPanel,
                    Tag = ContentBorderTag
                };
                wrapper.SetResourceReference(Border.BackgroundProperty, "FloatingBarBackgroundBrush");
                wrapper.SetResourceReference(Border.BorderBrushProperty, "FloatingBarBorderBrush");

                view.HorizontalAlignment = HorizontalAlignment.Center;
                view.VerticalAlignment = VerticalAlignment.Center;
            }

            SetHidingRuleset(wrapper, ruleset);
            return wrapper;
        }

        private static void ApplyInitialVisibility(FrameworkElement element, ToolbarRuleset ruleset)
        {
            element.Visibility = Visibility.Visible;
        }

        public static void UpdateVisibilityByMode(Panel rootPanel, bool isAnnotating, bool isPPTMode)
        {
            var context = new Dictionary<string, bool>
            {
                ["isAnnotating"] = isAnnotating,
                ["isPPTMode"] = isPPTMode,
                ["isContentCollapsedByUser"] = _isContentCollapsedByUser
            };

            if (rootPanel == null) return;

            // 记录切换前的宽度与各子项位置：用于宽度补间与"邻居跟随移动"。
            double beforeWidth = rootPanel.ActualWidth;
            var beforeOffsets = _animateVisibilityChange
                ? CaptureChildOffsets(rootPanel)
                : null;

            UpdatePanelVisibility(rootPanel, context);

            AnimatePanelWidthToFit(rootPanel, beforeWidth);

            if (beforeOffsets != null)
            {
                AnimateChildrenToNewOffsets(rootPanel, beforeOffsets);
            }
        }

        /// <summary>
        /// 记录面板直接子项当前相对面板左上角的位置。
        /// </summary>
        /// <remarks>
        /// 用于"某个组件显隐后，它后面的组件平滑滑到新位置"。
        /// 只记录直接子项：工具栏是横向排列，显隐影响的是后面兄弟的横向位置。
        /// </remarks>
        private static Dictionary<FrameworkElement, double> CaptureChildOffsets(Panel panel)
        {
            var result = new Dictionary<FrameworkElement, double>();
            if (panel == null) return result;

            // 组件是三层嵌套：ContentBorder(Border) → ContentPanel(StackPanel) → 按钮(InjectedTag)。
            // 只取直接子项会漏掉真正会移动的按钮，必须递归收集。
            CollectVisibleLayoutElements(panel, panel, result);
            return result;
        }

        /// <summary>
        /// 递归收集参与横向排布的元素，记录它们相对根面板的横坐标。
        /// </summary>
        private static void CollectVisibleLayoutElements(
            Panel root, FrameworkElement current, Dictionary<FrameworkElement, double> sink)
        {
            if (current == null || current.Visibility != Visibility.Visible) return;

            // 记录"真正会移动的组件"：带注入标记的那些（按钮/分隔线/分组面板）。
            // 不用类型判断：ToolbarImageButton 是 UserControl（属 ContentControl），
            // 按类型排除会把所有按钮漏掉。
            // 也不做视觉树深搜：那会在每次显隐时产生数百次查询，得不偿失。
            if (current.Tag as string == InjectedTag && current != root)
            {
                try
                {
                    var point = current.TransformToAncestor(root).Transform(new Point(0, 0));
                    if (point.X > 0.01 || point.Y > 0.01)
                    {
                        sink[current] = point.X;
                    }
                }
                catch
                {
                    // 尚未进入视觉树
                }

                // 带标记的分组面板内部还有按钮，继续往下收
            }

            // 继续向下递归
            if (current is Panel panel)
            {
                foreach (var child in panel.Children.OfType<FrameworkElement>())
                {
                    CollectVisibleLayoutElements(root, child, sink);
                }
            }
            else if (current is Border border && border.Child is FrameworkElement borderChild)
            {
                CollectVisibleLayoutElements(root, borderChild, sink);
            }
            else if (current is ContentControl contentControl && contentControl.Content is FrameworkElement content)
            {
                CollectVisibleLayoutElements(root, content, sink);
            }
        }

        /// <summary>
        /// 让子项从"切换前的位置"平滑滑到"切换后的新位置"。
        /// </summary>
        /// <remarks>
        /// 布局一旦重算，子项会瞬间跳到新位置。要让它看起来是滑过去的，
        /// 就在跳变之后立刻给每个子项叠加一个反向位移，再把这个位移补间回 0。
        ///
        /// 位移写 RenderTransform（合成层），不触发布局，比动画 Margin 便宜得多。
        /// 必须等布局跑完才能量到新位置，所以排到 Loaded 优先级执行。
        /// </remarks>
        private static void AnimateChildrenToNewOffsets(
            Panel panel, Dictionary<FrameworkElement, double> beforeOffsets)
        {
            if (panel == null || beforeOffsets == null || beforeOffsets.Count == 0) return;

            panel.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    foreach (var kvp in beforeOffsets)
                    {
                        var child = kvp.Key;
                        if (child == null || child.Visibility != Visibility.Visible) continue;

                        double newOffset;
                        try
                        {
                            newOffset = child.TransformToAncestor(panel).Transform(new Point(0, 0)).X;
                        }
                        catch
                        {
                            continue;
                        }

                        double delta = kvp.Value - newOffset;
                        if (Math.Abs(delta) < 1.0) continue;

                        // 用独立的"邻居让位"通道，不与出场滑入抢同一个 TranslateTransform
                        var translate = GetOrCreateChannel(child, SiblingChannelIndex);

                        var animation = new System.Windows.Media.Animation.DoubleAnimation
                        {
                            From = delta,
                            To = 0,
                            Duration = TimeSpan.FromMilliseconds(ToolbarSiblingSlideMs),
                            EasingFunction = new System.Windows.Media.Animation.CubicEase
                            {
                                EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
                            }
                        };

                        animation.Completed += (_, _) =>
                        {
                            translate.BeginAnimation(TranslateTransform.XProperty, null);
                            translate.X = 0;
                        };

                        translate.BeginAnimation(TranslateTransform.XProperty, animation);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ToolbarRegistry] 邻居滑动动画失败: {ex.Message}");
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>邻居组件滑到新位置的时长（毫秒），与宽度过渡同节奏。</summary>
        private const int ToolbarSiblingSlideMs = 180;



        /// <summary>出场滑入使用的位移通道。</summary>
        private const int EnterChannelIndex = 0;

        /// <summary>邻居让位使用的位移通道。</summary>
        private const int SiblingChannelIndex = 1;

        /// <summary>
        /// 取得组件指定通道的位移变换。
        /// 两个通道分开，避免出场滑入与邻居让位同时发生时互相覆盖。
        /// </summary>
        private static TranslateTransform GetOrCreateChannel(FrameworkElement element, int channelIndex)
        {
            if (element.RenderTransform is TransformGroup group
                && group.Children.Count > channelIndex
                && group.Children[channelIndex] is TranslateTransform existing)
            {
                return existing;
            }

            // 重建两条通道（沿用已有值，避免已在播的动画被抹掉位置）
            double x0 = 0, x1 = 0;
            if (element.RenderTransform is TransformGroup old && old.Children.Count > 1)
            {
                if (old.Children[0] is TranslateTransform t0) x0 = t0.X;
                if (old.Children[1] is TranslateTransform t1) x1 = t1.X;
            }

            var enter = new TranslateTransform(x0, 0);
            var sibling = new TranslateTransform(x1, 0);
            var rebuilt = new TransformGroup();
            rebuilt.Children.Add(enter);
            rebuilt.Children.Add(sibling);
            element.RenderTransform = rebuilt;

            return channelIndex == EnterChannelIndex ? enter : sibling;
        }

        /// <summary>
        /// 组件显隐后，让工具栏宽度的变化有个过渡，而不是"啪"一下变宽/变窄。
        /// </summary>
        /// <remarks>
        /// 为什么不直接对 Width 逐帧动画：
        /// 宽度是布局属性，逐帧变化会每帧触发一次 measure/arrange，
        /// 工具栏里每个按钮都要重新排列，正是掉帧的典型来源。
        ///
        /// 这里改用的做法是「一次布局 + 一次补间」：
        /// 1. 先把面板宽度固定在旧值，避免子项显隐立刻把布局宽度改掉；
        /// 2. 强制一次布局拿到新内容应有的宽度；
        /// 3. 用一条短补间把 Width 从旧值推到新值；
        /// 4. 结束后清掉 Width，让面板回到自动布局。
        ///
        /// 期间仍有布局，但只发生在补间的每一帧（不可避免——宽度本来就是布局量），
        /// 且只涉及这一个面板，不再叠加组件显隐的重复测量。
        /// 关掉「禁止工具栏动画」时直接跳过，保持原来的瞬时切换。
        /// </remarks>
        private static void AnimatePanelWidthToFit(Panel rootPanel, double beforeWidth)
        {
            if (rootPanel == null) return;
            if (!_animateVisibilityChange) return;

            // 竖向工具栏动画的是高度而非宽度，这里不处理
            if (rootPanel is StackPanel sp && sp.Orientation == Orientation.Vertical) return;

            try
            {
                // 先摘掉上一轮可能残留的宽度动画与固定值
                rootPanel.BeginAnimation(FrameworkElement.WidthProperty, null);
                rootPanel.ClearValue(FrameworkElement.WidthProperty);

                // 关键：先让布局跑一遍，ActualWidth/DesiredSize 才是**新的**内容宽度。
                // 只调 Measure 而不 UpdateLayout 时，元素若已参与过布局会复用缓存结果，
                // 拿到的仍是切换前的旧宽度——之前"新值没拿对"就是这里。
                rootPanel.UpdateLayout();
                double targetWidth = rootPanel.ActualWidth;
                if (targetWidth <= 0)
                {
                    rootPanel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    targetWidth = rootPanel.DesiredSize.Width;
                }

                // 新旧宽度差不多就不必补间
                if (targetWidth <= 0 || beforeWidth <= 0 ||
                    Math.Abs(targetWidth - beforeWidth) < 1.0)
                {
                    return;
                }

                // 把宽度退回旧值（此时布局已算出新值，写死宽度会立刻生效），
                // 再用补间推到新值，视觉上就是"慢慢变宽/变窄"。
                rootPanel.Width = beforeWidth;

                var animation = new System.Windows.Media.Animation.DoubleAnimation
                {
                    From = beforeWidth,
                    To = targetWidth,
                    Duration = TimeSpan.FromMilliseconds(ToolbarWidthTransitionMs),
                    EasingFunction = new System.Windows.Media.Animation.CubicEase
                    {
                        EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
                    }
                };

                // 结束后解除固定宽度，交还自动布局（否则之后内容变化会被旧值卡住）
                animation.Completed += (_, _) =>
                {
                    rootPanel.BeginAnimation(FrameworkElement.WidthProperty, null);
                    rootPanel.ClearValue(FrameworkElement.WidthProperty);
                };

                rootPanel.BeginAnimation(FrameworkElement.WidthProperty, animation);
            }
            catch (Exception ex)
            {
                // 补间失败时不能留下固定宽度，否则工具栏会永远卡在旧尺寸
                rootPanel.BeginAnimation(FrameworkElement.WidthProperty, null);
                rootPanel.ClearValue(FrameworkElement.WidthProperty);
                LogHelper.WriteLogToFile($"ToolbarRegistry: 宽度补间失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        /// <summary>工具栏宽度过渡时长（毫秒），略短于按钮出场，避免拖沓。</summary>
        private const int ToolbarWidthTransitionMs = 180;

        private static void UpdatePanelVisibility(Panel panel, Dictionary<string, bool> context)
        {
            if (panel == null) return;

            foreach (var child in panel.Children.OfType<FrameworkElement>())
            {
                if (child.Tag as string == InjectedTag)
                {
                    var ruleset = GetHidingRuleset(child);
                    if (ruleset == null)
                    {
                        SetAnimatedVisibility(child, Visibility.Visible, _animateVisibilityChange);
                    }
                    else
                    {
                        bool shouldHide = EvaluateRuleset(ruleset, context);
                        SetAnimatedVisibility(child,
                            shouldHide ? Visibility.Collapsed : Visibility.Visible,
                            _animateVisibilityChange);
                    }
                    if (child is Panel innerPanel)
                    {
                        UpdatePanelVisibility(innerPanel, context);
                    }
                }
                if (child is Border border && border.Tag as string == ContentBorderTag)
                {
                    if (border.Child is StackPanel sp && sp.Tag as string == ContentPanelTag)
                    {
                        UpdatePanelVisibility(sp, context);
                        bool anyVisible = HasVisibleLeafContent(sp);
                        SetAnimatedVisibility(border,
                            anyVisible ? Visibility.Visible : Visibility.Collapsed,
                            _animateVisibilityChange);
                    }
                    else if (border.Child is Grid grid)
                    {
                        foreach (var gridChild in grid.Children.OfType<FrameworkElement>())
                        {
                            if (gridChild is StackPanel sp2 && sp2.Tag as string == ContentPanelTag)
                            {
                                UpdatePanelVisibility(sp2, context);
                            }
                        }
                        bool anyVisible = false;
                        foreach (var gridChild in grid.Children.OfType<FrameworkElement>())
                        {
                            if (gridChild is StackPanel sp2 && sp2.Tag as string == ContentPanelTag)
                            {
                                if (HasVisibleLeafContent(sp2))
                                {
                                    anyVisible = true;
                                    break;
                                }
                            }
                        }
                        SetAnimatedVisibility(border,
                            anyVisible ? Visibility.Visible : Visibility.Collapsed,
                            _animateVisibilityChange);
                    }
                }
            }
        }

        /// <summary>
        /// 按目标状态设置 Visibility，并在"从隐藏变为显示"时播放一次出场动画。
        /// </summary>
        /// <remarks>
        /// 直接赋值 Visibility 会让按钮瞬间出现，观感生硬；这里给按钮加一段
        /// 淡入 + 轻微缩放的过渡。
        ///
        /// 只有"由隐藏→显示"才播动画：<see cref="UpdatePanelVisibility"/> 会在每次模式变化时
        /// 遍历全部子项，若对每次赋值都播动画，已经在屏幕上的按钮会不断重播、整体发虚。
        ///
        /// 动画写在按钮自身而不是改布局尺寸：宽度由工具栏布局决定（缩放会挤压相邻按钮），
        /// 所以用 RenderTransform 做视觉缩放，布局位置保持最终状态不变。
        /// </remarks>
        private static void SetAnimatedVisibility(FrameworkElement element, Visibility target, bool animate)
        {
            if (element == null) return;

            bool becameVisible = target == Visibility.Visible && element.Visibility != Visibility.Visible;
            bool becameHidden = target != Visibility.Visible && element.Visibility == Visibility.Visible;

            if (!animate)
            {
                element.BeginAnimation(UIElement.OpacityProperty, null);
                element.Opacity = 1;
                element.Visibility = target;
                return;
            }

            if (becameVisible)
            {
                element.Visibility = Visibility.Visible;

                // 淡入：透明度和位移共用同一条时间轴，与 Inkeys 的组件显现做法一致。
                var fade = new System.Windows.Media.Animation.DoubleAnimation(0, 1,
                    TimeSpan.FromMilliseconds(ToolbarItemShowDurationMs))
                {
                    EasingFunction = new System.Windows.Media.Animation.CubicEase
                    {
                        EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
                    }
                };
                element.BeginAnimation(UIElement.OpacityProperty, fade);

                // 位置滑动而不是缩放：缩放会让按钮变形，且相邻按钮的视觉间距会跳动。
                // 位移写 RenderTransform，不触发布局、不挤动邻居。
                //
                // 这里用 TransformGroup 划分两个独立通道：
                //   [0] 出场滑入    [1] 邻居跟随
                // 分开是因为两者会同时发生（新按钮滑入的同时，它后面的按钮也在让位），
                // 若共用同一个 TranslateTransform.X 会互相覆盖，只剩其中一个生效。
                var enterSlide = GetOrCreateChannel(element, EnterChannelIndex);
                var slideIn = new System.Windows.Media.Animation.DoubleAnimation(
                    ToolbarItemShowSlideOffset, 0,
                    TimeSpan.FromMilliseconds(ToolbarItemShowDurationMs))
                {
                    EasingFunction = new System.Windows.Media.Animation.CubicEase
                    {
                        EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
                    }
                };
                enterSlide.BeginAnimation(TranslateTransform.XProperty, slideIn);
                return;
            }

            if (becameHidden)
            {
                // 隐藏不做过渡：收起后要立刻让出布局空间，否则工具栏宽度会滞后于内容。
                element.BeginAnimation(UIElement.OpacityProperty, null);
                element.Opacity = 1;
                element.Visibility = target;
                return;
            }

            // 状态未变：不重复播动画
            element.Visibility = target;
        }

        /// <summary>
        /// 按钮出场时长（毫秒）。
        /// 对齐 Inkeys 的 0.4s 会显得慢——那是它整套 UI 的统一节奏，
        /// 而工具栏按钮是被频繁触发的元素，这里折中取 220ms。
        /// </summary>
        internal const int ToolbarItemShowDurationMs = 220;

        /// <summary>按钮出场时的水平滑动起始偏移（DIP），0 → 该值反向滑入。</summary>
        internal const double ToolbarItemShowSlideOffset = -8;

        private static bool HasVisibleLeafContent(FrameworkElement element)
        {
            if (element.Visibility != Visibility.Visible) return false;
            if (element is Panel panel)
            {
                foreach (var child in panel.Children.OfType<FrameworkElement>())
                {
                    if (HasVisibleLeafContent(child)) return true;
                }
                return false;
            }
            if (element is Border border && border.Child is FrameworkElement borderChild)
            {
                return HasVisibleLeafContent(borderChild);
            }
            return true;
        }

        private static FrameworkElement BuildAndRegister(IToolbarHost host, IToolbarItem item, Orientation orientation = Orientation.Horizontal)
        {
            try
            {
                var view = item.BuildView(host);
                if (view == null) return null;
                host.RegisterView(item.Id, view);
                item.ApplyOrientation(view, orientation);
                return view;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"ToolbarRegistry: 构建 {item.Id} 失败: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}", LogHelper.LogType.Error);
                return null;
            }
        }

        internal static void ApplyComponentSettings(FrameworkElement view, ToolbarComponentEntry entry)
        {
            if (view == null || entry == null) return;

            var fixedWidth = entry.GetSettingDouble(ComponentSettingKeys.FixedWidth);
            if (fixedWidth.HasValue && fixedWidth.Value > 0)
                view.Width = fixedWidth.Value;
            else
            {
                var minWidth = entry.GetSettingDouble(ComponentSettingKeys.MinWidth);
                if (minWidth.HasValue && minWidth.Value > 0) view.MinWidth = minWidth.Value;
                var maxWidth = entry.GetSettingDouble(ComponentSettingKeys.MaxWidth);
                if (maxWidth.HasValue && maxWidth.Value > 0) view.MaxWidth = maxWidth.Value;
            }

            var fixedHeight = entry.GetSettingDouble(ComponentSettingKeys.FixedHeight);
            if (fixedHeight.HasValue && fixedHeight.Value > 0)
                view.Height = fixedHeight.Value;
            else
            {
                var minHeight = entry.GetSettingDouble(ComponentSettingKeys.MinHeight);
                if (minHeight.HasValue && minHeight.Value > 0) view.MinHeight = minHeight.Value;
                var maxHeight = entry.GetSettingDouble(ComponentSettingKeys.MaxHeight);
                if (maxHeight.HasValue && maxHeight.Value > 0) view.MaxHeight = maxHeight.Value;
            }

            var hAlign = entry.GetSettingString(ComponentSettingKeys.HorizontalAlignment);
            if (!string.IsNullOrEmpty(hAlign))
            {
                view.HorizontalAlignment = hAlign switch
                {
                    "Left" => HorizontalAlignment.Left,
                    "Center" => HorizontalAlignment.Center,
                    "Right" => HorizontalAlignment.Right,
                    "Stretch" => HorizontalAlignment.Stretch,
                    _ => view.HorizontalAlignment
                };
            }

            var vAlign = entry.GetSettingString(ComponentSettingKeys.VerticalAlignment);
            if (!string.IsNullOrEmpty(vAlign))
            {
                view.VerticalAlignment = vAlign switch
                {
                    "Top" => VerticalAlignment.Top,
                    "Center" => VerticalAlignment.Center,
                    "Bottom" => VerticalAlignment.Bottom,
                    "Stretch" => VerticalAlignment.Stretch,
                    _ => view.VerticalAlignment
                };
            }

            var mLeft = entry.GetSettingDouble(ComponentSettingKeys.MarginLeft) ?? 0;
            var mTop = entry.GetSettingDouble(ComponentSettingKeys.MarginTop) ?? 0;
            var mRight = entry.GetSettingDouble(ComponentSettingKeys.MarginRight) ?? 0;
            var mBottom = entry.GetSettingDouble(ComponentSettingKeys.MarginBottom) ?? 0;
            if (mLeft != 0 || mTop != 0 || mRight != 0 || mBottom != 0)
                view.Margin = new Thickness(mLeft, mTop, mRight, mBottom);

            var pLeft = entry.GetSettingDouble(ComponentSettingKeys.PaddingLeft);
            var pTop = entry.GetSettingDouble(ComponentSettingKeys.PaddingTop);
            var pRight = entry.GetSettingDouble(ComponentSettingKeys.PaddingRight);
            var pBottom = entry.GetSettingDouble(ComponentSettingKeys.PaddingBottom);
            if (pLeft.HasValue || pTop.HasValue || pRight.HasValue || pBottom.HasValue)
            {
                if (view is Border border)
                    border.Padding = new Thickness(pLeft ?? 0, pTop ?? 0, pRight ?? 0, pBottom ?? 0);
            }

            var opacity = entry.GetSettingDouble(ComponentSettingKeys.Opacity);
            if (opacity.HasValue) view.Opacity = Math.Clamp(opacity.Value, 0, 1);

            if (view is ToolbarImageButton btn)
            {
                var fontSize = entry.GetSettingDouble(ComponentSettingKeys.FontSize);
                if (fontSize.HasValue && fontSize.Value > 0)
                {
                    btn.LabelFontSize = fontSize.Value;
                    if (btn.LabelTextBlockControl != null)
                    {
                        AutoFontSizeHelper.SetOriginalFontSize(btn.LabelTextBlockControl, fontSize.Value);
                    }
                }

                var iconSize = entry.GetSettingDouble(ComponentSettingKeys.IconSize);
                if (iconSize.HasValue && iconSize.Value > 0)
                    btn.IconHeight = iconSize.Value;

                bool redStyleIcon = entry.GetSettingBool(ComponentSettingKeys.RedStyleIcon);
                bool redStyleText = entry.GetSettingBool(ComponentSettingKeys.RedStyleText);
                if (redStyleIcon || redStyleText)
                {
                    ApplyRedStyle(btn, redStyleIcon, redStyleText);
                }

                if (entry.Id == "builtin.cursorWithDel")
                {
                    var buttonName = entry.GetSettingString(ComponentSettingKeys.ButtonName);
                    bool isLongName = buttonName == "1" || buttonName == FloatingBarStrings.FloatingBar_MouseClear;
                    btn.Label = isLongName
                        ? FloatingBarStrings.FloatingBar_MouseClear
                        : FloatingBarStrings.FloatingBar_ClearAndMouse;

                    double targetFontSize = isLongName
                        ? ((fontSize.HasValue && fontSize.Value > 0) ? Math.Min(fontSize.Value, 8) : 8)
                        : ((fontSize.HasValue && fontSize.Value > 0) ? fontSize.Value : 13);

                    btn.LabelFontSize = targetFontSize;

                    var tb = btn.LabelTextBlockControl;
                    if (tb != null)
                    {
                        if (isLongName)
                        {
                            AutoFontSizeHelper.SetIsEnabled(tb, false);
                            AutoFontSizeHelper.SetOriginalFontSize(tb, targetFontSize);
                        }
                        else
                        {
                            AutoFontSizeHelper.SetOriginalFontSize(tb, targetFontSize);
                            AutoFontSizeHelper.SetIsEnabled(tb, true);
                        }
                    }

                    btn.Loaded += (s, e) =>
                    {
                        if (isLongName)
                        {
                            btn.LabelFontSize = targetFontSize;
                            if (btn.LabelTextBlockControl != null)
                            {
                                AutoFontSizeHelper.SetIsEnabled(btn.LabelTextBlockControl, false);
                                AutoFontSizeHelper.SetOriginalFontSize(btn.LabelTextBlockControl, targetFontSize);
                            }
                        }
                    };
                }
            }

            if (view is QuickColorPaletteControl qcp)
            {
                var displayMode = entry.GetSettingString(ComponentSettingKeys.DisplayMode);
                if (!string.IsNullOrEmpty(displayMode) && int.TryParse(displayMode, out var mode))
                    qcp.DisplayMode = mode;
                else
                    // 如果组件设置中没有找到，回退到全局设置
                    qcp.SyncFromSettings();

                // 强制应用显示模式，确保独立边框模式下也能正确显示
                qcp.ForceApplyDisplayMode();
            }

            // 插件自定义设置：通过 PluginToolbarItemInfo.ApplySettings 回调应用
            var pluginItem = _pluginItems.FirstOrDefault(p => p.Id == entry.Id);
            if (pluginItem != null)
            {
                pluginItem.ApplySettings?.Invoke(view, entry.Settings);
            }
        }

        private static void ApplyRedStyle(ToolbarImageButton btn, bool icon, bool text)
        {
            if (btn == null) return;

            // 仅当图标使用红色强调时标记该按钮，避免主题配色逻辑覆盖红色图标
            SetUseRedStyle(btn, icon);

            var redBrush = btn.TryFindResource("RedBrush") as Brush;
            if (icon)
            {
                if (redBrush != null) btn.IconBrush = redBrush;
                else btn.SetResourceReference(ToolbarImageButton.IconBrushProperty, "RedBrush");
            }
            if (text)
            {
                if (redBrush != null) btn.LabelBrush = redBrush;
                else btn.SetResourceReference(ToolbarImageButton.LabelBrushProperty, "RedBrush");
            }
        }

        public static ToolbarLayoutSettings CreateDefaultLayout()
        {
            return new ToolbarLayoutSettings
            {
                Components = new List<ToolbarComponentEntry>
                {
                    new ToolbarComponentEntry { Id = "builtin.cursor", HidingRuleset = ToolbarRuleset.AlwaysShow().WithHideOnCollapsed() },
                    new ToolbarComponentEntry { Id = "builtin.pen", HidingRuleset = ToolbarRuleset.AlwaysShow().WithHideOnCollapsed() },
                    new ToolbarComponentEntry { Id = "builtin.quickColorPalette", HidingRuleset = ToolbarRuleset.AnnotationOnly().WithHideOnCollapsed() },
                    new ToolbarComponentEntry { Id = "builtin.inkFreeze", HidingRuleset = ToolbarRuleset.AlwaysShow().WithHideOnCollapsed() },
                    new ToolbarComponentEntry
                    {
                        Id = "builtin.clear",
                        HidingRuleset = ToolbarRuleset.AlwaysShow().WithHideOnCollapsed(),
                        Settings = new Dictionary<string, object>
                        {
                            [ComponentSettingKeys.RedStyleIcon] = true,
                            [ComponentSettingKeys.RedStyleText] = true
                        }
                    },
                    new ToolbarComponentEntry
                    {
                        Id = "builtin.group",
                        HidingRuleset = ToolbarRuleset.AnnotationOnly().WithHideOnCollapsed(),
                        Children = new List<ToolbarComponentEntry>
                        {
                            new ToolbarComponentEntry { Id = "builtin.eraser" },
                            new ToolbarComponentEntry { Id = "builtin.eraserByStrokes" },
                            new ToolbarComponentEntry { Id = "builtin.select" },
                            new ToolbarComponentEntry { Id = "builtin.shapeDraw" },
                            new ToolbarComponentEntry { Id = "builtin.undo" },
                            new ToolbarComponentEntry { Id = "builtin.redo" },
                            new ToolbarComponentEntry { Id = "builtin.cursorWithDel" }
                        }
                    },
                    new ToolbarComponentEntry { Id = "builtin.separator", HidingRuleset = ToolbarRuleset.AlwaysShow().WithHideOnCollapsed() },
                    new ToolbarComponentEntry { Id = "builtin.whiteboard", HidingRuleset = ToolbarRuleset.AlwaysShow().WithHideOnCollapsed() },
                    new ToolbarComponentEntry { Id = "builtin.tools", HidingRuleset = ToolbarRuleset.AlwaysShow().WithHideOnCollapsed() },
                    new ToolbarComponentEntry { Id = "builtin.fold", HidingRuleset = ToolbarRuleset.AlwaysShow().WithHideOnCollapsed() },
                    new ToolbarComponentEntry { Id = "builtin.gesture", HidingRuleset = ToolbarRuleset.AnnotationOnly(), ShowSeparateBorder = true },
                    new ToolbarComponentEntry { Id = "builtin.exit", HidingRuleset = ToolbarRuleset.PPTOnly(), ShowSeparateBorder = true }
                }
            };
        }
    }

    /// <summary>
    /// 将 PluginToolbarItemInfo 包装为 IToolbarItem，供 ToolbarRegistry 内部使用。
    /// </summary>
    internal class PluginToolbarItemWrapper : IToolbarItem
    {
        private readonly PluginToolbarItemInfo _info;

        public string Id => _info.Id;
        public string DisplayName => _info.DisplayName;
        public string Description => _info.Description;
        public string IconGeometry => _info.IconGeometry;
        public FontIconData? IconKey => null;
        public ToolbarRuleset DefaultHidingRuleset => ToolbarRuleset.AlwaysShow().WithHideOnCollapsed();
        public bool DefaultShowSeparateBorder => false;
        public bool DefaultPreventHideOnDragClick => false;

        public PluginToolbarItemWrapper(PluginToolbarItemInfo info)
        {
            _info = info;
        }

        public FrameworkElement BuildView(IToolbarHost host)
        {
            var view = _info.ViewFactory?.Invoke();
            if (view != null)
                view.Tag = ToolbarRegistry.InjectedTag;

            // 如果提供了弹窗内容工厂，自动创建 Popup 并绑定按钮点击
            if (_info.PopupContentFactory != null && view is ToolbarImageButton btn)
            {
                var popup = new System.Windows.Controls.Primitives.Popup
                {
                    Name = "PluginPopup_" + _info.Id.Replace('.', '_'),
                    AllowsTransparency = true,
                    StaysOpen = true,
                    Focusable = true,
                    IsOpen = false,
                    PlacementTarget = btn,
                    Placement = System.Windows.Controls.Primitives.PlacementMode.Custom
                };

                var popupContent = _info.PopupContentFactory();
                if (popupContent != null)
                    popup.Child = popupContent;

                popup.CustomPopupPlacementCallback = (popupSize, targetSize, offset) =>
                {
                    return new[]
                    {
                        new CustomPopupPlacement(
                            new Point(targetSize.Width / 2 - popupSize.Width / 2, -popupSize.Height - 8),
                            PopupPrimaryAxis.Vertical)
                    };
                };

                // 注册 Popup 到 PopupManagerHelper
                btn.Loaded += (s, e) =>
                {
                    var window = Window.GetWindow(btn);
                    if (window is MainWindow mw)
                    {
                        mw.GetPopupManager()?.RegisterPopup(popup);
                    }
                };

                btn.ButtonMouseUp += (s, e) =>
                {
                    if (popup.IsOpen)
                    {
                        popup.IsOpen = false;
                    }
                    else
                    {
                        // 关闭主窗口中其他已打开的 Popup
                        var window = Window.GetWindow(btn);
                        if (window is MainWindow mw)
                        {
                            mw.CloseAllPopups();
                        }
                        AnimationsHelper.ShowPopupWithSlideAndFade(popup);
                        popup.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            if (popup.Child is UIElement child)
                            {
                                child.Focus();
                                Keyboard.Focus(child);
                                child.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
                            }
                        }), System.Windows.Threading.DispatcherPriority.Input);
                    }
                };

                // 弹窗关闭按钮支持。
                // 直接返回 PopupShellContent（或 PopupTabShellContent）时按原样接线；
                // 插件常返回外层 UserControl、内含 Shell（如 PdfReader 的 ReaderPopupContent），
                // 此时在视觉树里递归查找 Shell 再接线，否则标题栏关闭按钮点了没反应。
                if (popupContent is PopupShellContent shell)
                {
                    shell.CloseButtonControl.Click += (s, e) => popup.IsOpen = false;
                }
                else if (popupContent is PopupTabShellContent tabShell)
                {
                    tabShell.CloseButtonControl.Click += (s, e) => popup.IsOpen = false;
                }
                else if (popupContent is FrameworkElement contentElement)
                {
                    // 立即尝试一次；Popup 未打开时子元素的视觉树可能尚未完全展开，
                    // 因此再在 Opened 后重试一次（视觉树此时一定完整）。
                    WireNestedShellCloseButton(contentElement, popup);
                    popup.Opened += (s, e) => WireNestedShellCloseButton(contentElement, popup);
                }
            }

            return view;
        }

        /// <summary>在弹窗内容里递归查找 PopupShellContent，把它的标题栏关闭按钮接到 popup 收起。</summary>
        private static void WireNestedShellCloseButton(FrameworkElement content, Popup popup)
        {
            if (content == null || popup == null) return;

            foreach (var nestedShell in FindVisualChildren<PopupShellContent>(content))
            {
                var closeButton = nestedShell.CloseButtonControl;
                if (closeButton == null) continue;

                // 用 Tag 记录已接线的 popup，避免 Opened 重试时重复订阅导致连关两次。
                if (ReferenceEquals(closeButton.Tag, popup)) return;
                closeButton.Tag = popup;
                closeButton.Click += (s, e) => popup.IsOpen = false;
                return; // 只需接最外层那个 Shell
            }
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) yield break;
            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result) yield return result;
                foreach (var descendant in FindVisualChildren<T>(child))
                    yield return descendant;
            }
        }

        public void ApplyOrientation(FrameworkElement view, Orientation orientation)
        {
            _info.ApplyOrientation?.Invoke(view, orientation);
        }
    }
}
