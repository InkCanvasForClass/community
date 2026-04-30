using System.Threading.Tasks;

namespace Ink_Canvas.Plugins
{
    public interface IInkCanvasService
    {
        void OpenWhiteboard();
        void CloseWhiteboard();
        Task OpenWhiteboardAsync(int delayMilliseconds = 0);
    }
}
