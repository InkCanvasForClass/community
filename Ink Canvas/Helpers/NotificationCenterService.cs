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
        private struct JudgeRepeatMessageStruct
        {
            public string Title { get; set; }
            public DateTime Time { get; set; }
            public string Source { get; set; }
        }
        private static JudgeRepeatMessageStruct JudgeRepeatMessage = new JudgeRepeatMessageStruct();

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
                ///<summary>
                ///在一定时间内和前一条相同且来源一样就不重复显示
                ///场景：教学课堂环境，插件可能因异常（如死循环、网络重连）高频推送相同通知。
                ///策略：采用滑动窗口去重（2秒内）。若同一来源的相同标题持续出现，每次出现都刷新窗口起点，使通知彻底沉默，直到插件停止刷屏 2 秒以上。
                ///效果：杜绝课堂被反复弹窗干扰，同时不影响正常间隔（> 2秒）的有效通知。
                ///</summary>
                if (!string.IsNullOrEmpty(message.Title) && JudgeRepeatMessage.Title!=null)
                {
                    TimeSpan interval = message.CreatedAt - JudgeRepeatMessage.Time;
                    double TotalSeconds = interval.TotalSeconds;
                    if(JudgeRepeatMessage.Title == message.Title && TotalSeconds<=2 && message.Source==JudgeRepeatMessage.Source)
                    {
                        JudgeRepeatMessage.Time = message.CreatedAt;
                        return;
                    }
                    
                }
                JudgeRepeatMessage.Title = message.Title;
                JudgeRepeatMessage.Time = message.CreatedAt;
                JudgeRepeatMessage.Source = message.Source;
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
