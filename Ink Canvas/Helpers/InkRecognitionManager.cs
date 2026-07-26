using System;
using System.Threading.Tasks;
using System.Windows.Ink;

namespace Ink_Canvas.Helpers
{
    public sealed class InkRecognitionManager
    {
        private static InkRecognitionManager _instance;
        private static readonly object _lock = new object();
        private readonly object _initSync = new object();

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

        private InkRecognitionManager() { }

        private void Initialize()
        {
            if (_isInitialized) return;

            try
            {
                // 启动阶段只做能力探测，不做 WinRT 组件实例化（避免冷启动延迟）
                _isModernSystemAvailable = WinRtInkShapeRecognizer.IsApiAvailable;
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile("墨迹识别管理器初始化失败: " + ex.Message, LogHelper.LogType.Error);
                _isInitialized = false;
            }
        }

        private void EnsureInitialized()
        {
            if (_isInitialized) return;
            lock (_initSync)
            {
                if (_isInitialized) return;
                Initialize();
            }
        }

        public Task<InkShapeRecognitionResult> RecognizeShapeAsync(
            StrokeCollection strokes,
            ShapeRecognitionEngineMode mode)
        {
            EnsureInitialized();
            if (!_isInitialized || strokes == null || strokes.Count == 0)
                return Task.FromResult(InkShapeRecognitionResult.Empty);

            try
            {
                if (ShapeRecognitionRouter.ResolveUseWinRt(mode)
                    && WinRtInkShapeRecognizer.IsApiAvailable)
                {
                    return RecognizeShapeWinRtOnDispatcherContext(strokes);
                }

                // IACore 必须走 IPC 辅助进程（x86/.NET 4.7.2）。
                // 在 .NET 6 x64 主进程中本地加载 IAWinFX 会失败，故不再本地回退。
                var ipcResult = IpcIACoreClient.Instance.Recognize(strokes);
                return Task.FromResult(ipcResult);
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

        /// <param name="applyHandwritingBeautify">为 true 时，将识别成功的词替换为手写风格字体的轮廓墨迹（见设置中的字体列表）。</param>
        /// <param name="handwritingFontFamilyList">逗号分隔的字体回退列表（WPF FontFamily）；null 时使用内置默认。</param>
        public async Task<StrokeCollection> CorrectInkAsync(
            StrokeCollection strokes,
            ShapeRecognitionEngineMode mode,
            bool applyHandwritingBeautify = false,
            string handwritingFontFamilyList = null)
        {
            EnsureInitialized();
            if (!_isInitialized)
            {
                LogHelper.WriteLogToFile("[手写体] CorrectInkAsync 跳过：InkRecognitionManager 未初始化。", LogHelper.LogType.Info);
                return strokes;
            }

            if (strokes == null || strokes.Count == 0)
            {
                LogHelper.WriteLogToFile("[手写体] CorrectInkAsync 跳过：无笔画。", LogHelper.LogType.Info);
                return strokes;
            }

            try
            {
                if (!applyHandwritingBeautify)
                {
                    LogHelper.WriteLogToFile(
                        "[手写体] CorrectInkAsync 跳过：未开启「手写体纠正」（applyHandwritingBeautify=false）。笔画数=" +
                        strokes.Count,
                        LogHelper.LogType.Info);
                    return strokes;
                }

                // 识别引擎跟随形状引擎：IACore 走 IPC（可注入上下文），WinRT 在本进程；失败回落 WinRT。
                // 字形渲染与引擎无关，统一由 RenderHandwritingGlyphsFromResult 完成。
                LogHelper.WriteLogToFile(
                    "[手写体] CorrectInkAsync 开始：笔画数=" + strokes.Count +
                    "，引擎=" + mode +
                    "，字体=" + (string.IsNullOrWhiteSpace(handwritingFontFamilyList) ? "(默认)" : handwritingFontFamilyList.Trim()),
                    LogHelper.LogType.Info);

                var reco = await RecognizeHandwritingAsync(strokes, mode).ConfigureAwait(true);
                if (reco == null || !reco.IsSuccess)
                {
                    LogHelper.WriteLogToFile(
                        "[手写体] CorrectInkAsync 识别未成功，原样返回笔画。笔画数=" + strokes.Count,
                        LogHelper.LogType.Info);
                    return strokes;
                }

                return WinRtHandwritingRecognizer.RenderHandwritingGlyphsFromResult(
                    strokes, reco, handwritingFontFamilyList);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile("墨迹纠正失败: " + ex.Message, LogHelper.LogType.Error);
                return strokes;
            }
        }

        /// <summary>
        /// 手写体识别（需 Windows 10+ 及系统手写识别组件，或 IACore IPC 辅助进程）。返回分词候选与包围框，供剪贴板或插件使用。
        /// 文字识别引擎跟随形状识别引擎（传入的 <paramref name="mode"/>）：IACore 走 IPC 辅助进程（可注入 Factoid/WordList/WordMode
        /// 等上下文层，UWP WinRT 无法访问）；WinRT 在本进程跑 Windows.UI.Input.Inking.Analysis.InkAnalyzer。
        /// </summary>
        public Task<HandwritingRecognitionResult> RecognizeHandwritingAsync(
            StrokeCollection strokes,
            ShapeRecognitionEngineMode mode)
        {
            EnsureInitialized();
            if (!_isInitialized || strokes == null || strokes.Count == 0)
                return Task.FromResult(HandwritingRecognitionResult.Empty);

            try
            {
                // 文字引擎跟随形状引擎：IACore 形状 → IACore 文字(IPC)；WinRT/Auto 形状 → WinRT 文字。
                if (!ShapeRecognitionRouter.ResolveUseWinRt(mode))
                {
                    // IACore 文字识别：走 IPC 辅助进程（IAWinFX InkAnalyzer + AnalysisHintNode）。
                    // 辅助进程不可用或识别失败时回落 WinRT（若可用），保证功能不丢失。
                    try
                    {
                        IpcIACoreClient.Instance.Start();
                        if (IpcIACoreClient.Instance.IsAvailable)
                        {
                            var hint = BuildIacoreTextHint();
                            var ipcResult = IpcIACoreClient.Instance.RecognizeText(strokes, hint);
                            if (ipcResult != null && ipcResult.IsSuccess)
                                return Task.FromResult(ipcResult);

                            LogHelper.WriteLogToFile(
                                "[手写识别] IACore IPC 文字识别返回空，回落 WinRT。笔画数=" + strokes.Count,
                                LogHelper.LogType.Info);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile(
                            "[手写识别] IACore IPC 文字识别异常，回落 WinRT: " + ex.Message,
                            LogHelper.LogType.Warning);
                    }
                }

                if (!WinRtHandwritingRecognizer.IsApiAvailable)
                    return Task.FromResult(HandwritingRecognitionResult.Empty);

                return WinRtHandwritingRecognizer.RecognizeHandwritingAsync(strokes);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile("手写识别失败: " + ex.Message, LogHelper.LogType.Error);
                return Task.FromResult(HandwritingRecognitionResult.Empty);
            }
        }

        /// <summary>
        /// 从 settings 构造 IACore 文字识别上下文提示。当前按 LCID 推断 CJK 单字模式（WordMode），
        /// 暂不注入 Factoid/WordList（留作后续 UI 暴露）。Hint 区域为空表示作用于全部笔画。
        /// </summary>
        private static IacoreTextHint BuildIacoreTextHint()
        {
            var hint = new IacoreTextHint
            {
                // CJK 一字多笔：启用 WordMode 让识别器优先返回单字/单词结果，减少跨字误并。
                WordMode = HandwritingRecognitionTuning.IsCjkRecognizerActive
            };
            return hint;
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
            if (_isModernSystemAvailable)
                return $"现代化墨迹识别系统 (Windows Runtime API) - 进程架构: {Environment.Is64BitProcess}";
            if (IpcIACoreClient.Instance.IsAvailable)
                return $"传统墨迹识别系统 (IACore via IPC) - 进程架构: {Environment.Is64BitProcess}";
            return $"传统墨迹识别系统 (IACore 本地) - 进程架构: {Environment.Is64BitProcess}";
        }

        public void Dispose()
        {
            _isInitialized = false;
        }
    }
}
