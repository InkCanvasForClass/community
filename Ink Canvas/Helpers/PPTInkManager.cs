using Microsoft.Office.Interop.PowerPoint;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Ink;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// PPT墨迹管理器 - 负责PPT中墨迹的保存、加载和同步
    /// </summary>
    public class PPTInkManager : IDisposable
    {
        #region Properties
        public bool IsAutoSaveEnabled { get; set; } = true;
        public string AutoSaveLocation { get; set; } = "";
        public StrokeCollection CurrentStrokes { get; private set; } = new StrokeCollection();
        #endregion

        #region Private Fields
        private MemoryStream[] _memoryStreams;
        private int _maxSlides = 100;
        private string _currentPresentationId = "";
        private readonly object _lockObject = new object();
        private bool _disposed;

        // 墨迹锁定机制，防止翻页时的墨迹冲突
        private DateTime _inkLockUntil = DateTime.MinValue;
        private int _lockedSlideIndex = -1;
        private const int InkLockMilliseconds = 500;

        // 添加快速切换保护机制
        private DateTime _lastSwitchTime = DateTime.MinValue;
        private int _lastSwitchSlideIndex = -1;
        private const int MinSwitchIntervalMs = 100; // 最小切换间隔100毫秒

        // 内存管理相关字段
        private long _totalMemoryUsage = 0;
        private const long MaxMemoryUsageBytes = 100 * 1024 * 1024; // 100MB限制
        private DateTime _lastMemoryCleanup = DateTime.MinValue;
        private const int MemoryCleanupIntervalMinutes = 5; // 5分钟清理一次
        #endregion

        #region Constructor
        public PPTInkManager()
        {
            InitializeMemoryStreams();
        }

        private void InitializeMemoryStreams()
        {
            _memoryStreams = new MemoryStream[_maxSlides + 2];
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// 初始化新的演示文稿
        /// </summary>
        public void InitializePresentation(Presentation presentation)
        {
            if (presentation == null) return;

            lock (_lockObject)
            {
                try
                {
                    // 完全清理之前的墨迹状态
                    ClearAllStrokes();

                    // 重置墨迹锁定状态
                    _inkLockUntil = DateTime.MinValue;
                    _lockedSlideIndex = -1;

                    // 生成演示文稿唯一标识符
                    _currentPresentationId = GeneratePresentationId(presentation);

                    // 重新初始化内存流数组
                    int slideCount = 0;
                    try
                    {
                        slideCount = presentation.Slides.Count;
                    }
                    catch (COMException comEx)
                    {
                        var hr = (uint)comEx.HResult;
                        if (hr == 0x80048010)
                        {
                            return;
                        }
                        throw;
                    }
                    _memoryStreams = new MemoryStream[slideCount + 2];

                    // 如果启用自动保存，尝试加载已保存的墨迹
                    if (IsAutoSaveEnabled && !string.IsNullOrEmpty(AutoSaveLocation))
                    {
                        LoadSavedStrokes();
                    }

                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"初始化演示文稿墨迹管理失败: {ex}", LogHelper.LogType.Error);
                }
            }
        }

        /// <summary>
        /// 保存当前页面的墨迹
        /// </summary>
        public void SaveCurrentSlideStrokes(int slideIndex, StrokeCollection strokes)
        {
            if (slideIndex <= 0 || strokes == null) return;

            lock (_lockObject)
            {
                try
                {
                    // 检查墨迹锁定 
                    if (!CanWriteInk(slideIndex))
                    {
                        if (DateTime.Now < _inkLockUntil)
                        {
                        }
                        return;
                    }

                    if (slideIndex < _memoryStreams.Length)
                    {
                        // 先释放旧的内存流，防止内存泄漏
                        try
                        {
                            _memoryStreams[slideIndex]?.Dispose();
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLogToFile($"释放旧内存流失败: {ex}", LogHelper.LogType.Warning);
                        }

                        // 创建新的内存流
                        var ms = new MemoryStream();
                        strokes.Save(ms);
                        ms.Position = 0;
                        _memoryStreams[slideIndex] = ms;

                        if (ms.Length > 0)
                        {
                        }

                        // 检查内存使用情况
                        CheckAndPerformMemoryCleanup();
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"保存第{slideIndex}页墨迹失败: {ex}", LogHelper.LogType.Error);
                }
            }
        }

        /// <summary>
        /// 强制保存指定页面的墨迹
        /// </summary>
        public void ForceSaveSlideStrokes(int slideIndex, StrokeCollection strokes)
        {
            if (slideIndex <= 0 || strokes == null) return;

            lock (_lockObject)
            {
                try
                {
                    if (slideIndex < _memoryStreams.Length)
                    {
                        // 先释放旧的内存流，防止内存泄漏
                        try
                        {
                            _memoryStreams[slideIndex]?.Dispose();
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLogToFile($"释放旧内存流失败: {ex}", LogHelper.LogType.Warning);
                        }

                        // 创建新的内存流
                        var ms = new MemoryStream();
                        strokes.Save(ms);
                        ms.Position = 0;
                        _memoryStreams[slideIndex] = ms;

                        LogHelper.WriteLogToFile($"已强制保存第{slideIndex}页墨迹，大小: {ms.Length} bytes", LogHelper.LogType.Trace);
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"强制保存第{slideIndex}页墨迹失败: {ex}", LogHelper.LogType.Error);
                }
            }
        }

        /// <summary>
        /// 加载指定页面的墨迹
        /// </summary>
        public StrokeCollection LoadSlideStrokes(int slideIndex)
        {
            if (slideIndex <= 0) return new StrokeCollection();

            lock (_lockObject)
            {
                try
                {
                    if (slideIndex < _memoryStreams.Length && _memoryStreams[slideIndex] != null && _memoryStreams[slideIndex].Length > 0)
                    {
                        _memoryStreams[slideIndex].Position = 0;
                        var strokes = new StrokeCollection(_memoryStreams[slideIndex]);
                        return strokes;
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"加载第{slideIndex}页墨迹失败: {ex}", LogHelper.LogType.Error);
                }
            }

            return new StrokeCollection();
        }

        /// <summary>
        /// 切换到指定页面并加载墨迹
        /// </summary>
        public StrokeCollection SwitchToSlide(int slideIndex, StrokeCollection currentStrokes = null)
        {
            lock (_lockObject)
            {
                try
                {
                    // 检查快速切换保护
                    var now = DateTime.Now;
                    if (now - _lastSwitchTime < TimeSpan.FromMilliseconds(MinSwitchIntervalMs) &&
                        _lastSwitchSlideIndex == slideIndex)
                    {
                        LogHelper.WriteLogToFile($"快速切换保护：忽略重复的页面切换请求 {slideIndex}", LogHelper.LogType.Warning);
                        return LoadSlideStrokes(slideIndex);
                    }


                    // 设置墨迹锁定
                    LockInkForSlide(slideIndex);

                    // 加载新页面的墨迹
                    var newStrokes = LoadSlideStrokes(slideIndex);

                    // 更新切换记录
                    _lastSwitchTime = now;
                    _lastSwitchSlideIndex = slideIndex;

                    if (newStrokes.Count > 0)
                    {
                    }

                    return newStrokes;
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"切换到第{slideIndex}页失败: {ex}", LogHelper.LogType.Error);
                    return new StrokeCollection();
                }
            }
        }

        /// <summary>
        /// 保存所有墨迹到文件
        /// </summary>
        /// <param name="presentation">演示文稿对象</param>
        /// <param name="currentSlideIndex">当前播放的页码，如果提供则使用此值保存位置，否则使用_lockedSlideIndex</param>
        public void SaveAllStrokesToFile(Presentation presentation, int currentSlideIndex = -1)
        {
            if (!IsAutoSaveEnabled || string.IsNullOrEmpty(AutoSaveLocation) || presentation == null) return;

            lock (_lockObject)
            {
                try
                {
                    var folderPath = GetPresentationFolderPath();
                    if (!Directory.Exists(folderPath))
                    {
                        Directory.CreateDirectory(folderPath);
                    }

                    // 保存位置信息
                    try
                    {
                        // 优先使用传入的当前页码，否则使用锁定的页码
                        int positionToSave = currentSlideIndex > 0 ? currentSlideIndex : _lockedSlideIndex;
                        // 如果都没有有效值，尝试使用最后切换的页码
                        if (positionToSave <= 0 && _lastSwitchSlideIndex > 0)
                        {
                            positionToSave = _lastSwitchSlideIndex;
                        }
                        
                        if (positionToSave > 0)
                        {
                            File.WriteAllText(Path.Combine(folderPath, "Position"), positionToSave.ToString());
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"保存位置信息失败: {ex}", LogHelper.LogType.Error);
                    }

                    // 保存所有页面的墨迹
                    int savedCount = 0;
                    int slideCount = 0;

                    try
                    {
                        slideCount = presentation.Slides.Count;
                    }
                    catch (COMException comEx)
                    {
                        var hr = (uint)comEx.HResult;
                        if (hr == 0x80048010)
                        {
                            return;
                        }
                        throw;
                    }

                    for (int i = 1; i <= slideCount && i < _memoryStreams.Length; i++)
                    {
                        if (_memoryStreams[i] != null)
                        {
                            try
                            {
                                if (_memoryStreams[i].Length > 8)
                                {
                                    var srcBuf = new byte[_memoryStreams[i].Length];
                                    _memoryStreams[i].Position = 0;
                                    var byteLength = _memoryStreams[i].Read(srcBuf, 0, srcBuf.Length);

                                    var filePath = Path.Combine(folderPath, i.ToString("0000") + ".icstk");
                                    File.WriteAllBytes(filePath, srcBuf);
                                    savedCount++;

                                }
                                else
                                {
                                    // 删除空的墨迹文件
                                    var filePath = Path.Combine(folderPath, i.ToString("0000") + ".icstk");
                                    if (File.Exists(filePath))
                                    {
                                        File.Delete(filePath);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                LogHelper.WriteLogToFile($"保存第{i}页墨迹失败: {ex}", LogHelper.LogType.Error);
                            }
                        }
                    }

                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"保存墨迹到文件失败: {ex}", LogHelper.LogType.Error);
                }
            }
        }

        /// <summary>
        /// 从文件加载已保存的墨迹
        /// </summary>
        public void LoadSavedStrokes()
        {
            if (!IsAutoSaveEnabled || string.IsNullOrEmpty(AutoSaveLocation)) return;

            lock (_lockObject)
            {
                try
                {
                    var folderPath = GetPresentationFolderPath();
                    if (!Directory.Exists(folderPath)) return;

                    var files = new DirectoryInfo(folderPath).GetFiles("*.icstk");
                    int loadedCount = 0;

                    foreach (var file in files)
                    {
                        try
                        {
                            if (int.TryParse(Path.GetFileNameWithoutExtension(file.Name), out int slideIndex))
                            {
                                if (slideIndex > 0 && slideIndex < _memoryStreams.Length)
                                {
                                    var fileBytes = File.ReadAllBytes(file.FullName);
                                    _memoryStreams[slideIndex] = new MemoryStream(fileBytes);
                                    _memoryStreams[slideIndex].Position = 0;
                                    loadedCount++;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLogToFile($"加载墨迹文件{file.Name}失败: {ex}", LogHelper.LogType.Error);
                        }
                    }

                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"从文件加载墨迹失败: {ex}", LogHelper.LogType.Error);
                }
            }
        }

        /// <summary>
        /// 清除所有墨迹
        /// </summary>
        public void ClearAllStrokes()
        {
            lock (_lockObject)
            {
                try
                {
                    // 安全释放所有内存流
                    if (_memoryStreams != null)
                    {
                        for (int i = 0; i < _memoryStreams.Length; i++)
                        {
                            try
                            {
                                _memoryStreams[i]?.Dispose();
                            }
                            catch (Exception ex)
                            {
                                LogHelper.WriteLogToFile($"释放内存流{i}失败: {ex}", LogHelper.LogType.Warning);
                            }
                            finally
                            {
                                _memoryStreams[i] = null;
                            }
                        }

                        // 重新初始化数组
                        _memoryStreams = new MemoryStream[_maxSlides + 2];
                    }

                    CurrentStrokes?.Clear();
                    LogHelper.WriteLogToFile("已清除所有墨迹", LogHelper.LogType.Trace);
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"清除墨迹失败: {ex}", LogHelper.LogType.Error);
                }
            }
        }

        /// <summary>
        /// 翻页后锁定墨迹写入
        /// </summary>
        public void LockInkForSlide(int slideIndex)
        {
            _inkLockUntil = DateTime.Now.AddMilliseconds(InkLockMilliseconds);
            _lockedSlideIndex = slideIndex;
        }

        /// <summary>
        /// 检查是否可以写入墨迹
        /// </summary>
        public bool CanWriteInk(int currentSlideIndex)
        {
            // 如果锁定时间已过，允许写入
            if (DateTime.Now >= _inkLockUntil)
            {
                return true;
            }

            // 如果当前页面与锁定页面相同，允许写入（用户在当前页面绘制）
            if (currentSlideIndex == _lockedSlideIndex)
            {
                return true;
            }

            // 如果当前页面不是锁定页面，但锁定时间很短（小于50ms），允许写入
            // 这样可以确保旧页面的墨迹能够及时保存
            if (DateTime.Now - (_inkLockUntil.AddMilliseconds(-InkLockMilliseconds)) < TimeSpan.FromMilliseconds(50))
            {
                return true;
            }

            // 只有在快速切换且页面不同时才锁定
            return false;
        }

        /// <summary>
        /// 重置墨迹锁定状态
        /// </summary>
        public void ResetLockState()
        {
            lock (_lockObject)
            {
                _inkLockUntil = DateTime.MinValue;
                _lockedSlideIndex = -1;
                _lastSwitchTime = DateTime.MinValue;
                _lastSwitchSlideIndex = -1;
            }
        }

        /// <summary>
        /// 检查并执行内存清理
        /// </summary>
        private void CheckAndPerformMemoryCleanup()
        {
            try
            {
                var now = DateTime.Now;

                // 检查是否需要执行内存清理
                if (now - _lastMemoryCleanup < TimeSpan.FromMinutes(MemoryCleanupIntervalMinutes))
                {
                    return;
                }

                // 计算当前内存使用量
                long currentMemoryUsage = 0;
                if (_memoryStreams != null)
                {
                    for (int i = 0; i < _memoryStreams.Length; i++)
                    {
                        if (_memoryStreams[i] != null)
                        {
                            currentMemoryUsage += _memoryStreams[i].Length;
                        }
                    }
                }

                _totalMemoryUsage = currentMemoryUsage;

                // 如果内存使用量超过限制，执行清理
                if (currentMemoryUsage > MaxMemoryUsageBytes)
                {
                    LogHelper.WriteLogToFile($"内存使用量超限 ({currentMemoryUsage / 1024 / 1024}MB)，开始清理", LogHelper.LogType.Warning);

                    // 清理非当前页面的墨迹
                    CleanupInactiveSlideStrokes();

                    _lastMemoryCleanup = now;
                    LogHelper.WriteLogToFile($"内存清理完成，当前使用量: {_totalMemoryUsage / 1024 / 1024}MB", LogHelper.LogType.Trace);
                }
                else
                {
                    _lastMemoryCleanup = now;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"内存清理检查失败: {ex}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 清理非活跃页面的墨迹
        /// </summary>
        private void CleanupInactiveSlideStrokes()
        {
            try
            {
                if (_memoryStreams == null) return;

                int cleanedCount = 0;
                long freedMemory = 0;

                for (int i = 0; i < _memoryStreams.Length; i++)
                {
                    // 保留当前锁定页面和最近访问的页面
                    if (i == _lockedSlideIndex || i == _lastSwitchSlideIndex)
                    {
                        continue;
                    }

                    if (_memoryStreams[i] != null)
                    {
                        long memorySize = _memoryStreams[i].Length;

                        try
                        {
                            _memoryStreams[i].Dispose();
                            freedMemory += memorySize;
                            cleanedCount++;
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLogToFile($"清理页面{i}墨迹失败: {ex}", LogHelper.LogType.Warning);
                        }
                        finally
                        {
                            _memoryStreams[i] = null;
                        }
                    }
                }

                if (cleanedCount > 0)
                {
                    LogHelper.WriteLogToFile($"已清理{cleanedCount}个页面的墨迹，释放内存: {freedMemory / 1024}KB", LogHelper.LogType.Trace);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"清理非活跃页面墨迹失败: {ex}", LogHelper.LogType.Error);
            }
        }
        #endregion

        #region Private Methods
        private string GeneratePresentationId(Presentation presentation)
        {
            try
            {
                var presentationPath = presentation.FullName;
                var fileHash = GetFileHash(presentationPath);
                return $"{presentation.Name}_{presentation.Slides.Count}_{fileHash}";
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"生成演示文稿ID失败: {ex}", LogHelper.LogType.Error);
                return $"unknown_{DateTime.Now.Ticks}";
            }
        }

        private string GetFileHash(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath)) return "unknown";

                using (var md5 = MD5.Create())
                {
                    byte[] hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(filePath));
                    return BitConverter.ToString(hashBytes).Replace("-", "").Substring(0, 8);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"计算文件哈希值失败: {ex}", LogHelper.LogType.Error);
                return "error";
            }
        }

        private string GetPresentationFolderPath()
        {
            return Path.Combine(AutoSaveLocation, "Auto Saved - Presentations", _currentPresentationId);
        }
        #endregion

        #region Dispose
        public void Dispose()
        {
            if (!_disposed)
            {
                lock (_lockObject)
                {
                    ClearAllStrokes();
                }
                _disposed = true;
            }
        }
        #endregion
    }
}
