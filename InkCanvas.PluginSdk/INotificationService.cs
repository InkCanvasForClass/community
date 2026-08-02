namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 通知服务，供插件发送应用内通知。
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        /// 发送一条通知消息。
        /// </summary>
        /// <param name="title">通知标题</param>
        /// <param name="message">通知内容</param>
        /// <param name="level">通知级别</param>
        void Show(string title, string message, NotificationLevel level = NotificationLevel.Info);

        /// <summary>
        /// 发送一条带点击回调的通知。
        /// 灵动通知会显示操作按钮，用户点击后触发 <paramref name="onClicked"/>。
        /// </summary>
        void Show(string title, string message, NotificationLevel level, System.Action onClicked);

        /// <summary>
        /// 读取通知历史（宿主通知中心保留最近 100 条）。
        /// </summary>
        /// <param name="source">按来源过滤；null 表示全部。</param>
        System.Collections.Generic.IReadOnlyList<PluginNotification> GetHistory(string source = null);

        /// <summary>
        /// 清空通知历史（按来源过滤；null 清空全部）。
        /// </summary>
        void ClearHistory(string source = null);

        /// <summary>
        /// 发送一条 Windows 系统通知中心 toast（Win7 自动降级为托盘气球通知）。
        /// </summary>
        void ShowWindowsToast(string title, string message);
    }

    /// <summary>
    /// 通知级别。
    /// </summary>
    public enum NotificationLevel
    {
        Info,
        Warning,
        Error,
        Success
    }

    /// <summary>
    /// 通知历史条目（只读描述）。
    /// </summary>
    public sealed class PluginNotification
    {
        /// <summary>通知标题。</summary>
        public string Title { get; set; } = "";

        /// <summary>通知内容。</summary>
        public string Summary { get; set; } = "";

        /// <summary>通知来源。</summary>
        public string Source { get; set; } = "";

        /// <summary>通知图标名（如 "Info"/"Warning"）。</summary>
        public string Icon { get; set; } = "";

        /// <summary>通知级别字符串（"Low"/"Normal"/"High"/"Critical"）。</summary>
        public string Level { get; set; } = "";

        /// <summary>创建时间。</summary>
        public System.DateTime CreatedAt { get; set; }
    }
}
