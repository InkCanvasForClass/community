using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas.Plugins
{
    public interface IPluginHost
    {
        void Log(string message);
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
        /// 从 DI 容器获取服务（兼容旧接口）。
        /// </summary>
        T GetService<T>() where T : class;

        /// <summary>
        /// 向 DI 容器注册服务（兼容旧接口，仅在 Initialize 阶段有效）。
        /// </summary>
        void RegisterService<T>(T service) where T : class;

        /// <summary>
        /// 向工具栏注册插件组件。
        /// </summary>
        void RegisterToolbarItem(PluginToolbarItemInfo itemInfo);

        /// <summary>
        /// 注册一个 IPC 方法，由插件调用。返回前请确保未注册相同 <paramref name="method"/>。
        /// </summary>
        void RegisterIpcHandler(string method, Func<System.Text.Json.JsonElement?, object> handler);

        /// <summary>
        /// 获取当前的 IPC 服务实例（仅在 Initialize 之后可用）。
        /// </summary>
        IPluginIpcBus Ipc { get; }

        /// <summary>
        /// 根据文件路径评估即将安装的插件包的安全等级。
        /// <para>实现可参考 <see cref="PluginSecurityCheck"/>。</para>
        /// </summary>
        SecurityVerdict EvaluateTrust(string packagePath, string expectedSha256, string declaredPluginId);
    }

    /// <summary>
    /// IPC 总线抽象。SDK 暴露接口，实现在主项目中。
    /// </summary>
    public interface IPluginIpcBus
    {
        /// <summary>
        /// 启动服务器。
        /// </summary>
        void Start();

        /// <summary>
        /// 注册一个方法处理函数。
        /// </summary>
        void RegisterHandler(string method, Func<System.Text.Json.JsonElement?, object> handler);

        /// <summary>
        /// 调用对端服务。
        /// </summary>
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
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string IconGeometry { get; set; }
        public Func<FrameworkElement> ViewFactory { get; set; }
        public Action<FrameworkElement, Orientation> ApplyOrientation { get; set; }
        public Action<FrameworkElement, Dictionary<string, object>> ApplySettings { get; set; }
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
        public string Key { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public PluginToolbarSettingType Type { get; set; }
        public List<string> Options { get; set; } = new List<string>();
        /// <summary>
        /// ComboBox 选项的保存值。若数量与 Options 一致，则 Options 用作显示文本、OptionValues 用作保存值；
        /// 否则 Options 同时用作显示文本和保存值。
        /// </summary>
        public List<string> OptionValues { get; set; } = new List<string>();
        public string DefaultValue { get; set; }
    }

    public enum PluginToolbarSettingType
    {
        ComboBox,
        Slider,
        Toggle
    }

    /// <summary>
    /// IPC 消息结构（JSON 透明传输）。宿主与插件共用。
    /// </summary>
    public class IpcMessage
    {
        public string Id { get; set; } = "";
        public string Method { get; set; } = "";
        public System.Text.Json.JsonElement? Params { get; set; }
        public System.Text.Json.JsonElement? Result { get; set; }
        public IpcError Error { get; set; }
        public string From { get; set; } = "";

        public bool IsError => Error != null;
    }

    public class IpcError
    {
        public int Code { get; set; }
        public string Message { get; set; } = "";
    }

    /// <summary>
    /// 插件来源信任度。
    /// </summary>
    public enum PluginTrustLevel
    {
        Unknown = 0,
        Known = 1,
        Trusted = 2
    }

    /// <summary>
    /// 评估结果，用于安装前的安全提示。
    /// </summary>
    public class SecurityVerdict
    {
        public string PackagePath { get; set; } = "";
        public string PluginId { get; set; } = "";
        public PluginTrustLevel TrustLevel { get; set; } = PluginTrustLevel.Unknown;
        public string PackageSha256 { get; set; } = "";
        public bool IsOnMarket { get; set; }
        public List<string> Permissions { get; } = new();
        public List<string> Reasons { get; } = new();
        public System.DateTime DetectedAt { get; set; } = System.DateTime.UtcNow;
    }
}
