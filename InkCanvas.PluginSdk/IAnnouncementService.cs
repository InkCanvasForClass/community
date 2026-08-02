using System;
using System.Collections.Generic;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 公告服务：供插件读取宿主公告中心的未读数、历史与标记已读。
    /// </summary>
    public interface IAnnouncementService
    {
        /// <summary>未读公告数。</summary>
        int GetUnreadCount();

        /// <summary>公告历史列表。</summary>
        IReadOnlyList<PluginAnnouncement> GetHistory();

        /// <summary>把指定公告标记为已读。</summary>
        void MarkAsRead(string announcementId);

        /// <summary>全部标记为已读。</summary>
        void MarkAllAsRead();

        /// <summary>清空公告历史。</summary>
        void ClearHistory();

        /// <summary>未读数变化时触发。</summary>
        event Action UnreadCountChanged;
    }

    /// <summary>
    /// 公告条目（只读描述）。
    /// </summary>
    public sealed class PluginAnnouncement
    {
        /// <summary>公告 ID。</summary>
        public string Id { get; set; } = "";

        /// <summary>标题。</summary>
        public string Title { get; set; } = "";

        /// <summary>摘要。</summary>
        public string Summary { get; set; } = "";

        /// <summary>完整内容。</summary>
        public string Content { get; set; } = "";

        /// <summary>发布时间。</summary>
        public System.DateTime CreatedAt { get; set; }

        /// <summary>是否已读。</summary>
        public bool IsRead { get; set; }

        /// <summary>是否为新增（未读且未展示过）。</summary>
        public bool IsNew { get; set; }
    }
}
