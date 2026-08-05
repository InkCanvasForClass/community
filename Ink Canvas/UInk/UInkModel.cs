using System;
using System.Collections.Generic;

namespace Ink_Canvas.UInk
{
    // ============================================================
    // UInk 1.0 Beta（规范 version 10）模型契约
    // 顶层块类型 ID
    // ============================================================
    public enum UInkBlockType : ushort
    {
        Header = 0,
        HeaderExtension = 1,
        Canvas = 2,
        Ink = 3,
        Media = 4,
        Shape = 5,
    }

    /// <summary>Ink 渲染类型（inkType）。ICC 统一使用 Pen=1，透明度经块级 opacity 承载。</summary>
    public enum UInkInkType : int
    {
        Erase = 0,
        Pen = 1,
        Highlighter = 2,
        AdvancedHighlighter = 3,
    }

    /// <summary>Shape 几何类型（shapeType）。</summary>
    public enum UInkShapeType : int
    {
        Line = 0,
        Polyline = 1,
        Rectangle = 2,
        Square = 3,
        Ellipse = 4,
        Circle = 5,
        Polygon = 6,
    }

    /// <summary>Workspace 场景类型（workspaceType）。</summary>
    public enum UInkWorkspaceType : int
    {
        ScreenAnnotation = 0,
        Whiteboard = 1,
        Presentation = 2,
    }

    /// <summary>Device 类型（deviceType）。</summary>
    public enum UInkDeviceType : int
    {
        Display = 0,
        Window = 1,
    }

    /// <summary>Fill 类型（fillType）。</summary>
    public enum UInkFillType : int
    {
        Solid = 0,
    }

    /// <summary>Ink/Shape/Media 内容块统一标记接口（字段以具体类型访问）。</summary>
    public interface IUInkContentBlock { }

    // ============================================================
    // Header（Type ID = 0，强制 array(7)）
    // ============================================================
    public sealed class UInkHeader
    {
        public ushort Type = (ushort)UInkBlockType.Header;  // 固定 0
        public ushort Version = 10;                          // 固定 10
        public string Guid = "";                             // 36 字符 UUID，标识同一逻辑文件
        public uint DeviceNum;
        public uint WorkspaceNum;
        public uint PageNum;
        public ulong Time;                                   // Unix UTC 秒
    }

    // ============================================================
    // Header Extension（Type ID = 1，可选，最多 1 个）
    // ============================================================
    public sealed class UInkHeaderExtension
    {
        public string Name;                      // 可选
        public string Explanation;               // 可选
        public List<UInkDevice> Devices;         // Device 注册表
        public List<UInkWorkspace> Workspaces;   // Workspace 注册表
        public Dictionary<string, string> Extra; // 文件级私有扩展
    }

    // ============================================================
    // Device 注册项（deviceType 0=Display / 1=Window）
    // ============================================================
    public sealed class UInkHardware
    {
        public string Name;
        public string Id;
        public Dictionary<string, string> Identifiers;
        public uint? PhysicalWidth;
        public uint? PhysicalHeight;
        public float? ScaleFactor;
    }

    public sealed class UInkDevice
    {
        public string Guid = "";
        public int DeviceType = (int)UInkDeviceType.Display;
        public string Name;                      // 可选
        public UInkHardware Hardware;            // 可选
        public Dictionary<string, string> Extra;

        // Display（deviceType=0）字段
        public int? DisplayX;
        public int? DisplayY;
        public uint? DisplayWidth;
        public uint? DisplayHeight;

        // Window（deviceType=1）字段
        public string ParentDeviceGuid;
        public float? WindowX;
        public float? WindowY;
        public float? WindowWidth;
        public float? WindowHeight;
        public uint? ZIndex;
    }

    // ============================================================
    // Workspace 注册项
    // ============================================================
    public sealed class UInkWorkspace
    {
        public string Guid = "";
        public int WorkspaceType = (int)UInkWorkspaceType.ScreenAnnotation;
        public string Name;
        public string ParentWorkspaceGuid;
        public string HostId;
        public uint? CurrentPageIndex;
        public Dictionary<string, string> Extra;
    }

    // ============================================================
    // Canvas（Type ID = 2，扁平内容流中的页面图层记录）
    // ============================================================
    public sealed class UInkViewport
    {
        public float X;
        public float Y;
        public float Scale = 1f;
    }

    public sealed class UInkCanvas
    {
        public string WorkspaceGuid = "";        // 用显式 Workspace 注册表时必填，否则省略
        public string DeviceGuid = "";           // 用显式 Device 注册表时必填，否则省略
        public string PageGuid = "";             // 逻辑页面永久 UUID
        public uint PageIndex;
        public uint PageNumber;
        public uint LayerIndex;
        public uint LayerNumber;
        public int? SlideId;                     // PPT Workspace 必填的 PowerPoint COM SlideID
        public UInkViewport Viewport;            // 仅 layerIndex=0 可保存
        public Dictionary<string, string> Extra;
    }

