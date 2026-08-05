using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Ink;
using Ink_Canvas.Helpers;

namespace Ink_Canvas.UInk
{
    /// <summary>一个 Canvas 的撤回适配结果：最终可见墨迹 + 可导入 ICC TimeMachine 的 delta 链。</summary>
    public sealed class UInkUndoAdaptation
    {
        /// <summary>最终可见墨迹（与 History 共享 Stroke 引用）。</summary>
        public StrokeCollection FinalStrokes = new StrokeCollection();

        /// <summary>delta 链：每 undoId 边界的 added→UserInput(false) / removed→UserInput(true)。</summary>
        public TimeMachineHistory[] History;
    }

    /// <summary>
    /// 加载方向撤回语义适配（UInk → ICC TimeMachine）。
    /// UInk 用 undoId 分组 + renderOnlyWhenLatest 末尾最新组表达撤回；ICC TimeMachine 是增量栈
    /// （ApplyHistoryToCanvas 对 UserInput：cleared=false 添加 CurrentStroke，true 移除）。
    /// 适配算法：
    ///  1. 按 undoId 分组（连续同值 = 一次撤回操作）；
    ///  2. 对每个前缀应用末尾最新组规则得到可见集 visibleSet(i)；
    ///  3. 相邻边界差集得 added/removed，生成 UserInput 历史项；
    ///  4. 最终可见集 = 载入画布的墨迹。此后 ICC 的 Undo 沿链逐步把「隐藏但未撤回的原稿」展现出来。
    /// Media 块不参与墨迹撤回链（其 undoId 分组语义由元素恢复管线处理，见 UInkIccMapper）。
    /// </summary>
    public static class UInkUndoAdapter
    {
        public static UInkUndoAdaptation Adapt(UInkCanvasRecord record, Func<IUInkContentBlock, Stroke> toStroke)
        {
            var result = new UInkUndoAdaptation();
            if (record?.Blocks == null) return result;

            // 只取 Ink/Shape（Media 不参与墨迹撤回链），并把每个块惰性转换为 Stroke（共享引用）
            var content = new List<IUInkContentBlock>();
            var strokeOf = new Dictionary<IUInkContentBlock, Stroke>();
            foreach (var b in record.Blocks)
            {
                if (!(b is UInkInk || b is UInkShape)) continue;
                var s = toStroke?.Invoke(b);
                if (s == null) continue;
                content.Add(b);
                strokeOf[b] = s;
            }
            if (content.Count == 0) return result;

            // 1. 按 undoId 分组
            var groups = GroupByUndoId(content);

            // 2. 每个前缀的可见集
            var visibleSets = new List<HashSet<IUInkContentBlock>>
            {
                new HashSet<IUInkContentBlock>(), // S_0 = 空
            };
            var prefix = new List<IUInkContentBlock>();
            for (int g = 0; g < groups.Count; g++)
            {
                prefix.AddRange(groups[g]);
                visibleSets.Add(ComputeVisibleSet(prefix));
            }

            // 3. 相邻边界差集 → delta 链
            var history = new List<TimeMachineHistory>();
            for (int i = 1; i < visibleSets.Count; i++)
            {
                var prev = visibleSets[i - 1];
                var cur = visibleSets[i];
                var added = cur.Where(x => !prev.Contains(x)).Select(x => strokeOf[x]).ToList();
                var removed = prev.Where(x => !cur.Contains(x)).Select(x => strokeOf[x]).ToList();
                if (added.Count > 0)
                    history.Add(new TimeMachineHistory(ToCollection(added), TimeMachineHistoryType.UserInput, false));
                if (removed.Count > 0)
                    history.Add(new TimeMachineHistory(ToCollection(removed), TimeMachineHistoryType.UserInput, true));
            }
            if (history.Count == 0)
                history.Add(new TimeMachineHistory(
                    ToCollection(content.Select(x => strokeOf[x])), TimeMachineHistoryType.UserInput, false));

            result.FinalStrokes = ToCollection(visibleSets[visibleSets.Count - 1].Select(x => strokeOf[x]));
            result.History = history.ToArray();
            return result;
        }

        private static List<List<IUInkContentBlock>> GroupByUndoId(List<IUInkContentBlock> content)
        {
            var groups = new List<List<IUInkContentBlock>>();
            List<IUInkContentBlock> cur = null;
            uint? lastUndo = null;
            foreach (var b in content)
            {
                uint undo = GetUndoId(b);
                if (cur == null || undo != lastUndo.Value)
                {
                    cur = new List<IUInkContentBlock>();
                    groups.Add(cur);
                    lastUndo = undo;
                }
                cur.Add(b);
            }
            return groups;
        }

        private static uint GetUndoId(IUInkContentBlock b) => b switch
        {
            UInkInk i => i.UndoId,
            UInkShape s => s.UndoId,
            _ => 0,
        };

        private static bool GetMarked(IUInkContentBlock b) => b switch
        {
            UInkInk i => i.RenderOnlyWhenLatest,
            UInkShape s => s.RenderOnlyWhenLatest,
            _ => false,
        };

        /// <summary>
        /// 对前缀应用末尾最新组规则：跳过 Media（此处已过滤），从尾部收集连续
        /// renderOnlyWhenLatest=true 的 Ink/Shape，遇到第一个未标记即停止。
        /// 可见 = 所有未标记块 + 末尾最新组；其他标记块隐藏。
        /// </summary>
        private static HashSet<IUInkContentBlock> ComputeVisibleSet(IReadOnlyList<IUInkContentBlock> prefix)
        {
            int runStart = prefix.Count;
            for (int i = prefix.Count - 1; i >= 0; i--)
            {
                if (GetMarked(prefix[i])) runStart = i;
                else break;
            }
            var visible = new HashSet<IUInkContentBlock>();
            for (int i = 0; i < prefix.Count; i++)
            {
                if (!GetMarked(prefix[i]) || i >= runStart)
                    visible.Add(prefix[i]);
            }
            return visible;
        }

        private static StrokeCollection ToCollection(IEnumerable<Stroke> strokes)
        {
            var c = new StrokeCollection();
            foreach (var s in strokes) c.Add(s);
            return c;
        }
    }
}
