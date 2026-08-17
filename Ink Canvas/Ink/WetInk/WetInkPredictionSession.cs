using System;
using System.Collections.Generic;

namespace Ink_Canvas.Ink.WetInk
{
    /// <summary>
    /// 单笔预测会话。真实采样到达后立即重建预测尾，预测点只暴露给湿覆盖层预览，
    /// 不进入 <see cref="WetInkDryCandidate"/>、inkCanvas.Strokes 或任何烘干提交路径。
    /// </summary>
    internal sealed class WetInkPredictionSession
    {
        private const int MaxHistoryPoints = 64;

        private readonly List<WetInkRealPoint> _history = new List<WetInkRealPoint>(MaxHistoryPoints);
        private readonly WetInkPredictionSmoother _smoother = new WetInkPredictionSmoother();
        private long _lastTimestampMicroseconds;

        /// <summary>当前是否处于落笔状态。</summary>
        public bool InStroke { get; private set; }

        /// <summary>最近一次重建的预测尾（只读预览，永不烘干）。</summary>
        public IReadOnlyList<WetInkPredictedPoint> PredictedPoints { get; private set; } =
            Array.Empty<WetInkPredictedPoint>();

        public int RealPointCount => _history.Count;

        public void BeginStroke(long timestampMicroseconds)
        {
            _history.Clear();
            _smoother.Reset();
            _lastTimestampMicroseconds = timestampMicroseconds;
            InStroke = true;
            PredictedPoints = Array.Empty<WetInkPredictedPoint>();
        }

        /// <summary>
        /// 喂入一个真实采样点并立即重建预测：真实点到达即替换上一帧预测尾根部。
        /// 坐标采用覆盖窗口客户端 DIP，时间戳微秒。
        /// </summary>
        public void OnRealSample(double xDip, double yDip, float pressure, long timestampMicroseconds)
        {
            if (!InStroke)
                BeginStroke(timestampMicroseconds);

            var lastIndex = _history.Count - 1;
            if (lastIndex >= 0)
            {
                var last = _history[lastIndex];
                if (last.TimestampMicroseconds == timestampMicroseconds
                    && Math.Abs(last.X - xDip) < 0.0001
                    && Math.Abs(last.Y - yDip) < 0.0001)
                {
                    return;
                }
            }

            _history.Add(new WetInkRealPoint(xDip, yDip, Math.Max(0, Math.Min(1, pressure)), timestampMicroseconds));
            if (_history.Count > MaxHistoryPoints)
                _history.RemoveAt(0);

            _lastTimestampMicroseconds = timestampMicroseconds;
            RebuildPrediction();
        }

        /// <summary>用最近真实点重建预测尾（供覆盖层在渲染帧时取用）。</summary>
        public void RebuildPrediction()
        {
            if (!InStroke || _history.Count < 2)
            {
                PredictedPoints = Array.Empty<WetInkPredictedPoint>();
                return;
            }

            try
            {
                PredictedPoints = WetInkTailPredictor.Build(_history, _smoother);
            }
            catch
            {
                PredictedPoints = Array.Empty<WetInkPredictedPoint>();
            }
        }

        /// <summary>抬笔/取消：清空预测与历史，永不把预测点留给下一笔。</summary>
        public void EndStroke()
        {
            InStroke = false;
            _history.Clear();
            _smoother.Reset();
            _lastTimestampMicroseconds = 0;
            PredictedPoints = Array.Empty<WetInkPredictedPoint>();
        }

        /// <summary>清空预测但保留落笔状态（例如切工具前由调用方决定是否结束整笔）。</summary>
        public void ClearPrediction()
        {
            PredictedPoints = Array.Empty<WetInkPredictedPoint>();
        }

        public IReadOnlyList<WetInkRealPoint> GetRecentRealPoints()
        {
            return _history.ToArray();
        }
    }
}
