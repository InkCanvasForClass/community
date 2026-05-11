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
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas.Controls.Toolbar
{
    public static class ToolbarRegistry
    {
        private static List<IToolbarItem> _items;
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
                        SaveConfigFile(name, bakResult);
                        LogHelper.WriteLogToFile($"ToolbarRegistry: 从备份恢复配置 [{name}] 成功", LogHelper.LogType.Info);
                    }
                    return bakResult;
                }
                LogHelper.WriteLogToFile($"ToolbarRegistry: 配置文件不存在 [{path}]", LogHelper.LogType.Warning);
                return null;
            }
            var result = TryDeserializeConfig(path, name);
            if (result != null) return result;

            var backupPath = GetBackupFilePath(name);
            if (File.Exists(backupPath))
            {
                LogHelper.WriteLogToFile($"ToolbarRegistry: 主配置文件损坏，尝试加载备份 [{backupPath}]", LogHelper.LogType.Warning);
                var bakResult = TryDeserializeConfig(backupPath, name);
                if (bakResult != null)
                {
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
                LogHelper.WriteLogToFile($"ToolbarRegistry: 加载配置 [{name}] 成功, {layout.Components.Count} 个条目", LogHelper.LogType.Info);
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
                .Where(e => e.Tag as string == InjectedTag || e.Tag as string == ContentBorderTag)
                .ToList();
            foreach (var element in toRemove)
                container.Children.Remove(element);
            LogHelper.WriteLogToFile($"ToolbarRegistry: ClearInjected 清除 {toRemove.Count} 个元素 [{container.Name}]", LogHelper.LogType.Info);
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

        private static List<DisplayItem> FlattenEntries(IToolbarHost host, List<ToolbarComponentEntry> entries, Dictionary<string, IToolbarItem> itemMap)
        {
            var result = new List<DisplayItem>();
            foreach (var entry in entries)
            {
                if (entry.IsGroup)
                {
                    var groupRuleset = GetEffectiveRuleset(entry);
                    var groupContentItems = new List<DisplayItem>();

                    foreach (var childEntry in entry.Children)
                    {
                        if (!itemMap.TryGetValue(childEntry.Id, out var item)) continue;
                        var view = BuildAndRegister(host, item);
                        if (view == null) continue;
                        view.Tag = InjectedTag;
                        ApplyComponentSettings(view, childEntry);
                        var childRuleset = GetEffectiveRuleset(childEntry);
                        SetHidingRuleset(view, childRuleset);

                        if (childEntry.ShowSeparateBorder)
                        {
                            if (groupContentItems.Count > 0)
                            {
                                FlushGroupContentItems(result, groupContentItems, groupRuleset, entry.ShowSeparateBorder);
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
                        FlushGroupContentItems(result, groupContentItems, groupRuleset, entry.ShowSeparateBorder);
                    }
                }
                else
                {
                    if (!itemMap.TryGetValue(entry.Id, out var item))
                    {
                        LogHelper.WriteLogToFile($"ToolbarRegistry: 未找到条目 [{entry.Id}]", LogHelper.LogType.Warning);
                        continue;
                    }
                    var view = BuildAndRegister(host, item);
                    if (view == null) continue;
                    view.Tag = InjectedTag;
                    ApplyComponentSettings(view, entry);
                    var ruleset = GetEffectiveRuleset(entry);
                    SetHidingRuleset(view, ruleset);
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

        private static void FlushGroupContentItems(List<DisplayItem> result, List<DisplayItem> groupContentItems, ToolbarRuleset groupRuleset, bool groupShowSeparateBorder)
        {
            if (groupContentItems.Count == 0) return;

            if (groupShowSeparateBorder)
            {
                var innerPanel = new StackPanel { Orientation = Orientation.Horizontal };
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
                result.Add(CreateGroupContentDisplayItem(groupContentItems, groupRuleset));
            }
        }

        private static DisplayItem CreateGroupContentDisplayItem(List<DisplayItem> groupContentItems, ToolbarRuleset groupRuleset)
        {
            var innerPanel = new StackPanel { Orientation = Orientation.Horizontal };
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

        public static void Populate(IToolbarHost host, Panel rootPanel, ToolbarLayoutSettings layout)
        {
            LogHelper.WriteLogToFile($"ToolbarRegistry: Populate 开始", LogHelper.LogType.Info);
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

            var displayItems = FlattenEntries(host, layout.Components, itemMap);
            var segments = GroupIntoSegments(displayItems);

            bool hasExistingChildren = rootPanel.Children.Count > 0;
            bool isFirst = true;
            foreach (var segment in segments)
            {
                if (segment.IsSeparateBorder)
                {
                    var item = segment.Items[0];
                    var elementToAdd = WrapInSeparateBorder(item.View, item.Ruleset, item.IsToolbarButton);
                    elementToAdd.Margin = (isFirst && !hasExistingChildren) ? new Thickness(0) : new Thickness(3, 0, 0, 0);
                    ApplyInitialVisibility(elementToAdd, item.Ruleset);
                    rootPanel.Children.Add(elementToAdd);
                    LogHelper.WriteLogToFile($"ToolbarRegistry: 添加独立边框条目到根面板", LogHelper.LogType.Info);
                }
                else
                {
                    var contentBorder = CreateContentBorder(segment.Items);
                    contentBorder.Margin = (isFirst && !hasExistingChildren) ? new Thickness(0) : new Thickness(3, 0, 0, 0);
                    rootPanel.Children.Add(contentBorder);
                    LogHelper.WriteLogToFile($"ToolbarRegistry: 添加内容边框 ({segment.Items.Count} 项) 到根面板", LogHelper.LogType.Info);
                }
                isFirst = false;
            }

            LogHelper.WriteLogToFile($"ToolbarRegistry: Populate 完成, 共 {segments.Count} 个段, {layout.Components.Count} 个条目", LogHelper.LogType.Info);
        }

        private static Border CreateContentBorder(List<DisplayItem> items)
        {
            var bgBrush = Application.Current.TryFindResource("FloatBarBackground") as Brush
                ?? new SolidColorBrush(Colors.White);
            var borderBrush = Application.Current.TryFindResource("FloatBarBorderBrush") as Brush
                ?? new SolidColorBrush(Color.FromRgb(0x7D, 0x7D, 0x7D));

            var contentPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(2, 0, 2, 0),
                Cursor = Cursors.Arrow,
                HorizontalAlignment = HorizontalAlignment.Left,
                Tag = ContentPanelTag
            };

            foreach (var item in items)
            {
                ApplyInitialVisibility(item.View, item.Ruleset);
                contentPanel.Children.Add(item.View);
            }

            var grid = new Grid();

            var canvas = new System.Windows.Controls.Canvas
            {
                Margin = new Thickness(2, 0, 2, 0),
                Tag = SelectionCanvasTag
            };

            var selectionBG = new Border
            {
                Visibility = Visibility.Hidden,
                Width = 28,
                Height = 46,
                Margin = new Thickness(0, -2, 0, -2),
                Background = new SolidColorBrush(Color.FromArgb(0x15, 0x3b, 0x82, 0xf6)),
                Tag = SelectionBGTag
            };
            System.Windows.Controls.Canvas.SetLeft(selectionBG, 28);
            canvas.Children.Add(selectionBG);

            var indicatorBar = new Border
            {
                Visibility = Visibility.Hidden,
                Width = 16,
                Height = 3,
                CornerRadius = new CornerRadius(1.5),
                Background = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xeb)),
                Tag = IndicatorBarTag
            };
            System.Windows.Controls.Canvas.SetLeft(indicatorBar, 34);
            System.Windows.Controls.Canvas.SetBottom(indicatorBar, 1);
            canvas.Children.Add(indicatorBar);

            grid.Children.Add(canvas);
            grid.Children.Add(contentPanel);

            var border = new Border
            {
                Padding = new Thickness(2),
                Visibility = Visibility.Visible,
                Height = 50,
                Background = bgBrush,
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(2),
                BorderBrush = borderBrush,
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = grid,
                Tag = ContentBorderTag
            };

            return border;
        }

        private static Border WrapInSeparateBorder(FrameworkElement view, ToolbarRuleset ruleset, bool isToolbarButton)
        {
            var bgBrush = Application.Current.TryFindResource("FloatBarBackground") as Brush
                ?? new SolidColorBrush(Colors.White);
            var borderBrush = Application.Current.TryFindResource("FloatBarBorderBrush") as Brush
                ?? new SolidColorBrush(Color.FromRgb(0x7D, 0x7D, 0x7D));
            var wrapper = new Border
            {
                Margin = new Thickness(0),
                Padding = isToolbarButton ? new Thickness(0) : new Thickness(4, 2, 4, 2),
                Width = double.NaN,
                MinWidth = isToolbarButton ? 50 : 0,
                Height = double.NaN,
                MinHeight = 50,
                Background = bgBrush,
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(2),
                BorderBrush = borderBrush,
                Child = view,
                Tag = InjectedTag
            };

            if (isToolbarButton)
            {
                view.HorizontalAlignment = HorizontalAlignment.Center;
                view.VerticalAlignment = VerticalAlignment.Center;
            }
            else
            {
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

        public static void UpdateVisibilityByMode(Panel rootPanel, bool isAnnotating, bool isPptMode, bool isGestureEnabled = false)
        {
            var context = new Dictionary<string, bool>
            {
                ["isAnnotating"] = isAnnotating,
                ["isPptMode"] = isPptMode,
                ["isGestureEnabled"] = isGestureEnabled
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
                }
                if (child is Border border && border.Tag as string == ContentBorderTag && border.Child is Grid grid)
                {
                    foreach (var gridChild in grid.Children.OfType<FrameworkElement>())
                    {
                        if (gridChild is StackPanel sp && sp.Tag as string == ContentPanelTag)
                        {
                            UpdatePanelVisibility(sp, context);
                        }
                    }
                }
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