    // ============================================================
    // Color Map（fallback 为 0xRRGGBB sRGB + 可选 HDR space/components）
    // ============================================================
    public sealed class UInkColor
    {
        public uint Fallback;                    // 0xRRGGBB
        public string Space;                     // "srgb" | "scrgb"
        public float[] Components;               // 长度 3
    }

    // ============================================================
    // Ink（Type ID = 3）
    // ============================================================
    public sealed class UInkInkPoint
    {
        public float X;          // 首点绝对 X，后续点为相对前一点位移
        public float Y;          // 同上
        public float Width;      // 该点完整直径，> 0
        public UInkColor Color;  // 高级荧光笔点级颜色（可选）
        public float? Opacity;   // 高级荧光笔点级透明度（与 Color 成对）
    }

    public sealed class UInkInk : IUInkContentBlock
    {
        public uint ContentId;
        public uint UndoId;
        public int InkType = (int)UInkInkType.Pen;
        public UInkColor Color = new UInkColor();
        public float Opacity = 1f;
        public int Texture;
        public List<UInkInkPoint> Points = new List<UInkInkPoint>();
        public bool RenderOnlyWhenLatest;
        public Dictionary<string, string> Extra;
    }

    // ============================================================
    // Shape（Type ID = 5，参数化图形）
    // ============================================================
    public sealed class UInkShapePoint
    {
        public float X;   // Canvas 绝对坐标
        public float Y;
    }

    public abstract class UInkShapeGeometry { }

    public sealed class UInkLineGeometry : UInkShapeGeometry
    {
        public List<UInkShapePoint> Points = new List<UInkShapePoint>(); // Line=2 / Polyline>=2 / Polygon>=3
    }

    public sealed class UInkRectGeometry : UInkShapeGeometry
    {
        public float CenterX;
        public float CenterY;
        public float Width;
        public float Height;
        public float? Rotation;   // 弧度，正值顺时针
    }

    public sealed class UInkSquareGeometry : UInkShapeGeometry
    {
        public float CenterX;
        public float CenterY;
        public float Size;
        public float? Rotation;
    }

    public sealed class UInkCircleGeometry : UInkShapeGeometry
    {
        public float CenterX;
        public float CenterY;
        public float Radius;
    }

    public sealed class UInkStroke
    {
        public UInkColor Color = new UInkColor();
        public float Opacity = 1f;
        public float Width;
        public List<float> DashArray;   // 可选，偶数个非负有限值
        public float? DashOffset;
        public int? StartMarker;
        public int? EndMarker;
    }

    public sealed class UInkFill
    {
        public int FillType = (int)UInkFillType.Solid;
        public UInkColor Color = new UInkColor();
        public float Opacity = 1f;
    }

    public sealed class UInkShape : IUInkContentBlock
    {
        public uint ContentId;
        public uint UndoId;
        public int ShapeType;
        public UInkShapeGeometry Geometry;
        public UInkStroke Stroke;
        public UInkFill Fill;
        public bool RenderOnlyWhenLatest;
        public Dictionary<string, string> Extra;
    }

    // ============================================================
    // Media（Type ID = 4）
    // ============================================================
    public sealed class UInkMedia : IUInkContentBlock
    {
        public uint ContentId;
        public uint UndoId;
        public string Path = "";          // .uink.extra 内安全相对路径（NFC / 分隔）
        public string MimeType = "";
        public Dictionary<string, string> Extra;

        // 视觉媒体（图片/SVG/视频/PDF 单页视口）
        public float? Width;
        public float? Height;
        public float[] Transform;         // 仿射矩阵 [a,b,c,d,e,f]，可选
        public float? Opacity;

        // PDF 文档媒体
        public uint? PageCount;
        public uint? PageIndex;

        // 音视频
        public bool Autoplay;
        public bool Loop;
        public float Volume = 1f;
        public double StartTime;
        public float PlaybackRate = 1f;
    }

    // ============================================================
    // 文档内存态：Header + 注册表 + 各 Canvas 及其内容块
    // ============================================================
    public sealed class UInkCanvasRecord
    {
        public UInkCanvas Canvas;
        public List<IUInkContentBlock> Blocks = new List<IUInkContentBlock>();
    }

    public sealed class UInkDocument
    {
        public UInkHeader Header;
        public UInkHeaderExtension HeaderExtension;
        public List<UInkCanvasRecord> Canvases = new List<UInkCanvasRecord>();

        /// <summary>按流顺序展开所有内容块（含所属 Canvas 的引用），供撤回适配使用。</summary>
        public List<(UInkCanvasRecord record, IUInkContentBlock block)> AllBlocks()
        {
            var list = new List<(UInkCanvasRecord, IUInkContentBlock)>();
            foreach (var record in Canvases)
                foreach (var block in record.Blocks)
                    list.Add((record, block));
            return list;
        }
    }
}
