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
                // 把 settings 中的 LCID 覆盖推到 Tuning（LCID 变化时自动失效缓存重解析）。
                HandwritingRecognitionTuning.ApplyFromSettings(
                    MainWindow.Settings?.InkToShape?.HandwritingLanguageOverrideLcid ??
                    HandwritingRecognitionTuning.LcidFollowSystem);
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

                // C1：CJK 防拆字回检合并。InkAnalyzer 按水平间距切 InkWord，CJK 一字多笔常被误拆。
                // 把「相邻 InkWord 水平间距 < 0.3×字高 且 垂直重叠 > 0.5×字高」的节点合并为一组，
                // 整体重新 RecognizeAsync 取候选，避免部件级误识别。西文不合并（词间距本就大）。
                var cjkMergeActive = HandwritingRecognitionTuning.IsCjkRecognizerActive;
                var wordGroups = BuildCjkMergedWordGroups(wordNodes, idToWpf, cjkMergeActive, traceRecognition);

                var segments = new List<HandwritingWordSegment>();

                foreach (var wg in wordGroups)
                {
                    if (wg.Strokes.Count == 0)
                        continue;

                    var wpfRect = GetOriginalStrokeBounds(wg.Strokes);
                    var analysisText = wg.CombinedRecognizedText;

                    IReadOnlyList<string> candList = Array.Empty<string>();
                    try
                    {
                        candList = await RecognizeStrokeGroupAsync(recognizer, wg.Strokes).ConfigureAwait(true);
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
                        wg.Strokes));
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
            // 识别器选择/LCID/FOD/缓存统一在 HandwritingRecognitionTuning 内；本方法仅做日志门面。
            HandwritingRecognitionTuning.TryApplyPreferredRecognizer(container, logDetail);
        }

        /// <summary>对一个笔画组单独跑 RecognizeAsync 取文本候选（供 C1 合并组与原 per-word 路径复用）。</summary>
        private static async Task<IReadOnlyList<string>> RecognizeStrokeGroupAsync(
            WinRtInk.InkRecognizerContainer recognizer,
            IReadOnlyList<Stroke> group)
        {
            if (recognizer == null || group == null || group.Count == 0)
                return Array.Empty<string>();

            var mini = new WinRtInk.InkStrokeContainer();
            foreach (var st in group)
            {
                var ink = WinRtInkShapeRecognizer.CreateInkStrokeFromWpf(st);
                if (ink != null)
                    mini.AddStroke(ink);
            }

            var miniStrokes = mini.GetStrokes();
            if (miniStrokes == null || miniStrokes.Count == 0)
                return Array.Empty<string>();

            var rr = await recognizer
                .RecognizeAsync(mini, WinRtInk.InkRecognitionTarget.All)
                .AsTask()
                .ConfigureAwait(true);

            if (rr == null || rr.Count == 0 || rr[0] == null)
                return Array.Empty<string>();

            var cands = rr[0].GetTextCandidates();
            if (cands == null || cands.Count == 0)
                return Array.Empty<string>();

            return cands.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
        }

        /// <summary>
        /// C1：CJK 防拆字合并。InkAnalyzer 按水平间距切 InkWord，CJK 一字多笔常被误拆成相邻 InkWord。
        /// 把「相邻 InkWord 水平间距 &lt; 0.3×字高 且 垂直重叠 &gt; 0.5×字高」的节点合并为一组，
        /// 整组重新识别取候选。西文（cjkMergeActive=false）每个 InkWord 独立成组，行为与旧 per-word 路径一致。
        /// </summary>
        private static List<WordGroup> BuildCjkMergedWordGroups(
            IReadOnlyList<WinAnalysis.IInkAnalysisNode> wordNodes,
            Dictionary<uint, Stroke> idToWpf,
            bool cjkMergeActive,
            bool traceRecognition)
        {
            // 1) 把每个 InkWord 解析为 (笔画组, 原始包围框, RecognizedText)。
            var units = new List<(List<Stroke> Strokes, Rect Bounds, string Text)>();
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

                units.Add((group, GetOriginalStrokeBounds(group), word.RecognizedText ?? string.Empty));
            }

            // 西文或不合并：每个 InkWord 独立成组，行为等价于原 per-word 循环。
            if (!cjkMergeActive || units.Count <= 1)
            {
                return units.Select(u =>
                {
                    var g = new WordGroup();
                    g.Strokes.AddRange(u.Strokes);
                    g.CombinedRecognizedText = u.Text;
                    return g;
                }).ToList();
            }

            // CJK：按水平位置排序，相邻「水平间距 < 0.3×字高 且 垂直重叠 > 0.5×字高」合并为一组。
            var ordered = units.OrderBy(u => u.Bounds.IsEmpty ? 0 : u.Bounds.Left).ToList();
            var groups = new List<WordGroup>();
            var current = new WordGroup();
            current.Strokes.AddRange(ordered[0].Strokes);
            current.CombinedRecognizedText = ordered[0].Text ?? string.Empty;
            var currentBounds = ordered[0].Bounds;

            for (var i = 1; i < ordered.Count; i++)
            {
                var u = ordered[i];
                var charHeight = Math.Max(currentBounds.Height, u.Bounds.Height);
                if (charHeight <= 0) charHeight = 1;

                var horizontalGap = currentBounds.IsEmpty || u.Bounds.IsEmpty
                    ? 0.0
                    : Math.Max(0.0, u.Bounds.Left - currentBounds.Right);
                var verticalOverlap = VerticalOverlapRatio(currentBounds, u.Bounds);

                if (horizontalGap < 0.3 * charHeight && verticalOverlap > 0.5)
                {
                    // 合并到当前组：笔画累加、文本拼接（仅作候选兜底，主识别以整组重新 RecognizeAsync 为准）。
                    current.Strokes.AddRange(u.Strokes);
                    current.CombinedRecognizedText =
                        (current.CombinedRecognizedText ?? string.Empty) + (u.Text ?? string.Empty);
                    currentBounds = currentBounds.IsEmpty ? u.Bounds : Rect.Union(currentBounds, u.Bounds);
                }
                else
                {
                    groups.Add(current);
                    current = new WordGroup();
                    current.Strokes.AddRange(u.Strokes);
                    current.CombinedRecognizedText = u.Text ?? string.Empty;
                    currentBounds = u.Bounds;
                }
            }

            groups.Add(current);

            if (traceRecognition)
                LogHandwriting("CJK 防拆字合并：InkWord 数=" + units.Count + " → 合并后组数=" + groups.Count);

            return groups;
        }

        /// <summary>两框垂直相交高度占较小框高度的比例（0~1），用于判定是否同一行同一字。</summary>
        private static double VerticalOverlapRatio(Rect a, Rect b)
        {
            if (a.IsEmpty || b.IsEmpty) return 0;
            var top = Math.Max(a.Top, b.Top);
            var bottom = Math.Min(a.Bottom, b.Bottom);
            var overlap = Math.Max(0.0, bottom - top);
            var minH = Math.Min(a.Height, b.Height);
            if (minH <= 0) return 0;
            return overlap / minH;
        }

        private sealed class NormalizedHandwritingInput
        {
            public Stroke Original { get; set; }
            public Stroke Analysis { get; set; }
        }

        /// <summary>
        /// C1 合并产物：一个或多个 InkWord 的笔画合集 + 拼接的 RecognizedText（仅作候选兜底，
        /// 主识别以整组重新 RecognizeAsync 取 GetTextCandidates 为准）。
        /// </summary>
        private sealed class WordGroup
        {
            public List<Stroke> Strokes { get; } = new List<Stroke>();
            public string CombinedRecognizedText { get; set; } = string.Empty;
        }

        private static List<NormalizedHandwritingInput> CreateNormalizedHandwritingInputs(StrokeCollection strokes)
        {
            var inputs = new List<NormalizedHandwritingInput>();
            if (strokes == null || strokes.Count == 0)
                return inputs;

            // CJK：一字多笔、部件间距本就大于拉丁字母间距。行内 Y 缩放会改变部件相对位置、
            // 进一步误导 InkAnalyzer 的水平间距分词（把一字拆成多个 InkWord）。CJK 下跳过 Y 归一化，
            // 保留原始几何比例；拉丁/西文仍做行高归一化（其分词本就按词间距，归一化有益）。
            var cjkActive = HandwritingRecognitionTuning.IsCjkRecognizerActive;

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
                // CJK 不做行内 Y 缩放（scaleY=1.0）；西文保持原归一化逻辑。
                var scaleY = cjkActive ? 1.0 : Math.Max(0.5, Math.Min(2.0, referenceHeight / rowHeight));
                var rowCenter = rowBounds.Top + rowBounds.Height / 2.0;
                var angle = GetRowAngle(row);
                // CJK 下也不做倾斜矫正（旋转同样改变部件相对位置，且 CJK 通常横平竖直书写）。
                var rotate = !cjkActive && Math.Abs(angle) > 20.0 * Math.PI / 180.0;
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
        /// 识别走本类 WinRT <see cref="RecognizeHandwritingAsync"/>；字形渲染与引擎无关。
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

            var reco = await RecognizeHandwritingAsync(strokes).ConfigureAwait(true);
            return RenderHandwritingGlyphsFromResult(strokes, reco, handwritingFontFamilyList);
        }

        /// <summary>
        /// 用已识别的分词结果，把「有识别文本」的分词替换为手写风格字体的字形轮廓墨迹；未识别或空文本的词保留原笔画。
        /// 仅做字形渲染（WPF 字体轮廓），不依赖任何识别引擎——识别结果可来自 WinRT 或 IACore IPC。
        /// </summary>
        public static StrokeCollection RenderHandwritingGlyphsFromResult(
            StrokeCollection strokes,
            HandwritingRecognitionResult reco,
            string handwritingFontFamilyList)
        {
            if (strokes == null || strokes.Count == 0)
                return strokes;
            if (reco == null || !reco.IsSuccess || reco.Words == null || reco.Words.Count == 0)
            {
                LogHandwriting(
                    "字形替换中止：识别未成功（IsSuccess=" + (reco?.IsSuccess ?? false) +
                    "，词数=" + (reco?.Words?.Count ?? 0) + "），原样返回笔画。");
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
                LogHelper.WriteLogToFile("手写体字形替换失败: " + ex.Message, LogHelper.LogType.Warning);
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
            // 先按高度给 em（CJK 方块字、单字场景应填满高度），再仅在宽度溢出时按比例缩 em，
            // 避免「多字词被原 14 次 0.9 缩放过度缩小」的问题。最小 em 取 box 高度 40%（相对下限，替代原绝对 4.5px）。
            var minEm = Math.Max(4.5, placeRect.Height * 0.40);
            var em = Math.Max(minEm, placeRect.Height * 0.92);
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

                // 高度必然 ≤ box（em 由 height 派生）；只需保证宽度不超过 box 的 1.05 倍（允许轻微外溢，
                // 因为识别词的实际包围框可能比理想字形略窄）。
                if (ft.Width <= placeRect.Width * 1.05)
                    break;

                em *= 0.9;
                if (em < minEm)
                    break;
            }

            if (ft == null || ft.Width < 0.5 || ft.Height < 0.5)
                return list;

            // 最终等比缩放：以高度为主轴填满 box，宽度超 box 时按宽度收紧；二者取小。
            var scaleByHeight = placeRect.Height * 0.94 / Math.Max(1e-6, ft.Height);
            var scaleByWidth = placeRect.Width * 0.94 / Math.Max(1e-6, ft.Width);
            var scale = Math.Min(scaleByHeight, scaleByWidth);
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
