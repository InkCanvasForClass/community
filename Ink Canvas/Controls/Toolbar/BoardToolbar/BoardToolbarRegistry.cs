using Ink_Canvas.Controls;
using Ink_Canvas.Helpers;
using Ink_Canvas.Properties;
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
            return _items;
        }

        public static IBoardToolbarItem FindItem(string id)
        {
            var items = Discover();
            return items.FirstOrDefault(i => i.Id == id);
        }

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

                if (entry.Id == "board.pageInfo")
                {
                    var pageInfoView = BuildPageInfoView(host, areaId);
                    if (pageInfoView != null)
                    {
                        views.Add(pageInfoView);
                    }
                    continue;
                }

                if (!itemMap.TryGetValue(entry.Id, out var item))
                {
                    LogHelper.WriteLogToFile($"BoardToolbarRegistry: 未找到组件 [{entry.Id}]", LogHelper.LogType.Warning);
                    continue;
                }

                try
                {
                    var view = item.BuildView(host);
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

        private static FrameworkElement BuildPageInfoView(IBoardToolbarHost host, string areaId)
        {
            var pageInfoTextBlock = new TextBlock
            {
                Text = "1/1",
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, -1, 0, 0),
                FontSize = 17,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center
            };
            host.RegisterView($"board.pageInfo.{areaId}", pageInfoTextBlock);

            var pageLabel = new TextBlock
            {
                Text = FloatingBarStrings.Board_Page,
                Foreground = (Brush)Application.Current.TryFindResource("FloatBarForeground"),
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Center,
                FontSize = 12
            };

            var grid = new Grid { Margin = new Thickness(6, 6, 6, 4) };
            grid.Children.Add(pageInfoTextBlock);
            grid.Children.Add(pageLabel);

            var pageInfoBorder = new Border
            {
                Width = 75,
                Height = 50,
                BorderThickness = new Thickness(0),
                Background = (Brush)Application.Current.TryFindResource("BoardFloatBarBackground"),
                Opacity = 1,
                Child = grid,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            host.RegisterView($"board.pageList.{areaId}Btn", pageInfoBorder);
            return pageInfoBorder;
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
                Background = (Brush)Application.Current.TryFindResource("BoardFloatBarBackground"),
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
                LogHelper.WriteLogToFile($"BoardToolbarRegistry: 加载配置 [{name}] 失败: {ex.Message}", LogHelper.LogType.Error);
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
                File.WriteAllText(path, json);
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

        public static void EnsureDefaultConfigExists()
        {
            var dir = GetConfigDirectory();
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var defaultPath = GetConfigFilePath("default");
            var layout = BoardToolbarLayoutSettings.CreateDefault();

            if (!File.Exists(defaultPath))
            {
                SaveConfigFile("default", layout);
                LogHelper.WriteLogToFile("BoardToolbarRegistry: 首次启动，创建 default.json", LogHelper.LogType.Info);
                return;
            }

            try
            {
                var existing = LoadConfigFile("default");
                if (existing == null || existing.Areas == null)
                {
                    SaveConfigFile("default", layout);
                    LogHelper.WriteLogToFile("BoardToolbarRegistry: 配置无效，重建 default.json", LogHelper.LogType.Info);
                    return;
                }

                var defaultIds = new HashSet<string>();
                foreach (var area in layout.Areas)
                {
                    foreach (var group in area.Groups)
                    {
                        foreach (var comp in group.Components)
                            defaultIds.Add($"{area.Id}:{comp.Id}");
                    }
                }

                var existingIds = new HashSet<string>();
                foreach (var area in existing.Areas)
                {
                    foreach (var group in area.Groups)
                    {
                        foreach (var comp in group.Components)
                            existingIds.Add($"{area.Id}:{comp.Id}");
                    }
                }

                if (!defaultIds.SetEquals(existingIds))
                {
                    SaveConfigFile("default", layout);
                    LogHelper.WriteLogToFile("BoardToolbarRegistry: 检测到组件变更，更新 default.json", LogHelper.LogType.Info);
                }
            }
            catch (Exception ex)
            {
                SaveConfigFile("default", layout);
                LogHelper.WriteLogToFile($"BoardToolbarRegistry: 配置校验失败，重建 default.json: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        public static BoardToolbarLayoutSettings LoadActiveConfig()
        {
            var layout = LoadConfigFile("default");
            if (layout != null && layout.Areas != null && layout.Areas.Count > 0)
                return layout;

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

            foreach (var component in area.Components)
            {
                var view = BuildView(component.Id, host);
                if (view != null)
                {
                    ApplyComponentSettings(view, component);

                    FrameworkElement elementToAdd;
                    if (component.ShowSeparateBorder)
                    {
                        elementToAdd = WrapInSeparateBorder(view);
                    }
                    else
                    {
                        elementToAdd = view;
                    }

                    if (!isFirst)
                    {
                        elementToAdd.Margin = new Thickness(3, 0, 0, 0);
                    }
                    container.Children.Add(elementToAdd);
                    isFirst = false;
                }
            }
        }

        private static Border WrapInSeparateBorder(FrameworkElement view)
        {
            var border = new Border
            {
                CornerRadius = new CornerRadius(5),
                Background = (Brush)Application.Current.TryFindResource("BoardFloatBarBackground"),
                Margin = new Thickness(0),
                Child = view
            };
            return border;
        }

        public static void RebuildLeftToolbar(IBoardToolbarHost host, Panel container) { }

        public static void RebuildCenterToolbar(IBoardToolbarHost host, Panel container) { }

        public static void RebuildRightToolbar(IBoardToolbarHost host, Panel container) { }

        #endregion
    }
}
