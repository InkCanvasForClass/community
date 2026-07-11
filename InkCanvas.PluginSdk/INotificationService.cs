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
        /// </summary>
        void Show(string title, string message, NotificationLevel level, System.Action onClicked);
    }

    public enum NotificationLevel
    {
        Info,
        Warning,
        Error,
        Success
    }
}
