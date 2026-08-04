using Ink_Canvas.Helpers;
using Ink_Canvas.Plugins;
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
using System.Windows.Media;

namespace Ink_Canvas.Controls.Toolbar.BoardToolbar
{
    public static class BoardToolbarRegistry
    {
        private static List<IBoardToolbarItem> _items;
        private static readonly List<PluginToolbarItemInfo> _pluginItems = new List<PluginToolbarItemInfo>();
        private static readonly string ConfigSubDir = Path.Combine("Configs", "BoardToolbarConfigs");

        public static IReadOnlyList<IBoardToolbarItem> Discover()
        {
            if (_items != null) return _items;

            var itemType = typeof(IBoardToolbarItem);
            _items = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => !t.IsAbstract && !t.IsInterface && itemType.IsAssignableFrom(t))
                .Select(t =>
                {
                    try { return (IBoardToolbarItem)Activator.CreateInstance(t); }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"BoardToolbarRegistry: 实例化 {t.FullName} 失败: {ex.Message}", LogHelper.LogType.Warning);
                        return null;
                    }
                })
                .Where(i => i != null)
                .ToList();

            // 追加插件注册的白板工具栏组件。
            foreach (var pluginItem in _pluginItems)
            {
                _items.Add(new PluginBoardToolbarItemWrapper(pluginItem));
            }

            return _items;
        }

        public static IBoardToolbarItem FindItem(string id)
        {
            var items = Discover();
            return items.FirstOrDefault(i => i.Id == id);
        }

        #region 插件组件注册

        /// <summary>
        /// 注册一个插件白板工具栏组件。首个注册的插件启动时把组件追加进 active 配置（默认 center→tools），
        /// 后续启动只加入组件库，避免用户删除组件后重启又被自动加回。
        /// </summary>
        public static void RegisterPluginItem(PluginToolbarItemInfo itemInfo, bool autoAddToActiveConfig = true)
        {
            if (itemInfo == null || string.IsNullOrEmpty(itemInfo.Id)) return;
            if (_pluginItems.Any(item => string.Equals(item.Id, itemInfo.Id, StringComparison.OrdinalIgnoreCase))) return;

            _pluginItems.Add(itemInfo);
            LogHelper.WriteLogToFile($"BoardToolbarRegistry: 插件注册白板工具栏组件 [{itemInfo.Id}] (autoAddToActiveConfig={autoAddToActiveConfig})", LogHelper.LogType.Info);

            if (autoAddToActiveConfig)
            {
                EnsurePluginItemInActiveConfig(itemInfo.Id);
            }

            if (_items != null)
            {
                _items.Add(new PluginBoardToolbarItemWrapper(itemInfo));
            }
        }

        private static void EnsurePluginItemInActiveConfig(string itemId)
        {
            EnsureDefaultConfigExists();

            var configName = SettingsManager.Settings?.BoardToolbarConfigName;
            if (string.IsNullOrWhiteSpace(configName)) configName = "default";

            var layout = LoadActiveConfig() ?? BoardToolbarLayoutSettings.CreateDefault();
            layout.Areas ??= new List<BoardToolbarAreaEntry>();

            // 定位 center 区；没有则新建。
            var centerArea = layout.Areas.FirstOrDefault(a => string.Equals(a.Id, "center", StringComparison.OrdinalIgnoreCase));
            if (centerArea == null)
            {
                centerArea = new BoardToolbarAreaEntry { Id = "center", Groups = new List<BoardToolbarGroupEntry>() };
                layout.Areas.Add(centerArea);
            }
            centerArea.Groups ??= new List<BoardToolbarGroupEntry>();

            // 定位 center 的 tools 组；没有则用第一个组，再没有则新建 "plugin" 组。
            var group = centerArea.Groups.FirstOrDefault(g => string.Equals(g.Id, "tools", StringComparison.OrdinalIgnoreCase))
                        ?? centerArea.Groups.FirstOrDefault();
            if (group == null)
            {
                group = new BoardToolbarGroupEntry { Id = "plugin", Components = new List<BoardToolbarComponentEntry>() };
                centerArea.Groups.Add(group);
            }
            group.Components ??= new List<BoardToolbarComponentEntry>();

            if (group.Components.Any(c => string.Equals(c.Id, itemId, StringComparison.OrdinalIgnoreCase))) return;

            group.Components.Add(new BoardToolbarComponentEntry { Id = itemId });
            SaveConfigFile(configName, layout);
            LogHelper.WriteLogToFile(
                $"BoardToolbarRegistry: 已将插件组件 [{itemId}] 加入当前配置 [{configName}] 的 {group.Id} 组",
                LogHelper.LogType.Info);
        }

        /// <summary>
        /// 注销插件注册的白板工具栏组件，断开对插件程序集中委托的引用。语义同
        /// <see cref="FloatingToolbar.ToolbarRegistry.UnregisterPluginItem"/>：热重载必需，
        /// 且不动用户布局配置。
        /// </summary>
        public static bool UnregisterPluginItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;

            var removed = _pluginItems.RemoveAll(
                item => string.Equals(item.Id, itemId, StringComparison.OrdinalIgnoreCase)) > 0;

            _items?.RemoveAll(item => item is PluginBoardToolbarItemWrapper
                                      && string.Equals(item.Id, itemId, StringComparison.OrdinalIgnoreCase));

            if (removed)
                LogHelper.WriteLogToFile($"BoardToolbarRegistry: 已注销插件白板工具栏组件 [{itemId}]", LogHelper.LogType.Info);

            return removed;
        }

        #endregion

        public static FrameworkElement BuildView(string id, IBoardToolbarHost host)
        {
            var item = FindItem(id);
            if (item == null)
            {
                LogHelper.WriteLogToFile($"BoardToolbarRegistry: 未找到组件 [{id}]", LogHelper.LogType.Warning);
                return null;
            }

            try
            {
                var view = item.BuildView(host);
                if (view != null)
                {
                    host.RegisterView(id, view);
                }
                return view;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"BoardToolbarRegistry: 构建 {id} 失败: {ex.Message}", LogHelper.LogType.Error);
                return null;
            }
        }

        public static List<FrameworkElement> BuildGroup(IBoardToolbarHost host, List<BoardToolbarComponentEntry> components, string areaId = null)
        {
            var views = new List<FrameworkElement>();
            var items = Discover();
            var itemMap = items.ToDictionary(i => i.Id, i => i);

            for (int i = 0; i < components.Count; i++)
            {
                var entry = components[i];

                if (!itemMap.TryGetValue(entry.Id, out var item))
                {
                    LogHelper.WriteLogToFile($"BoardToolbarRegistry: 未找到组件 [{entry.Id}]", LogHelper.LogType.Warning);
                    continue;
                }

                try
                {
                    FrameworkElement view;
                    if (item is Items.BoardPageInfoToolItem pageInfoItem)
                    {
                        view = Items.BoardPageInfoToolItem.BuildPageInfoView(host, areaId);
                    }
                    else
                    {
                        view = item.BuildView(host);
                    }

                    if (view != null)
                    {
                        var position = ComputeButtonPosition(i, components.Count);
                        item.ApplyPosition(view, position);
                        ApplyComponentSettings(view, entry);
                        host.RegisterView(entry.Id, view);
                        if (areaId != null)
                            host.RegisterView($"{entry.Id}.{areaId}", view);
                        views.Add(view);
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"BoardToolbarRegistry: 构建 {entry.Id} 失败: {ex.Message}", LogHelper.LogType.Error);
                }
            }

            return views;
        }

        internal static ButtonPosition ComputeButtonPosition(int index, int totalCount)
        {
            if (totalCount == 1) return ButtonPosition.Single;
            if (index == 0) return ButtonPosition.First;
            if (index == totalCount - 1) return ButtonPosition.Last;
            return ButtonPosition.Middle;
        }

        public static List<FrameworkElement> BuildGroup(IBoardToolbarHost host, params string[] ids)
        {
            var components = ids.Select(id => new BoardToolbarComponentEntry { Id = id }).ToList();
            return BuildGroup(host, components);
        }

        private static void ApplyComponentSettings(FrameworkElement view, BoardToolbarComponentEntry entry)
        {
            if (view == null || entry == null) return;

            var fixedWidth = entry.GetSettingDouble("fixedWidth");
            if (fixedWidth.HasValue && fixedWidth.Value > 0)
                view.Width = fixedWidth.Value;

            var fixedHeight = entry.GetSettingDouble("fixedHeight");
            if (fixedHeight.HasValue && fixedHeight.Value > 0)
                view.Height = fixedHeight.Value;

            var minWidth = entry.GetSettingDouble("minWidth");
            if (minWidth.HasValue && minWidth.Value > 0)
                view.MinWidth = minWidth.Value;

            var minHeight = entry.GetSettingDouble("minHeight");
            if (minHeight.HasValue && minHeight.Value > 0)
                view.MinHeight = minHeight.Value;

            var opacity = entry.GetSettingDouble("opacity");
            if (opacity.HasValue)
                view.Opacity = Math.Clamp(opacity.Value, 0, 1);

            // 插件自定义设置：通过 PluginToolbarItemInfo.ApplySettings 回调应用。
            var pluginItem = _pluginItems.FirstOrDefault(p => p.Id == entry.Id);
            if (pluginItem != null)
            {
                pluginItem.ApplySettings?.Invoke(view, entry.Settings);
            }
        }

        public static Border CreateGroupBorder(List<FrameworkElement> views, Orientation orientation = Orientation.Horizontal)
        {
            var panel = new StackPanel
            {
                Orientation = orientation,
                Margin = new Thickness(0)
            };

            foreach (var view in views)
            {
                panel.Children.Add(view);
            }

            var border = new Border
            {
                CornerRadius = new CornerRadius(5, 5, 5, 5),
                Background = (Brush)Application.Current.TryFindResource("FloatingBarBackgroundBrush")
                    ?? (Brush)Application.Current.TryFindResource("BoardFloatBarBackground"),
                Margin = new Thickness(0),
                Child = panel
            };

            return border;
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

        public static BoardToolbarLayoutSettings LoadConfigFile(string name)
        {
            var path = GetConfigFilePath(name);
            if (!File.Exists(path))
            {
                LogHelper.WriteLogToFile($"BoardToolbarRegistry: 配置文件不存在 [{path}]", LogHelper.LogType.Warning);
                return null;
            }

            try
            {
                var json = File.ReadAllText(path);
                var layout = JsonConvert.DeserializeObject<BoardToolbarLayoutSettings>(json);
                if (layout?.Areas == null || layout.Areas.Count == 0)
                {
                    LogHelper.WriteLogToFile($"BoardToolbarRegistry: 配置 [{name}] 内容为空或无效", LogHelper.LogType.Warning);
                    return null;
                }
                return layout;
            }
            catch (Exception ex)
            {
                // 把损坏文件改名隔离：避免下次启动再尝试读同样的坏数据把 fallback 也覆盖；
                // 给运维或用户一次"翻人工"恢复机会。
                try
                {
                    var brokenPath = path + ".broken_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    File.Move(path, brokenPath);
                    LogHelper.WriteLogToFile(
                        $"BoardToolbarRegistry: 加载配置 [{name}] 失败且被隔离为 [{brokenPath}]: {ex.Message}",
                        LogHelper.LogType.Error);
                }
                catch (Exception moveEx)
                {
                    LogHelper.WriteLogToFile($"BoardToolbarRegistry: 加载配置 [{name}] 失败: {ex.Message}", LogHelper.LogType.Error);
                    LogHelper.WriteLogToFile($"BoardToolbarRegistry: 隔离损坏配置 [{name}] 失败: {moveEx.Message}", LogHelper.LogType.Warning);
                }
                return null;
            }
        }

        public static void SaveConfigFile(string name, BoardToolbarLayoutSettings layout)
        {
            try
            {
                var dir = GetConfigDirectory();
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var path = GetConfigFilePath(name);
                var json = JsonConvert.SerializeObject(layout, Formatting.Indented);
                // 临时文件 + File.Replace/Move 原子替换，避免断电/进程被杀导致 default.json
                // 停在 0 字节或半截，下次启动 LoadConfigFile 反序列化失败→fallback CreateDefault，
                // 用户整套自定义布局静默丢失。
                var tmpPath = path + ".tmp";
                try
                {
                    File.WriteAllText(tmpPath, json);
                    if (File.Exists(path))
                        File.Replace(tmpPath, path, null);
                    else
                        File.Move(tmpPath, path);
                }
                catch (Exception innerEx)
                {
                    try { if (File.Exists(tmpPath)) File.Delete(tmpPath); } catch { }
                    throw new Exception($"原子写入失败: {innerEx.Message}", innerEx);
                }
                LogHelper.WriteLogToFile($"BoardToolbarRegistry: 保存配置 [{name}] 成功", LogHelper.LogType.Info);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"BoardToolbarRegistry: 保存配置 [{name}] 失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        public static List<string> ListConfigFiles()
        {
            try
            {
                var dir = GetConfigDirectory();
                if (!Directory.Exists(dir))
                    return new List<string> { "default" };

                var files = Directory.GetFiles(dir, "*.json");
                var names = new List<string>();
                foreach (var file in files)
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    if (!string.IsNullOrEmpty(name))
                        names.Add(name);
                }
                if (names.Count == 0)
                    names.Add("default");
                names.Sort();
                return names;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"BoardToolbarRegistry: 列出配置失败: {ex.Message}", LogHelper.LogType.Error);
                return new List<string> { "default" };
            }
        }

        public static void DeleteConfigFile(string name)
        {
            try
            {
                var path = GetConfigFilePath(name);
                if (File.Exists(path))
                    File.Delete(path);

                var bakPath = path + ".bak";
                if (File.Exists(bakPath))
                    File.Delete(bakPath);

                LogHelper.WriteLogToFile($"BoardToolbarRegistry: 删除配置 [{name}]", LogHelper.LogType.Info);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"BoardToolbarRegistry: 删除配置 [{name}] 失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private static volatile bool _defaultConfigEnsured;

        public static void EnsureDefaultConfigExists()
        {
            if (_defaultConfigEnsured) return;

            var dir = GetConfigDirectory();
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var defaultPath = GetConfigFilePath("default");
            if (!File.Exists(defaultPath))
            {
                SaveConfigFile("default", BoardToolbarLayoutSettings.CreateDefault());
                LogHelper.WriteLogToFile("BoardToolbarRegistry: 首次启动，创建 default.json", LogHelper.LogType.Info);
            }

            _defaultConfigEnsured = true;
        }

        public static BoardToolbarLayoutSettings LoadActiveConfig()
        {
            // 优先读 SettingsManager.Settings.BoardToolbarConfigName，让用户在 BoardToolbarPage
            // 切换配置时 MainWindow 工具栏跟随切换；缺失/损坏/未设置时回退到 "default"，
            // 再损坏则使用内置 CreateDefault——保证启动永远能加载出可用布局。
            var configName = SettingsManager.Settings?.BoardToolbarConfigName;
            if (string.IsNullOrWhiteSpace(configName)) configName = "default";

            var layout = LoadConfigFile(configName);
            if (layout != null && layout.Areas != null && layout.Areas.Count > 0)
                return layout;

            if (!string.Equals(configName, "default", StringComparison.OrdinalIgnoreCase))
            {
                layout = LoadConfigFile("default");
                if (layout != null && layout.Areas != null && layout.Areas.Count > 0)
                    return layout;
            }

            return BoardToolbarLayoutSettings.CreateDefault();
        }

        #endregion

        #region Rebuild methods

        public static void RebuildToolbar(IBoardToolbarHost host, Panel leftContainer, Panel centerContainer, Panel rightContainer)
        {
            var layout = LoadActiveConfig();
            RebuildToolbar(host, leftContainer, centerContainer, rightContainer, layout);
        }

        public static void RebuildToolbar(IBoardToolbarHost host, Panel leftContainer, Panel centerContainer, Panel rightContainer, BoardToolbarLayoutSettings layout)
        {
            if (layout == null)
                layout = BoardToolbarLayoutSettings.CreateDefault();

            foreach (var area in layout.Areas)
            {
                switch (area.Id.ToLower())
                {
                    case "left":
                        RebuildArea(host, leftContainer, area);
                        break;
                    case "center":
                        RebuildArea(host, centerContainer, area);
                        break;
                    case "right":
                        RebuildArea(host, rightContainer, area);
                        break;
                }
            }
        }

        private static void RebuildArea(IBoardToolbarHost host, Panel container, BoardToolbarAreaEntry area)
        {
            if (container == null) return;

            container.Children.Clear();

            bool isFirst = true;
            foreach (var group in area.Groups)
            {
                var views = BuildGroup(host, group.Components, area.Id);
                if (views.Count > 0)
                {
                    var groupBorder = CreateGroupBorder(views);
                    if (!isFirst)
                    {
                        groupBorder.Margin = new Thickness(3, 0, 0, 0);
                    }
                    container.Children.Add(groupBorder);
                    isFirst = false;
                }
            }
        }

        public static void RebuildLeftToolbar(IBoardToolbarHost host, Panel container) { }

        public static void RebuildCenterToolbar(IBoardToolbarHost host, Panel container) { }

        public static void RebuildRightToolbar(IBoardToolbarHost host, Panel container) { }

        #endregion
    }

    /// <summary>
    /// 将 <see cref="PluginToolbarItemInfo"/> 包装为 <see cref="IBoardToolbarItem"/>，
    /// 供 <see cref="BoardToolbarRegistry"/> 在构建白板工具栏时使用（与浮动栏 PluginToolbarItemWrapper 同构）。
    /// </summary>
    internal sealed class PluginBoardToolbarItemWrapper : IBoardToolbarItem
    {
        private readonly PluginToolbarItemInfo _info;

        public string Id => _info.Id;
        public string DisplayName => _info.DisplayName;
        public string Description => _info.Description;
        public string IconGeometry => _info.IconGeometry;
        public FontIconData? IconKey => null;
        public ButtonPosition DefaultPosition => ButtonPosition.Middle;

        public PluginBoardToolbarItemWrapper(PluginToolbarItemInfo info)
        {
            _info = info;
        }

        public FrameworkElement BuildView(IBoardToolbarHost host)
        {
            var view = _info.ViewFactory?.Invoke();
            if (view != null)
            {
                _info.ApplyOrientation?.Invoke(view, Orientation.Horizontal);
            }
            return view;
        }

        public void ApplyPosition(FrameworkElement view, ButtonPosition position)
        {
            if (view is BoardToolbarButton btn)
            {
                btn.Position = position;
            }
        }
    }
}
