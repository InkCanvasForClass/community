using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Windows.Foundation;
using Windows = global::Windows;

namespace Ink_Canvas.Ink.WetInk
{
    /// <summary>
    /// 湿→干提交：把 InkPresenter 完成的 InkStroke 转成 WPF Stroke，进 inkCanvas.Strokes
    /// 并复用 ProcessCommittedStroke 后处理；等 WPF 合成 N 帧后再从覆盖层删除湿墨
    /// （防「烘干闪变」）。干墨属性直接取自 ink.DrawingAttributes，保证与湿墨渲染一致。
    /// </summary>
    internal sealed class WetInkCommitSink
    {
        private const int DryCompositeFenceFrames = 5;

        private readonly Dispatcher _dispatcher;
        private readonly WetInkPresenterBridge _bridge;
        private readonly Action<System.Windows.Ink.Stroke> _commitToDryLayer;

        /// <summary>inkCanvas 相对覆盖窗口客户端的偏移（全屏布局通常为 0）。</summary>
        public double InkCanvasOffsetXDip { get; set; }
        public double InkCanvasOffsetYDip { get; set; }

        /// <summary>
        /// InkPresenter SetSize 用物理像素，GetInkPoints() 返回物理像素坐标；
        /// 干层 inkCanvas.Strokes 用 DIP。缩放 = 1/dpiScale。
        /// </summary>
        public double PointsToDipScale { get; set; } = 1.0;

        /// <summary>当前笔样式快照，用于把激光笔等模式标记带到干墨笔画。</summary>
        public WetInkStyleSnapshot CurrentStyle { get; set; }

        public WetInkCommitSink(
            Dispatcher dispatcher,
            WetInkPresenterBridge bridge,
            Action<System.Windows.Ink.Stroke> commitToDryLayer)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
            _commitToDryLayer = commitToDryLayer ?? throw new ArgumentNullException(nameof(commitToDryLayer));
        }

        public void OnStrokesCollected(object sender, IReadOnlyList<global::Windows.UI.Input.Inking.InkStroke> inkStrokes)
        {
            if (inkStrokes == null || inkStrokes.Count == 0) return;

            if (_dispatcher.CheckAccess())
                Process(inkStrokes);
            else
                _dispatcher.BeginInvoke(new Action(() => Process(inkStrokes)));
        }

        private void Process(IReadOnlyList<global::Windows.UI.Input.Inking.InkStroke> inkStrokes)
        {
            // 同步提交到干层（inkCanvas.Strokes + ProcessCommittedStroke），并记录原始 InkStroke
            // 用于等 WPF 合成后从覆盖层撤掉湿墨（避免 InkPresenter 自渲染干墨与 WPF 干墨双层叠加）。
            var drained = new List<global::Windows.UI.Input.Inking.InkStroke>(inkStrokes.Count);

            foreach (var ink in inkStrokes)
            {
                try
                {
                    var stroke = BuildWpfStroke(ink);
                    if (stroke == null) continue;

                    _commitToDryLayer(stroke);
                    drained.Add(ink);
                }
                catch (Exception ex)
                {
                    Helpers.LogHelper.WriteLogToFile(
                        $"WetInkCommitSink 提交失败: {ex}", Helpers.LogHelper.LogType.Error);
                }
            }

            if (drained.Count > 0)
                ScheduleRemoveAfterComposite(drained);
        }

        /// <summary>由 InkStroke（含其渲染用的 InkDrawingAttributes）构建 WPF Stroke。</summary>
        private System.Windows.Ink.Stroke BuildWpfStroke(global::Windows.UI.Input.Inking.InkStroke ink)
        {
            var inkPoints = ink.GetInkPoints();
            if (inkPoints == null || inkPoints.Count < 2)
                return null; // 轻点/无效笔画过滤

            var stylusPoints = new List<StylusPoint>(inkPoints.Count);
            foreach (var p in inkPoints)
            {
                var x = p.Position.X * PointsToDipScale - InkCanvasOffsetXDip;
                var y = p.Position.Y * PointsToDipScale - InkCanvasOffsetYDip;
                var pressure = (float)Math.Max(0, Math.Min(1, p.Pressure));
                stylusPoints.Add(new StylusPoint(x, y, pressure));
            }

            var da = ink.DrawingAttributes;
            var drawingAttributes = new DrawingAttributes
            {
                Color = ToWpfColor(da.Color),
                // InkDrawingAttributes.Size 为物理像素（湿墨渲染尺寸），干层用 DIP 需同比例缩放
                Width = Math.Max(0.1, da.Size.Width * PointsToDipScale),
                Height = Math.Max(0.1, da.Size.Height * PointsToDipScale),
                FitToCurve = da.FitToCurve,
                IgnorePressure = da.IgnorePressure,
                IsHighlighter = da.DrawAsHighlighter,
                StylusTip = da.PenTip == global::Windows.UI.Input.Inking.PenTipShape.Rectangle ? StylusTip.Rectangle : StylusTip.Ellipse
            };

            var collection = new StylusPointCollection(stylusPoints);
            var stroke = new System.Windows.Ink.Stroke(collection, drawingAttributes);
            if (CurrentStyle.IsLaser)
                stroke.AddPropertyData(Ink_Canvas.Helpers.InkFadeManager.LaserRenderModeGuid, true);
            return stroke;
        }

        private static System.Windows.Media.Color ToWpfColor(global::Windows.UI.Color c)
        {
            return System.Windows.Media.Color.FromArgb(c.A, c.R, c.G, c.B);
        }

        /// <summary>等 WPF 把干墨合成上屏后再从覆盖层撤掉湿墨（防双层叠加）。</summary>
        private void ScheduleRemoveAfterComposite(IReadOnlyList<global::Windows.UI.Input.Inking.InkStroke> strokes)
        {
            var frames = 0;
            EventHandler handler = null;
            handler = (s, e) =>
            {
                frames++;
                if (frames < DryCompositeFenceFrames) return;
                CompositionTarget.Rendering -= handler;
                Helpers.LogHelper.WriteLogToFile($"干墨已完成 {DryCompositeFenceFrames} 帧 WPF 画面合成，通知覆盖层移除湿墨: {strokes.Count} 条", Helpers.LogHelper.LogType.Trace);
                _bridge.RemoveStrokes(strokes);
            };
            CompositionTarget.Rendering += handler;
        }
    }
}
