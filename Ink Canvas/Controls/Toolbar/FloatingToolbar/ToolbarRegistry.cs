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

        /// <summary>根面板的排列方向，<see cref="Populate"/> 时记录。</summary>
        private static Orientation _rootOrientation = Orientation.Horizontal;

        /// <summary>
        /// 新组件滑入的方向：-1 从头一侧（负方向）滑入，+1 从尾一侧（正方向）滑入。
        /// </summary>
        /// <remarks>
        /// 拖动头在哪一端，组件就该从哪一端被"推出来"：头在左时从左侧滑入（-1），
        /// 头在右时从右侧滑入（+1）；竖排同理（头在上 -1、头在下 +1）。
        /// Inkeys 的做法是让新按钮从锚点（相邻可见按钮）的位置长出来——
        /// 锚点一侧永远是内容生长的那一端，这里用符号表达同一件事。
        ///
        /// 早先靠主窗口在翻转时回调设置符号，但头的实际位置还会被折叠、自动换边、
        /// 内容翻转等多条路径改动，外部逐点同步必然漏（表现为"第一次对、第二次反"）。
        /// 现在每次动效前就地从布局推断，见 <see cref="RefreshEnterSlideSign"/>。
        /// </remarks>
        private static int _enterSlideSign = -1;

        /// <summary>
        /// 当前这一轮显隐统一使用的滑入符号（null 表示本轮尚未开始）。
        /// </summary>
        /// <remarks>
        /// 一轮显隐会遍历整棵工具栏，期间布局不断变化：先显形的元素和后面显形的元素，
        /// 量到的"头在哪端"可能不同。若每次都重新判定，同一轮里方向会当场翻转，
        /// 看起来就是组件先往左滑、紧接着又往右滑。
        /// 因此每轮开头判定一次并冻结，整轮沿用。
        /// </remarks>
        private static int? _slideSignFrozenForRound;

        /// <summary>
        /// 下一次 <see cref="UpdateVisibilityByMode"/> 是否跳过动画（重建工具栏后第一次同步用）。
        /// </summary>
        /// <remarks>
        /// 重建时所有组件都先按"可见"建出来，真实显隐要等紧接着的第一次同步才纠正。
        /// 如果那次同步带动画，用户会看到启动瞬间"图标冒出来又缩回去"。
        /// 首轮直接瞬时落定，之后的变化才值得有动画。
        /// </remarks>
        private static bool _skipAnimationOnNextSync;

        /// <summary>整段胶囊正在"从无到有"地长出来时置位，用于抑制段内按钮各自的出场动画。</summary>
        /// <remarks>整段长出来就是那一次的出场；再让每个按钮各滑一遍，会是两层动作叠在一起。</remarks>
        private static bool _suppressChildEnterAnimation;

        /// <summary>
        /// 这个面板是不是竖排（决定组件显隐时沿哪个轴让位、沿哪个轴滑入）。
        /// </summary>
        /// <remarks>
        /// 根面板在 XAML 里是 ikw:SimpleStackPanel，不保证是 StackPanel 的派生类。
        /// 认不出来就退回 <see cref="Populate"/> 记下的朝向，
        /// 免得竖排工具栏悄悄退化成横排动画。
        /// </remarks>
        private static bool IsVerticalPanel(Panel panel)
        {
            if (panel is StackPanel stackPanel)
            {
                return stackPanel.Orientation == Orientation.Vertical;
            }

            return _rootOrientation == Orientation.Vertical;
        }

        /// <summary>设置组件显隐是否播放过渡动画（对应「禁止工具栏动画」的取反）。</summary>
        public static void SetVisibilityAnimationEnabled(bool enabled)
        {
            _animateVisibilityChange = enabled;
        }

        /// <summary>
        /// 立刻把所有分段胶囊的跨度补间收尾，让布局回到"内容应有的稳定尺寸"。
        /// </summary>
        /// <remarks>
        /// 居中、换边、折叠这些操作都要量工具栏宽度，而补间期间量到的是**中间值**。
        /// 动画倍率 0.1x 时一次补间长达 2.2 秒，这段时间里量的宽度可以差出几十像素，
        /// 居中位置自然就偏了——"居中模式下居中异常"就是这么来的。
        /// 需要在量宽度前先调用本方法，把跨度立刻落到终值再量。
        /// </remarks>
        public static void SettleExtentAnimations(Panel rootPanel)
        {
            if (rootPanel == null) return;

            bool vertical = IsVerticalPanel(rootPanel);
            foreach (var child in rootPanel.Children.OfType<FrameworkElement>())
            {
                if (!(child is Border border) || border.Tag as string != ContentBorderTag) continue;
                SettleSegment(border, vertical);
            }

            NotifyExtentChanged(vertical);
        }

        /// <summary>
        /// 工具栏沿排布方向的实际跨度变化时回调（居中、贴边等跟随者据此重新落位）。
        /// </summary>
        /// <remarks>
        /// 背景宽度补间期间，工具栏的可见跨度每一帧都在变；居中若只在补间开始前
        /// 算一次，补间走完就会停在偏掉的位置。这里在补间结束时再通知一次，
        /// 跟随者重新量、重新落位，"背景变宽"与"整体居中"才对得上。
        /// 回调只在补间收尾时发一次，不是每帧都发——每帧重算居中会打断用户拖动。
        /// </remarks>
        public static event Action ExtentSettled;

        private static void NotifyExtentChanged(bool vertical)
        {
            try
            {
                ExtentSettled?.Invoke();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"ToolbarRegistry: 跨度变化回调失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        /// <summary>把一个分段的跨度动画立即收尾：清动画、清固定值，回到内容自动尺寸。</summary>
        /// <remarks>
        /// 只清**被补间的那一个轴**。横排分段本身带着 Height = 58（写在 CreateContentBorder 里），
        /// 竖排分段带着 Width = 58；把两个轴一起清掉会连固定的厚度也一起丢掉，
        /// 分段高度塌成内容高度、胶囊外观随之崩掉。这是早先版本的实打实的缺陷。
        /// </remarks>
        private static void SettleSegment(Border border, bool vertical)
        {
            if (border == null) return;
            NextSegmentGeneration(border);
            _activeSegmentTweens.Remove(border);

            DependencyProperty extentProperty = vertical
                ? FrameworkElement.HeightProperty
                : FrameworkElement.WidthProperty;
            border.BeginAnimation(extentProperty, null);
            border.ClearValue(extentProperty);

            border.BeginAnimation(UIElement.OpacityProperty, null);
            border.Opacity = 1;
            border.ClipToBounds = false;
        }

        /// <summary>
        /// 按根面板的子项排列顺序推断滑入方向：拖动头排在内容之前则为 -1，排在其后则为 +1。
        /// </summary>
        /// <remarks>
        /// 只看**顺序**，不量坐标。两个原因：
        ///
        /// 一、几何判据在这套布局里是退化的。根面板是自适应内容尺寸的 StackPanel，
        /// 它的跨度就等于内容跨度，"内容重心 vs 面板中点"两边同时移动，
        /// 比较结果只剩浮点噪声——这正是"第一次对、第二次反"的直接来源。
        ///
        /// 二、几何判据会被动画污染。TransformToAncestor 包含 RenderTransform，
        /// 而动画倍率 0.1x 时一次出场要 2.2 秒，第二次触发几乎必然落在第一次动画中途，
        /// 量到的位置带着在飞的位移，判据自然翻转。
        ///
        /// 排列顺序既是 StackPanel 排布的唯一依据，又完全不受变换影响，
        /// 换边、折叠、内容翻转之后都仍然成立。
        /// </remarks>
        private static void RefreshEnterSlideSign(Panel panel)
        {
            if (panel == null) return;

            var head = FindDragHandle(panel);
            if (head == null) return;

            bool vertical = IsVerticalPanel(panel);

            // 量"拖动头"与"内容整体"在排布方向上的真实位置。
            // 之前用子项索引判断，但主窗口在换边/折叠时会 rootChildren.Clear() 再重排，
            // 同一根面板上头会从索引 0 跳到索引 7（日志里两种取值都出现过），
            // 索引根本不能代表"头在哪一端"，位置才是排布的真实结果。
            double headPos;
            try
            {
                var offset = head.TransformToAncestor(panel).Transform(new Point(0, 0));
                headPos = vertical ? offset.Y : offset.X;
            }
            catch
            {
                return;
            }

            double contentMin = double.MaxValue;
            double contentMax = double.MinValue;
            CollectContentExtent(panel, panel, vertical, ref contentMin, ref contentMax);
            if (contentMin > contentMax) return;

            // 头贴着内容前沿（左/上）→ 内容向另一侧生长 → 从负方向滑入
            // 头贴着内容尾沿（右/下）→ 内容向前导侧生长 → 从正方向滑入
            double distToStart = Math.Abs(headPos - contentMin);
            double distToEnd = Math.Abs(headPos - contentMax);
            _enterSlideSign = distToStart <= distToEnd ? -1 : 1;

            // 记一行日志：方向出问题时可以直接对照，不必再靠猜
            LogHelper.WriteLogToFile(
                $"[Toolbar] 滑入方向 = {_enterSlideSign}（头={headPos:F1} 内容 {contentMin:F1}..{contentMax:F1} " +
                $"到前沿={distToStart:F1} 到后沿={distToEnd:F1}）",
                LogHelper.LogType.Trace);
        }

        /// <summary>测量可见内容在排布方向上占据的区间。</summary>
        /// <remarks>
        /// 用"内容占用的两端"而不是"子项索引"：索引会被重排打乱，位置不会。
        /// 也不用"重心 vs 面板中点"——根面板自适应内容宽度，那个比较恒等于噪声。
        /// </remarks>
        private static void CollectContentExtent(
            Panel root, FrameworkElement current, bool vertical, ref double min, ref double max)
        {
            if (current == null || current.Visibility != Visibility.Visible) return;

            bool isContent = current.Tag as string == InjectedTag || IsContentBorder(current);
            if (isContent && current != root)
            {
                try
                {
                    var t = current.TransformToAncestor(root);
                    var p0 = t.Transform(new Point(0, 0));
                    var p1 = t.Transform(new Point(
                        vertical ? 0 : current.ActualWidth,
                        vertical ? current.ActualHeight : 0));
                    double a = vertical ? p0.Y : p0.X;
                    double b = vertical ? p1.Y : p1.X;
                    if (a > b) { var tmp = a; a = b; b = tmp; }
                    if (a < min) min = a;
                    if (b > max) max = b;
                }
                catch
                {
                    // 尚未进入视觉树
                }
            }

            if (current is Panel pnl)
            {
                foreach (var child in pnl.Children.OfType<FrameworkElement>())
                {
                    CollectContentExtent(root, child, vertical, ref min, ref max);
                }
            }
            else if (current is Border br && br.Child is FrameworkElement bc)
            {
                CollectContentExtent(root, bc, vertical, ref min, ref max);
            }
            else if (current is ContentControl cc && cc.Content is FrameworkElement cce)
            {
                CollectContentExtent(root, cce, vertical, ref min, ref max);
            }
        }

        /// <summary>供主窗口在重建、换边、复位后主动刷新一次滑入方向。</summary>
        public static void RefreshEnterSlideDirection(Panel rootPanel)
        {
            RefreshEnterSlideSign(rootPanel);
        }

        /// <summary>这个子项是不是分段胶囊（内容分段的 Border 或独立分段的 Border）。</summary>
        private static bool IsContentBorder(object child)
        {
            return child is Border border && border.Tag as string == ContentBorderTag;
        }

        /// <summary>
        /// 找根面板里的拖动头：既不是分段胶囊，也不是选择框画布。
        /// </summary>
        /// <remarks>
        /// 不按名字或类型硬认：选择框画布没有标签、也不是 Border，
        /// 这里按"排除已知成员"的写法把它跳过去，剩下的第一个就是拖动头。
        /// </remarks>
        private static FrameworkElement FindDragHandle(Panel panel)
        {
            foreach (var child in panel.Children.OfType<FrameworkElement>())
            {
                if (IsContentBorder(child)) continue;
                if (child is System.Windows.Controls.Canvas) continue;
                if (child.Tag as string == InjectedTag) continue;
                return child;
            }
            return null;
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

        public static bool RegisterPluginItem(PluginToolbarItemInfo itemInfo, bool autoAddToActiveConfig = true)
        {
            if (itemInfo == null || string.IsNullOrEmpty(itemInfo.Id)) return false;
            if (_pluginItems.Any(item => string.Equals(item.Id, itemInfo.Id, StringComparison.OrdinalIgnoreCase))) return false;

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
            return true;
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
            if (_items != null)
            {
                removed |= _items.RemoveAll(item => item is PluginToolbarItemWrapper
                                                    && string.Equals(item.Id, itemId, StringComparison.OrdinalIgnoreCase)) > 0;
            }

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
                // 关闭也必须保留键，否则下次加载清空组件时会被当作未迁移的旧配置。
                entry.SetSetting(ComponentSettingKeys.RedStyleIcon, legacy);
                entry.SetSetting(ComponentSettingKeys.RedStyleText, legacy);
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

            // 记下朝向：根面板未必是 StackPanel 的派生类，拿不到 Orientation 时靠它判断动画轴
            _rootOrientation = orientation;

            layout = layout ?? CreateDefaultLayout();
            if (layout.Components == null || layout.Components.Count == 0)
            {
                layout = CreateDefaultLayout();
            }

            var discovered = Discover();
            var itemMap = discovered.ToDictionary(i => i.Id, i => i);

            ClearInjected(rootPanel);

            // 下面所有组件都按"可见"建出来，真实显隐要等紧接着的第一次同步才纠正。
            // 那次同步必须瞬时：否则启动瞬间会看到图标冒出来又缩回去。
            _skipAnimationOnNextSync = true;

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

            LogHelper.WriteLogToFile(
                $"[Toolbar] 浮动栏已构建: 条目 {displayItems.Count} 个, 分段 {segments.Count} 个, 组件库 {discovered.Count} 个",
                LogHelper.LogType.Info);
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

            // 重建后的第一次同步：组件是按"全可见"建出来的，这次纠正必须瞬时落定，
            // 否则启动瞬间会看到图标冒出来又缩回去。
            bool animateThisRound = _animateVisibilityChange && !_skipAnimationOnNextSync;
            _skipAnimationOnNextSync = false;

            // 方向每轮只判定**一次**，之后整轮沿用同一个符号。
            // 之前这里判一次、SetAnimatedVisibility 内部又量一次，两次之间布局已随显隐改变，
            // 于是同一轮里前半段用 -1、后半段用 +1——肉眼就是"先往左、再往右"。
            RefreshEnterSlideSign(rootPanel);
            _slideSignFrozenForRound = _enterSlideSign;

            // 记录切换前各子项的位置：整段显隐后由"邻居让位"通道把它们平滑滑到新位置。
            var beforeOffsets = animateThisRound
                ? CaptureChildOffsets(rootPanel)
                : null;

            bool animationBefore = _animateVisibilityChange;
            _animateVisibilityChange = animateThisRound;
            try
            {
                UpdatePanelVisibility(rootPanel, context);
            }
            finally
            {
                _animateVisibilityChange = animationBefore;
            }

            // 本轮结束，下一轮重新判定
            _slideSignFrozenForRound = null;

            if (beforeOffsets != null)
            {
                AnimateChildrenToNewOffsets(rootPanel, beforeOffsets);
            }
        }

        /// <summary>
        /// 记录参与排布的元素当前相对面板左上角的位置。
        /// </summary>
        /// <remarks>
        /// 用于"某个组件显隐后，它后面的组件平滑滑到新位置"。
        /// 记的是完整坐标而不是单个数：横排工具栏的兄弟只沿 X 移动、竖排只沿 Y 移动，
        /// 具体取哪个分量由调用方按面板朝向决定。
        /// </remarks>
        private static Dictionary<FrameworkElement, Point> CaptureChildOffsets(Panel panel)
        {
            var result = new Dictionary<FrameworkElement, Point>();
            if (panel == null) return result;

            // 组件是三层嵌套：ContentBorder(Border) → ContentPanel(StackPanel) → 按钮(InjectedTag)。
            // 只取直接子项会漏掉真正会移动的按钮，必须递归收集。
            CollectVisibleLayoutElements(panel, panel, result);

            // 根面板的直接子项（拖动头、分段胶囊）也要记：整段显隐时它们的位置会瞬跳，
            // 让位补偿必须覆盖到，否则拖动头会"啪"地横移。
            foreach (var child in panel.Children.OfType<FrameworkElement>())
            {
                if (child.Visibility != Visibility.Visible || result.ContainsKey(child)) continue;
                try
                {
                    result[child] = child.TransformToAncestor(panel).Transform(new Point(0, 0));
                }
                catch
                {
                    // 尚未进入视觉树
                }
            }
            return result;
        }

        /// <summary>
        /// 递归收集参与排布的元素，记录它们相对根面板的位置。
        /// </summary>
        /// <remarks>
        /// 两个分量都记：这里还不知道面板朝哪边排，先量全，取哪个分量交给调用方。
        /// </remarks>
        private static void CollectVisibleLayoutElements(
            Panel root, FrameworkElement current, Dictionary<FrameworkElement, Point> sink)
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
                        sink[current] = point;
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
        ///
        /// 横排工具栏沿 X 让位、竖排沿 Y 让位：捕获时两个分量都记了，
        /// 这里按面板朝向只取对应的那一个。另一个分量本来就不该变，参与计算只会引入噪声。
        /// </remarks>
        private static void AnimateChildrenToNewOffsets(
            Panel panel, Dictionary<FrameworkElement, Point> beforeOffsets)
        {
            if (panel == null || beforeOffsets == null || beforeOffsets.Count == 0) return;

            bool vertical = IsVerticalPanel(panel);
            DependencyProperty axisProperty = vertical
                ? TranslateTransform.YProperty
                : TranslateTransform.XProperty;

            panel.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    foreach (var kvp in beforeOffsets)
                    {
                        var child = kvp.Key;
                        if (child == null || child.Visibility != Visibility.Visible) continue;

                        Point newOffset;
                        try
                        {
                            newOffset = child.TransformToAncestor(panel).Transform(new Point(0, 0));
                        }
                        catch
                        {
                            continue;
                        }

                        // 横排取 X、竖排取 Y，两者只有一个会真的变
                        double delta = vertical
                            ? kvp.Value.Y - newOffset.Y
                            : kvp.Value.X - newOffset.X;
                        if (Math.Abs(delta) < 1.0) continue;

                        // 用独立的"邻居让位"通道，不与出场滑入抢同一个 TranslateTransform
                        var translate = GetOrCreateChannel(child, SiblingChannelIndex);

                        var animation = new System.Windows.Media.Animation.DoubleAnimation
                        {
                            From = delta,
                            To = 0,
                            Duration = AnimationSpeed.ScaleMilliseconds(ToolbarSiblingSlideMs),
                            EasingFunction = new System.Windows.Media.Animation.CubicEase
                            {
                                EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
                            }
                        };

                        animation.Completed += (_, _) =>
                        {
                            translate.BeginAnimation(axisProperty, null);
                            translate.X = 0;
                            translate.Y = 0;
                        };

                        translate.BeginAnimation(axisProperty, animation);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ToolbarRegistry] 邻居滑动动画失败: {ex.Message}");
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>邻居组件滑到新位置的时长（毫秒），与按钮出场同长。</summary>
        /// <remarks>
        /// 新组件出现时，右边所有组件要向右让出"它的宽度"，
        /// 而新组件自己也要从 -宽度 滑到 0——两者走的是同一段距离，
        /// 时长必须一致，否则看起来是一前一后收尾。
        /// </remarks>
        private const int ToolbarSiblingSlideMs = ToolbarItemShowDurationMs;



        /// <summary>出场缩放使用的通道。</summary>
        private const int ScaleChannelIndex = 0;

        /// <summary>出场滑入使用的位移通道。</summary>
        private const int EnterChannelIndex = 1;

        /// <summary>邻居让位使用的位移通道。</summary>
        private const int SiblingChannelIndex = 2;

        /// <summary>
        /// 取得（必要时重建）组件的三条动画通道。
        /// </summary>
        /// <remarks>
        /// 三条通道必须各自独立：
        ///   [0] 出场缩放    [1] 出场滑入    [2] 邻居让位
        /// 它们会同时发生（新组件滑入并缩放，同时后面的按钮让位），
        /// 共用一个变换会互相覆盖，只剩最后写进去的那条生效。
        ///
        /// 缩放排在位移之前是有意的：变换按顺序依次作用，
        /// 缩放若排在位移之后，平移量会被一起缩放，量出来的位置就对不上了。
        ///
        /// 重建时沿用已有通道上的值，避免抹掉正在播的动画位置。
        /// </remarks>
        private static TransformGroup EnsureChannelGroup(FrameworkElement element)
        {
            if (element.RenderTransform is TransformGroup group
                && group.Children.Count > SiblingChannelIndex
                && group.Children[ScaleChannelIndex] is ScaleTransform
                && group.Children[EnterChannelIndex] is TranslateTransform
                && group.Children[SiblingChannelIndex] is TranslateTransform)
            {
                return group;
            }

            // 沿用旧通道上的值：按类型扫一遍，不依赖下标，兼容上一版只有两条位移通道的组
            double enterX = 0, siblingX = 0, scale = 1;
            bool enterSeen = false, siblingSeen = false;
            if (element.RenderTransform is TransformGroup old)
            {
                foreach (var child in old.Children)
                {
                    if (child is TranslateTransform translate)
                    {
                        if (!enterSeen) { enterX = translate.X; enterSeen = true; }
                        else if (!siblingSeen) { siblingX = translate.X; siblingSeen = true; }
                    }
                    else if (child is ScaleTransform existingScale)
                    {
                        scale = existingScale.ScaleX;
                    }
                }
            }

            var rebuilt = new TransformGroup();
            rebuilt.Children.Add(new ScaleTransform(scale, scale));
            rebuilt.Children.Add(new TranslateTransform(enterX, 0));
            rebuilt.Children.Add(new TranslateTransform(siblingX, 0));
            element.RenderTransform = rebuilt;

            // 缩放绕自身中心，否则组件会从左上角"胀"出来而不是原地长大
            element.RenderTransformOrigin = new Point(0.5, 0.5);

            return rebuilt;
        }

        private static TranslateTransform GetOrCreateChannel(FrameworkElement element, int channelIndex)
        {
            return (TranslateTransform)EnsureChannelGroup(element).Children[channelIndex];
        }

        private static ScaleTransform GetOrCreateScaleTransform(FrameworkElement element)
        {
            return (ScaleTransform)EnsureChannelGroup(element).Children[ScaleChannelIndex];
        }

        /// <summary>
        /// 分段胶囊（ContentBorder）的跨度随内容显隐平滑补间：背景跟着内容实时变宽/变窄。
        /// </summary>
        /// <remarks>
        /// 之前补间的是根面板的跨度——但根面板没有背景、分段按内容尺寸排布，
        /// 那条补间在视觉上完全不可见，肉眼看到的仍是胶囊背景"啪"一下变宽变窄。
        /// 真正可见的宽度就是每个分段 Border 自己的跨度，所以补间挪到分段上做。
        /// Inkeys 对应的实现是逐帧补间 MainBar 形状的宽度（w.SetTar），
        /// 这里是同一思想在"分段各自带背景"架构下的落地。
        ///
        /// 做法沿用「一次布局 + 一次补间」：先清残留动画与固定值，UpdateLayout
        /// 拿到内容变化后的新尺寸；差不足 1 就不动，否则把跨度钉回旧值，
        /// 用与按钮出场同时长同曲线的补间推到新值，结束后 ClearValue 交还自动尺寸。
        ///
        /// 段内按钮的位置瞬跳由"邻居让位"通道补偿；段外兄弟（含拖动头）的位置
        /// 跟着钉住的旧跨度走，随补间自然滑动，正好省掉对它们的额外补偿。
        ///
        /// 整段刚出现（出场动画已覆盖整段）或刚隐藏（瞬时收起的取舍）时跳过。
        /// 关掉动画开关时直接跳过，保持瞬时切换。横排补宽度，竖排补高度。
        /// </remarks>
        private static void AnimateBorderExtentToFit(Border border, bool vertical, double beforeExtent, bool wasVisible)
        {
            if (border == null || !_animateVisibilityChange) return;
            // 整段刚出现（出场动画已覆盖）或刚隐藏（瞬时收起的取舍）时不做跨度补间
            if (!wasVisible || border.Visibility != Visibility.Visible) return;

            DependencyProperty extentProperty = vertical
                ? FrameworkElement.HeightProperty
                : FrameworkElement.WidthProperty;

            try
            {
                // 打断支持：若上一轮跨度补间还在飞，就从它**当前所在的那个值**接上，
                // 而不是退回切换前的旧值。否则中途被打断会先跳回起点再走——看着像抽搐。
                double liveExtent = vertical ? border.ActualHeight : border.ActualWidth;
                bool wasAnimating = _activeSegmentTweens.Contains(border);
                double startExtent = wasAnimating && liveExtent > 0 ? liveExtent : beforeExtent;

                // 先摘掉上一轮可能残留的尺寸动画与固定值（中途被打断也能从这里平滑重启）
                int generation = NextSegmentGeneration(border);
                border.BeginAnimation(extentProperty, null);
                border.ClearValue(extentProperty);

                // 强制一次布局：内容显隐后的新尺寸这时才量得到
                border.UpdateLayout();
                double targetExtent = vertical ? border.ActualHeight : border.ActualWidth;

                // 新旧尺寸差不多就不必补间
                if (targetExtent <= 0 || startExtent <= 0 ||
                    Math.Abs(targetExtent - startExtent) < 1.0)
                {
                    return;
                }

                // 跨度退回起点（布局已算出新值，写死尺寸会立刻生效），
                // 再用补间推到新值：视觉上就是胶囊背景慢慢变宽/变窄
                if (vertical) border.Height = startExtent;
                else border.Width = startExtent;

                var animation = new System.Windows.Media.Animation.DoubleAnimation
                {
                    From = startExtent,
                    To = targetExtent,
                    // 被打断时按剩余距离缩短时长：从半途接上却仍按整段时长走会显得拖沓
                    Duration = ScaleDurationForRemaining(
                        startExtent, targetExtent, beforeExtent, ToolbarWidthTransitionMs),
                    EasingFunction = new System.Windows.Media.Animation.CubicEase
                    {
                        EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
                    }
                };

                // 结束后解除固定尺寸，交还内容自动尺寸（否则之后内容变化会被旧值卡住）
                // 代际守卫：被打断的旧动画仍会触发 Completed，不能让它清掉新动画的值
                _activeSegmentTweens.Add(border);
                animation.Completed += (_, _) =>
                {
                    if (!IsCurrentSegmentGeneration(border, generation)) return;
                    _activeSegmentTweens.Remove(border);
                    border.BeginAnimation(extentProperty, null);
                    border.ClearValue(extentProperty);
                    // 补间收尾后通知跟随者重新落位：居中要跟着**最终**宽度走
                    NotifyExtentChanged(vertical);
                };

                border.BeginAnimation(extentProperty, animation);
            }
            catch (Exception ex)
            {
                // 补间失败不能留下固定尺寸，否则分段会永远卡在旧跨度
                border.BeginAnimation(extentProperty, null);
                border.ClearValue(extentProperty);
                LogHelper.WriteLogToFile($"ToolbarRegistry: 分段尺寸补间失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        /// <summary>
        /// 整段胶囊的出现与消失：跨度从 0 长到目标值，或从当前值收到 0 后再真正折叠。
        /// </summary>
        /// <remarks>
        /// 只补"段内内容变化"不够——模式切换时往往是整段消失/整段出现，
        /// 那一瞬间胶囊凭空不见或凭空出现，肉眼看到的就是"闪"。
        /// 这里把整段也纳入跨度补间，胶囊本身就是动画对象。
        ///
        /// 出现：先显示并量出内容应有的跨度，再把跨度钉到 0 补间到该值；
        /// 期间打开 ClipToBounds，否则固定尺寸的按钮会画到胶囊外。
        /// 段内的按钮不再各自播出场动画（由 _suppressChildEnterAnimation 抑制）：
        /// 整段长出来本身就是那一次的出场。
        ///
        /// 消失：段内按钮已瞬时折叠，这里把空胶囊从当前跨度收到 0，收完才 Collapsed。
        /// 钉住的是旧跨度，布局跑完时邻居仍在原位，位移差约为 0，
        /// 邻居让位通道自然几乎不介入，不会和逐帧跨度打架。
        ///
        /// 段一直在显示、只是内容增减时交给 <see cref="AnimateBorderExtentToFit"/>。
        /// 关掉动画开关时全部瞬时。
        /// </remarks>
        private static void AnimateSegmentTransition(
            Border border, bool vertical, double beforeExtent, bool wasVisible, bool shouldShow)
        {
            if (border == null) return;

            DependencyProperty extentProperty = vertical
                ? FrameworkElement.HeightProperty
                : FrameworkElement.WidthProperty;

            if (!_animateVisibilityChange)
            {
                border.BeginAnimation(extentProperty, null);
                border.ClearValue(extentProperty);
                border.BeginAnimation(UIElement.OpacityProperty, null);
                border.Opacity = 1;
                border.ClipToBounds = false;
                border.Visibility = shouldShow ? Visibility.Visible : Visibility.Collapsed;
                return;
            }

            // 一直显示、只是内容增减：走内容变化的跨度补间
            if (shouldShow && wasVisible)
            {
                AnimateBorderExtentToFit(border, vertical, beforeExtent, true);
                return;
            }

            // 一直隐藏、仍判为隐藏：不动
            if (!shouldShow && !wasVisible) return;

            var duration = AnimationSpeed.ScaleMilliseconds(ToolbarWidthTransitionMs);

            try
            {
                // 打断上一轮：动画与固定值都先摘掉，否则会从旧值原地重启
                int generation = NextSegmentGeneration(border);
                border.BeginAnimation(extentProperty, null);
                border.ClearValue(extentProperty);
                border.BeginAnimation(UIElement.OpacityProperty, null);
                border.Opacity = 1;

                if (shouldShow)
                {
                    border.Visibility = Visibility.Visible;
                    border.UpdateLayout();
                    double targetExtent = vertical ? border.ActualHeight : border.ActualWidth;

                    if (targetExtent <= 0)
                    {
                        // 量不到尺寸（尚未进视觉树）：退化成淡入，至少不要卡在 0 跨度
                        border.ClearValue(extentProperty);
                        border.BeginAnimation(UIElement.OpacityProperty,
                            new System.Windows.Media.Animation.DoubleAnimation(0.35, 1.0, duration)
                            {
                                EasingFunction = CreateEnterEasing()
                            });
                        return;
                    }

                    // 从当前可见跨度长到目标值（被打断时就从半途接着长，不退回 0）
                    border.ClipToBounds = true;
                    double growFrom = beforeExtent > 0 && beforeExtent < targetExtent ? beforeExtent : 0.0;
                    if (vertical) border.Height = growFrom;
                    else border.Width = growFrom;

                    var grow = new System.Windows.Media.Animation.DoubleAnimation
                    {
                        From = growFrom,
                        To = targetExtent,
                        Duration = ScaleDurationForRemaining(growFrom, targetExtent, targetExtent, ToolbarWidthTransitionMs),
                        EasingFunction = new System.Windows.Media.Animation.CubicEase
                        {
                            EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
                        }
                    };
                    _activeSegmentTweens.Add(border);
                    grow.Completed += (_, _) =>
                    {
                        if (!IsCurrentSegmentGeneration(border, generation)) return;
                        _activeSegmentTweens.Remove(border);
                        border.BeginAnimation(extentProperty, null);
                        border.ClearValue(extentProperty);
                        border.ClipToBounds = false;
                        NotifyExtentChanged(vertical);
                    };
                    border.BeginAnimation(extentProperty, grow);

                    border.BeginAnimation(UIElement.OpacityProperty,
                        new System.Windows.Media.Animation.DoubleAnimation(0.35, 1.0, duration)
                        {
                            EasingFunction = CreateEnterEasing()
                        });
                    return;
                }

                // 消失：钉住当前跨度收到 0，收完才真正折叠
                double fromExtent = beforeExtent > 0
                    ? beforeExtent
                    : (vertical ? border.ActualHeight : border.ActualWidth);
                if (fromExtent <= 0)
                {
                    border.Visibility = Visibility.Collapsed;
                    return;
                }

                if (vertical) border.Height = fromExtent;
                else border.Width = fromExtent;

                var shrink = new System.Windows.Media.Animation.DoubleAnimation
                {
                    From = fromExtent,
                    To = 0.0,
                    Duration = ScaleDurationForRemaining(fromExtent, 0.0, fromExtent, ToolbarWidthTransitionMs),
                    EasingFunction = new System.Windows.Media.Animation.CubicEase
                    {
                        EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
                    }
                };
                _activeSegmentTweens.Add(border);
                shrink.Completed += (_, _) =>
                {
                    if (!IsCurrentSegmentGeneration(border, generation)) return;
                    _activeSegmentTweens.Remove(border);
                    border.BeginAnimation(extentProperty, null);
                    border.ClearValue(extentProperty);
                    border.BeginAnimation(UIElement.OpacityProperty, null);
                    border.Opacity = 1;
                    border.Visibility = Visibility.Collapsed;
                    NotifyExtentChanged(vertical);
                };
                border.BeginAnimation(extentProperty, shrink);

                border.BeginAnimation(UIElement.OpacityProperty,
                    new System.Windows.Media.Animation.DoubleAnimation(1.0, 0.0, duration));
            }
            catch (Exception ex)
            {
                border.BeginAnimation(extentProperty, null);
                border.ClearValue(extentProperty);
                border.BeginAnimation(UIElement.OpacityProperty, null);
                border.Opacity = 1;
                border.ClipToBounds = false;
                border.Visibility = shouldShow ? Visibility.Visible : Visibility.Collapsed;
                LogHelper.WriteLogToFile($"ToolbarRegistry: 分段显隐补间失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        /// <summary>
        /// 按"还剩多长距离"折算补间时长：被打断的动画从半途接上时，
        /// 若仍按整段时长走，前半段会显得拖沓（距离短了、时间没短）。
        /// </summary>
        /// <remarks>
        /// 距离比取自"本次起点到终点"占"原始全程"的比例，下限 30%：
        /// 太短的时长会让收尾看起来像硬切，失掉过渡的意义。
        /// </remarks>
        private static TimeSpan ScaleDurationForRemaining(
            double startExtent, double targetExtent, double fullExtent, int baseMilliseconds)
        {
            var baseDuration = AnimationSpeed.ScaleMilliseconds(baseMilliseconds);
            double fullDistance = Math.Abs(fullExtent - targetExtent);
            double remaining = Math.Abs(startExtent - targetExtent);
            if (fullDistance < 1.0 || remaining >= fullDistance) return baseDuration;

            double ratio = Math.Max(0.3, remaining / fullDistance);
            return TimeSpan.FromMilliseconds(baseDuration.TotalMilliseconds * ratio);
        }

        /// <summary>分段胶囊当前动画的代际号：每次发起新动效都自增，用来作废旧动画的收尾回调。</summary>
        /// <remarks>
        /// 动画倍率可以被调到 0.1x，一次出场要 2.2 秒；期间几乎一定会被下一次显隐打断。
        /// 被打断的旧动画照样会走 Completed，若它在那里 ClearValue，
        /// 就会把**新**动画刚钉住的跨度清掉——表现是背景抽搐一下再跳到位。
        /// 代际守卫让只有"当前这一轮"的回调才有权收尾。
        /// </remarks>
        private static readonly Dictionary<Border, int> _segmentGenerations = new Dictionary<Border, int>();

        /// <summary>当前正在做跨度补间的分段。用来判断"能否打断接续"以及"居中是否需要逐帧跟随"。</summary>
        /// <remarks>
        /// 早先用 _segmentGenerations.ContainsKey 当"正在动画"的判据——但那个字典只增不减，
        /// 一旦补间过一次就永远为真，于是之后每次显隐都会误判成"被打断"，
        /// 从当前值接着走而不是从切换前的真实值起步，观感就是抽风。
        /// 这里用显式的活动集合，补间收尾即移除。
        /// </remarks>
        private static readonly HashSet<Border> _activeSegmentTweens = new HashSet<Border>();

        /// <summary>是否还有分段的跨度补间在飞（居中据此决定要不要逐帧跟随）。</summary>
        public static bool IsExtentAnimating => _activeSegmentTweens.Count > 0;

        private static int NextSegmentGeneration(Border border)
        {
            int next = _segmentGenerations.TryGetValue(border, out var current) ? current + 1 : 1;
            _segmentGenerations[border] = next;
            return next;
        }

        private static bool IsCurrentSegmentGeneration(Border border, int generation)
        {
            return _segmentGenerations.TryGetValue(border, out var current) && current == generation;
        }

        /// <summary>分段胶囊跨度补间的时长（毫秒），与按钮出场同长。</summary>
        private const int ToolbarWidthTransitionMs = ToolbarItemShowDurationMs;

        private static void UpdatePanelVisibility(Panel panel, Dictionary<string, bool> context)
        {
            if (panel == null) return;

            // 朝向决定了组件显隐时沿哪个轴滑入、让位，一次算好传给下面所有调用
            bool vertical = IsVerticalPanel(panel);

            foreach (var child in panel.Children.OfType<FrameworkElement>())
            {
                if (child.Tag as string == InjectedTag)
                {
                    var ruleset = GetHidingRuleset(child);
                    if (ruleset == null)
                    {
                        SetAnimatedVisibility(child, Visibility.Visible, _animateVisibilityChange, vertical);
                    }
                    else
                    {
                        bool shouldHide = EvaluateRuleset(ruleset, context);
                        SetAnimatedVisibility(child,
                            shouldHide ? Visibility.Collapsed : Visibility.Visible,
                            _animateVisibilityChange,
                            vertical);
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
                        // 段内容显隐前后各量一次跨度：胶囊背景要跟着内容平滑变宽/变窄
                        bool wasSegmentVisible = border.Visibility == Visibility.Visible;
                        double beforeExtent = vertical ? border.ActualHeight : border.ActualWidth;
                        // 整段是"从无到有"时，让胶囊长大当出场，段内按钮不再各自动画
                        bool suppressBefore = _suppressChildEnterAnimation;
                        if (!wasSegmentVisible) _suppressChildEnterAnimation = true;
                        try
                        {
                            UpdatePanelVisibility(sp, context);
                        }
                        finally
                        {
                            _suppressChildEnterAnimation = suppressBefore;
                        }
                        bool anyVisible = HasVisibleLeafContent(sp);
                        AnimateSegmentTransition(border, vertical, beforeExtent, wasSegmentVisible, anyVisible);
                    }
                    else if (border.Child is Grid grid)
                    {
                        // 同上：胶囊背景的跨度补间
                        bool wasSegmentVisible = border.Visibility == Visibility.Visible;
                        double beforeExtent = vertical ? border.ActualHeight : border.ActualWidth;
                        bool suppressBefore = _suppressChildEnterAnimation;
                        if (!wasSegmentVisible) _suppressChildEnterAnimation = true;
                        try
                        {
                            foreach (var gridChild in grid.Children.OfType<FrameworkElement>())
                            {
                                if (gridChild is StackPanel sp2 && sp2.Tag as string == ContentPanelTag)
                                {
                                    UpdatePanelVisibility(sp2, context);
                                }
                            }
                        }
                        finally
                        {
                            _suppressChildEnterAnimation = suppressBefore;
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
                        AnimateSegmentTransition(border, vertical, beforeExtent, wasSegmentVisible, anyVisible);
                    }
                }
            }
        }

        /// <summary>
        /// 按目标状态设置 Visibility，并在"从隐藏变为显示"时播放一次出场动画。
        /// </summary>
        /// <remarks>
        /// 直接赋值 Visibility 会让按钮瞬间出现，观感生硬；这里给按钮加一段
        /// 位移 + 透明度 + 缩放的过渡，三者同长同曲，一起收尾。
        ///
        /// 位移距离取组件自身的跨度（横排是宽度、竖排是高度），方向由拖动头
        /// 所在一端决定（_enterSlideSign）：从头部一侧滑到 0，
        /// 看起来才是"被从那一端推出来"，而不是凭空冒出。
        ///
        /// 只有"由隐藏→显示"才播动画：<see cref="UpdatePanelVisibility"/> 会在每次模式变化时
        /// 遍历全部子项，若对每次赋值都播动画，已经在屏幕上的按钮会不断重播、整体发虚。
        ///
        /// 隐藏仍然瞬时：收起后要立刻让出布局空间，否则工具栏尺寸会滞后于内容。
        /// 淡出需要时间，而布局等不起——这是取舍，不是遗漏。
        /// 邻居会从旧位置平滑补到新位置，所以肉眼看到的是"它让开、其余滑动"，不是整条工具栏弹一下。
        ///
        /// 缩放写在 RenderTransform 上而不是改布局尺寸：布局尺寸一变，相邻按钮的位置
        /// 会跟着跳；缩放只影响这一段的绘制，落位后回到 1.0，布局位置始终是最终状态。
        /// </remarks>
        private static void SetAnimatedVisibility(
            FrameworkElement element, Visibility target, bool animate, bool vertical = false)
        {
            if (element == null) return;

            bool becameVisible = target == Visibility.Visible && element.Visibility != Visibility.Visible;
            bool becameHidden = target != Visibility.Visible && element.Visibility == Visibility.Visible;

            if (!animate)
            {
                element.BeginAnimation(UIElement.OpacityProperty, null);
                element.Opacity = 1;
                ResetMotionChannels(element);
                element.Visibility = target;
                return;
            }

            if (becameVisible)
            {
                element.Visibility = Visibility.Visible;

                // 整段胶囊正在长出来：按钮跟着显形即可，不必再各自滑入
                if (_suppressChildEnterAnimation) return;

                var duration = AnimationSpeed.ScaleMilliseconds(ToolbarItemShowDurationMs);

                // 1. 透明度：半透明 → 不透明。
                // 不从 0 起是有意的：全透明会让按钮先"空"一下再显形，
                // 而此刻右边正有邻居在让位，两者叠加会显得整条工具栏在闪。
                var fade = new System.Windows.Media.Animation.DoubleAnimation(
                    ToolbarItemShowStartOpacity, 1.0, duration)
                {
                    EasingFunction = CreateEnterEasing()
                };
                element.BeginAnimation(UIElement.OpacityProperty, fade);

                // 2. 位移：从"拖动头所在的一侧"滑到 0，看起来是被那一端推出来的。
                //    横排量宽度沿 X，竖排量高度沿 Y；符号由头部在哪端决定（_enterSlideSign）。
                var enterSlide = GetOrCreateChannel(element, EnterChannelIndex);
                double slideDistance = ResolveEnterSlideDistance(element, vertical);
                // 使用本轮开头冻结的符号；没有冻结值（单点调用）才回退到当前推断
                int slideSign = _slideSignFrozenForRound ?? _enterSlideSign;
                // 记一行可对照的日志：方向再出错时，符号、距离、元素类型都能直接查
                LogHelper.WriteLogToFile(
                    $"[Toolbar] 滑入 {element.GetType().Name} 符号={slideSign} 距离={slideDistance:F1} " +
                    $"起点={slideSign * slideDistance:F1} 朝向={(vertical ? "竖" : "横")}",
                    LogHelper.LogType.Trace);
                // 位移走**恒定速度**：时长按距离算，且不加缓动。
                // 缓动曲线会让同一段位移中途变速，与"速度恒定"的要求相悖。
                var slideIn = new System.Windows.Media.Animation.DoubleAnimation(
                    slideSign * slideDistance, 0.0,
                    DurationForDistance(slideDistance, ToolbarItemShowDurationMs));
                enterSlide.BeginAnimation(
                    vertical ? TranslateTransform.YProperty : TranslateTransform.XProperty,
                    slideIn);

                // 3. 缩放：绕自身中心从 0.82 长到 1.0。
                // 结束时必须把值落回 1.0 并摘掉动画：残留的缩放会让
                // TransformToAncestor 量到的位置带上偏差，邻居让位就会算错。
                var scale = GetOrCreateScaleTransform(element);
                var scaleX = new System.Windows.Media.Animation.DoubleAnimation(
                    ToolbarItemShowStartScale, 1.0, duration)
                {
                    EasingFunction = CreateEnterEasing()
                };
                var scaleY = new System.Windows.Media.Animation.DoubleAnimation(
                    ToolbarItemShowStartScale, 1.0, duration)
                {
                    EasingFunction = CreateEnterEasing()
                };
                scaleX.Completed += (_, _) => ResetScaleChannel(scale);
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);
                return;
            }

            if (becameHidden)
            {
                // 隐藏不做过渡：收起后要立刻让出布局空间，否则工具栏尺寸会滞后于内容。
                element.BeginAnimation(UIElement.OpacityProperty, null);
                element.Opacity = 1;
                ResetMotionChannels(element);
                element.Visibility = target;
                return;
            }

            // 状态未变：不重复播动画
            element.Visibility = target;
        }

        /// <summary>
        /// 组件出场/让位的**基准速度**：像素/秒。
        /// </summary>
        /// <remarks>
        /// 动画应当是**速度恒定**而不是时间恒定：
        /// 32px 宽的按钮和 308px 宽的整条工具栏，若共用同一个时长，
        /// 前者慢得像凝滞、后者快得像闪一下，速度天差地别。
        /// 这里统一改用"距离 ÷ 速度"算时长，跨距大小只影响时长，不影响观感节奏。
        /// 基准值按 220ms 走完 44px 按钮反推（约 200px/s），与原单手感受接近。
        /// </remarks>
        private const double EnterSlidePixelsPerSecond = 200.0;

        /// <summary>按距离与基准速度算出时长；距离为 0 时退化为最短时长。</summary>
        /// <remarks>
        /// 上下限用来兜底：距离极小时不要短到看不见，极大时不要长到拖沓。
        /// 最终时长再经 <see cref="AnimationSpeed"/> 按用户倍率缩放。
        /// </remarks>
        private static TimeSpan DurationForDistance(double distancePixels, double fallbackMilliseconds)
        {
            if (double.IsNaN(distancePixels) || double.IsInfinity(distancePixels) || distancePixels <= 1.0)
            {
                return AnimationSpeed.ScaleMilliseconds(fallbackMilliseconds);
            }

            double seconds = distancePixels / EnterSlidePixelsPerSecond;
            double milliseconds = Math.Max(90.0, Math.Min(600.0, seconds * 1000.0));
            return AnimationSpeed.ScaleMilliseconds(milliseconds);
        }

        /// <summary>按钮出场用的一条缓动曲线（先快后慢）。</summary>
        /// <remarks>每条动画各取一个实例，避免多个动画共用同一个 Freezable。</remarks>
        private static System.Windows.Media.Animation.CubicEase CreateEnterEasing()
        {
            return new System.Windows.Media.Animation.CubicEase
            {
                EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
            };
        }

        /// <summary>把组件上的动画通道全部归位，供"不要动画"与隐藏时使用。</summary>
        private static void ResetMotionChannels(FrameworkElement element)
        {
            if (element == null) return;
            if (!(element.RenderTransform is TransformGroup group)) return;

            foreach (var child in group.Children)
            {
                if (child is TranslateTransform translate)
                {
                    translate.BeginAnimation(TranslateTransform.XProperty, null);
                    translate.BeginAnimation(TranslateTransform.YProperty, null);
                    translate.X = 0;
                    translate.Y = 0;
                }
                else if (child is ScaleTransform scale)
                {
                    ResetScaleChannel(scale);
                }
            }
        }

        /// <summary>把缩放通道落回 1.0 并摘掉动画。</summary>
        private static void ResetScaleChannel(ScaleTransform scale)
        {
            if (scale == null) return;
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            scale.ScaleX = 1.0;
            scale.ScaleY = 1.0;
        }

        /// <summary>
        /// 新出现的组件从拖动头所在的一侧滑进来，滑行距离就是它自己的跨度。
        /// </summary>
        /// <remarks>
        /// 为什么取自身跨度：滑入方向上的邻居刚好让出同等的距离，组件滑过这段距离落位，
        /// 看起来才是被推出来的。方向由 <see cref="RefreshEnterSlideSign"/> 按真实布局推断。
        ///
        /// 量宽度有个时序坑：调用这里时元素刚从 Collapsed 切到 Visible，
        /// 布局还没跑，ActualWidth 可能还是 0。
        /// 所以按 Width（显式尺寸）→ ActualWidth → DesiredSize 的顺序退让，
        /// 都拿不到才退回一个固定的小位移。
        /// </remarks>
        private static double ResolveEnterSlideDistance(FrameworkElement element, bool vertical = false)
        {
            double extent = vertical ? element.Height : element.Width;
            if (double.IsNaN(extent) || extent <= 0)
            {
                extent = vertical ? element.ActualHeight : element.ActualWidth;
            }
            if (double.IsNaN(extent) || extent <= 0)
            {
                extent = vertical ? element.DesiredSize.Height : element.DesiredSize.Width;
            }
            if (double.IsNaN(extent) || extent <= 0)
            {
                try
                {
                    element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    extent = vertical ? element.DesiredSize.Height : element.DesiredSize.Width;
                }
                catch
                {
                    extent = 0;
                }
            }

            if (double.IsNaN(extent) || double.IsInfinity(extent) || extent <= 0)
            {
                return FallbackEnterSlideDistance;
            }

            return Math.Min(extent, MaxEnterSlideDistance);
        }

        /// <summary>
        /// 按钮出场时长（毫秒）。
        /// 对齐 Inkeys 的 0.4s 会显得慢——那是它整套 UI 的统一节奏，
        /// 而工具栏按钮是被频繁触发的元素，这里折中取 220ms。
        /// 组件位移、邻居让位、工具栏尺寸过渡共用这一个数：三者走的其实是同一段距离
        /// （组件自身的宽度），时长一旦不同步，就会一前一后收尾。
        /// </summary>
        internal const int ToolbarItemShowDurationMs = 220;

        /// <summary>按钮出场时的起始透明度。</summary>
        /// <remarks>
        /// 取半透明而不是全透明：全透明会让按钮先"空"一下再显形，
        /// 与同时进行的邻居让位叠加，看起来像整条工具栏在闪。
        /// </remarks>
        internal const double ToolbarItemShowStartOpacity = 0.35;

        /// <summary>按钮出场时的起始缩放。</summary>
        internal const double ToolbarItemShowStartScale = 0.82;

        /// <summary>拿不到组件宽度时退而使用的滑入距离（DIP）。</summary>
        private const double FallbackEnterSlideDistance = 8.0;

        /// <summary>滑入距离上限（DIP），防止异常宽度把组件甩到屏幕外。</summary>
        private const double MaxEnterSlideDistance = 400.0;

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
                    if (AnimationsHelper.IsPopupTargetingOpen(popup))
                    {
                        AnimationsHelper.HidePopupWithSlideAndFade(popup);
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
                    // 关闭按钮也要走收起动画：直接置 IsOpen 会截断进行中的动画（画面瞬间消失）。
                    shell.CloseButtonControl.Click += (s, e) => AnimationsHelper.HidePopupWithSlideAndFade(popup);
                }
                else if (popupContent is PopupTabShellContent tabShell)
                {
                    tabShell.CloseButtonControl.Click += (s, e) => AnimationsHelper.HidePopupWithSlideAndFade(popup);
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
                closeButton.Click += (s, e) => AnimationsHelper.HidePopupWithSlideAndFade(popup);
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
