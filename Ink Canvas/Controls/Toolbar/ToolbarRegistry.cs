using Ink_Canvas.Helpers;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Ink_Canvas.Controls.Toolbar
{
    public static class ToolbarRegistry
    {
        private static List<IToolbarItem> _items;
        internal const string InjectedTag = "ToolbarRegistryInjected";

        private static readonly string ConfigSubDir = Path.Combine("Configs", "ToolbarConfigs");

        public static readonly DependencyProperty HidingRulesetProperty =
            DependencyProperty.RegisterAttached("HidingRuleset", typeof(ToolbarRuleset), typeof(ToolbarRegistry),
                new PropertyMetadata(null));

        public static void SetHidingRuleset(FrameworkElement element, ToolbarRuleset value)
            => element.SetValue(HidingRulesetProperty, value);

        public static ToolbarRuleset GetHidingRuleset(FrameworkElement element)
            => (ToolbarRuleset)element.GetValue(HidingRulesetProperty);

        public static List<KeyValuePair<string, string>> AvailableConditions { get; } = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("isAnnotating", "批注模式"),
            new KeyValuePair<string, string>("isPptMode", "PPT模式"),
            new KeyValuePair<string, string>("isGestureEnabled", "手势开关已启用")
        };

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
                return null;

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
                ToolbarHidingRule.PptOnly => ToolbarRuleset.PptOnly(),
                ToolbarHidingRule.PptAnnotationOnly => ToolbarRuleset.PptAnnotationOnly(),
                ToolbarHidingRule.AnnotationOrPptGesture => ToolbarRuleset.GestureRule(),
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
                .Where(t => !t.IsAbstract && !t.IsInterface && itemType.IsAssignableFrom(t))
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
            LogHelper.WriteLogToFile($"ToolbarRegistry: Discover 完成, 发现 {_items.Count} 个条目", LogHelper.LogType.Info);
            return _items;
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
                LogHelper.WriteLogToFile($"ToolbarRegistry: 配置文件不存在 [{path}]", LogHelper.LogType.Warning);
                return null;
            }
            try
            {
                var json = File.ReadAllText(path);
                var layout = JsonConvert.DeserializeObject<ToolbarLayoutSettings>(json);
                LogHelper.WriteLogToFile($"ToolbarRegistry: 加载配置 [{name}] 成功, {layout?.Components?.Count ?? 0} 个条目", LogHelper.LogType.Info);
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
                var json = JsonConvert.SerializeObject(layout, Formatting.Indented);
                var path = GetConfigFilePath(name);
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
                LogHelper.WriteLogToFile($"ToolbarRegistry: 删除配置 [{name}]", LogHelper.LogType.Info);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"ToolbarRegistry: 删除配置 [{name}] 失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        public static void EnsureDefaultConfigExists()
        {
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

        #endregion

        public static void ClearInjected(Panel container)
        {
            if (container == null) return;
            var toRemove = container.Children.OfType<FrameworkElement>()
                .Where(e => e.Tag as string == InjectedTag)
                .ToList();
            foreach (var element in toRemove)
                container.Children.Remove(element);
            LogHelper.WriteLogToFile($"ToolbarRegistry: ClearInjected 清除 {toRemove.Count} 个元素 [{container.Name}]", LogHelper.LogType.Info);
        }

        public static void Populate(IToolbarHost host, Panel container, ToolbarLayoutSettings layout)
        {
            LogHelper.WriteLogToFile($"ToolbarRegistry: Populate 开始", LogHelper.LogType.Info);
            if (host == null || container == null)
            {
                LogHelper.WriteLogToFile("ToolbarRegistry: Populate host 或 container 为空", LogHelper.LogType.Warning);
                return;
            }

            layout = layout ?? CreateDefaultLayout();
            if (layout.Components == null || layout.Components.Count == 0)
            {
                layout = CreateDefaultLayout();
            }

            var discovered = Discover();
            var itemMap = discovered.ToDictionary(i => i.Id, i => i);

            PopulateEntries(host, container, layout.Components, itemMap);

            LogHelper.WriteLogToFile($"ToolbarRegistry: Populate 完成, 共添加 {layout.Components.Count} 个条目", LogHelper.LogType.Info);
        }

        private static void PopulateEntries(IToolbarHost host, Panel container, List<ToolbarComponentEntry> entries, Dictionary<string, IToolbarItem> itemMap)
        {
            foreach (var entry in entries)
            {
                if (entry.IsGroup)
                {
                    PopulateGroup(host, container, entry, itemMap);
                    continue;
                }

                if (!itemMap.TryGetValue(entry.Id, out var item))
                {
                    LogHelper.WriteLogToFile($"ToolbarRegistry: 未找到条目 [{entry.Id}]", LogHelper.LogType.Warning);
                    continue;
                }

                var view = BuildAndRegister(host, item);
                if (view == null) continue;

                view.Tag = InjectedTag;
                var ruleset = GetEffectiveRuleset(entry);
                SetHidingRuleset(view, ruleset);

                FrameworkElement elementToAdd;

                if (entry.ShowSeparateBorder)
                {
                    elementToAdd = WrapInSeparateBorder(view, ruleset);
                }
                else
                {
                    elementToAdd = view;
                }

                ApplyInitialVisibility(elementToAdd, ruleset);
                container.Children.Add(elementToAdd);
                LogHelper.WriteLogToFile($"ToolbarRegistry: 添加条目 [{entry.Id}]", LogHelper.LogType.Info);
            }
        }

        private static void PopulateGroup(IToolbarHost host, Panel container, ToolbarComponentEntry groupEntry, Dictionary<string, IToolbarItem> itemMap)
        {
            if (groupEntry.Children == null || groupEntry.Children.Count == 0)
            {
                LogHelper.WriteLogToFile("ToolbarRegistry: 分组组件无子项，跳过", LogHelper.LogType.Warning);
                return;
            }

            var innerPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };

            foreach (var childEntry in groupEntry.Children)
            {
                if (!itemMap.TryGetValue(childEntry.Id, out var item))
                {
                    LogHelper.WriteLogToFile($"ToolbarRegistry: 分组内未找到条目 [{childEntry.Id}]", LogHelper.LogType.Warning);
                    continue;
                }

                var childView = BuildAndRegister(host, item);
                if (childView == null) continue;

                childView.Tag = InjectedTag;
                childView.Margin = new Thickness(0);
                innerPanel.Children.Add(childView);
            }

            FrameworkElement groupElement;
            var ruleset = GetEffectiveRuleset(groupEntry);

            if (groupEntry.ShowSeparateBorder)
            {
                groupElement = WrapInSeparateBorder(innerPanel, ruleset);
            }
            else
            {
                innerPanel.Tag = InjectedTag;
                SetHidingRuleset(innerPanel, ruleset);
                groupElement = innerPanel;
            }

            ApplyInitialVisibility(groupElement, ruleset);
            container.Children.Add(groupElement);
            LogHelper.WriteLogToFile($"ToolbarRegistry: 添加分组 Children={groupEntry.Children.Count}", LogHelper.LogType.Info);
        }

        private static Border WrapInSeparateBorder(FrameworkElement view, ToolbarRuleset ruleset)
        {
            var bgBrush = Application.Current.TryFindResource("FloatBarBackground") as Brush
                ?? new SolidColorBrush(Colors.White);
            var borderBrush = Application.Current.TryFindResource("FloatBarBorderBrush") as Brush
                ?? new SolidColorBrush(Color.FromRgb(0x7D, 0x7D, 0x7D));
            var wrapper = new Border
            {
                Margin = new Thickness(3, 0, 0, 0),
                Padding = new Thickness(0),
                Background = bgBrush,
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(2),
                BorderBrush = borderBrush,
                Child = view,
                Tag = InjectedTag
            };
            SetHidingRuleset(wrapper, ruleset);
            return wrapper;
        }

        private static void ApplyInitialVisibility(FrameworkElement element, ToolbarRuleset ruleset)
        {
            element.Visibility = Visibility.Visible;
        }

        public static void UpdateVisibilityByMode(Panel container, bool isAnnotating, bool isPptMode, bool isGestureEnabled = false)
        {
            if (container == null) return;

            var context = new Dictionary<string, bool>
            {
                ["isAnnotating"] = isAnnotating,
                ["isPptMode"] = isPptMode,
                ["isGestureEnabled"] = isGestureEnabled
            };

            foreach (var child in container.Children.OfType<FrameworkElement>())
            {
                if (child.Tag as string != InjectedTag) continue;
                var ruleset = GetHidingRuleset(child);
                if (ruleset == null)
                {
                    child.Visibility = Visibility.Visible;
                    continue;
                }

                bool shouldHide = EvaluateRuleset(ruleset, context);
                child.Visibility = shouldHide ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private static FrameworkElement BuildAndRegister(IToolbarHost host, IToolbarItem item)
        {
            try
            {
                var view = item.BuildView(host);
                if (view == null) return null;
                host.RegisterView(item.Id, view);
                return view;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"ToolbarRegistry: 构建 {item.Id} 失败: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}", LogHelper.LogType.Error);
                return null;
            }
        }

        public static ToolbarLayoutSettings CreateDefaultLayout()
        {
            return new ToolbarLayoutSettings
            {
                Components = new List<ToolbarComponentEntry>
                {
                    new ToolbarComponentEntry { Id = "builtin.cursor", HidingRuleset = ToolbarRuleset.AlwaysShow() },
                    new ToolbarComponentEntry { Id = "builtin.pen", HidingRuleset = ToolbarRuleset.AlwaysShow() },
                    new ToolbarComponentEntry { Id = "builtin.quickColorPalette", HidingRuleset = ToolbarRuleset.AnnotationOnly() },
                    new ToolbarComponentEntry { Id = "builtin.inkFreeze", HidingRuleset = ToolbarRuleset.AlwaysShow() },
                    new ToolbarComponentEntry { Id = "builtin.clear", HidingRuleset = ToolbarRuleset.AlwaysShow() },
                    new ToolbarComponentEntry
                    {
                        Id = "builtin.group",
                        HidingRuleset = ToolbarRuleset.AnnotationOnly(),
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
                    new ToolbarComponentEntry { Id = "builtin.separator", HidingRuleset = ToolbarRuleset.AlwaysShow() },
                    new ToolbarComponentEntry { Id = "builtin.whiteboard", HidingRuleset = ToolbarRuleset.AlwaysShow() },
                    new ToolbarComponentEntry { Id = "builtin.tools", HidingRuleset = ToolbarRuleset.AlwaysShow() },
                    new ToolbarComponentEntry { Id = "builtin.fold", HidingRuleset = ToolbarRuleset.AlwaysShow() },
                    new ToolbarComponentEntry { Id = "builtin.gesture", HidingRuleset = ToolbarRuleset.GestureRule(), ShowSeparateBorder = true },
                    new ToolbarComponentEntry { Id = "builtin.exit", HidingRuleset = ToolbarRuleset.PptOnly(), ShowSeparateBorder = true }
                }
            };
        }
    }
}
