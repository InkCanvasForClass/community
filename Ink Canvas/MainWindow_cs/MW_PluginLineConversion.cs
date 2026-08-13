using Ink_Canvas.Helpers;
using Ink_Canvas.Plugins;
using System;
using System.Collections.Generic;
using System.Windows.Ink;

namespace Ink_Canvas
{
    public partial class MainWindow
    {
        private const int PluginLineCandidateLimit = 64;
        private readonly Dictionary<string, PluginLineCandidate> _pluginLineCandidates =
            new Dictionary<string, PluginLineCandidate>(StringComparer.Ordinal);
        private readonly Queue<string> _pluginLineCandidateOrder = new Queue<string>();

        internal event EventHandler<CanvasLineFinalizedEventArgs> PluginCanvasLineFinalized;

        private sealed class PluginLineCandidate
        {
            internal string PageId { get; set; }
            internal Stroke Stroke { get; set; }
        }

        private void PublishPluginCanvasLineCandidate(Stroke stroke, CanvasLineSource source)
        {
            if (!IsWhiteboardMode || stroke?.StylusPoints == null || stroke.StylusPoints.Count < 2 ||
                !inkCanvas.Strokes.Contains(stroke))
                return;

            var page = GetCurrentPluginWhiteboardPage();
            if (page == null || string.IsNullOrWhiteSpace(page.Id)) return;

            var token = Guid.NewGuid().ToString("N");
            _pluginLineCandidates[token] = new PluginLineCandidate
            {
                PageId = page.Id,
                Stroke = stroke
            };
            _pluginLineCandidateOrder.Enqueue(token);
            while (_pluginLineCandidateOrder.Count > PluginLineCandidateLimit)
                _pluginLineCandidates.Remove(_pluginLineCandidateOrder.Dequeue());

            var args = new CanvasLineFinalizedEventArgs
            {
                CandidateToken = token,
                PageId = page.Id,
                Start = stroke.StylusPoints[0].ToPoint(),
                End = stroke.StylusPoints[stroke.StylusPoints.Count - 1].ToPoint(),
                Source = source
            };
            RaisePluginCanvasLineFinalized(args);
        }

        private void RaisePluginCanvasLineFinalized(CanvasLineFinalizedEventArgs args)
        {
            var handlers = PluginCanvasLineFinalized;
            if (handlers == null) return;

            foreach (EventHandler<CanvasLineFinalizedEventArgs> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(this, args);
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile(
                        $"转发插件最终线段事件失败: {ex.Message}",
                        LogHelper.LogType.Warning);
                }
            }
        }

        internal bool TryConvertPluginCanvasLine(
            string pluginId,
            string candidateToken,
            string beforeState,
            string afterState)
        {
            if (string.IsNullOrWhiteSpace(pluginId))
                throw new ArgumentException("插件 ID 不能为空。", nameof(pluginId));
            if (string.IsNullOrWhiteSpace(candidateToken)) return false;
            if (beforeState == null) throw new ArgumentNullException(nameof(beforeState));
            if (afterState == null) throw new ArgumentNullException(nameof(afterState));
            if (string.Equals(beforeState, afterState, StringComparison.Ordinal)) return false;

            var converted = false;
            RunOnUiThread(() =>
            {
                if (!_pluginLineCandidates.Remove(candidateToken, out var candidate)) return;
                var currentPage = GetCurrentPluginWhiteboardPage();
                if (!IsWhiteboardMode || currentPage == null ||
                    !string.Equals(currentPage.Id, candidate.PageId, StringComparison.Ordinal) ||
                    candidate.Stroke == null || !inkCanvas.Strokes.Contains(candidate.Stroke) ||
                    !_pluginUndoStateHandlers.ContainsKey(pluginId) ||
                    TryBlockFrozenPageMutation("转换结构化线段"))
                    return;

                var previousCommitType = _currentCommitType;
                _currentCommitType = CommitReason.CodeInput;
                try
                {
                    inkCanvas.Strokes.Remove(candidate.Stroke);
                    timeMachine.CommitPluginInkConversionHistory(
                        pluginId,
                        beforeState,
                        afterState,
                        new StrokeCollection { candidate.Stroke });
                    MarkCurrentPageInkChanged();
                    converted = true;
                }
                catch (Exception ex)
                {
                    if (!inkCanvas.Strokes.Contains(candidate.Stroke))
                        inkCanvas.Strokes.Add(candidate.Stroke);
                    LogHelper.WriteLogToFile(
                        $"提交插件线段转换失败: {ex}",
                        LogHelper.LogType.Error);
                }
                finally
                {
                    _currentCommitType = previousCommitType;
                }
            });
            return converted;
        }
    }
}
