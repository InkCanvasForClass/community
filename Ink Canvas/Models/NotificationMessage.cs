using Newtonsoft.Json;
using System;

namespace Ink_Canvas.Models
{
    public enum NotificationMessageType
    {
        Update,
        Urgent,
        Important,
        Reminder,
        Other
    }

    public enum NotificationMessageLevel
    {
        Low,
        Normal,
        High,
        Critical
    }

    public class NotificationMessage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public NotificationMessageType Type { get; set; } = NotificationMessageType.Other;
        public NotificationMessageLevel Level { get; set; } = NotificationMessageLevel.Normal;
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Icon { get; set; } = "Info";
        public string ActionText { get; set; } = string.Empty;
        public string ActionUrl { get; set; } = string.Empty;
        public int DisplaySeconds { get; set; } = 5;
        public bool ForcePopup { get; set; }
        public int Priority { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string Source { get; set; } = string.Empty;
        public string ProviderId { get; set; } = string.Empty;
        public string AnnouncementId { get; set; } = string.Empty;
        public string AnnouncementType { get; set; } = string.Empty;

        [JsonIgnore]
        public Action Action { get; set; }
    }
}
