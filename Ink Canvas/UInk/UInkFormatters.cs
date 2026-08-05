using System;
using System.Collections.Generic;
using System.IO;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;

namespace Ink_Canvas.UInk
{
    // ============================================================
    // 写入端强制字段表精确位宽；读取端容错接受可无损转换的其他数值编码。
    // 整数一律 WriteUInt16/WriteUInt32/WriteInt32/WriteUInt64，浮点 Write(Single)。
    // ============================================================
    internal static class UInkFmt
    {
        // ---------- MessagePack 类型探测（3.x 无 NextMessageType，用 NextCode 解码） ----------
        public static bool IsNil(ref MessagePackReader r) => r.NextCode == MessagePackCode.Nil;

        public static bool IsInteger(ref MessagePackReader r) => IsIntegerCode(r.NextCode);

        private static bool IsIntegerCode(byte code) =>
            code <= 0x7F || code >= 0xE0 || (code >= 0xCC && code <= 0xCF) || (code >= 0xD0 && code <= 0xD3);

        private static bool IsStringCode(byte code) =>
            (code >= 0xA0 && code <= 0xBF) || code == MessagePackCode.Str8 || code == MessagePackCode.Str16 || code == MessagePackCode.Str32;

        // ---------- 容错读取（数值字段） ----------
        public static ushort ReadUInt16Tolerant(ref MessagePackReader r)
        {
            var code = r.NextCode;
            if (code == MessagePackCode.Float32) return (ushort)r.ReadSingle();
            if (code == MessagePackCode.Float64) return (ushort)r.ReadDouble();
            return r.ReadUInt16();
        }

        public static uint ReadUInt32Tolerant(ref MessagePackReader r)
        {
            var code = r.NextCode;
            if (code == MessagePackCode.Float32) return (uint)r.ReadSingle();
            if (code == MessagePackCode.Float64) return (uint)r.ReadDouble();
            return r.ReadUInt32();
        }

        public static ulong ReadUInt64Tolerant(ref MessagePackReader r)
        {
            var code = r.NextCode;
            if (code == MessagePackCode.Float32) return (ulong)r.ReadSingle();
            if (code == MessagePackCode.Float64) return (ulong)r.ReadDouble();
            return r.ReadUInt64();
        }

        public static int ReadInt32Tolerant(ref MessagePackReader r)
        {
            var code = r.NextCode;
            if (code == MessagePackCode.Float32) return (int)r.ReadSingle();
            if (code == MessagePackCode.Float64) return (int)r.ReadDouble();
            return r.ReadInt32();
        }

        public static bool ReadBoolTolerant(ref MessagePackReader r)
        {
            var code = r.NextCode;
            if (code == MessagePackCode.False) { r.ReadBoolean(); return false; }
            if (code == MessagePackCode.True) { r.ReadBoolean(); return true; }
            if (IsIntegerCode(code)) return r.ReadInt32() != 0;
            throw new MessagePackSerializationException("不是布尔/整数");
        }

        public static float ReadSingleTolerant(ref MessagePackReader r)
        {
            var code = r.NextCode;
            if (code == MessagePackCode.Float32) return r.ReadSingle();
            if (code == MessagePackCode.Float64) return (float)r.ReadDouble();
            if (IsIntegerCode(code)) return r.ReadInt64();
            throw new MessagePackSerializationException("不是数值");
        }

        public static double ReadDoubleTolerant(ref MessagePackReader r)
        {
            var code = r.NextCode;
            if (code == MessagePackCode.Float32) return r.ReadSingle();
            if (code == MessagePackCode.Float64) return r.ReadDouble();
            if (IsIntegerCode(code)) return r.ReadInt64();
            throw new MessagePackSerializationException("不是数值");
        }

        public static string ReadOptionalString(ref MessagePackReader r)
        {
            if (IsNil(ref r)) { r.ReadNil(); return null; }
            return r.ReadString();
        }

        // ---------- string→string Map（extra 用） ----------
        public static void WriteStringMap(ref MessagePackWriter w, Dictionary<string, string> map)
        {
            if (map == null || map.Count == 0) { w.WriteNil(); return; }
            w.WriteMapHeader(map.Count);
            foreach (var kv in map)
            {
                w.Write(kv.Key ?? "");
                w.Write(kv.Value ?? "");
            }
        }

        public static Dictionary<string, string> ReadStringMap(ref MessagePackReader r)
        {
            if (IsNil(ref r)) { r.ReadNil(); return null; }
            int count = r.ReadMapHeader();
            var map = new Dictionary<string, string>(count);
            for (int i = 0; i < count; i++)
            {
                var key = r.ReadString();
                if (IsStringCode(r.NextCode))
                    map[key] = r.ReadString();
                else
                    r.Skip(); // 非字符串值按未知键忽略
            }
            return map;
        }

        // ---------- Color Map ----------
        public static void WriteColor(ref MessagePackWriter w, UInkColor c)
        {
            bool hasHdr = !string.IsNullOrEmpty(c?.Space) && c.Components != null && c.Components.Length == 3;
            w.WriteMapHeader(hasHdr ? 3 : 1);
            w.Write("fallback");
            w.WriteUInt32(c?.Fallback ?? 0u);
            if (hasHdr)
            {
                w.Write("space");
                w.Write(c.Space);
                w.Write("components");
                w.WriteArrayHeader(3);
                for (int i = 0; i < 3; i++) w.Write(c.Components[i]);
            }
        }

        public static UInkColor ReadColor(ref MessagePackReader r)
        {
            if (IsNil(ref r)) { r.ReadNil(); return null; }
            var c = new UInkColor();
            int count = r.ReadMapHeader();
            for (int i = 0; i < count; i++)
            {
                var key = r.ReadString();
                switch (key)
                {
                    case "fallback":
                        c.Fallback = ReadUInt32Tolerant(ref r);
                        break;
                    case "space":
                        c.Space = r.ReadString();
                        break;
                    case "components":
                        int len = r.ReadArrayHeader();
                        if (len == 3)
                        {
                            c.Components = new float[3];
                            for (int j = 0; j < 3; j++) c.Components[j] = ReadSingleTolerant(ref r);
                        }
                        else
                        {
                            for (int j = 0; j < len; j++) r.Skip();
                        }
                        break;
                    default:
                        r.Skip();
                        break;
                }
            }
            return c;
        }

