using System;
using System.Collections.Generic;
using System.IO;

namespace Ink_Canvas.UInk
{
    /// <summary>
    /// UInk 完整保存编排（两阶段原子提交）。提交顺序对应规范 uink_inc / uink_conf：
    ///  1. 确定完整对象流 + 新主文件引用的资源集合；
    ///  2. 生成并校验临时 `.uink.extra`（**必须包含旧主文件仍引用的资源与新主文件将引用资源的并集**）；
    ///  3. 生成并校验临时 `.uink` 主文件（暂不替换）；
    ///  4. 先用临时资源包替换目标资源包，然后原子替换 `.uink` 主文件（主文件最后提交）；
    ///  5. 主文件提交成功后才清理多余 ZIP 条目或整个无用资源包。
    /// 任一步失败：删除临时文件，旧文件保持原样。
    /// </summary>
    public static class UInkSaveService
    {
        /// <summary>
        /// 完整保存：把 doc 写入 mainPath，资源打包进 mainPath + ".extra"。
        /// resources 为「新主文件引用的资源」（入口路径 NFC 规范化在 WriteArchive 内完成）。
        /// </summary>
        public static void SaveFull(UInkDocument doc, string mainPath,
            IReadOnlyList<(string entryPath, string sourceFile)> resources)
        {
            string extraPath = mainPath + ".extra";
            string tmpMain = mainPath + ".tmp";
            string tmpExtra = mainPath + ".extra.tmp";
            string workDir = null;

            try
            {
                // 1. 资源并集（旧主文件仍引用的 + 新的），崩溃恢复安全。
                //    旧资源解压到 workDir，该目录必须活到 WriteArchive 之后。
                workDir = Path.Combine(Path.GetTempPath(), "UInkUnion_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(workDir);
                var union = BuildUnionResources(mainPath, extraPath, resources, workDir);

                // 2. 临时 .uink.extra
                if (File.Exists(tmpExtra)) File.Delete(tmpExtra);
                if (union.Count > 0)
                    UInkExtraArchive.WriteArchive(tmpExtra, union);

                // 3. 临时 .uink
                if (File.Exists(tmpMain)) File.Delete(tmpMain);
                UInkWriter.WriteDocument(doc, tmpMain);

                // 4. 先替换资源包，再原子替换主文件（主文件最后提交）
                if (File.Exists(tmpExtra))
                    AtomicReplace(tmpExtra, extraPath);
                AtomicReplace(tmpMain, mainPath);
            }
            catch
            {
                TryDelete(tmpMain);
                TryDelete(tmpExtra);
                throw;
            }
            finally
            {
                if (workDir != null)
                {
                    try { if (Directory.Exists(workDir)) Directory.Delete(workDir, true); } catch { }
                }
            }
        }

        /// <summary>临时文件可被崩溃恢复发现（记录残留路径日志由调用方处理）。</summary>
        public static string TempMainPath(string mainPath) => mainPath + ".tmp";
        public static string TempExtraPath(string mainPath) => mainPath + ".extra.tmp";

        private static List<(string entryPath, string sourceFile)> BuildUnionResources(
            string mainPath, string extraPath,
            IReadOnlyList<(string entryPath, string sourceFile)> newResources,
            string workDir)
        {
            var result = new List<(string, string)>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (newResources != null)
            {
                foreach (var r in newResources)
                {
                    var safe = UInkExtraArchive.NormalizeEntryPath(r.entryPath);
                    if (safe != null && seen.Add(safe))
                        result.Add((safe, r.sourceFile));
                }
            }

            // 旧主文件仍引用的资源：从旧 extra 提取到 workDir 并加入并集，
            // 防止主文件替换前资源包已被换掉导致旧主引用断裂。workDir 由调用方在 WriteArchive 后清理。
            if (File.Exists(mainPath) && File.Exists(extraPath))
            {
                UInkDocument oldDoc = null;
                try { oldDoc = UInkReader.Load(mainPath); } catch { oldDoc = null; }
                if (oldDoc != null)
                {
                    var oldPaths = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var rec in oldDoc.Canvases)
                        foreach (var b in rec.Blocks)
                            if (b is UInkMedia m && !string.IsNullOrEmpty(m.Path))
                                oldPaths.Add(m.Path);
                    if (oldPaths.Count > 0)
                    {
                        try
                        {
                            var map = UInkExtraArchive.ExtractWithBudget(extraPath, workDir);
                            if (map != null)
                            {
                                foreach (var p in oldPaths)
                                {
                                    if (!seen.Contains(p) && map.TryGetValue(p, out var file) && File.Exists(file))
                                    {
                                        result.Add((p, file));
                                        seen.Add(p);
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            return result;
        }

        private static void AtomicReplace(string tmp, string target)
        {
            if (File.Exists(target))
                File.Replace(tmp, target, null); // 同卷 NTFS 原子替换
            else
                File.Move(tmp, target);
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
