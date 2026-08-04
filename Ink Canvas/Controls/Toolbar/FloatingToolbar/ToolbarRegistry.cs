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
            }

            if (changed)
            {
                SaveConfigFile(configName, layout);
                LogHelper.WriteLogToFile(
                    $"ToolbarRegistry: 已修正配置 [{configName}] 中 ConditionId 大小写",
                    LogHelper.LogType.Info);
            }
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

        private static List<DisplayItem> FlattenEntries(IToolbarHost host, List<ToolbarComponentEntry> entries, Dictionary<string, IToolbarItem> itemMap, Orientation orientation = Orientation.Horizontal, bool inheritedUseRedStyle = false)
        {
            var result = new List<DisplayItem>();
            foreach (var entry in entries)
            {
                if (entry.IsGroup)
                {
                    var groupRuleset = GetEffectiveRuleset(entry);
                    var groupUseRedStyle = inheritedUseRedStyle || entry.GetSettingBool(ComponentSettingKeys.UseRedStyle);
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
                            var nestedItems = FlattenEntries(host, new List<ToolbarComponentEntry> { childEntry }, itemMap, orientation, groupUseRedStyle);
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
                        if (groupUseRedStyle && view is ToolbarImageButton groupButton)
                        {
                            SetUseRedStyle(groupButton, true);
                            ApplyRedStyle(groupButton);
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
                    if (inheritedUseRedStyle && view is ToolbarImageButton inheritedButton)
                    {
                        ApplyRedStyle(inheritedButton);
                    }
                    var ruleset = GetEffectiveRuleset(entry);
                    SetHidingRuleset(view, ruleset);
                    if (entry.GetSettingBool(ComponentSettingKeys.UseRedStyle))
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
            UpdatePanelVisibility(rootPanel, context);
        }

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
                        child.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        bool shouldHide = EvaluateRuleset(ruleset, context);
                        child.Visibility = shouldHide ? Visibility.Collapsed : Visibility.Visible;
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
                        border.Visibility = anyVisible ? Visibility.Visible : Visibility.Collapsed;
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
                        border.Visibility = anyVisible ? Visibility.Visible : Visibility.Collapsed;
                    }
                }
            }
        }

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
                    btn.LabelFontSize = fontSize.Value;

                var iconSize = entry.GetSettingDouble(ComponentSettingKeys.IconSize);
                if (iconSize.HasValue && iconSize.Value > 0)
                    btn.IconHeight = iconSize.Value;

                if (entry.GetSettingBool(ComponentSettingKeys.UseRedStyle))
                {
                    ApplyRedStyle(btn);
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

        private static void ApplyRedStyle(ToolbarImageButton btn)
        {
            if (btn == null) return;

            SetUseRedStyle(btn, true);
            if (btn.TryFindResource("RedBrush") is Brush redBrush)
            {
                btn.IconBrush = redBrush;
                btn.LabelBrush = redBrush;
            }
            else
            {
                btn.SetResourceReference(ToolbarImageButton.IconBrushProperty, "RedBrush");
                btn.SetResourceReference(ToolbarImageButton.LabelBrushProperty, "RedBrush");
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
                    new ToolbarComponentEntry { Id = "builtin.clear", HidingRuleset = ToolbarRuleset.AlwaysShow().WithHideOnCollapsed() },
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