        // ---------- Viewport Map ----------
        public static void WriteViewport(ref MessagePackWriter w, UInkViewport v)
        {
            w.WriteMapHeader(3);
            w.Write("x"); w.Write(v.X);
            w.Write("y"); w.Write(v.Y);
            w.Write("scale"); w.Write(v.Scale);
        }

        public static UInkViewport ReadViewport(ref MessagePackReader r)
        {
            var v = new UInkViewport();
            int count = r.ReadMapHeader();
            for (int i = 0; i < count; i++)
            {
                var key = r.ReadString();
                switch (key)
                {
                    case "x": v.X = ReadSingleTolerant(ref r); break;
                    case "y": v.Y = ReadSingleTolerant(ref r); break;
                    case "scale": v.Scale = ReadSingleTolerant(ref r); break;
                    default: r.Skip(); break;
                }
            }
            return v;
        }

        // ---------- Hardware Map（Device 内） ----------
        public static void WriteHardware(ref MessagePackWriter w, UInkHardware h)
        {
            w.WriteMapHeader(6);
            w.Write("name"); w.Write(h.Name ?? "");
            w.Write("id"); w.Write(h.Id ?? "");
            w.Write("identifiers");
            WriteStringMap(ref w, h.Identifiers);
            w.Write("physicalWidth");
            if (h.PhysicalWidth.HasValue) w.WriteUInt32(h.PhysicalWidth.Value); else w.WriteNil();
            w.Write("physicalHeight");
            if (h.PhysicalHeight.HasValue) w.WriteUInt32(h.PhysicalHeight.Value); else w.WriteNil();
            w.Write("scaleFactor");
            if (h.ScaleFactor.HasValue) w.Write(h.ScaleFactor.Value); else w.WriteNil();
        }

        public static UInkHardware ReadHardware(ref MessagePackReader r)
        {
            var h = new UInkHardware();
            int count = r.ReadMapHeader();
            for (int i = 0; i < count; i++)
            {
                var key = r.ReadString();
                switch (key)
                {
                    case "name": h.Name = r.ReadString(); break;
                    case "id": h.Id = r.ReadString(); break;
                    case "identifiers": h.Identifiers = ReadStringMap(ref r); break;
                    case "physicalWidth":
                        if (!IsNil(ref r)) h.PhysicalWidth = ReadUInt32Tolerant(ref r); else r.ReadNil();
                        break;
                    case "physicalHeight":
                        if (!IsNil(ref r)) h.PhysicalHeight = ReadUInt32Tolerant(ref r); else r.ReadNil();
                        break;
                    case "scaleFactor":
                        if (!IsNil(ref r)) h.ScaleFactor = ReadSingleTolerant(ref r); else r.ReadNil();
                        break;
                    default: r.Skip(); break;
                }
            }
            return h;
        }

        // ---------- Device Map（Header Extension devices 数组项） ----------
        public static void WriteDevice(ref MessagePackWriter w, UInkDevice d)
        {
            int n = 4; // guid, deviceType, name, extra
            bool isDisplay = d.DeviceType == (int)UInkDeviceType.Display;
            if (d.Hardware != null) n++;
            if (isDisplay) n += 4;
            else n += 5;
            w.WriteMapHeader(n);
            w.Write("guid"); w.Write(d.Guid ?? "");
            w.Write("deviceType"); w.WriteInt32(d.DeviceType);
            w.Write("name"); w.Write(d.Name ?? "");
            if (d.Hardware != null) { w.Write("hardware"); WriteHardware(ref w, d.Hardware); }
            if (isDisplay)
            {
                w.Write("x"); w.WriteInt32(d.DisplayX ?? 0);
                w.Write("y"); w.WriteInt32(d.DisplayY ?? 0);
                w.Write("width"); w.WriteUInt32(d.DisplayWidth ?? 0);
                w.Write("height"); w.WriteUInt32(d.DisplayHeight ?? 0);
            }
            else
            {
                w.Write("parentDeviceGuid"); w.Write(d.ParentDeviceGuid ?? "");
                w.Write("x"); w.Write(d.WindowX ?? 0f);
                w.Write("y"); w.Write(d.WindowY ?? 0f);
                w.Write("width"); w.Write(d.WindowWidth ?? 0f);
                w.Write("height"); w.Write(d.WindowHeight ?? 0f);
                w.Write("zIndex"); w.WriteUInt32(d.ZIndex ?? 0);
            }
            w.Write("extra");
            WriteStringMap(ref w, d.Extra);
        }

