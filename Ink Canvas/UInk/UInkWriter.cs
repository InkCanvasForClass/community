using System;
using System.Collections.Generic;
using System.IO;

namespace Ink_Canvas.UInk
{
    /// <summary>
    /// UInk 主文件写入器。按对象流顺序写出 Header → HeaderExtension → (Canvas → 内容块)*。
    /// 完整保存走 <see cref="Save(UInkDocument, string)"/>；增量追加走 <see cref="AppendBlocks"/>。
    /// 两阶段提交（先 .uink.extra 后主文件）由 UInkSaveService 编排。
    /// </summary>
    public static class UInkWriter
    {
        public static void Save(UInkDocument doc, string path)
        {
            var tmp = path + ".tmp";
            try
            {
                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write))
                {
                    Save(doc, fs);
                }
                // 同目录移动替换是原子操作（Windows 同卷 NTFS）
                if (File.Exists(path))
                    File.Replace(tmp, path, null);
                else
                    File.Move(tmp, path);
            }
            catch
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                throw;
            }
        }

        public static void Save(UInkDocument doc, Stream stream)
        {
            UInkSerializer.WriteBlock(stream, doc.Header);
            if (doc.HeaderExtension != null)
                UInkSerializer.WriteBlock(stream, doc.HeaderExtension);
            foreach (var rec in doc.Canvases)
            {
                UInkSerializer.WriteBlock(stream, rec.Canvas);
                foreach (var b in rec.Blocks)
                    UInkSerializer.WriteBlock(stream, b);
            }
        }

        /// <summary>纯写入（无原子替换），供两阶段提交写临时文件使用。</summary>
        public static void WriteDocument(UInkDocument doc, string path)
        {
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            Save(doc, fs);
        }

        /// <summary>
        /// 增量追加：把已完成的 Ink/Shape/Media 块追加到文件末尾最后一个 Canvas 之后。
        /// 不更新 Header（增量语义：Header 统计可暂时落后，读取端按对象流重算）。
        /// </summary>
        public static void AppendBlocks(string path, IEnumerable<object> blocks)
        {
            using var fs = new FileStream(path, FileMode.Append, FileAccess.Write);
            foreach (var b in blocks)
                UInkSerializer.WriteBlock(fs, b);
        }

        /// <summary>按对象流统计 deviceNum / workspaceNum / pageNum（供完整保存重算 Header）。</summary>
        public static (uint deviceNum, uint workspaceNum, uint pageNum) ComputeStats(UInkDocument doc)
        {
            uint deviceNum = 0, workspaceNum = 0, pageNum = 0;
            if (doc.HeaderExtension != null)
            {
                deviceNum = (uint)(doc.HeaderExtension.Devices?.Count ?? 0);
                workspaceNum = (uint)(doc.HeaderExtension.Workspaces?.Count ?? 0);
            }
            if (deviceNum == 0) deviceNum = 1;   // 隐式单例 Device
            if (workspaceNum == 0) workspaceNum = 1; // 隐式单例 Workspace

            // pageNum：各 Workspace 逻辑页数之和，跨设备/跨图层共享 pageGuid 的同页只计一次
            var pageGuids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var rec in doc.Canvases)
            {
                if (!string.IsNullOrEmpty(rec.Canvas?.PageGuid))
                    pageGuids.Add(rec.Canvas.PageGuid);
            }
            pageNum = (uint)pageGuids.Count;
            return (deviceNum, workspaceNum, pageNum);
        }
    }
}
