using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 外部演示源服务：让插件把自己声明为一个「可翻页的演示文档」，
    /// 复用宿主 PPT 放映模式的整套 UI（四个翻页条、工具栏放映布局、全屏处理）。
    /// <para>
    /// 与 <see cref="IPowerPointService"/> 的区别：后者是遥控真实 PowerPoint，
    /// 本服务是把插件自己的文档接进放映模式，翻页请求会回调到插件。
    /// </para>
    /// <para>
    /// 典型用法（以 PDF 阅读器为例）：
    /// <list type="number">
    /// <item>打开文档并注入背景层后调用 <see cref="BeginAsync"/>，宿主进入放映模式并显示翻页条；</item>
    /// <item>宿主翻页条被点击时回调 <see cref="PresentationSourceDescriptor.NavigateAsync"/>；</item>
    /// <item>插件自己翻页（滚轮、弹窗按钮）后调用 <see cref="UpdatePageAsync"/> 同步页码；</item>
    /// <item>关闭文档时调用 <see cref="EndAsync"/> 退出放映模式。</item>
    /// </list>
    /// </para>
    /// 所有方法都可以从任意线程调用，宿主内部会切换到 UI 线程。
    /// </summary>
    public interface IPresentationSourceService
    {
        /// <summary>当前是否有插件正在以外部演示源身份占用放映模式。</summary>
        bool IsActive { get; }

        /// <summary>当前外部演示源的页数；未激活时为 0。</summary>
        int PageCount { get; }

        /// <summary>当前外部演示源的页码（从 1 开始）；未激活时为 0。</summary>
        int CurrentPage { get; }

        /// <summary>
        /// 进入放映模式。宿主会显示翻页条、切换工具栏为放映布局，并把翻页请求路由到
        /// <paramref name="descriptor"/> 提供的回调。
        /// <para>
        /// 真实 PowerPoint 正在放映时调用会被拒绝（返回 <c>false</c>），避免两个演示源争抢同一套 UI。
        /// 重复调用等价于先 <see cref="EndAsync"/> 再重新开始。
        /// </para>
        /// </summary>
        /// <returns>成功进入放映模式返回 <c>true</c>。</returns>
        Task<bool> BeginAsync(PresentationSourceDescriptor descriptor,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 退出放映模式，隐藏翻页条并恢复工具栏布局。
        /// 非本插件激活的演示源不受影响（内部按 <see cref="PresentationSourceDescriptor.Id"/> 校验）。
        /// </summary>
        Task EndAsync(string sourceId = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 同步页码到所有翻页条。插件自己完成翻页后调用；
        /// 由宿主翻页条触发的翻页无需调用，宿主会在回调返回后自动读取。
        /// </summary>
        /// <param name="currentPage">当前页码（从 1 开始）。</param>
        /// <param name="pageCount">总页数；传 0 表示沿用原值。</param>
        Task UpdatePageAsync(int currentPage, int pageCount = 0,
            CancellationToken cancellationToken = default);

        /// <summary>外部演示源被宿主强制结束时触发（例如真实 PPT 开始放映、宿主退出）。</summary>
        event Action<string> Ended;
    }

    /// <summary>
    /// 翻页方向。
    /// </summary>
    public enum PresentationNavigation
    {
        /// <summary>上一页。</summary>
        Previous = 0,

        /// <summary>下一页。</summary>
        Next = 1
    }

    /// <summary>
    /// 外部演示源描述。
    /// </summary>
    public class PresentationSourceDescriptor
    {
        /// <summary>
        /// 演示源唯一标识，建议用插件 Id。<see cref="IPresentationSourceService.EndAsync"/>
        /// 按此值校验，避免插件误关掉别人的放映。
        /// </summary>
        public string Id { get; set; } = "";

        /// <summary>显示名称，用于日志与提示。</summary>
        public string Name { get; set; } = "";

        /// <summary>总页数，必须大于 0，否则宿主不会显示翻页条。</summary>
        public int PageCount { get; set; }

        /// <summary>起始页码（从 1 开始）。</summary>
        public int CurrentPage { get; set; } = 1;

        /// <summary>
        /// 翻页回调。宿主翻页条被点击（含长按连续翻页）时调用，插件完成渲染后返回新的页码
        /// （从 1 开始）；返回 0 或负数表示已到边界/翻页失败，宿主不更新页码。
        /// <para>
        /// 返回值即宿主用来刷新翻页条的页码，插件无需再调用 <see cref="IPresentationSourceService.UpdatePageAsync"/>。
        /// </para>
        /// <para>回调在 UI 线程之外执行，插件内部若要触碰 WPF 元素需自行切回 Dispatcher。</para>
        /// </summary>
        public Func<PresentationNavigation, CancellationToken, Task<int>> NavigateAsync { get; set; }

        /// <summary>
        /// 是否允许点击页码按钮跳页。外部演示源通常没有缩略图与跳页对话框，
        /// 置为 <c>false</c> 时宿主会禁用页码点击与增强预览。
        /// </summary>
        public bool AllowPageNumberClick { get; set; }

        /// <summary>是否在本次放映期间显示课件“选择”工具。不涉及宿主墨迹选择。</summary>
        public bool SupportsElementSelection { get; set; }

        /// <summary>
        /// 宿主进入或离开课件选择工具时在 UI 线程回调。true 时课件区域输入穿透，
        /// 工具栏仍可操作；结束放映时一定退出选择。插件应同步自己的播放器交互模式。
        /// </summary>
        public Action<bool> ElementSelectionChanged { get; set; }

        /// <summary>由演示源接管全部批注输入；宿主只保留工具栏。</summary>
        public bool SupportsExternalAnnotation { get; set; }
        public Action<PresentationAnnotationState> AnnotationStateChanged { get; set; }
        public Action<string> AnnotationCommand { get; set; }
        /// <summary>播放器更新状态，宿主在 UI 线程读取。</summary>
        public PresentationAnnotationStatus AnnotationStatus { get; set; }
    }

    public class PresentationAnnotationState
    {
        public string Tool { get; set; } = "play";
        public string Color { get; set; } = "#e53935";
        public double Width { get; set; } = 4;
        public double Opacity { get; set; } = 1;
        public bool Pressure { get; set; }
        public string BrushKind { get; set; } = "pen";
        public double Height { get; set; } = 4;
        public string Tip { get; set; } = "ellipse";
        public int InkStyle { get; set; } = 2;
        public double VelocityMix { get; set; } = 0.45;
        public double MinDistanceScale { get; set; } = 0.5;
        public bool HighlighterOverlap { get; set; }
        public bool FadeEnabled { get; set; }
        public double FadeDelay { get; set; } = 3000;
        public double FadeSpeed { get; set; } = 1;
        public bool Smoothing { get; set; }
        public int SmoothingQuality { get; set; } = 2;
        public double EraserWidth { get; set; } = 64;
        public string EraserShape { get; set; } = "rectangle";
        public bool Frozen { get; set; }
    }

    public class PresentationAnnotationStatus
    {
        public string Tool { get; set; } = "play";
        public bool CanUndo { get; set; }
        public bool CanRedo { get; set; }
        public bool Frozen { get; set; }
    }
}
