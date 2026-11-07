using Microsoft.Office.Interop.PowerPoint;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Timers;
using Timer = System.Timers.Timer;

namespace Ink_Canvas.Helpers
{
    public partial class ROTPPTManager : IPPTLinkManager
    {
        #region Events
        public event Action<object> SlideShowBegin;
        public event Action<object> SlideShowNextSlide;
        public event Action<object> SlideShowEnd;
        public event Action<object> PresentationOpen;
        public event Action<object> PresentationClose;
        public event Action<bool> PPTConnectionChanged;
        public event Action<bool> SlideShowStateChanged;
        #endregion

        #region Properties
        public dynamic PPTApplication { get; private set; }
        public dynamic CurrentPresentation { get; private set; }
        public dynamic CurrentSlides { get; private set; }
        public dynamic CurrentSlide { get; private set; }
        public int SlidesCount { get; private set; }

        private volatile bool _cachedIsConnected;
        private volatile bool _cachedIsInSlideShow;

        public bool IsConnected => _cachedIsConnected && PPTApplication != null;

        public bool IsInSlideShow => _cachedIsInSlideShow && _pptSlideShowWindow != null;
        public bool IsSupportWPS { get; set; } = false;
        public bool SkipAnimationsWhenNavigating { get; set; } = false;
        #endregion

        #region Private Fields
        private Thread _monitoringThread;
        private bool _shouldStop = false;
        private readonly object _monitoringLock = new object();
        private Timer _wpsProcessCheckTimer;
        private Process _wpsProcess;
        private bool _isModuleUnloading = false;
        private bool _hasWpsProcessId;
        private DateTime _wpsProcessRecordTime = DateTime.MinValue;
        private int _wpsProcessCheckCount;
        private WpsWindowInfo _lastForegroundWpsWindow;
        private DateTime _lastWindowCheckTime = DateTime.MinValue;
        private bool _lastSlideShowState;
        private readonly object _lockObject = new object();
        private bool _disposed;

        private dynamic _pptActivePresentation;
        private dynamic _pptSlideShowWindow;
        private int _polling = 0;
        private bool _forcePolling = true;
        private bool _bindingEvents = false;
        private DateTime _updateTime;
        private int _lastPolledSlideNumber = -1;
        private int _reconnectFailureCount = 0;
        private DateTime _nextReconnectAttemptUtc = DateTime.MinValue;
        #endregion

        #region Throttled diagnostics
        private static int _diagExceptionCount;

        /// <summary>
        /// 高频路径（500ms 轮询循环 / 窗口枚举回调 / 连续翻页）用的节流日志。
        /// 坏设备下每轮都会抛，直接写 Info 会瞬间刷爆 5MB 日志并触发 LogHelper 清空整个 Logs 目录。
        /// </summary>
        private static void LogCallbackException(string what, Exception ex)
        {
            var n = Interlocked.Increment(ref _diagExceptionCount);
            if (n == 1 || n % 100 == 0)
                LogHelper.WriteLogToFile($"[PPT] {what} 异常（累计 {n} 次）: {ex.Message}", LogHelper.LogType.Info);
        }
        #endregion

        #region Constructor & Initialization
        public ROTPPTManager()
        {
        }

        public void StartMonitoring()
        {
            if (_disposed) return;

            lock (_monitoringLock)
            {
                if (_monitoringThread != null && _monitoringThread.IsAlive)
                {
                    return;
                }

                _shouldStop = false;
                _monitoringThread = new Thread(PPTComService)
                {
                    IsBackground = true,
                    Name = "PPTMonitoringThread"
                };
                _monitoringThread.Start();
            }
        }

        public void StopMonitoring(bool isShutdown = false)
        {
            lock (_monitoringLock)
            {
                _shouldStop = true;

                if (_monitoringThread != null && _monitoringThread.IsAlive)
                {
                    if (!_monitoringThread.Join(2000))
                    {
                        LogHelper.WriteLogToFile("等待监控线程退出超时", LogHelper.LogType.Warning);
                    }
                }

                DisconnectFromPPT();
            }
        }

        private int _isReloading; // 0 = idle, 1 = in reload
        private volatile bool _suppressDisconnectEvent;

        public void ReloadConnection()
        {
            if (_disposed) return;

            if (Interlocked.CompareExchange(ref _isReloading, 1, 0) != 0)
            {
                return;
            }

            try
            {
                LogHelper.WriteLogToFile("[ROT] 执行热重载：强制断开并重新连接", LogHelper.LogType.Event);

                lock (_monitoringLock)
                {
                    _shouldStop = true;

                    if (_monitoringThread != null && _monitoringThread.IsAlive)
                    {
                        if (!_monitoringThread.Join(2000))
                        {
                            LogHelper.WriteLogToFile("等待监控线程退出超时（热重载）", LogHelper.LogType.Warning);
                        }
                    }

                    _suppressDisconnectEvent = true;
                    try
                    {
                        DisconnectFromPPT();
                    }
                    finally
                    {
                        _suppressDisconnectEvent = false;
                    }
                    _monitoringThread = null;
                    _shouldStop = false;
                    _isModuleUnloading = false;
                }

                StartMonitoring();
            }
            finally
            {
                Interlocked.Exchange(ref _isReloading, 0);
            }
        }
        #endregion

        #region Dispose
        public void Dispose()
        {
            if (!_disposed)
            {
                StopMonitoring();
                StopWpsProcessCheckTimer();

                _disposed = true;
            }
        }
        #endregion
    }
}


