using Ink_Canvas.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ink_Canvas.Helpers
{
    internal static class NotificationCenterService
    {
        private static readonly object SyncRoot = new object();
        private static readonly List<NotificationMessage> Queue = new List<NotificationMessage>();
        private static readonly List<NotificationMessage> History = new List<NotificationMessage>();
        private static bool isShowing;
        private static bool _isFirstDuplicateInCurrentSequence;
        private const short _deduplicationWindowSeconds = 2;
        private class LastMessageInfo
        {
            public string Title { get; set; }
            public DateTime Time { get; set; }
            public string Source { get; set; }
            public string Summary { get; set; }
        }
        private static LastMessageInfo _lastMessage = new LastMessageInfo();
        private static bool IsDuplicate(NotificationMessage message)
        {
            ///<summary>
            ///在一定时间内和前条标题相同，内容相同，来源相同的消息返回true
            ///</summary>
            if (string.IsNullOrWhiteSpace(_lastMessage.Title) || string.IsNullOrWhiteSpace(message.Title)) return false;

            TimeSpan interval = message.CreatedAt - _lastMessage.Time;
            if (interval.TotalSeconds < 0) LogHelper.WriteLogToFile("消息队列为乱序", LogHelper.LogType.Info);
            double totalSeconds = Math.Abs(interval.TotalSeconds);
            if (_lastMessage.Title == message.Title && totalSeconds <= _deduplicationWindowSeconds && message.Source == _lastMessage.Source && message.Summary == _lastMessage.Summary)
            {
                _lastMessage.Time = message.CreatedAt;
                if (_isFirstDuplicateInCurrentSequence == true) LogHelper.WriteLogToFile($"{message.Source}发送的标题为{message.Title}的消息已被消息去重拦截", LogHelper.LogType.Info);
                _isFirstDuplicateInCurrentSequence = false;
                return true;
            }

            return false;
        }

        public static event Action<NotificationMessage> NotificationRequested;

        public static IReadOnlyList<NotificationMessage> GetHistory(string source = null)
        {
            lock (SyncRoot)
            {
                if (string.IsNullOrWhiteSpace(source)) return History.ToList();
                return History.Where(x => string.Equals(x.Source, source, StringComparison.OrdinalIgnoreCase)).ToList();
            }
        }

        public static void ClearHistory(string source = null)
        {
            lock (SyncRoot)
            {
                if (string.IsNullOrWhiteSpace(source)) History.Clear();
                else History.RemoveAll(x => string.Equals(x.Source, source, StringComparison.OrdinalIgnoreCase));
            }
        }

        public static void Enqueue(NotificationMessage message)
        {
            if (message == null) return;
            if (string.IsNullOrWhiteSpace(message.Title) && string.IsNullOrWhiteSpace(message.Summary)) return;

            lock (SyncRoot)
            {
                if (IsDuplicate(message)) return;
                if (!string.IsNullOrWhiteSpace(message.Title))
                {
                    //只有非空消息才做去重
                    _isFirstDuplicateInCurrentSequence = true;
                    _lastMessage.Title = message.Title;
                    _lastMessage.Time = message.CreatedAt;
                    _lastMessage.Source = message.Source;
                    _lastMessage.Summary = message.Summary;
                }
                Queue.Add(message);
                History.Insert(0, message);
                if (History.Count > 100) History.RemoveRange(100, History.Count - 100);
            }

            TryShowNext();
        }

        public static void EnqueueText(string text, NotificationMessageLevel level = NotificationMessageLevel.Normal, int displaySeconds = 3)
        {
            Enqueue(new NotificationMessage
            {
                Id = "local-" + Guid.NewGuid().ToString("N"),
                Type = level >= NotificationMessageLevel.High ? NotificationMessageType.Important : NotificationMessageType.Other,
                Level = level,
                Title = text,
                Summary = string.Empty,
                Icon = level >= NotificationMessageLevel.High ? "Warning" : "Info",
                DisplaySeconds = displaySeconds,
                Priority = (int)level * 100,
                Source = "local",
                ProviderId = "local"
            });
        }

        public static void NotifyCurrentClosed()
        {
            lock (SyncRoot)
            {
                isShowing = false;
            }

            TryShowNext();
        }

        /// <summary>
        /// 摘除属于指定插件的通知回调。通知消息的 <see cref="NotificationMessage.Action"/>
        /// 字段直接指向插件 ALC 里的 Action，留在队列或历史中都会阻止插件 AssemblyLoadContext 卸载。
        /// 该方法同时清空队列中指定来源的项，并把历史项的 <c>Action</c> 字段置空，
        /// 避免插件用户的通知历史因热重载而被清空。
        /// </summary>
        public static int ClearPluginCallbacks(string pluginId, string providerId = "plugin")
        {
            if (string.IsNullOrEmpty(pluginId)) return 0;

            var removed = 0;
            lock (SyncRoot)
            {
                // 队列中待显示的：直接整条移除，避免用户点击触发旧回调。
                for (var i = Queue.Count - 1; i >= 0; i--)
                {
                    var msg = Queue[i];
                    if (msg == null) continue;
                    if (msg.Action == null) continue;
                    if (!pluginId.Equals(msg.Source, StringComparison.OrdinalIgnoreCase)
                        && !pluginId.Equals(msg.ProviderId, StringComparison.OrdinalIgnoreCase))
                        continue;

                    Queue.RemoveAt(i);
                    removed++;
                }

                // 历史中已显示的：保留文案给用户看，但清掉 Action 回调本身。
                foreach (var msg in History)
                {
                    if (msg == null || msg.Action == null) continue;
                    if (!pluginId.Equals(msg.Source, StringComparison.OrdinalIgnoreCase)
                        && !pluginId.Equals(msg.ProviderId, StringComparison.OrdinalIgnoreCase))
                        continue;

                    msg.Action = null;
                    removed++;
                }

                // 当前正在显示的那条走 NotificationRequested 事件的订阅；事件订阅由
                // PluginDelegateCleaner.SweepStaticEvents 单独清理，这里不再处理。
            }

            return removed;
        }

        private static void TryShowNext()
        {
            NotificationMessage next = null;

            lock (SyncRoot)
            {
                if (isShowing || Queue.Count == 0) return;

                next = Queue
                    .OrderByDescending(x => x.Level)
                    .ThenByDescending(x => x.Priority)
                    .ThenBy(x => x.CreatedAt)
                    .First();
                Queue.Remove(next);
                isShowing = true;
            }

            try
            {
                NotificationRequested?.Invoke(next);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"NotificationCenterService 显示通知失败: {ex.Message}", LogHelper.LogType.Error);
                NotifyCurrentClosed();
            }
        }
    }
}
