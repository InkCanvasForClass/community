using System;
using System.Windows.Media;

namespace Ink_Canvas.Plugins
{
    /// <summary>宿主对整张普通白板内容应用的增量视口变换。</summary>
    public sealed class CanvasViewportTransformEventArgs : EventArgs
    {
        public Matrix Delta { get; set; } = Matrix.Identity;

        public bool IsCompleted { get; set; }
    }

    public interface ICanvasViewportService
    {
        event EventHandler<CanvasViewportTransformEventArgs> TransformChanged;
    }
}
