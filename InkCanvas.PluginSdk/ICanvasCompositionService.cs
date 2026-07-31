using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Ink;
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
}
