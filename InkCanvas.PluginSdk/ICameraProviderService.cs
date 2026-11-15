using System;
using System.Collections.Generic;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 摄像头提供者服务（camera provider）：插件把自己采集的画面（网络摄像头、无线视频展台、
    /// 视频采集卡、屏幕采集等非 DirectShow 来源）交给宿主，让它在内置「视频展台」里
    /// 和普通 USB 摄像头一样可用。
    /// <para>工作方式：插件注册一个 <see cref="IPluginCameraProvider"/> 并声明若干虚拟摄像头；
    /// 宿主把这些摄像头合并进视频展台的摄像头下拉框（与 DirectShow 设备并列）。用户选中后，
    /// 宿主回调 <see cref="IPluginCameraProvider.StartCamera"/> 并传入一个
    /// <see cref="IPluginCameraFrameSink"/>，插件只需按自己的节奏把 BGR24 帧
    /// <see cref="IPluginCameraFrameSink.Submit"/> 进来；渲染、拍照、缩放/旋转/镜像、
    /// 分辨率下拉框、批注、退出清理全部由宿主负责。</para>
    /// <para>从注册到出图，插件端最少只需要实现 3 件事：声明摄像头能力、StartCamera 时启动采集、
    /// 采集线程里 <c>sink.Submit(...)</c>。不需要写任何 DirectShow / COM 代码，
    /// 也不需要在系统里注册虚拟摄像头。</para>
    /// <para>与「系统级虚拟 DirectShow 摄像头」的区别：后者对全系统可见、需要写原生 COM 滤波器并注册；
    /// 本服务只对宿主可见，纯托管、随插件安装/卸载，互不冲突。</para>
    /// </summary>
    /// <example>
    /// 最小接入示例：
    /// <code>
    /// // 1) Initialize 阶段注册（所有注册动作都必须在 Initialize 内完成）
    /// _cameraProvider = host.GetService&lt;ICameraProviderService&gt;();
    /// _cameraProvider?.RegisterProvider(new MyCameraProvider());
    ///
    /// // 2) 插件实现 IPluginCameraProvider
    /// public IReadOnlyList&lt;PluginCameraDescriptor&gt; GetCameras() =&gt; new[]
    /// {
    ///     new PluginCameraDescriptor
    ///     {
    ///         Id = "my-booth",
    ///         DisplayName = "无线视频展台（插件）",
    ///         Capabilities = new List&lt;PluginCameraCapability&gt;
    ///         {
    ///             new PluginCameraCapability { Width = 1920, Height = 1080, FrameRate = 30 },
    ///             new PluginCameraCapability { Width = 1280, Height = 720, FrameRate = 30 },
    ///         },
    ///     },
    /// };
    ///
    /// public bool StartCamera(string cameraId, IPluginCameraFrameSink sink)
    /// {
    ///     _sink = sink;                    // 保存 sink，推帧时用它
    ///     _session = StartCapture();       // 启动你自己的采集（RTSP / 解码 / 等）
    ///     return true;                     // false = 启动失败，宿主会给用户提示
    /// }
    ///
    /// public void StopCamera(string cameraId)
    /// {
    ///     _sink = null;
    ///     _session?.Stop();                // 尽快返回，不要在这里长时间阻塞
    /// }
    ///
    /// // 3) 采集线程（任意线程）推帧
    /// void OnFrame(byte[] bgr24, int w, int h)
    /// {
    ///     var sink = _sink;
    ///     if (sink == null) return;
    ///     int stride = (w * 3 + 3) &amp; ~3;
    ///     if (!sink.Submit(bgr24, w, h, stride)) { /* false=宿主已停止，可停采集 */ }
    /// }
    /// </code>
    /// </example>
    public interface ICameraProviderService
    {
        /// <summary>
        /// 注册一个摄像头提供者。必须在插件 <c>Initialize</c> 阶段调用（与工具栏/服务等注册动作一致）。
        /// <para>重复注册同一 <see cref="IPluginCameraProvider.ProviderId"/> 会覆盖旧的摄像头列表并刷新视频展台下拉框。</para>
        /// </summary>
        /// <param name="provider">摄像头提供者实现，不可为 null。</param>
        /// <exception cref="ArgumentNullException"><paramref name="provider"/> 为 null。</exception>
        /// <exception cref="ArgumentException"><see cref="IPluginCameraProvider.ProviderId"/> 为空。</exception>
        void RegisterProvider(IPluginCameraProvider provider);

        /// <summary>
        /// 注销摄像头提供者：宿主立即停止该提供者正在推流的摄像头（若正在预览），
        /// 并从视频展台摄像头下拉框中移除它声明的全部摄像头。
        /// <para>插件通常在 <c>Shutdown</c> 中调用；宿主卸载插件时也会按
        /// <see cref="IPluginCameraProvider.ProviderId"/> 自动清理，避免残留。</para>
        /// </summary>
        /// <param name="provider">要注销的提供者，不可为 null。</param>
        void UnregisterProvider(IPluginCameraProvider provider);

        /// <summary>
        /// 通知宿主「该提供者的摄像头列表已变化」（新增/删除/改名/能力变化）。
        /// 宿主会刷新视频展台的摄像头下拉框；正在预览的摄像头不受影响。
        /// 可在任意线程调用。摄像头列表未变化时无需调用。
        /// </summary>
        /// <param name="provider">摄像头列表已变化的提供者，不可为 null。</param>
        void NotifyCamerasChanged(IPluginCameraProvider provider);
    }

    /// <summary>
    /// 摄像头提供者（由插件实现）。宿主按需调用它来启停插件侧的采集。
    /// <para>线程约定：<see cref="StartCamera"/> / <see cref="StopCamera"/> /
    /// <see cref="SetCameraFormat"/> 由宿主在后台线程调用，实现里不要做长耗时阻塞操作
    /// （启动网络连接之类的异步工作请在实现内部转后台进行，先返回 true 再异步连，首帧到达前
    /// 宿主显示"正在启动摄像头…"）。<see cref="GetCameras"/> 可能在 UI 线程调用，应快速返回缓存结果。</para>
    /// <para>生命周期：同一时刻宿主最多只向一路摄像头推流；切换摄像头/分辨率、退出展台、
    /// 卸载插件时会调用 <see cref="StopCamera"/>。插件必须保证 StopCamera 后不再调用 Submit。</para>
    /// </summary>
    public interface IPluginCameraProvider
    {
        /// <summary>
        /// 提供者唯一标识。建议直接用插件 manifest 的 <c>Id</c>（如 <c>com.example.boothcam</c>），
        /// 宿主会在卸载插件时按它自动清理该提供者注册的摄像头。
        /// </summary>
        string ProviderId { get; }

        /// <summary>
        /// 返回该提供者当前声明的摄像头列表。宿主把它合并进视频展台下拉框。
        /// 返回空列表表示当前没有可用摄像头（例如设备未连接），此时下拉框不出现条目。
        /// 列表变化后请调用 <see cref="ICameraProviderService.NotifyCamerasChanged"/>。
        /// </summary>
        IReadOnlyList<PluginCameraDescriptor> GetCameras();

        /// <summary>
        /// 宿主请求开始消费指定摄像头的画面。
        /// <para>实现应启动采集，并持续通过 <paramref name="sink"/> 推帧，直到
        /// <see cref="StopCamera"/> 被调用或 <see cref="IPluginCameraFrameSink.Submit"/> 返回 false。</para>
        /// </summary>
        /// <param name="cameraId">要启动的摄像头 Id，即 <see cref="PluginCameraDescriptor.Id"/>。</param>
        /// <param name="sink">帧接收器，线程安全；插件可保存并在采集线程中反复调用。</param>
        /// <returns>true = 已开始启动；false = 启动失败（宿主会在预览界面提示用户）。
        /// 即使返回 true，也允许异步连接、稍后才有帧；连续长时间无帧时用户会看到"正在启动摄像头…"。</returns>
        bool StartCamera(string cameraId, IPluginCameraFrameSink sink);

        /// <summary>
        /// 宿主请求停止消费指定摄像头的画面。实现应停止采集、释放相关资源，并保证返回后
        /// 不再调用 <see cref="IPluginCameraFrameSink.Submit"/>。宿主在切换摄像头/分辨率、
        /// 退出视频展台、卸载插件时调用。
        /// </summary>
        /// <param name="cameraId">要停止的摄像头 Id。</param>
        void StopCamera(string cameraId);

        /// <summary>
        /// 宿主请求切换采集格式（来自视频展台的分辨率下拉框）。实现可以：
        /// 调整采集/解码输出尺寸并继续推同一路帧（推荐，无需重启推流）；
        /// 或返回 false 表示不支持运行时切换，此时宿主仍会重建推流（Stop→Start），
        /// 并以实际推送的帧尺寸为准渲染（下拉框选择只是期望值）。
        /// </summary>
        /// <param name="cameraId">摄像头 Id。</param>
        /// <param name="width">期望宽度（像素），保证是 <see cref="GetCameras"/> 中声明过的能力组合之一。</param>
        /// <param name="height">期望高度（像素）。</param>
        /// <param name="frameRate">期望帧率（FPS）。</param>
        /// <returns>是否接受该格式。</returns>
        bool SetCameraFormat(string cameraId, int width, int height, int frameRate);
    }

    /// <summary>
    /// 帧接收器：插件把采集到的画面提交给宿主，由宿主负责预览渲染与拍照。
    /// <para>实现由宿主提供，线程安全，可在任意线程调用。
    /// 提交的帧必须是 <b>BGR24、top-down</b>（第 0 行 = 图像顶部）的紧密排列像素数据。</para>
    /// </summary>
    public interface IPluginCameraFrameSink
    {
        /// <summary>
        /// 当前宿主是否仍在消费该摄像头。返回 false 表示宿主已停止（等价于紧接着会调用
        /// <see cref="IPluginCameraProvider.StopCamera"/>），插件可以据此结束采集循环。
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// 提交一帧画面。
        /// <para>像素格式固定为 BGR24（每个像素 3 字节，字节序 B、G、R），第 0 行是图像顶部（top-down）。
        /// stride 为每行字节数，通常取 <c>(width * 3 + 3) &amp; ~3</c>，也可以带额外的行对齐填充；
        /// 缓冲区长度必须不小于 <c>stride * height</c>。</para>
        /// <para>宿主会在本次调用返回前完成拷贝，因此实现可以立即复用自己的缓冲区。
        /// 宿主内部只保留最近一帧（不支持帧队列），推得太快时旧帧会被丢弃，插件不需要自己做节流。</para>
        /// </summary>
        /// <param name="bgr24Buffer">BGR24 像素数据，长度至少 <c>stride * height</c>。</param>
        /// <param name="width">帧宽度（像素），必须 &gt; 0。</param>
        /// <param name="height">帧高度（像素），必须 &gt; 0。</param>
        /// <param name="stride">每行字节数，必须 &gt;= <c>width * 3</c>。</param>
        /// <param name="timestampMs">可选的采集时间戳（毫秒），用于插件侧诊断，宿主当前不据此播放。</param>
        /// <returns>true = 已接收；false = 宿主已停止或参数非法（插件应停止推帧）。</returns>
        bool Submit(byte[] bgr24Buffer, int width, int height, int stride, long timestampMs = 0L);
    }

    /// <summary>
    /// 插件声明的虚拟摄像头描述。宿主据此在视频展台下拉框中显示名称、填充分辨率下拉框。
    /// </summary>
    public sealed class PluginCameraDescriptor
    {
        /// <summary>
        /// 摄像头 Id，在同一提供者内唯一（建议用稳定的英文短标识，如 <c>my-booth</c>）。
        /// 宿主在 StartCamera/StopCamera/SetCameraFormat 中原样回传。
        /// </summary>
        public string Id { get; set; } = "";

        /// <summary>
        /// 在视频展台摄像头下拉框中显示的名称（用户可见）。
        /// 建议带上可辨识的后缀，例如 <c>无线视频展台（插件）</c>，与普通 USB 摄像头区分。
        /// 该名称同时用于记忆"上次选中的摄像头"，应保持稳定。
        /// </summary>
        public string DisplayName { get; set; } = "";

        /// <summary>
        /// 声明的采集能力列表（宽/高/帧率），用于填充视频展台的分辨率下拉框。
        /// 可以为空：此时分辨率下拉框显示"加载中…"，预览仍按插件实际推送的帧尺寸渲染。
        /// </summary>
        public List<PluginCameraCapability> Capabilities { get; set; } = new List<PluginCameraCapability>();

        /// <summary>
        /// 是否支持类似 DirectShow 摄像头的能力项调节（亮度/对比度/饱和度/色温/增益/焦距/曝光）。
        /// 当前宿主版本只支持 DirectShow 摄像头的属性调节；插件摄像头请保持 false（默认值），
        /// 此时展台的属性滑块会自动置灰（与不支持的 USB 摄像头表现一致）。
        /// </summary>
        public bool SupportsPropertyControl { get; set; }
    }

    /// <summary>
    /// 插件摄像头声明的一种采集能力（宽 × 高 × 帧率）。
    /// </summary>
    public sealed class PluginCameraCapability
    {
        /// <summary>宽度（像素）。</summary>
        public int Width { get; set; }

        /// <summary>高度（像素）。</summary>
        public int Height { get; set; }

        /// <summary>帧率（FPS）；&lt;= 0 时宿主按 30 处理。</summary>
        public int FrameRate { get; set; }

        /// <summary>返回 <c>1920×1080@30fps</c> 形式的文本，便于日志与调试。</summary>
        public override string ToString() =>
            $"{Width}×{Height}" + (FrameRate > 0 ? $"@{FrameRate}fps" : "");
    }
}
