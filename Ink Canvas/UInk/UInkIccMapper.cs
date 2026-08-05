using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Ink;
using System.Windows.Media;
using Ink_Canvas.Helpers;

namespace Ink_Canvas.UInk
{
    /// <summary>一个页面的映射结果（加载方向）：Canvas + 最终墨迹 + 撤回链 + 媒体 + Shape。</summary>
    public sealed class UInkPageData
    {
        public UInkCanvas Canvas;
        public StrokeCollection FinalStrokes = new StrokeCollection();
        public TimeMachineHistory[] History;
        public List<UInkMedia> Media = new List<UInkMedia>();
        public List<UInkShape> Shapes = new List<UInkShape>();
    }

    /// <summary>保存方向的一页输入：Canvas 元数据 + 该页墨迹 + 该页媒体。</summary>
    public sealed class UInkPageInput
    {
        public UInkCanvas Canvas;
        public StrokeCollection Strokes;
        public List<UInkMedia> Media = new List<UInkMedia>();
    }

    /// <summary>
    /// ICC 运行态 ⇄ UInkDocument 的结构映射（屏幕→Display Device、模式→Workspace、页/PPT 幻灯片→Canvas、viewport）。
    /// 设备/工作区/页 GUID 规则见规范：pageGuid 全程唯一；(workspaceKey, deviceKey, pageGuid, layerIndex) 是 Canvas 唯一键。
    /// 写侧把当前可见状态归一化（undoId=0、全 renderOnlyWhenLatest=false）。
    /// </summary>
    public static class UInkIccMapper
    {
        // ==================== 保存方向 ====================

        /// <summary>把系统屏幕映射为 Display Device 列表（逻辑像素，支持负原点/多显示器）。</summary>
        public static List<UInkDevice> BuildDisplayDevices()
        {
            var devices = new List<UInkDevice>();
            foreach (Screen screen in Screen.AllScreens)
            {
                var bounds = screen.Bounds;
                devices.Add(new UInkDevice
                {
                    Guid = Guid.NewGuid().ToString(),
                    DeviceType = (int)UInkDeviceType.Display,
                    Name = screen.DeviceName,
                    DisplayX = bounds.X,
                    DisplayY = bounds.Y,
                    DisplayWidth = (uint)bounds.Width,
                    DisplayHeight = (uint)bounds.Height,
                });
            }
            return devices;
        }

        /// <summary>把一个 Workspace 注册项加入列表（同 GUID 去重）。</summary>
        public static UInkWorkspace EnsureWorkspace(List<UInkWorkspace> workspaces, UInkWorkspace ws)
        {
            var exist = workspaces.FirstOrDefault(x => x.Guid == ws.Guid);
            if (exist != null) return exist;
            workspaces.Add(ws);
            return ws;
        }

        /// <summary>构建一个 Canvas（用于写侧）。workspaceGuid/deviceGuid 为空时省略（隐式单例）。</summary>
        public static UInkCanvas BuildCanvas(string workspaceGuid, string deviceGuid,
            string pageGuid, uint pageIndex, uint pageNumber, int? slideId, UInkViewport viewport)
        {
            return new UInkCanvas
            {
                WorkspaceGuid = workspaceGuid ?? "",
                DeviceGuid = deviceGuid ?? "",
                PageGuid = pageGuid,
                PageIndex = pageIndex,
                PageNumber = pageNumber,
                LayerIndex = 0,
                LayerNumber = 0,
                SlideId = slideId,
                Viewport = viewport,
            };
        }

        /// <summary>默认 viewport：ICC 无持久画布变换时用恒等 {0,0,1}。</summary>
        public static UInkViewport IdentityViewport() => new UInkViewport { X = 0f, Y = 0f, Scale = 1f };

        /// <summary>汇总为 UInkDocument：写 Header/HeaderExtension、逐页写 Canvas+内容块、重算 Header 统计。</summary>
        public static UInkDocument BuildDocument(string headerGuid,
            IReadOnlyList<UInkDevice> devices, IReadOnlyList<UInkWorkspace> workspaces,
            IReadOnlyList<UInkPageInput> pages, ulong nowUnixSeconds)
        {
            var doc = new UInkDocument { Header = new UInkHeader { Guid = headerGuid, Time = nowUnixSeconds } };
            if ((devices != null && devices.Count > 0) || (workspaces != null && workspaces.Count > 0))
            {
                doc.HeaderExtension = new UInkHeaderExtension
                {
                    Devices = devices?.ToList(),
                    Workspaces = workspaces?.ToList(),
                };
            }

            uint contentId = 0;
            foreach (var page in pages)
            {
                var record = new UInkCanvasRecord { Canvas = page.Canvas };
                if (page.Strokes != null)
                {
                    foreach (var ink in UInkConversion.StrokesToInks(page.Strokes, contentId, 0))
                    {
                        record.Blocks.Add(ink);
                        contentId++;
                    }
                }
                if (page.Media != null)
                {
                    foreach (var media in page.Media)
                    {
                        media.ContentId = contentId++;
                        media.UndoId = 0;
                        record.Blocks.Add(media);
                    }
                }
                doc.Canvases.Add(record);
            }

            var (deviceNum, workspaceNum, pageNum) = UInkWriter.ComputeStats(doc);
            doc.Header.DeviceNum = deviceNum;
            doc.Header.WorkspaceNum = workspaceNum;
            doc.Header.PageNum = pageNum;
            return doc;
        }

