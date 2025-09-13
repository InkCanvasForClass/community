using Microsoft.Office.Interop.PowerPoint;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Ink;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 多PPT墨迹管理器 - 支持多个PPT窗口分别管理墨迹
    /// </summary>
    public class MultiPPTInkManager : IDisposable
    {
        #region Properties
        public bool IsAutoSaveEnabled { get; set; } = true;
        public string AutoSaveLocation { get; set; } = "";
        public PPTManager PPTManager { get; set; }
        #endregion

        #region Private Fields
        private readonly Dictionary<string, PPTInkManager> _presentationManagers;
        private readonly Dictionary<string, PresentationInfo> _presentationInfos;
        private readonly object _lockObject = new object();
        private bool _disposed;
        private string _currentActivePresentationId = "";
        #endregion

        #region Constructor
        public MultiPPTInkManager()
        {
            _presentationManagers = new Dictionary<string, PPTInkManager>();
            _presentationInfos = new Dictionary<string, PresentationInfo>();
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
                    var presentationId = GeneratePresentationId(presentation);
                    
                    // 如果已存在该演示文稿的管理器，先清理
                    if (_presentationManagers.ContainsKey(presentationId))
                    {
                        _presentationManagers[presentationId].Dispose();
                        _presentationManagers.Remove(presentationId);
                    }

                    // 创建新的墨迹管理器
                    var inkManager = new PPTInkManager();
                    inkManager.IsAutoSaveEnabled = IsAutoSaveEnabled;
                    inkManager.AutoSaveLocation = AutoSaveLocation;
                    inkManager.InitializePresentation(presentation);

                    // 保存管理器和演示文稿信息
                    _presentationManagers[presentationId] = inkManager;
                    _presentationInfos[presentationId] = new PresentationInfo
                    {
                        Id = presentationId,
                        Name = presentation.Name,
                        FullName = presentation.FullName,
                        SlideCount = presentation.Slides.Count,
                        CreatedTime = DateTime.Now,
                        LastAccessTime = DateTime.Now
                    };

                    // 设置为当前活跃的演示文稿
                    _currentActivePresentationId = presentationId;

                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"初始化多PPT墨迹管理失败: {ex}", LogHelper.LogType.Error);
                }
            }
        }

        /// <summary>
        /// 切换到指定的演示文稿
        /// </summary>
        public bool SwitchToPresentation(Presentation presentation)
        {
            if (presentation == null) return false;

            lock (_lockObject)
            {
                try
                {
                    var presentationId = GeneratePresentationId(presentation);
                    
                    if (_presentationManagers.ContainsKey(presentationId))
                    {
                        // 如果切换的是不同的演示文稿，先保存当前活跃演示文稿的墨迹
                        if (!string.IsNullOrEmpty(_currentActivePresentationId) && 
                            _currentActivePresentationId != presentationId)
                        {
                            var currentManager = GetCurrentManager();
                            if (currentManager != null)
                            {
                                // 获取当前活跃的演示文稿并保存墨迹
                                var currentPresentation = GetCurrentActivePresentation();
                                if (currentPresentation != null)
                                {
                                    currentManager.SaveAllStrokesToFile(currentPresentation);
                                    LogHelper.WriteLogToFile($"已保存当前演示文稿墨迹: {currentPresentation.Name}", LogHelper.LogType.Trace);
                                }
                            }
                        }

                        _currentActivePresentationId = presentationId;
                        
                        // 更新最后访问时间
                        if (_presentationInfos.ContainsKey(presentationId))
                        {
                            _presentationInfos[presentationId].LastAccessTime = DateTime.Now;
                        }

                    if (_currentActivePresentationId != presentationId)
                    {
                        LogHelper.WriteLogToFile($"已切换到演示文稿: {presentation.Name}", LogHelper.LogType.Trace);
                    }
                        return true;
                    }
                    else
                    {
                        // 如果不存在，尝试初始化
                        InitializePresentation(presentation);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"切换到演示文稿失败: {ex}", LogHelper.LogType.Error);
                    return false;
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
                    var manager = GetCurrentManager();
                    if (manager != null)
                    {
                        manager.SaveCurrentSlideStrokes(slideIndex, strokes);
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"保存当前页面墨迹失败: {ex}", LogHelper.LogType.Error);
                }
            }
        }

        /// <summary>
        /// 强制保存指定页面的墨迹（忽略锁定状态）
        /// </summary>
        public void ForceSaveSlideStrokes(int slideIndex, StrokeCollection strokes)
        {
            if (slideIndex <= 0 || strokes == null) return;

            lock (_lockObject)
            {
                try
                {
                    var manager = GetCurrentManager();
                    if (manager != null)
                    {
                        manager.ForceSaveSlideStrokes(slideIndex, strokes);
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"强制保存页面墨迹失败: {ex}", LogHelper.LogType.Error);
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
                    var manager = GetCurrentManager();
                    if (manager != null)
                    {
                        return manager.LoadSlideStrokes(slideIndex);
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"加载页面墨迹失败: {ex}", LogHelper.LogType.Error);
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
                    var manager = GetCurrentManager();
                    if (manager != null)
                    {
                        return manager.SwitchToSlide(slideIndex, currentStrokes);
                    }
                    else
                    {
                        LogHelper.WriteLogToFile($"无法获取当前墨迹管理器，页面切换失败: {slideIndex}", LogHelper.LogType.Warning);
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"切换页面墨迹失败: {ex}", LogHelper.LogType.Error);
                }
            }

            return new StrokeCollection();
        }

        /// <summary>
        /// 保存所有墨迹到文件
        /// </summary>
        public void SaveAllStrokesToFile(Presentation presentation)
        {
            if (!IsAutoSaveEnabled || string.IsNullOrEmpty(AutoSaveLocation) || presentation == null) return;

            lock (_lockObject)
            {
                try
                {
                    var presentationId = GeneratePresentationId(presentation);
                    if (_presentationManagers.ContainsKey(presentationId))
                    {
                        _presentationManagers[presentationId].SaveAllStrokesToFile(presentation);
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"保存所有墨迹到文件失败: {ex}", LogHelper.LogType.Error);
                }
            }
        }

        /// <summary>
        /// 从文件加载已保存的墨迹
        /// </summary>
        public void LoadSavedStrokes(Presentation presentation)
        {
            if (!IsAutoSaveEnabled || string.IsNullOrEmpty(AutoSaveLocation) || presentation == null) return;

            lock (_lockObject)
            {
                try
                {
                    var presentationId = GeneratePresentationId(presentation);
                    if (_presentationManagers.ContainsKey(presentationId))
                    {
                        _presentationManagers[presentationId].LoadSavedStrokes();
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"从文件加载墨迹失败: {ex}", LogHelper.LogType.Error);
                }
            }
        }

        /// <summary>
        /// 清除指定演示文稿的所有墨迹
        /// </summary>
        public void ClearPresentationStrokes(Presentation presentation)
        {
            if (presentation == null) return;

            lock (_lockObject)
            {
                try
                {
                    var presentationId = GeneratePresentationId(presentation);
                    if (_presentationManagers.ContainsKey(presentationId))
                    {
                        _presentationManagers[presentationId].ClearAllStrokes();
                        LogHelper.WriteLogToFile($"已清除演示文稿墨迹: {presentation.Name}", LogHelper.LogType.Trace);
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"清除演示文稿墨迹失败: {ex}", LogHelper.LogType.Error);
                }
            }
        }

        /// <summary>
        /// 清除所有演示文稿的墨迹
        /// </summary>
        public void ClearAllStrokes()
        {
            lock (_lockObject)
            {
                try
                {
                    foreach (var manager in _presentationManagers.Values)
                    {
                        manager?.ClearAllStrokes();
                    }
                    LogHelper.WriteLogToFile("已清除所有演示文稿墨迹", LogHelper.LogType.Trace);
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"清除所有墨迹失败: {ex}", LogHelper.LogType.Error);
                }
            }
        }

        /// <summary>
        /// 翻页后锁定墨迹写入
        /// </summary>
        public void LockInkForSlide(int slideIndex)
        {
            lock (_lockObject)
            {
                try
                {
                    var manager = GetCurrentManager();
                    if (manager != null)
                    {
                        manager.LockInkForSlide(slideIndex);
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"锁定墨迹写入失败: {ex}", LogHelper.LogType.Error);
                }
            }
        }

        /// <summary>
        /// 检查是否可以写入墨迹
        /// </summary>
        public bool CanWriteInk(int currentSlideIndex)
        {
            lock (_lockObject)
            {
                try
                {
                    var manager = GetCurrentManager();
                    if (manager != null)
                    {
                        return manager.CanWriteInk(currentSlideIndex);
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"检查墨迹写入权限失败: {ex}", LogHelper.LogType.Error);
                }
            }

            return false;
        }

        /// <summary>
        /// 重置当前演示文稿的墨迹锁定状态
        /// </summary>
        public void ResetCurrentPresentationLockState()
        {
            lock (_lockObject)
            {
                try
                {
                    var manager = GetCurrentManager();
                    if (manager != null)
                    {
                        manager.ResetLockState();
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"重置墨迹锁定状态失败: {ex}", LogHelper.LogType.Error);
                }
            }
        }

        /// <summary>
        /// 移除演示文稿管理器
        /// </summary>
        public void RemovePresentation(Presentation presentation)
        {
            if (presentation == null) return;

            lock (_lockObject)
            {
                try
                {
                    var presentationId = GeneratePresentationId(presentation);
                    
                    if (_presentationManagers.ContainsKey(presentationId))
                    {
                        // 保存墨迹到文件
                        _presentationManagers[presentationId].SaveAllStrokesToFile(presentation);
                        
                        // 释放资源
                        _presentationManagers[presentationId].Dispose();
                        _presentationManagers.Remove(presentationId);
                    }

                    if (_presentationInfos.ContainsKey(presentationId))
                    {
                        _presentationInfos.Remove(presentationId);
                    }

                    // 如果移除的是当前活跃的演示文稿，重置活跃ID
                    if (_currentActivePresentationId == presentationId)
                    {
                        _currentActivePresentationId = "";
                    }

                }
                catch (COMException comEx)
                {
                    var hr = (uint)comEx.HResult;
                    if (hr == 0x8001010E || hr == 0x80004005 || hr == 0x800706BA || hr == 0x800706BE || hr == 0x80048010)
                    {
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        /// <summary>
        /// 获取当前管理的演示文稿数量
        /// </summary>
        public int GetPresentationCount()
        {
            lock (_lockObject)
            {
                return _presentationManagers.Count;
            }
        }

        /// <summary>
        /// 获取所有演示文稿信息
        /// </summary>
        public List<PresentationInfo> GetAllPresentationInfos()
        {
            lock (_lockObject)
            {
                return _presentationInfos.Values.ToList();
            }
        }

        /// <summary>
        /// 清理长时间未访问的演示文稿管理器
        /// </summary>
        public void CleanupInactivePresentations(TimeSpan inactiveThreshold)
        {
            lock (_lockObject)
            {
                try
                {
                    var inactiveIds = new List<string>();
                    var cutoffTime = DateTime.Now - inactiveThreshold;

                    foreach (var info in _presentationInfos.Values)
                    {
                        if (info.LastAccessTime < cutoffTime && info.Id != _currentActivePresentationId)
                        {
                            inactiveIds.Add(info.Id);
                        }
                    }

                    foreach (var id in inactiveIds)
                    {
                        if (_presentationManagers.ContainsKey(id))
                        {
                            _presentationManagers[id].Dispose();
                            _presentationManagers.Remove(id);
                        }
                        _presentationInfos.Remove(id);
                        
                        LogHelper.WriteLogToFile($"已清理非活跃演示文稿: {id}", LogHelper.LogType.Trace);
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"清理非活跃演示文稿失败: {ex}", LogHelper.LogType.Error);
                }
            }
        }
        #endregion

        #region Private Methods
        private PPTInkManager GetCurrentManager()
        {
            if (string.IsNullOrEmpty(_currentActivePresentationId) || 
                !_presentationManagers.ContainsKey(_currentActivePresentationId))
            {
                return null;
            }

            return _presentationManagers[_currentActivePresentationId];
        }

        private Presentation GetCurrentActivePresentation()
        {
            try
            {
                // 通过PPTManager获取当前活跃的演示文稿
                return PPTManager?.GetCurrentActivePresentation();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"获取当前活跃演示文稿失败: {ex}", LogHelper.LogType.Error);
                return null;
            }
        }

        private string GeneratePresentationId(Presentation presentation)
        {
            try
            {
                // 检查COM对象是否仍然有效
                if (presentation == null)
                {
                    return $"invalid_{DateTime.Now.Ticks}";
                }

                var presentationPath = presentation.FullName;
                var fileHash = GetFileHash(presentationPath);
                var processId = GetProcessId(presentation);
                return $"{presentation.Name}_{presentation.Slides.Count}_{fileHash}_{processId}";
            }
            catch (COMException comEx)
            {
                var hr = (uint)comEx.HResult;
                if (hr == 0x8001010E || hr == 0x80004005 || hr == 0x800706BA || hr == 0x800706BE || hr == 0x80048010)
                {
                    return $"disconnected_{DateTime.Now.Ticks}";
                }
                return $"error_{DateTime.Now.Ticks}";
            }
            catch (Exception)
            {
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
            catch (Exception)
            {
                // 所有异常都静默处理，避免日志噪音
                return "error";
            }
        }

        private string GetProcessId(Presentation presentation)
        {
            try
            {
                // 尝试获取PowerPoint应用程序的进程ID
                if (presentation.Application != null)
                {
                    // 通过COM对象获取进程信息
                    var hwnd = presentation.Application.HWND;
                    if (hwnd != 0)
                    {
                        return hwnd.ToString();
                    }
                }
                return "unknown";
            }
            catch (COMException comEx)
            {
                // COM对象已失效，这是正常情况，完全静默处理
                var hr = (uint)comEx.HResult;
                if (hr == 0x8001010E || hr == 0x80004005 || hr == 0x800706BA || hr == 0x800706BE || hr == 0x80048010)
                {
                    return "disconnected";
                }
                return "error";
            }
            catch (Exception)
            {
                return "error";
            }
        }
        #endregion

        #region Dispose
        public void Dispose()
        {
            if (!_disposed)
            {
                lock (_lockObject)
                {
                    foreach (var manager in _presentationManagers.Values)
                    {
                        manager?.Dispose();
                    }
                    _presentationManagers.Clear();
                    _presentationInfos.Clear();
                }
                _disposed = true;
            }
        }
        #endregion
    }

    /// <summary>
    /// 演示文稿信息
    /// </summary>
    public class PresentationInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string FullName { get; set; }
        public int SlideCount { get; set; }
        public DateTime CreatedTime { get; set; }
        public DateTime LastAccessTime { get; set; }
    }
}