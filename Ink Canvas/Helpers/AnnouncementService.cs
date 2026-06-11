using Ink_Canvas.Models;
using Ink_Canvas.Properties;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ink_Canvas.Helpers
{
    internal class AnnouncementService : INotificationProvider
    {
        private readonly Settings settings;
        private ClientWebSocket webSocket;
        private bool disposed;
        private bool isRealtimePushUnavailable;
        private static readonly object HistorySyncRoot = new object();
        private static readonly List<AnnouncementCenterItem> AnnouncementHistory = new List<AnnouncementCenterItem>();

        public static event Action UnreadCountChanged;

        public string ProviderId => "announcement";

        public static IReadOnlyList<AnnouncementCenterItem> GetAnnouncementHistory()
        {
            lock (HistorySyncRoot)
            {
                return AnnouncementHistory.Select(CloneAnnouncementCenterItem).ToList();
            }
        }

        public static void ClearAnnouncementHistory()
        {
            lock (HistorySyncRoot)
            {
                AnnouncementHistory.Clear();
            }
            UnreadCountChanged?.Invoke();
        }

        public static int GetUnreadCount(Settings settings)
        {
            lock (HistorySyncRoot)
            {
                return AnnouncementHistory.Count(x => !IsRead(settings, x.Id));
            }
        }

        public static void MarkAsRead(Settings settings, string announcementId)
        {
            if (settings?.Notification == null || string.IsNullOrWhiteSpace(announcementId)) return;

            var changed = false;
            lock (HistorySyncRoot)
            {
                if (settings.Notification.ReadAnnouncementIds == null)
                {
                    settings.Notification.ReadAnnouncementIds = new List<string>();
                }

                if (!settings.Notification.ReadAnnouncementIds.Contains(announcementId))
                {
                    settings.Notification.ReadAnnouncementIds.Add(announcementId);
                    changed = true;
                }

                foreach (var item in AnnouncementHistory.Where(x => x.Id == announcementId))
                {
                    if (!item.IsRead || item.IsNew)
                    {
                        item.IsRead = true;
                        item.IsNew = false;
                        changed = true;
                    }
                }
            }

            if (!changed) return;
            SettingsManager.SaveSettingsToFile();
            UnreadCountChanged?.Invoke();
        }

        public static void MarkAllAsRead(Settings settings)
        {
            if (settings?.Notification == null) return;

            lock (HistorySyncRoot)
            {
                if (settings.Notification.ReadAnnouncementIds == null)
                {
                    settings.Notification.ReadAnnouncementIds = new List<string>();
                }

                foreach (var item in AnnouncementHistory)
                {
                    if (!settings.Notification.ReadAnnouncementIds.Contains(item.Id))
                    {
                        settings.Notification.ReadAnnouncementIds.Add(item.Id);
                    }
                    item.IsRead = true;
                    item.IsNew = false;
                }
            }

            SettingsManager.SaveSettingsToFile();
            UnreadCountChanged?.Invoke();
        }

        private static bool IsRead(Settings settings, string announcementId)
        {
            return settings?.Notification?.ReadAnnouncementIds?.Contains(announcementId) == true;
        }

        private static AnnouncementCenterItem CloneAnnouncementCenterItem(AnnouncementCenterItem item)
        {
            return new AnnouncementCenterItem
            {
                Id = item.Id,
                Type = item.Type,
                Level = item.Level,
                Title = item.Title,
                Summary = item.Summary,
                Content = item.Content,
                ActionUrl = item.ActionUrl,
                CreatedAt = item.CreatedAt,
                IsRead = item.IsRead,
                IsNew = item.IsNew
            };
        }

        public AnnouncementService(Settings settings)
        {
            this.settings = settings;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            NotificationProviderRegistry.RegisterOrUpdate(new NotificationProviderStatus
            {
                ProviderId = ProviderId,
                DisplayName = NotificationStrings.Provider_Announcement,
                Description = NotificationStrings.Provider_AnnouncementDesc,
                IsEnabled = settings?.Notification?.IsAnnouncementEnabled == true,
                IsRunning = false,
                Status = NotificationStrings.Provider_Starting
            });

            if (settings?.Notification?.IsAnnouncementEnabled != true)
            {
                NotificationProviderRegistry.SetRunning(ProviderId, false, NotificationStrings.Provider_Disabled);
                return;
            }
            if (string.IsNullOrWhiteSpace(settings.Notification.AnnouncementSoftwareToken))
            {
                NotificationProviderRegistry.SetRunning(ProviderId, false, NotificationStrings.Provider_NoToken);
                return;
            }

            await FetchAnnouncementsAsync(cancellationToken);
            NotificationProviderRegistry.SetRunning(ProviderId, true, NotificationStrings.Provider_Running);

            if (!string.IsNullOrWhiteSpace(BuildWebSocketUrl()))
            {
                _ = Task.Run(() => ConnectWebSocketLoopAsync(cancellationToken));
            }
        }

        public Task StopAsync()
        {
            disposed = true;
            webSocket?.Abort();
            webSocket?.Dispose();
            webSocket = null;
            NotificationProviderRegistry.SetRunning(ProviderId, false, NotificationStrings.Provider_Stopped);
            return Task.CompletedTask;
        }

        public async Task FetchAnnouncementsAsync(CancellationToken cancellationToken)
        {
            var baseUrl = settings?.Notification?.AnnouncementApiBaseUrl;
            var token = settings.Notification.AnnouncementSoftwareToken;
            if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(token)) return;

            try
            {
                var requestUrl = BuildClientAnnouncementsUrl(baseUrl, token);
                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) })
                {
                    using (var response = await client.GetAsync(requestUrl, cancellationToken))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            LogHelper.WriteLogToFile($"AnnouncementService 拉取公告失败: HTTP {(int)response.StatusCode} {response.ReasonPhrase}", LogHelper.LogType.Warning);
                            return;
                        }

                        var json = await response.Content.ReadAsStringAsync();
                        var items = ParseAnnouncementItems(json);
                        foreach (var item in items)
                        {
                            AddAnnouncementHistory(ToNotificationMessage(item), item.Id, false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"AnnouncementService 拉取公告失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        private async Task ConnectWebSocketLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && !disposed)
            {
                var wsUrl = BuildWebSocketUrl();
                if (string.IsNullOrWhiteSpace(wsUrl)) return;
                var connected = false;

                foreach (var candidateUrl in BuildWebSocketUrlCandidates(wsUrl))
                {
                    if (cancellationToken.IsCancellationRequested || disposed) return;
                    try
                    {
                        using (webSocket = new ClientWebSocket())
                        {
                            await webSocket.ConnectAsync(new Uri(candidateUrl), cancellationToken);
                            connected = true;
                            isRealtimePushUnavailable = false;
                            NotificationProviderRegistry.SetRunning(ProviderId, true, NotificationStrings.Provider_Running);
                            await ReceiveWebSocketMessagesAsync(webSocket, cancellationToken);
                        }
                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    catch (WebSocketException ex) when (IsWebSocketServerError(ex))
                    {
                        if (!isRealtimePushUnavailable)
                        {
                            LogHelper.WriteLogToFile($"AnnouncementService WebSocket 服务端暂不可用，将继续重连并保留 HTTP 公告拉取通道: {ex.Message}", LogHelper.LogType.Trace);
                            isRealtimePushUnavailable = true;
                        }
                        NotificationProviderRegistry.SetRunning(ProviderId, true, NotificationStrings.Provider_HttpOnly);
                    }
                    catch (Exception ex)
                    {
                        if (!isRealtimePushUnavailable)
                        {
                            LogHelper.WriteLogToFile($"AnnouncementService WebSocket 连接失败: {ex.Message}", LogHelper.LogType.Trace);
                        }
                        NotificationProviderRegistry.SetRunning(ProviderId, false, NotificationStrings.Provider_Reconnecting);
                    }
                }

                if (connected) continue;

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        private static bool IsWebSocketServerError(WebSocketException ex)
        {
            return ex.Message.Contains("status code '500'")
                || ex.Message.Contains("status code \"500\"")
                || ex.InnerException is HttpRequestException
                || ex.InnerException is SocketException;
        }

        private static IEnumerable<string> BuildWebSocketUrlCandidates(string wsUrl)
        {
            var trimmed = wsUrl.TrimEnd('/');
            yield return trimmed + "/";
            yield return trimmed;
        }

        private async Task ReceiveWebSocketMessagesAsync(ClientWebSocket socket, CancellationToken cancellationToken)
        {
            var buffer = new byte[8192];

            while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var builder = new StringBuilder();
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                    if (result.MessageType == WebSocketMessageType.Close) return;
                    builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                } while (!result.EndOfMessage);

                HandleWebSocketMessage(builder.ToString());
            }
        }

        private void HandleWebSocketMessage(string json)
        {
            try
            {
                var message = JsonConvert.DeserializeObject<AnnouncementWebSocketMessage>(json);
                if ((message?.Type == "announcement_message" || message?.Type == "announcement") && message.Data != null)
                {
                    if (ShouldShow(message.Data)) EnqueueAnnouncement(message.Data, true);
                    return;
                }

                var item = JsonConvert.DeserializeObject<AnnouncementItem>(json);
                if (item != null && !string.IsNullOrWhiteSpace(item.Id) && ShouldShow(item))
                {
                    EnqueueAnnouncement(item, true);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"AnnouncementService 解析实时公告失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        private List<AnnouncementItem> ParseAnnouncementItems(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<AnnouncementItem>();

            var token = JToken.Parse(json);
            if (token.Type == JTokenType.Array)
            {
                return token.ToObject<List<AnnouncementItem>>() ?? new List<AnnouncementItem>();
            }

            if (token.Type == JTokenType.Object)
            {
                var obj = (JObject)token;
                var array = obj["results"] ?? obj["items"] ?? obj["data"] ?? obj["announcements"];
                if (array?.Type == JTokenType.Array)
                {
                    return array.ToObject<List<AnnouncementItem>>() ?? new List<AnnouncementItem>();
                }

                var item = obj.ToObject<AnnouncementItem>();
                if (item != null && !string.IsNullOrWhiteSpace(item.Id)) return new List<AnnouncementItem> { item };
            }

            return new List<AnnouncementItem>();
        }

        private bool ShouldShow(AnnouncementItem item)
        {
            return GetSkipReason(item) == null;
        }

        private string GetSkipReason(AnnouncementItem item)
        {
            if (item == null) return "item is null";
            if (string.IsNullOrWhiteSpace(item.Id)) return "id is empty";
            if (!string.IsNullOrWhiteSpace(item.Status) && !string.Equals(item.Status, "published", StringComparison.OrdinalIgnoreCase)) return "status is not published";
            if (settings.Notification.ReadAnnouncementIds?.Contains(item.Id) == true) return "already read";

            var now = DateTimeOffset.Now;
            if (item.ExpiresAt.HasValue && item.ExpiresAt.Value < now) return "expired";
            if (item.EndAt.HasValue && item.EndAt.Value < now) return "ended";
            if (item.StartAt.HasValue && item.StartAt.Value > now) return "not started";

            if (!IsVersionMatched(item)) return "version mismatch";
            if (!IsChannelMatched(item)) return "channel mismatch";

            return null;
        }

        private bool IsVersionMatched(AnnouncementItem item)
        {
            var localVersion = Assembly.GetExecutingAssembly().GetName().Version;
            if (localVersion == null) return true;

            if (string.Equals(item.FilterType, "version", StringComparison.OrdinalIgnoreCase) && item.FilterVersions?.Count > 0)
            {
                return item.FilterVersions.Contains(localVersion.ToString());
            }

            if (!string.IsNullOrWhiteSpace(item.MinVersion) && Version.TryParse(item.MinVersion, out var minVersion) && localVersion < minVersion) return false;
            if (!string.IsNullOrWhiteSpace(item.MaxVersion) && Version.TryParse(item.MaxVersion, out var maxVersion) && localVersion > maxVersion) return false;

            return true;
        }

        private bool IsChannelMatched(AnnouncementItem item)
        {
            var currentChannel = settings.Startup.UpdateChannel.ToString();
            if (string.Equals(item.FilterType, "channel", StringComparison.OrdinalIgnoreCase) && item.FilterChannels?.Count > 0)
            {
                return item.FilterChannels.Any(x => string.Equals(x, currentChannel, StringComparison.OrdinalIgnoreCase));
            }

            if (item.Channels?.Count > 0)
            {
                return item.Channels.Any(x => string.Equals(x, currentChannel, StringComparison.OrdinalIgnoreCase));
            }

            return true;
        }

        private void EnqueueAnnouncement(AnnouncementItem item, bool isNew)
        {
            var message = ToNotificationMessage(item);
            AddAnnouncementHistory(message, item.Id, isNew);
            NotificationCenterService.Enqueue(message);
        }

        private void AddAnnouncementHistory(NotificationMessage message, string announcementId, bool isNew)
        {
            lock (HistorySyncRoot)
            {
                AnnouncementHistory.RemoveAll(x => x.Id == announcementId);
                AnnouncementHistory.Insert(0, new AnnouncementCenterItem
                {
                    Id = announcementId,
                    Type = message.Type,
                    Level = message.Level,
                    Title = message.Title,
                    Summary = message.Summary,
                    Content = message.Content,
                    ActionUrl = message.ActionUrl,
                    CreatedAt = message.CreatedAt,
                    IsRead = IsRead(settings, announcementId),
                    IsNew = isNew && !IsRead(settings, announcementId)
                });
                if (AnnouncementHistory.Count > 100) AnnouncementHistory.RemoveRange(100, AnnouncementHistory.Count - 100);
            }
            UnreadCountChanged?.Invoke();
        }

        private NotificationMessage ToNotificationMessage(AnnouncementItem item)
        {
            var type = MapType(item.AnnouncementType, item.Type);
            var level = MapLevel(item.Level, item.AnnouncementType);
            var displaySeconds = item.DisplaySeconds > 0 ? item.DisplaySeconds : GetDefaultDisplaySeconds(type, level);

            return new NotificationMessage
            {
                Id = "announcement-" + item.Id,
                Type = type,
                Level = level,
                Title = PickLocalizedText(item.Title),
                Summary = BuildSummary(item),
                Content = PickLocalizedText(item.Content),
                Icon = string.IsNullOrWhiteSpace(item.Icon) ? GetDefaultIcon(type, level) : item.Icon,
                ActionText = PickLocalizedText(item.ActionText),
                ActionUrl = item.ActionUrl,
                DisplaySeconds = displaySeconds,
                ForcePopup = item.ForcePopup || (level == NotificationMessageLevel.Critical && settings.Notification.IsForcePopupEnabled),
                Priority = item.Priority,
                CreatedAt = item.PublishedAt?.LocalDateTime ?? item.CreatedAt?.LocalDateTime ?? DateTime.Now,
                Source = "announcement",
                ProviderId = ProviderId,
                AnnouncementId = item.Id,
                AnnouncementType = string.IsNullOrWhiteSpace(item.AnnouncementType) ? item.Type : item.AnnouncementType
            };
        }

        private string BuildSummary(AnnouncementItem item)
        {
            var summary = PickLocalizedText(item.Summary);
            if (!string.IsNullOrWhiteSpace(summary)) return summary;

            var content = PickLocalizedText(item.Content);
            if (string.IsNullOrWhiteSpace(content)) return string.Empty;
            content = content.Replace("#", "").Replace("*", "").Replace("\r", " ").Replace("\n", " ").Trim();
            return content.Length > 80 ? content.Substring(0, 80) + "..." : content;
        }

        private string PickLocalizedText(object value)
        {
            if (value == null) return string.Empty;
            if (value is string text) return text;

            try
            {
                var token = value as JToken ?? JToken.FromObject(value);
                if (token.Type == JTokenType.String) return token.Value<string>();
                if (token.Type != JTokenType.Object) return token.ToString();

                var culture = CultureInfo.CurrentUICulture.Name;
                var neutralCulture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                var obj = (JObject)token;
                return obj[culture]?.Value<string>()
                    ?? obj[neutralCulture]?.Value<string>()
                    ?? obj["zh-CN"]?.Value<string>()
                    ?? obj["en-US"]?.Value<string>()
                    ?? obj.Properties().FirstOrDefault()?.Value?.Value<string>()
                    ?? string.Empty;
            }
            catch
            {
                return value.ToString();
            }
        }

        private NotificationMessageType MapType(string announcementType, string type)
        {
            var value = string.IsNullOrWhiteSpace(announcementType) ? type : announcementType;
            switch (value?.ToLowerInvariant())
            {
                case "urgent":
                    return NotificationMessageType.Urgent;
                case "important":
                case "operation":
                    return NotificationMessageType.Important;
                case "update":
                    return NotificationMessageType.Update;
                case "reminder":
                    return NotificationMessageType.Reminder;
                default:
                    return NotificationMessageType.Other;
            }
        }

        private NotificationMessageLevel MapLevel(string level, string announcementType)
        {
            if (string.Equals(announcementType, "urgent", StringComparison.OrdinalIgnoreCase)) return NotificationMessageLevel.Critical;
            if (string.Equals(announcementType, "operation", StringComparison.OrdinalIgnoreCase)) return NotificationMessageLevel.High;

            switch (level?.ToLowerInvariant())
            {
                case "critical": return NotificationMessageLevel.Critical;
                case "high": return NotificationMessageLevel.High;
                case "low": return NotificationMessageLevel.Low;
                default: return NotificationMessageLevel.Normal;
            }
        }

        private int GetDefaultDisplaySeconds(NotificationMessageType type, NotificationMessageLevel level)
        {
            if (type == NotificationMessageType.Urgent || level == NotificationMessageLevel.Critical) return Math.Max(1, settings.Notification.UrgentDurationSeconds);
            if (type == NotificationMessageType.Important || level == NotificationMessageLevel.High) return Math.Max(1, settings.Notification.ImportantDurationSeconds);
            if (type == NotificationMessageType.Update) return Math.Max(1, settings.Notification.UpdateDurationSeconds);
            if (type == NotificationMessageType.Reminder) return Math.Max(1, settings.Notification.ReminderDurationSeconds);
            return Math.Max(1, settings.Notification.OtherDurationSeconds);
        }

        private string GetDefaultIcon(NotificationMessageType type, NotificationMessageLevel level)
        {
            if (type == NotificationMessageType.Urgent || level == NotificationMessageLevel.Critical) return "Warning";
            if (type == NotificationMessageType.Important || level == NotificationMessageLevel.High) return "Important";
            if (type == NotificationMessageType.Update) return "Update";
            if (type == NotificationMessageType.Reminder) return "Reminder";
            return "Info";
        }

        private string BuildClientAnnouncementsUrl(string baseUrl, string token)
        {
            var trimmed = baseUrl.TrimEnd('/');
            if (trimmed.EndsWith("/api/announcement/client/announcements", StringComparison.OrdinalIgnoreCase))
            {
                return $"{trimmed}/?token={Uri.EscapeDataString(token)}";
            }
            if (!trimmed.EndsWith("/api/announcement", StringComparison.OrdinalIgnoreCase))
            {
                trimmed += "/api/announcement";
            }
            return $"{trimmed}/client/announcements/?token={Uri.EscapeDataString(token)}";
        }

        private string BuildWebSocketUrl()
        {
            var token = settings.Notification.AnnouncementSoftwareToken;
            if (string.IsNullOrWhiteSpace(token)) return string.Empty;

            var wsUrl = settings.Notification.AnnouncementWebSocketUrl;
            if (!string.IsNullOrWhiteSpace(wsUrl))
            {
                var trimmed = wsUrl.TrimEnd('/');
                if (trimmed.EndsWith("/" + token, StringComparison.OrdinalIgnoreCase)) return trimmed + "/";
                if (trimmed.EndsWith("/ws/announcement", StringComparison.OrdinalIgnoreCase)) return $"{trimmed}/{Uri.EscapeDataString(token)}/";
                return trimmed + "/";
            }

            var baseUrl = settings.Notification.AnnouncementApiBaseUrl;
            if (string.IsNullOrWhiteSpace(baseUrl)) return string.Empty;

            var uri = new Uri(baseUrl.TrimEnd('/'));
            var scheme = uri.Scheme == "https" ? "wss" : "ws";
            return $"{scheme}://{uri.Authority}/ws/announcement/{Uri.EscapeDataString(token)}/";
        }

        public void Dispose()
        {
            StopAsync().ConfigureAwait(false);
        }
    }
}
