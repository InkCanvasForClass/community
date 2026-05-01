using Ink_Canvas.Controls;
using Ink_Canvas.Controls.Toolbar;
using System.Collections.Generic;
using System.Windows.Controls;

namespace Ink_Canvas
{
    public partial class MainWindow
    {
        // 这批属性替代了 XAML 中原有的 x:Name 自动生成字段；外部代码继续按原名访问。
        // 由对应 Toolbar Item 的 AfterBuild 回填，Populate 发生在 Window_Loaded 早期。
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
        internal void AttachToolsBtn(ToolbarImageButton btn) => ToolsFloatingBarBtn = btn;
        internal void AttachFoldIcon(ToolbarImageButton btn) => Fold_Icon = btn;

        /// <summary>
        /// 在 Window_Loaded 早期调用：按 Settings.Toolbar 配置把插件化按钮填充到对应容器。
        /// 必须在 LoadSettings 之前，因为 LoadSettings 会访问 Cursor_Icon/Pen_Icon/Eraser_Icon 等。
        /// </summary>
        internal void InitializeToolbarPlugins()
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
        }
    }
}