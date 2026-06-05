using System;

namespace Ink_Canvas.Models
{
    public class NotificationProviderStatus
    {
        public string ProviderId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public bool IsRunning { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? LastUpdatedAt { get; set; }
    }

    public class AnnouncementCenterItem
    {
        public string Id { get; set; } = string.Empty;
        public NotificationMessageType Type { get; set; }
        public NotificationMessageLevel Level { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string ActionUrl { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool IsRead { get; set; }
        public bool IsUnread => !IsRead;
        public bool IsNew { get; set; }
    }
}
