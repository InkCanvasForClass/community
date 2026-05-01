using Ink_Canvas.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas.Controls.Toolbar
{
    /// <summary>
    /// 扫描当前程序集里的 IToolbarItem 实现，按用户配置（Settings.Toolbar）排序/过滤后注入到目标容器。
    /// </summary>
    public static class ToolbarRegistry
    {
        private static List<IToolbarItem> _items;

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
            return _items;
        }

        /// <summary>按 slot 分配工具栏条目到对应容器。调用者负责清空目标容器里要被接管的旧内容。</summary>
        public static void Populate(IToolbarHost host, IDictionary<ToolbarSlot, Panel> slots, ToolbarLayoutSettings layout)
        {
            if (host == null || slots == null) return;
            layout = layout ?? new ToolbarLayoutSettings();

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

            foreach (var kv in grouped)
            {
                if (!slots.TryGetValue(kv.Key, out var container) || container == null) continue;
                InjectIntoContainer(host, container, kv.Value);
            }
        }

        private static void InjectIntoContainer(IToolbarHost host, Panel container,
            List<(IToolbarItem item, ToolbarItemConfig cfg)> entries)
        {
            // 按 Position 分桶，每桶内按 Order 升序。
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
                LogHelper.WriteLogToFile($"ToolbarRegistry: 构建 {item.Id} 失败: {ex.Message}", LogHelper.LogType.Warning);
                return null;
            }
        }
    }
}