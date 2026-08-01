using Ink_Canvas.Helpers;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Ink;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// <see cref="IRecognitionService"/> 的宿主实现：包装 <see cref="InkRecognitionManager"/>，
    /// 把宿主枚举与识别结果映射为 SDK 的 DTO。识别引擎不可用时返回失败结果，不抛异常。
    /// </summary>
    internal sealed class RecognitionService : IRecognitionService
    {
        private static InkRecognitionManager Manager => InkRecognitionManager.Instance;

        private static ShapeRecognitionEngineMode MapEngine(PluginRecognitionEngine engine) => engine switch
        {
            PluginRecognitionEngine.IACore => ShapeRecognitionEngineMode.IACore,
            PluginRecognitionEngine.WinRT => ShapeRecognitionEngineMode.WinRT,
            _ => ShapeRecognitionEngineMode.Auto,
        };

        public async Task<PluginShapeRecognitionResult> RecognizeShapeAsync(StrokeCollection strokes,
            PluginRecognitionEngine engine = PluginRecognitionEngine.Auto)
        {
            var result = await Manager.RecognizeShapeAsync(strokes, MapEngine(engine)).ConfigureAwait(false);
            return new PluginShapeRecognitionResult
            {
                IsSuccess = result.IsSuccess,
                ShapeName = result.ShapeName,
                Centroid = result.Centroid,
                HotPoints = result.HotPoints,
                ShapeWidth = result.ShapeWidth,
                ShapeHeight = result.ShapeHeight,
                StrokesToRemove = result.StrokesToRemove,
            };
        }

        public async Task<PluginHandwritingResult> RecognizeHandwritingAsync(StrokeCollection strokes,
            PluginRecognitionEngine engine = PluginRecognitionEngine.Auto)
        {
            var result = await Manager.RecognizeHandwritingAsync(strokes, MapEngine(engine)).ConfigureAwait(false);

            var words = new List<PluginHandwritingWord>();
            if (result.Words != null)
            {
                foreach (var word in result.Words)
                {
                    words.Add(new PluginHandwritingWord
                    {
                        TextCandidates = word.TextCandidates != null
                            ? new List<string>(word.TextCandidates)
                            : new List<string>(),
                        BoundingRectangle = word.BoundingRectangle,
                    });
                }
            }

            return new PluginHandwritingResult
            {
                IsSuccess = result.IsSuccess,
                CombinedText = result.CombinedText,
                Words = words,
            };
        }

        public Task<StrokeCollection> CorrectInkAsync(StrokeCollection strokes,
            PluginRecognitionEngine engine = PluginRecognitionEngine.Auto,
            bool applyHandwritingBeautify = false,
            string handwritingFontFamilyList = null)
            => Manager.CorrectInkAsync(strokes, MapEngine(engine), applyHandwritingBeautify, handwritingFontFamilyList);

        public bool IsValidShapeType(string shapeName)
            => Manager.IsValidShapeType(shapeName);

        public string GetSystemInfo()
            => Manager.GetSystemInfo();
    }
}
