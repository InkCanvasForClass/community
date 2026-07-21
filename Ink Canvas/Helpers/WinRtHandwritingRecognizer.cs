using OSVersionExtension;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using WinAnalysis = global::Windows.UI.Input.Inking.Analysis;
using WinRtInk = global::Windows.UI.Input.Inking;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// WinRT 手写体识别，以及将识别结果用手写风格字体轮廓转为墨迹笔画（「识别转手写体字形」）。
    /// </summary>
    internal static class WinRtHandwritingRecognizer
    {
        private static WinRtInk.InkRecognizer _preferredHandwritingRecognizer;
        private static bool _preferredHandwritingRecognizerResolved;

        private static void LogHandwriting(string message, LogHelper.LogType logType = LogHelper.LogType.Info)
        {
            LogHelper.WriteLogToFile("[手写体] " + message, logType);
        }

        public static bool IsApiAvailable =>
            OSVersion.GetOperatingSystem() >= OSVersionExtension.OperatingSystem.Windows10;

        /// <summary>
        /// 启动阶段不再预热线程内 WinRT 手写管线。历史上曾用 <see cref="WinRtInkShapeRecognizer.CreateMinimalWarmupStrokeCollection"/> 跑全链路，
        /// 会显著拖慢启动；与更早的「空 <see cref="StrokeCollection"/>」一样，此处不再在 Idle 上做任何工作。
        /// 首次真正需要手写识别时由 <see cref="RecognizeHandwritingAsync"/> 承担冷启动成本。
        /// </summary>
        public static void Warmup()
        {
        }

        /// <summary>
        /// 将当前笔画集合识别为文字片段（含候选）：先用墨迹分析得到分词与 <see cref="WinAnalysis.InkAnalysisInkWord.RecognizedText"/>，
        /// 再对每一分词用 <see cref="WinRtInk.InkRecognizerContainer"/> 取 <c>GetTextCandidates</c>（与当前 SDK 中部分版本的
        /// <see cref="WinRtInk.InkRecognitionResult"/> 未暴露笔画映射的局限兼容）。
        /// </summary>
        /// <param name="verboseTrace">为 false 时跳过详细识别日志（用于 <see cref="Warmup"/> 等）。</param>
        public static async Task<HandwritingRecognitionResult> RecognizeHandwritingAsync(
            StrokeCollection strokes,
            bool verboseTrace = true)
        {
            if (!IsApiAvailable || strokes == null || strokes.Count == 0)
                return HandwritingRecognitionResult.Empty;

            var traceRecognition = verboseTrace;

            try
            {
                var recognizer = new WinRtInk.InkRecognizerContainer();
                TryApplyPreferredHandwritingRecognizer(recognizer, traceRecognition);

                var analyzer = new WinAnalysis.InkAnalyzer();
                var idToWpf = new Dictionary<uint, Stroke>();
                var handwritingInputs = CreateNormalizedHandwritingInputs(strokes);

                foreach (var input in handwritingInputs)
                {
                    var ink = WinRtInkShapeRecognizer.CreateInkStrokeFromWpf(input.Analysis);
                    if (ink == null) continue;
                    analyzer.AddDataForStroke(ink);
                    analyzer.SetStrokeDataKind(ink.Id, WinAnalysis.InkAnalysisStrokeKind.Writing);
                    idToWpf[ink.Id] = input.Original;
                }

                if (idToWpf.Count == 0)
                {
                    if (traceRecognition)
                        LogHandwriting("识别：无有效 WinRT 笔画（全部转换失败），输入笔画数=" + strokes.Count);
                    return HandwritingRecognitionResult.Empty;
                }

                var analysisResult = await analyzer.AnalyzeAsync().AsTask().ConfigureAwait(true);
                if (analysisResult == null || analysisResult.Status != WinAnalysis.InkAnalysisStatus.Updated)
                {
                    if (traceRecognition)
                        LogHandwriting(
                            "识别：AnalyzeAsync 未得到 Updated，Status=" +
                            (analysisResult == null ? "null" : analysisResult.Status.ToString()) +
                            "，有效笔画数=" + idToWpf.Count + "，不再执行整批 RecognizeAsync 回退，返回空结果。",
                            LogHelper.LogType.Warning);
                    return HandwritingRecognitionResult.Empty;
                }

                var wordNodes = analyzer.AnalysisRoot?.FindNodes(WinAnalysis.InkAnalysisNodeKind.InkWord);
                if (wordNodes == null || wordNodes.Count == 0)
                {
                    if (traceRecognition)
                        LogHandwriting(
                            "识别：未找到 InkWord 节点（有效笔画数=" + idToWpf.Count +
                            "），不再执行整批 RecognizeAsync 回退，返回空结果。",
                            LogHelper.LogType.Warning);
                    return HandwritingRecognitionResult.Empty;
                }

                var segments = new List<HandwritingWordSegment>();

                foreach (var node in wordNodes)
                {
                    if (!(node is WinAnalysis.InkAnalysisInkWord word))
                        continue;

                    var ids = word.GetStrokeIds();
                    if (ids == null || ids.Count == 0)
                        continue;

                    var group = new List<Stroke>();
                    foreach (var sid in ids)
                    {
                        if (idToWpf.TryGetValue(sid, out var st))
                            group.Add(st);
                    }

                    if (group.Count == 0)
                        continue;

                    var wpfRect = GetOriginalStrokeBounds(group);
                    var analysisText = word.RecognizedText ?? string.Empty;

                    IReadOnlyList<string> candList = Array.Empty<string>();
                    try
                    {
                        if (recognizer != null)
                        {
                            var mini = new WinRtInk.InkStrokeContainer();
                            foreach (var st in group)
                            {
                                var ink = WinRtInkShapeRecognizer.CreateInkStrokeFromWpf(st);
                                if (ink != null)
                                    mini.AddStroke(ink);
                            }

                            var miniStrokes = mini.GetStrokes();
                            if (miniStrokes != null && miniStrokes.Count > 0)
                            {
                                var rr = await recognizer
                                    .RecognizeAsync(mini, WinRtInk.InkRecognitionTarget.All)
                                    .AsTask()
                                    .ConfigureAwait(true);
                                if (rr != null && rr.Count > 0 && rr[0] != null)
                                {
                                    var cands = rr[0].GetTextCandidates();
                            if (cands != null && cands.Count > 0)
                            {
                                candList = cands
                                    .Where(c => !string.IsNullOrWhiteSpace(c))
                                    .ToList();
                            }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (traceRecognition)
                            LogHandwriting("识别：分词候选获取失败，保留 InkWord.RecognizedText。异常=" + ex.Message, LogHelper.LogType.Warning);
                        candList = Array.Empty<string>();
                    }

                    var primary = candList.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)) ?? analysisText;
                    var mergedCandidates = new List<string>();
                    if (candList.Count > 0)
                    {
                        foreach (var c in candList)
                        {
                            if (!string.IsNullOrEmpty(c) && !mergedCandidates.Contains(c))
                                mergedCandidates.Add(c);
                        }
                    }

                    if (!string.IsNullOrEmpty(analysisText) && !mergedCandidates.Contains(analysisText))
                        mergedCandidates.Insert(0, analysisText);

                    if (mergedCandidates.Count == 0 && !string.IsNullOrWhiteSpace(primary))
                        mergedCandidates.Add(primary);

                    segments.Add(new HandwritingWordSegment(
                        primary,
                        mergedCandidates,
                        wpfRect,
                        group));
                }

                if (segments.Count == 0)
                {
                    if (traceRecognition)
                        LogHandwriting("识别：分词列表为空（InkWord 无有效笔画映射）。");
                    return HandwritingRecognitionResult.Empty;
                }

                var hr = new HandwritingRecognitionResult(segments);
                if (traceRecognition)
                {
                    var preview = hr.CombinedText;
                    if (preview.Length > 120)
                        preview = preview.Substring(0, 117) + "...";
                    LogHandwriting(
                        "识别成功：词数=" + segments.Count +
                        "，合并文本=\"" + preview + "\"" +
                        "，进程位数=" + (Environment.Is64BitProcess ? "x64" : "x86"));
                    for (var i = 0; i < segments.Count; i++)
                    {
                        var seg = segments[i];
                        var t = seg.Text ?? "";
                        if (t.Length > 40)
                            t = t.Substring(0, 37) + "...";
                        LogHandwriting(
                            "  词[" + i + "] 文本=\"" + t + "\"，笔画数=" + seg.Strokes.Count +
                            "，候选数=" + (seg.TextCandidates?.Count ?? 0) +
                            "，框=(" + Math.Round(seg.BoundingRectangle.X, 1) + "," +
                            Math.Round(seg.BoundingRectangle.Y, 1) + "," +
                            Math.Round(seg.BoundingRectangle.Width, 1) + "×" +
                            Math.Round(seg.BoundingRectangle.Height, 1) + ")");
                    }
                }

                return hr;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile("WinRT 手写识别失败: " + ex.Message, LogHelper.LogType.Warning);
                if (strokes != null && strokes.Count > 0)
                    LogHandwriting("识别异常：" + ex.Message, LogHelper.LogType.Warning);
                return HandwritingRecognitionResult.Empty;
            }
        }

        private static void TryApplyPreferredHandwritingRecognizer(
            WinRtInk.InkRecognizerContainer container,
            bool logDetail)
        {
            if (container == null)
                return;
            try
            {
                if (!_preferredHandwritingRecognizerResolved)
                {
                    _preferredHandwritingRecognizerResolved = true;
                    var all = container.GetRecognizers();
                    _preferredHandwritingRecognizer = SelectBestInkRecognizer(all);
                    if (logDetail)
                    {
                        if (_preferredHandwritingRecognizer != null)
                            LogHandwriting("识别器：已选用 \"" + _preferredHandwritingRecognizer.Name + "\"。");
                        else if (all != null && all.Count > 0)
                            LogHandwriting("识别器：未匹配到与 UI/区域语言对应的引擎，使用系统默认（共 " + all.Count + " 个）。");
                    }
                }

                if (_preferredHandwritingRecognizer != null)
                    container.SetDefaultRecognizer(_preferredHandwritingRecognizer);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile("[手写体] 设置默认手写识别器失败: " + ex.Message, LogHelper.LogType.Warning);
            }
        }

        private static WinRtInk.InkRecognizer SelectBestInkRecognizer(
            IReadOnlyList<WinRtInk.InkRecognizer> list)
        {
            if (list == null || list.Count == 0)
                return null;

            var culture = PrimaryHandwritingCulture();
            var lang = (culture?.TwoLetterISOLanguageName ?? string.Empty).ToLowerInvariant();
            var name = culture?.Name ?? string.Empty;

            bool wantZhHans = lang == "zh" &&
                              (name.IndexOf("hans", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               name.Equals("zh-cn", StringComparison.OrdinalIgnoreCase) ||
                               name.Equals("zh-sg", StringComparison.OrdinalIgnoreCase) ||
                               (name.IndexOf("hant", StringComparison.OrdinalIgnoreCase) < 0 &&
                                !name.Equals("zh-tw", StringComparison.OrdinalIgnoreCase) &&
                                !name.Equals("zh-hk", StringComparison.OrdinalIgnoreCase) &&
                                !name.Equals("zh-mo", StringComparison.OrdinalIgnoreCase)));

            bool wantZhHant = lang == "zh" &&
                              (name.IndexOf("hant", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               name.Equals("zh-tw", StringComparison.OrdinalIgnoreCase) ||
                               name.Equals("zh-hk", StringComparison.OrdinalIgnoreCase) ||
                               name.Equals("zh-mo", StringComparison.OrdinalIgnoreCase));

            WinRtInk.InkRecognizer Pick(Func<string, bool> match)
            {
                foreach (var r in list)
                {
                    var n = r?.Name;
                    if (string.IsNullOrEmpty(n))
                        continue;
                    if (match(n))
                        return r;
                }

                return null;
            }

            if (wantZhHans)
            {
                var r = Pick(n =>
                    n.IndexOf("简体", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("簡體", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (n.IndexOf("中文", StringComparison.OrdinalIgnoreCase) >= 0 &&
                     (n.IndexOf("简体", StringComparison.OrdinalIgnoreCase) >= 0 ||
                      n.IndexOf("簡體", StringComparison.OrdinalIgnoreCase) >= 0)) ||
                    (n.IndexOf("Chinese", StringComparison.OrdinalIgnoreCase) >= 0 &&
                     (n.IndexOf("Simplified", StringComparison.OrdinalIgnoreCase) >= 0 ||
                      n.IndexOf("Hans", StringComparison.OrdinalIgnoreCase) >= 0 ||
                      n.IndexOf("PRC", StringComparison.OrdinalIgnoreCase) >= 0)));
                if (r != null)
                    return r;
                r = Pick(n =>
                    n.IndexOf("中文", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("Chinese", StringComparison.OrdinalIgnoreCase) >= 0);
                if (r != null)
                    return r;
            }
            else if (wantZhHant)
            {
                var r = Pick(n =>
                    n.IndexOf("繁体", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("繁體", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (n.IndexOf("中文", StringComparison.OrdinalIgnoreCase) >= 0 &&
                     (n.IndexOf("繁体", StringComparison.OrdinalIgnoreCase) >= 0 ||
                      n.IndexOf("繁體", StringComparison.OrdinalIgnoreCase) >= 0)) ||
                    (n.IndexOf("Chinese", StringComparison.OrdinalIgnoreCase) >= 0 &&
                     (n.IndexOf("Traditional", StringComparison.OrdinalIgnoreCase) >= 0 ||
                      n.IndexOf("Hant", StringComparison.OrdinalIgnoreCase) >= 0 ||
                      n.IndexOf("Taiwan", StringComparison.OrdinalIgnoreCase) >= 0 ||
                      n.IndexOf("Hong Kong", StringComparison.OrdinalIgnoreCase) >= 0)));
                if (r != null)
                    return r;
                r = Pick(n =>
                    n.IndexOf("中文", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("Chinese", StringComparison.OrdinalIgnoreCase) >= 0);
                if (r != null)
                    return r;
            }
            else if (lang == "ja")
            {
                var r = Pick(n =>
                    n.IndexOf("Japanese", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("日本語", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("日语", StringComparison.OrdinalIgnoreCase) >= 0);
                if (r != null)
                    return r;
            }
            else if (lang == "en")
            {
                var r = Pick(n => n.IndexOf("English", StringComparison.OrdinalIgnoreCase) >= 0);
                if (r != null)
                    return r;
            }

            if (lang == "zh")
            {
                var r = Pick(n =>
                    n.IndexOf("中文", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("Chinese", StringComparison.OrdinalIgnoreCase) >= 0);
                if (r != null)
                    return r;
            }

            return null;
        }

        private static CultureInfo PrimaryHandwritingCulture()
        {
            var ui = CultureInfo.CurrentUICulture;
            var ct = CultureInfo.CurrentCulture;
            if (string.Equals(ui.TwoLetterISOLanguageName, "zh", StringComparison.OrdinalIgnoreCase))
                return ui;
            if (string.Equals(ct.TwoLetterISOLanguageName, "zh", StringComparison.OrdinalIgnoreCase))
                return ct;
            return ui;
        }

        private sealed class NormalizedHandwritingInput
        {
            public Stroke Original { get; set; }
            public Stroke Analysis { get; set; }
        }

        private static List<NormalizedHandwritingInput> CreateNormalizedHandwritingInputs(StrokeCollection strokes)
        {
            var inputs = new List<NormalizedHandwritingInput>();
            if (strokes == null || strokes.Count == 0)
                return inputs;

            var valid = strokes.Cast<Stroke>()
                .Where(s => s?.StylusPoints != null && s.StylusPoints.Count > 0)
                .ToList();
            if (valid.Count == 0)
                return inputs;

            var heights = valid.Select(s => Math.Max(1.0, s.GetBounds().Height)).OrderBy(h => h).ToList();
            var referenceHeight = heights[heights.Count / 2];
            var ordered = valid.OrderBy(s => s.GetBounds().Top + s.GetBounds().Height / 2.0).ToList();
            var rows = new List<List<Stroke>>();
            var rowCenters = new List<double>();
            var rowTolerance = Math.Max(12.0, referenceHeight * 0.9);

            foreach (var stroke in ordered)
            {
                var bounds = stroke.GetBounds();
                var centerY = bounds.Top + bounds.Height / 2.0;
                var bestRow = -1;
                var bestDistance = double.MaxValue;
                for (var i = 0; i < rowCenters.Count; i++)
                {
                    var distance = Math.Abs(centerY - rowCenters[i]);
                    if (distance <= rowTolerance && distance < bestDistance)
                    {
                        bestRow = i;
                        bestDistance = distance;
                    }
                }

                if (bestRow < 0)
                {
                    bestRow = rows.Count;
                    rows.Add(new List<Stroke>());
                    rowCenters.Add(centerY);
                }

                rows[bestRow].Add(stroke);
                rowCenters[bestRow] = rowCenters[bestRow] +
                    (centerY - rowCenters[bestRow]) / rows[bestRow].Count;
            }

            foreach (var row in rows)
            {
                var rowBounds = Rect.Empty;
                foreach (var stroke in row)
                    rowBounds = rowBounds.IsEmpty ? stroke.GetBounds() : Rect.Union(rowBounds, stroke.GetBounds());

                var rowHeight = Math.Max(1.0, rowBounds.Height);
                var scaleY = Math.Max(0.5, Math.Min(2.0, referenceHeight / rowHeight));
                var rowCenter = rowBounds.Top + rowBounds.Height / 2.0;
                var angle = GetRowAngle(row);
                var rotate = Math.Abs(angle) > 20.0 * Math.PI / 180.0;
                var transform = new Matrix();
                transform.Translate(-rowBounds.Left, -rowCenter);
                if (rotate)
                    transform.Rotate(-angle * 180.0 / Math.PI);
                transform.Scale(1.0, scaleY);
                transform.Translate(rowBounds.Left, rowCenter);

                foreach (var original in row)
                {
                    var analysis = CloneStrokeForRecognition(original, transform);
                    if (analysis != null)
                        inputs.Add(new NormalizedHandwritingInput { Original = original, Analysis = analysis });
                }
            }

            return inputs;
        }

        private static Stroke CloneStrokeForRecognition(Stroke source, Matrix transform)
        {
            var clone = CloneStroke(source);
            if (clone == null)
                return null;
            clone.Transform(transform, false);
            return clone;
        }

        private static Stroke CloneStroke(Stroke source)
        {
            if (source?.StylusPoints == null || source.StylusPoints.Count == 0)
                return null;
            return new Stroke(new StylusPointCollection(source.StylusPoints.ToArray()))
            {
                DrawingAttributes = source.DrawingAttributes?.Clone() ?? new DrawingAttributes()
            };
        }

        private static double GetRowAngle(IReadOnlyList<Stroke> row)
        {
            if (row == null || row.Count == 0)
                return 0;
            var first = row[0].StylusPoints[0].ToPoint();
            var lastStroke = row[row.Count - 1];
            var last = lastStroke.StylusPoints[lastStroke.StylusPoints.Count - 1].ToPoint();
            return Math.Atan2(last.Y - first.Y, last.X - first.X);
        }

        private static Rect GetOriginalStrokeBounds(IReadOnlyList<Stroke> strokes)
        {
            if (strokes == null || strokes.Count == 0)
                return Rect.Empty;
            var bounds = strokes[0].GetBounds();
            for (var i = 1; i < strokes.Count; i++)
                bounds = Rect.Union(bounds, strokes[i].GetBounds());
            return bounds;
        }

        private static Rect UnionStrokeBounds(StrokeCollection strokes)
        {
            if (strokes == null || strokes.Count == 0)
                return Rect.Empty;

            var r = strokes[0].GetBounds();
            for (var i = 1; i < strokes.Count; i++)
                r = Rect.Union(r, strokes[i].GetBounds());
            return r;
        }

        private const string DefaultHandwritingFontFamilyList = "Ink Free,KaiTi,Segoe Script";

        /// <summary>
        /// 识别手写词后，将「有识别文本」的分词替换为指定手写风格字体的字形轮廓墨迹；未识别或空文本的词保留原笔画。
        /// </summary>
        public static async Task<StrokeCollection> ConvertRecognizedTextToHandwritingInkAsync(
            StrokeCollection strokes,
            string handwritingFontFamilyList)
        {
            if (!IsApiAvailable || strokes == null || strokes.Count == 0)
            {
                if (strokes != null && strokes.Count > 0 && !IsApiAvailable)
                    LogHandwriting("字形替换：跳过，IsApiAvailable=false。");
                return strokes;
            }

            var fontList = string.IsNullOrWhiteSpace(handwritingFontFamilyList)
                ? DefaultHandwritingFontFamilyList
                : handwritingFontFamilyList.Trim();
            LogHandwriting(
                "字形替换开始：输入笔画数=" + strokes.Count +
                "，字体链=\"" + fontList + "\"" +
                "，PixelsPerDip=" + Math.Round(GetPixelsPerDipSafe(), 3));

            try
            {
                var reco = await RecognizeHandwritingAsync(strokes).ConfigureAwait(true);
                if (!reco.IsSuccess || reco.Words == null || reco.Words.Count == 0)
                {
                    LogHandwriting(
                        "字形替换中止：识别未成功（IsSuccess=" + reco.IsSuccess +
                        "，词数=" + (reco.Words?.Count ?? 0) + "），原样返回笔画。");
                    return strokes;
                }

                var firstStrokeToSegment = new Dictionary<Stroke, HandwritingWordSegment>();
                foreach (var w in reco.Words)
                {
                    if (w?.Strokes == null || w.Strokes.Count == 0)
                        continue;
                    var ordered = w.Strokes.OrderBy(st => IndexOfStrokeInCollection(strokes, st)).ToList();
                    var first = ordered[0];
                    if (!firstStrokeToSegment.ContainsKey(first))
                        firstStrokeToSegment[first] = w;
                }

                if (firstStrokeToSegment.Count == 0)
                {
                    LogHandwriting("字形替换中止：无法建立「首笔画→分词」映射，原样返回。");
                    return strokes;
                }

                var consumed = new HashSet<Stroke>();
                var result = new StrokeCollection();
                var pixelsPerDip = GetPixelsPerDipSafe();
                var replacedWordCount = 0;
                var keptOriginalWordCount = 0;
                var glyphStrokeTotal = 0;

                foreach (Stroke s in strokes)
                {
                    if (consumed.Contains(s))
                        continue;

                    if (!firstStrokeToSegment.TryGetValue(s, out var seg))
                    {
                        result.Add(s);
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(seg.Text))
                    {
                        LogHandwriting(
                            "  分词：文本为空，保留原笔画，笔画数=" + seg.Strokes.Count);
                        keptOriginalWordCount++;
                        foreach (var z in seg.Strokes)
                        {
                            if (!consumed.Contains(z))
                            {
                                result.Add(z);
                                consumed.Add(z);
                            }
                        }

                        continue;
                    }

                    var templateDa = seg.Strokes[0]?.DrawingAttributes?.Clone() ?? new DrawingAttributes();
                    OutlineAttributesForGlyphInk(templateDa);

                    var glyphStrokes = CreateHandwritingGlyphStrokes(
                        seg.Text.Trim(),
                        seg.BoundingRectangle,
                        templateDa,
                        fontList,
                        pixelsPerDip);

                    if (glyphStrokes == null || glyphStrokes.Count == 0)
                    {
                        LogHandwriting(
                            "  分词：字形轮廓生成失败，保留原笔画。文本=\"" +
                            (seg.Text.Length > 30 ? seg.Text.Substring(0, 27) + "..." : seg.Text) + "\"");
                        keptOriginalWordCount++;
                        foreach (var z in seg.Strokes)
                        {
                            if (!consumed.Contains(z))
                            {
                                result.Add(z);
                                consumed.Add(z);
                            }
                        }

                        continue;
                    }

                    foreach (var nk in glyphStrokes)
                        result.Add(nk);
                    glyphStrokeTotal += glyphStrokes.Count;
                    replacedWordCount++;
                    LogHandwriting(
                        "  分词：已替换为手写体字形墨迹，文本=\"" +
                        (seg.Text.Length > 30 ? seg.Text.Substring(0, 27) + "..." : seg.Text) +
                        "\"，生成笔画数=" + glyphStrokes.Count + "，移除原笔画数=" + seg.Strokes.Count);

                    foreach (var z in seg.Strokes)
                        consumed.Add(z);
                }

                LogHandwriting(
                    "字形替换结束：输出笔画数=" + result.Count +
                    "（输入=" + strokes.Count + "），替换词数=" + replacedWordCount +
                    "，保留原迹词数=" + keptOriginalWordCount +
                    "，字形子笔画合计=" + glyphStrokeTotal);
                return result;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile("WinRT 手写体字形替换失败: " + ex.Message, LogHelper.LogType.Warning);
                LogHandwriting("字形替换异常：" + ex, LogHelper.LogType.Warning);
                return strokes;
            }
        }

        private static int IndexOfStrokeInCollection(StrokeCollection collection, Stroke stroke)
        {
            if (collection == null || stroke == null)
                return int.MaxValue;
            for (var i = 0; i < collection.Count; i++)
            {
                if (ReferenceEquals(collection[i], stroke))
                    return i;
            }

            return int.MaxValue;
        }

        private static void OutlineAttributesForGlyphInk(DrawingAttributes da)
        {
            if (da == null) return;
            var w = Math.Max(0.8, Math.Min(da.Width, da.Height) * 0.2);
            da.Width = w;
            da.Height = w;
            da.StylusTip = StylusTip.Ellipse;
            da.IsHighlighter = false;
        }

        private static double GetPixelsPerDipSafe()
        {
            try
            {
                if (Application.Current?.MainWindow is Visual v)
                    return VisualTreeHelper.GetDpi(v).PixelsPerDip;
            }
            catch
            {
                // ignore
            }

            return 1.0;
        }

        private static Typeface ResolveHandwritingTypeface(string fontFamilyList)
        {
            try
            {
                var ff = new FontFamily(fontFamilyList ?? DefaultHandwritingFontFamilyList);
                return new Typeface(ff, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            }
            catch
            {
                return new Typeface(
                    SystemFonts.MessageFontFamily,
                    SystemFonts.MessageFontStyle,
                    SystemFonts.MessageFontWeight,
                    FontStretches.Normal);
            }
        }

        private static List<Stroke> CreateHandwritingGlyphStrokes(
            string text,
            Rect placeRect,
            DrawingAttributes templateDa,
            string fontFamilyList,
            double pixelsPerDip)
        {
            var list = new List<Stroke>();
            if (string.IsNullOrEmpty(text) || placeRect.Width < 1 || placeRect.Height < 1)
                return list;

            var typeface = ResolveHandwritingTypeface(fontFamilyList);
            var culture = CultureInfo.CurrentCulture;
            var em = Math.Max(6.0, placeRect.Height * 0.72);
            FormattedText ft = null;

            for (var i = 0; i < 14; i++)
            {
                ft = new FormattedText(
                    text,
                    culture,
                    FlowDirection.LeftToRight,
                    typeface,
                    em,
                    Brushes.Black,
                    new NumberSubstitution(NumberCultureSource.Text, culture, NumberSubstitutionMethod.Context),
                    TextFormattingMode.Display,
                    pixelsPerDip);

                if (ft.Width <= placeRect.Width * 0.96 && ft.Height <= placeRect.Height * 0.96)
                    break;

                em *= 0.9;
                if (em < 4.5)
                    break;
            }

            if (ft == null || ft.Width < 0.5 || ft.Height < 0.5)
                return list;

            var scale = Math.Min(
                placeRect.Width * 0.94 / Math.Max(1e-6, ft.Width),
                placeRect.Height * 0.94 / Math.Max(1e-6, ft.Height));
            var tx = placeRect.Left + (placeRect.Width - ft.Width * scale) / 2.0;
            var ty = placeRect.Top + (placeRect.Height - ft.Height * scale) / 2.0;

            Geometry geom;
            try
            {
                geom = ft.BuildGeometry(new Point(0, 0));
            }
            catch
            {
                return list;
            }

            if (geom == null || geom.IsEmpty())
                return list;

            var m = new Matrix(scale, 0, 0, scale, tx, ty);
            geom.Transform = new MatrixTransform(m);

            var filled = FilledGlyphStroke.TryCreate(geom, templateDa);
            if (filled == null)
                return list;

            list.Add(filled);
            return list;
        }
    }

    /// <summary>
    /// 把字形几何作为「实心填充」绘制的笔画。仍是 WPF <see cref="Stroke"/>，可被 InkCanvas 选择/移动/删除，
    /// 但渲染时直接 DrawGeometry(brush, null, geom)，不再走 StylusPoints 描边路径。
    /// </summary>
    internal sealed class FilledGlyphStroke : Stroke
    {
        private readonly Geometry _geometry;

        private FilledGlyphStroke(StylusPointCollection pts, Geometry geometry, DrawingAttributes da)
            : base(pts)
        {
            _geometry = geometry;
            if (da != null)
                DrawingAttributes = da.Clone();
        }

        public static FilledGlyphStroke TryCreate(Geometry geometry, DrawingAttributes templateDa)
        {
            if (geometry == null || geometry.IsEmpty())
                return null;

            var b = geometry.Bounds;
            if (b.IsEmpty || b.Width < 0.5 || b.Height < 0.5)
                return null;

            // StylusPoints 用 bounds 四角，保证命中测试 / 选区 / 包围盒计算正常。
            var pts = new StylusPointCollection
            {
                new StylusPoint(b.Left,  b.Top,    0.5f),
                new StylusPoint(b.Right, b.Top,    0.5f),
                new StylusPoint(b.Right, b.Bottom, 0.5f),
                new StylusPoint(b.Left,  b.Bottom, 0.5f),
            };

            return new FilledGlyphStroke(pts, geometry, templateDa);
        }

        protected override void DrawCore(DrawingContext drawingContext, DrawingAttributes drawingAttributes)
        {
            if (drawingContext == null || _geometry == null)
                return;

            var color = drawingAttributes != null ? drawingAttributes.Color : Colors.Black;
            drawingContext.DrawGeometry(new SolidColorBrush(color), null, _geometry);
        }
    }

    /// <summary>单个手写词片段的识别结果。</summary>
    public sealed class HandwritingWordSegment
    {
        public HandwritingWordSegment(
            string text,
            IReadOnlyList<string> textCandidates,
            Rect boundingRectangle,
            IReadOnlyList<Stroke> strokes)
        {
            Text = text ?? string.Empty;
            TextCandidates = textCandidates ?? Array.Empty<string>();
            BoundingRectangle = boundingRectangle;
            Strokes = strokes ?? Array.Empty<Stroke>();
        }

        public string Text { get; }
        public IReadOnlyList<string> TextCandidates { get; }
        public Rect BoundingRectangle { get; }
        public IReadOnlyList<Stroke> Strokes { get; }
    }

    /// <summary>一次手写识别批次的汇总结果。</summary>
    public sealed class HandwritingRecognitionResult
    {
        public static readonly HandwritingRecognitionResult Empty = new HandwritingRecognitionResult();

        private HandwritingRecognitionResult()
        {
            Words = Array.Empty<HandwritingWordSegment>();
            IsSuccess = false;
            CombinedText = string.Empty;
        }

        public HandwritingRecognitionResult(IReadOnlyList<HandwritingWordSegment> words)
        {
            Words = words ?? Array.Empty<HandwritingWordSegment>();
            IsSuccess = Words.Count > 0;
            CombinedText = string.Join("", Words.Select(w => w.Text ?? string.Empty));
        }

        public bool IsSuccess { get; }
        public IReadOnlyList<HandwritingWordSegment> Words { get; }
        public string CombinedText { get; }
    }
}
