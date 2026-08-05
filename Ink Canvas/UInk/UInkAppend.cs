using System;
using System.Collections.Generic;

namespace Ink_Canvas.UInk
{
    /// <summary>
    /// UInk 增量追加（可选崩溃保护路径）。只向既有对象流末尾最后一个 Canvas 追加已完整结束的
    /// Ink/Shape/Media，不更新 Header（读取端按对象流重算状态）。不满足追加条件时调用方应回退完整保存。
    /// </summary>
    public static class UInkAppend
    {
        /// <summary>
        /// 尝试增量追加。成功返回 true；文件不存在/非 UInk/无 Canvas 时返回 false。
        /// contentId 承接末尾 Canvas 序列（max+1 起）；undoId 从 max+1 起，每个块独立一次撤回操作。
        /// </summary>
        public static bool TryAppend(string path, IReadOnlyList<IUInkContentBlock> newBlocks)
        {
            if (newBlocks == null || newBlocks.Count == 0) return false;
            var doc = UInkReader.Load(path);
            if (doc == null || doc.Canvases.Count == 0) return false;
            var last = doc.Canvases[doc.Canvases.Count - 1];

            uint nextContentId = 0;
            uint maxUndoId = 0;
            foreach (var b in last.Blocks)
            {
                uint cid = GetContentId(b);
                if (cid >= nextContentId) nextContentId = cid + 1;
                uint uid = GetUndoId(b);
                if (uid > maxUndoId) maxUndoId = uid;
            }

            var blocks = new List<object>(newBlocks.Count);
            uint undo = maxUndoId + 1;
            foreach (var b in newBlocks)
            {
                SetIds(b, nextContentId++, undo++); // 每个块独立撤回组
                blocks.Add(b);
            }
            UInkWriter.AppendBlocks(path, blocks);
            return true;
        }

        private static uint GetContentId(IUInkContentBlock b) => b switch
        {
            UInkInk i => i.ContentId,
            UInkShape s => s.ContentId,
            UInkMedia m => m.ContentId,
            _ => 0,
        };

        private static uint GetUndoId(IUInkContentBlock b) => b switch
        {
            UInkInk i => i.UndoId,
            UInkShape s => s.UndoId,
            UInkMedia m => m.UndoId,
            _ => 0,
        };

        private static void SetIds(IUInkContentBlock b, uint contentId, uint undoId)
        {
            switch (b)
            {
                case UInkInk i: i.ContentId = contentId; i.UndoId = undoId; break;
                case UInkShape s: s.ContentId = contentId; s.UndoId = undoId; break;
                case UInkMedia m: m.ContentId = contentId; m.UndoId = undoId; break;
            }
        }
    }
}
