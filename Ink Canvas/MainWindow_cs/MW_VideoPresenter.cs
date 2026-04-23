using AForge.Imaging;
using AForge.Imaging.Filters;
using AForge.Math.Geometry;
using Ink_Canvas.Helpers;
using Ink_Canvas.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Ink_Canvas
{
    public partial class MainWindow : Window
    {
        private static readonly SolidColorBrush BoothButtonHighlightBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF66CCFF"));
        private bool _boothButtonPressHandlersAttached;

        // 标记：用于在保存/恢复白板内容时排除“展台实时上屏”画面
        private const string VideoPresenterLiveFrameTag = "__VideoPresenterLiveFrame";

        private CameraService _cameraService;
        private readonly object _videoPresenterFrameLock = new object();
        private Bitmap _lastFrame;

        private readonly List<CapturedImage> _capturedPhotos = new List<CapturedImage>();
        private const int MaxCapturedPhotos = 50; // 容量上限：比 UI 显示的 30 项多一些，避免频繁清理

        // 按页绑定：每一页对应一个“实时画面”元素与布局/设备信息
        private readonly Dictionary<int, System.Windows.Controls.Image> _liveFrameImageByPage = new Dictionary<int, System.Windows.Controls.Image>();
        private readonly HashSet<int> _liveEnabledPages = new HashSet<int>();
        private readonly Dictionary<int, int> _cameraIndexByPage = new Dictionary<int, int>();
        private readonly Dictionary<int, (double left, double top, double width)> _liveFrameLayoutByPage =
            new Dictionary<int, (double left, double top, double width)>();

        private DateTime _lastCaptureTime = DateTime.MinValue;
        private const int VideoPresenterCaptureCooldownMs = 1000;

        private const int CorrectedPaperHeight = 600;

        /// <summary>
        /// 切换视频呈现侧边栏的显示状态（显示或隐藏）。
        /// </summary>
        /// <param name="sender">触发事件的源对象。</param>
        /// <param name="e">鼠标按钮事件的参数。</param>
        private void BtnToggleVideoPresenter_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (Settings?.Canvas?.LaunchSeewoVideoShowcaseForWhiteboardBooth == true)
            {
                // 与主窗口「希沃视频展台」入口（BoardLaunchEasiCamera_MouseUp）一致：先走黑板/白板入口逻辑再启动
                ImageBlackboard_MouseUp(null, null);
                SoftwareLauncher.LaunchEasiCamera("希沃视频展台");
                return;
            }

            ToggleVideoPresenterSidebar();
        }

        /// <summary>
        /// 切换视频演示侧栏的显示状态并在显示时初始化相关控件与状态。
        /// </summary>
        /// <remarks>
        /// 当侧栏被显示时：确保摄像头服务已初始化、暂时禁用拍照按钮、刷新可用摄像头列表，并将“照片校正”和当前页面的“上屏（live on canvas）”开关同步为保存的设置或页面状态；
        /// 当侧栏被隐藏时：将其折叠并停止进一步初始化操作。
        /// </remarks>
        private void ToggleVideoPresenterSidebar()
        {
            if (VideoPresenterSidebar == null) return;

            if (VideoPresenterSidebar.Visibility == Visibility.Visible)
            {
                VideoPresenterSidebar.Visibility = Visibility.Collapsed;
                return;
            }

            VideoPresenterSidebar.Visibility = Visibility.Visible;
            EnsureCameraService();
            if (BtnCapturePhoto != null) BtnCapturePhoto.IsEnabled = false;
            RefreshVideoPresenterDeviceList();
            UpdateBoothResolutionTabState();

            if (ToggleBtnPhotoCorrection != null)
            {
                ToggleBtnPhotoCorrection.IsChecked = Settings?.Automation?.IsEnablePhotoCorrection ?? false;
            }

            // 同步“上屏”按钮状态（按页绑定）
            if (BtnToggleVideoPresenterLiveOnCanvas != null)
            {
                BtnToggleVideoPresenterLiveOnCanvas.IsChecked = _liveEnabledPages.Contains(GetCurrentPageIndex());
            }

            if (!_boothButtonPressHandlersAttached)
            {
                AttachBoothButtonPressHandlers();
            }
        }

        private void AttachBoothButtonPressHandlers()
        {
            if (BtnCapturePhoto == null || BtnRotateImage == null) return;
            BtnCapturePhoto.PreviewMouseDown += BoothButton_PreviewMouseDown;
            BtnCapturePhoto.PreviewMouseUp += BoothButton_PreviewMouseUp;
            BtnCapturePhoto.LostMouseCapture += BoothButton_PreviewMouseUp;
            BtnRotateImage.PreviewMouseDown += BoothButton_PreviewMouseDown;
            BtnRotateImage.PreviewMouseUp += BoothButton_PreviewMouseUp;
            BtnRotateImage.LostMouseCapture += BoothButton_PreviewMouseUp;
            _boothButtonPressHandlersAttached = true;
        }

        private void BoothButton_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Control c) ApplyBoothButtonHighlight(c, true);
        }

        private void BoothButton_PreviewMouseUp(object sender, EventArgs e)
        {
            if (sender is Control c) ApplyBoothButtonHighlight(c, false);
        }

        private static void ApplyBoothButtonHighlight(Control control, bool highlight)
        {
            if (control == null) return;
            if (highlight)
            {
                control.Background = BoothButtonHighlightBrush;
                control.BorderBrush = BoothButtonHighlightBrush;
            }
            else
            {
                control.SetResourceReference(Control.BackgroundProperty, "FloatBarBackground");
                control.SetResourceReference(Control.BorderBrushProperty, "FloatBarBorderBrush");
            }
        }

        /// <summary>
        /// 关闭视频呈现侧边栏（将其可见性设为 Collapsed）。
        /// </summary>
        private void BtnCloseVideoPresenter_Click(object sender, RoutedEventArgs e)
        {
            if (VideoPresenterSidebar != null)
            {
                VideoPresenterSidebar.Visibility = Visibility.Collapsed;
            }
        }

        private void CloseVideoPresenterSidebarAndReleaseResources()
        {
            if (VideoPresenterSidebar != null)
            {
                VideoPresenterSidebar.Visibility = Visibility.Collapsed;
            }

            StopVideoPresenterPreviewAndFrameCache(clearPreviewImage: true);
        }

        private void StopVideoPresenterPreviewAndFrameCache(bool clearPreviewImage)
        {
            if (BtnCapturePhoto != null)
            {
                BtnCapturePhoto.IsEnabled = false;
            }

            if (clearPreviewImage && VideoPresenterPreviewImage != null)
            {
                VideoPresenterPreviewImage.Source = null;
            }

            try { _cameraService?.StopPreview(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
            lock (_videoPresenterFrameLock)
            {
                _lastFrame?.Dispose();
                _lastFrame = null;
            }
        }

        /// <summary>
        /// 延迟初始化摄像头服务并订阅其帧和错误事件；如果服务已存在则不做任何操作。
        /// </summary>
        private void EnsureCameraService()
        {
            if (_cameraService != null) return;

            _cameraService = new CameraService();
            _cameraService.FrameReceived += CameraService_FrameReceived;
            _cameraService.ErrorOccurred += CameraService_ErrorOccurred;
            SyncBoothResolutionToCameraService();
        }

        internal void SyncBoothResolutionToCameraService()
        {
            if (_cameraService == null) return;
            _cameraService.ResolutionWidth = BoothResolutionWidth;
            _cameraService.ResolutionHeight = BoothResolutionHeight;
        }

        /// <summary>
        /// 在相机服务发生错误时将错误信息写入错误日志文件。
        /// </summary>
        /// <param name="e">来自相机服务的错误描述，会被写入错误日志。</param>
        private void CameraService_ErrorOccurred(object sender, string e)
        {
            try
            {
                LogHelper.WriteLogToFile($"视频展台摄像头错误: {e}", LogHelper.LogType.Error);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
        }

        /// <summary>
        /// 处理来自摄像头的单帧图像，用于更新预览、缓存最新帧并刷新当前页的实时画面显示。
        /// </summary>
        /// <param name="frame">来自摄像头的位图帧；为 null 时忽略。</param>
        /// <remarks>
        /// 缓存该帧为最新帧，并通过 CameraService 提供的 BitmapSource 直接更新预览与实时上屏
        /// </remarks>
        private void CameraService_FrameReceived(object sender, Bitmap frame)
        {
            if (frame == null) return;

            try
            {
                Bitmap serviceCopy;
                try
                {
                    serviceCopy = (Bitmap)frame.Clone();
                }
                catch
                {
                    // 可能在下一帧到来时被 CameraService 释放，直接忽略这一帧
                    return;
                }

                lock (_videoPresenterFrameLock)
                {
                    _lastFrame?.Dispose();
                    _lastFrame = (Bitmap)serviceCopy.Clone();
                }

                var previewSource = _cameraService?.GetCurrentFrameAsBitmapSource();
                serviceCopy.Dispose();
                if (previewSource == null) return;

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (VideoPresenterPreviewImage != null)
                    {
                        VideoPresenterPreviewImage.Source = previewSource;
                    }

                    if (BtnCapturePhoto != null)
                    {
                        BtnCapturePhoto.IsEnabled = true;
                    }

                    // 实时上屏：刷新当前页的画面元素
                    TryUpdateLiveFrameOnCanvas(previewSource);
                }));
            }
            catch
            {
                // 忽略预览刷新异常
            }
        }

        /// <summary>
        /// 获取当前白板页索引（确保返回值至少为 1）。
        /// </summary>
        /// <returns>当前白板页索引；如果内部索引小于 1，则返回 1。</returns>
        private int GetCurrentPageIndex()
        {
            return Math.Max(1, CurrentWhiteboardIndex);
        }

        /// <summary>
        /// 在当前白板页面（若已启用）将给定预览图像应用到页面上的实时摄像框元素，并确保该元素已添加到画布且可见。
        /// </summary>
        /// <remarks>
        /// 如果当前页面未启用实时显示，或画布/对应图像元素不可用，则函数不执行任何操作。
        /// </remarks>
        private void TryUpdateLiveFrameOnCanvas(ImageSource preview)
        {
            try
            {
                if (preview == null) return;

                int page = GetCurrentPageIndex();
                if (!_liveEnabledPages.Contains(page)) return;
                if (inkCanvas == null) return;
                if (!_liveFrameImageByPage.TryGetValue(page, out var img) || img == null) return;

                if (!inkCanvas.Children.Contains(img))
                {
                    inkCanvas.Children.Add(img);
                }

                img.Source = preview;
                img.Visibility = Visibility.Visible;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
        }

        private const double VideoPresenterLiveFrameScreenRatio = 0.75;

        /// <summary>
        /// 获取或创建并缓存用于指定白板页的实时视频帧 Image 元素。
        /// </summary>
        /// <param name="page">白板页索引（页面编号，用于在每页间区分并缓存元素）。</param>
        /// <returns>返回指定页对应的 Image 元素；若已存在则返回已缓存实例，否则创建新的 Image（根据画布大小设置默认宽高、标记为实时帧并初始化变换与交互绑定）并将其缓存后返回。</returns>
        private System.Windows.Controls.Image EnsureLiveFrameElementForPage(int page)
        {
            if (_liveFrameImageByPage.TryGetValue(page, out var existing) && existing != null) return existing;

            double canvasW = inkCanvas?.ActualWidth ?? 0;
            double canvasH = inkCanvas?.ActualHeight ?? 0;
            double w = canvasW > 10 && canvasH > 10
                ? canvasW * VideoPresenterLiveFrameScreenRatio
                : 520;
            double h = canvasW > 10 && canvasH > 10
                ? canvasH * VideoPresenterLiveFrameScreenRatio
                : 390;

            var img = new System.Windows.Controls.Image
            {
                Tag = VideoPresenterLiveFrameTag,
                Stretch = System.Windows.Media.Stretch.Uniform,
                Width = w,
                Height = h,
                Visibility = Visibility.Visible,
                Opacity = 1.0
            };
            try
            {
                InitializeElementTransform(img);
                BindElementEvents(img);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

            _liveFrameImageByPage[page] = img;
            return img;
        }

        /// <summary>
        /// 将已保存的布局（或默认布局）应用到指定白板页面上的直播帧 Image 元素，设置其位置和尺寸并确保坐标有效。
        /// </summary>
        /// <param name="page">目标白板页面的索引。</param>
        /// <param name="img">要应用布局的 Image 元素；为 null 时不执行任何操作。</param>
        /// <remarks>
        /// 如果存在为该页面保存的布局则使用其宽度和左/上坐标；否则将 Image 调整为画布尺寸的 75% 并居中。最终位置会限制为不小于 0 的坐标，且对无效计算结果使用合理的默认偏移。
        /// </remarks>
        private void ApplyLiveFrameLayoutForPage(int page, System.Windows.Controls.Image img)
        {
            if (img == null) return;

            if (_liveFrameLayoutByPage.TryGetValue(page, out var layout))
            {
                if (!double.IsNaN(layout.width) && layout.width > 10) img.Width = layout.width;
                InkCanvas.SetLeft(img, Math.Max(0, layout.left));
                InkCanvas.SetTop(img, Math.Max(0, layout.top));
                return;
            }

            // 默认尺寸：画布宽高的 75%；位置居中
            double cw = inkCanvas?.ActualWidth ?? 0;
            double ch = inkCanvas?.ActualHeight ?? 0;
            if (cw > 10 && ch > 10)
            {
                img.Width = cw * VideoPresenterLiveFrameScreenRatio;
                img.Height = ch * VideoPresenterLiveFrameScreenRatio;
            }
            double x = (inkCanvas?.ActualWidth ?? 0) / 2 - img.Width / 2;
            double y = (inkCanvas?.ActualHeight ?? 0) / 2 - img.Height / 2;
            if (double.IsNaN(x) || double.IsInfinity(x)) x = 100;
            if (double.IsNaN(y) || double.IsInfinity(y)) y = 100;
            InkCanvas.SetLeft(img, Math.Max(0, x));
            InkCanvas.SetTop(img, Math.Max(0, y));
        }

        /// <summary>
        /// 刷新视频呈现器侧栏中的摄像头设备列表并在界面上显示可选项。
        /// </summary>
        /// <remarks>
        /// 若未检测到摄像头，会在面板中显示提示文本；若存在设备，则为每个设备创建一个用于选择的单选按钮，选择某项会启动对应的摄像头预览。函数在列表生成后会尝试恢复并启动当前页面在 _cameraIndexByPage 中存储的摄像头索引，仅当没有保存的索引时才会选择并启动第一个可用设备。保存的每页选择优先于默认选择第一个设备。
        /// </remarks>
        private void RefreshVideoPresenterDeviceList()
        {
            if (_cameraService == null) return;
            if (CameraDevicesStackPanel == null) return;

            _cameraService.RefreshCameraList();
            CameraDevicesStackPanel.Children.Clear();

            if (_cameraService.AvailableCameras == null || _cameraService.AvailableCameras.Count == 0)
            {
                var tb = new TextBlock
                {
                    Text = "未检测到摄像头设备",
                    FontSize = 12,
                    Margin = new Thickness(5),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                tb.SetResourceReference(TextBlock.ForegroundProperty, "FloatBarForeground");
                CameraDevicesStackPanel.Children.Add(tb);
                return;
            }

            for (int i = 0; i < _cameraService.AvailableCameras.Count; i++)
            {
                int idx = i;
                var dev = _cameraService.AvailableCameras[i];
                var rb = new RadioButton
                {
                    Content = dev.Name,
                    Margin = new Thickness(0, 2, 0, 2),
                    FontSize = 12,
                    Tag = idx,
                };
                rb.SetResourceReference(Control.ForegroundProperty, "FloatBarForeground");
                rb.Checked += (s, e) => StartVideoPresenterPreview(idx);
                CameraDevicesStackPanel.Children.Add(rb);
            }

            // 预选该页已保存的摄像头，否则使用第一个
            if (_cameraService.AvailableCameras.Count > 0)
            {
                int currentPage = GetCurrentPageIndex();
                int cameraToSelect = 0;
                if (_cameraIndexByPage.TryGetValue(currentPage, out int savedIdx) && savedIdx >= 0 && savedIdx < _cameraService.AvailableCameras.Count)
                {
                    cameraToSelect = savedIdx;
                }

                if (cameraToSelect < CameraDevicesStackPanel.Children.Count && CameraDevicesStackPanel.Children[cameraToSelect] is RadioButton rb)
                {
                    rb.IsChecked = true;
                }
                else
                {
                    StartVideoPresenterPreview(cameraToSelect);
                }
            }
        }

        /// <summary>
        /// 为当前白板页开始指定摄像头的预览并保存该页的摄像头选择。
        /// </summary>
        /// <param name="cameraIndex">要启动的摄像头在设备列表中的索引。</param>
        /// <remarks>预览成功时会允许拍照按钮可用。</remarks>
        private void StartVideoPresenterPreview(int cameraIndex)
        {
            try
            {
                EnsureCameraService();
                _cameraIndexByPage[GetCurrentPageIndex()] = cameraIndex;
                if (_cameraService.StartPreview(cameraIndex))
                {
                    if (BtnCapturePhoto != null) BtnCapturePhoto.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"启动视频展台预览失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 在当前白板页面启用“在画布上显示实时视频”功能并将对应的实时画面元素添加到画布上。
        /// </summary>
        /// <remarks>
        /// 确保为当前页面创建并应用已保存的布局，将实时画面 Image 加入 inkCanvas（若尚未存在），并尝试切换编辑工具为选择模式。若侧栏预览已有帧，则立即用该预览刷新画布上的实时画面图像源。
        /// </remarks>
        private void BtnToggleVideoPresenterLiveOnCanvas_Checked(object sender, RoutedEventArgs e)
        {
            ApplyBoothButtonHighlight(BtnToggleVideoPresenterLiveOnCanvas, true);
            int page = GetCurrentPageIndex();
            _liveEnabledPages.Add(page);
            StartVideoPresenterPreviewForCurrentPageIfNeeded();

            var img = EnsureLiveFrameElementForPage(page);
            ApplyLiveFrameLayoutForPage(page, img);

            if (inkCanvas != null && !inkCanvas.Children.Contains(img))
            {
                inkCanvas.Children.Add(img);
            }

            try
            {
                SetCurrentToolMode(InkCanvasEditingMode.Select);
                UpdateCurrentToolMode("select");
                HideSubPanels("select");
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

            // 立即用侧栏预览刷新一次
            if (VideoPresenterPreviewImage?.Source is ImageSource src)
            {
                img.Source = src;
            }
        }

        /// <summary>
        /// 在当前页面禁用画布上的实时视频覆盖并移除其视觉元素。
        /// </summary>
        /// <remarks>
        /// 从记录已启用实时显示的集合中删除当前页面索引，并在存在对应的 Image 元素且已添加到 inkCanvas 时尝试将其移除。
        /// </remarks>
        private void BtnToggleVideoPresenterLiveOnCanvas_Unchecked(object sender, RoutedEventArgs e)
        {
            ApplyBoothButtonHighlight(BtnToggleVideoPresenterLiveOnCanvas, false);
            int page = GetCurrentPageIndex();
            _liveEnabledPages.Remove(page);

            if (_liveFrameImageByPage.TryGetValue(page, out var img) && img != null)
            {
                try
                {
                    if (inkCanvas != null && inkCanvas.Children.Contains(img))
                    {
                        inkCanvas.Children.Remove(img);
                    }
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
            }

            if (_liveEnabledPages.Count == 0)
            {
                StopVideoPresenterPreviewAndFrameCache(clearPreviewImage: false);
            }
        }

        private void StartVideoPresenterPreviewForCurrentPageIfNeeded()
        {
            try
            {
                EnsureCameraService();
                if (_cameraService == null || _cameraService.IsCapturing) return;

                int page = GetCurrentPageIndex();
                int idx = 0;
                if (_cameraIndexByPage.TryGetValue(page, out int savedIdx))
                {
                    idx = savedIdx;
                }

                if (_cameraService.AvailableCameras == null || _cameraService.AvailableCameras.Count == 0)
                {
                    _cameraService.RefreshCameraList();
                }

                if (_cameraService.AvailableCameras == null || _cameraService.AvailableCameras.Count == 0)
                {
                    return;
                }

                idx = Math.Max(0, Math.Min(idx, _cameraService.AvailableCameras.Count - 1));
                _cameraService.StartPreview(idx);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"启动视频展台预览失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 在离开当前白板页之前保存该页实时视频画面在画布上的位置和宽度（按页索引进行存储）。
        /// </summary>
        /// <remarks>
        /// 若画面元素的 Left 或 Top 为 NaN，则按 0 处理；保存的数据格式为 (left, top, width) 到页面布局映射中供后续恢复使用。
        /// </remarks>
        private void VideoPresenter_BeforePageLeave()
        {
            try
            {
                int page = GetCurrentPageIndex();
                if (!_liveFrameImageByPage.TryGetValue(page, out var img) || img == null) return;

                double left = InkCanvas.GetLeft(img);
                double top = InkCanvas.GetTop(img);
                if (double.IsNaN(left)) left = 0;
                if (double.IsNaN(top)) top = 0;

                _liveFrameLayoutByPage[page] = (left, top, img.Width);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
        }

        /// <summary>
        /// 在页面切换后恢复该页的实时画面状态并同步相关设备与 UI 控件状态。
        /// </summary>
        /// <remarks>
        /// 同步“上屏”切换按钮状态；若当前页启用了在画布上显示实时画面，则确保并布局对应的 Image 元素并用当前预览图像填充其 Source；同时恢复该页保存的摄像头索引并启动对应摄像头预览。
        /// </remarks>
        private void VideoPresenter_OnPageChanged()
        {
            try
            {
                int page = GetCurrentPageIndex();

                // 同步“上屏”按钮状态
                if (BtnToggleVideoPresenterLiveOnCanvas != null)
                {
                    BtnToggleVideoPresenterLiveOnCanvas.IsChecked = _liveEnabledPages.Contains(page);
                }

                // 若该页上屏，恢复画面元素（RestoreStrokes 会清空 inkCanvas.Children）
                if (_liveEnabledPages.Contains(page))
                {
                    var img = EnsureLiveFrameElementForPage(page);
                    ApplyLiveFrameLayoutForPage(page, img);
                    if (inkCanvas != null && !inkCanvas.Children.Contains(img))
                    {
                        inkCanvas.Children.Add(img);
                    }

                    if (VideoPresenterPreviewImage?.Source is ImageSource src)
                    {
                        img.Source = src;
                    }
                }

                // 按页摄像头索引：仅在展台侧栏可见时，切页后自动切回该页的摄像头
                if (VideoPresenterSidebar?.Visibility == Visibility.Visible
                    && _cameraIndexByPage.TryGetValue(page, out int idx))
                {
                    EnsureCameraService();
                    _cameraService?.StartPreview(idx);
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
        }

        /// <summary>
        /// 处理“拍照”按钮的点击：捕获当前视频帧并将照片加入捕获列表，随后刷新捕获照片的显示。
        /// </summary>
        /// <remarks>
        /// - 在拍照前会检查并强制执行最小冷却时间，防止短时间内重复拍照。
        /// - 如果用户已启用照片纠正，会尝试检测纸张轮廓并对照片做透视校正再保存。  
        /// - 照片处理在后台线程完成，最终的列表更新和 UI 刷新在 UI 线程上执行。  
        /// - 发生异常时会记录错误日志，不会向上抛出异常。
        /// </remarks>
        private void BtnCapturePhoto_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if ((DateTime.Now - _lastCaptureTime).TotalMilliseconds < VideoPresenterCaptureCooldownMs) return;
                _lastCaptureTime = DateTime.Now;

                Bitmap frame;
                lock (_videoPresenterFrameLock)
                {
                    if (_lastFrame == null) return;
                    frame = (Bitmap)_lastFrame.Clone();
                }

                Task.Run(() =>
                {
                    try
                    {
                        using (frame)
                        {
                            Bitmap toSave = frame;

                            if (Settings?.Automation?.IsEnablePhotoCorrection == true
                                && TryDetectPaperCorners(toSave, out List<AForge.IntPoint> corners))
                            {
                                var corrected = ApplyPerspectiveCorrection(toSave, corners);
                                if (corrected != null) toSave = corrected;
                            }

                            var bmpImage = ConvertBitmapToBitmapImage(toSave);
                            if (!ReferenceEquals(toSave, frame))
                            {
                                toSave.Dispose();
                            }

                            if (bmpImage == null) return;

                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                var ci = new CapturedImage(bmpImage);
                                _capturedPhotos.Insert(0, ci);

                                while (_capturedPhotos.Count > MaxCapturedPhotos)
                                {
                                    _capturedPhotos.RemoveAt(_capturedPhotos.Count - 1);
                                }

                                UpdateCapturedPhotosDisplay();
                            }));
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"视频展台拍照失败: {ex.Message}", LogHelper.LogType.Error);
                    }
                });
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"视频展台拍照失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 将当前相机预览的显示角度顺时针旋转 90°（在四个方向间切换）。
        /// </summary>
        /// <remarks>
        /// 更新内部 CameraService 的旋转状态以切换到下一个方向；在错误发生时会记录日志但不会抛出异常到调用者。
        /// </remarks>
        /// <param name="sender">触发该事件的控件（通常为旋转按钮）。</param>
        /// <param name="e">事件参数。</param>
        private void BtnRotateImage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                EnsureCameraService();
                _cameraService.RotationAngle = (_cameraService.RotationAngle + 1) % 4;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"视频展台旋转失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 在启用照片校正的切换按钮被选中时，将该偏好设置为开启并保存到设置文件。
        /// </summary>
        private void ToggleBtnPhotoCorrection_Checked(object sender, RoutedEventArgs e)
        {
            ApplyBoothButtonHighlight(ToggleBtnPhotoCorrection, true);
            if (Settings?.Automation == null) return;
            Settings.Automation.IsEnablePhotoCorrection = true;
            SaveSettingsToFile();
        }

        /// <summary>
        /// 关闭“相片校正”设置并将变更持久化到设置文件。
        /// </summary>
        private void ToggleBtnPhotoCorrection_Unchecked(object sender, RoutedEventArgs e)
        {
            ApplyBoothButtonHighlight(ToggleBtnPhotoCorrection, false);
            if (Settings?.Automation == null) return;
            Settings.Automation.IsEnablePhotoCorrection = false;
            SaveSettingsToFile();
        }

        /// <summary>
        /// 刷新并在 CapturedPhotosStackPanel 中显示最近捕获的照片缩略图，最多显示 30 张。
        /// </summary>
        /// <remarks>
        /// 如果 CapturedPhotosStackPanel 为 null 则不执行任何操作。该方法会清空面板现有内容，并为每张照片创建一个包含缩略图的按钮；点击按钮会将对应照片插入画布。
        /// </remarks>
        private void UpdateCapturedPhotosDisplay()
        {
            if (CapturedPhotosStackPanel == null) return;

            CapturedPhotosStackPanel.Children.Clear();

            const double PhotoListImageWidth = 310;
            const double PhotoListImageHeight = 180;

            foreach (var photo in _capturedPhotos.Take(30))
            {
                var btn = new Button
                {
                    Margin = new Thickness(0, 0, 0, 6),
                    Padding = new Thickness(0),
                    BorderThickness = new Thickness(0),
                    Background = System.Windows.Media.Brushes.Transparent,
                    Tag = photo
                };
                btn.Click += (s, e) =>
                {
                    if (btn.Tag is CapturedImage p) InsertPhotoToCanvas(p);
                };

                var img = new System.Windows.Controls.Image
                {
                    Source = photo.Thumbnail,
                    Stretch = System.Windows.Media.Stretch.Uniform,
                    Width = PhotoListImageWidth,
                    Height = PhotoListImageHeight
                };
                btn.Content = img;
                CapturedPhotosStackPanel.Children.Add(btn);
            }
        }

        /// <summary>
        /// 将选定的捕获图片作为图像元素插入到画布中央并切换到选择工具模式。
        /// </summary>
        /// <param name="photo">要插入的捕获图片；若为 null 或其 Image 为 null，则不进行任何操作。</param>
        /// <remarks>
        /// 在画布上创建并配置一个 Image 元素（设置 Source、Stretch、默认宽度及位置），初始化其变换与事件绑定，提交插入历史记录，添加到 inkCanvas，并将当前工具切换为“选择”同时隐藏相关子面板。方法内部捕获并记录异常，不会向外抛出。
        /// </remarks>
        private void InsertPhotoToCanvas(CapturedImage photo)
        {
            if (photo?.Image == null) return;

            try
            {
                var img = new System.Windows.Controls.Image
                {
                    Source = photo.Image,
                    Stretch = System.Windows.Media.Stretch.Uniform,
                    Width = 500
                };

                double x = (inkCanvas?.ActualWidth ?? 0) / 2 - img.Width / 2;
                double y = (inkCanvas?.ActualHeight ?? 0) / 2 - 200;
                if (double.IsNaN(x) || double.IsInfinity(x)) x = 100;
                if (double.IsNaN(y) || double.IsInfinity(y)) y = 100;

                InkCanvas.SetLeft(img, Math.Max(0, x));
                InkCanvas.SetTop(img, Math.Max(0, y));
                InitializeElementTransform(img);
                BindElementEvents(img);
                timeMachine.CommitElementInsertHistory(img);

                inkCanvas?.Children.Add(img);

                SetCurrentToolMode(InkCanvasEditingMode.Select);
                UpdateCurrentToolMode("select");
                HideSubPanels("select");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"插入展台照片失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 在离开白板模式时关闭并清理视频呈现器相关的 UI 与运行状态。
        /// </summary>
        /// <remarks>
        /// 隐藏视频呈现侧栏、将“在画布上显示实时帧”开关取消选中、从画布中移除并隐藏所有每页的实时帧图像实例，并尝试停止相机预览。该方法在执行过程中会吞并内部异常以避免抛出至调用方。
        /// </remarks>
        private void VideoPresenter_OnExitWhiteboardMode()
        {
            try
            {
                CloseVideoPresenterSidebarAndReleaseResources();

                if (BtnToggleVideoPresenterLiveOnCanvas != null)
                {
                    BtnToggleVideoPresenterLiveOnCanvas.IsChecked = false;
                }

                if (inkCanvas != null)
                {
                    foreach (var kv in _liveFrameImageByPage.ToList())
                    {
                        var img = kv.Value;
                        if (img == null) continue;
                        try
                        {
                            if (inkCanvas.Children.Contains(img))
                            {
                                inkCanvas.Children.Remove(img);
                            }
                            img.Visibility = Visibility.Collapsed;
                        }
                        catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
                    }
                }

            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
        }

        /// <summary>
        /// 将一个 System.Drawing.Bitmap 转换为可跨线程使用的 WPF BitmapImage。
        /// </summary>
        /// <param name="bitmap">要转换的源位图；若为 <see langword="null"/> 则直接返回 <see langword="null"/>。</param>
        /// <returns>转换得到的 <see cref="BitmapImage"/>；若输入为 <see langword="null"/> 或转换失败则返回 <see langword="null"/>。</returns>
        private static BitmapImage ConvertBitmapToBitmapImage(Bitmap bitmap)
        {
            try
            {
                if (bitmap == null) return null;

                using (var ms = new MemoryStream())
                {
                    bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    ms.Position = 0;
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.StreamSource = ms;
                    bi.EndInit();
                    bi.Freeze();
                    return bi;
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 在给定帧中尝试检测纸张（四边形）角点，并返回按原始帧坐标排列的四个点。
        /// </summary>
        /// <param name="frame">要检测的输入位图帧。</param>
        /// <param name="cornersOut">检测到的四个角点（按顺序：左上、右上、左下、右下），坐标以输入帧的像素空间为准；检测失败时为 null。</param>
        /// <returns><see langword="true"/> 如果成功检测到四个角点并填充 <paramref name="cornersOut"/>，<see langword="false"/> 否则（包括输入为 null 或检测过程中发生错误）。</returns>
        private static bool TryDetectPaperCorners(Bitmap frame, out List<AForge.IntPoint> cornersOut)
        {
            cornersOut = null;
            try
            {
                if (frame == null) return false;

                int targetWidth = 640;
                int ow = frame.Width;
                int oh = frame.Height;
                double scale = 1.0;
                Bitmap work = frame;
                if (ow > targetWidth)
                {
                    int nh = (int)Math.Round(oh * (targetWidth / (double)ow));
                    var resize = new ResizeBilinear(targetWidth, nh);
                    work = resize.Apply(frame);
                    scale = (double)ow / targetWidth;
                }

                var gray = Grayscale.CommonAlgorithms.BT709.Apply(work);
                var blur = new GaussianBlur(3, 3);
                blur.ApplyInPlace(gray);
                var canny = new CannyEdgeDetector();
                canny.ApplyInPlace(gray);
                var dilate = new Dilatation3x3();
                dilate.ApplyInPlace(gray);

                var bc = new BlobCounter
                {
                    FilterBlobs = true,
                    MinHeight = 50,
                    MinWidth = 50,
                    ObjectsOrder = ObjectsOrder.Size
                };
                bc.ProcessImage(gray);
                var blobs = bc.GetObjectsInformation();
                var sc = new SimpleShapeChecker();
                List<AForge.IntPoint> best = null;
                double bestArea = 0;

                foreach (var blob in blobs)
                {
                    var edgePoints = bc.GetBlobsEdgePoints(blob);
                    if (edgePoints == null || edgePoints.Count < 4) continue;
                    if (sc.IsQuadrilateral(edgePoints, out List<AForge.IntPoint> crn))
                    {
                        double area = Math.Abs(PolygonArea(crn));
                        if (area > bestArea)
                        {
                            bestArea = area;
                            best = crn;
                        }
                    }
                }

                if (best != null)
                {
                    var pts = best
                        .Select(p => new AForge.IntPoint((int)Math.Round(p.X * scale), (int)Math.Round(p.Y * scale)))
                        .ToList();
                    pts.Sort((a, b) => a.Y.CompareTo(b.Y));
                    if (pts[0].X > pts[1].X) (pts[0], pts[1]) = (pts[1], pts[0]);
                    if (pts[2].X > pts[3].X) (pts[2], pts[3]) = (pts[3], pts[2]);
                    cornersOut = pts;
                    if (!ReferenceEquals(work, frame)) work.Dispose();
                    gray.Dispose();
                    return true;
                }

                if (!ReferenceEquals(work, frame)) work.Dispose();
                gray.Dispose();
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 将源图像中由四个角点定义的纸张区域进行透视矫正并裁切为目标尺寸的位图，目标高度为 CorrectedPaperHeight，宽度按纸张比例计算。
        /// </summary>
        /// <param name="frame">包含待矫正纸张的源位图。</param>
        /// <param name="corners">纸张在源图像中的四个角点，按顺序提供：左上 (top-left)、右上 (top-right)、左下 (bottom-left)、右下 (bottom-right)。坐标为图像像素坐标系。</param>
        /// <returns>透视矫正并裁切后的位图；在输入无效或矫正失败时返回 <see langword="null"/>。</returns>
        private static Bitmap ApplyPerspectiveCorrection(Bitmap frame, List<AForge.IntPoint> corners)
        {
            try
            {
                if (frame == null || corners == null || corners.Count != 4) return null;
                var tl = corners[0];
                var tr = corners[1];
                var bl = corners[2];
                var br = corners[3];

                double topW = Math.Sqrt((tr.X - tl.X) * (tr.X - tl.X) + (tr.Y - tl.Y) * (tr.Y - tl.Y));
                double bottomW = Math.Sqrt((br.X - bl.X) * (br.X - bl.X) + (br.Y - bl.Y) * (br.Y - bl.Y));
                double leftH = Math.Sqrt((bl.X - tl.X) * (bl.X - tl.X) + (bl.Y - tl.Y) * (bl.Y - tl.Y));
                double rightH = Math.Sqrt((br.X - tr.X) * (br.X - tr.X) + (br.Y - tr.Y) * (br.Y - tr.Y));

                double avgW = (topW + bottomW) / 2.0;
                double avgH = (leftH + rightH) / 2.0;
                if (avgH <= 0) avgH = 1;
                double ratio = avgW / avgH;

                int targetH = CorrectedPaperHeight;
                int targetW = Math.Max(1, (int)Math.Round(targetH * ratio));

                var orderedCorners = new List<AForge.IntPoint> { tl, tr, br, bl };
                var qtf = new QuadrilateralTransformation(orderedCorners, targetW, targetH);
                return qtf.Apply(frame);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 计算由给定顶点按顺序构成的多边形的有向面积（使用高斯面积/鞋带公式）。
        /// </summary>
        /// <param name="pts">按顶点顺序排列的多边形顶点列表（至少应包含三个点以形成多边形）。</param>
        /// <returns>多边形的有向面积；当顶点顺时针时为负值，逆时针为正值；点数少于三时返回 0。</returns>
        private static double PolygonArea(List<AForge.IntPoint> pts)
        {
            int n = pts.Count;
            if (n < 3) return 0;
            long sum = 0;
            for (int i = 0; i < n; i++)
            {
                var p = pts[i];
                var q = pts[(i + 1) % n];
                sum += (long)p.X * q.Y - (long)p.Y * q.X;
            }
            return 0.5 * sum;
        }
    }
}
