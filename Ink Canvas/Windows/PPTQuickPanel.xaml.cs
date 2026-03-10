using Ink_Canvas.Helpers;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SystemEvents = Microsoft.Win32.SystemEvents;

namespace Ink_Canvas.Windows
{
    /// <summary>
    /// PPT侧滑快捷面板
    /// </summary>
    public partial class PPTQuickPanel : UserControl
    {
        // Windows Core Audio API
        [ComImport]
        [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioEndpointVolume
        {
            int NotImpl1();
            int NotImpl2();
            int GetChannelCount(out int channelCount);
            int SetMasterVolumeLevel(float level, Guid eventContext);
            int SetMasterVolumeLevelScalar(float level, Guid eventContext);
            int GetMasterVolumeLevel(out float level);
            int GetMasterVolumeLevelScalar(out float level);
            int SetChannelVolumeLevel(uint channelNumber, float level, Guid eventContext);
            int SetChannelVolumeLevelScalar(uint channelNumber, float level, Guid eventContext);
            int GetChannelVolumeLevel(uint channelNumber, out float level);
            int GetChannelVolumeLevelScalar(uint channelNumber, out float level);
            int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, Guid eventContext);
            int GetMute(out bool mute);
        }

        [ComImport]
        [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object interfacePointer);
            int OpenPropertyStore(int stgmAccess, out IPropertyStore propertyStore);
            int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
            int GetState(out int state);
        }

