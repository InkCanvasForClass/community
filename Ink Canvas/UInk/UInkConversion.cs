using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas.UInk
{
    /// <summary>
    /// ICC WPF 墨迹模型 ⇄ UInk Ink 块 的双向转换。
    /// 语义（对应方案决策）：
    ///  - inkType 统一 = Pen(1)；
    ///  - 块级 opacity 承载 ICC 的 alpha（AARRGGBB 的 A）：写 opacity = A / 255f，读回 A = (byte)(opacity * 255f)；
    ///  - 剩余 DrawingAttributes（Width/Height/FitToCurve/IsHighlighter/IgnorePressure/StylusTip）经 extra["icc:da"]
    ///    私有键无损往返；读取端有 icc:da 用 icc:da，否则从块级字段推导（外部文件回退，透明度仍尊重块级 opacity）。
    /// </summary>
    public static class UInkConversion
    {
        /// <summary>私有扩展键：序列化的 ICC DrawingAttributes 字符串。</summary>
        public const string IccDaKey = "icc:da";

        // ---------- Stroke → UInkInk ----------

        public static UInkInk StrokeToInk(Stroke stroke, uint contentId = 0, uint undoId = 0)
        {
            var da = stroke.DrawingAttributes;
            var ink = new UInkInk
            {
                ContentId = contentId,
                UndoId = undoId,
                InkType = (int)UInkInkType.Pen,          // 统一普通笔
                Color = new UInkColor { Fallback = RgbOf(da.Color) },
                Opacity = da.Color.A / 255f,             // 块级 opacity 承载 alpha
                Texture = 0,
                Extra = new Dictionary<string, string> { [IccDaKey] = SerializeDrawingAttributes(da) },
            };

            var sps = stroke.StylusPoints;
            bool ignorePressure = da.IgnorePressure;
            float baseWidth = (float)da.Width;
            for (int i = 0; i < sps.Count; i++)
            {
                var p = sps[i];
                // UInk width = 该点完整直径；WPF 渲染宽度 = DA.Width * PressureFactor
                float width = ignorePressure
                    ? Math.Max(0.01f, baseWidth)
                    : Math.Max(0.01f, baseWidth * p.PressureFactor);
                ink.Points.Add(new UInkInkPoint
                {
                    X = i == 0 ? (float)p.X : (float)(p.X - sps[i - 1].X), // 首点绝对，后续相对
                    Y = i == 0 ? (float)p.Y : (float)(p.Y - sps[i - 1].Y),
                    Width = width,
                });
            }
            return ink;
        }

        public static List<UInkInk> StrokesToInks(StrokeCollection strokes, uint startContentId = 0, uint undoId = 0)
        {
            var inks = new List<UInkInk>(strokes.Count);
            uint cid = startContentId;
            foreach (Stroke s in strokes)
                inks.Add(StrokeToInk(s, cid++, undoId));
            return inks;
        }

        // ---------- UInkInk → Stroke ----------

        public static StrokeCollection InksToStrokes(IEnumerable<UInkInk> inks)
        {
            var strokes = new StrokeCollection();
            foreach (var ink in inks)
            {
                var stroke = InkToStroke(ink);
                if (stroke != null) strokes.Add(stroke);
            }
            return strokes;
        }

        public static Stroke InkToStroke(UInkInk ink)
        {
            if (ink.Points == null || ink.Points.Count == 0) return null;

            // 块级透明度：alpha = opacity * 255（尊重外部文件，保证透明度统一）
            byte alpha = (byte)Math.Max(0, Math.Min(255, Math.Round(ink.Opacity * 255f)));

            DrawingAttributes da;
            if (ink.Extra != null && ink.Extra.TryGetValue(IccDaKey, out var daStr) && !string.IsNullOrEmpty(daStr))
            {
                da = ParseDrawingAttributes(daStr);
                var c = da.Color; c.A = alpha; da.Color = c; // 块级 opacity 覆盖 alpha
            }
            else
            {
                da = new DrawingAttributes();
                uint fb = ink.Color?.Fallback ?? 0;
                da.Color = Color.FromArgb(alpha, (byte)(fb >> 16), (byte)(fb >> 8), (byte)fb);
            }

            float maxWidth = ink.Points.Max(p => p.Width);
            if (maxWidth <= 0f) maxWidth = 1f;
            da.Width = maxWidth;
            da.Height = maxWidth;

            var sps = new StylusPointCollection();
            float cx = 0f, cy = 0f;
            foreach (var p in ink.Points)
            {
                cx += p.X;   // 累加 delta 还原绝对坐标
                cy += p.Y;
                float pressure = da.IgnorePressure
                    ? 0.5f
                    : Math.Max(0f, Math.Min(1f, p.Width / maxWidth));
                sps.Add(new StylusPoint(cx, cy, pressure));
            }
            return new Stroke(sps) { DrawingAttributes = da };
        }

        // ---------- UInkShape → Stroke（读侧渲染） ----------

        /// <summary>
        /// 把 UInkShape 渲染为一条 Stroke：按几何生成轮廓点列（闭合图形回绕首点），
        /// 用 stroke 样式（color/opacity/width）着色；仅 fill 时用 fill 颜色描轮廓近似。
        /// dashArray/markers 目前按实线近似（规范允许渲染差异）。
        /// </summary>
        public static Stroke ShapeToStroke(UInkShape shape)
        {
            if (shape?.Geometry == null) return null;
            var points = ComputeShapeOutlinePoints(shape.ShapeType, shape.Geometry);
            if (points.Count < 2) return null;

            var color = shape.Stroke?.Color ?? shape.Fill?.Color ?? new UInkColor();
            float opacity = shape.Stroke?.Opacity ?? shape.Fill?.Opacity ?? 1f;
            float width = shape.Stroke != null && shape.Stroke.Width > 0
                ? shape.Stroke.Width
                : (shape.Fill != null ? 8f : 4f); // 仅填充时用较粗描边近似

            uint fb = color.Fallback & 0xFFFFFF;
            var da = new DrawingAttributes
            {
                Color = Color.FromArgb(
                    (byte)Math.Max(0, Math.Min(255, Math.Round(opacity * 255f))),
                    (byte)(fb >> 16), (byte)(fb >> 8), (byte)fb),
                Width = width,
                Height = width,
                FitToCurve = false,
            };
            var sps = new StylusPointCollection();
            foreach (var (x, y) in points)
                sps.Add(new StylusPoint(x, y, 0.5f));
            return new Stroke(sps) { DrawingAttributes = da };
        }

        /// <summary>内容块 → Stroke 分发（Ink→InkToStroke，Shape→ShapeToStroke，Media→null）。供撤回适配/映射器使用。</summary>
        public static Stroke BlockToStroke(IUInkContentBlock block) => block switch
        {
            UInkInk ink => InkToStroke(ink),
            UInkShape shape => ShapeToStroke(shape),
            _ => null,
        };

        private static List<(double x, double y)> ComputeShapeOutlinePoints(int shapeType, UInkShapeGeometry geometry)
        {
            var list = new List<(double, double)>();
            switch (shapeType)
            {
                case (int)UInkShapeType.Line:
                case (int)UInkShapeType.Polyline:
                case (int)UInkShapeType.Polygon:
                {
                    if (geometry is UInkLineGeometry line && line.Points.Count >= 2)
                    {
                        foreach (var p in line.Points)
                            list.Add((p.X, p.Y));
                        if (shapeType == (int)UInkShapeType.Polygon && line.Points.Count >= 3)
                            list.Add((line.Points[0].X, line.Points[0].Y)); // 闭合
                    }
                    break;
                }
                case (int)UInkShapeType.Rectangle:
                case (int)UInkShapeType.Ellipse:
                {
                    if (geometry is UInkRectGeometry r)
                    {
                        var rx = r.Width / 2f;
                        var ry = r.Height / 2f;
                        if (shapeType == (int)UInkShapeType.Rectangle)
                        {
                            AddCorner(list, r.CenterX, r.CenterY, rx, ry, r.Rotation ?? 0f, -1, -1);
                            AddCorner(list, r.CenterX, r.CenterY, rx, ry, r.Rotation ?? 0f, +1, -1);
                            AddCorner(list, r.CenterX, r.CenterY, rx, ry, r.Rotation ?? 0f, +1, +1);
                            AddCorner(list, r.CenterX, r.CenterY, rx, ry, r.Rotation ?? 0f, -1, +1);
                            AddCorner(list, r.CenterX, r.CenterY, rx, ry, r.Rotation ?? 0f, -1, -1); // 闭合
                        }
                        else
                        {
                            AddEllipsePoints(list, r.CenterX, r.CenterY, rx, ry, r.Rotation ?? 0f);
                        }
                    }
                    break;
                }
                case (int)UInkShapeType.Square:
                {
                    if (geometry is UInkSquareGeometry s)
                    {
                        var half = s.Size / 2f;
                        AddCorner(list, s.CenterX, s.CenterY, half, half, s.Rotation ?? 0f, -1, -1);
                        AddCorner(list, s.CenterX, s.CenterY, half, half, s.Rotation ?? 0f, +1, -1);
                        AddCorner(list, s.CenterX, s.CenterY, half, half, s.Rotation ?? 0f, +1, +1);
                        AddCorner(list, s.CenterX, s.CenterY, half, half, s.Rotation ?? 0f, -1, +1);
                        AddCorner(list, s.CenterX, s.CenterY, half, half, s.Rotation ?? 0f, -1, -1);
                    }
                    break;
                }
                case (int)UInkShapeType.Circle:
                {
                    if (geometry is UInkCircleGeometry c)
                        AddEllipsePoints(list, c.CenterX, c.CenterY, c.Radius, c.Radius, 0f);
                    break;
                }
            }
            return list;
        }

        private static void AddCorner(List<(double, double)> list, float cx, float cy,
            float rx, float ry, float rot, int sx, int sy)
        {
            double dx = rx * sx, dy = ry * sy;
            double cos = Math.Cos(rot), sin = Math.Sin(rot);
            list.Add((cx + dx * cos - dy * sin, cy + dx * sin + dy * cos));
        }

        private static void AddEllipsePoints(List<(double, double)> list, float cx, float cy,
            float rx, float ry, float rot)
        {
            const int n = 64;
            double cos = Math.Cos(rot), sin = Math.Sin(rot);
            for (int i = 0; i < n; i++)
            {
                double t = 2.0 * Math.PI * i / n;
                double ex = rx * Math.Cos(t), ey = ry * Math.Sin(t);
                list.Add((cx + ex * cos - ey * sin, cy + ex * sin + ey * cos));
            }
            if (list.Count > 0) list.Add(list[0]); // 闭合
        }

        // ---------- DrawingAttributes 私有序列化（extra["icc:da"]） ----------

        private static string SerializeDrawingAttributes(DrawingAttributes da)
        {
            var sb = new StringBuilder();
            sb.Append("Color=").Append(da.Color.ToString()).Append(';');
            sb.Append("Width=").Append(da.Width.ToString(CultureInfo.InvariantCulture)).Append(';');
            sb.Append("Height=").Append(da.Height.ToString(CultureInfo.InvariantCulture)).Append(';');
            sb.Append("FitToCurve=").Append(da.FitToCurve).Append(';');
            sb.Append("IsHighlighter=").Append(da.IsHighlighter).Append(';');
            sb.Append("IgnorePressure=").Append(da.IgnorePressure).Append(';');
            sb.Append("StylusTip=").Append(da.StylusTip).Append(';');
            return sb.ToString();
        }

        private static DrawingAttributes ParseDrawingAttributes(string s)
        {
            var da = new DrawingAttributes();
            foreach (var part in s.Split(';'))
            {
                var kv = part.Split('=');
                if (kv.Length != 2) continue;
                var key = kv[0].Trim();
                var value = kv[1].Trim();
                switch (key)
                {
                    case "Color":
                        if (ColorConverter.ConvertFromString(value) is Color c) da.Color = c;
                        break;
                    case "Width":
                        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var w)) da.Width = w;
                        break;
                    case "Height":
                        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var h)) da.Height = h;
                        break;
                    case "FitToCurve":
                        if (bool.TryParse(value, out var f)) da.FitToCurve = f;
                        break;
                    case "IsHighlighter":
                        if (bool.TryParse(value, out var hg)) da.IsHighlighter = hg;
                        break;
                    case "IgnorePressure":
                        if (bool.TryParse(value, out var ip)) da.IgnorePressure = ip;
                        break;
                    case "StylusTip":
                        da.StylusTip = Enum.TryParse<StylusTip>(value, out var tip) ? tip : StylusTip.Ellipse;
                        break;
                }
            }
            return da;
        }

        private static uint RgbOf(Color c) => (uint)((c.R << 16) | (c.G << 8) | c.B);
    }
}
