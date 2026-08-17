using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace Ink_Canvas.Ink.WetInk
{
    /// <summary>
    /// 预测尾实时预览窗口：只把“真实采样 + 预测外推点”画成一条轻量折线。
    ///
    /// 不变式：这里只是视觉预览，预测点永远不进入 <see cref="WetInkDryCandidate"/>、
    /// inkCanvas.Strokes 或 InkStrokeContainer，也不调用任何“把预测烘进真实墨迹”的接口。
    /// </summary>
    internal sealed class WetInkPredictionOverlayWindow : IDisposable
    {
        private readonly Window _window;
        private readonly PredictionCanvasElement _element;
        private bool _shown;
        private bool _disposed;

        public WetInkPredictionOverlayWindow()
        {
            _element = new PredictionCanvasElement();
            _window = new Window
            {
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ShowInTaskbar = false,
                ShowActivated = false,
                Topmost = true,
                Focusable = false,
                IsHitTestVisible = false,
                Content = _element,
                Left = HiddenPosition,
                Top = HiddenPosition,
                Width = 1,
                Height = 1
            };
        }

        private const int HiddenPosition = -100000;

        /// <summary>
        /// 更新预测尾。realPoints/predictedPoints 均为 inkCanvas 坐标 DIP；
        /// canvasOffsetDip 是 inkCanvas 相对主窗口客户区原点，用于转换到覆盖窗口自身坐标。
        /// </summary>
        public void UpdatePrediction(
            IReadOnlyList<WetInkRealPoint> realPoints,
            IReadOnlyList<WetInkPredictedPoint> predictedPoints,
            double canvasOffsetXDip,
            double canvasOffsetYDip,
            double screenOriginXDip,
            double screenOriginYDip,
            double widthDip,
            double heightDip,
            double penWidthDip,
            Color color,
            bool highlighter,
            bool laser)
        {
            if (_disposed) return;
            if ((realPoints == null || realPoints.Count == 0)
                && (predictedPoints == null || predictedPoints.Count == 0))
            {
                ParkOffscreen();
                return;
            }

            if (!_shown)
            {
                _window.Show();
                _shown = true;
            }

            var points = BuildRenderPoints(realPoints, predictedPoints, canvasOffsetXDip, canvasOffsetYDip);
            _element.Update(points, penWidthDip, color, highlighter, laser);
            _window.Opacity = 1;
            _window.Left = screenOriginXDip;
            _window.Top = screenOriginYDip;
            _window.Width = Math.Max(1, widthDip);
            _window.Height = Math.Max(1, heightDip);
        }

        public void Clear()
        {
            if (_disposed) return;
            _element.Update(Array.Empty<Point>(), 1, Colors.Black, false, false);
            ParkOffscreen();
        }

        public void ParkOffscreen()
        {
            if (_disposed || !_shown) return;
            _window.Left = HiddenPosition;
            _window.Top = HiddenPosition;
            _window.Opacity = 0;
        }

        private static IReadOnlyList<Point> BuildRenderPoints(
            IReadOnlyList<WetInkRealPoint> realPoints,
            IReadOnlyList<WetInkPredictedPoint> predictedPoints,
            double offsetX,
            double offsetY)
        {
            var result = new List<Point>(
                (realPoints?.Count ?? 0) + (predictedPoints?.Count ?? 0));
            if (realPoints != null)
            {
                var start = Math.Max(0, realPoints.Count - 3);
                for (int i = start; i < realPoints.Count; i++)
                    result.Add(new Point(realPoints[i].X + offsetX, realPoints[i].Y + offsetY));
            }
            if (predictedPoints != null)
            {
                foreach (var p in predictedPoints)
                    result.Add(new Point(p.X + offsetX, p.Y + offsetY));
            }
            return result;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                if (_shown)
                {
                    _window.Close();
                    _shown = false;
                }
            }
            catch
            {
                // 关闭期间窗口句柄可能已失效，忽略。
            }
        }

        private sealed class PredictionCanvasElement : FrameworkElement
        {
            private readonly VisualCollection _visuals;
            private readonly DrawingVisual _visual;
            private Point[] _points = Array.Empty<Point>();
            private double _width = 1;
            private Color _color = Colors.Black;
            private bool _highlighter;
            private bool _laser;

            public PredictionCanvasElement()
            {
                _visual = new DrawingVisual();
                _visuals = new VisualCollection(this) { _visual };
            }

            public void Update(
                IReadOnlyList<Point> points,
                double widthDip,
                Color color,
                bool highlighter,
                bool laser)
            {
                if (points == null)
                    _points = Array.Empty<Point>();
                else
                    _points = new List<Point>(points).ToArray();
                _width = Math.Max(0.5, widthDip);
                _color = color;
                _highlighter = highlighter;
                _laser = laser;
                Render();
            }

            protected override int VisualChildrenCount => _visuals.Count;

            protected override Visual GetVisualChild(int index) => _visuals[index];

            private void Render()
            {
                using (var dc = _visual.RenderOpen())
                {
                    if (_points.Length < 2)
                        return;

                    var geometry = new StreamGeometry();
                    using (var ctx = geometry.Open())
                    {
                        ctx.BeginFigure(_points[0], false, false);
                        ctx.PolyLineTo(_points, true, false);
                    }
                    geometry.Freeze();

                    var alpha = _laser ? (byte)200 : _highlighter ? (byte)120 : (byte)230;
                    var brush = new SolidColorBrush(Color.FromArgb(alpha, _color.R, _color.G, _color.B));
                    brush.Freeze();
                    var pen = new Pen(brush, _highlighter ? Math.Max(1.5, _width) : Math.Max(0.8, _width * 0.9))
                    {
                        StartLineCap = PenLineCap.Round,
                        EndLineCap = PenLineCap.Round,
                        LineJoin = PenLineJoin.Round
                    };
                    pen.Freeze();
                    dc.DrawGeometry(null, pen, geometry);
                }
            }
        }
    }
}