        public static UInkDevice ReadDevice(ref MessagePackReader r)
        {
            var d = new UInkDevice();
            int count = r.ReadMapHeader();
            for (int i = 0; i < count; i++)
            {
                var key = r.ReadString();
                switch (key)
                {
                    case "guid": d.Guid = r.ReadString(); break;
                    case "deviceType": d.DeviceType = ReadInt32Tolerant(ref r); break;
                    case "name": d.Name = r.ReadString(); break;
                    case "hardware": d.Hardware = ReadHardware(ref r); break;
                    case "parentDeviceGuid": d.ParentDeviceGuid = r.ReadString(); break;
                    case "x":
                        if (d.DeviceType == (int)UInkDeviceType.Window)
                        {
                            if (IsIntegerCode(r.NextCode)) d.WindowX = r.ReadInt32();
                            else d.WindowX = ReadSingleTolerant(ref r);
                        }
                        else d.DisplayX = ReadInt32Tolerant(ref r);
                        break;
                    case "y":
                        if (d.DeviceType == (int)UInkDeviceType.Window)
                        {
                            if (IsIntegerCode(r.NextCode)) d.WindowY = r.ReadInt32();
                            else d.WindowY = ReadSingleTolerant(ref r);
                        }
                        else d.DisplayY = ReadInt32Tolerant(ref r);
                        break;
                    case "width":
                        if (d.DeviceType == (int)UInkDeviceType.Window)
                        {
                            if (IsIntegerCode(r.NextCode)) d.WindowWidth = r.ReadInt32();
                            else d.WindowWidth = ReadSingleTolerant(ref r);
                        }
                        else d.DisplayWidth = ReadUInt32Tolerant(ref r);
                        break;
                    case "height":
                        if (d.DeviceType == (int)UInkDeviceType.Window)
                        {
                            if (IsIntegerCode(r.NextCode)) d.WindowHeight = r.ReadInt32();
                            else d.WindowHeight = ReadSingleTolerant(ref r);
                        }
                        else d.DisplayHeight = ReadUInt32Tolerant(ref r);
                        break;
                    case "zIndex": d.ZIndex = ReadUInt32Tolerant(ref r); break;
                    case "extra": d.Extra = ReadStringMap(ref r); break;
                    default: r.Skip(); break;
                }
            }
            return d;
        }

        // ---------- Workspace Map（Header Extension workspaces 数组项） ----------
        public static void WriteWorkspace(ref MessagePackWriter w, UInkWorkspace ws)
        {
            int n = 2; // guid, workspaceType
            if (!string.IsNullOrEmpty(ws.Name)) n++;
            if (!string.IsNullOrEmpty(ws.ParentWorkspaceGuid)) n++;
            if (!string.IsNullOrEmpty(ws.HostId)) n++;
            if (ws.CurrentPageIndex.HasValue) n++;
            if (ws.Extra != null && ws.Extra.Count > 0) n++;
            w.WriteMapHeader(n);
            w.Write("guid"); w.Write(ws.Guid ?? "");
            w.Write("workspaceType"); w.WriteInt32(ws.WorkspaceType);
            if (!string.IsNullOrEmpty(ws.Name)) { w.Write("name"); w.Write(ws.Name); }
            if (!string.IsNullOrEmpty(ws.ParentWorkspaceGuid)) { w.Write("parentWorkspaceGuid"); w.Write(ws.ParentWorkspaceGuid); }
            if (!string.IsNullOrEmpty(ws.HostId)) { w.Write("hostId"); w.Write(ws.HostId); }
            if (ws.CurrentPageIndex.HasValue) { w.Write("currentPageIndex"); w.WriteUInt32(ws.CurrentPageIndex.Value); }
            if (ws.Extra != null && ws.Extra.Count > 0) { w.Write("extra"); WriteStringMap(ref w, ws.Extra); }
        }

        public static UInkWorkspace ReadWorkspace(ref MessagePackReader r)
        {
            var ws = new UInkWorkspace();
            int count = r.ReadMapHeader();
            for (int i = 0; i < count; i++)
            {
                var key = r.ReadString();
                switch (key)
                {
                    case "guid": ws.Guid = r.ReadString(); break;
                    case "workspaceType": ws.WorkspaceType = ReadInt32Tolerant(ref r); break;
                    case "name": ws.Name = r.ReadString(); break;
                    case "parentWorkspaceGuid": ws.ParentWorkspaceGuid = r.ReadString(); break;
                    case "hostId": ws.HostId = r.ReadString(); break;
                    case "currentPageIndex": ws.CurrentPageIndex = ReadUInt32Tolerant(ref r); break;
                    case "extra": ws.Extra = ReadStringMap(ref r); break;
                    default: r.Skip(); break;
                }
            }
            return ws;
        }

        // ---------- Shape Geometry / Stroke / Fill ----------
        public static void WriteShapePoint(ref MessagePackWriter w, UInkShapePoint p)
        {
            w.WriteMapHeader(2);
            w.Write("x"); w.Write(p.X);
            w.Write("y"); w.Write(p.Y);
        }

        public static UInkShapePoint ReadShapePoint(ref MessagePackReader r)
        {
            var p = new UInkShapePoint();
            int count = r.ReadMapHeader();
            for (int i = 0; i < count; i++)
            {
                var key = r.ReadString();
                switch (key)
                {
                    case "x": p.X = ReadSingleTolerant(ref r); break;
                    case "y": p.Y = ReadSingleTolerant(ref r); break;
                    default: r.Skip(); break;
                }
            }
            return p;
        }

        public static void WriteGeometry(ref MessagePackWriter w, int shapeType, UInkShapeGeometry g)
        {
            switch (shapeType)
            {
                case (int)UInkShapeType.Line:
                case (int)UInkShapeType.Polyline:
                case (int)UInkShapeType.Polygon:
                {
                    var line = (UInkLineGeometry)g;
                    w.WriteMapHeader(1);
                    w.Write("points");
                    w.WriteArrayHeader(line.Points.Count);
                    foreach (var p in line.Points) WriteShapePoint(ref w, p);
                    break;
                }
                case (int)UInkShapeType.Rectangle:
                case (int)UInkShapeType.Ellipse:
                {
                    var rect = (UInkRectGeometry)g;
                    int n = rect.Rotation.HasValue ? 5 : 4;
                    w.WriteMapHeader(n);
                    w.Write("centerX"); w.Write(rect.CenterX);
                    w.Write("centerY"); w.Write(rect.CenterY);
                    w.Write("width"); w.Write(rect.Width);
                    w.Write("height"); w.Write(rect.Height);
                    if (rect.Rotation.HasValue) { w.Write("rotation"); w.Write(rect.Rotation.Value); }
                    break;
                }
                case (int)UInkShapeType.Square:
                {
                    var sq = (UInkSquareGeometry)g;
                    int n = sq.Rotation.HasValue ? 4 : 3;
                    w.WriteMapHeader(n);
                    w.Write("centerX"); w.Write(sq.CenterX);
                    w.Write("centerY"); w.Write(sq.CenterY);
                    w.Write("size"); w.Write(sq.Size);
                    if (sq.Rotation.HasValue) { w.Write("rotation"); w.Write(sq.Rotation.Value); }
                    break;
                }
                case (int)UInkShapeType.Circle:
                {
                    var ci = (UInkCircleGeometry)g;
                    w.WriteMapHeader(3);
                    w.Write("centerX"); w.Write(ci.CenterX);
                    w.Write("centerY"); w.Write(ci.CenterY);
                    w.Write("radius"); w.Write(ci.Radius);
                    break;
                }
                default:
                    w.WriteMapHeader(0);
                    break;
            }
        }

