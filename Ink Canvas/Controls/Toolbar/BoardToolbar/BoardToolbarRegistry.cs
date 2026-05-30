using Ink_Canvas.Controls;
using Ink_Canvas.Helpers;
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

        public static List<FrameworkElement> BuildGroup(IBoardToolbarHost host, List<BoardToolbarComponentEntry> components)
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
                    var view = item.BuildView(host);
                    if (view != null)
                    {
                        var position = ParseButtonPosition(entry.Position);
                        item.ApplyPosition(view, position);
                        ApplyComponentSettings(view, entry);
                        host.RegisterView(entry.Id, view);
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

        public static List<FrameworkElement> BuildGroup(IBoardToolbarHost host, params string[] ids)
        {
            var components = ids.Select(id => new BoardToolbarComponentEntry { Id = id }).ToList();
            return BuildGroup(host, components);
        }

        private static ButtonPosition ParseButtonPosition(string position)
        {
            return position?.ToLower() switch
            {
                "first" => ButtonPosition.First,
                "last" => ButtonPosition.Last,
                "single" => ButtonPosition.Single,
                _ => ButtonPosition.Middle
            };
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
                Margin = new Thickness(0, 0, 5, 0),
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

        public static void EnsureDefaultConfigExists()
        {
            var dir = GetConfigDirectory();
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var defaultPath = GetConfigFilePath("default");
            if (!File.Exists(defaultPath))
            {
                var layout = BoardToolbarLayoutSettings.CreateDefault();
                SaveConfigFile("default", layout);
                LogHelper.WriteLogToFile("BoardToolbarRegistry: 首次启动，创建 default.json", LogHelper.LogType.Info);
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

            foreach (var group in area.Groups)
            {
                var views = BuildGroup(host, group.Components);
                if (views.Count > 0)
                {
                    container.Children.Add(CreateGroupBorder(views));
                }
            }

            foreach (var component in area.Components)
            {
                var view = BuildView(component.Id, host);
                if (view != null)
                {
                    container.Children.Add(view);
                }
            }
        }

        public static void RebuildLeftToolbar(IBoardToolbarHost host, Panel container)
        {
            if (container == null) return;

            container.Children.Clear();

            var navigationViews = BuildGroup(host,
                "board.previousPage",
                "board.nextPage"
            );
            if (navigationViews.Count > 0)
            {
                container.Children.Add(CreateGroupBorder(navigationViews));
            }
        }

        public static void RebuildCenterToolbar(IBoardToolbarHost host, Panel container)
        {
            if (container == null) return;

            container.Children.Clear();

            var gestureViews = BuildGroup(host,
                "board.gesture",
                "board.backgroundColor"
            );
            if (gestureViews.Count > 0)
            {
                container.Children.Add(CreateGroupBorder(gestureViews));
            }

            var toolViews = BuildGroup(host,
                "board.select",
                "board.pen",
                "board.inkFreeze",
                "board.eraser",
                "board.strokeEraser",
                "board.shape",
                "board.insertImage",
                "board.undo",
                "board.redo"
            );
            if (toolViews.Count > 0)
            {
                container.Children.Add(CreateGroupBorder(toolViews));
            }

            var systemViews = BuildGroup(host,
                "board.tools",
                "board.exit"
            );
            if (systemViews.Count > 0)
            {
                container.Children.Add(CreateGroupBorder(systemViews));
            }
        }

        public static void RebuildRightToolbar(IBoardToolbarHost host, Panel container)
        {
            if (container == null) return;

            container.Children.Clear();

            var addPageView = BuildView("board.addNewPage", host);
            if (addPageView != null)
            {
                container.Children.Add(addPageView);
            }

            var navigationViews = BuildGroup(host,
                "board.previousPage",
                "board.nextPage"
            );
            if (navigationViews.Count > 0)
            {
                container.Children.Add(CreateGroupBorder(navigationViews));
            }
        }

        #endregion
    }
}
