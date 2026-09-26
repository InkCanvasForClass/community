using System.Windows;
using System.Windows.Ink;
using System.Windows.Media;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 文字转标准 WPF 墨迹的选项。
    /// </summary>
    public sealed class PluginInkTextOptions
    {
        /// <summary>优先使用的字体；为空时使用系统默认字体回退。</summary>
        public string FontFamily { get; set; } = "Microsoft YaHei UI";

        /// <summary>逗号分隔的字体回退列表。</summary>
        public string FallbackFontFamilies { get; set; } = "Microsoft YaHei UI,Microsoft YaHei,Segoe UI Symbol";

        /// <summary>字体字号（DIP）。</summary>
        public double FontSize { get; set; } = 32;

        /// <summary>行高（DIP）；小于等于零时由字体自动决定。</summary>
        public double LineHeight { get; set; }

        /// <summary>最大排版宽度（DIP）；小于等于零表示不限制。</summary>
        public double MaxWidth { get; set; }

        /// <summary>墨迹颜色。</summary>
        public Color Color { get; set; } = Colors.Black;

        /// <summary>生成轮廓墨迹的线宽（DIP）。</summary>
        public double StrokeWidth { get; set; } = 1.5;

        /// <summary>是否让 WPF 对笔画进行曲线拟合。</summary>
        public bool FitToCurve { get; set; } = true;
    }

    /// <summary>
    /// 宿主统一的文字转墨迹服务。返回的 StrokeCollection 为调用方独立副本。
    /// </summary>
    public interface IInkTextService
    {
        /// <summary>把文字渲染为从原点开始的画布坐标墨迹。</summary>
        StrokeCollection CreateTextStrokes(string text, PluginInkTextOptions options = null);

        /// <summary>
        /// 把文字作为墨迹插入当前画布。center 为 null 时使用画布中心；
        /// 当前页冻结或画布不可用时返回 false，插入进入宿主撤销历史。
        /// </summary>
        bool TryInsertTextAsInk(string text, Point? center = null, PluginInkTextOptions options = null);
    }
}
