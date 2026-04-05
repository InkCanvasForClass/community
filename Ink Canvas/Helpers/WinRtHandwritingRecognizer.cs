using OSVersionExtension;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
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
        private static void LogHandwriting(string message, LogHelper.LogType logType = LogHelper.LogType.Info)
        {
            LogHelper.WriteLogToFile("[手写体] " + message, logType);
        }

        public static bool IsApiAvailable =>
            OSVersion.GetOperatingSystem() >= OSVersionExtension.OperatingSystem.Windows10;

        public static void Warmup()
        {
            if (!IsApiAvailable || !Environment.Is64BitProcess) return;
            try
            {
                var d = Application.Current?.Dispatcher;
                if (d == null) return;
                d.BeginInvoke(new Action(async () =>
                {
                    try
                    {
                        await RecognizeHandwritingAsync(new StrokeCollection()).ConfigureAwait(true);
                    }
                    catch
                    {
                        // ignore
                    }
                }));
            }
            catch
            {
                // ignore
            }
        }

        /// <summary>
        /// 将当前笔画集合识别为文字片段（含候选）：先用墨迹分析得到分词与 <see cref="WinAnalysis.InkAnalysisInkWord.RecognizedText"/>，
        /// 再对每一分词用 <see cref="WinRtInk.InkRecognizerContainer"/> 取 <c>GetTextCandidates</c>（与当前 SDK 中部分版本的
        /// <see cref="WinRtInk.InkRecognitionResult"/> 未暴露笔画映射的局限兼容）。
        /// </summary>
        public static async Task<HandwritingRecognitionResult> RecognizeHandwritingAsync(StrokeCollection strokes)
        {
            if (!IsApiAvailable || strokes == null || strokes.Count == 0)
                return HandwritingRecognitionResult.Empty;

            var traceRecognition = strokes.Count > 0;

            try
            {
                var analyzer = new WinAnalysis.InkAnalyzer();
                var idToWpf = new Dictionary<uint, Stroke>();

                foreach (Stroke s in strokes)
                {
                    var ink = WinRtInkShapeRecognizer.CreateInkStrokeFromWpf(s);
                    if (ink == null) continue;
                    analyzer.AddDataForStroke(ink);
                    analyzer.SetStrokeDataKind(ink.Id, WinAnalysis.InkAnalysisStrokeKind.Writing);
                    idToWpf[ink.Id] = s;
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
                            "，有效笔画数=" + idToWpf.Count);
                    return HandwritingRecognitionResult.Empty;
                }

                var wordNodes = analyzer.AnalysisRoot?.FindNodes(WinAnalysis.InkAnalysisNodeKind.InkWord);
                if (wordNodes == null || wordNodes.Count == 0)
                {
                    if (traceRecognition)
                        LogHandwriting("识别：未找到 InkWord 节点（可能被判为绘图或非书写），有效笔画数=" + idToWpf.Count);
                    return HandwritingRecognitionResult.Empty;
                }

                var recognizer = new WinRtInk.InkRecognizerContainer();
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

                    var wbr = word.BoundingRect;
                    var wpfRect = new Rect(wbr.X, wbr.Y, wbr.Width, wbr.Height);
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
                                        candList = cands.ToList();
                                }
                            }
                        }
                    }
                    catch
                    {
                        candList = Array.Empty<string>();
                    }

                    var primary = candList.Count > 0 ? candList[0] : analysisText;
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

                    if (mergedCandidates.Count == 0 && !string.IsNullOrEmpty(primary))
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
            return StrokesFromOutlinedGeometry(geom, templateDa, 0.35);
        }

        private static List<Stroke> StrokesFromOutlinedGeometry(Geometry geometry, DrawingAttributes da, double tolerance)
        {
            var list = new List<Stroke>();
            if (geometry == null || geometry.IsEmpty() || da == null)
                return list;

            Geometry outlined;
            try
            {
                outlined = geometry.GetOutlinedPathGeometry(tolerance, ToleranceType.Absolute);
            }
            catch
            {
                return list;
            }

            if (outlined == null || outlined.IsEmpty())
                return list;

            Geometry flat;
            try
            {
                flat = outlined.GetFlattenedPathGeometry(tolerance, ToleranceType.Absolute);
            }
            catch
            {
                return list;
            }

            if (!(flat is PathGeometry pg))
                return list;

            foreach (var fig in pg.Figures)
            {
                var pts = new StylusPointCollection();
                pts.Add(new StylusPoint(fig.StartPoint.X, fig.StartPoint.Y, 0.5f));
                foreach (var seg in fig.Segments)
                {
                    switch (seg)
                    {
                        case LineSegment ls:
                            pts.Add(new StylusPoint(ls.Point.X, ls.Point.Y, 0.5f));
                            break;
                        case PolyLineSegment pls:
                            foreach (var p in pls.Points)
                                pts.Add(new StylusPoint(p.X, p.Y, 0.5f));
                            break;
                    }
                }

                if (pts.Count >= 2)
                    list.Add(new Stroke(pts) { DrawingAttributes = da.Clone() });
            }

            return list;
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
