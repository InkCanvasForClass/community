using Ink_Canvas.Plugins;
using System;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Ink_Canvas.Plugins
{
    public class InkCanvasService : IInkCanvasService
    {
        private readonly MainWindow _mainWindow;

        public InkCanvasService(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
        }

        public void OpenWhiteboard()
        {
            if (_mainWindow != null)
            {
                _mainWindow.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        _mainWindow.UnFoldFloatingBar_MouseUp(null, null);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error opening whiteboard: {ex.Message}");
                    }
                });
            }
        }

        public void CloseWhiteboard()
        {
            if (_mainWindow != null)
            {
                _mainWindow.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        _mainWindow.FoldFloatingBar_MouseUp(null, null);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error closing whiteboard: {ex.Message}");
                    }
                });
            }
        }

        public async Task OpenWhiteboardAsync(int delayMilliseconds = 0)
        {
            if (delayMilliseconds > 0)
            {
                await Task.Delay(delayMilliseconds);
            }
            OpenWhiteboard();
        }
    }
}
