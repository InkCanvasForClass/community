using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Ink_Canvas.Helpers;

namespace Ink_Canvas
{
    public partial class MainWindow
    {
        private int _secAgentDiagEraserMoveCount;
        private int _secAgentDiagDragMoveCount;
        private DateTime _secAgentDiagLastEraserMoveLogUtc;
        private DateTime _secAgentDiagLastDragMoveLogUtc;

        private void SecAgentDiag(string message, LogHelper.LogType type = LogHelper.LogType.Info)
        {
            var line = $"[SecAgentDiag] {message}";
            try { LogHelper.WriteLogToFile(line, type); }
            catch { Debug.WriteLine(line); }

            // Keep a dedicated diagnostic file independent of the user's normal log
            // setting, so a reproduction is still useful when ordinary logs are off.
            try
            {
                var root = App.RootPath;
                if (!string.IsNullOrWhiteSpace(root))
                {
                    Directory.CreateDirectory(root);
                    File.AppendAllText(
                        Path.Combine(root, "SecAgentDiag.log"),
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} T{Environment.CurrentManagedThreadId} {line}{Environment.NewLine}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SecAgentDiag] dedicated-log-failed {ex.GetType().Name}: {ex.Message}");
            }
        }

        private string SecAgentDiagCanvasState()
        {
            if (inkCanvas == null) return "canvas=null";
            var children = inkCanvas.Children.OfType<FrameworkElement>().ToArray();
            var sceneChildren = children.Where(IsSecAgentDiagnosticElement).Select(SecAgentDiagElement).ToArray();
            return $"mode={inkCanvas.EditingMode}; canvas={inkCanvas.Visibility}/hit={inkCanvas.IsHitTestVisible}; " +
                   $"fakeBackground={GridTransparencyFakeBackground?.Visibility}/{GridTransparencyFakeBackground?.Opacity:0.##}/" +
                   $"{GridTransparencyFakeBackground?.Background}; strokes={inkCanvas.Strokes.Count}; children={inkCanvas.Children.Count}; " +
                   $"selected={SecAgentDiagElement(currentSelectedElement)}; scenes=[{string.Join(" | ", sceneChildren)}]";
        }

        private static bool IsSecAgentDiagnosticElement(FrameworkElement element)
        {
            if (element == null) return false;
            var typeName = element.GetType().FullName ?? string.Empty;
            return typeName.EndsWith("SvgSceneElement", StringComparison.Ordinal) ||
                   typeName.EndsWith("SvgSceneGroup", StringComparison.Ordinal) ||
                   typeName.EndsWith("SvgCanvasElement", StringComparison.Ordinal);
        }

        private static string SecAgentDiagElement(FrameworkElement element)
        {
            if (element == null) return "null";
            try
            {
                var left = InkCanvas.GetLeft(element);
                var top = InkCanvas.GetTop(element);
                var actualWidth = element.ActualWidth;
                var actualHeight = element.ActualHeight;
                var width = double.IsNaN(actualWidth) || actualWidth <= 0 ? element.Width : actualWidth;
                var height = double.IsNaN(actualHeight) || actualHeight <= 0 ? element.Height : actualHeight;
                var visualContent = element.GetType().GetProperty("HasVisualContent")?.GetValue(element);
                var elementCount = element.GetType().GetProperty("ElementCount")?.GetValue(element);
                return $"{element.GetType().Name} name={element.Name} pos=({left:0.##},{top:0.##}) " +
                       $"size=({width:0.##}x{height:0.##}) actual=({actualWidth:0.##}x{actualHeight:0.##}) " +
                       $"vis={element.Visibility} hit={element.IsHitTestVisible} parent={element.Parent?.GetType().Name ?? "null"} " +
                       $"content={visualContent?.ToString() ?? "n/a"} count={elementCount?.ToString() ?? "n/a"}";
            }
            catch (Exception ex)
            {
                return $"{element.GetType().Name} name={element.Name} describe-error={ex.GetType().Name}:{ex.Message}";
            }
        }

        private void SecAgentDiagEraserMove(Point point, Rect bounds, IReadOnlyCollection<FrameworkElement> candidates)
        {
            _secAgentDiagEraserMoveCount++;
            var now = DateTime.UtcNow;
            if (_secAgentDiagEraserMoveCount != 1 &&
                _secAgentDiagEraserMoveCount % 10 != 0 &&
                (now - _secAgentDiagLastEraserMoveLogUtc).TotalMilliseconds < 250)
                return;

            _secAgentDiagLastEraserMoveLogUtc = now;
            var candidateText = candidates == null || candidates.Count == 0
                ? "none"
                : string.Join(" | ", candidates.Select(SecAgentDiagElement));
            SecAgentDiag($"ERASER_MOVE n={_secAgentDiagEraserMoveCount} point={point} bounds={bounds} " +
                         $"overlay={eraserOverlayCanvas?.IsHitTestVisible}/{eraserOverlayCanvas?.Visibility} " +
                         $"geometryActive={isUsingGeometryEraser} candidates={candidates?.Count ?? 0} " +
                         $"candidateDetails={candidateText}");
        }

        private void SecAgentDiagDragMove(FrameworkElement element, Point point)
        {
            _secAgentDiagDragMoveCount++;
            var now = DateTime.UtcNow;
            if (_secAgentDiagDragMoveCount != 1 && (now - _secAgentDiagLastDragMoveLogUtc).TotalMilliseconds < 250)
                return;
            _secAgentDiagLastDragMoveLogUtc = now;
            SecAgentDiag($"DRAG_MOVE n={_secAgentDiagDragMoveCount} point={point} dragging={isDragging} " +
                         $"captured={element?.IsMouseCaptured} mode={inkCanvas?.EditingMode} element={SecAgentDiagElement(element)}");
        }

        private void SecAgentDiagResetDragCounter()
        {
            _secAgentDiagDragMoveCount = 0;
            _secAgentDiagLastDragMoveLogUtc = DateTime.MinValue;
        }
    }
}