        // ==================== 加载方向 ====================

        /// <summary>
        /// 把文档映射为逻辑页面列表：按 (workspaceGuid, deviceGuid, pageGuid) 合并同页所有 layer，
        /// layerIndex 越大越靠前；layer 1+ 继承 layer 0 的 viewport。各 layer 的撤回 delta 链按层顺序拼接。
        /// </summary>
        public static List<UInkPageData> ToPages(UInkDocument doc, Func<IUInkContentBlock, Stroke> toStroke)
        {
            var pages = new List<UInkPageData>();
            if (doc?.Canvases == null) return pages;

            var groups = doc.Canvases
                .Where(x => x?.Canvas != null)
                .GroupBy(x => new
                {
                    Workspace = x.Canvas.WorkspaceGuid ?? "",
                    Device = x.Canvas.DeviceGuid ?? "",
                    Page = x.Canvas.PageGuid ?? "",
                });

            foreach (var group in groups)
            {
                var layers = group.OrderBy(x => x.Canvas.LayerIndex).ToList();
                var baseLayer = layers.FirstOrDefault(x => x.Canvas.LayerIndex == 0) ?? layers[0];
                var viewport = baseLayer.Canvas.Viewport;
                var page = new UInkPageData { Canvas = baseLayer.Canvas };
                var history = new List<TimeMachineHistory>();

                foreach (var record in layers)
                {
                    foreach (var b in record.Blocks)
                    {
                        switch (b)
                        {
                            case UInkMedia m: page.Media.Add(m); break;
                            case UInkShape s: page.Shapes.Add(s); break;
                        }
                    }

                    var adapt = UInkUndoAdapter.Adapt(record, toStroke);
                    ApplyViewportToAdaptation(adapt, viewport);
                    foreach (Stroke stroke in adapt.FinalStrokes)
                        if (!page.FinalStrokes.Contains(stroke))
                            page.FinalStrokes.Add(stroke);
                    if (adapt.History != null)
                        history.AddRange(adapt.History);
                }

                page.History = history.Count == 0 ? null : history.ToArray();
                pages.Add(page);
            }
            return pages;
        }

        /// <summary>
        /// viewport 必须应用到撤回适配中的所有 Stroke，而不只是最终可见集合；否则 Undo 后重新显露的
        /// renderOnlyWhenLatest 原稿仍停留在 Canvas 世界坐标。用引用去重确保每条 Stroke 只变换一次。
        /// </summary>
        private static void ApplyViewportToAdaptation(UInkUndoAdaptation adaptation, UInkViewport viewport)
        {
            if (adaptation == null || viewport == null) return;
            if (viewport.Scale <= 0 || !float.IsFinite(viewport.Scale)) return;
            if (Math.Abs(viewport.Scale - 1f) < 1e-6 && Math.Abs(viewport.X) < 1e-6 && Math.Abs(viewport.Y) < 1e-6) return;

            var matrix = new Matrix(viewport.Scale, 0, 0, viewport.Scale,
                -viewport.X * viewport.Scale, -viewport.Y * viewport.Scale);
            var transformed = new HashSet<Stroke>();

            if (adaptation.FinalStrokes != null)
                foreach (Stroke stroke in adaptation.FinalStrokes)
                    if (transformed.Add(stroke)) stroke.Transform(matrix, false);

            if (adaptation.History == null) return;
            foreach (var item in adaptation.History)
            {
                TransformHistoryCollection(item?.CurrentStroke, matrix, transformed);
                TransformHistoryCollection(item?.ReplacedStroke, matrix, transformed);
            }
        }

        private static void TransformHistoryCollection(StrokeCollection strokes, Matrix matrix, HashSet<Stroke> transformed)
        {
            if (strokes == null) return;
            foreach (Stroke stroke in strokes)
                if (transformed.Add(stroke))
                    stroke.Transform(matrix, false);
        }

        /// <summary>
        /// 把 Canvas 世界坐标按 viewport 逆变换转回 Device 坐标：device = (canvas − vp.xy) * vp.scale。
        /// History 与 FinalStrokes 共享 Stroke 引用，转换后同步生效。scale=1 且无平移时不动。
        /// </summary>
        public static void ApplyViewportToStrokes(StrokeCollection strokes, UInkViewport vp)
        {
            if (strokes == null || strokes.Count == 0 || vp == null) return;
            if (vp.Scale <= 0 || !float.IsFinite(vp.Scale)) return;
            if (Math.Abs(vp.Scale - 1f) < 1e-6 && Math.Abs(vp.X) < 1e-6 && Math.Abs(vp.Y) < 1e-6) return;

            var matrix = new Matrix(vp.Scale, 0, 0, vp.Scale, -vp.X * vp.Scale, -vp.Y * vp.Scale);
            foreach (Stroke stroke in strokes)
                stroke.Transform(matrix, false);
        }

        /// <summary>生成文件级 UUID（Header.guid；"另存为"才换）。</summary>
        public static string NewFileGuid() => Guid.NewGuid().ToString();
    }
}
