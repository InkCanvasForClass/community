using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Ink;
using System.Windows.Threading;
using Microsoft.Office.Interop.PowerPoint;

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
        private bool _disposed = false;
        
        // 墨迹锁定机制，防止翻页时的墨迹冲突
        private DateTime _inkLockUntil = DateTime.MinValue;
        private int _lockedSlideIndex = -1;
        private const int InkLockMilliseconds = 500;
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
                    // 生成演示文稿唯一标识符
                    _currentPresentationId = GeneratePresentationId(presentation);
                    
                    // 重新初始化内存流数组
                    var slideCount = presentation.Slides.Count;
                    _memoryStreams = new MemoryStream[slideCount + 2];
                    
                    // 如果启用自动保存，尝试加载已保存的墨迹
                    if (IsAutoSaveEnabled && !string.IsNullOrEmpty(AutoSaveLocation))
                    {
                        LoadSavedStrokes();
                    }
                    
                    LogHelper.WriteLogToFile($"已初始化演示文稿墨迹管理: {presentation.Name}, 幻灯片数量: {slideCount}", LogHelper.LogType.Trace);
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
                        LogHelper.WriteLogToFile($"墨迹写入被锁定，当前页:{slideIndex}，锁定页:{_lockedSlideIndex}", LogHelper.LogType.Warning);
                        return;
                    }

                    if (slideIndex < _memoryStreams.Length)
                    {
                        var ms = new MemoryStream();
                        strokes.Save(ms);
                        ms.Position = 0;
                        
                        // 释放旧的内存流
                        _memoryStreams[slideIndex]?.Dispose();
                        _memoryStreams[slideIndex] = ms;
                        
                        LogHelper.WriteLogToFile($"已保存第{slideIndex}页墨迹，大小: {ms.Length} bytes", LogHelper.LogType.Trace);
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"保存第{slideIndex}页墨迹失败: {ex}", LogHelper.LogType.Error);
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
                        LogHelper.WriteLogToFile($"已加载第{slideIndex}页墨迹，笔画数量: {strokes.Count}", LogHelper.LogType.Trace);
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
                    // 如果有当前墨迹，先保存
                    if (currentStrokes != null && currentStrokes.Count > 0)
                    {
                        SaveCurrentSlideStrokes(_lockedSlideIndex > 0 ? _lockedSlideIndex : slideIndex, currentStrokes);
                    }

                    // 设置墨迹锁定
                    LockInkForSlide(slideIndex);

                    // 加载新页面的墨迹
                    return LoadSlideStrokes(slideIndex);
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
        public void SaveAllStrokesToFile(Presentation presentation)
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
                        File.WriteAllText(Path.Combine(folderPath, "Position"), _lockedSlideIndex.ToString());
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"保存位置信息失败: {ex}", LogHelper.LogType.Error);
                    }

                    // 保存所有页面的墨迹
                    int savedCount = 0;
                    for (int i = 1; i <= presentation.Slides.Count && i < _memoryStreams.Length; i++)
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
                                    
                                    LogHelper.WriteLogToFile($"已保存第{i}页墨迹，大小: {byteLength} bytes", LogHelper.LogType.Trace);
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

                    LogHelper.WriteLogToFile($"已保存{savedCount}页墨迹到文件", LogHelper.LogType.Event);
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

                    LogHelper.WriteLogToFile($"已从文件加载{loadedCount}页墨迹", LogHelper.LogType.Event);
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
                    for (int i = 0; i < _memoryStreams.Length; i++)
                    {
                        _memoryStreams[i]?.Dispose();
                        _memoryStreams[i] = null;
                    }
                    
                    CurrentStrokes.Clear();
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
            if (DateTime.Now < _inkLockUntil) return false;
            if (currentSlideIndex != _lockedSlideIndex && _lockedSlideIndex > 0) return false;
            return true;
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
