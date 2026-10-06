using Ink_Canvas.Helpers;
using Ink_Canvas.Plugins;
using System;
using System.Windows.Media;

namespace Ink_Canvas
{
    public partial class MainWindow
    {
        private bool _pluginCanvasViewportTransformActive;

        internal event EventHandler<CanvasViewportTransformEventArgs> PluginCanvasViewportTransformChanged;

        private void PublishPluginCanvasViewportTransform(Matrix delta)
        {
            if (!IsWhiteboardMode || delta.IsIdentity || !IsFinite(delta)) return;
            _pluginCanvasViewportTransformActive = true;
            RaisePluginCanvasViewportTransform(new CanvasViewportTransformEventArgs { Delta = delta });
        }

        private void CompletePluginCanvasViewportTransform()
        {
            if (!_pluginCanvasViewportTransformActive) return;
            _pluginCanvasViewportTransformActive = false;
            RaisePluginCanvasViewportTransform(new CanvasViewportTransformEventArgs
            {
                Delta = Matrix.Identity,
                IsCompleted = true
            });
        }

        private void RaisePluginCanvasViewportTransform(CanvasViewportTransformEventArgs args)
        {
            var handlers = PluginCanvasViewportTransformChanged;
            if (handlers == null) return;
            foreach (EventHandler<CanvasViewportTransformEventArgs> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(this, args);
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile(
                        $"转发插件画布变换事件失败: {ex.Message}",
                        LogHelper.LogType.Warning);
                }
            }
        }

        private static bool IsFinite(Matrix matrix)
        {
            return double.IsFinite(matrix.M11) && double.IsFinite(matrix.M12) &&
                   double.IsFinite(matrix.M21) && double.IsFinite(matrix.M22) &&
                   double.IsFinite(matrix.OffsetX) && double.IsFinite(matrix.OffsetY);
        }
    }
}
