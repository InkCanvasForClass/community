using System;
using System.Windows;
using System.Windows.Ink;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// <see cref="ICanvasInkService"/> 的宿主实现：墨迹/工具核心逻辑落在 MainWindow，
    /// 本类负责线程切换（所有方法可从任意线程调用）与参数校验。
    /// </summary>
    internal sealed class CanvasInkService : ICanvasInkService
    {
        private readonly MainWindow _mainWindow;

        public CanvasInkService(MainWindow mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        }

        private T RunOnUi<T>(Func<T> func)
            => _mainWindow.Dispatcher.CheckAccess() ? func() : _mainWindow.Dispatcher.Invoke(func);

        private void RunOnUi(Action action)
        {
            if (_mainWindow.Dispatcher.CheckAccess()) action();
            else _mainWindow.Dispatcher.Invoke(action);
        }

        public bool IsPenMode => RunOnUi(() => _mainWindow.IsPluginPenMode);

        public bool IsPageFrozen => RunOnUi(() => _mainWindow.IsPluginPageFrozen);

        public bool CanUndo => RunOnUi(() => _mainWindow.IsUndoEnabled);

        public bool CanRedo => RunOnUi(() => _mainWindow.IsRedoEnabled);

        public int CurrentWhiteboardPage
            => RunOnUi(() => _mainWindow.IsWhiteboardMode ? _mainWindow.CurrentWhiteboardIndex : 0);

        public int WhiteboardPageCount
            => RunOnUi(() => _mainWindow.IsWhiteboardMode ? _mainWindow.WhiteboardTotalCount : 0);

        public Size CanvasSize => RunOnUi(() => _mainWindow.GetPluginCanvasSize());

        public DrawingAttributes GetDefaultDrawingAttributes()
            => RunOnUi(() => _mainWindow.GetPluginDefaultDrawingAttributes());

        public StrokeCollection GetStrokes()
            => RunOnUi(() => _mainWindow.GetPluginCanvasStrokes());

        public bool TryAddStrokes(StrokeCollection strokes)
            => RunOnUi(() => _mainWindow.TryAddPluginStrokes(strokes, null));

        public bool TryAddStrokes(StrokeCollection strokes, Point center)
            => RunOnUi(() => _mainWindow.TryAddPluginStrokes(strokes, center));

        public bool TryClearStrokes()
            => RunOnUi(() => _mainWindow.TryClearPluginStrokes());

        public bool SelectTool(PluginInkTool tool)
            => RunOnUi(() => _mainWindow.SelectPluginTool(tool));

        public void Undo()
            => RunOnUi(() => _mainWindow.SymbolIconUndo_MouseUp(null, null));

        public void Redo()
            => RunOnUi(() => _mainWindow.SymbolIconRedo_MouseUp(null, null));

        public void SwitchToPreviousPage()
            => RunOnUi(() => _mainWindow.SwitchToPreviousPage());

        public void SwitchToNextPage()
            => RunOnUi(() => _mainWindow.SwitchToNextPage());

        public void AddWhiteboardPage()
            => RunOnUi(() => _mainWindow.AddWhiteboardPage());

        public void DeleteWhiteboardPage()
            => RunOnUi(() => _mainWindow.DeleteWhiteboardPage());

        public bool InsertImage()
            => RunOnUi(() => _mainWindow.InsertPluginImage());

        public void ChangeBackgroundColor()
            => RunOnUi(() => _mainWindow.ChangePluginBackgroundColor());

        public void ToggleGesture()
            => RunOnUi(() => _mainWindow.TogglePluginGesture());

        public void ExitWhiteboard()
            => RunOnUi(() => _mainWindow.ExitPluginWhiteboard());

        public void ToggleInkFreeze()
            => RunOnUi(() => _mainWindow.ToggleInkFreeze());

        public bool ExportCurrentPageAsPng(string filePath)
            => RunOnUi(() => _mainWindow.ExportCurrentPageAsPngForPlugin(filePath));

        public bool ExportStrokesAsPng(StrokeCollection strokes, string filePath)
            => RunOnUi(() => _mainWindow.ExportStrokesAsPngForPlugin(strokes, filePath));

        public bool InsertBitmap(System.Windows.Media.Imaging.BitmapSource bitmapSource)
        {
            if (bitmapSource == null) return false;
            return RunOnUi(() =>
            {
                try
                {
                    _mainWindow.InsertBitmapSourceToCanvasForPlugin(bitmapSource);
                    return true;
                }
                catch (Exception ex)
                {
                    Helpers.LogHelper.WriteLogToFile($"CanvasInkService.InsertBitmap failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
                    return false;
                }
            });
        }

        public System.Threading.Tasks.Task<bool> PasteClipboardImageAsync(Point? position = null)
        {
            bool result = RunOnUi(() =>
            {
                try
                {
                    _mainWindow.PasteClipboardImageForPlugin(position);
                    return true;
                }
                catch (Exception ex)
                {
                    Helpers.LogHelper.WriteLogToFile($"CanvasInkService.PasteClipboardImageAsync failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
                    return false;
                }
            });
            return System.Threading.Tasks.Task.FromResult(result);
        }
    }
}
