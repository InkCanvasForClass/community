using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Ink_Canvas.Models
{
    public class AnnouncementFeed
    {
        [JsonProperty("schemaVersion")]
        public int SchemaVersion { get; set; }

        [JsonProperty("updatedAt")]
        public DateTimeOffset? UpdatedAt { get; set; }

        [JsonProperty("items")]
        public List<AnnouncementItem> Items { get; set; } = new List<AnnouncementItem>();
    }

    public class AnnouncementWebSocketMessage
    {
        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;

        [JsonProperty("data")]
        public AnnouncementItem Data { get; set; }
    }

    public class AnnouncementItem
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("software")]
        public string Software { get; set; } = string.Empty;

        [JsonProperty("software_name")]
        public string SoftwareName { get; set; } = string.Empty;

        [JsonProperty("announcement_type")]
        public string AnnouncementType { get; set; } = string.Empty;

        [JsonProperty("type")]
        public string Type { get; set; } = "notice";

        [JsonProperty("level")]
        public string Level { get; set; } = "normal";

        [JsonProperty("title")]
        public object Title { get; set; }

        [JsonProperty("summary")]
        public object Summary { get; set; }

        [JsonProperty("content")]
        public object Content { get; set; }

        [JsonProperty("icon")]
        public string Icon { get; set; } = "Info";

        [JsonProperty("actionText")]
        public object ActionText { get; set; }

        [JsonProperty("actionUrl")]
        public string ActionUrl { get; set; } = string.Empty;

        [JsonProperty("filter_type")]
        public string FilterType { get; set; } = "all";

        [JsonProperty("filter_versions")]
        public List<string> FilterVersions { get; set; } = new List<string>();

        [JsonProperty("filter_channels")]
        public List<string> FilterChannels { get; set; } = new List<string>();

        [JsonProperty("status")]
        public string Status { get; set; } = string.Empty;

        [JsonProperty("published_at")]
        public DateTimeOffset? PublishedAt { get; set; }

        [JsonProperty("expires_at")]
        public DateTimeOffset? ExpiresAt { get; set; }

        [JsonProperty("created_at")]
        public DateTimeOffset? CreatedAt { get; set; }

        [JsonProperty("updated_at")]
        public DateTimeOffset? UpdatedAt { get; set; }

        [JsonProperty("minVersion")]
        public string MinVersion { get; set; }

        [JsonProperty("maxVersion")]
        public string MaxVersion { get; set; }

        [JsonProperty("channels")]
        public List<string> Channels { get; set; } = new List<string>();

        [JsonProperty("startAt")]
        public DateTimeOffset? StartAt { get; set; }

        [JsonProperty("endAt")]
        public DateTimeOffset? EndAt { get; set; }

        [JsonProperty("displaySeconds")]
        public int DisplaySeconds { get; set; } = 5;

        [JsonProperty("forcePopup")]
        public bool ForcePopup { get; set; }

        [JsonProperty("priority")]
        public int Priority { get; set; }
    }
}
