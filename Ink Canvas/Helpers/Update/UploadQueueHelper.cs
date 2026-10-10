using System;
using System.Collections.Generic;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 上传队列帮助类，提供统一的队列管理功能
    /// </summary>
    public static class UploadQueueHelper
    {
        private static readonly List<BaseUploadQueue> _queues = new List<BaseUploadQueue>();
        private static readonly object _syncLock = new object();
        private static volatile bool _initialized = false;

        /// <summary>
        /// 初始化所有上传队列
        /// </summary>
        public static void InitializeAllQueues()
        {
            lock (_syncLock)
            {
                if (_initialized)
                    return;

                // 初始化所有注册的队列
                foreach (var queue in _queues)
                {
                    try
                    {
                        queue.InitializeQueue();
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"[UploadQueueHelper] 初始化队列时出错: {ex.Message}", LogHelper.LogType.Error);
                    }
                }

                _initialized = true;
            }
        }

        /// <summary>
        /// 注册上传队列
        /// </summary>
        /// <param name="queue">上传队列实例</param>
        public static void RegisterQueue(BaseUploadQueue queue)
        {
            if (queue == null)
                return;

            lock (_syncLock)
            {
                if (!_queues.Contains(queue))
                {
                    try
                    {
                        // 先初始化队列，再添加到列表
                        queue.InitializeQueue();
                        _queues.Add(queue);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"[UploadQueueHelper] 注册队列时出错: {ex.Message}", LogHelper.LogType.Error);
                    }
                }
            }
        }

        /// <summary>
        /// 获取所有注册的上传队列
        /// </summary>
        /// <returns>上传队列列表</returns>
        public static IReadOnlyList<BaseUploadQueue> GetAllQueues()
        {
            lock (_syncLock)
            {
                return new List<BaseUploadQueue>(_queues).AsReadOnly();
            }
        }

        /// <summary>
        /// 确保所有队列都已初始化
        /// </summary>
        public static void EnsureQueuesInitialized()
        {
            if (!_initialized)
            {
                InitializeAllQueues();
            }
        }
    }
}