        public static UInkShapeGeometry ReadGeometry(ref MessagePackReader r, int shapeType)
        {
            int count = r.ReadMapHeader();
            switch (shapeType)
            {
                case (int)UInkShapeType.Line:
                case (int)UInkShapeType.Polyline:
                case (int)UInkShapeType.Polygon:
                {
                    var line = new UInkLineGeometry();
                    for (int i = 0; i < count; i++)
                    {
                        var key = r.ReadString();
                        if (key == "points")
                        {
                            int len = r.ReadArrayHeader();
                            for (int j = 0; j < len; j++) line.Points.Add(ReadShapePoint(ref r));
                        }
                        else r.Skip();
                    }
                    return line;
                }
                case (int)UInkShapeType.Rectangle:
                case (int)UInkShapeType.Ellipse:
                {
                    var rect = new UInkRectGeometry();
                    for (int i = 0; i < count; i++)
                    {
                        var key = r.ReadString();
                        switch (key)
                        {
                            case "centerX": rect.CenterX = ReadSingleTolerant(ref r); break;
                            case "centerY": rect.CenterY = ReadSingleTolerant(ref r); break;
                            case "width": rect.Width = ReadSingleTolerant(ref r); break;
                            case "height": rect.Height = ReadSingleTolerant(ref r); break;
                            case "rotation": rect.Rotation = ReadSingleTolerant(ref r); break;
                            default: r.Skip(); break;
                        }
                    }
                    return rect;
                }
                case (int)UInkShapeType.Square:
                {
                    var sq = new UInkSquareGeometry();
                    for (int i = 0; i < count; i++)
                    {
                        var key = r.ReadString();
                        switch (key)
                        {
                            case "centerX": sq.CenterX = ReadSingleTolerant(ref r); break;
                            case "centerY": sq.CenterY = ReadSingleTolerant(ref r); break;
                            case "size": sq.Size = ReadSingleTolerant(ref r); break;
                            case "rotation": sq.Rotation = ReadSingleTolerant(ref r); break;
                            default: r.Skip(); break;
                        }
                    }
                    return sq;
                }
                case (int)UInkShapeType.Circle:
                {
                    var ci = new UInkCircleGeometry();
                    for (int i = 0; i < count; i++)
                    {
                        var key = r.ReadString();
                        switch (key)
                        {
                            case "centerX": ci.CenterX = ReadSingleTolerant(ref r); break;
                            case "centerY": ci.CenterY = ReadSingleTolerant(ref r); break;
                            case "radius": ci.Radius = ReadSingleTolerant(ref r); break;
                            default: r.Skip(); break;
                        }
                    }
                    return ci;
                }
                default:
                    for (int i = 0; i < count; i++) { r.ReadString(); r.Skip(); }
                    return null;
            }
        }

        public static void WriteStroke(ref MessagePackWriter w, UInkStroke s)
        {
            int n = 3; // color, opacity, width
            if (s.DashArray != null && s.DashArray.Count > 0) n++;
            if (s.DashOffset.HasValue) n++;
            if (s.StartMarker.HasValue) n++;
            if (s.EndMarker.HasValue) n++;
            w.WriteMapHeader(n);
            w.Write("color"); WriteColor(ref w, s.Color);
            w.Write("opacity"); w.Write(s.Opacity);
            w.Write("width"); w.Write(s.Width);
            if (s.DashArray != null && s.DashArray.Count > 0)
            {
                w.Write("dashArray");
                w.WriteArrayHeader(s.DashArray.Count);
                foreach (var f in s.DashArray) w.Write(f);
            }
            if (s.DashOffset.HasValue) { w.Write("dashOffset"); w.Write(s.DashOffset.Value); }
            if (s.StartMarker.HasValue) { w.Write("startMarker"); w.WriteInt32(s.StartMarker.Value); }
            if (s.EndMarker.HasValue) { w.Write("endMarker"); w.WriteInt32(s.EndMarker.Value); }
        }

        public static UInkStroke ReadStroke(ref MessagePackReader r)
        {
            var s = new UInkStroke();
            int count = r.ReadMapHeader();
            for (int i = 0; i < count; i++)
            {
                var key = r.ReadString();
                switch (key)
                {
                    case "color": s.Color = ReadColor(ref r); break;
                    case "opacity": s.Opacity = ReadSingleTolerant(ref r); break;
                    case "width": s.Width = ReadSingleTolerant(ref r); break;
                    case "dashArray":
                    {
                        int len = r.ReadArrayHeader();
                        s.DashArray = new List<float>(len);
                        for (int j = 0; j < len; j++) s.DashArray.Add(ReadSingleTolerant(ref r));
                        break;
                    }
                    case "dashOffset": s.DashOffset = ReadSingleTolerant(ref r); break;
                    case "startMarker": s.StartMarker = ReadInt32Tolerant(ref r); break;
                    case "endMarker": s.EndMarker = ReadInt32Tolerant(ref r); break;
                    default: r.Skip(); break;
                }
            }
            return s;
        }

        public static void WriteFill(ref MessagePackWriter w, UInkFill f)
        {
            w.WriteMapHeader(3);
            w.Write("fillType"); w.WriteInt32(f.FillType);
            w.Write("color"); WriteColor(ref w, f.Color);
            w.Write("opacity"); w.Write(f.Opacity);
        }

