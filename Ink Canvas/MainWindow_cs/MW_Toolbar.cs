using System;
using Ink_Canvas.Controls;
using Ink_Canvas.Controls.Toolbar;
using Ink_Canvas.Helpers;
using System.Collections.Generic;
using System.Windows.Controls;

namespace Ink_Canvas
{
    public partial class MainWindow
    {
        internal ToolbarImageButton SymbolIconDelete { get; private set; }
        internal ToolbarImageButton Eraser_Icon { get; private set; }
        internal ToolbarImageButton EraserByStrokes_Icon { get; private set; }
        internal ToolbarImageButton SymbolIconSelect { get; private set; }
        internal ToolbarImageButton ShapeDrawFloatingBarBtn { get; private set; }
        internal ToolbarImageButton SymbolIconUndo { get; private set; }
        internal ToolbarImageButton SymbolIconRedo { get; private set; }
        internal ToolbarImageButton CursorWithDelFloatingBarBtn { get; private set; }
        internal ToolbarImageButton WhiteboardFloatingBarBtn { get; private set; }
        internal ToolbarImageButton ToolsFloatingBarBtn { get; private set; }
        internal ToolbarImageButton Fold_Icon { get; private set; }

        internal void AttachCursorIconView(ToolbarImageButton btn) => Cursor_Icon = btn;
        internal void AttachPenIconView(ToolbarImageButton btn) => Pen_Icon = btn;
        internal void AttachSymbolIconDelete(ToolbarImageButton btn) => SymbolIconDelete = btn;
        internal void AttachEraserIcon(ToolbarImageButton btn) => Eraser_Icon = btn;
        internal void AttachEraserByStrokesIcon(ToolbarImageButton btn) => EraserByStrokes_Icon = btn;
        internal void AttachSymbolIconSelect(ToolbarImageButton btn) => SymbolIconSelect = btn;
        internal void AttachShapeDrawBtn(ToolbarImageButton btn) => ShapeDrawFloatingBarBtn = btn;
        internal void AttachSymbolIconUndo(ToolbarImageButton btn) => SymbolIconUndo = btn;
        internal void AttachSymbolIconRedo(ToolbarImageButton btn) => SymbolIconRedo = btn;
        internal void AttachCursorWithDelBtn(ToolbarImageButton btn) => CursorWithDelFloatingBarBtn = btn;
        internal void AttachWhiteboardBtn(ToolbarImageButton btn) => WhiteboardFloatingBarBtn = btn;
        internal void AttachToolsBtn(ToolbarImageButton btn)
        {
            ToolsFloatingBarBtn = btn;
            BorderTools.PlacementTarget = btn;
        }
        internal void AttachFoldIcon(ToolbarImageButton btn) => Fold_Icon = btn;

        internal void InitializeToolbarPlugins()
        {
            LogHelper.WriteLogToFile("MW_Toolbar: InitializeToolbarPlugins 开始", LogHelper.LogType.Info);
            try
            {
                ToolbarHost = new ToolbarHost(this);
                var slots = new Dictionary<ToolbarSlot, Panel>
                {
                    { ToolbarSlot.FloatingBarMain, StackPanelFloatingBar },
                    { ToolbarSlot.FloatingBarCanvasControls, StackPanelCanvasControls },
                    { ToolbarSlot.FloatingBarEnd, StackPanelFloatingBarEnd },
                    { ToolbarSlot.BlackboardLeft, BlackboardLeftSide },
                    { ToolbarSlot.BlackboardRight, BlackboardRightSide }
                };
                ToolbarRegistry.Populate(ToolbarHost, slots, Settings?.Toolbar);
                LogHelper.WriteLogToFile("MW_Toolbar: InitializeToolbarPlugins 完成", LogHelper.LogType.Info);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"MW_Toolbar: InitializeToolbarPlugins 异常: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}", LogHelper.LogType.Error);
            }
        }

        internal void RebuildToolbar()
        {
            LogHelper.WriteLogToFile("MW_Toolbar: RebuildToolbar 开始", LogHelper.LogType.Info);
            try
            {
                ToolbarRegistry.ClearInjected(StackPanelFloatingBar);
                ToolbarRegistry.ClearInjected(StackPanelCanvasControls);
                ToolbarRegistry.ClearInjected(StackPanelFloatingBarEnd);
                ToolbarRegistry.ClearInjected(BlackboardLeftSide);
                ToolbarRegistry.ClearInjected(BlackboardRightSide);
                InitializeToolbarPlugins();
                LogHelper.WriteLogToFile("MW_Toolbar: RebuildToolbar 完成", LogHelper.LogType.Info);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"MW_Toolbar: RebuildToolbar 异常: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}", LogHelper.LogType.Error);
            }
        }
    }
}