        [ComImport]
        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            int NotImpl1();
            int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
        }

        [ComImport]
        [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPropertyStore
        {
            int GetCount(out int propertyCount);
            int GetAt(int propertyIndex, out Guid propertyKey);
            int GetValue(ref Guid propertyKey, out object value);
            int SetValue(ref Guid propertyKey, ref object value);
            int Commit();
        }

        private static class MMDeviceEnumeratorFactory
        {
            [ComImport]
            [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
            private class MMDeviceEnumerator
            {
            }

            public static IMMDeviceEnumerator CreateInstance()
            {
                return new MMDeviceEnumerator() as IMMDeviceEnumerator;
            }
        }

        private const int DEVICE_STATE_ACTIVE = 1;
        private const int eRender = 0;
        private const int eConsole = 0;

        private IAudioEndpointVolume _audioEndpointVolume;
        private bool _isExpanded = false;
        private bool _isDragging = false;
        private Point _dragStartPoint;
        private double _collapsedOffset = 200; // 折叠时的偏移量（隐藏内容区域）
        private MainWindow _mainWindow;

        private Dictionary<System.Windows.Controls.Image, int> _pptImages = new Dictionary<System.Windows.Controls.Image, int>();

        private Dictionary<int, List<string>> _pptImagePaths = new Dictionary<int, List<string>>();

        public PPTQuickPanel()
        {
            InitializeComponent();
            InitializeAudio();
            ApplyTheme();

            // 监听主题变化
            SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;

            Loaded += PPTQuickPanel_Loaded;
            Unloaded += PPTQuickPanel_Unloaded;
            IsVisibleChanged += PPTQuickPanel_IsVisibleChanged;
        }

        private void PPTQuickPanel_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (Visibility == Visibility.Visible)
            {
                ApplyTheme();
            }
        }

        private void PPTQuickPanel_Loaded(object sender, RoutedEventArgs e)
        {
            // 初始状态为折叠
            PanelTransform.X = _collapsedOffset;
            UpdateArrowRotation();

            // 获取MainWindow引用
            _mainWindow = Application.Current.MainWindow as MainWindow;

            SubscribeToPPTEvents();
            SubscribeToInkCanvasChildrenChanges();
            Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateVolumeDisplay();
            }), DispatcherPriority.Loaded);
        }

        private void SubscribeToInkCanvasChildrenChanges()
        {
            try
            {
                if (_mainWindow == null) return;

                System.Windows.Controls.InkCanvas inkCanvas = null;
                var inkCanvasField = _mainWindow.GetType().GetField("inkCanvas",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                if (inkCanvasField != null)
                {
                    inkCanvas = inkCanvasField.GetValue(_mainWindow) as System.Windows.Controls.InkCanvas;
                }
                if (inkCanvas == null)
                {
                    var inkCanvasProperty = _mainWindow.GetType().GetProperty("inkCanvas",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                    inkCanvas = inkCanvasProperty?.GetValue(_mainWindow) as System.Windows.Controls.InkCanvas;
                }

                if (inkCanvas != null && inkCanvas.Children is INotifyCollectionChanged notifyCollection)
                {
                    notifyCollection.CollectionChanged += InkCanvasChildren_CollectionChanged;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"订阅inkCanvas.Children变化事件失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void InkCanvasChildren_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            try
            {
                if (e.Action == NotifyCollectionChangedAction.Remove)
                {
                    foreach (var item in e.OldItems)
                    {
                        if (item is System.Windows.Controls.Image image)
                        {
                            RemoveImageFromPPT(image);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理inkCanvas.Children变化失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void RemoveImageFromPPT(System.Windows.Controls.Image image)
        {
            try
            {
                if (image == null) return;

                if (_pptImages.ContainsKey(image))
                {
                    int slideNumber = _pptImages[image];
                    _pptImages.Remove(image);

                    if (_pptImagePaths.ContainsKey(slideNumber))
                    {
                        string imagePath = image.Tag as string;
                        if (!string.IsNullOrEmpty(imagePath) && _pptImagePaths[slideNumber].Contains(imagePath))
                        {
                            _pptImagePaths[slideNumber].Remove(imagePath);

                            if (_pptImagePaths[slideNumber].Count == 0)
                            {
                                _pptImagePaths.Remove(slideNumber);
                                DeletePPTImagePathsFile(slideNumber);
                            }
                            else
                            {
                                SavePPTImagePaths(slideNumber);
                            }

                            LogHelper.WriteLogToFile($"已从PPT页面{slideNumber}移除图片: {imagePath}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"从PPT关联数据中移除图片失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void DeletePPTImagePathsFile(int slideIndex)
        {
            try
            {
                if (slideIndex <= 0) return;

                var folderPath = GetPresentationFolderPath();
                if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
                    return;

                var jsonFilePath = Path.Combine(folderPath, slideIndex.ToString("0000") + ".images.json");
                if (File.Exists(jsonFilePath))
                {
                    File.Delete(jsonFilePath);
                    LogHelper.WriteLogToFile($"已删除第{slideIndex}页图片路径JSON文件: {jsonFilePath}");
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"删除PPT图片路径JSON文件失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void SubscribeToPPTEvents()
        {
            try
            {
                if (_mainWindow == null) return;

                // 获取PPTManager
                var pptManagerProperty = _mainWindow.GetType().GetProperty("PPTManager",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                var pptManager = pptManagerProperty?.GetValue(_mainWindow);

                if (pptManager != null)
                {
                    // 订阅PPT演示文稿打开事件
                    var presentationOpenEvent = pptManager.GetType().GetEvent("PresentationOpen");
                    if (presentationOpenEvent != null)
                    {
                        var openHandler = new Action<object>(OnPPTPresentationOpen);
                        presentationOpenEvent.AddEventHandler(pptManager, openHandler);
                    }

                    // 订阅PPT开始事件
                    var slideShowBeginEvent = pptManager.GetType().GetEvent("SlideShowBegin");
                    if (slideShowBeginEvent != null)
                    {
                        var beginHandler = new Action<object>(OnPPTSlideShowBegin);
                        slideShowBeginEvent.AddEventHandler(pptManager, beginHandler);
                    }

                    // 订阅PPT翻页事件
                    var slideShowNextSlideEvent = pptManager.GetType().GetEvent("SlideShowNextSlide");
                    if (slideShowNextSlideEvent != null)
                    {
                        var handler = new Action<object>(OnPPTSlideChanged);
                        slideShowNextSlideEvent.AddEventHandler(pptManager, handler);
                    }

                    // 订阅PPT结束事件
                    var slideShowEndEvent = pptManager.GetType().GetEvent("SlideShowEnd");
                    if (slideShowEndEvent != null)
                    {
                        var handler = new Action<object>(OnPPTSlideShowEnd);
                        slideShowEndEvent.AddEventHandler(pptManager, handler);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"订阅PPT事件失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void OnPPTPresentationOpen(object presentation)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    // PPT打开时，加载所有图片路径
                    LoadAllPPTImagePaths();
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"处理PPT打开事件失败: {ex.Message}", LogHelper.LogType.Error);
                }
            }), DispatcherPriority.Normal);
        }

        private void OnPPTSlideShowBegin(object window)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(async () =>
            {
                try
                {
                    // PPT开始时，加载所有图片路径并加载当前页面的图片
                    LoadAllPPTImagePaths();

                    if (_mainWindow == null) return;

                    // 获取当前PPT页面编号
                    var pptManagerProperty = _mainWindow.GetType().GetProperty("PPTManager",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                    var pptManager = pptManagerProperty?.GetValue(_mainWindow);

                    if (pptManager != null)
                    {
                        var getCurrentSlideNumberMethod = pptManager.GetType().GetMethod("GetCurrentSlideNumber");
                        int currentSlide = 0;
                        if (getCurrentSlideNumberMethod != null)
                        {
                            var result = getCurrentSlideNumberMethod.Invoke(pptManager, null);
                            if (result != null)
                            {
                                currentSlide = (int)result;
                            }
                        }

                        // 加载当前页面的图片
                        if (currentSlide > 0)
                        {
                            await LoadPPTImages(currentSlide);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"处理PPT开始事件失败: {ex.Message}", LogHelper.LogType.Error);
                }
            }), DispatcherPriority.Normal);
        }

        private void OnPPTSlideChanged(object window)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(async () =>
            {
                try
                {
                    if (_mainWindow == null) return;

                    // 获取当前PPT页面编号
                    var pptManagerProperty = _mainWindow.GetType().GetProperty("PPTManager",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                    var pptManager = pptManagerProperty?.GetValue(_mainWindow);

                    if (pptManager != null)
                    {
                        var getCurrentSlideNumberMethod = pptManager.GetType().GetMethod("GetCurrentSlideNumber");
                        int currentSlide = 0;
                        if (getCurrentSlideNumberMethod != null)
                        {
                            var result = getCurrentSlideNumberMethod.Invoke(pptManager, null);
                            if (result != null)
                            {
                                currentSlide = (int)result;
                            }
                        }

                        // 更新图片可见性
                        UpdatePPTImagesVisibility(currentSlide);

                        // 加载当前页面的图片（如果还没有加载）
                        if (currentSlide > 0 && (!_pptImagePaths.ContainsKey(currentSlide) || _pptImagePaths[currentSlide].Count == 0))
                        {
                            // 先加载图片路径
                            var imagePaths = LoadPPTImagePaths(currentSlide);
                            if (imagePaths != null && imagePaths.Count > 0)
                            {
                                _pptImagePaths[currentSlide] = imagePaths;

                                // 加载图片
                                await LoadPPTImages(currentSlide);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"处理PPT翻页事件失败: {ex.Message}", LogHelper.LogType.Error);
                }
            }), DispatcherPriority.Normal);
        }

        private void OnPPTSlideShowEnd(object presentation)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    // PPT退出时隐藏所有图片
                    HideAllPPTImages();
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"处理PPT结束事件失败: {ex.Message}", LogHelper.LogType.Error);
                }
            }), DispatcherPriority.Normal);
        }

        private void UpdatePPTImagesVisibility(int currentSlide)
        {
            try
            {
                foreach (var kvp in _pptImages)
                {
                    var image = kvp.Key;
                    var slideNumber = kvp.Value;

                    // 如果图片在当前页面，显示；否则隐藏
                    image.Visibility = (slideNumber == currentSlide) ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"更新PPT图片可见性失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void HideAllPPTImages()
        {
            try
            {
                foreach (var image in _pptImages.Keys)
                {
                    image.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"隐藏所有PPT图片失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void PPTQuickPanel_Unloaded(object sender, RoutedEventArgs e)
        {
            SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
            UnsubscribeFromInkCanvasChildrenChanges();
        }

        private void UnsubscribeFromInkCanvasChildrenChanges()
        {
            try
            {
                if (_mainWindow == null) return;

                System.Windows.Controls.InkCanvas inkCanvas = null;
                var inkCanvasField = _mainWindow.GetType().GetField("inkCanvas",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                if (inkCanvasField != null)
                {
                    inkCanvas = inkCanvasField.GetValue(_mainWindow) as System.Windows.Controls.InkCanvas;
                }
                if (inkCanvas == null)
                {
                    var inkCanvasProperty = _mainWindow.GetType().GetProperty("inkCanvas",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                    inkCanvas = inkCanvasProperty?.GetValue(_mainWindow) as System.Windows.Controls.InkCanvas;
                }

                if (inkCanvas != null && inkCanvas.Children is INotifyCollectionChanged notifyCollection)
                {
                    notifyCollection.CollectionChanged -= InkCanvasChildren_CollectionChanged;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"取消订阅inkCanvas.Children变化事件失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                ApplyTheme();
            }), DispatcherPriority.Normal);
        }

        #region 音频控制

        private void InitializeAudio()
        {
            try
            {
                var deviceEnumerator = MMDeviceEnumeratorFactory.CreateInstance();
                IMMDevice device;
                deviceEnumerator.GetDefaultAudioEndpoint(eRender, eConsole, out device);

                Guid IID_IAudioEndpointVolume = new Guid("5CDF2C82-841E-4546-9722-0CF74078229A");
                object interfacePointer;
                device.Activate(ref IID_IAudioEndpointVolume, 0, IntPtr.Zero, out interfacePointer);
                _audioEndpointVolume = interfacePointer as IAudioEndpointVolume;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"初始化音频控制失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private float GetVolume()
        {
            if (_audioEndpointVolume == null) return 0.5f;
            try
            {
                float level;
                _audioEndpointVolume.GetMasterVolumeLevelScalar(out level);
                return level;
            }
            catch
            {
                return 0.5f;
            }
        }

        private void SetVolume(float volume)
        {
            if (_audioEndpointVolume == null) return;
            try
            {
                volume = Math.Max(0f, Math.Min(1f, volume));
                _audioEndpointVolume.SetMasterVolumeLevelScalar(volume, Guid.Empty);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"设置音量失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private bool GetMute()
        {
            if (_audioEndpointVolume == null) return false;
            try
            {
                bool mute;
                _audioEndpointVolume.GetMute(out mute);
                return mute;
            }
            catch
            {
                return false;
            }
        }

        private void SetMute(bool mute)
        {
            if (_audioEndpointVolume == null) return;
            try
            {
                _audioEndpointVolume.SetMute(mute, Guid.Empty);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"设置静音失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void UpdateVolumeDisplay()
        {
            if (_audioEndpointVolume == null) return;

            try
            {
                float volume = GetVolume();
                bool isMuted = GetMute();

                // 更新滑块值（不触发事件）
                VolumeSlider.ValueChanged -= VolumeSlider_ValueChanged;
                VolumeSlider.Value = volume * 100;
                VolumeSlider.ValueChanged += VolumeSlider_ValueChanged;

                // 更新文本显示
                VolumeValueText.Text = $"{(int)(volume * 100)}%";

                // 更新图标
                UpdateVolumeIcon(isMuted, volume);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"更新音量显示失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void UpdateVolumeIcon(bool isMuted, float volume)
        {
            // 清除描边
            VolumeIconPath.Stroke = null;
            VolumeIconPath.StrokeThickness = 0;
            VolumeIconPath2.Stroke = null;
            VolumeIconPath2.StrokeThickness = 0;

            // 默认隐藏第二个Path
            VolumeIconPath2.Visibility = Visibility.Collapsed;

            // 静音或音量为0%时显示静音图标
            if (isMuted || volume <= 0f)
            {
                // 静音图标：扬声器 + X
                // 扬声器部分
                var speakerGeometry = Geometry.Parse("M 7,1.00772 C 6.70313,1.00381 6.42188,1.13272 6.23048,1.35928 L 3,4.99991 H 2 C 0.906251,4.99991 0,5.84366 0,6.99991 V 8.99991 C 0,10.0898 0.910157,10.9999 2,10.9999 H 3 L 6.23048,14.6405 C 6.44141,14.8944 6.72266,15.0038 7,14.9999 V 1.00772 Z");
                // X部分
                var xGeometry = Geometry.Parse("M 10,5.00012 C 9.73441,5.00012 9.4805,5.10559 9.293,5.29309 C 8.90237,5.68372 8.90237,6.31653 9.293,6.70715 L 10.586,8.00013 L 9.293,9.2931 C 8.90237,9.68372 8.90237,10.3165 9.293,10.7072 C 9.68362,11.0978 10.3164,11.0978 10.7071,10.7072 L 12,9.41419 L 13.293,10.7072 C 13.6836,11.0978 14.3164,11.0978 14.7071,10.7072 C 15.0977,10.3165 15.0977,9.68372 14.7071,9.2931 L 13.4141,8.00013 L 14.7071,6.70715 C 15.0977,6.31653 15.0977,5.68372 14.7071,5.29309 C 14.5196,5.10559 14.2657,5.00012 14,5.00012 C 13.7344,5.00012 13.4805,5.10559 13.293,5.29309 L 12,6.58606 L 10.7071,5.29309 C 10.5196,5.10559 10.2657,5.00012 10,5.00012 Z");

                var group = new GeometryGroup();
                group.Children.Add(speakerGeometry);
                group.Children.Add(xGeometry);
                VolumeIconPath.Data = group;
                VolumeIconPath2.Visibility = Visibility.Collapsed;
            }
            else if (volume >= 0.5f)
            {
                // 音量>=50%：扬声器 + 两条声波线
                // 扬声器部分
                var speakerGeometry = Geometry.Parse("M 7,0 C 6.70313,0 6.42188,0.125086 6.23048,0.35165 L 3,3.99228 H 2 C 0.906251,3.99228 0,4.83603 0,5.99228 V 7.99228 C 0,9.08213 0.910157,9.99228 2,9.99228 H 3 L 6.23048,13.6329 C 6.44141,13.8868 6.72266,13.9962 7,13.9923 V 0 Z");
                // 第一条声波线
                var wave1Geometry = Geometry.Parse("M 13.461,0.961025 C 13.2695,0.957119 13.0742,1.01571 12.9024,1.12899 C 12.4453,1.44149 12.3242,2.06259 12.6328,2.51962 C 14.457,5.22666 14.457,8.75791 12.6328,11.4649 C 12.3242,11.922 12.4453,12.5431 12.9024,12.8556 C 13.3594,13.1642 13.9805,13.0431 14.293,12.586 C 15.4297,10.8946 16,8.94541 16,6.99228 C 16,5.03915 15.4297,3.08993 14.293,1.39853 C 14.0977,1.11337 13.7813,0.961025 13.461,0.961025 Z");
                // 第二条声波线
                var wave2Geometry = Geometry.Parse("M 10.0391,2.98056 C 9.81642,2.97275 9.58595,3.03915 9.39454,3.18368 C 9.13282,3.3829 9.00001,3.68368 9.00001,3.98837 V 4.04697 C 9.01173,4.23837 9.07423,4.42197 9.19923,4.58212 C 10.2734,6.01181 10.2734,7.97275 9.19923,9.39853 C 9.07423,9.5626 9.01173,9.74619 9.00001,9.93369 V 9.99619 C 9.00001,10.3009 9.13282,10.6017 9.39454,10.8009 C 9.83595,11.1329 10.4609,11.0431 10.793,10.6017 C 11.5977,9.53525 12,8.26572 12,6.99228 C 12,5.71884 11.5977,4.44931 10.793,3.379 C 10.6094,3.1329 10.3281,2.99618 10.0391,2.98056 Z");

                var group = new GeometryGroup();
                group.Children.Add(speakerGeometry);
                group.Children.Add(wave1Geometry);
                group.Children.Add(wave2Geometry);
                VolumeIconPath.Data = group;
                VolumeIconPath2.Visibility = Visibility.Collapsed;
            }
            else
            {
                // 音量<50%：扬声器 + 一条实线声波线
                // 扬声器部分
                var speakerGeometry = Geometry.Parse("M 7,1.00759 C 6.70313,1.00369 6.42188,1.13259 6.23048,1.35916 L 3,4.99979 H 2 C 0.906251,4.99979 0,5.84354 0,6.99979 V 8.99979 C 0,10.0896 0.910157,10.9998 2,10.9998 H 3 L 6.23048,14.6404 C 6.44141,14.8943 6.72266,15.0037 7,14.9998 V 1.00759 Z");
                // 声波线（实线，不透明度100%）
                var wave1Geometry = Geometry.Parse("M 10.0391,3.98807 C 9.81642,3.98025 9.58595,4.04666 9.39454,4.19119 C 9.13282,4.39041 9.00001,4.69119 9.00001,4.99588 V 5.06229 C 9.01173,5.24979 9.07813,5.43338 9.19923,5.58963 C 10.2734,7.01932 10.2734,8.98026 9.19923,10.406 C 9.07813,10.5662 9.01173,10.7498 9.00001,10.9373 V 11.0037 C 9.00001,11.3084 9.13282,11.6092 9.39454,11.8084 C 9.83595,12.1404 10.4609,12.0506 10.793,11.6092 C 11.5977,10.5428 12,9.27323 12,7.99979 C 12,6.72635 11.5977,5.45682 10.793,4.3865 C 10.6094,4.14041 10.3281,4.00369 10.0391,3.98807 Z");

                // 主Path：扬声器 + 声波线
                var group = new GeometryGroup();
                group.Children.Add(speakerGeometry);
                group.Children.Add(wave1Geometry);
                VolumeIconPath.Data = group;

                // 隐藏第二个Path
                VolumeIconPath2.Visibility = Visibility.Collapsed;
            }
        }

        #endregion

        #region 展开/折叠动画

        private void ExpandPanel()
        {
            if (_isExpanded) return;

            _isExpanded = true;
            UpdateArrowRotation();

            var animation = (Storyboard)Resources["ExpandAnimation"];
            var doubleAnimation = animation.Children[0] as DoubleAnimation;
            doubleAnimation.From = PanelTransform.X;
            doubleAnimation.To = 0;
            animation.Begin();
        }

        private void CollapsePanel()
        {
            if (!_isExpanded) return;

            _isExpanded = false;
            UpdateArrowRotation();

            var animation = (Storyboard)Resources["CollapseAnimation"];
            var doubleAnimation = animation.Children[0] as DoubleAnimation;
            doubleAnimation.From = PanelTransform.X;
            doubleAnimation.To = _collapsedOffset;
            animation.Begin();
        }

        private void UpdateArrowRotation()
        {
            if (_isExpanded)
            {
                // 展开时箭头指向左（折叠）
                ArrowRotateTransform.Angle = 0;
            }
            else
            {
                // 折叠时箭头指向右（展开）
                ArrowRotateTransform.Angle = 180;
            }
        }

        #endregion

        #region 箭头按钮事件

        private void ArrowButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (_isExpanded)
            {
                CollapsePanel();
            }
            else
            {
                ExpandPanel();
            }
        }

        private void ArrowButton_MouseEnter(object sender, MouseEventArgs e)
        {
            // 根据当前主题设置悬停颜色
            bool isDark = ArrowButtonBackgroundBrush.Color.R < 128;
            if (isDark)
            {
                ArrowButtonBackgroundBrush.Color = Color.FromArgb(230, 32, 32, 32);
            }
            else
            {
                ArrowButtonBackgroundBrush.Color = Color.FromArgb(230, 255, 255, 255);
            }
        }

        private void ArrowButton_MouseLeave(object sender, MouseEventArgs e)
        {
            // 恢复主题颜色
            ApplyTheme();
        }

        #endregion

        #region 拖动手势

        private void ContentBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is Slider) return; // 如果点击的是滑块，不处理拖动

            _isDragging = true;
            _dragStartPoint = e.GetPosition(MainCanvas);
            ContentBorder.CaptureMouse();
            e.Handled = true;
        }

        private void ContentBorder_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging) return;

            Point currentPoint = e.GetPosition(MainCanvas);
            double deltaX = currentPoint.X - _dragStartPoint.X;

            // 计算新位置
            double newX = PanelTransform.X + deltaX;

            // 限制拖动范围
            newX = Math.Max(0, Math.Min(_collapsedOffset, newX));

            PanelTransform.X = newX;
            _dragStartPoint = currentPoint;
        }

        private void ContentBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDragging) return;

            _isDragging = false;
            ContentBorder.ReleaseMouseCapture();

            // 判断是否超过一半
            if (PanelTransform.X < _collapsedOffset / 2)
            {
                ExpandPanel();
            }
            else
            {
                CollapsePanel();
            }
        }

        private void ContentBorder_TouchDown(object sender, TouchEventArgs e)
        {
            if (e.OriginalSource is Slider) return;

            _isDragging = true;
            _dragStartPoint = e.GetTouchPoint(MainCanvas).Position;
            e.Handled = true;
        }

        private void ContentBorder_TouchMove(object sender, TouchEventArgs e)
        {
            if (!_isDragging) return;

            Point currentPoint = e.GetTouchPoint(MainCanvas).Position;
            double deltaX = currentPoint.X - _dragStartPoint.X;

            double newX = PanelTransform.X + deltaX;
            newX = Math.Max(0, Math.Min(_collapsedOffset, newX));

            PanelTransform.X = newX;
            _dragStartPoint = currentPoint;
        }

        private void ContentBorder_TouchUp(object sender, TouchEventArgs e)
        {
            if (!_isDragging) return;

            _isDragging = false;

            if (PanelTransform.X < _collapsedOffset / 2)
            {
                ExpandPanel();
            }
            else
            {
                CollapsePanel();
            }
        }

        #endregion

        #region 音量控制事件

        private void VolumeMuteButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                bool currentMute = GetMute();
                float volume = GetVolume();
                SetMute(!currentMute);
                UpdateVolumeIcon(!currentMute, volume);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"切换静音失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded) return;

            try
            {
                float volume = (float)(e.NewValue / 100.0);
                SetVolume(volume);

                // 更新文本显示
                VolumeValueText.Text = $"{(int)e.NewValue}%";

                // 如果音量大于0，取消静音
                if (e.NewValue > 0)
                {
                    bool isMuted = GetMute();
                    if (isMuted)
                    {
                        SetMute(false);
                    }
                }

                // 更新图标（根据当前音量和静音状态）
                bool currentMute = GetMute();
                UpdateVolumeIcon(currentMute, volume);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"音量滑块值改变处理失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void VolumeSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // 确保精度为1%
            double value = VolumeSlider.Value;
            VolumeSlider.Value = Math.Round(value);
        }

        private void VolumeSlider_ManipulationCompleted(object sender, ManipulationCompletedEventArgs e)
        {
            // 触摸操作完成时，确保精度为1%
            double value = VolumeSlider.Value;
            VolumeSlider.Value = Math.Round(value);
        }

        #endregion

        #region 图片插入

        private async void InsertImageSelectFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (_mainWindow == null) return;

            try
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.gif"
                };

                if (dialog.ShowDialog() == true)
                {
                    string filePath = dialog.FileName;

                    var createImageMethod = _mainWindow.GetType().GetMethod("CreateAndCompressImageAsync",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (createImageMethod != null)
                    {
                        var imageTask = createImageMethod.Invoke(_mainWindow, new object[] { filePath }) as System.Threading.Tasks.Task<System.Windows.Controls.Image>;
                        if (imageTask != null)
                        {
                            var image = await imageTask;
                            if (image != null)
                            {
                                image.Tag = filePath;
                                await InsertImageToMainWindow(image, filePath);
                            }
                        }
                    }
                    else
                    {
                        LogHelper.WriteLogToFile("无法找到CreateAndCompressImageAsync方法", LogHelper.LogType.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"插入图片失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private async void InsertImageScreenshotButton_Click(object sender, RoutedEventArgs e)
        {
            if (_mainWindow == null) return;

            try
            {
                var captureScreenshotMethod = _mainWindow.GetType().GetMethod("CaptureScreenshotAndInsert",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (captureScreenshotMethod != null)
                {
                    var task = captureScreenshotMethod.Invoke(_mainWindow, null) as System.Threading.Tasks.Task;
                    if (task != null)
                    {
                        await task;
                        await SaveScreenshotToPPT();
                    }
                }
                else
                {
                    LogHelper.WriteLogToFile("无法找到CaptureScreenshotAndInsert方法", LogHelper.LogType.Warning);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"截图插入失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private async System.Threading.Tasks.Task SaveScreenshotToPPT()
        {
            try
            {
                if (_mainWindow == null) return;

                System.Windows.Controls.InkCanvas inkCanvas = null;
                var inkCanvasField = _mainWindow.GetType().GetField("inkCanvas",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                if (inkCanvasField != null)
                {
                    inkCanvas = inkCanvasField.GetValue(_mainWindow) as System.Windows.Controls.InkCanvas;
                }
                if (inkCanvas == null)
                {
                    var inkCanvasProperty = _mainWindow.GetType().GetProperty("inkCanvas",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                    inkCanvas = inkCanvasProperty?.GetValue(_mainWindow) as System.Windows.Controls.InkCanvas;
                }

                if (inkCanvas == null) return;

                System.Windows.Controls.Image lastScreenshot = null;
                foreach (System.Windows.Controls.Image img in inkCanvas.Children.OfType<System.Windows.Controls.Image>())
                {
                    if (img.Name != null && (img.Name.StartsWith("screenshot_") || img.Name.StartsWith("camera_")) && img.Tag == null)
                    {
                        lastScreenshot = img;
                    }
                }

                if (lastScreenshot == null) return;

                string screenshotFilePath = await SaveScreenshotToFile(lastScreenshot);

                if (string.IsNullOrEmpty(screenshotFilePath))
                {
                    LogHelper.WriteLogToFile("保存截图文件失败", LogHelper.LogType.Warning);
                    return;
                }

                lastScreenshot.Tag = screenshotFilePath;
                await ManageScreenshotInPPT(lastScreenshot, screenshotFilePath);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"保存截图到PPT失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private async System.Threading.Tasks.Task<string> SaveScreenshotToFile(System.Windows.Controls.Image image)
        {
            try
            {
                if (image?.Source == null) return null;

                string savePath = null;
                var settingsProperty = _mainWindow.GetType().GetProperty("Settings");
                if (settingsProperty != null)
                {
                    var settings = settingsProperty.GetValue(_mainWindow);
                    if (settings != null)
                    {
                        var autoSavedStrokesLocationProperty = settings.GetType().GetProperty("Automation");
                        if (autoSavedStrokesLocationProperty != null)
                        {
                            var automation = autoSavedStrokesLocationProperty.GetValue(settings);
                            if (automation != null)
                            {
                                var locationProperty = automation.GetType().GetProperty("AutoSavedStrokesLocation");
                                if (locationProperty != null)
                                {
                                    var location = locationProperty.GetValue(automation) as string;
                                    if (!string.IsNullOrEmpty(location))
                                    {
                                        savePath = Path.Combine(location, "File Dependency");
                                    }
                                }
                            }
                        }
                    }
                }

                if (string.IsNullOrEmpty(savePath))
                {
                    LogHelper.WriteLogToFile("无法获取AutoSavedStrokesLocation", LogHelper.LogType.Warning);
                    return null;
                }

                if (!Directory.Exists(savePath))
                {
                    Directory.CreateDirectory(savePath);
                }

                string timestamp = image.Name;
                string filePath = Path.Combine(savePath, timestamp + ".png");

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (image.Source is BitmapSource bitmapSource)
                    {
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bitmapSource));

                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            encoder.Save(fileStream);
                        }
                    }
                });

                return filePath;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"保存截图到文件失败: {ex.Message}", LogHelper.LogType.Error);
                return null;
            }
        }

        private async System.Threading.Tasks.Task ManageScreenshotInPPT(System.Windows.Controls.Image image, string filePath)
        {
            if (_mainWindow == null || image == null || string.IsNullOrEmpty(filePath)) return;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    int currentSlideNumber = 0;
                    try
                    {
                        var pptManagerProperty = _mainWindow.GetType().GetProperty("PPTManager",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                        var pptManager = pptManagerProperty?.GetValue(_mainWindow);

                        if (pptManager != null)
                        {
                            var isInSlideShowProperty = pptManager.GetType().GetProperty("IsInSlideShow");
                            bool isInSlideShow = false;
                            if (isInSlideShowProperty != null)
                            {
                                var result = isInSlideShowProperty.GetValue(pptManager);
                                if (result != null)
                                {
                                    isInSlideShow = (bool)result;
                                }
                            }

                            if (isInSlideShow)
                            {
                                var getCurrentSlideNumberMethod = pptManager.GetType().GetMethod("GetCurrentSlideNumber");
                                if (getCurrentSlideNumberMethod != null)
                                {
                                    var result = getCurrentSlideNumberMethod.Invoke(pptManager, null);
                                    if (result != null)
                                    {
                                        currentSlideNumber = (int)result;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"获取当前PPT页面编号失败: {ex.Message}", LogHelper.LogType.Warning);
                    }

                    if (currentSlideNumber > 0 && !string.IsNullOrEmpty(filePath))
                    {
                        _pptImages[image] = currentSlideNumber;

                        if (!_pptImagePaths.ContainsKey(currentSlideNumber))
                        {
                            _pptImagePaths[currentSlideNumber] = new List<string>();
                        }
                        _pptImagePaths[currentSlideNumber].Add(filePath);
                        SavePPTImagePaths(currentSlideNumber);

                        LogHelper.WriteLogToFile($"截图已关联到PPT页面{currentSlideNumber}: {filePath}");
                    }
                    else if (currentSlideNumber > 0)
                    {
                        _pptImages[image] = currentSlideNumber;
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"管理截图在PPT中的关联失败: {ex.Message}", LogHelper.LogType.Error);
                }
            }, DispatcherPriority.Normal);
        }

        private async System.Threading.Tasks.Task InsertImageToMainWindow(System.Windows.Controls.Image image, string originalFilePath = null, bool saveToJson = true)
        {
            if (_mainWindow == null || image == null) return;

            // 确保在UI线程上执行
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    // 生成唯一名称
                    string timestamp = "img_" + DateTime.Now.ToString("yyyyMMdd_HH_mm_ss_fff");
                    image.Name = timestamp;

                    // 设置图片属性
                    image.IsHitTestVisible = true;
                    image.Focusable = false;

                    System.Windows.Controls.InkCanvas inkCanvas = null;
                    var inkCanvasField = _mainWindow.GetType().GetField("inkCanvas",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                    if (inkCanvasField != null)
                    {
                        inkCanvas = inkCanvasField.GetValue(_mainWindow) as System.Windows.Controls.InkCanvas;
                    }

                    if (inkCanvas == null)
                    {
                        var inkCanvasProperty = _mainWindow.GetType().GetProperty("inkCanvas",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                        if (inkCanvasProperty != null)
                        {
                            inkCanvas = inkCanvasProperty.GetValue(_mainWindow) as System.Windows.Controls.InkCanvas;
                        }
                    }

                    if (inkCanvas == null)
                    {
                        LogHelper.WriteLogToFile("无法获取inkCanvas", LogHelper.LogType.Error);
                        return;
                    }

                    // 初始化InkCanvas选择设置
                    var initializeInkCanvasSelectionSettingsMethod = _mainWindow.GetType().GetMethod("InitializeInkCanvasSelectionSettings",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    initializeInkCanvasSelectionSettingsMethod?.Invoke(_mainWindow, null);

                    // 获取当前PPT页面编号（如果在PPT模式下）
                    int currentSlideNumber = 0;
                    try
                    {
                        var pptManagerProperty = _mainWindow.GetType().GetProperty("PPTManager",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                        var pptManager = pptManagerProperty?.GetValue(_mainWindow);

                        if (pptManager != null)
                        {
                            var isInSlideShowProperty = pptManager.GetType().GetProperty("IsInSlideShow");
                            bool isInSlideShow = false;
                            if (isInSlideShowProperty != null)
                            {
                                var result = isInSlideShowProperty.GetValue(pptManager);
                                if (result != null)
                                {
                                    isInSlideShow = (bool)result;
                                }
                            }

                            if (isInSlideShow)
                            {
                                var getCurrentSlideNumberMethod = pptManager.GetType().GetMethod("GetCurrentSlideNumber");
                                if (getCurrentSlideNumberMethod != null)
                                {
                                    var result = getCurrentSlideNumberMethod.Invoke(pptManager, null);
                                    if (result != null)
                                    {
                                        currentSlideNumber = (int)result;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"获取当前PPT页面编号失败: {ex.Message}", LogHelper.LogType.Warning);
                    }

                    // 如果在PPT模式下，记录图片和页面编号的关联，并保存图片路径
                    if (currentSlideNumber > 0 && !string.IsNullOrEmpty(originalFilePath) && saveToJson)
                    {
                        _pptImages[image] = currentSlideNumber;

                        // 添加到页面图片路径列表
                        if (!_pptImagePaths.ContainsKey(currentSlideNumber))
                        {
                            _pptImagePaths[currentSlideNumber] = new List<string>();
                        }
                        _pptImagePaths[currentSlideNumber].Add(originalFilePath);

                        // 保存图片路径到JSON文件
                        SavePPTImagePaths(currentSlideNumber);
                    }
                    else if (currentSlideNumber > 0)
                    {
                        // 即使不保存到JSON，也要记录图片和页面编号的关联（用于翻页显示/隐藏）
                        _pptImages[image] = currentSlideNumber;
                    }

                    // 先添加到画布（与MainWindow的实现保持一致）
                    inkCanvas.Children.Add(image);

                    // 等待图片加载完成后再进行后续处理
                    image.Loaded += (s, args) =>
                    {
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                // 初始化TransformGroup
                                var initializeTransformMethod = _mainWindow.GetType().GetMethod("InitializeElementTransform",
                                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                initializeTransformMethod?.Invoke(_mainWindow, new object[] { image });

                                // 居中缩放
                                var centerMethod = _mainWindow.GetType().GetMethod("CenterAndScaleElement",
                                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                centerMethod?.Invoke(_mainWindow, new object[] { image });

                                // 绑定事件处理器
                                var bindEventsMethod = _mainWindow.GetType().GetMethod("BindElementEvents",
                                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                bindEventsMethod?.Invoke(_mainWindow, new object[] { image });

                                LogHelper.WriteLogToFile($"图片插入完成: {image.Name}, PPT页面: {currentSlideNumber}");
                            }
                            catch (Exception ex)
                            {
                                LogHelper.WriteLogToFile($"图片加载后处理失败: {ex.Message}", LogHelper.LogType.Error);
                            }
                        }), DispatcherPriority.Loaded);
                    };

                    // 提交历史记录
                    var timeMachineProperty = _mainWindow.GetType().GetProperty("timeMachine",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var timeMachine = timeMachineProperty?.GetValue(_mainWindow);
                    if (timeMachine != null)
                    {
                        var commitMethod = timeMachine.GetType().GetMethod("CommitElementInsertHistory");
                        commitMethod?.Invoke(timeMachine, new object[] { image });
                    }

                    // 切换到选择模式
                    var setModeMethod = _mainWindow.GetType().GetMethod("SetCurrentToolMode",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    setModeMethod?.Invoke(_mainWindow, new object[] { System.Windows.Controls.InkCanvasEditingMode.Select });

                    var updateModeMethod = _mainWindow.GetType().GetMethod("UpdateCurrentToolMode",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    updateModeMethod?.Invoke(_mainWindow, new object[] { "select" });

                    var hidePanelsMethod = _mainWindow.GetType().GetMethod("HideSubPanels",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    hidePanelsMethod?.Invoke(_mainWindow, new object[] { "select" });
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"插入图片到MainWindow失败: {ex.Message}", LogHelper.LogType.Error);
                }
            }, DispatcherPriority.Normal);
        }

        #endregion

        #region PPT图片保存和加载

        /// <summary>
        /// 获取PPT演示文稿的文件夹路径
        /// </summary>
        private string GetPresentationFolderPath()
        {
            try
            {
                if (_mainWindow == null) return null;

                // 获取PPTInkManager
                var singlePPTInkManagerField = _mainWindow.GetType().GetField("_singlePPTInkManager",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var singlePPTInkManager = singlePPTInkManagerField?.GetValue(_mainWindow);

                if (singlePPTInkManager != null)
                {
                    // 使用反射获取AutoSaveLocation
                    var autoSaveLocationProperty = singlePPTInkManager.GetType().GetProperty("AutoSaveLocation");
                    var autoSaveLocation = autoSaveLocationProperty?.GetValue(singlePPTInkManager) as string;

                    if (!string.IsNullOrEmpty(autoSaveLocation))
                    {
                        // 获取PPTManager以获取当前演示文稿信息
                        var pptManagerProperty = _mainWindow.GetType().GetProperty("PPTManager",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                        var pptManager = pptManagerProperty?.GetValue(_mainWindow);

                        if (pptManager != null)
                        {
                            // 尝试获取当前演示文稿，使用重试机制
                            Microsoft.Office.Interop.PowerPoint.Presentation presentation = null;

                            // 首先尝试使用 CurrentPresentation 属性（可能是缓存的）
                            try
                            {
                                var currentPresentationProperty = pptManager.GetType().GetProperty("CurrentPresentation",
                                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                                presentation = currentPresentationProperty?.GetValue(pptManager) as Microsoft.Office.Interop.PowerPoint.Presentation;
                            }
                            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

                            // 如果 CurrentPresentation 不可用，尝试 GetCurrentActivePresentation
                            if (presentation == null)
                            {
                                try
                                {
                                    var getCurrentActivePresentationMethod = pptManager.GetType().GetMethod("GetCurrentActivePresentation");
                                    presentation = getCurrentActivePresentationMethod?.Invoke(pptManager, null) as Microsoft.Office.Interop.PowerPoint.Presentation;
                                }
                                catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
                            }

                            if (presentation != null)
                            {
                                try
                                {
                                    // 立即检查COM对象是否仍然有效
                                    if (!System.Runtime.InteropServices.Marshal.IsComObject(presentation))
                                    {
                                        return null;
                                    }

                                    // 尝试访问对象以验证其有效性
                                    try
                                    {
                                        var _ = System.Runtime.InteropServices.Marshal.GetIUnknownForObject(presentation);
                                        System.Runtime.InteropServices.Marshal.Release(_);
                                    }
                                    catch (System.Runtime.InteropServices.InvalidComObjectException)
                                    {
                                        // COM对象已失效，静默返回
                                        return null;
                                    }

                                    // 立即生成演示文稿ID（在对象失效前）
                                    string presentationId = GeneratePresentationId(presentation);
                                    if (string.IsNullOrEmpty(presentationId) || presentationId.StartsWith("unknown_") || presentationId.StartsWith("invalid_") || presentationId.StartsWith("com_error_"))
                                    {
                                        // 生成ID失败，返回null而不是抛出异常
                                        return null;
                                    }

                                    return Path.Combine(autoSaveLocation, "Auto Saved - Presentations", presentationId);
                                }
                                catch (System.Runtime.InteropServices.InvalidComObjectException)
                                {
                                    // COM对象已失效，静默返回（不记录错误日志）
                                    return null;
                                }
                                catch (System.Runtime.InteropServices.COMException)
                                {
                                    // COM异常，对象可能已失效，静默返回（不记录错误日志）
                                    return null;
                                }
                                catch (Exception ex)
                                {
                                    // 其他异常，只记录非COM相关异常
                                    if (!(ex is System.Runtime.InteropServices.InvalidComObjectException) &&
                                        !(ex is System.Runtime.InteropServices.COMException))
                                    {
                                        LogHelper.WriteLogToFile($"获取PPT文件夹路径时发生非COM异常: {ex.Message}", LogHelper.LogType.Warning);
                                    }
                                    return null;
                                }
                            }
                        }
                    }
                }
            }
            catch (System.Runtime.InteropServices.InvalidComObjectException)
            {
                // COM对象已失效，静默返回（不记录错误日志）
                return null;
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                // COM异常，对象可能已失效，静默返回（不记录错误日志）
                return null;
            }
            catch (Exception ex)
            {
                // 只记录非COM相关异常
                if (!(ex is System.Runtime.InteropServices.InvalidComObjectException) &&
                    !(ex is System.Runtime.InteropServices.COMException))
                {
                    LogHelper.WriteLogToFile($"获取PPT文件夹路径失败: {ex.Message}", LogHelper.LogType.Error);
                }
            }

            return null;
        }

        /// <summary>
        /// 生成演示文稿ID（与PPTInkManager一致）
        /// </summary>
        private string GeneratePresentationId(Microsoft.Office.Interop.PowerPoint.Presentation presentation)
        {
            if (presentation == null)
            {
                return $"unknown_{DateTime.Now.Ticks}";
            }

            try
            {
                // 检查COM对象是否仍然有效
                if (!System.Runtime.InteropServices.Marshal.IsComObject(presentation))
                {
                    return $"unknown_{DateTime.Now.Ticks}";
                }

                // 验证COM对象有效性
                try
                {
                    var _ = System.Runtime.InteropServices.Marshal.GetIUnknownForObject(presentation);
                    System.Runtime.InteropServices.Marshal.Release(_);
                }
                catch (System.Runtime.InteropServices.InvalidComObjectException)
                {
                    // COM对象已失效
                    return $"unknown_{DateTime.Now.Ticks}";
                }

                // 逐个访问属性，每个都进行异常处理
                string presentationName = null;
                string presentationPath = null;
                int slidesCount = 0;

                try
                {
                    presentationName = presentation.Name;
                }
                catch (System.Runtime.InteropServices.InvalidComObjectException)
                {
                    return $"unknown_{DateTime.Now.Ticks}";
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                    return $"unknown_{DateTime.Now.Ticks}";
                }

                try
                {
                    presentationPath = presentation.FullName;
                }
                catch (System.Runtime.InteropServices.InvalidComObjectException)
                {
                    return $"unknown_{DateTime.Now.Ticks}";
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                    return $"unknown_{DateTime.Now.Ticks}";
                }

                try
                {
                    slidesCount = presentation.Slides.Count;
                }
                catch (System.Runtime.InteropServices.InvalidComObjectException)
                {
                    return $"unknown_{DateTime.Now.Ticks}";
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                    return $"unknown_{DateTime.Now.Ticks}";
                }

                var fileHash = GetFileHash(presentationPath);
                return $"{presentationName}_{slidesCount}_{fileHash}";
            }
            catch (System.Runtime.InteropServices.InvalidComObjectException)
            {
                // COM对象已失效
                return $"unknown_{DateTime.Now.Ticks}";
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                // COM异常，对象可能已失效
                return $"unknown_{DateTime.Now.Ticks}";
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"生成演示文稿ID失败: {ex}", LogHelper.LogType.Error);
                return $"unknown_{DateTime.Now.Ticks}";
            }
        }

        /// <summary>
        /// 计算文件哈希值
        /// </summary>
        private string GetFileHash(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath)) return "unknown";

                using (var md5 = System.Security.Cryptography.MD5.Create())
                {
                    byte[] hashBytes = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(filePath));
                    return BitConverter.ToString(hashBytes).Replace("-", "").Substring(0, 8);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"计算文件哈希值失败: {ex}", LogHelper.LogType.Error);
                return "error";
            }
        }

        /// <summary>
        /// 保存指定页面的图片路径到JSON文件
        /// </summary>
        private void SavePPTImagePaths(int slideIndex)
        {
            try
            {
                if (slideIndex <= 0 || !_pptImagePaths.ContainsKey(slideIndex)) return;

                var folderPath = GetPresentationFolderPath();
                if (string.IsNullOrEmpty(folderPath)) return;

                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                var jsonFilePath = Path.Combine(folderPath, slideIndex.ToString("0000") + ".images.json");
                var imagePaths = _pptImagePaths[slideIndex];

                string json = JsonConvert.SerializeObject(imagePaths, Formatting.Indented);
                File.WriteAllText(jsonFilePath, json);

                LogHelper.WriteLogToFile($"已保存第{slideIndex}页图片路径到JSON: {jsonFilePath}");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"保存PPT图片路径失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 从JSON文件加载指定页面的图片路径
        /// </summary>
        private List<string> LoadPPTImagePaths(int slideIndex)
        {
            try
            {
                if (slideIndex <= 0) return new List<string>();

                var folderPath = GetPresentationFolderPath();
                if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
                    return new List<string>();

                var jsonFilePath = Path.Combine(folderPath, slideIndex.ToString("0000") + ".images.json");
                if (!File.Exists(jsonFilePath))
                    return new List<string>();

                string json = File.ReadAllText(jsonFilePath);
                var imagePaths = JsonConvert.DeserializeObject<List<string>>(json);

                return imagePaths ?? new List<string>();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"加载PPT图片路径失败: {ex.Message}", LogHelper.LogType.Error);
                return new List<string>();
            }
        }

        /// <summary>
        /// 加载指定页面的图片
        /// </summary>
        private async System.Threading.Tasks.Task LoadPPTImages(int slideIndex)
        {
            try
            {
                if (_mainWindow == null || slideIndex <= 0) return;

                var imagePaths = LoadPPTImagePaths(slideIndex);
                if (imagePaths == null || imagePaths.Count == 0) return;

                // 获取inkCanvas，检查是否已有图片
                System.Windows.Controls.InkCanvas inkCanvas = null;
                var inkCanvasField = _mainWindow.GetType().GetField("inkCanvas",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                if (inkCanvasField != null)
                {
                    inkCanvas = inkCanvasField.GetValue(_mainWindow) as System.Windows.Controls.InkCanvas;
                }
                if (inkCanvas == null)
                {
                    var inkCanvasProperty = _mainWindow.GetType().GetProperty("inkCanvas",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                    inkCanvas = inkCanvasProperty?.GetValue(_mainWindow) as System.Windows.Controls.InkCanvas;
                }

                if (inkCanvas == null) return;

                // 检查已存在的图片路径（通过Tag）
                var existingImagePaths = new HashSet<string>();
                foreach (System.Windows.Controls.Image existingImage in inkCanvas.Children.OfType<System.Windows.Controls.Image>())
                {
                    if (existingImage.Tag is string tagPath && !string.IsNullOrEmpty(tagPath))
                    {
                        existingImagePaths.Add(tagPath);
                    }
                }

                // 使用反射调用MainWindow的CreateAndCompressImageAsync方法
                var createImageMethod = _mainWindow.GetType().GetMethod("CreateAndCompressImageAsync",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (createImageMethod == null)
                {
                    LogHelper.WriteLogToFile("无法找到CreateAndCompressImageAsync方法", LogHelper.LogType.Warning);
                    return;
                }

                foreach (var imagePath in imagePaths)
                {
                    try
                    {
                        // 如果图片已存在，跳过
                        if (existingImagePaths.Contains(imagePath))
                        {
                            continue;
                        }

                        if (!File.Exists(imagePath))
                        {
                            LogHelper.WriteLogToFile($"图片文件不存在: {imagePath}", LogHelper.LogType.Warning);
                            continue;
                        }

                        var imageTask = createImageMethod.Invoke(_mainWindow, new object[] { imagePath }) as System.Threading.Tasks.Task<System.Windows.Controls.Image>;
                        if (imageTask != null)
                        {
                            var image = await imageTask;
                            if (image != null)
                            {
                                // 保存原始文件路径到Tag
                                image.Tag = imagePath;

                                // 插入图片（不保存路径，因为已经存在）
                                await InsertImageToMainWindow(image, imagePath, false);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"加载图片失败: {imagePath}, 错误: {ex.Message}", LogHelper.LogType.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"加载PPT图片失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 加载所有页面的图片路径（在PPT打开时调用）
        /// </summary>
        public void LoadAllPPTImagePaths()
        {
            try
            {
                if (_mainWindow == null) return;

                var folderPath = GetPresentationFolderPath();
                if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
                    return;

                // 清空现有数据
                _pptImagePaths.Clear();

                // 查找所有图片JSON文件
                var jsonFiles = Directory.GetFiles(folderPath, "*.images.json");
                foreach (var jsonFile in jsonFiles)
                {
                    try
                    {
                        var fileName = Path.GetFileNameWithoutExtension(jsonFile);
                        fileName = fileName.Replace(".images", ""); // 移除.images后缀

                        if (int.TryParse(fileName, out int slideIndex) && slideIndex > 0)
                        {
                            var imagePaths = LoadPPTImagePaths(slideIndex);
                            if (imagePaths != null && imagePaths.Count > 0)
                            {
                                _pptImagePaths[slideIndex] = imagePaths;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"加载图片路径文件失败: {jsonFile}, 错误: {ex.Message}", LogHelper.LogType.Error);
                    }
                }

                LogHelper.WriteLogToFile($"已加载{_pptImagePaths.Count}个页面的图片路径");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"加载所有PPT图片路径失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        #endregion

        #region 主题适配

        private void ApplyTheme()
        {
            try
            {
                if (MainWindow.Settings != null)
                {
                    ApplyTheme(MainWindow.Settings);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"应用PPT快捷面板主题失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void ApplyTheme(Settings settings)
        {
            try
            {
                bool isDarkTheme = false;

                if (settings.Appearance.Theme == 0) // 浅色主题
                {
                    isDarkTheme = false;
                }
                else if (settings.Appearance.Theme == 1) // 深色主题
                {
                    isDarkTheme = true;
                }
                else // 跟随系统主题
                {
                    bool isSystemLight = IsSystemThemeLight();
                    isDarkTheme = !isSystemLight;
                }

                if (isDarkTheme)
                {
                    // 深色主题：使用80%不透明度的深色背景
                    ArrowButtonBackgroundBrush.Color = Color.FromArgb(204, 32, 32, 32); // #CC202020
                    ContentBackgroundBrush.Color = Color.FromArgb(204, 32, 32, 32); // #CC202020
                    ArrowPathFillBrush.Color = Colors.White;
                    VolumeIconFillBrush.Color = Colors.White;
                    VolumeIconFillBrush2.Color = Colors.White;
                    VolumeValueForegroundBrush.Color = Color.FromArgb(200, 255, 255, 255);
                    MagnifierTitleForegroundBrush.Color = Colors.White;
                    MagnifierDescForegroundBrush.Color = Color.FromArgb(200, 255, 255, 255);
                    Separator1BackgroundBrush.Color = Color.FromArgb(128, 255, 255, 255);
                }
                else
                {
                    // 浅色主题：使用80%不透明度的白色背景
                    ArrowButtonBackgroundBrush.Color = Color.FromArgb(204, 255, 255, 255); // #CCFFFFFF
                    ContentBackgroundBrush.Color = Color.FromArgb(204, 255, 255, 255); // #CCFFFFFF
                    ArrowPathFillBrush.Color = Colors.Black;
                    VolumeIconFillBrush.Color = Colors.Black;
                    VolumeIconFillBrush2.Color = Colors.Black;
                    VolumeValueForegroundBrush.Color = Color.FromArgb(128, 0, 0, 0);
                    MagnifierTitleForegroundBrush.Color = Colors.Black;
                    MagnifierDescForegroundBrush.Color = Color.FromArgb(128, 0, 0, 0);
                    Separator1BackgroundBrush.Color = Color.FromArgb(255, 224, 224, 224);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"应用PPT快捷面板主题失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private bool IsSystemThemeLight()
        {
            var light = false;
            try
            {
                var registryKey = Microsoft.Win32.Registry.CurrentUser;
                var themeKey = registryKey.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (themeKey != null)
                {
                    var value = themeKey.GetValue("AppsUseLightTheme");
                    if (value != null)
                    {
                        light = (int)value == 1;
                    }
                    themeKey.Close();
                }
            }
            catch
            {
                // 如果读取注册表失败，默认为浅色主题
                light = true;
            }
            return light;
        }

        #endregion

        #region 公开方法

        /// <summary>
        /// 设置面板的可见性（仅在PPT模式下显示）
        /// </summary>
        public void UpdateVisibility(bool isInPPTMode)
        {
            Visibility = isInPPTMode ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion
    }
}