        public static UInkFill ReadFill(ref MessagePackReader r)
        {
            var f = new UInkFill();
            int count = r.ReadMapHeader();
            for (int i = 0; i < count; i++)
            {
                var key = r.ReadString();
                switch (key)
                {
                    case "fillType": f.FillType = ReadInt32Tolerant(ref r); break;
                    case "color": f.Color = ReadColor(ref r); break;
                    case "opacity": f.Opacity = ReadSingleTolerant(ref r); break;
                    default: r.Skip(); break;
                }
            }
            return f;
        }
    }

    // ============================================================
    // Header：强制 array(7)，字段顺序/位宽/guid 编码不得更改
    // ============================================================
    public sealed class UInkHeaderFormatter : IMessagePackFormatter<UInkHeader>
    {
        public static readonly UInkHeaderFormatter Instance = new UInkHeaderFormatter();

        public void Serialize(ref MessagePackWriter writer, UInkHeader value, MessagePackSerializerOptions options)
        {
            writer.WriteArrayHeader(7);
            writer.WriteUInt16(value.Type);        // 0
            writer.WriteUInt16(value.Version);     // 10
            writer.Write(value.Guid ?? "");
            writer.WriteUInt32(value.DeviceNum);
            writer.WriteUInt32(value.WorkspaceNum);
            writer.WriteUInt32(value.PageNum);
            writer.WriteUInt64(value.Time);
        }

