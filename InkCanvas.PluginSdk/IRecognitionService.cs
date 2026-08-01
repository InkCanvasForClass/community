using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Media;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 墨迹识别后端：自动 / IACore / WinRT。自动模式在 Windows 10 及以上默认 WinRT。
    /// </summary>
    public enum PluginRecognitionEngine
    {
        /// <summary>自动选择后端。</summary>
        Auto = 0,
        /// <summary>传统 IACore 识别（走 IPC 辅助进程）。</summary>
        IACore = 1,
        /// <summary>Windows Runtime 识别。</summary>
        WinRT = 2,
    }

    /// <summary>
    /// 与具体识别后端无关的形状识别结果。宿主识别到形状时，
    /// <see cref="StrokesToRemove"/> 指明应移除的原始笔画，插件可据此用标准形状替换。
    /// </summary>
    public sealed class PluginShapeRecognitionResult
    {
        /// <summary>是否识别成功。</summary>
        public bool IsSuccess { get; set; }

        /// <summary>识别出的形状名，如 "Triangle"、"Circle"、"Rectangle"、"Arrow" 等。</summary>
        public string ShapeName { get; set; } = "";

        /// <summary>形状中心点。</summary>
        public Point Centroid { get; set; }

        /// <summary>形状关键点。</summary>
        public PointCollection HotPoints { get; set; } = new PointCollection();

        /// <summary>形状宽度（DIP）。</summary>
        public double ShapeWidth { get; set; }

        /// <summary>形状高度（DIP）。</summary>
        public double ShapeHeight { get; set; }

        /// <summary>识别为形状、应从画布移除的原始笔画。</summary>
        public StrokeCollection StrokesToRemove { get; set; } = new StrokeCollection();
    }

    /// <summary>
    /// 手写识别结果中的单个分词：候选文本与包围框。
    /// </summary>
    public sealed class PluginHandwritingWord
    {
        /// <summary>候选文本，按置信度降序。</summary>
        public List<string> TextCandidates { get; set; } = new List<string>();

        /// <summary>该词在画布上的包围框。</summary>
        public Rect BoundingRectangle { get; set; }
    }

    /// <summary>一次手写识别批次的汇总结果。</summary>
    public sealed class PluginHandwritingResult
    {
        /// <summary>是否识别成功（有词结果）。</summary>
        public bool IsSuccess { get; set; }

        /// <summary>全部词拼接后的文本。</summary>
        public string CombinedText { get; set; } = "";

        /// <summary>分词列表。</summary>
        public List<PluginHandwritingWord> Words { get; set; } = new List<PluginHandwritingWord>();
    }

    /// <summary>
    /// 墨迹识别服务：包装宿主的 WinRT / IACore 双引擎识别能力，
    /// 供插件做手写转文字、图形识别/纠正与手写体美化。
    /// <para>识别引擎可能需要系统组件（Windows 10+ 手写识别或 IACore IPC 辅助进程），
    /// 不可用时返回 <c>IsSuccess=false</c> 的结果，不会抛出异常。</para>
    /// </summary>
    public interface IRecognitionService
    {
        /// <summary>
        /// 形状识别：把手写笔画识别为几何形状（三角形/圆/矩形/箭头等）。
        /// </summary>
        /// <param name="strokes">待识别的笔画。</param>
        /// <param name="engine">识别后端。</param>
        /// <returns>识别结果；失败时 <see cref="PluginShapeRecognitionResult.IsSuccess"/> 为 false。</returns>
        Task<PluginShapeRecognitionResult> RecognizeShapeAsync(StrokeCollection strokes,
            PluginRecognitionEngine engine = PluginRecognitionEngine.Auto);

        /// <summary>
        /// 手写转文字识别，返回分词候选与包围框。
        /// </summary>
        /// <param name="strokes">待识别的笔画。</param>
        /// <param name="engine">识别后端（文字引擎跟随形状引擎选择）。</param>
        /// <returns>识别结果；失败时 <see cref="PluginHandwritingResult.IsSuccess"/> 为 false。</returns>
        Task<PluginHandwritingResult> RecognizeHandwritingAsync(StrokeCollection strokes,
            PluginRecognitionEngine engine = PluginRecognitionEngine.Auto);

        /// <summary>
        /// 墨迹纠正（手写体美化）：识别成功后把原始笔画替换为手写风格字体的轮廓墨迹。
        /// </summary>
        /// <param name="strokes">待纠正的笔画。</param>
        /// <param name="engine">识别后端。</param>
        /// <param name="applyHandwritingBeautify">
        /// 为 true 时用识别结果替换为手写风格字体轮廓墨迹；false 时原样返回。</param>
        /// <param name="handwritingFontFamilyList">
        /// 逗号分隔的字体回退列表（WPF FontFamily）；null 时使用宿主内置默认。</param>
        /// <returns>纠正后的墨迹；识别失败或未启用美化时原样返回。</returns>
        Task<StrokeCollection> CorrectInkAsync(StrokeCollection strokes,
            PluginRecognitionEngine engine = PluginRecognitionEngine.Auto,
            bool applyHandwritingBeautify = false,
            string handwritingFontFamilyList = null);

        /// <summary>判断形状名是否为宿主支持的标准形状类型。</summary>
        bool IsValidShapeType(string shapeName);

        /// <summary>当前识别引擎的系统信息（用于诊断/展示）。</summary>
        string GetSystemInfo();
    }
}
