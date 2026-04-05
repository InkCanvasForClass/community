using System;
using System.Threading.Tasks;
using System.Windows.Ink;

namespace Ink_Canvas.Helpers
{
    public sealed class InkRecognitionManager
    {
        private static InkRecognitionManager _instance;
        private static readonly object _lock = new object();

        private ModernInkProcessor _modernProcessor;
        private ModernInkAnalyzer _modernAnalyzer;
        private bool _isModernSystemAvailable;
        private bool _isInitialized;

        public static InkRecognitionManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                            _instance = new InkRecognitionManager();
                    }
                }

                return _instance;
            }
        }

        private InkRecognitionManager()
        {
            Initialize();
        }

        private void Initialize()
        {
            try
            {
                var tryModern = WinRtInkShapeRecognizer.IsApiAvailable && Environment.Is64BitProcess;

                _isModernSystemAvailable = false;
                if (tryModern)
                {
                    try
                    {
                        _modernProcessor = new ModernInkProcessor();
                        _modernAnalyzer = new ModernInkAnalyzer();
                        _isModernSystemAvailable = true;
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile("WinRT 墨迹初始化失败: " + ex.Message, LogHelper.LogType.Warning);
                        _isModernSystemAvailable = false;
                        _modernProcessor = null;
                        _modernAnalyzer = null;
                    }
                }

                _isInitialized = true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile("墨迹识别管理器初始化失败: " + ex.Message, LogHelper.LogType.Error);
                _isInitialized = false;
            }
        }

        public Task<InkShapeRecognitionResult> RecognizeShapeAsync(
            StrokeCollection strokes,
            ShapeRecognitionEngineMode mode)
        {
            if (!_isInitialized || strokes == null || strokes.Count == 0)
                return Task.FromResult(InkShapeRecognitionResult.Empty);

            try
            {
                if (ShapeRecognitionRouter.ResolveUseWinRt(mode)
                    && WinRtInkShapeRecognizer.IsApiAvailable)
                {
                    return RecognizeShapeWinRtOnDispatcherContext(strokes);
                }

                var legacy = InkRecognizeHelper.RecognizeShapeIACore(strokes);
                return Task.FromResult(InkRecognizeHelper.FromIACoreOrEmpty(legacy));
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile("墨迹形状识别失败: " + ex.Message, LogHelper.LogType.Error);
                return Task.FromResult(InkShapeRecognitionResult.Empty);
            }
        }

        private static async Task<InkShapeRecognitionResult> RecognizeShapeWinRtOnDispatcherContext(
            StrokeCollection strokes)
        {
            return await WinRtInkShapeRecognizer.RecognizeShapeAsync(strokes).ConfigureAwait(true);
        }

        /// <param name="applyHandwritingBeautify">为 true 且走 WinRT 时，将识别成功的词替换为手写风格字体的轮廓墨迹（见设置中的字体列表）。</param>
        /// <param name="handwritingFontFamilyList">逗号分隔的字体回退列表（WPF FontFamily）；null 时使用内置默认。</param>
        public Task<StrokeCollection> CorrectInkAsync(
            StrokeCollection strokes,
            ShapeRecognitionEngineMode mode,
            bool applyHandwritingBeautify = false,
            string handwritingFontFamilyList = null)
        {
            if (!_isInitialized)
            {
                LogHelper.WriteLogToFile("[手写体] CorrectInkAsync 跳过：InkRecognitionManager 未初始化。", LogHelper.LogType.Info);
                return Task.FromResult(strokes);
            }

            if (strokes == null || strokes.Count == 0)
            {
                LogHelper.WriteLogToFile("[手写体] CorrectInkAsync 跳过：无笔画。", LogHelper.LogType.Info);
                return Task.FromResult(strokes);
            }

            try
            {
                var useWinRt = ShapeRecognitionRouter.ResolveUseWinRt(mode);
                if (!applyHandwritingBeautify)
                {
                    LogHelper.WriteLogToFile(
                        "[手写体] CorrectInkAsync 跳过：未开启「识别转手写体字形」（applyHandwritingBeautify=false）。笔画数=" +
                        strokes.Count,
                        LogHelper.LogType.Info);
                    return Task.FromResult(strokes);
                }

                if (!useWinRt)
                {
                    LogHelper.WriteLogToFile(
                        "[手写体] CorrectInkAsync 跳过：当前引擎非 WinRT（模式=" + mode + "）。笔画数=" + strokes.Count,
                        LogHelper.LogType.Info);
                    return Task.FromResult(strokes);
                }

                if (!Environment.Is64BitProcess)
                {
                    LogHelper.WriteLogToFile(
                        "[手写体] CorrectInkAsync 跳过：非 64 位进程，WinRT 手写体替换不可用。笔画数=" + strokes.Count,
                        LogHelper.LogType.Info);
                    return Task.FromResult(strokes);
                }

                if (_modernAnalyzer == null)
                {
                    LogHelper.WriteLogToFile(
                        "[手写体] CorrectInkAsync 跳过：ModernInkAnalyzer 未就绪（WinRT 初始化失败？）。笔画数=" +
                        strokes.Count,
                        LogHelper.LogType.Warning);
                    return Task.FromResult(strokes);
                }

                LogHelper.WriteLogToFile(
                    "[手写体] CorrectInkAsync 开始：笔画数=" + strokes.Count +
                    "，字体=" + (string.IsNullOrWhiteSpace(handwritingFontFamilyList) ? "(默认)" : handwritingFontFamilyList.Trim()),
                    LogHelper.LogType.Info);
                return _modernAnalyzer.AnalyzeAndCorrectAsync(strokes, handwritingFontFamilyList);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile("墨迹纠正失败: " + ex.Message, LogHelper.LogType.Error);
                return Task.FromResult(strokes);
            }
        }

        /// <summary>
        /// WinRT 手写体识别（需 64 位进程、Windows 10+ 及系统手写识别组件）。返回分词候选与包围框，供剪贴板或插件使用。
        /// </summary>
        public Task<HandwritingRecognitionResult> RecognizeHandwritingAsync(
            StrokeCollection strokes,
            ShapeRecognitionEngineMode mode)
        {
            if (!_isInitialized || strokes == null || strokes.Count == 0)
                return Task.FromResult(HandwritingRecognitionResult.Empty);

            try
            {
                if (!Environment.Is64BitProcess
                    || !ShapeRecognitionRouter.ResolveUseWinRt(mode)
                    || !WinRtHandwritingRecognizer.IsApiAvailable)
                    return Task.FromResult(HandwritingRecognitionResult.Empty);

                return WinRtHandwritingRecognizer.RecognizeHandwritingAsync(strokes);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile("手写识别失败: " + ex.Message, LogHelper.LogType.Error);
                return Task.FromResult(HandwritingRecognitionResult.Empty);
            }
        }

        public bool IsValidShapeType(string shapeName)
        {
            return !string.IsNullOrEmpty(shapeName)
                   && (shapeName.Contains("Triangle") || shapeName.Contains("Circle")
                       || shapeName.Contains("Rectangle") || shapeName.Contains("Diamond")
                       || shapeName.Contains("Parallelogram") || shapeName.Contains("Square")
                       || shapeName.Contains("Ellipse") || shapeName.Contains("Line")
                       || shapeName.Contains("Arrow"));
        }

        public string GetSystemInfo()
        {
            return _isModernSystemAvailable
                ? $"现代化64位墨迹识别系统 (Windows Runtime API) - 进程架构: {Environment.Is64BitProcess}"
                : $"传统墨迹识别系统 (IACore) - 进程架构: {Environment.Is64BitProcess}";
        }

        public void Dispose()
        {
            _modernProcessor?.Dispose();
            _modernAnalyzer?.Dispose();
            _isInitialized = false;
        }
    }

    internal sealed class ModernInkProcessor : IDisposable
    {
        public ModernInkProcessor()
        {
            if (!WinRtInkShapeRecognizer.IsApiAvailable)
                throw new InvalidOperationException("WinRT 墨迹分析需要 Windows 10 及以上。");
        }

        public Task<InkShapeRecognitionResult> RecognizeShapeAsync(StrokeCollection strokes)
        {
            return WinRtInkShapeRecognizer.RecognizeShapeAsync(strokes);
        }

        public void Dispose()
        {
        }
    }

    internal sealed class ModernInkAnalyzer : IDisposable
    {
        public Task<StrokeCollection> AnalyzeAndCorrectAsync(
            StrokeCollection strokes,
            string handwritingFontFamilyList)
        {
            return WinRtHandwritingRecognizer.ConvertRecognizedTextToHandwritingInkAsync(
                strokes,
                handwritingFontFamilyList);
        }

        public void Dispose()
        {
        }
    }
}
