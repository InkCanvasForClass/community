using Ink_Canvas.Helpers;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Media;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Ink_Canvas
{
    public partial class MainWindow
    {
        /// <summary>
        /// 当前幻灯片可用的文字行（已换算到墨迹画布坐标系）；null 表示尚未取过。
        /// </summary>
        private List<PptTextLineRect> _pptTextLineRects;

        /// <summary>
        /// 上面那份文字行缓存的对应页码（1 起）。页码变化即视为缓存失效。
        /// </summary>
        private int _pptTextLineRectsSlide = -1;

        /// <summary>
        /// 缓存所属的放映窗口句柄，用于识别「换了一个放映窗口」的情况。
        /// </summary>
        private IntPtr _pptTextLineRectsHwnd = IntPtr.Zero;

        /// <summary>
        /// 上一次取文字行时的幻灯片内容屏幕矩形，作为窗口尺寸变化时的复用兜底。
        /// </summary>
        private Rect _pptLastSlideScreenRect = Rect.Empty;

        /// <summary>
        /// 纠正结果缓存的容量上限。撤销会回到纠正前的笔画，
        /// 重做时据此还原成纠正后的条形墨迹，避免退化成原始歪线。
        /// </summary>
        private const int MaxPptHighlighterSnapHistory = 256;

        private readonly Dictionary<Stroke, Stroke> _pptHighlighterSnapHistory =
            new Dictionary<Stroke, Stroke>(MaxPptHighlighterSnapHistory);

        /// <summary>
        /// 纠正后的笔画 → 原始笔画，用于撤销时还原。
        /// </summary>
        private readonly Dictionary<Stroke, Stroke> _pptHighlighterSnapReverse =
            new Dictionary<Stroke, Stroke>(MaxPptHighlighterSnapHistory);

        /// <summary>
        /// 记录纠正映射的插入顺序，用于按容量淘汰最旧的条目。
        /// </summary>
        private readonly Queue<Stroke> _pptHighlighterSnapOrder = new Queue<Stroke>();

        #region PPT 荧光笔墨迹纠正：入口

        /// <summary>
        /// 判断当前是否应执行「PPT 荧光笔墨迹纠正」。
        /// </summary>
        /// <remarks>
        /// 三个条件缺一不可：
        /// 1. 设置里打开了开关；
        /// 2. 当前笔类型是荧光笔；
        /// 3. PPT 处于放映状态且已连上联动（不在放映时取不到文字行，也没有纠正的意义）。
        /// </remarks>
        private bool ShouldApplyPptHighlighterSnap()
        {
            try
            {
                if (!Settings.PowerPointSettings.EnableHighlighterSnapToText) return false;
                if (penType != 1) return false;
                if (_pptManager == null || !_pptManager.IsInSlideShow) return false;
                if (_currentSlideShowPosition <= 0) return false;
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[PptHighlightSnap] 条件判定异常: {ex.Message}", LogHelper.LogType.Warning);
                return false;
            }
        }

        /// <summary>
        /// 尝试把刚写完的荧光笔笔画纠正到 PPT 文字行上。
        /// </summary>
        /// <param name="stroke">刚提交的笔画。</param>
        /// <param name="isHighlighterStroke">该笔画是否按荧光笔渲染。</param>
        /// <returns><c>true</c> 表示已完成替换，调用方应停止后续墨迹美化流水线。</returns>
        /// <remarks>
        /// 纯几何逻辑全部委托给 <see cref="PptHighlighterSnapper"/>，本方法只负责
        /// 「取文字行 → 调用算法 → 替换笔画 → 记录可撤销映射」。
        /// 任何一步失败都返回 false，让笔画走原有处理流程，绝不吞掉用户的笔迹。
        /// </remarks>
        private bool TryApplyPptHighlighterSnap(Stroke stroke, bool isHighlighterStroke)
        {
            if (stroke == null || !isHighlighterStroke) return false;
            if (!ShouldApplyPptHighlighterSnap()) return false;

            try
            {
                var lines = GetPptTextLineRectsForCanvas();
                if (lines == null || lines.Count == 0) return false;

                // 自动贴合文字大小：按文字行自身的字形尺寸决定条厚与长度，
                // 解决大字号下「条又细又短、只包住一小条」的问题。
                PptTextFitOptions fitOptions = null;
                if (Settings.PowerPointSettings.HighlighterSnapFitTextSize)
                {
                    fitOptions = PptTextFitOptions.FromSettings(
                        Settings.PowerPointSettings.HighlighterSnapTextFillRatio,
                        Settings.PowerPointSettings.HighlighterSnapTextExtendRatio);
                }

                var snapped = PptHighlighterSnapper.TrySnap(
                    stroke,
                    lines,
                    Settings.Canvas.HighlighterWidth,
                    isHighlighterStroke,
                    fitOptions,
                    out var reason);

                if (snapped == null)
                {
                    // 「未命中文字行」属于高频正常情况，只在开启详细日志时不打扰。
                    if (!string.IsNullOrEmpty(reason))
                        LogHelper.WriteLogToFile($"[PptHighlightSnap] 跳过纠正: {reason}", LogHelper.LogType.Trace);
                    return false;
                }

                if (!ReplaceStrokeWithSnapped(stroke, snapped)) return false;

                LogHelper.WriteLogToFile(
                    $"[PptHighlightSnap] 已纠正荧光笔 → 第 {_currentSlideShowPosition} 页 " +
                    $"({snapped.GetBounds().Left:F0},{snapped.GetBounds().Top:F0}) " +
                    $"{snapped.GetBounds().Width:F0}x{snapped.GetBounds().Height:F0}",
                    LogHelper.LogType.Event);

                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[PptHighlightSnap] 纠正失败，保留原始笔迹: {ex}", LogHelper.LogType.Warning);
                return false;
            }
        }

        /// <summary>
        /// 用纠正后的高亮条替换原始笔画，并记录可撤销的映射关系。
        /// </summary>
        /// <remarks>
        /// 直接改写 <see cref="StrokeCollection"/> 会触发墨迹变更事件，
        /// 由既有的事件处理链路正常写入撤销历史，本方法不需要也不应该再手动提交一次历史。
        /// </remarks>
        private bool ReplaceStrokeWithSnapped(Stroke original, Stroke snapped)
        {
            if (original == null || snapped == null) return false;
            if (inkCanvas == null) return false;

            var strokes = inkCanvas.Strokes;
            int index = strokes.IndexOf(original);
            if (index < 0)
            {
                // 笔画不在画布上（例如已被墨迹渐隐移除），不做替换。
                return false;
            }

            strokes[index] = snapped;

            RecordPptHighlighterSnapMapping(original, snapped);
            return true;
        }

        /// <summary>
        /// 记录「原始笔画 ↔ 纠正后笔画」的映射，并按容量上限淘汰最旧条目。
        /// </summary>
        private void RecordPptHighlighterSnapMapping(Stroke original, Stroke snapped)
        {
            _pptHighlighterSnapHistory[original] = snapped;

            if (!_pptHighlighterSnapReverse.ContainsKey(snapped))
                _pptHighlighterSnapOrder.Enqueue(snapped);
            _pptHighlighterSnapReverse[snapped] = original;

            while (_pptHighlighterSnapOrder.Count > MaxPptHighlighterSnapHistory)
            {
                var stale = _pptHighlighterSnapOrder.Dequeue();
                _pptHighlighterSnapReverse.Remove(stale);
                foreach (var key in new List<Stroke>(_pptHighlighterSnapHistory.Keys))
                {
                    if (ReferenceEquals(_pptHighlighterSnapHistory[key], stale))
                        _pptHighlighterSnapHistory.Remove(key);
                }
            }
        }

        /// <summary>
        /// 撤销/重做即将把某个笔画放回画布时，把「原始笔画」替换成「纠正后笔画」。
        /// </summary>
        /// <param name="stroke">历史记录里即将被还原的笔画。</param>
        /// <returns>应放入画布的笔画；无需转换时返回原对象。</returns>
        /// <remarks>
        /// 撤销时历史里存的是纠正前的原始笔画，若原样还原，用户看到的是自己画的歪线，
        /// 而重做又会变回规整的高亮条——同一个操作两次得到不同结果。
        /// 这里在还原入口做一次等价替换，保证观感一致。
        /// </remarks>
        private Stroke ResolvePptHighlighterSnapForRestore(Stroke stroke)
        {
            if (stroke == null) return null;
            if (_pptHighlighterSnapHistory.Count == 0) return stroke;

            try
            {
                if (_pptHighlighterSnapHistory.TryGetValue(stroke, out var snapped)) return snapped;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[PptHighlightSnap] 还原映射查找失败: {ex.Message}", LogHelper.LogType.Trace);
            }

            return stroke;
        }

        /// <summary>
        /// 缩放或翻转画布内容时，同步变换纠正映射里的笔画，
        /// 避免历史记录里的引用与画布上的对象脱节。
        /// </summary>
        private void TransformPptHighlighterSnapMapping(Matrix matrix)
        {
            if (_pptHighlighterSnapHistory.Count == 0) return;

            try
            {
                foreach (var snapped in _pptHighlighterSnapReverse.Keys)
                    snapped.Transform(matrix, false);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[PptHighlightSnap] 同步变换纠正映射失败: {ex.Message}", LogHelper.LogType.Trace);
            }
        }

        /// <summary>
        /// 翻页 / 清空时丢弃文字行缓存与投影缓存（映射保留，撤销可能跨越清空操作）。
        /// </summary>
        private void InvalidatePptHighlighterSnapCache()
        {
            _pptTextLineRects = null;
            _pptTextLineRectsSlide = -1;
            _pptTextLineRectsHwnd = IntPtr.Zero;
            _pptLastSlideScreenRect = Rect.Empty;
        }

        #endregion

        #region PPT 荧光笔墨迹纠正：文字行获取

        /// <summary>
        /// 取得当前幻灯片的文字行（已换算到墨迹画布坐标系），带页码级缓存。
        /// </summary>
        /// <returns>可直接交给 <see cref="PptHighlighterSnapper"/> 使用的文字行列表。</returns>
        private List<PptTextLineRect> GetPptTextLineRectsForCanvas()
        {
            int slide = _currentSlideShowPosition;
            var hwnd = TryGetSlideShowHwnd();

            if (_pptTextLineRects != null
                && _pptTextLineRectsSlide == slide
                && _pptTextLineRectsHwnd == hwnd)
            {
                return _pptTextLineRects;
            }

            var slideScreenRect = GetSlideContentScreenRect(hwnd);
            var linesInPoints = ReadCurrentSlideTextLines();
            if (linesInPoints == null || linesInPoints.Count == 0)
            {
                // 空幻灯片是正常情况：缓存空结果，避免同一页反复走 COM。
                _pptTextLineRects = new List<PptTextLineRect>();
                _pptTextLineRectsSlide = slide;
                _pptTextLineRectsHwnd = hwnd;
                if (!slideScreenRect.IsEmpty) _pptLastSlideScreenRect = slideScreenRect;
                return _pptTextLineRects;
            }

            var mapped = PptHighlighterSnapper.Sanitize(
                PptHighlighterSnapper.MapToCanvas(
                    linesInPoints,
                    _pptSlideWidthPoints > 0 ? _pptSlideWidthPoints : 960,
                    _pptSlideHeightPoints > 0 ? _pptSlideHeightPoints : 540,
                    slideScreenRect,
                    ScreenPixelToInkCanvasPoint));

            _pptTextLineRects = mapped;
            _pptTextLineRectsSlide = slide;
            _pptTextLineRectsHwnd = hwnd;
            if (!slideScreenRect.IsEmpty) _pptLastSlideScreenRect = slideScreenRect;

            // 逐行几何数据只在 Trace 等级生效：实机调参（铺满系数、外扩系数）
            // 只能靠这些数字判断，光看截图猜不出来，但默认日志不该被刷屏。
            if (LogHelper.LogLevel == LogHelper.LogType.Trace && mapped.Count > 0)
            {
                var detail = new System.Text.StringBuilder();
                detail.Append($"[PptHighlightSnap] 第 {slide} 页文字行明细：");
                foreach (var line in mapped)
                {
                    detail.Append(
                        $" [{line.ShapeName} x{line.Rect.Left:F0}~{line.Rect.Right:F0} " +
                        $"y{line.Rect.Top:F0}~{line.Rect.Bottom:F0} " +
                        $"h{line.Rect.Height:F1} c{line.GlyphCenterY:F1} r{line.GlyphHeightRatio:F3}]");
                }
                LogHelper.WriteLogToFile(detail.ToString(), LogHelper.LogType.Trace);
            }

            LogHelper.WriteLogToFile(
                $"[PptHighlightSnap] 第 {slide} 页载入 {mapped.Count} 行文字区域（原始 {linesInPoints.Count} 行）",
                LogHelper.LogType.Info);

            return mapped;
        }

        /// <summary>
        /// 文字行换算所用的幻灯片尺寸（磅）。
        /// </summary>
        private double _pptSlideWidthPoints;
        private double _pptSlideHeightPoints;

        /// <summary>
        /// 通过 COM 读取当前放映幻灯片的逐行文字边界（PPT 磅值坐标系）。
        /// </summary>
        /// <returns>文字行列表；无文字或读取失败时返回空列表。</returns>
        private List<PptTextLineRect> ReadCurrentSlideTextLines()
        {
            var shapes = new List<PptTextShapeBounds>();

            try
            {
                object appObj = _pptManager?.PPTApplication ?? pptApplication;
                if (appObj == null) return new List<PptTextLineRect>();

                dynamic app = appObj;

                Microsoft.Office.Interop.PowerPoint.Presentation pres = null;
                try { pres = app.ActivePresentation as Microsoft.Office.Interop.PowerPoint.Presentation; } catch { return new List<PptTextLineRect>(); }
                if (pres == null) return new List<PptTextLineRect>();

                // 尺寸每次重新读取：不同演示文稿的版面尺寸可能不同。
                _pptSlideWidthPoints = pres.PageSetup.SlideWidth;
                _pptSlideHeightPoints = pres.PageSetup.SlideHeight;

                Microsoft.Office.Interop.PowerPoint.SlideShowWindow ssw = null;
                try { ssw = pres.SlideShowWindow; } catch { return new List<PptTextLineRect>(); }
                if (ssw == null) return new List<PptTextLineRect>();

                dynamic view = null;
                try { view = ssw.View; } catch { return new List<PptTextLineRect>(); }
                if (view == null) return new List<PptTextLineRect>();

                Microsoft.Office.Interop.PowerPoint.Slide slide = null;
                try { slide = view.Slide as Microsoft.Office.Interop.PowerPoint.Slide; } catch { return new List<PptTextLineRect>(); }
                if (slide == null) return new List<PptTextLineRect>();

                foreach (Microsoft.Office.Interop.PowerPoint.Shape shape in slide.Shapes)
                {
                    try
                    {
                        var bounds = ReadShapeTextBounds(shape);
                        if (bounds != null) shapes.Add(bounds);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"[PptHighlightSnap] 读取形状文字边界失败，跳过该形状: {ex.Message}", LogHelper.LogType.Trace);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[PptHighlightSnap] COM 读取文字行失败: {ex.Message}", LogHelper.LogType.Trace);
                return new List<PptTextLineRect>();
            }

            return PptHighlighterSnapper.FlattenTextLines(shapes);
        }

        /// <summary>
        /// 读取单个形状的文字边界：优先逐行，取不到时退化为整个形状矩形。
        /// </summary>
        /// <returns>无文字的形状返回 <c>null</c>。</returns>
        private static PptTextShapeBounds ReadShapeTextBounds(Microsoft.Office.Interop.PowerPoint.Shape shape)
        {
            if (shape == null) return null;

            // msoHasTextFrame = 1：只有带文本框的形状才可能有文字。
            Microsoft.Office.Core.MsoTriState hasText;
            try { hasText = shape.HasTextFrame; } catch { return null; }
            if (hasText != Microsoft.Office.Core.MsoTriState.msoTrue) return null;

            Microsoft.Office.Core.MsoTriState hasText2;
            try { hasText2 = shape.TextFrame2.HasText; } catch { return null; }
            if (hasText2 != Microsoft.Office.Core.MsoTriState.msoTrue) return null;

            var result = new PptTextShapeBounds
            {
                ShapeName = shape.Name,
                // 用 Shape 自身矩形而非文字区，兜底时更接近用户看到的空白文本框范围。
                ShapeRect = new Rect(shape.Left, shape.Top, Math.Max(0, shape.Width), Math.Max(0, shape.Height)),
                Lines = new List<Rect>()
            };

            try
            {
                var textRange = shape.TextFrame2.TextRange;

                // TextRange2.Lines 是带索引器的属性（Lines[int Start, int Length]），不是方法，
                // 且索引器返回的是 TextRange2 而非集合。先取全量拿到行数，再逐行取单行范围，
                // 单行 TextRange2 的 BoundLeft/Top/Width/Height 才是这一行的真实边界。
                var allLines = textRange.Lines[-1, -1];
                int count = allLines?.Lines[-1, -1]?.Count ?? allLines?.Paragraphs[-1, -1]?.Count ?? 0;

                for (int i = 1; i <= count; i++)
                {
                    Microsoft.Office.Core.TextRange2 line;
                    try { line = textRange.Lines[i, 1]; } catch { continue; }
                    if (line == null) continue;

                    // BoundWidth / BoundHeight 对空行可能返回 0，交由后续过滤器剔除。
                    var rect = new Rect(line.BoundLeft, line.BoundTop, line.BoundWidth, line.BoundHeight);
                    if (rect.IsEmpty || rect.Width <= 0 || rect.Height <= 0) continue;
                    result.Lines.Add(rect);
                }
            }
            catch (Exception)
            {
                // Lines() 在 WPS 与部分 PPT 版本里不可用，退化到 ShapeRect。
                result.Lines.Clear();
            }

            // 由字号推算字形占行盒的比例：这是「大字号条太细」的关键参数。
            if (result.Lines.Count > 0)
            {
                result.GlyphHeightRatio = EstimateShapeGlyphRatio(shape, result.Lines[0].Height);
            }

            return result;
        }

        /// <summary>
        /// 中文字形实际高度占 Em 字号的典型比例。
        /// </summary>
        /// <remarks>
        /// 实测（WPF BuildGeometry，100px 字号）：微软雅黑 0.983、黑体 0.934、
        /// 宋体 0.930、等线 0.957、楷体 0.913。取 0.93 作为中文字形的代表值，
        /// 对不出字体的场景误差在 5% 以内。
        /// </remarks>
        private const double CjkGlyphEmRatio = 0.93;

        /// <summary>
        /// 用形状的字号 + 单行行盒高度，估算字形实际高度占行盒的比例。
        /// </summary>
        /// <param name="shape">PPT 文本形状。</param>
        /// <param name="lineHeightPoints">该形状单行行盒的高度（磅）。</param>
        /// <returns>比例；无法读取字号时返回 0，交由算法层用默认估值。</returns>
        /// <remarks>
        /// 中文 PPT 的默认行距约 1.2 倍，此时比例 ≈ 0.93/1.2 ≈ 0.78；
        /// 而单倍行距时比例接近 0.93。固定写死一个系数会导致大字号、紧行距下条明显偏细，
        /// 所以这里逐形状算。
        /// </remarks>
        private static double EstimateShapeGlyphRatio(Microsoft.Office.Interop.PowerPoint.Shape shape, double lineHeightPoints)
        {
            if (lineHeightPoints <= 0) return 0;

            float fontSize = 0;
            try
            {
                // TextFrame2.TextRange.Font.Size 的单位就是磅，与 BoundHeight 一致，无需换算。
                fontSize = shape.TextFrame2.TextRange.Font.Size;
            }
            catch (Exception)
            {
                return 0;
            }

            if (fontSize <= 0 || float.IsNaN(fontSize)) return 0;

            double glyphHeight = fontSize * CjkGlyphEmRatio;
            double ratio = glyphHeight / lineHeightPoints;

            // 行距极端时比例会失真，夹紧到可解释的区间。
            if (ratio < 0.45) ratio = 0.45;
            if (ratio > 1.05) ratio = 1.05;

            return ratio;
        }

        #endregion

        #region PPT 荧光笔墨迹纠正：坐标换算

        /// <summary>
        /// 取当前放映窗口句柄；取不到时回退到 <c>screenClass</c> 窗口枚举。
        /// </summary>
        private IntPtr TryGetSlideShowHwnd()
        {
            if (_smartModeSlideShowHwnd != IntPtr.Zero) return _smartModeSlideShowHwnd;

            try
            {
                var appObj = _pptManager?.PPTApplication ?? pptApplication;
                if (appObj != null)
                {
                    dynamic app = appObj;
                    dynamic pres = app.ActivePresentation;
                    if (pres != null)
                    {
                        dynamic ssw = pres.SlideShowWindow;
                        if (ssw != null) return new IntPtr((int)ssw.HWND);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[PptHighlightSnap] 读取放映窗口句柄失败: {ex.Message}", LogHelper.LogType.Trace);
            }

            return FindActiveScreenClassWindow();
        }

        /// <summary>
        /// 计算幻灯片内容在屏幕上的实际像素矩形（剔除放映窗口的黑边）。
        /// </summary>
        /// <remarks>
        /// 放映时窗口比例与幻灯片比例通常不一致，实际画面居中并留黑边。
        /// 这里按「等比缩放取小」重建内容矩形，与 SmartMode 的区域换算保持一致，
        /// 否则文字区会整体偏移，高亮条就会落到文字旁边。
        /// </remarks>
        private Rect GetSlideContentScreenRect(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return Rect.Empty;
            if (_pptSlideWidthPoints <= 0 || _pptSlideHeightPoints <= 0) return Rect.Empty;

            try
            {
                if (!PInvoke.GetWindowRect(new HWND(hwnd), out RECT winRect)) return Rect.Empty;

                uint dpi = 96;
                try { dpi = PInvoke.GetDpiForWindow(new HWND(hwnd)); } catch { }
                if (dpi == 0) dpi = 96;
                double dpiScale = dpi / 96.0;

                double winWidthPt = (winRect.right - winRect.left) / dpiScale;
                double winHeightPt = (winRect.bottom - winRect.top) / dpiScale;
                if (winWidthPt <= 0 || winHeightPt <= 0) return Rect.Empty;

                double scale = Math.Min(winWidthPt / _pptSlideWidthPoints, winHeightPt / _pptSlideHeightPoints);
                double offsetX = (winWidthPt - _pptSlideWidthPoints * scale) / 2.0;
                double offsetY = (winHeightPt - _pptSlideHeightPoints * scale) / 2.0;

                return new Rect(
                    winRect.left + offsetX * dpiScale,
                    winRect.top + offsetY * dpiScale,
                    _pptSlideWidthPoints * scale * dpiScale,
                    _pptSlideHeightPoints * scale * dpiScale);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[PptHighlightSnap] 计算内容矩形失败: {ex.Message}", LogHelper.LogType.Trace);
                return Rect.Empty;
            }
        }

        /// <summary>
        /// 屏幕像素点 → 墨迹画布坐标。
        /// </summary>
        private Point ScreenPixelToInkCanvasPoint(Point screenPixelPoint)
        {
            if (inkCanvas == null) return screenPixelPoint;

            // Window.PointFromScreen 接收的是 DIP 屏幕坐标，先把物理像素换算回去。
            var transform = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
            double m11 = transform.M11 == 0 ? 1 : transform.M11;
            double m22 = transform.M22 == 0 ? 1 : transform.M22;
            var dipScreen = new Point(screenPixelPoint.X / m11, screenPixelPoint.Y / m22);

            return inkCanvas.PointFromScreen(dipScreen);
        }

        #endregion
    }
}