        public UInkHeader Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            int len = reader.ReadArrayHeader();
            if (len != 7)
                throw new MessagePackSerializationException($"UInk Header 必须为 array(7)，实际长度 {len}");
            var h = new UInkHeader();
            h.Type = UInkFmt.ReadUInt16Tolerant(ref reader);
            h.Version = UInkFmt.ReadUInt16Tolerant(ref reader);
            h.Guid = reader.ReadString();
            h.DeviceNum = UInkFmt.ReadUInt32Tolerant(ref reader);
            h.WorkspaceNum = UInkFmt.ReadUInt32Tolerant(ref reader);
            h.PageNum = UInkFmt.ReadUInt32Tolerant(ref reader);
            h.Time = UInkFmt.ReadUInt64Tolerant(ref reader);
            return h;
        }
    }

    // ============================================================
    // Header Extension（Type ID = 1）
    // ============================================================
    public sealed class UInkHeaderExtensionFormatter : IMessagePackFormatter<UInkHeaderExtension>
    {
        public static readonly UInkHeaderExtensionFormatter Instance = new UInkHeaderExtensionFormatter();

        public void Serialize(ref MessagePackWriter writer, UInkHeaderExtension value, MessagePackSerializerOptions options)
        {
            int n = 1; // type
            if (!string.IsNullOrEmpty(value.Name)) n++;
            if (!string.IsNullOrEmpty(value.Explanation)) n++;
            if (value.Devices != null && value.Devices.Count > 0) n++;
            if (value.Workspaces != null && value.Workspaces.Count > 0) n++;
            if (value.Extra != null && value.Extra.Count > 0) n++;
            writer.WriteMapHeader(n);
            writer.Write("type");
            writer.WriteUInt16((ushort)UInkBlockType.HeaderExtension);
            if (!string.IsNullOrEmpty(value.Name)) { writer.Write("name"); writer.Write(value.Name); }
            if (!string.IsNullOrEmpty(value.Explanation)) { writer.Write("explanation"); writer.Write(value.Explanation); }
            if (value.Devices != null && value.Devices.Count > 0)
            {
                writer.Write("devices");
                writer.WriteArrayHeader(value.Devices.Count);
                foreach (var d in value.Devices) UInkFmt.WriteDevice(ref writer, d);
            }
            if (value.Workspaces != null && value.Workspaces.Count > 0)
            {
                writer.Write("workspaces");
                writer.WriteArrayHeader(value.Workspaces.Count);
                foreach (var ws in value.Workspaces) UInkFmt.WriteWorkspace(ref writer, ws);
            }
            if (value.Extra != null && value.Extra.Count > 0) { writer.Write("extra"); UInkFmt.WriteStringMap(ref writer, value.Extra); }
        }

        public UInkHeaderExtension Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            var ext = new UInkHeaderExtension();
            int count = reader.ReadMapHeader();
            for (int i = 0; i < count; i++)
            {
                var key = reader.ReadString();
                switch (key)
                {
                    case "name": ext.Name = reader.ReadString(); break;
                    case "explanation": ext.Explanation = reader.ReadString(); break;
                    case "devices":
                    {
                        int len = reader.ReadArrayHeader();
                        ext.Devices = new List<UInkDevice>(len);
                        for (int j = 0; j < len; j++) ext.Devices.Add(UInkFmt.ReadDevice(ref reader));
                        break;
                    }
                    case "workspaces":
                    {
                        int len = reader.ReadArrayHeader();
                        ext.Workspaces = new List<UInkWorkspace>(len);
                        for (int j = 0; j < len; j++) ext.Workspaces.Add(UInkFmt.ReadWorkspace(ref reader));
                        break;
                    }
                    case "extra": ext.Extra = UInkFmt.ReadStringMap(ref reader); break;
                    default: reader.Skip(); break;
                }
            }
            return ext;
        }
    }

    // ============================================================
    // Canvas（Type ID = 2）
    // ============================================================
    public sealed class UInkCanvasFormatter : IMessagePackFormatter<UInkCanvas>
    {
        public static readonly UInkCanvasFormatter Instance = new UInkCanvasFormatter();

        public void Serialize(ref MessagePackWriter writer, UInkCanvas value, MessagePackSerializerOptions options)
        {
            int n = 6; // type, pageGuid, pageIndex, pageNumber, layerIndex, layerNumber
            if (!string.IsNullOrEmpty(value.WorkspaceGuid)) n++;
            if (!string.IsNullOrEmpty(value.DeviceGuid)) n++;
            if (value.SlideId.HasValue) n++;
            if (value.Viewport != null) n++;
            if (value.Extra != null && value.Extra.Count > 0) n++;
            writer.WriteMapHeader(n);
            writer.Write("type");
            writer.WriteUInt16((ushort)UInkBlockType.Canvas);
            if (!string.IsNullOrEmpty(value.WorkspaceGuid)) { writer.Write("workspaceGuid"); writer.Write(value.WorkspaceGuid); }
            if (!string.IsNullOrEmpty(value.DeviceGuid)) { writer.Write("deviceGuid"); writer.Write(value.DeviceGuid); }
            writer.Write("pageGuid"); writer.Write(value.PageGuid ?? "");
            writer.Write("pageIndex"); writer.WriteUInt32(value.PageIndex);
            writer.Write("pageNumber"); writer.WriteUInt32(value.PageNumber);
            writer.Write("layerIndex"); writer.WriteUInt32(value.LayerIndex);
            writer.Write("layerNumber"); writer.WriteUInt32(value.LayerNumber);
            if (value.SlideId.HasValue) { writer.Write("slideId"); writer.WriteInt32(value.SlideId.Value); }
            if (value.Viewport != null) { writer.Write("viewport"); UInkFmt.WriteViewport(ref writer, value.Viewport); }
            if (value.Extra != null && value.Extra.Count > 0) { writer.Write("extra"); UInkFmt.WriteStringMap(ref writer, value.Extra); }
        }

        public UInkCanvas Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            var c = new UInkCanvas();
            int count = reader.ReadMapHeader();
            for (int i = 0; i < count; i++)
            {
                var key = reader.ReadString();
                switch (key)
                {
                    case "workspaceGuid": c.WorkspaceGuid = reader.ReadString(); break;
                    case "deviceGuid": c.DeviceGuid = reader.ReadString(); break;
                    case "pageGuid": c.PageGuid = reader.ReadString(); break;
                    case "pageIndex": c.PageIndex = UInkFmt.ReadUInt32Tolerant(ref reader); break;
                    case "pageNumber": c.PageNumber = UInkFmt.ReadUInt32Tolerant(ref reader); break;
                    case "layerIndex": c.LayerIndex = UInkFmt.ReadUInt32Tolerant(ref reader); break;
                    case "layerNumber": c.LayerNumber = UInkFmt.ReadUInt32Tolerant(ref reader); break;
                    case "slideId":
                        if (UInkFmt.IsNil(ref reader)) reader.ReadNil();
                        else c.SlideId = UInkFmt.ReadInt32Tolerant(ref reader);
                        break;
                    case "viewport": c.Viewport = UInkFmt.ReadViewport(ref reader); break;
                    case "extra": c.Extra = UInkFmt.ReadStringMap(ref reader); break;
                    default: reader.Skip(); break;
                }
            }
            return c;
        }
    }

    // ============================================================
    // Ink（Type ID = 3）
    // ============================================================
    public sealed class UInkInkFormatter : IMessagePackFormatter<UInkInk>
    {
        public static readonly UInkInkFormatter Instance = new UInkInkFormatter();

        public void Serialize(ref MessagePackWriter writer, UInkInk value, MessagePackSerializerOptions options)
        {
            int n = 8; // type, contentId, undoId, inkType, color, opacity, texture, points
            if (value.RenderOnlyWhenLatest) n++;
            if (value.Extra != null && value.Extra.Count > 0) n++;
            writer.WriteMapHeader(n);
            writer.Write("type"); writer.WriteUInt16((ushort)UInkBlockType.Ink);
            writer.Write("contentId"); writer.WriteUInt32(value.ContentId);
            writer.Write("undoId"); writer.WriteUInt32(value.UndoId);
            writer.Write("inkType"); writer.WriteInt32(value.InkType);
            writer.Write("color"); UInkFmt.WriteColor(ref writer, value.Color);
            writer.Write("opacity"); writer.Write(value.Opacity);
            writer.Write("texture"); writer.WriteInt32(value.Texture);
            writer.Write("points");
            writer.WriteArrayHeader(value.Points.Count);
            foreach (var p in value.Points)
            {
                bool hasPointColor = p.Color != null && !string.IsNullOrEmpty(p.Color.Space) && p.Color.Components != null;
                int pn = hasPointColor ? 5 : 3;
                writer.WriteMapHeader(pn);
                writer.Write("x"); writer.Write(p.X);
                writer.Write("y"); writer.Write(p.Y);
                writer.Write("width"); writer.Write(p.Width);
                if (hasPointColor)
                {
                    writer.Write("color"); UInkFmt.WriteColor(ref writer, p.Color);
                    writer.Write("opacity"); writer.Write(p.Opacity ?? 1f);
                }
            }
            if (value.RenderOnlyWhenLatest) { writer.Write("renderOnlyWhenLatest"); writer.Write(true); }
            if (value.Extra != null && value.Extra.Count > 0) { writer.Write("extra"); UInkFmt.WriteStringMap(ref writer, value.Extra); }
        }

        public UInkInk Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            var ink = new UInkInk();
            int count = reader.ReadMapHeader();
            for (int i = 0; i < count; i++)
            {
                var key = reader.ReadString();
                switch (key)
                {
                    case "contentId": ink.ContentId = UInkFmt.ReadUInt32Tolerant(ref reader); break;
                    case "undoId": ink.UndoId = UInkFmt.ReadUInt32Tolerant(ref reader); break;
                    case "inkType": ink.InkType = UInkFmt.ReadInt32Tolerant(ref reader); break;
                    case "color": ink.Color = UInkFmt.ReadColor(ref reader) ?? new UInkColor(); break;
                    case "opacity": ink.Opacity = UInkFmt.ReadSingleTolerant(ref reader); break;
                    case "texture": ink.Texture = UInkFmt.ReadInt32Tolerant(ref reader); break;
                    case "points":
                    {
                        int len = reader.ReadArrayHeader();
                        ink.Points = new List<UInkInkPoint>(len);
                        for (int j = 0; j < len; j++) ink.Points.Add(ReadInkPoint(ref reader));
                        break;
                    }
                    case "renderOnlyWhenLatest": ink.RenderOnlyWhenLatest = UInkFmt.ReadBoolTolerant(ref reader); break;
                    case "extra": ink.Extra = UInkFmt.ReadStringMap(ref reader); break;
                    default: reader.Skip(); break;
                }
            }
            return ink;
        }

        private static UInkInkPoint ReadInkPoint(ref MessagePackReader reader)
        {
            var p = new UInkInkPoint { Width = 1f };
            int count = reader.ReadMapHeader();
            for (int i = 0; i < count; i++)
            {
                var key = reader.ReadString();
                switch (key)
                {
                    case "x": p.X = UInkFmt.ReadSingleTolerant(ref reader); break;
                    case "y": p.Y = UInkFmt.ReadSingleTolerant(ref reader); break;
                    case "width": p.Width = UInkFmt.ReadSingleTolerant(ref reader); break;
                    case "color": p.Color = UInkFmt.ReadColor(ref reader); break;
                    case "opacity":
                        if (UInkFmt.IsNil(ref reader)) reader.ReadNil();
                        else p.Opacity = UInkFmt.ReadSingleTolerant(ref reader);
                        break;
                    default: reader.Skip(); break;
                }
            }
            return p;
        }
    }

    // ============================================================
    // Shape（Type ID = 5）
    // ============================================================
    public sealed class UInkShapeFormatter : IMessagePackFormatter<UInkShape>
    {
        public static readonly UInkShapeFormatter Instance = new UInkShapeFormatter();

        public void Serialize(ref MessagePackWriter writer, UInkShape value, MessagePackSerializerOptions options)
        {
            int n = 5; // type, contentId, undoId, shapeType, geometry
            if (value.Stroke != null) n++;
            if (value.Fill != null) n++;
            if (value.RenderOnlyWhenLatest) n++;
            if (value.Extra != null && value.Extra.Count > 0) n++;
            writer.WriteMapHeader(n);
            writer.Write("type"); writer.WriteUInt16((ushort)UInkBlockType.Shape);
            writer.Write("contentId"); writer.WriteUInt32(value.ContentId);
            writer.Write("undoId"); writer.WriteUInt32(value.UndoId);
            writer.Write("shapeType"); writer.WriteInt32(value.ShapeType);
            writer.Write("geometry"); UInkFmt.WriteGeometry(ref writer, value.ShapeType, value.Geometry);
            if (value.Stroke != null) { writer.Write("stroke"); UInkFmt.WriteStroke(ref writer, value.Stroke); }
            if (value.Fill != null) { writer.Write("fill"); UInkFmt.WriteFill(ref writer, value.Fill); }
            if (value.RenderOnlyWhenLatest) { writer.Write("renderOnlyWhenLatest"); writer.Write(true); }
            if (value.Extra != null && value.Extra.Count > 0) { writer.Write("extra"); UInkFmt.WriteStringMap(ref writer, value.Extra); }
        }

        public UInkShape Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            var shape = new UInkShape();
            int count = reader.ReadMapHeader();
            for (int i = 0; i < count; i++)
            {
                var key = reader.ReadString();
                switch (key)
                {
                    case "contentId": shape.ContentId = UInkFmt.ReadUInt32Tolerant(ref reader); break;
                    case "undoId": shape.UndoId = UInkFmt.ReadUInt32Tolerant(ref reader); break;
                    case "shapeType": shape.ShapeType = UInkFmt.ReadInt32Tolerant(ref reader); break;
                    case "geometry": shape.Geometry = UInkFmt.ReadGeometry(ref reader, shape.ShapeType); break;
                    case "stroke": shape.Stroke = UInkFmt.ReadStroke(ref reader); break;
                    case "fill": shape.Fill = UInkFmt.ReadFill(ref reader); break;
                    case "renderOnlyWhenLatest": shape.RenderOnlyWhenLatest = UInkFmt.ReadBoolTolerant(ref reader); break;
                    case "extra": shape.Extra = UInkFmt.ReadStringMap(ref reader); break;
                    default: reader.Skip(); break;
                }
            }
            return shape;
        }
    }

    // ============================================================
    // Media（Type ID = 4）
    // ============================================================
    public sealed class UInkMediaFormatter : IMessagePackFormatter<UInkMedia>
    {
        public static readonly UInkMediaFormatter Instance = new UInkMediaFormatter();

        public void Serialize(ref MessagePackWriter writer, UInkMedia value, MessagePackSerializerOptions options)
        {
            int n = 5; // type, contentId, undoId, path, mimeType
            if (value.Width.HasValue) n++;
            if (value.Height.HasValue) n++;
            if (value.Transform != null) n++;
            if (value.Opacity.HasValue) n++;
            if (value.PageCount.HasValue) n++;
            if (value.PageIndex.HasValue) n++;
            if (value.Autoplay) n++;
            if (value.Loop) n++;
            if (Math.Abs(value.Volume - 1f) > 0.0001f) n++;
            if (Math.Abs(value.StartTime) > 0.0000001) n++;
            if (Math.Abs(value.PlaybackRate - 1f) > 0.0001f) n++;
            if (value.Extra != null && value.Extra.Count > 0) n++;
            writer.WriteMapHeader(n);
            writer.Write("type"); writer.WriteUInt16((ushort)UInkBlockType.Media);
            writer.Write("contentId"); writer.WriteUInt32(value.ContentId);
            writer.Write("undoId"); writer.WriteUInt32(value.UndoId);
            writer.Write("path"); writer.Write(value.Path ?? "");
            writer.Write("mimeType"); writer.Write(value.MimeType ?? "");
            if (value.Width.HasValue) { writer.Write("width"); writer.Write(value.Width.Value); }
            if (value.Height.HasValue) { writer.Write("height"); writer.Write(value.Height.Value); }
            if (value.Transform != null)
            {
                writer.Write("transform");
                writer.WriteArrayHeader(6);
                for (int i = 0; i < 6; i++)
                    writer.Write(i < value.Transform.Length ? value.Transform[i] : (i == 0 || i == 3 ? 1f : 0f));
            }
            if (value.Opacity.HasValue) { writer.Write("opacity"); writer.Write(value.Opacity.Value); }
            if (value.PageCount.HasValue) { writer.Write("pageCount"); writer.WriteUInt32(value.PageCount.Value); }
            if (value.PageIndex.HasValue) { writer.Write("pageIndex"); writer.WriteUInt32(value.PageIndex.Value); }
            if (value.Autoplay) { writer.Write("autoplay"); writer.Write(true); }
            if (value.Loop) { writer.Write("loop"); writer.Write(true); }
            if (Math.Abs(value.Volume - 1f) > 0.0001f) { writer.Write("volume"); writer.Write(value.Volume); }
            if (Math.Abs(value.StartTime) > 0.0000001) { writer.Write("startTime"); writer.Write(value.StartTime); }
            if (Math.Abs(value.PlaybackRate - 1f) > 0.0001f) { writer.Write("playbackRate"); writer.Write(value.PlaybackRate); }
            if (value.Extra != null && value.Extra.Count > 0) { writer.Write("extra"); UInkFmt.WriteStringMap(ref writer, value.Extra); }
        }

        public UInkMedia Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            var media = new UInkMedia();
            int count = reader.ReadMapHeader();
            for (int i = 0; i < count; i++)
            {
                var key = reader.ReadString();
                switch (key)
                {
                    case "contentId": media.ContentId = UInkFmt.ReadUInt32Tolerant(ref reader); break;
                    case "undoId": media.UndoId = UInkFmt.ReadUInt32Tolerant(ref reader); break;
                    case "path": media.Path = reader.ReadString(); break;
                    case "mimeType": media.MimeType = reader.ReadString(); break;
                    case "width": media.Width = UInkFmt.ReadSingleTolerant(ref reader); break;
                    case "height": media.Height = UInkFmt.ReadSingleTolerant(ref reader); break;
                    case "transform":
                    {
                        int len = reader.ReadArrayHeader();
                        media.Transform = new float[6];
                        for (int j = 0; j < 6; j++)
                            media.Transform[j] = j < len ? UInkFmt.ReadSingleTolerant(ref reader) : (j == 0 || j == 3 ? 1f : 0f);
                        for (int j = 6; j < len; j++) reader.Skip();
                        break;
                    }
                    case "opacity": media.Opacity = UInkFmt.ReadSingleTolerant(ref reader); break;
                    case "pageCount": media.PageCount = UInkFmt.ReadUInt32Tolerant(ref reader); break;
                    case "pageIndex": media.PageIndex = UInkFmt.ReadUInt32Tolerant(ref reader); break;
                    case "autoplay": media.Autoplay = UInkFmt.ReadBoolTolerant(ref reader); break;
                    case "loop": media.Loop = UInkFmt.ReadBoolTolerant(ref reader); break;
                    case "volume": media.Volume = UInkFmt.ReadSingleTolerant(ref reader); break;
                    case "startTime": media.StartTime = UInkFmt.ReadDoubleTolerant(ref reader); break;
                    case "playbackRate": media.PlaybackRate = UInkFmt.ReadSingleTolerant(ref reader); break;
                    case "extra": media.Extra = UInkFmt.ReadStringMap(ref reader); break;
                    default: reader.Skip(); break;
                }
            }
            return media;
        }
    }

    // ============================================================
    // 共享 SerializerOptions：注册全部顶层块 formatters
    // ============================================================
    public static class UInkSerializer
    {
        public static readonly MessagePackSerializerOptions Options;

        static UInkSerializer()
        {
            var resolver = CompositeResolver.Create(
                new IMessagePackFormatter[]
                {
                    UInkHeaderFormatter.Instance,
                    UInkHeaderExtensionFormatter.Instance,
                    UInkCanvasFormatter.Instance,
                    UInkInkFormatter.Instance,
                    UInkShapeFormatter.Instance,
                    UInkMediaFormatter.Instance,
                },
                new IFormatterResolver[] { StandardResolver.Instance });

            Options = MessagePackSerializerOptions.Standard
                .WithResolver(resolver)
                .WithSecurity(MessagePackSecurity.UntrustedData);
        }

        /// <summary>把一个顶层块序列化为一个完整 MessagePack 对象，追加到流末尾。</summary>
        public static void WriteBlock(Stream stream, object block)
        {
            switch (block)
            {
                case UInkHeader h: MessagePackSerializer.Serialize(stream, h, Options); break;
                case UInkHeaderExtension e: MessagePackSerializer.Serialize(stream, e, Options); break;
                case UInkCanvas c: MessagePackSerializer.Serialize(stream, c, Options); break;
                case UInkInk i: MessagePackSerializer.Serialize(stream, i, Options); break;
                case UInkShape s: MessagePackSerializer.Serialize(stream, s, Options); break;
                case UInkMedia m: MessagePackSerializer.Serialize(stream, m, Options); break;
                default: throw new NotSupportedException($"未知 UInk 块类型: {block?.GetType().Name}");
            }
        }

        /// <summary>把一个顶层块写入 MessagePackWriter（供增量追加在已有写入器上继续）。</summary>
        public static void WriteBlock(ref MessagePackWriter writer, object block)
        {
            switch (block)
            {
                case UInkHeader h: UInkHeaderFormatter.Instance.Serialize(ref writer, h, Options); break;
                case UInkHeaderExtension e: UInkHeaderExtensionFormatter.Instance.Serialize(ref writer, e, Options); break;
                case UInkCanvas c: UInkCanvasFormatter.Instance.Serialize(ref writer, c, Options); break;
                case UInkInk i: UInkInkFormatter.Instance.Serialize(ref writer, i, Options); break;
                case UInkShape s: UInkShapeFormatter.Instance.Serialize(ref writer, s, Options); break;
                case UInkMedia m: UInkMediaFormatter.Instance.Serialize(ref writer, m, Options); break;
                default: throw new NotSupportedException($"未知 UInk 块类型: {block?.GetType().Name}");
            }
        }
    }
}
