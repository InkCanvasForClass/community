using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 宿主 API 入口。每个插件在 Initialize 阶段获得自己的宿主代理（<c>PluginHostProxy</c>）：
    /// 日志写入该插件独立的日志目录，其余调用转发到宿主 <c>PluginManager</c>。
    /// 所有注册动作（服务、工具栏项、IPC 处理器等）必须在 Initialize 阶段完成。
    /// </summary>
    public interface IPluginHost
    {
        /// <summary>
        /// 写入普通日志。仅写入当前插件独立的日志文件（<c>PluginLogs/&lt;plugin-id&gt;/</c>），
        /// 不落入宿主日志与主程序日志。
        /// </summary>
        /// <param name="message">日志消息。</param>
        void Log(string message);

        /// <summary>
        /// 写入错误日志，可附带异常。仅写入当前插件独立的日志文件，
        /// 不落入宿主日志与主程序日志。
        /// </summary>
        /// <param name="message">错误描述。</param>
        /// <param name="ex">关联异常；可为 null。</param>
        void LogError(string message, Exception ex = null);

        /// <summary>
        /// 依赖注入服务集合。插件可在 Initialize 阶段向此集合注册自己的服务。
        /// </summary>
        IServiceCollection Services { get; }

        /// <summary>
        /// 依赖注入服务提供者。在所有插件 Initialize 完成后可用。
        /// </summary>
        IServiceProvider ServiceProvider { get; }

        /// <summary>
        /// 从 DI 容器获取服务（兼容旧接口）。优先从 DI 容器解析，其次回退到旧注册字典。
        /// </summary>
        /// <typeparam name="T">服务类型。</typeparam>
        /// <returns>已注册的服务实例；未注册时返回 null。</returns>
        T GetService<T>() where T : class;

        /// <summary>
        /// 向 DI 容器注册服务（兼容旧接口，仅在 Initialize 阶段有效）。
        /// </summary>
        /// <typeparam name="T">服务类型。</typeparam>
        /// <param name="service">要注册的服务实例。</param>
        void RegisterService<T>(T service) where T : class;

        /// <summary>
        /// 向浮动工具栏注册插件组件。
        /// </summary>
        /// <param name="itemInfo">要注册的工具栏组件信息。</param>
        void RegisterToolbarItem(PluginToolbarItemInfo itemInfo);

        /// <summary>
        /// 向白板工具栏注册插件组件。行为与 <see cref="RegisterToolbarItem"/> 相同，仅目标工具栏不同。
        /// </summary>
        /// <param name="itemInfo">要注册的工具栏组件信息。</param>
        void RegisterBoardToolbarItem(PluginToolbarItemInfo itemInfo);

        /// <summary>
        /// 注册一个 IPC 方法处理函数，由插件调用。
        /// 同一 <paramref name="method"/> 可注册多个处理函数，调用时第一个不抛异常的处理函数胜出。
        /// <paramref name="method"/> 为空字符串时宿主抛出 <see cref="ArgumentException"/>，
        /// <paramref name="handler"/> 为 null 时抛出 <see cref="ArgumentNullException"/>。
        /// </summary>
        /// <param name="method">方法名，不能为空。</param>
        /// <param name="handler">处理函数，接收 JSON 参数，返回任意可序列化对象（可为 null）。</param>
        void RegisterIpcHandler(string method, Func<System.Text.Json.JsonElement?, object> handler);

        /// <summary>
        /// 获取当前的 IPC 总线实例。调用 <see cref="RegisterIpcHandler"/> 或宿主启动 IPC 后可用，
        /// 之前为 null。仅在 Initialize 之后使用。
        /// </summary>
        IPluginIpcBus Ipc { get; }

        /// <summary>
        /// 根据文件路径评估即将安装的插件包的安全等级。
        /// <para>实现可参考 <see cref="PluginSecurityCheck"/>。</para>
        /// </summary>
        /// <param name="packagePath">安装包（.icpx）文件路径。</param>
        /// <param name="expectedSha256">期望的 SHA256 校验值；包尚未提取时可传 null。</param>
        /// <param name="declaredPluginId">插件声明 ID，用于与官方市场索引比对。</param>
        /// <returns>包含信任级别、权限声明与提示原因的安全评估结果。</returns>
        SecurityVerdict EvaluateTrust(string packagePath, string expectedSha256, string declaredPluginId);
    }

    /// <summary>
    /// IPC 总线抽象。SDK 暴露接口，实现在主项目中。
    /// </summary>
    public interface IPluginIpcBus
    {
        /// <summary>
        /// 启动命名管道服务端，循环接收客户端连接。
        /// </summary>
        void Start();

        /// <summary>
        /// 注册一个方法处理函数。同一方法可注册多个，调用时第一个不抛异常的处理函数胜出。
        /// </summary>
        /// <param name="method">方法名，不能为空。</param>
        /// <param name="handler">处理函数。</param>
        void RegisterHandler(string method, Func<System.Text.Json.JsonElement?, object> handler);

        /// <summary>
        /// 主动调用对端服务，<paramref name="args"/> 为任意 JSON 结构。
        /// 失败时抛出 <see cref="InvalidOperationException"/>，超时抛出 <see cref="TimeoutException"/>。
        /// </summary>
        /// <param name="method">要调用的方法名。</param>
        /// <param name="args">调用参数（任意 JSON 结构）。</param>
        /// <param name="timeout">超时时间；默认 5 秒，必须为正。</param>
        /// <returns>对端返回的 JSON 结果；无结果时为 null。</returns>
        System.Threading.Tasks.Task<object> InvokeAsync(string method, System.Text.Json.JsonElement? args, System.TimeSpan? timeout = null);

        /// <summary>
        /// 收到任何消息时触发。
        /// </summary>
        event System.EventHandler<IpcMessage> MessageReceived;
    }

    /// <summary>
    /// 插件工具栏项信息，用于向主程序注册工具栏组件。
    /// </summary>
    public class PluginToolbarItemInfo
    {
        /// <summary>组件唯一标识（在目标工具栏内必须唯一，建议用反域名风格如 "com.example.tool"）。</summary>
        public string Id { get; set; }

        /// <summary>组件在工具栏上显示的名称。</summary>
        public string DisplayName { get; set; }

        /// <summary>组件的描述文本，用于组件库与设置界面。</summary>
        public string Description { get; set; }

        /// <summary>组件图标（SVG Path 几何数据字符串）。为空时使用默认图标。</summary>
        public string IconGeometry { get; set; }

        /// <summary>创建组件视图（FrameworkElement）的工厂。返回 null 时该组件不显示。</summary>
        public Func<FrameworkElement> ViewFactory { get; set; }

        /// <summary>工具栏横竖排切换时回调，用于让视图自适应方向。</summary>
        public Action<FrameworkElement, Orientation> ApplyOrientation { get; set; }

        /// <summary>把持久化的组件设置字典应用到视图。宿主在构建视图后调用。</summary>
        public Action<FrameworkElement, Dictionary<string, object>> ApplySettings { get; set; }

        /// <summary>声明式自定义设置列表，宿主据此在设置界面生成设置面板（ComboBox/Slider/Toggle）。</summary>
        public List<PluginToolbarSettingInfo> CustomSettings { get; set; } = new List<PluginToolbarSettingInfo>();

        /// <summary>
        /// 弹窗内容工厂。若提供此属性，点击按钮时将自动打开包含此内容的弹窗菜单。
        /// 返回的 FrameworkElement 将作为 Popup 的 Child 显示。
        /// </summary>
        public Func<FrameworkElement> PopupContentFactory { get; set; }
    }

    /// <summary>
    /// 插件工具栏项的自定义设置描述。
    /// </summary>
    public class PluginToolbarSettingInfo
    {
        /// <summary>设置项键名，用于持久化与 <see cref="PluginToolbarItemInfo.ApplySettings"/> 回调。</summary>
        public string Key { get; set; }

        /// <summary>设置项在设置界面显示的名称。</summary>
        public string DisplayName { get; set; }

        /// <summary>设置项的说明文本。</summary>
        public string Description { get; set; }

        /// <summary>设置项类型（ComboBox/Slider/Toggle），决定宿主生成的控件。</summary>
        public PluginToolbarSettingType Type { get; set; }

        /// <summary>ComboBox 选项的显示文本。未提供 <see cref="OptionValues"/> 时同时用作保存值。</summary>
        public List<string> Options { get; set; } = new List<string>();

        /// <summary>
        /// ComboBox 选项的保存值。若数量与 Options 一致，则 Options 用作显示文本、OptionValues 用作保存值；
        /// 否则 Options 同时用作显示文本和保存值。
        /// </summary>
        public List<string> OptionValues { get; set; } = new List<string>();

        /// <summary>设置项默认值（字符串形式）。</summary>
        public string DefaultValue { get; set; }

        /// <summary>
        /// Slider 类型的最小值。默认 0。仅对 <see cref="PluginToolbarSettingType.Slider"/> 生效。
        /// </summary>
        public double? MinValue { get; set; }

        /// <summary>
        /// Slider 类型的最大值。默认 100。仅对 <see cref="PluginToolbarSettingType.Slider"/> 生效。
        /// </summary>
        public double? MaxValue { get; set; }

        /// <summary>
        /// Slider 类型的步长。设置后滑块吸附到该步长（含鼠标拖动/键盘/点击）。默认 1。
        /// 仅对 <see cref="PluginToolbarSettingType.Slider"/> 生效。
        /// </summary>
        public double? StepSize { get; set; }
    }

    /// <summary>
    /// 插件工具栏设置项类型。
    /// </summary>
    public enum PluginToolbarSettingType
    {
        /// <summary>下拉选择框。</summary>
        ComboBox,

        /// <summary>滑动条，配合 <see cref="PluginToolbarSettingInfo.MinValue"/> /
        /// <see cref="PluginToolbarSettingInfo.MaxValue"/> /
        /// <see cref="PluginToolbarSettingInfo.StepSize"/> 使用。</summary>
        Slider,

        /// <summary>开关。</summary>
        Toggle
    }

    /// <summary>
    /// IPC 消息结构（JSON 透明传输）。宿主与插件共用。
    /// </summary>
    public class IpcMessage
    {
        /// <summary>消息标识（用于请求-响应关联）。</summary>
        public string Id { get; set; } = "";

        /// <summary>方法名。</summary>
        public string Method { get; set; } = "";

        /// <summary>调用参数（任意 JSON 结构）。</summary>
        public System.Text.Json.JsonElement? Params { get; set; }

        /// <summary>调用结果（任意 JSON 结构）。</summary>
        public System.Text.Json.JsonElement? Result { get; set; }

        /// <summary>错误信息；非空表示调用失败。</summary>
        public IpcError Error { get; set; }

        /// <summary>消息来源标识，当前实现中宿主发出的消息为 "host"。</summary>
        public string From { get; set; } = "";

        /// <summary>是否携带错误信息（<see cref="Error"/> 非空）。</summary>
        public bool IsError => Error != null;
    }

    /// <summary>
    /// IPC 调用错误描述。
    /// </summary>
    public class IpcError
    {
        /// <summary>错误码。</summary>
        public int Code { get; set; }

        /// <summary>错误描述文本。</summary>
        public string Message { get; set; } = "";
    }

    /// <summary>
    /// 插件来源信任度。
    /// </summary>
    public enum PluginTrustLevel
    {
        /// <summary>未知来源（本地包/第三方镜像/SHA256 校验失败），建议安装前向用户明确确认。</summary>
        Unknown = 0,

        /// <summary>市场索引中存在但未提供 SHA256 校验值，无法核对文件完整性。</summary>
        Known = 1,

        /// <summary>官方插件市场索引中的条目且 SHA256 校验通过。</summary>
        Trusted = 2
    }

    /// <summary>
    /// 评估结果，用于安装前的安全提示。
    /// </summary>
    public class SecurityVerdict
    {
        /// <summary>被评估安装包的文件路径。</summary>
        public string PackagePath { get; set; } = "";

        /// <summary>插件 ID（调用方声明值，或从包内 manifest 解析）。</summary>
        public string PluginId { get; set; } = "";

        /// <summary>评估得到的信任级别。</summary>
        public PluginTrustLevel TrustLevel { get; set; } = PluginTrustLevel.Unknown;

        /// <summary>安装包实际计算出的 SHA256 校验值（十六进制小写）。</summary>
        public string PackageSha256 { get; set; } = "";

        /// <summary>是否能在官方插件市场索引中找到该插件。</summary>
        public bool IsOnMarket { get; set; }

        /// <summary>插件声明的权限列表（从包内 manifest 解析），用于安装前提示。</summary>
        public List<string> Permissions { get; } = new();

        /// <summary>评估结论的说明/警示原因列表，用于安装前提示。</summary>
        public List<string> Reasons { get; } = new();

        /// <summary>评估时间（UTC）。</summary>
        public System.DateTime DetectedAt { get; set; } = System.DateTime.UtcNow;
    }
}
