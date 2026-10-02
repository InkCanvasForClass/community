using System.Collections.Generic;

namespace Ink_Canvas.Models
{
    /// <summary>
    /// 工具模式（M17）。MainWindow 当前以字符串字面量表示工具模式（<c>_currentToolMode</c>），
    /// 本枚举是其类型化对应物；M18 起逐步替换字符串比较点。
    /// </summary>
    /// <remarks>
    /// 枚举成员名 = 字面量的 PascalCase。字面量集合经 MainWindow/ 全量 grep 穷举确认，
    /// 其中 <c>tool/state</c> URI 会把当前模式字符串原样写入临时文件对外暴露，
    /// 因此这些字符串是持久化锚点，永不可改名。
    /// </remarks>
    internal enum ToolMode
    {
        /// <summary>未识别/未注册的模式字符串。<see cref="ToolModeMapping.FromInternalString"/> 对未知输入返回此值。</summary>
        Unknown = 0,
        /// <summary>光标（闲置）模式，字面量 <c>"cursor"</c>。</summary>
        Cursor,
        /// <summary>画笔模式，字面量 <c>"pen"</c>。</summary>
        Pen,
        /// <summary>快速颜色笔模式，字面量 <c>"color"</c>。</summary>
        Color,
        /// <summary>点擦除模式，字面量 <c>"eraser"</c>。</summary>
        Eraser,
        /// <summary>笔画擦除模式，字面量 <c>"eraserByStrokes"</c>。</summary>
        EraserByStrokes,
        /// <summary>选择工具模式，字面量 <c>"select"</c>。</summary>
        Select,
        /// <summary>形状绘制模式，字面量 <c>"shape"</c>。</summary>
        Shape,
        /// <summary>白板漫游模式，字面量 <c>"roaming"</c>。</summary>
        Roaming
    }

    /// <summary>
    /// <see cref="ToolMode"/> 与现有字符串字面量之间的无损双向往返映射（M17）。
    /// </summary>
    internal static class ToolModeMapping
    {
        private static readonly (ToolMode Mode, string Literal)[] Entries =
        {
            (ToolMode.Cursor, "cursor"),
            (ToolMode.Pen, "pen"),
            (ToolMode.Color, "color"),
            (ToolMode.Eraser, "eraser"),
            (ToolMode.EraserByStrokes, "eraserByStrokes"),
            (ToolMode.Select, "select"),
            (ToolMode.Shape, "shape"),
            (ToolMode.Roaming, "roaming"),
        };

        private static readonly Dictionary<ToolMode, string> ModeToString = BuildModeToString();
        private static readonly Dictionary<string, ToolMode> StringToMode = BuildStringToMode();

        private static Dictionary<ToolMode, string> BuildModeToString()
        {
            var map = new Dictionary<ToolMode, string>();
            foreach (var (mode, literal) in Entries)
            {
                map[mode] = literal;
            }
            return map;
        }

        private static Dictionary<string, ToolMode> BuildStringToMode()
        {
            var map = new Dictionary<string, ToolMode>(System.StringComparer.Ordinal);
            foreach (var (mode, literal) in Entries)
            {
                map[literal] = mode;
            }
            return map;
        }

        /// <summary>
        /// 枚举 → 内部字符串字面量。<see cref="ToolMode.Unknown"/> 返回 <see cref="string.Empty"/>。
        /// </summary>
        internal static string ToInternalString(ToolMode mode)
            => ModeToString.TryGetValue(mode, out var literal) ? literal : string.Empty;

        /// <summary>
        /// 内部字符串字面量 → 枚举。对未注册的字符串（含 null/空串）返回
        /// <see cref="ToolMode.Unknown"/>，不抛异常；比较为序数、大小写敏感。
        /// </summary>
        internal static ToolMode FromInternalString(string mode)
            => mode != null && StringToMode.TryGetValue(mode, out var result) ? result : ToolMode.Unknown;
    }
}
