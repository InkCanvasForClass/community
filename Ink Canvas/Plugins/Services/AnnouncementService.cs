using System;
using System.Collections.Generic;
using System.Linq;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// <see cref="IAnnouncementService"/> 的宿主实现：包装 <see cref="Ink_Canvas.Helpers.AnnouncementService"/> 静态方法，
    /// 把宿主未读数变化事件桥接到 SDK 事件。
    /// </summary>
    internal sealed class AnnouncementService : IAnnouncementService
    {
        public AnnouncementService()
        {
            Ink_Canvas.Helpers.AnnouncementService.UnreadCountChanged -= OnUnreadCountChanged;
            Ink_Canvas.Helpers.AnnouncementService.UnreadCountChanged += OnUnreadCountChanged;
        }

        public event Action UnreadCountChanged;

        private void OnUnreadCountChanged() => UnreadCountChanged?.Invoke();

        public int GetUnreadCount()
        {
            try
            {
                var settings = MainWindow.Settings;
                return settings == null ? 0 : Ink_Canvas.Helpers.AnnouncementService.GetUnreadCount(settings);
            }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"AnnouncementService.GetUnreadCount failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
                return 0;
            }
        }

        public IReadOnlyList<PluginAnnouncement> GetHistory()
        {
            try
            {
                return Ink_Canvas.Helpers.AnnouncementService.GetAnnouncementHistory()
                    .Select(a => new PluginAnnouncement
                    {
                        Id = a.Id ?? "",
                        Title = a.Title ?? "",
                        Summary = a.Summary ?? "",
                        Content = a.Content ?? "",
                        CreatedAt = a.CreatedAt,
                        IsRead = a.IsRead,
                        IsNew = a.IsNew,
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"AnnouncementService.GetHistory failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
                return new List<PluginAnnouncement>();
            }
        }

        public void MarkAsRead(string announcementId)
        {
            try
            {
                var settings = MainWindow.Settings;
                if (settings != null && !string.IsNullOrWhiteSpace(announcementId))
                    Ink_Canvas.Helpers.AnnouncementService.MarkAsRead(settings, announcementId);
            }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"AnnouncementService.MarkAsRead failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
            }
        }

        public void MarkAllAsRead()
        {
            try
            {
                var settings = MainWindow.Settings;
                if (settings != null) Ink_Canvas.Helpers.AnnouncementService.MarkAllAsRead(settings);
            }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"AnnouncementService.MarkAllAsRead failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
            }
        }

        public void ClearHistory()
        {
            try { Ink_Canvas.Helpers.AnnouncementService.ClearAnnouncementHistory(); }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"AnnouncementService.ClearHistory failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
            }
        }
    }
}
