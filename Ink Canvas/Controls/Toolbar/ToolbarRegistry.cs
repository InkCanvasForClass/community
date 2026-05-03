using Ink_Canvas.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas.Controls.Toolbar
{
    public static class ToolbarRegistry
    {
        private static List<IToolbarItem> _items;
        internal const string InjectedTag = "ToolbarRegistryInjected";

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

        public static void Populate(IToolbarHost host, IDictionary<ToolbarSlot, Panel> slots, ToolbarLayoutSettings layout)
        {
            LogHelper.WriteLogToFile($"ToolbarRegistry: Populate 开始", LogHelper.LogType.Info);
            if (host == null || slots == null) { LogHelper.WriteLogToFile("ToolbarRegistry: Populate host 或 slots 为空", LogHelper.LogType.Warning); return; }
            layout = layout ?? new ToolbarLayoutSettings();
            MigrateLegacyDefaultLayout(layout);

            var grouped = new Dictionary<ToolbarSlot, List<(IToolbarItem item, ToolbarItemConfig cfg)>>();
            foreach (var item in Discover())
            {
                if (!layout.Items.TryGetValue(item.Id, out var cfg))
                {
                    cfg = new ToolbarItemConfig
                    {
                        Visible = item.DefaultVisible,
                        Order = item.DefaultOrder,
                        Slot = item.DefaultSlot,
                        Position = item.DefaultPosition,
                        AnchorName = item.DefaultAnchorName
                    };
                }
                if (!cfg.Visible) continue;
                if (!grouped.TryGetValue(cfg.Slot, out var list))
                {
                    list = new List<(IToolbarItem, ToolbarItemConfig)>();
                    grouped[cfg.Slot] = list;
                }
                list.Add((item, cfg));
            }
            LogHelper.WriteLogToFile($"ToolbarRegistry: 分组完成, {grouped.Count} 个 slot 有可见条目", LogHelper.LogType.Info);

            foreach (var kv in grouped)
            {
                if (!slots.TryGetValue(kv.Key, out var container) || container == null) continue;
                LogHelper.WriteLogToFile($"ToolbarRegistry: 注入到 {kv.Key}, 条目数={kv.Value.Count}", LogHelper.LogType.Info);
                InjectIntoContainer(host, container, kv.Value);
            }

            ApplyMenuVisibility(host, layout);
            LogHelper.WriteLogToFile($"ToolbarRegistry: Populate 完成", LogHelper.LogType.Info);
        }

        private static void MigrateLegacyDefaultLayout(ToolbarLayoutSettings layout)
        {
            if (layout?.Items == null) return;
            if (!layout.Items.TryGetValue("builtin.inkFreeze", out var cfg) || cfg == null) return;

            if (cfg.Slot == ToolbarSlot.FloatingBarEnd
                && cfg.Position == ToolbarInsertPosition.AfterAnchor
                && cfg.AnchorName == "FloatingBarEndSeparator"
                && cfg.Order == 130)
            {
                cfg.Slot = ToolbarSlot.FloatingBarMain;
                cfg.Order = 120;
                cfg.AnchorName = "QuickColorPaletteSingleRowPanel";
            }
        }

        public static void ApplyMenuVisibility(IToolbarHost host, ToolbarLayoutSettings layout)
        {
            if (host == null || layout == null) return;
            foreach (var item in Discover())
            {
                if (string.IsNullOrEmpty(item.MenuPanelName)) continue;
                bool visible = true;
                if (layout.Items.TryGetValue(item.Id, out var cfg))
                    visible = cfg.Visible;
                try
                {
                    var menuElement = host.Window.FindName(item.MenuPanelName);
                    if (menuElement is System.Windows.Controls.Primitives.Popup popup)
                    {
                        popup.IsOpen = visible;
                        LogHelper.WriteLogToFile($"ToolbarRegistry: 菜单 Popup [{item.MenuPanelName}] -> {(visible ? "Open" : "Closed")}", LogHelper.LogType.Info);
                    }
                    else if (menuElement is FrameworkElement fe)
                    {
                        fe.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
                        LogHelper.WriteLogToFile($"ToolbarRegistry: 菜单 [{item.MenuPanelName}] -> {(visible ? "Visible" : "Collapsed")}", LogHelper.LogType.Info);
                    }
                    else
                    {
                        LogHelper.WriteLogToFile($"ToolbarRegistry: 找不到菜单面板 [{item.MenuPanelName}]", LogHelper.LogType.Warning);
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"ToolbarRegistry: 设置菜单可见性异常 [{item.MenuPanelName}]: {ex.Message}", LogHelper.LogType.Warning);
                }
            }
        }

        private static void InjectIntoContainer(IToolbarHost host, Panel container,
            List<(IToolbarItem item, ToolbarItemConfig cfg)> entries)
        {
            var prepend = entries.Where(e => e.cfg.Position == ToolbarInsertPosition.Prepend).OrderBy(e => e.cfg.Order).ToList();
            var append = entries.Where(e => e.cfg.Position == ToolbarInsertPosition.Append).OrderBy(e => e.cfg.Order).ToList();
            var before = entries.Where(e => e.cfg.Position == ToolbarInsertPosition.BeforeAnchor).ToList();
            var after = entries.Where(e => e.cfg.Position == ToolbarInsertPosition.AfterAnchor).ToList();

            var prependIndex = 0;
            foreach (var entry in prepend)
            {
                var view = BuildAndRegister(host, entry.item);
                if (view == null) continue;
                container.Children.Insert(prependIndex++, view);
            }

            foreach (var entry in append)
            {
                var view = BuildAndRegister(host, entry.item);
                if (view == null) continue;
                container.Children.Add(view);
            }

            foreach (var group in before.GroupBy(e => e.cfg.AnchorName))
            {
                var anchor = FindNamedChild(container, group.Key);
                if (anchor == null)
                {
                    LogHelper.WriteLogToFile($"ToolbarRegistry: 未找到锚点 '{group.Key}' (BeforeAnchor)", LogHelper.LogType.Warning);
                    continue;
                }
                var idx = container.Children.IndexOf(anchor);
                foreach (var entry in group.OrderBy(e => e.cfg.Order))
                {
                    var view = BuildAndRegister(host, entry.item);
                    if (view == null) continue;
                    container.Children.Insert(idx++, view);
                }
            }

            foreach (var group in after.GroupBy(e => e.cfg.AnchorName))
            {
                var anchor = FindNamedChild(container, group.Key);
                if (anchor == null)
                {
                    LogHelper.WriteLogToFile($"ToolbarRegistry: 未找到锚点 '{group.Key}' (AfterAnchor)", LogHelper.LogType.Warning);
                    continue;
                }
                var idx = container.Children.IndexOf(anchor) + 1;
                foreach (var entry in group.OrderBy(e => e.cfg.Order))
                {
                    var view = BuildAndRegister(host, entry.item);
                    if (view == null) continue;
                    container.Children.Insert(idx++, view);
                }
            }
        }

        private static UIElement FindNamedChild(Panel container, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (UIElement child in container.Children)
            {
                if (child is FrameworkElement fe && fe.Name == name) return child;
            }
            return null;
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
    }
}