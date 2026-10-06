using System;

namespace Ink_Canvas.Plugins
{
    internal static class PluginCanvasResourceCleanup
    {
        internal static void Release(
            string pluginId,
            ICanvasToolService canvasToolService,
            ICanvasLayerService canvasLayerService,
            IFocusInteractionService focusInteractionService,
            IUndoService undoService,
            IWhiteboardDocumentService whiteboardDocumentService,
            Action<Exception> onError)
        {
            TryRelease(() => canvasToolService?.DeactivateTools(pluginId), onError);
            TryRelease(() => canvasLayerService?.RemoveLayers(pluginId), onError);
            TryRelease(() => focusInteractionService?.SetActive(pluginId, false), onError);
            TryRelease(() => undoService?.UnregisterStateHandler(pluginId), onError);
            TryRelease(() => whiteboardDocumentService?.UnregisterPageStateProvider(pluginId), onError);
        }

        private static void TryRelease(Action release, Action<Exception> onError)
        {
            try
            {
                release();
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex);
            }
        }
    }
}
