using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 画布合成服务：允许插件向宿主画布下方注入全屏背景层，并把「背景 + 墨迹」按页导出。
    /// <para>
    /// 典型用法（以 PDF 阅读器为例）：
    /// <list type="number">
    /// <item>调用 <see cref="InjectBackgroundLayer"/> 把自己的页面视图放到 InkCanvas 下方；</item>
    /// <item>调用 <see cref="ConfigurePages"/> 告知总页数、当前页与离屏渲染回调；</item>
    /// <item>自己翻页后调用 <see cref="SetCurrentPageAsync"/>，宿主会自动保存/恢复每页墨迹；</item>
    /// <item>需要成品时调用 <see cref="ExportWithInkAsync"/>。</item>
    /// </list>
    /// </para>
    /// 所有方法都可以从任意线程调用，宿主内部会切换到 UI 线程。
    /// </summary>
    public interface ICanvasCompositionService
    {
        /// <summary>
        /// 给插件注入全屏背景层。<paramref name="backgroundFactory"/> 在 UI 线程被调用一次，
        /// 返回的元素会被放到 InkCanvas 下方并铺满画布，不参与命中测试（不会抢走书写事件）。
        /// 重复调用会替换掉上一次注入的背景层。传入 <c>null</c> 等价于 <see cref="RemoveBackgroundLayer"/>。
        /// </summary>
        void InjectBackgroundLayer(Func<FrameworkElement> backgroundFactory);

        /// <summary>
        /// 移除已注入的背景层，并清空按页墨迹缓存与分页配置。
        /// </summary>
        void RemoveBackgroundLayer();

        /// <summary>当前是否已注入背景层。</summary>
        bool HasBackgroundLayer { get; }

        /// <summary>
        /// 声明背景层内真正承载页面内容的矩形（背景元素坐标系，DIP）。
        /// <para>
        /// 背景以 Uniform 等方式居中留边时必须调用：导出会只取该矩形作为 PDF 页面，
        /// 从而保持页面原始宽高比，并把墨迹按同一矩形换算，避免被拉伸成画布比例。
        /// 矩形外的墨迹（画在留边上的）导出时会被裁掉。
        /// </para>
        /// 传 <c>null</c> 表示整个背景层都是页面内容（默认行为）。
        /// </summary>
        void SetPageContentRect(Rect? contentRect);

        /// <summary>
        /// 配置分页信息。<paramref name="pageRenderer"/> 用于导出非当前页时离屏渲染背景，
        /// 参数为从 0 开始的页索引，返回已 Freeze 的位图；为 <c>null</c> 时只能导出当前页。
        /// </summary>
        void ConfigurePages(uint pageCount, uint currentPageIndex,
            Func<uint, CancellationToken, Task<BitmapSource>> pageRenderer);

        /// <summary>背景层的总页数，未配置时为 0。</summary>
        uint PageCount { get; }

        /// <summary>背景层当前页索引（从 0 开始）。</summary>
        uint CurrentPageIndex { get; }

        /// <summary>
        /// 通知宿主背景层已切换到 <paramref name="pageIndex"/>：
        /// 宿主会先把画布上的墨迹存入原页，清空画布，再恢复目标页此前的墨迹。
        /// 插件应在自己完成翻页渲染后调用。
        /// </summary>
        Task SetCurrentPageAsync(uint pageIndex, CancellationToken cancellationToken = default);

        /// <summary>
        /// 以「多可见页」方式切换背景层内容（双页等）。宿主会：
        /// <list type="number">
        /// <item>把画布墨迹按 <c>ContentRect</c> 逐个裁剪，存入对应 <c>PageIndex</c>；</item>
        /// <item>清空画布；</item>
        /// <item>把新可见页各自的墨迹恢复到画布。</item>
        /// </list>
        /// 与 <see cref="SetCurrentPageAsync"/> 的区别：一次显示多页时，墨迹必须按矩形切分到各物理页，
        /// 否则左右页笔迹会混进同一个页索引。列表里的页索引需按从 0 开始、升序给出。
        /// </summary>
        Task SetVisiblePagesAsync(IReadOnlyList<PluginVisiblePage> visiblePages,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 连续滚动：把当前画布墨迹整体平移 <paramref name="deltaY"/>（DIP），
        /// 与插件背景长条的滚动保持一致。插件应在滚动背景层后立即调用，使墨迹实时跟随。
        /// </summary>
        Task ScrollOffsetAsync(double deltaY, CancellationToken cancellationToken = default);

        /// <summary>
        /// 注册/注销画布双指手势处理器。宿主在检测到画布上的双指操作
        /// （捏合/平移，见 <see cref="IPluginCanvasGestureHandler"/>）时，会优先转发给该处理器；
        /// 处理器返回 <c>true</c> 表示插件接管该事件，宿主跳过默认的墨迹/画布变换。
        /// 传 <c>null</c> 表示注销。同一时刻只允许一个处理器。
        /// </summary>
        void SetCanvasGestureHandler(IPluginCanvasGestureHandler handler);

        /// <summary>
        /// 声明背景层内的「内容锚点」：墨迹换算（<see cref="TransformToVisual"/>）的目标元素。
        /// 当插件把页面内容放在一个会缩放/平移的容器里、而容器之外还有固定背景时，
        /// 必须把锚点指向该内容容器，宿主才能把缩放正确纳入墨迹的按页存取换算。
        /// 传 <c>null</c> 表示使用注入的背景层根节点（默认）。
        /// </summary>
        void SetCanvasContentAnchor(FrameworkElement contentLayer);

        /// <summary>
        /// 按 <paramref name="matrix"/> 变换当前画布上的全部墨迹（仅变换笔画坐标，
        /// 保留笔尖宽度），用于双指缩放/平移时让墨迹与插件背景层实时同步。
        /// 变换作用于画布坐标（与背景层 RenderTransform 同一坐标系）。
        /// </summary>
        Task TransformInkAsync(Matrix matrix, CancellationToken cancellationToken = default);

        /// <summary>
        /// 读取指定页的墨迹副本，坐标已绑定到背景层页面坐标系
        /// （原点为背景元素左上角，单位为设备无关像素，与 <see cref="FrameworkElement.ActualWidth"/> 同尺度）。
        /// 该页没有墨迹时返回空集合。
        /// </summary>
        Task<StrokeCollection> GetStrokesForPageAsync(uint pageIndex, CancellationToken cancellationToken = default);

        /// <summary>
        /// 把「背景 + 墨迹」合成后导出为 PDF：从 <paramref name="pageIndex"/> 起直到末页，
        /// 每页先合成一张图片再组装成新 PDF。返回实际写入的文件路径。
        /// </summary>
        /// <param name="outputPath">输出 PDF 路径；所在目录不存在时会被创建。</param>
        /// <param name="pageIndex">起始页索引（从 0 开始）。</param>
        Task<string> ExportWithInkAsync(string outputPath, uint pageIndex, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 一个可见页：页索引 + 该页在背景层内占据的矩形（背景元素坐标系，DIP）。
    /// 供 <see cref="ICanvasCompositionService.SetVisiblePagesAsync"/> 使用。
    /// </summary>
    public struct PluginVisiblePage
    {
        /// <summary>物理页索引（从 0 开始）。</summary>
        public uint PageIndex { get; set; }

        /// <summary>该页在背景层内占据的矩形；用于墨迹按矩形切分。</summary>
        public Rect ContentRect { get; set; }
    }
}
