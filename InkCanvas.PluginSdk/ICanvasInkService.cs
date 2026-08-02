using System.Windows;
using System.Windows.Ink;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 画布工具枚举。用于 <see cref="ICanvasInkService.SelectTool"/> 切换主画布工具。
    /// </summary>
    public enum PluginInkTool
    {
        /// <summary>选择（套索）。</summary>
        Select = 0,
        /// <summary>画笔。</summary>
        Pen = 1,
        /// <summary>点橡皮擦。</summary>
        Eraser = 2,
        /// <summary>笔画橡皮擦。</summary>
        StrokeEraser = 3,
        /// <summary>几何形状绘制（矩形）。</summary>
        Shape = 4,
        /// <summary>漫游（抓手，仅白板模式有效）。</summary>
        Roaming = 5,
    }

    /// <summary>
    /// 画布墨迹服务：允许插件读取、插入、清除主画布墨迹，切换工具，
    /// 控制白板分页、撤销/重做与墨迹冻结。
    /// <para>
    /// 所有方法都可以从任意线程调用，宿主内部会切换到 UI 线程。
    /// 插入/清除会写入 TimeMachine 历史（可按 Ctrl+Z 撤销），
    /// 当前页处于墨迹冻结状态时，变更类操作会被拒绝并返回 <c>false</c>。
    /// </para>
    /// </summary>
    public interface ICanvasInkService
    {
        /// <summary>当前是否处于画笔/墨迹模式。</summary>
        bool IsPenMode { get; }

        /// <summary>当前页是否已冻结（墨迹锁定，禁止变更）。</summary>
        bool IsPageFrozen { get; }

        /// <summary>是否可以撤销。</summary>
        bool CanUndo { get; }

        /// <summary>是否可以重做。</summary>
        bool CanRedo { get; }

        /// <summary>当前白板页索引（从 1 开始）；非白板模式返回 0。</summary>
        int CurrentWhiteboardPage { get; }

        /// <summary>白板总页数；非白板模式返回 0。</summary>
        int WhiteboardPageCount { get; }

        /// <summary>主画布的实际尺寸（设备无关像素），供坐标换算/居中插入。</summary>
        Size CanvasSize { get; }

        /// <summary>
        /// 当前默认笔触属性（颜色/粗细/荧光笔）。返回克隆副本，修改不影响宿主。
        /// </summary>
        DrawingAttributes GetDefaultDrawingAttributes();

        /// <summary>
        /// 当前画布上全部墨迹的克隆副本（画布坐标）。返回的集合不共享内部引用，
        /// 修改不会影响宿主画布。
        /// </summary>
        StrokeCollection GetStrokes();

        /// <summary>
        /// 把墨迹插入当前画布（保持原坐标），并写入 TimeMachine 历史（可按 Ctrl+Z 撤销）。
        /// 当前页冻结时返回 <c>false</c>；传入空集合返回 <c>false</c>。
        /// </summary>
        bool TryAddStrokes(StrokeCollection strokes);

        /// <summary>
        /// 把墨迹插入当前画布，并使墨迹包围盒中心对齐到 <paramref name="center"/>（画布坐标）。
        /// 其余行为同 <see cref="TryAddStrokes(StrokeCollection)"/>。
        /// </summary>
        bool TryAddStrokes(StrokeCollection strokes, Point center);

        /// <summary>
        /// 清空当前画布墨迹，并写入 TimeMachine 历史（可按 Ctrl+Z 撤销）。
        /// 当前页冻结时返回 <c>false</c>。
        /// </summary>
        bool TryClearStrokes();

        /// <summary>
        /// 切换画布工具。当前页冻结时，编辑类工具（笔/橡皮/选择）会被拒绝并返回 <c>false</c>。
        /// </summary>
        /// <returns>是否切换成功。</returns>
        bool SelectTool(PluginInkTool tool);

        /// <summary>撤销上一步操作。</summary>
        void Undo();

        /// <summary>重做下一步操作。</summary>
        void Redo();

        /// <summary>翻到上一白板页。</summary>
        void SwitchToPreviousPage();

        /// <summary>翻到下一白板页（已在末页时新增一页）。</summary>
        void SwitchToNextPage();

        /// <summary>新增一页白板。</summary>
        void AddWhiteboardPage();

        /// <summary>删除当前白板页（仅剩一页时无效）。</summary>
        void DeleteWhiteboardPage();

        /// <summary>
        /// 打开「从文件插入图片」流程（文件对话框 + 插入画布）。
        /// 返回是否成功触发流程；当前页冻结或不可插入时返回 false。
        /// </summary>
        bool InsertImage();

        /// <summary>更换当前画布背景色（打开颜色选择）。</summary>
        void ChangeBackgroundColor();

        /// <summary>切换双指手势（画布平移/缩放）开关。</summary>
        void ToggleGesture();

        /// <summary>退出白板模式（回到浮动栏）。</summary>
        void ExitWhiteboard();

        /// <summary>切换当前页的墨迹冻结状态。</summary>
        void ToggleInkFreeze();

        /// <summary>
        /// 把当前画布页（墨迹 + 背景色）导出为 PNG 文件。
        /// </summary>
        /// <param name="filePath">输出 PNG 路径（目录需已存在）。</param>
        /// <returns>是否导出成功。</returns>
        bool ExportCurrentPageAsPng(string filePath);

        /// <summary>
        /// 把指定墨迹集合渲染为 PNG 文件。
        /// </summary>
        /// <param name="strokes">要导出的墨迹。</param>
        /// <param name="filePath">输出 PNG 路径（目录需已存在）。</param>
        /// <returns>是否导出成功。</returns>
        bool ExportStrokesAsPng(System.Windows.Ink.StrokeCollection strokes, string filePath);

        /// <summary>
        /// 把图片插入当前画布（居中缩放、进入撤销历史、切换到选择模式）。
        /// </summary>
        /// <param name="bitmapSource">要插入的图片。</param>
        /// <returns>是否已触发插入流程。</returns>
        bool InsertBitmap(System.Windows.Media.Imaging.BitmapSource bitmapSource);

        /// <summary>
        /// 把剪贴板图片粘贴到画布（可选指定坐标）。
        /// </summary>
        /// <param name="position">插入位置（画布坐标）；null 表示居中。</param>
        /// <returns>是否已触发粘贴流程。</returns>
        System.Threading.Tasks.Task<bool> PasteClipboardImageAsync(System.Windows.Point? position = null);
    }
}
