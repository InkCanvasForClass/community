using Ink_Canvas.Helpers;
using Ink_Canvas.Helpers.Motion;
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
        /// 下一次 <see cref="UpdateVisibilityByMode"/> 是否跳过动画（重建工具栏后第一次同步用）。
        /// </summary>
        /// <remarks>
        /// 重建时所有组件都先按"可见"建出来，真实显隐要等紧接着的第一次同步才纠正。
        /// 如果那次同步带动画，用户会看到启动瞬间"图标冒出来又缩回去"。
        /// 首轮直接瞬时落定，之后的变化才值得有动画。
        /// </remarks>
        private static bool _skipAnimationOnNextSync;

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

        // ───────────────────────── 动画轨迹日志（排障用） ─────────────────────────
        //
        // 动画出问题时最贵的成本是"猜哪条分支跑了"。这里把决策点写成一行行轨迹，
        // 默认关闭，只在程序根目录出现 toolbar_motion_trace.flag 时落盘：
        // 用户复现一次，日志里就能读到这一轮走了哪条路径、跨度补间被谁撤销。
        private static int _motionTraceState; // 0=未探测 1=开 2=关

        private static bool MotionTraceEnabled()
        {
            int state = _motionTraceState;
            if (state == 0)
            {
                state = 2;
                try
                {
                    if (File.Exists(Path.Combine(App.RootPath, "toolbar_motion_trace.flag"))) state = 1;
                }
                catch
                {
                    // 读不到就按"关"处理：轨迹日志绝不该影响动画本身
                }
                _motionTraceState = state;
            }
            return state == 1;
        }

        // 轨迹日志攒批再落盘：LogHelper 每写一行要抓一次 StackTrace、重开一次文件、
        // 还要扫一遍 Logs 目录算大小，实测 9~10ms/行。一次模式切换会打二十多行，
        // 结果同步本身被日志拖到 230ms，用户看到的就是"切一下卡半秒"。
        // 现在一行只是往缓冲里追加，整批一次写，成本从 N×9ms 变成 ~9ms。
        private static readonly System.Text.StringBuilder _motionTracePending =
            new System.Text.StringBuilder(4096);
        private static bool _motionTraceFlushQueued;

        internal static void MotionTrace(string message)
        {
            if (!MotionTraceEnabled()) return;
            _motionTracePending.Append(DateTime.Now.ToString("HH:mm:ss.fff")).Append(" [ToolbarMotion] ").Append(message).Append('\n');
            ScheduleMotionTraceFlush();
        }

        private static void ScheduleMotionTraceFlush()
        {
            if (_motionTraceFlushQueued) return;
            _motionTraceFlushQueued = true;
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                _motionTraceFlushQueued = false;
                FlushMotionTrace();
                return;
            }
            dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,
                new Action(() => { _motionTraceFlushQueued = false; FlushMotionTrace(); }));
        }

        private static void FlushMotionTrace()
        {
            if (_motionTracePending.Length == 0) return;
            string text = _motionTracePending.ToString();
            _motionTracePending.Clear();
            LogHelper.WriteLogToFile(text.TrimEnd(), LogHelper.LogType.Info);
        }

        /// <summary>日志里的元素标签：Name → ToolTip → 类型名（组件大多没有 Name）。</summary>
        private static string Describe(FrameworkElement element)
        {
            if (element == null) return "<null>";
            if (!string.IsNullOrEmpty(element.Name)) return element.Name;
            if (element.ToolTip is string tip && tip.Length > 0)
            {
                return tip.Length <= 12 ? tip : tip.Substring(0, 12);
            }
            return element.GetType().Name;
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
        /// 收尾回调只发一次，不是每帧都发；逐帧的变化走 <see cref="ExtentFrame"/>。
        /// </remarks>
        public static event Action ExtentSettled;

        /// <summary>
        /// 分段胶囊的跨度**正在逐帧变化**时回调（每帧一次，收尾后不再发）。
        /// </summary>
        /// <remarks>
        /// 居中跟随者需要每一帧都知道当前跨度，才能让工具栏在变宽/变窄的全过程中保持居中。
        /// 但回调只负责**测量**：早先的版本在这个时机直接写 ViewboxFloatingBar.Margin 重新居中，
        /// 与出场滑入的 RenderTransform、收尾落位三方同时改视觉位置，观感是"被拽到中间再跳一下"，
        /// 整段被回退过（2f71a285）。位置补偿现在由跟随者自己在 RenderTransform 上做，
        /// 收尾再一次性原子提交，三者不再重叠。
        /// </remarks>
        public static event Action ExtentFrame;

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

        /// <summary>把一个分段的跨度动画立即收尾：还在补间的落到终值，再清固定值回到内容自动尺寸。</summary>
        /// <remarks>
        /// 只清**被补间的那一个轴**。横排分段本身带着 Height = 58（写在 CreateContentBorder 里），
        /// 竖排分段带着 Width = 58；把两个轴一起清掉会连固定的厚度也一起丢掉，
        /// 分段高度塌成内容高度、胶囊外观随之崩掉。这是早先版本的实打实的缺陷。
        /// </remarks>
        private static void SettleSegment(Border border, bool vertical)
        {
            if (border == null) return;

            // 还在跑的跨度弹簧：先落到终值再落定，量宽度的一方才拿得到稳定尺寸
            if (_extentMotions.TryGetValue(border, out var state))
            {
                WriteSegmentExtent(border, state.Vertical, state.HideWhenSettled ? 0.0 : state.Target);
                FinishExtentMotion(border, state);
                return;
            }

            DependencyProperty extentProperty = vertical
                ? FrameworkElement.HeightProperty
                : FrameworkElement.WidthProperty;
            border.BeginAnimation(extentProperty, null);
            border.ClearValue(extentProperty);

            border.BeginAnimation(UIElement.OpacityProperty, null);
            border.Opacity = 1;
            border.ClipToBounds = false;
        }

        // ───────────────────── 分段跨度：与组件同一条曲线 ─────────────────────
        //
        // 跨度不能是"布局自动尺寸 + 固定时长 EaseOut"：组件的位移走的是另一套节奏，
        // 两套收尾对不上，看起来就是"背景没跟上内容"。
        // 也不能再是弹簧：这一版按 Inkeys 工具栏的模型改成**时间驱动曲线**——
        // 所有组件与胶囊共用同一个时长、同一条曲线（EaseOutBack，见 ApplyMotionEasing），
        // 起与收都是同一个 Progress 的正反向播放：出现 0→1、收起 1→0，
        // 所以收起天然是出现的严格倒放（popup 菜单同构，用户 m01345）。
        // 每帧只写一次 Width，居中的补偿仍由跟随者订阅 ExtentFrame 逐帧做。

        /// <summary>一段胶囊正在跑的跨度补间。</summary>
        /// <remarks>
        /// 与 popup 菜单（AnimationsHelper 里的 token.Progress）同构：只有一个自变量 Progress，
        /// Direction 决定往哪边走。出现是 0→1、收起是 1→0，取的是同一条曲线，
        /// 所以收起天然是出现的倒放（EaseInBack ≡ 1 - EaseOutBack(1-t)）。
        /// 端点跨度固定不动，打断时按当前跨度反解进度再翻方向，位置连续。
        /// </remarks>
        private sealed class SegmentExtentMotion
        {
            public bool Vertical;

            /// <summary>进度 = 0 那一端的跨度（收起到底时是 0）。</summary>
            public double Start;

            /// <summary>进度 = 1 那一端的跨度（完全展开时的跨度）。</summary>
            public double Target;

            /// <summary>进度 [0,1]：唯一自变量。</summary>
            public double Progress;

            /// <summary>+1 展开、−1 收起。</summary>
            public double Direction = 1.0;

            /// <summary>最近一帧写下去的跨度；反解进度与撤销时读它。</summary>
            public double Current;

            /// <summary>走到端点时把这一整段真正折叠（整段消失时用）。</summary>
            public bool HideWhenSettled;

            /// <summary>这一轮是不是收尾纠偏（只纠一次，免得两次数出微差时来回起补间）。</summary>
            public bool Corrected;

            /// <summary>总时长（秒，已按全局动画倍率换算）。</summary>
            public double DurationSeconds;

            /// <summary>回弹强度：1.1 是 Inkeys 的原值（≈4.47% 过冲）。</summary>
            public double Back;

            /// <summary>已经推进到端点（收尾那一帧据此落定）。</summary>
            public bool Finished;

            /// <summary>
            /// 被补间轴上原来的最小跨度（独立边框段有 MinWidth/MinHeight = 58）。
            /// 不清零的话，写下去小于 58 的宽高会被布局夹住：曲线在跑、画面不动，
            /// 收尾那一帧才突然塌掉——"背景一下子缩短、没有动画"就是这么来的。
            /// </summary>
            public double SavedMin;

            /// <summary>是否接管过最小跨度（收尾时要还原）。</summary>
            public bool MinTaken;
        }

        /// <summary>某一进度下该写的跨度：两端固定，中间由 EaseOutBack 决定。</summary>
        private static double ExtentValueAt(SegmentExtentMotion state)
        {
            if (state == null) return 0;
            if (state.Progress <= 0.0) return state.Start;
            if (state.Progress >= 1.0) return state.Target;
            return state.Start + (state.Target - state.Start) * EaseOutBack(state.Progress, state.Back);
        }

        /// <summary>按「当前可见跨度」反解进度，供打断时接续。</summary>
        /// <remarks>
        /// popup 打断时靠 progress 不重置来保持连续；这里端点会换（目标变了），只能反解。
        /// EaseOutBack 在 [0, pPeak] 上单调，二分这一段就够：过冲区（pPeak 之后）
        /// 只占曲线尾巴的几 %，落到 pPeak 也看不出接缝。
        /// </remarks>
        private static double ProgressForValue(double start, double target, double value, double back)
        {
            double span = target - start;
            if (Math.Abs(span) < 0.001) return 0.0;

            double pPeak = 1.0 - 2.0 * back / (3.0 * (back + 1.0));
            if (pPeak <= 0.01 || pPeak > 1.0) pPeak = 1.0;

            double low = 0.0;
            double high = pPeak;
            for (int i = 0; i < 24; i++)
            {
                double mid = (low + high) * 0.5;
                double midValue = start + span * EaseOutBack(mid, back);
                if (span > 0 ? midValue < value : midValue > value) low = mid;
                else high = mid;
            }
            return Math.Max(0.0, Math.Min(pPeak, (low + high) * 0.5));
        }

        /// <summary>补间期间把最小跨度让开，否则写下去的跨度会被布局夹回去。</summary>
        private static void NormalizeExtentMin(Border border, SegmentExtentMotion state)
        {
            if (border == null || state == null || state.MinTaken) return;

            if (state.Vertical)
            {
                state.SavedMin = border.MinHeight;
                border.MinHeight = 0;
            }
            else
            {
                state.SavedMin = border.MinWidth;
                border.MinWidth = 0;
            }

            state.MinTaken = true;
        }

        /// <summary>补间结束：把借走的最小跨度还回去（多组件胶囊本来是 0，还回 0 无害）。</summary>
        private static void RestoreExtentMin(Border border, SegmentExtentMotion state)
        {
            if (border == null || state == null || !state.MinTaken) return;

            state.MinTaken = false;
            if (state.Vertical) border.MinHeight = state.SavedMin;
            else border.MinWidth = state.SavedMin;
        }

        /// <summary>每个分段最多一条跨度补间；后发起的直接替换前者，所以不需要代际号。</summary>
        private static readonly Dictionary<Border, SegmentExtentMotion> _extentMotions =
            new Dictionary<Border, SegmentExtentMotion>();

        /// <summary>所有分段共用的一只表：一次渲染回调里把它们一起推进。</summary>
        private static readonly System.Diagnostics.Stopwatch _extentMotionClock =
            new System.Diagnostics.Stopwatch();

        private static TimeSpan _extentMotionLastFrame;
        private static bool _extentMotionHooked;

        /// <summary>逐帧遍历用的快照：每帧 new 一个 List 会在长动画里持续产生垃圾。</summary>
        private static readonly List<Border> _extentFrameBorders = new List<Border>();
        private static readonly List<SegmentExtentMotion> _extentFrameStates = new List<SegmentExtentMotion>();

        /// <summary>逐帧成本采样：用来定位"展开/收起时掉帧"到底花在哪一段。</summary>
        private static readonly System.Diagnostics.Stopwatch _extentFrameCostWatch =
            new System.Diagnostics.Stopwatch();
        private static int _extentFrameSamples;
        private static double _extentFrameTotalMs;
        private static double _extentFrameWorstMs;
        private static double _extentFrameWorstGapMs;

        private static bool IsExtentMoving(Border border)
        {
            return border != null && _extentMotions.ContainsKey(border);
        }

        private static void WriteSegmentExtent(Border border, bool vertical, double extent)
        {
            if (border == null) return;
            if (double.IsNaN(extent) || double.IsInfinity(extent) || extent < 0) extent = 0;

            // 只写被补间的那一个轴：另一个轴的固定厚度是构造时写死的
            if (vertical) border.Height = extent;
            else border.Width = extent;
        }

        /// <summary>
        /// 用弹簧把分段的跨度驱动到 <paramref name="to"/>，与组件位移同一档、同一倍速。
        /// </summary>
        /// <param name="hideWhenSettled">收尾时把这一整段折叠（整段消失的倒放）。</param>
        /// <remarks>
        /// 打断即接续：同一分段只允许一条跨度弹簧在跑，新的一轮直接替换旧的，
        /// 起点由调用方给（通常就是当前可见跨度）。替换即意味着旧的不会再写值，
        /// 因此不需要"代际号"那套守卫——那个字典早就成了只增不减的状态泄漏源。
        /// </remarks>
        /// <summary>跨度补间的内部入口：显式给进度与方向。</summary>
        private static void BeginExtentMotion(
            Border border, bool vertical, double startExtent, double endExtent,
            double progress, double direction, bool hideWhenSettled,
            double seconds, double backAmount)
        {
            if (border == null) return;

            if (double.IsNaN(startExtent) || double.IsInfinity(startExtent)) startExtent = 0;
            if (double.IsNaN(endExtent) || double.IsInfinity(endExtent)) endExtent = 0;
            if (startExtent < 0) startExtent = 0;
            if (endExtent < 0) endExtent = 0;
            if (progress < 0) progress = 0;
            if (progress > 1) progress = 1;

            DependencyProperty extentProperty = vertical
                ? FrameworkElement.HeightProperty
                : FrameworkElement.WidthProperty;

            // 摘掉可能残留的尺寸动画与固定值，再把跨度钉到当前进度对应的值
            border.BeginAnimation(extentProperty, null);
            if (_extentMotions.TryGetValue(border, out var previous))
            {
                _extentMotions.Remove(border);
                RestoreExtentMin(border, previous);
            }

            var state = new SegmentExtentMotion
            {
                Vertical = vertical,
                Start = startExtent,
                Target = endExtent,
                Progress = progress,
                Direction = direction >= 0 ? 1.0 : -1.0,
                HideWhenSettled = hideWhenSettled,
                DurationSeconds = Math.Max(0.01, seconds),
                Back = double.IsNaN(backAmount) ? 0.0 : backAmount,
            };
            state.Current = ExtentValueAt(state);
            if (state.Progress <= 0.0 && state.Direction < 0) state.Finished = true;
            if (state.Progress >= 1.0 && state.Direction > 0) state.Finished = true;

            NormalizeExtentMin(border, state);
            _extentMotions[border] = state;
            WriteSegmentExtent(border, vertical, state.Current);
            MotionTrace($"跨度补间开始 {Describe(border)} 进度={state.Progress:F2} 方向={state.Direction:F0} 端点={startExtent:F1}/{endExtent:F1} 当前={state.Current:F1}（收起={hideWhenSettled} 时长={state.DurationSeconds * 1000:F0}ms 回弹={state.Back:F2}）");
            EnsureExtentMotionHook();

            // 起点即终点：不空跑一帧，直接收尾
            if (Math.Abs(endExtent - startExtent) < 0.5) FinishExtentMotion(border, state);
        }

        /// <summary>
        /// 跨度补间的唯一入口：给出「收起到底」与「完全展开」两端跨度，说明这次往哪边走。
        /// </summary>
        /// <remarks>
        /// 出现与收起用同一对端点、同一条曲线：出现进度 0→1，收起 1→0，
        /// 所以收起就是出现的严格倒放（与 popup 菜单一致，用户 m01345）。
        /// 中途打断时端点换成新的、进度按当前跨度反解、只翻方向，不重置时钟。
        /// </remarks>
        private static void RunExtentMotion(
            Border border, bool vertical, double collapsedExtent, double expandedExtent,
            bool collapsing, bool hideWhenSettled, double seconds = 0, double back = double.NaN)
        {
            if (border == null) return;

            var profile = ResolveMotionProfile();
            double durationSeconds = seconds > 0 ? seconds : profile.Seconds;
            double backAmount = double.IsNaN(back) ? profile.Back : back;

            double progress;
            if (_extentMotions.TryGetValue(border, out var previous) && previous != null && !previous.Finished)
            {
                progress = ProgressForValue(collapsedExtent, expandedExtent, previous.Current, backAmount);
            }
            else
            {
                progress = collapsing ? 1.0 : 0.0;
            }

            BeginExtentMotion(border, vertical, collapsedExtent, expandedExtent, progress,
                collapsing ? -1.0 : 1.0, hideWhenSettled, durationSeconds, backAmount);
        }

        private static void EnsureExtentMotionHook()
        {
            if (_extentMotionHooked) return;
            _extentMotionHooked = true;
            _extentMotionClock.Restart();
            _extentMotionLastFrame = TimeSpan.Zero;
            CompositionTarget.Rendering += OnExtentMotionFrame;
        }

        private static void UnhookExtentMotionHook()
        {
            if (!_extentMotionHooked) return;
            _extentMotionHooked = false;
            CompositionTarget.Rendering -= OnExtentMotionFrame;
            _extentMotionClock.Stop();

            if (_extentFrameSamples > 0)
            {
                MotionTrace($"跨度逐帧统计 帧数={_extentFrameSamples} 最差处理={_extentFrameWorstMs:F1}ms 平均处理={_extentFrameTotalMs / _extentFrameSamples:F1}ms 最差帧间隔={_extentFrameWorstGapMs:F1}ms");
                _extentFrameSamples = 0;
                _extentFrameTotalMs = 0;
                _extentFrameWorstMs = 0;
                _extentFrameWorstGapMs = 0;
            }
        }

        /// <summary>每帧推进一次所有分段的跨度补间（一个渲染回调，所有分段共用）。</summary>
        private static void OnExtentMotionFrame(object sender, EventArgs e)
        {
            if (_extentMotions.Count == 0)
            {
                UnhookExtentMotionHook();
                return;
            }

            double frameSeconds = (_extentMotionClock.Elapsed - _extentMotionLastFrame).TotalSeconds;
            _extentMotionLastFrame = _extentMotionClock.Elapsed;
            if (frameSeconds <= 0 || double.IsNaN(frameSeconds)) return;
            // 曲线按时间推进：一帧超过 0.25s 说明确实卡了，允许它跳过去，
            // 但再大的间隔（挂起、断点）不该让整段动画"凭空完成"，夹一下。
            if (frameSeconds > 0.25) frameSeconds = 0.25;

            _extentFrameCostWatch.Restart();

            _extentFrameBorders.Clear();
            _extentFrameStates.Clear();
            foreach (var pair in _extentMotions)
            {
                _extentFrameBorders.Add(pair.Key);
                _extentFrameStates.Add(pair.Value);
            }

            List<Border> settled = null;
            for (int i = 0; i < _extentFrameBorders.Count; i++)
            {
                var border = _extentFrameBorders[i];
                var state = _extentFrameStates[i];

                // 曲线是时间驱动的：进度由真实经过的秒数算，掉帧只是少画几帧，
                // 不会像弹簧那样"跨一大步然后回弹过冲"。
                // 与 popup 同构：progress 每帧按 direction * dt / duration 推进，越界钉到端点。
                double step = state.DurationSeconds > 0 ? frameSeconds / state.DurationSeconds : 1.0;
                state.Progress += state.Direction * step;
                if (state.Progress >= 1.0)
                {
                    state.Progress = 1.0;
                    if (state.Direction > 0) state.Finished = true;
                }
                else if (state.Progress <= 0.0)
                {
                    state.Progress = 0.0;
                    if (state.Direction < 0) state.Finished = true;
                }

                state.Current = ExtentValueAt(state);
                WriteSegmentExtent(border, state.Vertical, state.Current);

                if (!state.Finished) continue;

                (settled ?? (settled = new List<Border>())).Add(border);
            }

            // 跨度这一帧变过了：居中跟随者据此逐帧补偿（它只测量，位置由它自己在
            // RenderTransform 上写，这里绝不动 Margin——那正是 2f71a285 回退掉的做法）
            try
            {
                ExtentFrame?.Invoke();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"ToolbarRegistry: 跨度逐帧回调失败: {ex.Message}", LogHelper.LogType.Warning);
            }

            // 这一帧的处理成本：与"距上一帧隔了多久"一起记账，收尾时汇总一行。
            // 前者是我们自己的开销，后者是用户看到的最差帧间隔。
            double frameCostMs = _extentFrameCostWatch.Elapsed.TotalMilliseconds;
            _extentFrameSamples++;
            _extentFrameTotalMs += frameCostMs;
            if (frameCostMs > _extentFrameWorstMs) _extentFrameWorstMs = frameCostMs;
            if (frameSeconds * 1000.0 > _extentFrameWorstGapMs) _extentFrameWorstGapMs = frameSeconds * 1000.0;

            if (settled == null)
            {
                if (_extentMotions.Count == 0) UnhookExtentMotionHook();
                return;
            }

            foreach (var border in settled)
            {
                // 这一帧里被新一轮接管了：交给新状态收尾
                if (!_extentMotions.TryGetValue(border, out var state)) continue;
                FinishExtentMotion(border, state);
            }

            if (_extentMotions.Count == 0) UnhookExtentMotionHook();
        }

        /// <summary>跨度补间收尾：收起型的真正折叠，其余交还自动尺寸并通知落位。</summary>
        private static void FinishExtentMotion(Border border, SegmentExtentMotion state)
        {
            if (border == null || state == null) return;

            if (_extentMotions.TryGetValue(border, out var current) && ReferenceEquals(current, state))
            {
                _extentMotions.Remove(border);
            }
            if (_extentMotions.Count == 0) UnhookExtentMotionHook();

            MotionTrace($"跨度补间收尾 {Describe(border)} 进度={state.Progress:F2} 当前={state.Current:F1} 端点={state.Start:F1}/{state.Target:F1} 收起={state.HideWhenSettled}");

            if (state.HideWhenSettled)
            {
                // 收完才 Collapsed：段内组件还画在胶囊里，一起被裁掉
                ApplySegmentSettled(border, state.Vertical, false);
                RestoreExtentMin(border, state);
                return;
            }

            DependencyProperty extentProperty = state.Vertical
                ? FrameworkElement.HeightProperty
                : FrameworkElement.WidthProperty;

            border.BeginAnimation(extentProperty, null);
            border.ClearValue(extentProperty);
            RestoreExtentMin(border, state);

            // 收尾纠偏：补间是按「量出来的目标」收的，而清掉固定尺寸后真实落位未必是这个数
            // （DesiredSize 与排布实排能差几 px）。落定前量一次真值，差得多就用 120ms
            // 单调曲线补过去，用户就看不到「最后几 px 啪一下」。只纠一次，防止来回抖。
            if (!state.Corrected)
            {
                double settledExtent = ReadLaidOutExtent(border, state.Vertical);
                if (settledExtent > 0 && Math.Abs(settledExtent - state.Current) > 0.5)
                {
                    MotionTrace($"跨度收尾纠偏 {Describe(border)} {state.Current:F1} → {settledExtent:F1}（补 120ms）");
                    border.ClipToBounds = false;
                    border.Opacity = 1;
                    BeginExtentMotion(border, state.Vertical, state.Current, settledExtent,
                        progress: 0.0, direction: 1.0, hideWhenSettled: false,
                        seconds: 0.12, backAmount: 0.0);
                    if (_extentMotions.TryGetValue(border, out var correction) && correction != null)
                    {
                        correction.Corrected = true;
                    }
                    return;
                }
            }

            border.ClipToBounds = false;
            border.Opacity = 1;
            // 跨度已经稳定：让居中/换边重新量一次（这时量到的才是最终宽度）
            NotifyExtentChanged(state.Vertical);
        }

        /// <summary>撤掉一条跨度补间但不落定（交给调用方接续）。</summary>
        private static void StopExtentMotion(Border border, bool trace = true)
        {
            if (border == null) return;
            if (_extentMotions.TryGetValue(border, out var state))
            {
                _extentMotions.Remove(border);
                RestoreExtentMin(border, state);
                if (trace) MotionTrace($"跨度补间撤销 {Describe(border)} 停在 {state.Current:F1}");
            }
            if (_extentMotions.Count == 0) UnhookExtentMotionHook();
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
            // 方形胶囊不参与跨度动画（宽 = 高，固定）
            SetSeparateSegment(wrapper, true);
            return wrapper;
        }

        private static void ApplyInitialVisibility(FrameworkElement element, ToolbarRuleset ruleset)
        {
            element.Visibility = Visibility.Visible;
        }

        /// <summary>这个 Border 是不是"独立边框段"（一个组件一个方形胶囊，见 WrapInSeparateBorder）。</summary>
        /// <remarks>
        /// 独立段是固定方形（横向时宽 = 高 = 58，竖向时高 = 宽 = 58），
        /// 跨度永远由布局决定、两个方向都不参与补间（用户 m01461：
        /// 「独立边框的背景宽是固定宽=高的」）。
        /// 收起时的倒放只剩段内组件自己的位移/透明度/缩放，方形背景等它们退完再一起折。
        /// </remarks>
        public static readonly DependencyProperty SeparateSegmentProperty =
            DependencyProperty.RegisterAttached("SeparateSegment", typeof(bool), typeof(ToolbarRegistry),
                new PropertyMetadata(false));

        public static void SetSeparateSegment(FrameworkElement element, bool value)
            => element.SetValue(SeparateSegmentProperty, value);

        public static bool GetSeparateSegment(FrameworkElement element)
            => (bool)element.GetValue(SeparateSegmentProperty);

        private static bool IsSeparateSegment(Border border)
            => border != null && GetSeparateSegment(border);

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
            bool skippingFirstSync = _skipAnimationOnNextSync;
            bool animateThisRound = _animateVisibilityChange && !skippingFirstSync;
            _skipAnimationOnNextSync = false;

            var syncCostWatch = System.Diagnostics.Stopwatch.StartNew();
            MotionTrace($"同步开始 放动画={animateThisRound} 开关={_animateVisibilityChange} 首轮跳过={skippingFirstSync} 标注中={isAnnotating} PPT={isPPTMode} 用户收起={_isContentCollapsedByUser}");

            // 记下切换前的排布：显隐之后所有组件都按 FLIP 从旧位置滑到新位置，
            // 新出现的组件则以"左邻的旧位置"为起点，看起来就是从邻居身下钻出来。
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

            MotionTrace($"同步结束 耗时={syncCostWatch.Elapsed.TotalMilliseconds:F1}ms 退场中={_exiting.Count} 跨度补间={_extentMotions.Count}");

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
        /// <summary>
        /// 量元素相对 <paramref name="ancestor"/> 的**布局位置**（不含元素自身的 RenderTransform）。
        /// </summary>
        /// <remarks>
        /// 不能用 TransformToAncestor：那条链会把元素身上正在跑的位移补间一起算进去。
        /// 动画中途再同步一次时，量到的就是中间态，FLIP 的 delta 会算成反向的碎值，
        /// 组件于是被打断着重跑一遍（日志里同一元素先 Δ=-84、又 Δ=31）。
        /// 这里逐级累加 <see cref="VisualTreeHelper.GetOffset(Visual)"/>，只反映布局排出来的位置，
        /// 让"切换前在哪、切换后在哪"用同一个坐标系比较。
        /// </remarks>
        private static bool TryMeasureLayoutPosition(
            FrameworkElement element, Visual ancestor, out Point position)
        {
            position = default;
            if (element == null || ancestor == null) return false;

            try
            {
                double x = 0;
                double y = 0;
                for (var node = (DependencyObject)element; node != null; node = VisualTreeHelper.GetParent(node))
                {
                    if (ReferenceEquals(node, ancestor))
                    {
                        position = new Point(x, y);
                        return true;
                    }
                    if (node is Visual visual)
                    {
                        var offset = VisualTreeHelper.GetOffset(visual);
                        x += offset.X;
                        y += offset.Y;
                    }
                }
            }
            catch
            {
                // 尚未进入视觉树
            }

            return false;
        }

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
                if (TryMeasureLayoutPosition(child, panel, out var offset))
                {
                    result[child] = offset;
                }
            }

            // 正在倒放收起的组件不算"切换前就在场"：它此刻的位置是动画中间态，
            // 记下来会让下一轮把它当成老住户，倒放就失去了起点。
            if (_exiting.Count > 0)
            {
                foreach (var pending in _exiting.Keys) result.Remove(pending);
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
            CollectVisibleLayoutElements(root, current, sink, null);
        }

        /// <summary>
        /// 收集会移动的组件；<paramref name="order"/> 非空时顺带按排布顺序记一遍，
        /// 供 FLIP 找"左邻是谁"——光有位置字典分不出谁在谁左边。
        /// </summary>
        private static void CollectVisibleLayoutElements(
            Panel root, FrameworkElement current, Dictionary<FrameworkElement, Point> sink,
            List<FrameworkElement> order)
        {
            if (current == null || current.Visibility != Visibility.Visible) return;

            // 记录"真正会移动的组件"：带注入标记的那些（按钮/分隔线）。
            // 不用类型判断：ToolbarImageButton 是 UserControl（属 ContentControl），
            // 按类型排除会把所有按钮漏掉。
            // 也不做视觉树深搜：那会在每次显隐时产生数百次查询，得不偿失。
            //
            // **分组容器是例外**：带注入标记的 Panel 只是"分组内组件"的排版容器，
            // 它自己不是组件。把它也记进来，等于让一整个分组当一个组件动——
            // 整组的透明度/缩放会叠在内部按钮上，位移也会被算两遍
            // （容器一层、内部按钮再一层），看到的就是"整块方块淡入"。
            // 所以面板只递归、不记账，让内部每个分组内组件各自成为 FLIP/倒放的单位。
            if (current.Tag as string == InjectedTag && current != root && current is not Panel)
            {
                // 位置为 0 的元素过去被跳过（那时只看谁"挪动了"）；现在还要它当左邻锚点，
                // 所以顺序照记，只是不进位置字典以免污染位移计算。
                order?.Add(current);
                if (TryMeasureLayoutPosition(current, root, out var point)
                    && (point.X > 0.01 || point.Y > 0.01))
                {
                    sink[current] = point;
                }

                // 分组容器内部还有组件，继续往下收
            }

            // 继续向下递归
            if (current is Panel panel)
            {
                foreach (var child in panel.Children.OfType<FrameworkElement>())
                {
                    CollectVisibleLayoutElements(root, child, sink, order);
                }
            }
            else if (current is Border border && border.Child is FrameworkElement borderChild)
            {
                CollectVisibleLayoutElements(root, borderChild, sink, order);
            }
            else if (current is ContentControl contentControl && contentControl.Content is FrameworkElement content)
            {
                CollectVisibleLayoutElements(root, content, sink, order);
            }
        }

        /// <summary>
        /// 让每个组件从"切换前的位置"滑到"切换后的新位置"；新出现的组件从左邻的旧位置钻出。
        /// </summary>
        /// <remarks>
        /// 布局一旦重算，子项会瞬间跳到新位置。要让它看起来是滑过去的，
        /// 就在跳变之后立刻给每个子项叠加一个反向位移，再把这个位移补间回 0。
        ///
        /// 位移写 RenderTransform（合成层），不触发布局，比动画 Margin 便宜得多。
        /// 必须等布局跑完才能量到新位置，所以排到 Loaded 优先级执行。
        ///
        /// **新出现的组件不另算一套滑入**：它的起点就是左邻（切换前就在场的那个）的旧位置，
        /// 于是它从邻居身下"抽"出来。两者共用同一条曲线、同一个时钟，
        /// 走两格与走一格在同样时长内完成——"远的快、近的慢、同时收尾"不需要额外参数。
        ///
        /// 横排工具栏沿 X 让位、竖排沿 Y 让位：捕获时两个分量都记了，
        /// 这里按面板朝向只取对应的那一个。另一个分量本来就不该变，参与计算只会引入噪声。
        /// </remarks>
        private static void AnimateChildrenToNewOffsets(
            Panel panel, Dictionary<FrameworkElement, Point> beforeOffsets)
        {
            // 这里不再因 beforeOffsets 为空而提前返回：收起倒放只依赖"谁在退场"，
            // 面板刚被清空时正是它最需要跑的时候。
            if (panel == null || beforeOffsets == null) return;

            bool vertical = IsVerticalPanel(panel);

            panel.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    // 第一遍：段内的组件。按"切换后"的排布顺序遍历，新组件才能找到自己的左邻是谁。
                    var leaves = new List<FrameworkElement>();
                    CollectVisibleLayoutElements(panel, panel, new Dictionary<FrameworkElement, Point>(), leaves);

                    // 倒放收起先跑：它要读"切换前的位置"，而下面的让位会在同一帧里改写通道值。
                    AnimateExitingElements(panel, leaves, beforeOffsets, vertical);

                    // 这一遍是收起还是展开：决定让位用曲线的哪一侧（见 ApplyFlipOffsets）。
                    // 判据是"有没有组件在退场"——收起必然产生退场计划，展开不产生。
                    bool collapsing = _exiting.Count > 0;

                    ApplyFlipOffsets(panel, leaves, beforeOffsets, vertical, anchorForNewcomer: true, ownZIndex: true, collapsing: collapsing);

                    // 第二遍：根面板的直接子项（拖动头、独立边框、分段胶囊）。
                    // 它们不参与"从邻居身下钻出"——新分段由胶囊自己从 0 长出来负责，
                    // 这里只补位置平移，否则拖动头会"啪"地横移一段。
                    //
                    // 但**内部已经自己补过位移的外壳必须跳过**：外壳带着内容走一次、
                    // 内容自己再走一次，同一段路就走成两倍，组件会从比邻位远一倍的地方飞进来。
                    var rootChildren = new List<FrameworkElement>();
                    foreach (var child in panel.Children.OfType<FrameworkElement>())
                    {
                        if (child.Visibility != Visibility.Visible) continue;
                        if (HasAnimatedDescendant(child, leaves)) continue;
                        rootChildren.Add(child);
                    }
                    ApplyFlipOffsets(panel, rootChildren, beforeOffsets, vertical, anchorForNewcomer: false, ownZIndex: false, collapsing: collapsing);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ToolbarRegistry] 邻居滑动动画失败: {ex.Message}");
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>
        /// 元素自身、或它的后代里，是否有已经在补位移动画的组件。
        /// </summary>
        /// <remarks>
        /// 用来避免"外壳 + 内部"对同一段位移各补一次：渲染位移会沿视觉树累加，
        /// 两层各补一次就等于把距离翻倍，看起来像从屏幕外甩进来。
        /// </remarks>
        private static bool HasAnimatedDescendant(FrameworkElement element, List<FrameworkElement> animated)
        {
            if (element == null || animated == null) return false;
            foreach (var candidate in animated)
            {
                if (candidate == null) continue;
                if (ReferenceEquals(candidate, element)) return true;
                if (element.IsAncestorOf(candidate)) return true;
            }
            return false;
        }

        /// <summary>
        /// 给一串按排布顺序排列的元素叠加 FLIP 反向位移，再交给曲线补回 0。
        /// </summary>
        /// <param name="anchorForNewcomer">
        /// 切换前不在场的元素是否从"左邻的旧位置"起步。段内为 true；
        /// 根面板为 false——新分段自己从 0 宽长出来，不该再横移一段。
        /// </param>
        /// <param name="ownZIndex">
        /// 是否按排布顺序压低 z 序。WPF 里后添加的子项盖在上面，
        /// 不压低的话新组件会盖住左邻，"从底下抽出来"就变成"浮在上面出现"。
        /// </param>
        /// <param name="collapsing">
        /// 这一遍是收起还是展开。决定用曲线的哪一侧：展开是 EaseOutBack（冲出去再回落），
        /// 收起是它的时间反演 EaseInBack（先微微反向蓄力再冲进来）。
        /// </param>
        private static void ApplyFlipOffsets(
            Panel panel,
            List<FrameworkElement> ordered,
            Dictionary<FrameworkElement, Point> beforeOffsets,
            bool vertical,
            bool anchorForNewcomer,
            bool ownZIndex,
            bool collapsing)
        {
            // 锚点按"最近的左邻"滚动推进，且只认切换前就在场的元素：
            // 一串连续新出现的组件因此共用同一个起点，正好叠成一摞卡片，
            // 再由弹簧一起甩开——这就是 (ab)(cd)e 那种"从一处扇出"的观感。
            var anchors = new Dictionary<DependencyObject, double>();
            // 已经压过 z 序的分组容器：只认第一个进容器的组件，后面的不能覆盖
            var lowered = new HashSet<DependencyObject>();

            for (int i = 0; i < ordered.Count; i++)
            {
                var child = ordered[i];
                if (child == null || child.Visibility != Visibility.Visible) continue;

                // 正在倒放收起的组件归"收起"那一遍管：这里既不给它补位移，
                // 也不让它当别人的锚点（它的位置是动画中间态）。
                if (_exiting.ContainsKey(child)) continue;

                if (ownZIndex)
                {
                    Panel.SetZIndex(child, -i);
                    LowerGroupContainers(child, panel, -i, lowered);
                }

                if (!TryMeasureLayoutPosition(child, panel, out var now)) continue;

                double finalPos = vertical ? now.Y : now.X;
                bool existed = beforeOffsets.TryGetValue(child, out var before);
                double startPos;

                if (existed)
                {
                    startPos = vertical ? before.Y : before.X;
                }
                else
                {
                    startPos = anchorForNewcomer && anchors.TryGetValue(LayoutSlot(child, panel), out var anchorPos)
                        ? anchorPos
                        : finalPos;
                }

                // 只有切换前就在场的元素才配当锚点
                if (existed)
                {
                    anchors[LayoutSlot(child, panel)] = startPos;
                }

                double delta = startPos - finalPos;
                if (Math.Abs(delta) < 0.5) continue;

                MotionTrace($"让位 {Describe(child)} {finalPos:F1} ← 起点 {startPos:F1}（Δ={delta:F1} 新出现={!existed}）");

                // 让位与出场共用同一条位移通道：同一条曲线、同一个时钟，
                // 所以"新组件钻出"和"邻居让开"天然同步收尾。
                var translate = GetOrCreateChannel(child, TranslateChannelIndex);
                StartChannelMotion(translate, vertical, delta, 0.0, collapse: collapsing);
            }
        }

        /// <summary>
        /// 锚点槽位：组件序列所在的段内容面板。
        /// </summary>
        /// <remarks>
        /// 按 <see cref="FrameworkElement.Parent"/> 归组会看不见分组外面的左邻：
        /// 分组里第一个组件的父级是分组面板，锚点表里没有它的历史位置，
        /// 它就只能原地出现——"从邻居身下抽出来"在分组上恰好失效。
        /// 改按最近的段内容面板归组，分组内外的组件共用同一条锚点链，
        /// 一组同时出现的组件于是都从左邻那一点扇出。
        /// </remarks>
        private static DependencyObject LayoutSlot(FrameworkElement element, Panel panel)
        {
            for (var node = element?.Parent; node != null; node = System.Windows.Media.VisualTreeHelper.GetParent(node))
            {
                if (node == panel) break;
                if (node is FrameworkElement fe && fe.Tag as string == ContentPanelTag) return fe;
            }
            return element?.Parent as DependencyObject ?? panel;
        }

        /// <summary>
        /// 分组容器也跟着压 z 序。
        /// </summary>
        /// <remarks>
        /// 叠序是逐层生效的：只压内部按钮，整个容器仍按"后添加"盖在左邻上面，
        /// 跨分组边界时"从底下抽出来"就变成"浮在上面出现"。
        /// 容器取它第一个组件的序位；后到的组件不覆盖（先到先得）。
        /// </remarks>
        private static void LowerGroupContainers(
            FrameworkElement element, Panel panel, int zIndex, HashSet<DependencyObject> lowered)
        {
            for (var node = element?.Parent; node != null; node = System.Windows.Media.VisualTreeHelper.GetParent(node))
            {
                if (node == panel) break;
                if (node is not FrameworkElement fe || fe.Tag as string != InjectedTag || fe is not Panel) break;
                if (!lowered.Add(fe)) break;
                Panel.SetZIndex(fe, zIndex);
            }
        }

        // ───────────────────────── 收起：展开的倒放 ─────────────────────────

        /// <summary>
        /// 一个正在"倒放收起"的元素此刻的状态。
        /// </summary>
        /// <remarks>
        /// 收起不能立刻把组件 Collapsed——那样它就再也画不出来了。做法是让它**还画着、但不占布局**：
        /// 把 Margin 的右侧（竖排为下方）减去自己的尺寸，StackPanel 给它的槽位宽度就成了 0，
        /// 后面的兄弟立刻补上来，而它仍按原尺寸绘制（WPF 默认不裁剪溢出）。
        /// 于是"布局一瞬间到位"和"绘制还能慢慢退场"这两件事可以同时成立。
        ///
        /// 位移/透明度/缩放都在 FLIP 那一遍起播：只有拿到整轮排布，才知道"倒放到谁的位置去"。
        /// 透明度和位移两条时间线都收尾后才真正 Collapsed——先收位移会让淡出被硬切掉半截。
        /// </remarks>
        private sealed class ExitPlan
        {
            public Thickness Margin;
            public int ZIndex;
            public bool Vertical;
            /// <summary>整段胶囊收起（由胶囊自己补间跨度），不是段内组件。</summary>
            public bool Segment;
            public bool FadeDone;
            public bool SlideDone;
            /// <summary>独立方形的背景动画（淡出 + 缩回）是否已经收尾。</summary>
            public bool UnitDone;
            /// <summary>独立边框段：这一个正方形背景 + 里面那一个组件，整体当成一个组件来倒放。</summary>
            public bool SeparateUnit;
        }

        private static readonly Dictionary<FrameworkElement, ExitPlan> _exiting =
            new Dictionary<FrameworkElement, ExitPlan>();

        /// <summary>入场中的独立方形：排布已经回来，但背景还在从 0 淡入（值是它的排布跨度）。</summary>
        private static readonly Dictionary<Border, double> _enteringUnits = new Dictionary<Border, double>();

        /// <summary>把元素的排布尺寸压到 0：让出布局，但仍然绘制。</summary>
        private static Thickness ZeroFootprintMargin(Thickness margin, FrameworkElement element, bool vertical)
        {
            var size = element.DesiredSize;
            if (vertical)
            {
                return new Thickness(margin.Left, margin.Top, margin.Right, margin.Bottom - size.Height);
            }
            return new Thickness(margin.Left, margin.Top, margin.Right - size.Width, margin.Bottom);
        }

        /// <summary>
        /// 开始一次"倒放收起"。返回 false 表示这次没有动画可放，调用方应立即落定。
        /// </summary>
        private static bool TryBeginHide(FrameworkElement element, bool vertical)
        {
            if (element == null) return false;
            // 本轮不放动画（启动纠正 / 全局关动画）：绝不能留下零宽幻影——
            // 那种状态只有 FLIP 那一遍会来收尾，而这遍根本不会跑。
            if (!_animateVisibilityChange)
            {
                MotionTrace($"不倒放 {Describe(element)}：本轮关动画");
                return false;
            }
            // 已经在收：不要重复起（重复压 Margin 会把它的位置一路推出去）
            if (_exiting.ContainsKey(element)) return true;
            // 父级已经收起来了（整段一起收的那条路径）：没有倒放可放，直接落定
            if (!element.IsVisible)
            {
                MotionTrace($"不倒放 {Describe(element)}：父级已不可见");
                return false;
            }

            var size = element.DesiredSize;
            if (size.Width <= 0.5 || size.Height <= 0.5)
            {
                MotionTrace($"不倒放 {Describe(element)}：尺寸 {size.Width:F1}x{size.Height:F1}");
                return false;
            }

            _exiting[element] = new ExitPlan
            {
                Margin = element.Margin,
                ZIndex = Panel.GetZIndex(element),
                Vertical = vertical,
            };

            // 先让出布局：工具栏尺寸与邻居位置这一帧就要到位。
            // 绘制留到 FLIP 那一遍再倒放，那时才知道该退到哪儿。
            element.Margin = ZeroFootprintMargin(element.Margin, element, vertical);
            // 压到最底层：倒放是"缩回邻居身下"，不该压在邻居上面
            Panel.SetZIndex(element, -1000);
            return true;
        }

        /// <summary>结束一次收起：还原排布与图层，可选择顺便折叠。</summary>
        private static void FinishHide(FrameworkElement element, bool collapse)
        {
            if (element == null) return;
            if (!_exiting.TryGetValue(element, out var plan)) return;
            _exiting.Remove(element);

            element.BeginAnimation(UIElement.OpacityProperty, null);
            element.Opacity = 1;
            ResetMotionChannels(element);
            element.Margin = plan.Margin;
            Panel.SetZIndex(element, plan.ZIndex);
            if (!collapse) return;
            element.Visibility = Visibility.Collapsed;

            // 分组里最后一个组件收完了，容器空了就该真折叠：
            // 它始终占着 3px 左边距，留着会在工具栏中间留下一道缝、胶囊也多算 3px
            SettleGroupContainers(element);

            // 独立边框段：段内最后一个组件退完了，方形背景这才跟着折
            TryFinishSeparateSegmentHide(element);
        }

        /// <summary>
        /// 给所有正在收起的段内组件起播倒放：位移退到左邻身下，同时淡出并缩回。
        /// </summary>
        /// <remarks>
        /// 终点取"左边最近一个仍在场的组件的**新**位置"，而不是它自己的旧位置：
        /// 出场时它是从那个邻居的**旧**位置钻出来的，取新位置才是同一条路径的反向。
        /// abcde→ace 的几何校核：b 从 50 退回 4、d 从 142 退回 50，
        /// 正好是它们出场路程（46px、92px）的反向。
        /// </remarks>
        private static void AnimateExitingElements(
            Panel panel,
            List<FrameworkElement> ordered,
            Dictionary<FrameworkElement, Point> beforeOffsets,
            bool vertical)
        {
            if (panel == null || ordered == null || _exiting.Count == 0) return;

            double anchor = double.NaN; // 左边最近的"仍在场"组件的新位置

            for (int i = 0; i < ordered.Count; i++)
            {
                var child = ordered[i];
                if (child == null) continue;

                if (!_exiting.ContainsKey(child))
                {
                    // 量不到（尚未进入视觉树）时沿用上一个锚点
                    if (TryMeasureLayoutPosition(child, panel, out var point))
                    {
                        anchor = vertical ? point.Y : point.X;
                    }
                    continue;
                }

                var plan = _exiting[child];
                if (plan.Segment) continue; // 整段收起由胶囊自己补间跨度

                if (!child.IsVisible)
                {
                    FinishHide(child, collapse: true);
                    continue;
                }

                if (!TryMeasureLayoutPosition(child, panel, out var arrangedPoint))
                {
                    FinishHide(child, collapse: true);
                    continue;
                }
                double arranged = vertical ? arrangedPoint.Y : arrangedPoint.X;

                // 切换前的位置：倒放的起点。
                double from = arranged;
                if (beforeOffsets != null && beforeOffsets.TryGetValue(child, out var before))
                {
                    from = vertical ? before.Y : before.X;
                }

                // 整段一起收起时没有"还活着的左邻"可当锚点：这一段的起点就是所有人的终点。
                // 每个组件从自己的格子退回同一个点，走的格数不同、弹簧相同，
                // 于是"远的快、近的慢、同时收尾"——就是用户要的"bc 各自拆开退回 a"。
                bool segmentExit = IsInsideExitingSegment(child);
                double target = segmentExit ? arranged : (double.IsNaN(anchor) ? from : anchor);
                double startOffset = from - arranged;
                double endOffset = target - arranged;

                MotionTrace($"倒放 {Describe(child)} from={from:F1} arranged={arranged:F1} 锚点={(double.IsNaN(anchor) ? "-" : anchor.ToString("F1"))} 终点={target:F1} 整段={segmentExit} 位移={startOffset:F1}→{endOffset:F1}");

                var duration = AnimationSpeed.ScaleMilliseconds(ToolbarItemShowDurationMs);

                // 1. 透明度：1 → 0。若它还在淡入途中，就从当前值接着走，不跳变。
                var fade = new System.Windows.Media.Animation.DoubleAnimation(child.Opacity, 0.0, duration)
                {
                    EasingFunction = CreateExitEasing()
                };
                fade.Completed += (_, _) =>
                {
                    if (_exiting.TryGetValue(child, out var current)) current.FadeDone = true;
                    TryFinishReversedHide(child);
                };
                child.BeginAnimation(UIElement.OpacityProperty, fade);

                // 2. 缩放：1 → 出场起点（0.82），绕自身中心缩回去
                var scale = GetOrCreateScaleTransform(child);
                scale.BeginAnimation(ScaleTransform.ScaleXProperty,
                    new System.Windows.Media.Animation.DoubleAnimation(scale.ScaleX, ToolbarItemShowStartScale, duration)
                    {
                        EasingFunction = CreateExitEasing()
                    });
                scale.BeginAnimation(ScaleTransform.ScaleYProperty,
                    new System.Windows.Media.Animation.DoubleAnimation(scale.ScaleY, ToolbarItemShowStartScale, duration)
                    {
                        EasingFunction = CreateExitEasing()
                    });

                // 3. 位移：同一条通道、同一条曲线的反演，所以"退回去"就是"钻出来"的倒放
                var translate = GetOrCreateChannel(child, TranslateChannelIndex);
                StartChannelMotion(
                    translate, vertical, startOffset, endOffset, collapse: true,
                    onSettled: () =>
                    {
                        if (_exiting.TryGetValue(child, out var current)) current.SlideDone = true;
                        TryFinishReversedHide(child);
                    });
            }
        }

        /// <summary>透明度与位移都收尾了才真正折叠。</summary>
        private static void TryFinishReversedHide(FrameworkElement element)
        {
            if (element == null) return;
            if (!_exiting.TryGetValue(element, out var plan)) return;
            if (plan.Segment || !plan.FadeDone || !plan.SlideDone) return;
            FinishHide(element, collapse: true);
        }

        /// <summary>
        /// 整段消失：让胶囊自己从当前跨度缩回 0 再折叠。
        /// </summary>
        /// <remarks>
        /// 这是"整段出现时从 0 长出来"的倒放。曾经因两个原因放弃过：
        /// 补间被打断后 Completed 里的 Collapsed 再也不执行（代际守卫提前 return），
        /// 以及段内内容已瞬时收起、屏幕上只剩一个挂着的空壳。
        /// 现在两条都堵住了：收起被打断时新的一轮会从**当前跨度**接管（见 AnimateBorderExtentToFit），
        /// 段内内容也不再提前折叠，而是跟着胶囊一起被裁掉。
        /// </remarks>
        private static bool TryBeginSegmentHide(Border border, bool vertical)
        {
            if (border == null) return false;
            // 已经在收：让当前的补间跑完，别重开
            if (_exiting.ContainsKey(border)) return true;
            if (!_animateVisibilityChange || border.Visibility != Visibility.Visible)
            {
                MotionTrace($"整段收起跳过 {Describe(border)}：关动画={!_animateVisibilityChange} 可见={border.Visibility == Visibility.Visible}");
                return false;
            }

            DependencyProperty extentProperty = vertical
                ? FrameworkElement.HeightProperty
                : FrameworkElement.WidthProperty;

            // 屏幕上此刻的跨度：正在长的时候取补间写下去的那个值。
            // ActualWidth 会落后一帧，只读它会让收起的第一帧往回跳（"起步几百px"）。
            double currentExtent = vertical ? border.ActualHeight : border.ActualWidth;
            if (_extentMotions.TryGetValue(border, out var liveExtent) && liveExtent != null && !liveExtent.Finished)
            {
                currentExtent = Math.Max(currentExtent, liveExtent.Current);
            }
            if (currentExtent <= 1.0)
            {
                MotionTrace($"整段收起跳过 {Describe(border)}：当前跨度只有 {currentExtent:F1}");
                return false;
            }

            // 段内组件**不**在这里被撤销倒放：它们各自躺在 _exiting 里，
            // FLIP 那一遍会把每个组件从旧位置拉回这一段的起点（"各自拆开退回 a"）。
            // 这里曾经调 RestoreExitingLeaves —— 那正好把刚起播的倒放全部撤销，
            // 于是整段收起时组件是一整块被裁掉，而不是一个个退回去。
            MotionTrace($"整段收起开始 {Describe(border)} 当前跨度={currentExtent:F1} 段内退场组件={CountExitingInside(border)}");

            try
            {
                _exiting[border] = new ExitPlan
                {
                    Margin = border.Margin,
                    ZIndex = Panel.GetZIndex(border),
                    Vertical = vertical,
                    Segment = true,
                };

                border.BeginAnimation(extentProperty, null);
                border.ClearValue(extentProperty);
                border.BeginAnimation(UIElement.OpacityProperty, null);
                // 胶囊**不**跟着淡出：收起就是"把背景缩回去"。这里曾经写过 0.35→0 的淡出，
                // 画面上的观感正是"组件在倒放、背景却没动画、凭空没了"。
                border.Opacity = 1;
                // 固定尺寸的按钮会画到胶囊外，缩的时候必须裁
                border.ClipToBounds = true;

                // 与出场同一条曲线、反着走：出场弹出来，收起就弹回去，收完才 Collapsed
                RunExtentMotion(border, vertical, collapsedExtent: 0.0, expandedExtent: currentExtent,
                    collapsing: true, hideWhenSettled: true);
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"ToolbarRegistry: 分段收起补间失败: {ex.Message}", LogHelper.LogType.Warning);
                ApplySegmentSettled(border, vertical, false);
                return true;
            }
        }

        /// <summary>段内正在倒放收起的组件个数（只用于轨迹日志）。</summary>
        private static int CountExitingInside(Border border)
        {
            if (border == null || _exiting.Count == 0) return 0;

            int count = 0;
            foreach (var pair in _exiting)
            {
                if (pair.Value.Segment || pair.Key == border) continue;
                if (border.IsAncestorOf(pair.Key)) count++;
            }
            return count;
        }

        /// <summary>这个组件是不是"整段一起收起"里的一员：它的某个祖先分段正在收。</summary>
        private static bool IsInsideExitingSegment(FrameworkElement element)
        {
            if (element == null || _exiting.Count == 0) return false;

            foreach (var pair in _exiting)
            {
                if (!pair.Value.Segment) continue;
                if (pair.Key != null && pair.Key != element && pair.Key.IsAncestorOf(element)) return true;
            }
            return false;
        }

        /// <summary>收起被"又要显示"打断：还原排布，把跨度补间交还给内容补间。</summary>
        private static void CancelSegmentHide(Border border)
        {
            if (border == null) return;
            if (!_exiting.TryGetValue(border, out var plan)) return;
            _exiting.Remove(border);
            border.BeginAnimation(FrameworkElement.MarginProperty, null);
            border.Margin = plan.Margin;
            Panel.SetZIndex(border, plan.ZIndex);
            ResetSegmentUnitVisual(border);
            // 缩到一半的跨度弹簧也撤掉：接下来的跨度补间会从当前可见值接管，
            // 留着一个"收到 0 就折叠"的弹簧会把刚长出来的段又收掉。
            StopExtentMotion(border);
        }

        /// <summary>独立边框段的收起：方形背景自己淡出缩回，排布让出去，等段内组件退完再折。</summary>
        /// <remarks>
        /// 方形胶囊的跨度是布局给的常数（宽 = 高 = 58），不去补间它（用户 m01461），
        /// 但它自己也是「组件的一部分」：背景要有淡出 + 缩回，不能等组件退完后凭空消失（用户 m01615）。
        /// 排布让出量一旦瞬间归零，居中跟随当帧就少掉整个方形——栏心跳半个方形（用户 m01612），
        /// 所以让出去的那块由 <see cref="GetExitingVisualExtent"/> 按不透明度折算归还给跟随。
        /// 段内组件的倒放与背景淡出同时起播、同一时长，都收完才折方形。
        /// </remarks>
        private static bool BeginSeparateSegmentHide(Border border, bool vertical)
        {
            if (border == null) return false;
            if (_exiting.ContainsKey(border)) return true;
            if (!_animateVisibilityChange || border.Visibility != Visibility.Visible)
            {
                MotionTrace($"独立段收起跳过 {Describe(border)}：关动画={!_animateVisibilityChange} 可见={border.Visibility == Visibility.Visible}");
                return false;
            }

            try
            {
                _exiting[border] = new ExitPlan
                {
                    Margin = border.Margin,
                    ZIndex = Panel.GetZIndex(border),
                    Vertical = vertical,
                    Segment = true,
                    SeparateUnit = true,
                };

                border.ClipToBounds = false;

                // 让出排布但继续绘制：邻居这一帧就补上来，方形垫在最底层、自己慢慢淡出
                border.Margin = ZeroFootprintMargin(border.Margin, border, vertical);
                Panel.SetZIndex(border, -1000);

                MotionTrace($"独立段收起开始 {Describe(border)} 宽={border.ActualWidth:F1} 高={border.ActualHeight:F1} 段内退场组件={CountExitingInside(border)}");

                // 背景自己的退场动作：淡出 + 缩回。收尾后才置 UnitDone 并尝试折掉
                AnimateSegmentUnitExit(border, vertical);
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"ToolbarRegistry: 独立段收起失败: {ex.Message}", LogHelper.LogType.Warning);
                ApplySegmentSettled(border, vertical, false);
                return true;
            }
        }

        /// <summary>段内组件全退回了：把仍画着的独立方形一起折掉。</summary>
        private static void TryFinishSeparateSegmentHide(FrameworkElement element)
        {
            if (element == null || _exiting.Count == 0) return;

            DependencyObject node = VisualTreeHelper.GetParent(element);
            while (node != null)
            {
                if (node is Border border && _exiting.TryGetValue(border, out var plan)
                    && plan != null && plan.Segment)
                {
                    if (!plan.UnitDone || CountExitingInside(border) > 0) return;
                    MotionTrace($"独立段收起收尾 {Describe(border)}：组件与背景都收完了，折掉方形");
                    ApplySegmentSettled(border, plan.Vertical, false);
                    return;
                }
                node = VisualTreeHelper.GetParent(node);
            }
        }

        /// <summary>独立方形出现：排布瞬时落定（邻居交给 FLIP），背景自己从 0 淡入、从 0.82 长到 1。</summary>
        /// <remarks>
        /// 以前这里是"方形瞬间在位、图标自己弹出"，展开侧根本没有背景动作，
        /// 收起侧也就无从倒放——用户 m01615 要的正是"组件 + 组件背景当成一个组件"，两边同一套动作。
        /// 排布不在这里补间：展开时邻居的让位由 FLIP 负责，再叠一条排布动画会和它互相抵消。
        /// </remarks>
        private static void BeginSeparateSegmentShow(Border border, bool vertical)
        {
            ApplySegmentSettled(border, vertical, true);
            if (!_animateVisibilityChange) return;
            AnimateSegmentUnitEnter(border, vertical);
        }

        /// <summary>独立方形的背景退场：淡出 + 缩回 0.82，与段内组件同一条曲线、同一时长。</summary>
        private static void AnimateSegmentUnitExit(Border border, bool vertical)
        {
            var profile = ResolveMotionProfile();
            var duration = TimeSpan.FromSeconds(profile.Seconds);
            var scale = EnsureSegmentUnitScale(border);

            border.BeginAnimation(UIElement.OpacityProperty, null);
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);

            double fromOpacity = Math.Max(0.0, Math.Min(1.0, border.Opacity));
            _enteringUnits.Remove(border);
            scale.ScaleX = 1.0;
            scale.ScaleY = 1.0;

            var fade = new System.Windows.Media.Animation.DoubleAnimation(fromOpacity, 0.0, duration)
            {
                EasingFunction = CreateExitEasing()
            };
            fade.Completed += (_, _) =>
            {
                if (_exiting.TryGetValue(border, out var plan)) plan.UnitDone = true;
                TryFinishSeparateSegmentHide(border);
            };
            border.BeginAnimation(UIElement.OpacityProperty, fade);

            scale.BeginAnimation(ScaleTransform.ScaleXProperty,
                new System.Windows.Media.Animation.DoubleAnimation(1.0, ToolbarItemShowStartScale, duration)
                {
                    EasingFunction = CreateExitEasing()
                });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty,
                new System.Windows.Media.Animation.DoubleAnimation(1.0, ToolbarItemShowStartScale, duration)
                {
                    EasingFunction = CreateExitEasing()
                });

            MotionTrace($"独立段收起 背景倒放 {Describe(border)} 1→0 + 缩放 1→{ToolbarItemShowStartScale:F2} 时长={profile.Seconds * 1000:F0}ms");
        }

        /// <summary>独立方形的背景入场：0 → 1 淡入、0.82 → 1 长回原尺寸。</summary>
        private static void AnimateSegmentUnitEnter(Border border, bool vertical)
        {
            var profile = ResolveMotionProfile();
            var duration = TimeSpan.FromSeconds(profile.Seconds);
            var scale = EnsureSegmentUnitScale(border);

            border.BeginAnimation(UIElement.OpacityProperty, null);
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);

            scale.ScaleX = ToolbarItemShowStartScale;
            scale.ScaleY = ToolbarItemShowStartScale;
            border.Opacity = 0.0;

            // 排布已经回来了，眼睛还没看见它：记下这块跨度，由跟随按不透明度慢慢加回去
            double footprint = vertical
                ? border.ActualHeight + border.Margin.Top + border.Margin.Bottom
                : border.ActualWidth + border.Margin.Left + border.Margin.Right;
            if (footprint <= 0.5) footprint = 58.0;
            _enteringUnits[border] = footprint;

            var fade = new System.Windows.Media.Animation.DoubleAnimation(0.0, 1.0, duration)
            {
                EasingFunction = CreateEnterEasing()
            };
            fade.Completed += (_, _) => ResetSegmentUnitVisual(border);
            border.BeginAnimation(UIElement.OpacityProperty, fade);

            scale.BeginAnimation(ScaleTransform.ScaleXProperty,
                new System.Windows.Media.Animation.DoubleAnimation(ToolbarItemShowStartScale, 1.0, duration)
                {
                    EasingFunction = CreateEnterEasing()
                });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty,
                new System.Windows.Media.Animation.DoubleAnimation(ToolbarItemShowStartScale, 1.0, duration)
                {
                    EasingFunction = CreateEnterEasing()
                });

            MotionTrace($"独立段展开 背景入场 {Describe(border)} 0→1 + 缩放 {ToolbarItemShowStartScale:F2}→1 时长={profile.Seconds * 1000:F0}ms");
        }

        /// <summary>独立方形自己的缩放通道（不是组件那条位置通道）。</summary>
        private static ScaleTransform EnsureSegmentUnitScale(Border border)
        {
            if (border.RenderTransform is ScaleTransform existing) return existing;

            var scale = new ScaleTransform(1, 1);
            border.RenderTransform = scale;
            border.RenderTransformOrigin = new Point(0.5, 0.5);
            return scale;
        }

        /// <summary>把独立方形的背景视觉归位：撤销淡出/缩放，摘掉残留动画。</summary>
        private static void ResetSegmentUnitVisual(Border border)
        {
            if (border == null || !IsSeparateSegment(border)) return;

            _enteringUnits.Remove(border);
            border.BeginAnimation(UIElement.OpacityProperty, null);
            border.Opacity = 1;
            if (border.RenderTransform is ScaleTransform scale)
            {
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                scale.ScaleX = 1;
                scale.ScaleY = 1;
            }
        }

        /// <summary>退场中的独立方形还画着的跨度（按当前不透明度加权）。</summary>
        /// <remarks>
        /// 独立方形的排布在收起开始就被让出去了，但它还要淡出一整段。居中跟随只看布局跨度的话，
        /// 收起第一帧就少掉整个方形（58px → 整条栏心跳 29px，用户 m01612 的"瞬间往右闪"）。
        /// 把这块按不透明度折算回去，跨度就是连续的：开始一帧不掉、淡出中平滑减少、折掉时正好归零。
        /// </remarks>
        internal static double GetExitingVisualExtent(bool vertical)
        {
            if (_exiting.Count == 0) return 0;

            double sum = 0;
            foreach (var pair in _exiting)
            {
                var plan = pair.Value;
                var element = pair.Key;
                if (plan == null || element == null || !plan.SeparateUnit) continue;
                if (element.Visibility != Visibility.Visible) continue;

                double footprint = vertical
                    ? plan.Margin.Bottom - element.Margin.Bottom
                    : plan.Margin.Right - element.Margin.Right;
                if (footprint <= 0.5) continue;

                double opacity = Math.Min(1.0, Math.Max(0.0, element.Opacity));
                if (opacity <= 0.01) continue;
                sum += footprint * opacity;
            }
            return sum;
        }

        /// <summary>入场中的独立方形还没被眼睛看见的那部分跨度（按不透明度加权）。</summary>
        /// <remarks>
        /// 展开时方形的排布是瞬时回来的，但背景要从 0 淡入一整段。跟随只按布局量跨度的话，
        /// 展开第一帧就多算整个方形（58px → 整条栏心跳 29px，就是用户"展开会闪"那一下）。
        /// 这里把它还没显形的那部分扣掉：展开第一帧跨度不变，淡入过程中平滑把这块让出来。
        /// </remarks>
        internal static double GetEnteringVisualExtent(bool vertical)
        {
            if (_enteringUnits.Count == 0) return 0;

            double sum = 0;
            foreach (var pair in _enteringUnits)
            {
                var element = pair.Key;
                if (element == null) continue;

                double footprint = pair.Value;
                if (footprint <= 0.5) continue;

                double opacity = Math.Min(1.0, Math.Max(0.0, element.Opacity));
                if (opacity >= 0.999) continue;
                sum += footprint * (1.0 - opacity);
            }
            return sum;
        }

        /// <summary>每条位移通道当前挂着的那条动画的逐帧回调，用来在打断时把它摘掉。</summary>
        /// <remarks>
        /// 没有这张表时，每次 StartChannelMotion 都往 CompositionTarget.Rendering 上挂一个新的闭包，
        /// 而旧的闭包要等自己收敛才退订。显隐一旦被快速打断，两条弹簧就会同时往同一条通道写值，
        /// 互相把对方的位置覆盖掉——观感是抖动加走错方向，而且从代码上完全看不出来。
        /// </remarks>
        private static readonly Dictionary<TranslateTransform, EventHandler> _channelMotions =
            new Dictionary<TranslateTransform, EventHandler>();

        /// <summary>
        /// 用一条时间曲线驱动位移动画通道（Inkeys 同款：固定时长 + 回弹）。
        /// </summary>
        /// <remarks>
        /// 为什么不再是弹簧：
        /// 弹簧的收敛时间由 ω 与距离共同决定，1 格（46px）约 213ms、2 格约 246ms，
        /// 而透明度与缩放是固定时长——同一批次里几套节奏错开收尾，观感就是"各动各的"。
        /// Inkeys 的做法正好相反：所有值共享**一个时长**，位置走 EaseOutBack（出现）
        /// 或它的时间反演 EaseInBack（收起），于是整批同帧起、同帧收；
        /// "远的快、近的慢"不是靠物理，而是同一个时长里走得更远。
        ///
        /// 打断：先摘掉上一条，起点改成通道的当前值，
        /// 因此被下一次显隐打断时会从"正在显示的位置"继续，不会跳回起点。
        /// 每个元素每条通道只允许一条动画在跑，后发起的接管前者。
        /// 收敛时回调 onSettled，供"等位移走完再落定"的一次性收尾使用。
        /// </remarks>
        private static void StartChannelMotion(
            TranslateTransform translate, bool vertical, double from, double to, bool collapse,
            Action onSettled = null)
        {
            if (translate == null) return;

            DependencyProperty axisProperty = vertical
                ? TranslateTransform.YProperty
                : TranslateTransform.XProperty;

            // 先摘掉可能残留的旧动画，并把当前值读出来当起点（合成线程上的值也要读准）
            double current = vertical ? translate.Y : translate.X;
            translate.BeginAnimation(axisProperty, null);
            if (vertical) translate.Y = current;
            else translate.X = current;

            // 接管：打断时把上一条弹簧摘掉，否则两条闭包会同时往同一条通道写值
            bool interrupted = false;
            if (_channelMotions.TryGetValue(translate, out var previous))
            {
                if (previous != null) CompositionTarget.Rendering -= previous;
                _channelMotions.Remove(translate);
                interrupted = true;
            }

            // 起点：新起的通道用调用方给的 from；被打断的从"正在显示的位置"接着走，
            // 不能退回 from，否则会先跳回起点再跑一遍。
            double start = interrupted && !double.IsNaN(current) ? current : from;
            if (double.IsNaN(start)) start = from;

            if (Math.Abs(start - to) < 0.5)
            {
                if (vertical) translate.Y = to;
                else translate.X = to;
                onSettled?.Invoke();
                return;
            }

            var profile = ResolveMotionProfile();
            double distance = to - start;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            double elapsed = 0;
            EventHandler onFrame = null;

            onFrame = (_, _) =>
            {
                double frameSeconds = clock.Elapsed.TotalSeconds - elapsed;
                if (frameSeconds <= 0) return;
                elapsed += frameSeconds;

                double progress = profile.Seconds <= 0 ? 1.0 : Math.Min(1.0, elapsed / profile.Seconds);
                double value = start + distance * ApplyMotionEasing(collapse, progress, profile.Back);
                if (vertical) translate.Y = value;
                else translate.X = value;

                if (progress < 1.0) return;

                if (vertical) translate.Y = to;
                else translate.X = to;
                CompositionTarget.Rendering -= onFrame;
                if (_channelMotions.TryGetValue(translate, out var running) && ReferenceEquals(running, onFrame))
                {
                    _channelMotions.Remove(translate);
                }
                onSettled?.Invoke();
            };

            _channelMotions[translate] = onFrame;
            CompositionTarget.Rendering += onFrame;
        }

        /// <summary>停掉一条通道上的动画（不改通道的值）。</summary>
        private static void StopChannelMotion(TranslateTransform translate)
        {
            if (translate == null) return;
            if (_channelMotions.TryGetValue(translate, out var handler))
            {
                if (handler != null) CompositionTarget.Rendering -= handler;
                _channelMotions.Remove(translate);
            }
        }

        /// <summary>一次曲线补间的参数：时长（秒）与回弹强度。</summary>
        private struct MotionProfile
        {
            public double Seconds;
            public double Back;
        }

        /// <summary>
        /// 取这一批动画的时长与回弹强度（全组件、胶囊共用同一份：同帧起、同帧收）。
        /// </summary>
        /// <remarks>
        /// 基准取自 Inkeys 工具栏：400ms、back = 1.1（≈4.47% 过冲）。
        /// 设置里的手感预设不再改"弹簧刚度"，而是改**这一批的节奏**：
        /// 柔和更慢、回弹更小；灵敏更快、回弹更大。时长照旧吃全局动画倍率
        /// （透明度、缩放、位移、胶囊四者都用这一份，所以永远不会出现两套节奏）。
        /// </remarks>
        private static MotionProfile ResolveMotionProfile()
        {
            var appearance = SettingsManager.Settings?.Appearance;
            int preset = appearance?.ToolbarMotionPreset ?? 1;

            double baseMilliseconds = MotionCurveBaseMilliseconds;
            double back = MotionBackOvershoot;
            switch (preset)
            {
                case 0:
                    baseMilliseconds = MotionCurveBaseMilliseconds * 1.2;
                    back = 0.9;
                    break;
                case 2:
                    baseMilliseconds = MotionCurveBaseMilliseconds * 0.8;
                    back = 1.4;
                    break;
            }

            var scaled = AnimationSpeed.ScaleMilliseconds(baseMilliseconds);
            return new MotionProfile
            {
                // 0.1x 时 400ms 会变成 4s，仍然可用；只是别让 0 时长把进度除成 NaN
                Seconds = Math.Max(0.01, scaled.TotalSeconds),
                Back = back,
            };
        }

        /// <summary>出现用的位置曲线：EaseOutBack，冲过终点 4.47% 再落回（Inkeys 原值 back=1.1）。</summary>
        private static double EaseOutBack(double progress, double back)
        {
            double t = progress - 1.0;
            return 1.0 + (back + 1.0) * t * t * t + back * t * t;
        }

        /// <summary>收起用的位置曲线：EaseOutBack 的时间反演，先朝反方向蓄力再冲进来。</summary>
        private static double EaseInBack(double progress, double back)
        {
            return (back + 1.0) * progress * progress * progress - back * progress * progress;
        }

        /// <summary>透明度/缩放：出现用 EaseOutSine（先快后慢）。</summary>
        private static double EaseOutSine(double progress)
        {
            return Math.Sin(progress * Math.PI / 2.0);
        }

        /// <summary>透明度/缩放：收起用 EaseInSine（先慢后快），是上面那条的时间反演。</summary>
        private static double EaseInSine(double progress)
        {
            return 1.0 - Math.Cos(progress * Math.PI / 2.0);
        }

        /// <summary>按"展开/收起"选位置的曲线：收起就是展开的倒放。</summary>
        private static double ApplyMotionEasing(bool collapse, double progress, double back)
        {
            if (progress <= 0) return 0.0;
            if (progress >= 1.0) return 1.0;
            return collapse ? EaseInBack(progress, back) : EaseOutBack(progress, back);
        }

        /// <summary>出场缩放使用的通道。</summary>
        private const int ScaleChannelIndex = 0;

        /// <summary>位移通道：出场钻出与邻居让位共用这一条。</summary>
        private const int TranslateChannelIndex = 1;

        /// <summary>
        /// 取得（必要时重建）组件的两条动画通道。
        /// </summary>
        /// <remarks>
        ///   [0] 缩放    [1] 位移
        /// 两者会同时发生（新组件淡入并缩放，同时从邻居身下钻出），
        /// 共用一个变换会互相覆盖，只剩最后写进去的那条生效。
        ///
        /// 位移只留一条是有意的：出场钻出和邻居让位在几何上是同一件事——
        /// 都是从"切换前的排布"滑向"切换后的排布"，区别只在于新组件没有旧位置、
        /// 借用左邻的。拆成两条通道意味着同一方向上有两段位移叠加，
        /// 那正是之前"先往左再往右"的来源。
        ///
        /// 缩放排在位移之前是有意的：变换按顺序依次作用，
        /// 缩放若排在位移之后，平移量会被一起缩放，量出来的位置就对不上了。
        ///
        /// 重建时沿用已有通道上的值，避免抹掉正在播的动画位置。
        /// </remarks>
        private static TransformGroup EnsureChannelGroup(FrameworkElement element)
        {
            if (element.RenderTransform is TransformGroup group
                && group.Children.Count > TranslateChannelIndex
                && group.Children[ScaleChannelIndex] is ScaleTransform
                && group.Children[TranslateChannelIndex] is TranslateTransform)
            {
                return group;
            }

            // 沿用旧通道上的值：按类型扫一遍，不依赖下标。
            // 兼容上一版三通道的组（缩放 + 出场滑入 + 邻居让位）：取最后一条位移，
            // 那是"当前排布位置"对应的真实偏移，出场滑入那份是临时动画量。
            double translateX = 0, scale = 1;
            if (element.RenderTransform is TransformGroup old)
            {
                foreach (var child in old.Children)
                {
                    if (child is TranslateTransform translate) translateX = translate.X;
                    else if (child is ScaleTransform existingScale) scale = existingScale.ScaleX;
                }
            }

            var rebuilt = new TransformGroup();
            rebuilt.Children.Add(new ScaleTransform(scale, scale));
            rebuilt.Children.Add(new TranslateTransform(translateX, 0));
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
        /// 交给跨度弹簧推到新值（与组件同档，会一起过冲），结束后 ClearValue 交还自动尺寸。
        ///
        /// 段内按钮的位置瞬跳由"邻居让位"通道补偿；段外兄弟（含拖动头）的位置
        /// 跟着钉住的旧跨度走，随补间自然滑动，正好省掉对它们的额外补偿。
        ///
        /// 整段刚出现（出场动画已覆盖整段）或刚隐藏（瞬时收起的取舍）时跳过。
        /// 关掉动画开关时直接跳过，保持瞬时切换。横排补宽度，竖排补高度。
        /// </remarks>
        /// <summary>量分段在排布之后真正占的跨度；量不到返回 0。</summary>
        /// <remarks>
        /// 这是唯一可信的目标值来源：Measure(∞) 拿到的是 DesiredSize，而 DesiredSize 与
        /// 布局实排并不是同一个数——实测展开时量出 721 而实排 718、收起时量出 637 而实排 381
        /// （子项间距与内边距只在排列阶段才算进去）。拿 DesiredSize 当补间目标，
        /// 收尾清掉固定尺寸后就会啪地跳回真值：末帧几 px 的跳变、收起时几百 px 的跳变
        /// 都是这一个偏差造成的。
        /// UpdateLayout 只重排这一段所在的子树，而且只在每次过渡的起点与收尾跑，不是逐帧。
        /// </remarks>
        private static double ReadLaidOutExtent(Border border, bool vertical)
        {
            if (border == null) return 0;
            try
            {
                // 改 Margin 不会把缓存的 DesiredSize 标脏，先标脏再强制排版，读到的才是新排布
                if (border.Child is FrameworkElement content) content.InvalidateMeasure();
                border.InvalidateMeasure();
                border.UpdateLayout();
                double extent = vertical ? border.ActualHeight : border.ActualWidth;
                if (double.IsNaN(extent) || double.IsInfinity(extent) || extent < 0) return 0;
                return extent;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>目标跨度：优先用真实排布值，量不到再退回 DesiredSize。</summary>
        private static double MeasureSettledExtent(Border border, bool vertical)
        {
            double laidOut = ReadLaidOutExtent(border, vertical);
            return laidOut > 0 ? laidOut : MeasureNaturalExtent(border, vertical);
        }

        /// <summary>量分段在内容自然排布下的跨度；量不到返回 0。</summary>
        /// <remarks>
        /// 用 <see cref="UIElement.Measure(Size)"/> 而不是 window.UpdateLayout：
        /// 后者会把整棵可视化树重排一遍，每次内容变化都付一次整窗布局的钱，
        /// 展开/收起那半秒的掉帧主要就是从这里来的。这里只量这一段。
        /// </remarks>
        private static double MeasureNaturalExtent(Border border, bool vertical)
        {
            if (border == null) return 0;
            try
            {
                // 先标脏再量：子项"让出布局"是靠改 Margin 实现的，WPF 不会因此把内容面板
                // 缓存的 DesiredSize 标脏（实测：之后 Measure(∞) 直接早退，读到的还是
                // 收缩前的跨度 721，于是"目标没变"、整段胶囊一路不缩，收尾时再一下跳过去）。
                if (border.Child is FrameworkElement content) content.InvalidateMeasure();
                border.InvalidateMeasure();
                border.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                double extent = vertical ? border.DesiredSize.Height : border.DesiredSize.Width;
                if (double.IsNaN(extent) || double.IsInfinity(extent) || extent < 0) return 0;
                return extent;
            }
            catch
            {
                return 0;
            }
        }

        private static void AnimateBorderExtentToFit(Border border, bool vertical, double beforeExtent, bool wasVisible)
        {
            if (border == null || !_animateVisibilityChange)
            {
                MotionTrace($"跨度补间跳过 {Describe(border)}：关动画或空");
                return;
            }
            // 整段刚出现（出场动画已覆盖）或刚隐藏（瞬时收起的取舍）时不做跨度补间
            if (!wasVisible || border.Visibility != Visibility.Visible)
            {
                MotionTrace($"跨度补间跳过 {Describe(border)}：原本可见={wasVisible} 当前可见={border.Visibility == Visibility.Visible}");
                return;
            }

            DependencyProperty extentProperty = vertical
                ? FrameworkElement.HeightProperty
                : FrameworkElement.WidthProperty;

            try
            {
                // 屏幕上此刻的跨度：优先取上一轮最后写下去的值。
                // 不能只读 ActualWidth：刚写完 Width 还没跑布局，ActualWidth 还是旧跨度，
                // 读它就会得出"起点 == 目标"，于是先撤销再跳过，宽度瞬跳过去。
                SegmentExtentMotion previous = null;
                bool carried = _extentMotions.TryGetValue(border, out previous) && previous != null;
                double liveExtent = carried ? previous.Current : beforeExtent;
                if (liveExtent <= 0)
                {
                    liveExtent = vertical ? border.ActualHeight : border.ActualWidth;
                }

                // 量目标跨度必须先把补间写下去的固定尺寸摘掉，否则 Measure 只会读到它。
                // 摘掉到写回之间不跑布局，下一帧就会写回当前值，看不到中间态。
                border.BeginAnimation(extentProperty, null);
                border.ClearValue(extentProperty);
                // 目标跨度必须是「排布之后真正占多少」，不能是 DesiredSize（见 MeasureSettledExtent）
                double settledExtent = MeasureSettledExtent(border, vertical);

                double collapsedExtent = Math.Min(liveExtent, settledExtent);
                double expandedExtent = Math.Max(liveExtent, settledExtent);

                // 两端都没变：让上一轮原样跑完——不摘状态、不翻方向、不重置时钟。
                // 一次模式切换往往连着跑好几遍同步，这里要是每遍都"从当前位置重新起一轮"，
                // 曲线就被反复从 0 进度重放，看到的正是"背景粘住不动、最后一下窜到位"。
                if (carried && previous != null && !previous.Finished
                    && Math.Abs(previous.Start - collapsedExtent) < 1.0
                    && Math.Abs(previous.Target - expandedExtent) < 1.0)
                {
                    WriteSegmentExtent(border, vertical, previous.Current);
                    EnsureExtentMotionHook();
                    MotionTrace($"跨度补间接续 {Describe(border)} 进度={previous.Progress:F2} 当前={previous.Current:F1} 端点={previous.Start:F1}/{previous.Target:F1}（端点未变 继续跑）");
                    return;
                }

                if (settledExtent <= 0 || liveExtent <= 0 || Math.Abs(settledExtent - liveExtent) < 0.5)
                {
                    if (carried) StopExtentMotion(border, trace: false);
                    MotionTrace($"跨度补间跳过 {Describe(border)}：当前={liveExtent:F1} 目标={settledExtent:F1}（差 {(settledExtent - liveExtent):F1}）");
                    WriteSegmentExtent(border, vertical, liveExtent);
                    border.ClipToBounds = false;
                    NotifyExtentChanged(vertical);
                    return;
                }

                // 只换端点、进度由 RunExtentMotion 按当前跨度反解，方向由两端谁大谁小决定：
                // 变长 = 进度 0→1，变短 = 进度 1→0——同一条 EaseOutBack，后者就是前者的时间反演，
                // 与组件位移同一时长同一条曲线，于是胶囊的过冲与组件的过冲同时发生、同时收住。
                // 内容变化时胶囊跟着内容走，不裁剪：裁掉就看不到"背景跟着内容一起长"。
                border.ClipToBounds = false;
                RunExtentMotion(border, vertical,
                    collapsedExtent,
                    expandedExtent,
                    collapsing: settledExtent < liveExtent - 0.5,
                    hideWhenSettled: false);
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
        /// 段内按钮同时各播一次淡入与缩放：整段变宽是"外壳"那层的动作，
        /// 淡入是"东西终于有了"那层的动作，两层叠着才不显得空。
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

            MotionTrace($"分段过渡 {Describe(border)} 内容可见={shouldShow} 原本可见={wasVisible} 之前跨度={beforeExtent:F1} 实际跨度={(vertical ? border.ActualHeight : border.ActualWidth):F1}");

            if (!_animateVisibilityChange)
            {
                // 关动画也要把倒放留下的中间态收干净：跨度弹簧、裁剪、图层与排布
                CancelSegmentHide(border);
                StopExtentMotion(border);
                border.BeginAnimation(extentProperty, null);
                border.ClearValue(extentProperty);
                border.BeginAnimation(UIElement.OpacityProperty, null);
                border.Opacity = 1;
                border.ClipToBounds = false;
                border.Visibility = shouldShow ? Visibility.Visible : Visibility.Collapsed;
                if (!shouldShow) CollapseSegmentLeaves(border.Child as FrameworkElement);
                return;
            }

            // 又要它显示：若上一轮收起还没收完，先撤掉收起状态，
            // 让下面的跨度补间从**当前跨度**接着长回去（不跳回 0）。
            if (shouldShow) CancelSegmentHide(border);

            // 独立边框段：胶囊是固定方形（宽 = 高），两个方向都不碰跨度。
            // 展开侧它在位（图标自己弹出来），收起侧按倒放就是"图标先退回去、
            // 方形背景最后才收"——不缩宽、不淡出（用户 m01461）。
            if (IsSeparateSegment(border))
            {
                if (shouldShow)
                {
                    BeginSeparateSegmentShow(border, vertical);
                }
                else
                {
                    BeginSeparateSegmentHide(border, vertical);
                }
                return;
            }

            // 一直显示、只是内容增减：走内容变化的跨度补间（不碰 Visibility，安全）
            if (shouldShow && wasVisible)
            {
                AnimateBorderExtentToFit(border, vertical, beforeExtent, true);
                return;
            }

            // 隐藏：倒放收起——胶囊从当前跨度缩回 0，段内组件跟着一起被裁掉，收完才 Collapsed。
            //
            // 这里曾经是"瞬时收起"，因为试过补间到 0 之后踩到两类确定性故障：
            //   1. 补间被打断（0.1x 倍率下一次要 2.2 秒，几乎必然被打断）时代际守卫让 Completed
            //      提前 return，Collapsed 再也不会执行，分段卡在 Visible + 宽度 0；
            //      下一轮把 Visibility 当真值读，判据从此全错。
            //   2. 段内内容已经瞬时收起，屏幕上只剩一个空壳胶囊，按距离算时长会挂两秒。
            // 现在两条都堵住了：段内组件不再提前折叠（改成幻影排布、跟着胶囊一起被裁），
            // 打断时由新的一轮从当前跨度接管（CancelSegmentHide + AnimateBorderExtentToFit）。
            if (!shouldShow)
            {
                if (TryBeginSegmentHide(border, vertical)) return;
                ApplySegmentSettled(border, vertical, false);
                return;
            }

            // 从无到有：从 0 跨度弹到目标值（与组件同一条弹簧，过冲与收尾一起走）
            try
            {
                // 被打断时从当前可见跨度接着长，不退回 0
                bool wasSpringing = IsExtentMoving(border);
                StopExtentMotion(border);
                border.BeginAnimation(extentProperty, null);
                border.ClearValue(extentProperty);
                border.BeginAnimation(UIElement.OpacityProperty, null);
                border.Opacity = 1;

                border.Visibility = Visibility.Visible;
                border.UpdateLayout();
                double targetExtent = vertical ? border.ActualHeight : border.ActualWidth;
                if (targetExtent <= 0)
                {
                    // 量不到尺寸（尚未进视觉树）：直接落定，不要卡在 0 跨度
                    ApplySegmentSettled(border, vertical, true);
                    return;
                }

                double growFrom = wasSpringing && beforeExtent > 0 && beforeExtent < targetExtent
                    ? beforeExtent
                    : 0.0;

                if (targetExtent - growFrom < 1.0)
                {
                    ApplySegmentSettled(border, vertical, true);
                    return;
                }

                // 裁剪溢出，否则固定尺寸的按钮会画到胶囊外
                border.ClipToBounds = true;
                RunExtentMotion(border, vertical, collapsedExtent: growFrom, expandedExtent: targetExtent,
                    collapsing: false, hideWhenSettled: false);
            }
            catch (Exception ex)
            {
                ApplySegmentSettled(border, vertical, true);
                LogHelper.WriteLogToFile($"ToolbarRegistry: 分段显隐补间失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        /// <summary>
        /// 把分段落定到终态：清掉动画与固定值，按期望设置 Visibility。
        /// </summary>
        /// <remarks>
        /// 这是隐藏与"补间收尾"的**唯一出口**。清状态放在最前面、且不受任何守卫影响，
        /// 是为了避免出现"状态没清、Visibility 没落"的中间态——
        /// 一旦出现，下一轮把 Visibility 当真值读就会连环错下去。
        /// </remarks>
        private static void ApplySegmentSettled(Border border, bool vertical, bool shown)
        {
            if (border == null) return;

            // 收起倒放没走完就落定（被打断、异常）：排布与图层也要一起还原
            if (_exiting.TryGetValue(border, out var exitPlan))
            {
                _exiting.Remove(border);
                border.Margin = exitPlan.Margin;
                Panel.SetZIndex(border, exitPlan.ZIndex);
            }

            // 独立方形的背景动画（淡出/缩回）也要归位，别把 0 不透明度带进下一次显示
            ResetSegmentUnitVisual(border);

            // 跨度交给这里落定：还在跑的弹簧必须撤掉，否则它下一帧又把固定值写回来
            StopExtentMotion(border);

            DependencyProperty extentProperty = vertical
                ? FrameworkElement.HeightProperty
                : FrameworkElement.WidthProperty;

            border.BeginAnimation(extentProperty, null);
            border.ClearValue(extentProperty);

            // 取消「整段淡出」时可能正停在半透明上：直接跳到 1 会闪一下。
            // 只有真在显示、并且确实淡到一半时才补一段短淡入，其余一律瞬时置 1。
            double opacityNow = border.Opacity;
            border.BeginAnimation(UIElement.OpacityProperty, null);
            if (shown && _animateVisibilityChange && opacityNow > 0.01 && opacityNow < 0.99)
            {
                var refade = new System.Windows.Media.Animation.DoubleAnimation(
                    opacityNow, 1.0, AnimationSpeed.ScaleMilliseconds((int)(ToolbarItemShowDurationMs * 0.3)));
                refade.EasingFunction = CreateEnterEasing();
                border.BeginAnimation(UIElement.OpacityProperty, refade);
            }
            else
            {
                border.Opacity = 1;
            }

            border.ClipToBounds = false;
            border.Visibility = shown ? Visibility.Visible : Visibility.Collapsed;

            // 段落下之后把段内状态收干净：收起动画期间组件被故意留成 Visible，
            // 段一旦 Collapsed、这个中间态就必须清掉，否则下一次显示时
            // 它们会被当成"本来就在场"，出场动画和 FLIP 起点全都没了。
            if (!shown) CollapseSegmentLeaves(border.Child as FrameworkElement);

            NotifyExtentChanged(vertical);
        }

        /// <summary>把段内还留成 Visible 的注入组件收干净（只碰 Tag==InjectedTag 的组件）。</summary>
        private static void CollapseSegmentLeaves(FrameworkElement element)
        {
            if (element == null) return;

            if (element.Tag as string == InjectedTag)
            {
                // 分组容器不是组件：把里面的组件逐个收干净，容器自己最后才折。
                // 直接把容器 Collapsed 会把还留成 Visible 的组件留在里面，
                // 下次显示时它们"本来就在场"，出场动画和 FLIP 起点全没了。
                if (element is Panel groupContainer)
                {
                    foreach (UIElement child in groupContainer.Children)
                    {
                        CollapseSegmentLeaves(child as FrameworkElement);
                    }
                    if (HasVisibleLeafContent(groupContainer) || HasExitingDescendant(groupContainer)) return;
                    groupContainer.Visibility = Visibility.Collapsed;
                    return;
                }

                if (_exiting.ContainsKey(element)) FinishHide(element, collapse: true);
                else if (element.Visibility == Visibility.Visible) element.Visibility = Visibility.Collapsed;
                return;
            }

            if (element is Panel panel)
            {
                foreach (UIElement child in panel.Children)
                {
                    CollapseSegmentLeaves(child as FrameworkElement);
                }
                return;
            }

            if (element is Border border)
            {
                CollapseSegmentLeaves(border.Child as FrameworkElement);
                return;
            }

            if (element is ContentControl contentControl)
            {
                CollapseSegmentLeaves(contentControl.Content as FrameworkElement);
            }
        }

        /// <summary>分段胶囊当前动画的代际号：每次发起新动效都自增，用来作废旧动画的收尾回调。</summary>
        /// <remarks>
        /// 动画倍率可以被调到 0.1x，一次出场要 2.2 秒；期间几乎一定会被下一次显隐打断。
        /// 被打断的旧动画照样会走 Completed，若它在那里 ClearValue，
        /// 就会把**新**动画刚钉住的跨度清掉——表现是背景抽搐一下再跳到位。
        /// 代际守卫让只有"当前这一轮"的回调才有权收尾。
        /// </remarks>
        /// <summary>是否还有分段的跨度补间在跑（居中据此决定要不要逐帧跟随）。</summary>
        /// <remarks>
        /// 判据就是 _extentMotions 本身。这里曾经有一对"活动集合 + 代际号"的守卫：
        /// 代际号字典只增不减、补间过一次就永远判成"被打断"；活动集合又因为 Completed 里的
        /// 守卫提前 return 而清不掉，两个都是确定性的状态泄漏。跨度改成"同一分段只允许
        /// 一条补间、后发起的直接替换"之后，它们都不需要了。
        /// </remarks>
        public static bool IsExtentAnimating => _extentMotions.Count > 0;

        /// <param name="ancestorsHidden">
        /// 上层分组是否已经判成隐藏。分组容器自己不再动画，而是把条件往下传，
        /// 由每个分组内组件各自出场/倒放。
        /// </param>
        private static void UpdatePanelVisibility(Panel panel, Dictionary<string, bool> context, bool ancestorsHidden = false)
        {
            if (panel == null) return;

            // 朝向决定了组件显隐时沿哪个轴滑入、让位，一次算好传给下面所有调用
            bool vertical = IsVerticalPanel(panel);

            foreach (var child in panel.Children.OfType<FrameworkElement>())
            {
                if (child.Tag as string == InjectedTag)
                {
                    var ruleset = GetHidingRuleset(child);
                    bool shouldHide = ruleset != null && EvaluateRuleset(ruleset, context);

                    // 分组容器（带注入标记的 Panel）不是组件：它只负责排版，
                    // 把"分组这一层的条件"往下传，显隐与动画交给每个分组内组件。
                    // 面板自己也 SetAnimatedVisibility 的话，整组会作为一个方块淡入淡出，
                    // 收起时也只会整组退回，看不出哪个组件退到哪儿去。
                    if (child is Panel groupContainer)
                    {
                        bool hiddenBelow = ancestorsHidden || shouldHide;
                        if (!hiddenBelow)
                        {
                            if (groupContainer.Visibility != Visibility.Visible)
                            {
                                groupContainer.Visibility = Visibility.Visible;
                            }
                            RevealGroupContainers(groupContainer);
                        }
                        UpdatePanelVisibility(groupContainer, context, hiddenBelow);
                        // 容器只做落定：里面还有活着的子组件就不能折，
                        // 折了会把正在倒放的组件一起切掉
                        SettleGroupContainers(groupContainer);
                        continue;
                    }

                    SetAnimatedVisibility(
                        child,
                        ancestorsHidden || shouldHide ? Visibility.Collapsed : Visibility.Visible,
                        _animateVisibilityChange,
                        vertical);
                }
                if (child is Border border && border.Tag as string == ContentBorderTag)
                {
                    if (border.Child is StackPanel sp && sp.Tag as string == ContentPanelTag)
                    {
                        // 段内容显隐前后各量一次跨度：胶囊背景要跟着内容平滑变宽/变窄
                        bool wasSegmentVisible = border.Visibility == Visibility.Visible;
                        double beforeExtent = vertical ? border.ActualHeight : border.ActualWidth;
                        // 不要在这里抑制子组件的出场动画：
                        // 抑制会让段内每个按钮的缩放/透明度/滑入全部跳过，
                        // 结果是整段出现时只剩胶囊在长，组件"啪"地直接到位。
                        // 整段长大与组件各自出场是叠加效果，本来就该同时发生。
                        UpdatePanelVisibility(sp, context);
                        bool anyVisible = HasVisibleLeafContent(sp);
                        AnimateSegmentTransition(border, vertical, beforeExtent, wasSegmentVisible, anyVisible);
                    }
                    else if (border.Child is Grid grid)
                    {
                        // 同上：胶囊背景的跨度补间
                        bool wasSegmentVisible = border.Visibility == Visibility.Visible;
                        double beforeExtent = vertical ? border.ActualHeight : border.ActualWidth;
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
        /// 淡入 + 缩放的过渡，两者同长同曲，一起收尾。
        ///
        /// 位置**不在这里算**：新组件的起点取决于"左邻是谁"，只有拿到整轮排布才说得清，
        /// 所以交给 FLIP 那一遍统一处理（见 <see cref="AnimateChildrenToNewOffsets"/>）。
        /// 方法里再滑一次的话，两层位移会沿视觉树叠加、互相抵消，就成了旧版那种方向鬼畜。
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

            // 收起到一半又要它显示：撤掉倒放、还原完整排布，再按"从无到有"重新出场。
            // 先按一次 Collapsed 是为了统一走进下面那条 becameVisible 分支。
            if (target == Visibility.Visible && _exiting.TryGetValue(element, out var pending) && !pending.Segment)
            {
                FinishHide(element, collapse: false);
                element.Visibility = Visibility.Collapsed;
            }

            bool becameVisible = target == Visibility.Visible && element.Visibility != Visibility.Visible;
            bool becameHidden = target != Visibility.Visible && element.Visibility == Visibility.Visible;

            if (!animate)
            {
                // 动画被关掉：正在倒放的收起也要立刻落定，否则它永远占着零宽幻影排布
                if (_exiting.ContainsKey(element)) FinishHide(element, collapse: false);
                element.BeginAnimation(UIElement.OpacityProperty, null);
                element.Opacity = 1;
                ResetMotionChannels(element);
                element.Visibility = target;
                return;
            }

            if (becameVisible)
            {
                element.Visibility = Visibility.Visible;
                // 分组容器空着时是折叠的（不留 3px 缝），组件要出场先把它撑开，
                // 否则组件虽然 Visible，父级 Collapsed 时根本不会渲染、也就没有动画
                RevealGroupContainers(element);

                MotionTrace($"组件出场 {Describe(element)}：淡入+缩放 {ToolbarItemShowStartOpacity:F2}→1 / {ToolbarItemShowStartScale:F2}→1");

                var duration = AnimationSpeed.ScaleMilliseconds(ToolbarItemShowDurationMs);

                // 1. 透明度：无 → 有。
                // 新组件从邻居身下抽出来，淡入就是"抽出来"这件事的可见性信号，
                // 所以从 0 起；此处不再叠一层"半透明起步"，那会让它一开始就显形、像凭空出现。
                var fade = new System.Windows.Media.Animation.DoubleAnimation(
                    ToolbarItemShowStartOpacity, 1.0, duration)
                {
                    EasingFunction = CreateEnterEasing()
                };
                element.BeginAnimation(UIElement.OpacityProperty, fade);

                // 2. 位置：这里**不**动。从哪冒出来由 FLIP 那一遍统一决定
                // （起点=左邻的旧位置）；若这里再叠一层滑入，两个位移会互相抵消，
                // 正是之前"先往左再往右"那类鬼畜的来源。

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
                // 收起 = 展开的倒放（用户要求"和 popup 菜单一样倒放"）。
                //
                // 它先让出布局、但继续绘制：位移退到左邻身下、同时淡出并缩回 0.82，
                // 三者都在 FLIP 那一遍起播——倒放终点取决于"切换后谁还在场"，
                // 只有拿到整轮排布才说得清。收完才真正 Collapsed。
                // 父级已经收起来时 IsVisible 为假，没有倒放可放，直接落定。
                MotionTrace($"组件收起 {Describe(element)}：倒放（淡出+缩回+退回起点）");
                if (TryBeginHide(element, vertical)) return;

                MotionTrace($"组件收起 {Describe(element)}：瞬时落定");
                element.BeginAnimation(UIElement.OpacityProperty, null);
                element.Opacity = 1;
                ResetMotionChannels(element);
                element.Visibility = target;
                return;
            }

            // 状态未变：不重复播动画
            element.Visibility = target;
        }

        /// <summary>按钮出场用的缓动（SineEase EaseOut，Inkeys 同款透明度曲线）。</summary>
        /// <remarks>每条动画各取一个实例，避免多个动画共用同一个 Freezable。</remarks>
        private static System.Windows.Media.Animation.SineEase CreateEnterEasing()
        {
            return new System.Windows.Media.Animation.SineEase
            {
                EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
            };
        }

        /// <summary>收起用的缓动（SineEase EaseIn）：上面那条的时间反演，读起来才是"倒放"。</summary>
        private static System.Windows.Media.Animation.SineEase CreateExitEasing()
        {
            return new System.Windows.Media.Animation.SineEase
            {
                EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn
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
                    // 曲线在逐帧写值，不先摘掉的话归零后下一帧又会被写回去
                    StopChannelMotion(translate);
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
        /// 一批显隐动画的时长（毫秒）。取 Inkeys 工具栏的原值 400。
        /// 组件位移、邻居让位、透明度、缩放、胶囊跨度全都是这一个数：
        /// 同一批里任何一处时长不同步，收尾就会一前一后，看起来像几套动画在打架。
        /// 实际时长 = 这个数 ÷ 全局动画倍率。
        /// </summary>
        internal const int ToolbarItemShowDurationMs = 400;

        /// <summary>曲线补间的基准时长（毫秒，倍率换算前）。</summary>
        private const double MotionCurveBaseMilliseconds = ToolbarItemShowDurationMs;

        /// <summary>回弹强度：1.1 是 Inkeys 的原值，峰值过冲约 4.47%。</summary>
        private const double MotionBackOvershoot = 1.1;

        /// <summary>按钮出场时的起始透明度。</summary>
        /// <remarks>
        /// 从全透明起步是用户明确要求的："同时 b、c 不透明 0 逐渐变 100%"。
        /// 早期取 0.35 是为了掩盖滑入方向算错时的跳变——现在起点由 FLIP 保证连续，
        /// 那个补丁没有存在意义了。
        /// </remarks>
        internal const double ToolbarItemShowStartOpacity = 0.0;

        /// <summary>按钮出场时的起始缩放。</summary>
        internal const double ToolbarItemShowStartScale = 0.82;

        private static bool HasVisibleLeafContent(FrameworkElement element)
        {
            if (element == null) return false;
            // 正在倒放收起的组件不算在场：它在布局上已经让位，只是还在画。
            // 不排除的话，段内最后一个组件收起时 anyVisible 永远为真，整段就永远不会收。
            if (_exiting.ContainsKey(element)) return false;
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

        /// <summary>是否还有正在倒放收起的后代——它仍在绘制，只是已经不占布局。</summary>
        private static bool HasExitingDescendant(FrameworkElement element)
        {
            if (element == null) return false;
            foreach (var pair in _exiting)
            {
                if (pair.Value.Segment) continue;
                var candidate = pair.Key;
                if (candidate == null) continue;
                if (ReferenceEquals(candidate, element) || element.IsAncestorOf(candidate)) return true;
            }
            return false;
        }

        /// <summary>把分组容器的祖先链撑开：子组件要渲染，容器就不能是 Collapsed。</summary>
        private static void RevealGroupContainers(FrameworkElement element)
        {
            for (var node = element?.Parent; node != null; node = System.Windows.Media.VisualTreeHelper.GetParent(node))
            {
                if (node is not FrameworkElement fe || fe.Tag as string != InjectedTag || fe is not Panel) return;
                if (fe.Visibility != Visibility.Visible) fe.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// 分组容器空了就真折叠。
        /// </summary>
        /// <remarks>
        /// 容器只做排版、不参与动画，但它的左边距一直占着位置：
        /// 里面一个组件都不剩时若不折叠，工具栏中间会留一道缝、胶囊也多算 3px。
        /// 从刚收起的组件往上走，只有"无可见叶、也无正在倒放的后代"才折——
        /// 否则会把还没退完的邻居连同一刀切掉（Collapsed 是不渲染，不是透明）。
        /// </remarks>
        private static void SettleGroupContainers(FrameworkElement element)
        {
            for (var node = element?.Parent; node != null; node = System.Windows.Media.VisualTreeHelper.GetParent(node))
            {
                if (node is not FrameworkElement fe || fe.Tag as string != InjectedTag || fe is not Panel groupContainer) return;
                if (HasVisibleLeafContent(groupContainer) || HasExitingDescendant(groupContainer)) return;
                if (groupContainer.Visibility != Visibility.Collapsed)
                {
                    groupContainer.Visibility = Visibility.Collapsed;
                }
            }
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
