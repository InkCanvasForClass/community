using OpenCvSharp;
using OpenCvSharp.Extensions;
using Point = OpenCvSharp.Point;
using Size = OpenCvSharp.Size;
using Point2f = OpenCvSharp.Point2f;
using DirectShowLib;
using Ink_Canvas.Helpers;
using Ink_Canvas.Models;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Win32;
using Windows.Win32.Graphics.Gdi;
using WPFMediaKit.DirectShow.Controls;
using WPFMediaKit.DirectShow.MediaPlayers;
using WpfMediaKitMediaState = WPFMediaKit.DirectShow.MediaPlayers.MediaState;

namespace Ink_Canvas
{
    public partial class MainWindow : Ink_Canvas.Helpers.PerformanceTransparentWin
    {
        // 标记：用于在保存/恢复白板内容时排除“展台实时上屏”画面
        private const string VideoPresenterLiveFrameTag = "__VideoPresenterLiveFrame";

        private ICameraService _cameraService;
        private readonly object _videoPresenterFrameLock = new object();
        private Bitmap _lastFrame;

        // 视频展台特殊模式：开启后整个白板进入深色背景模式，预览铺满画布
        private bool _isVideoPresenterSpecialMode;
        // 视频展台虚拟分页：当前所在的页。-1=直播页，0..N-1=_capturedPhotos 中的照片索引
        private int _boothCurrentPhotoIndex = -1;
        private bool _isBoothCameraComboBoxUpdating;
        // 亮度滑块程序化赋值保护，避免回写设置/触发硬件写入的反馈循环
        private bool _isBoothBrightnessSliderUpdating;
        // 摄像头属性设置保存防抖：拖动滑块时 ValueChanged 高频触发，
        // SaveSettingsToFile 是同步序列化+写盘的阻塞操作，每次都写会卡 UI。
        // 用 DispatcherTimer debounce：拖动停止后 400ms 才写一次。
        private DispatcherTimer _boothPropsSaveDebounceTimer;
        // 进入特殊模式前 inkCanvas 的 EditingMode，退出时恢复
        private InkCanvasEditingMode _inkEditingModeBeforeSpecialMode = InkCanvasEditingMode.Ink;
        // 特殊模式 + 非笔模式下，触摸期间临时切到 None 避免 InkCanvas 内部框选；手指抬起后恢复
        // （不能改用 e.Handled=true 抑制 PreviewTouchDown，否则 WPF 不会把触摸提升为 Manipulation，
        //  导致 VideoPresenterSpecialMode_ManipulationDelta 收不到拖动/缩放事件）
        private InkCanvasEditingMode? _boothTouchSavedInkEditingMode;
        // 鼠标拖动相关：触摸通过 Manipulation 处理，但鼠标事件不会提升为 Manipulation，
        // 需要单独处理鼠标拖动来移动摄像头预览画面
        private bool _isBoothMouseDragging;
        private System.Windows.Point _boothMouseDragStartOrigin;
        private double _boothMouseDragStartTranslateX;
        private double _boothMouseDragStartTranslateY;
        // 上一帧的 translate，用于计算鼠标移动增量（同步墨迹平移）
        private double _boothMouseLastTranslateX;
        private double _boothMouseLastTranslateY;
        // 特殊模式下预览图像的缩放比例和位移
        private double _boothPreviewScale = 1.0;
        private double _boothPreviewTranslateX = 0;
        private double _boothPreviewTranslateY = 0;
        // 全屏预览：WriteableBitmap 复用后已不再需要节流字段
        // （OnFrameArrived 直接 WritePixels 更新内容，Image.Source 始终指向同一个 WriteableBitmap，
        //  WPF 合成器通过 AddDirtyRect 自动重绘，不会重新分配 GPU 纹理，不再有 DUCE.Channel.SyncFlush 堆积 OOM 风险）

        private readonly List<CapturedImage> _capturedPhotos = new List<CapturedImage>();
        private const int MaxCapturedPhotos = 50; // 容量上限：比 UI 显示的 30 项多一些，避免频繁清理

        // 直播页墨迹快照：与每张照片的 CapturedImage.Strokes 对应，
        // 切换页码时把当前 inkCanvas.Strokes 保存到当前槽位，再从目标槽位恢复。
        // 直播页的墨迹槽位独立存放（不与任何照片绑定），用户在直播页画的批注不会因切到照片页而丢失。
        private System.Windows.Ink.StrokeCollection _liveStrokesSnapshot = new System.Windows.Ink.StrokeCollection();

        // 照片预览页：把 FillImage 的 LayoutTransform/RenderTransform 替换为 VideoCaptureElement 的同名实例，
        // 让照片预览完全复用实时画面的变换管线（旋转/缩放/移动走同一条代码路径）。
        // _frozenFrameOriginalRenderTransform 保存照片 Image 原始的 RenderTransform，返回直播页时恢复。
        private System.Windows.Media.TransformGroup _frozenFrameOriginalRenderTransform = null;

        // 按页绑定：每一页对应一个“实时画面”元素与布局/设备信息
        private readonly Dictionary<int, System.Windows.Controls.Image> _liveFrameImageByPage = new Dictionary<int, System.Windows.Controls.Image>();
        private readonly HashSet<int> _liveEnabledPages = new HashSet<int>();
        private readonly Dictionary<int, int> _cameraIndexByPage = new Dictionary<int, int>();
        private readonly Dictionary<int, (double left, double top, double width)> _liveFrameLayoutByPage =
            new Dictionary<int, (double left, double top, double width)>();

        // 旋转基准：保存首次旋转前的画布快照，下次旋转用 M_baseline⁻¹ · M_target 直接重放，
        // 避免每次旋转都叠加 fit 缩放导致墨迹持续缩小。
        private System.Windows.Ink.StrokeCollection _rotationBaselineStrokes;
        private int _rotationBaselineAngle;
        // 旋转过程中（程序在变换墨迹）设为 true，避免 StrokesChanged 把基准当作用户编辑重置。
        private bool _isApplyingRotationToStrokes;

        private DateTime _lastCaptureTime = DateTime.MinValue;
        private const int VideoPresenterCaptureCooldownMs = 1000;

        // A4 纸实时识别框：定时检测间隔与矫正框动画时长对齐到 200ms，
        // 动画能完整跑完不被打断，避免拖影感。检测仍由 Interlocked 防重入保证不会堆积。
        private DispatcherTimer _paperDetectTimer;
        private const int PaperDetectIntervalMs = 200;
        // 上一次实时检测到的角点（已映射到 overlay 坐标），null 表示未检测到
        private System.Windows.Point[] _lastOverlayCorners;
        // 实时检测任务防重入标志（上一次后台检测未完成时不启动新的）
        private int _paperDetectRunning;

        // 矫正框移动动画：检测到新角点时，从上一帧位置平滑过渡到新位置，避免跳变。
        // 动画期间 _lastOverlayCorners 实时记录"当前显示的中间位置"，新目标到来时以此为新起点，自然衔接。
        private DispatcherTimer _overlayAnimTimer;
        private System.Windows.Point[] _animFromCorners;
        private System.Windows.Point[] _animToCorners;
        private DateTime _animStartTime;
        // 动画时长与抓帧间隔对齐到 200ms：每次新角点到来时上一次动画已刚好跑完，
        // 不会被频繁打断，框平滑过渡且能跟上纸张移动。
        private const int OverlayAnimDurationMs = 200;
        // 复用元素：动画过程中不 Clear+重建，直接更新 Points/位置，避免闪烁
        private System.Windows.Shapes.Polygon _overlayPolygon;
        private System.Windows.Shapes.Ellipse[] _overlayEllipses;
        // 诊断日志：记录新角点到来间隔，用于验证实际帧间隔与定时器间隔是否吻合
        private DateTime? _lastOverlayDrawTime;
        // 诊断日志：动画完成(正常结束)与被打断重启的计数，便于观察动画是否总在被截断
        private int _overlayAnimCompletedCount;
        private int _overlayAnimInterruptedCount;

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

        public void ToggleVideoPresenterSidebarPublic()
        {
            if (Settings?.Canvas?.LaunchSeewoVideoShowcaseForWhiteboardBooth == true)
            {
                ImageBlackboard_MouseUp(null, null);
                SoftwareLauncher.LaunchEasiCamera("希沃视频展台");
                return;
            }

            ToggleVideoPresenterSidebar();
        }

        /// <summary>
        /// 切换视频演示菜单的显示状态并在显示时初始化相关控件与状态。
        /// </summary>
        /// <remarks>
        /// 当菜单被显示时：确保摄像头服务已初始化、暂时禁用拍照按钮、刷新可用摄像头列表，并将"照片校正"开关同步为保存的设置；
        /// 当菜单被隐藏时：将其关闭并停止进一步初始化操作。
        /// 弹出 BoothPopupContent 菜单（VideoPresenterSidebar 已移除）。
        /// </remarks>
        private void ToggleVideoPresenterSidebar()
        {
            if (BoothPopup == null) return;

            // 打开 BoothPopup 前同步加速模式 ComboBox 选中项。
            // WireUpBoothPopupContentEvents 在 LoadSettings 之前调用，无法读到用户保存的值，
            // 必须在此处（LoadSettings 已完成）同步，避免 ComboBox 始终显示 CPU。
            SyncPhotoCorrectionAccelerationComboBox();
            // 同步亮度滑块：从 Settings 读值 + 按摄像头支持情况启用/禁用
            SyncBoothBrightnessSlider();

            if (BoothPopup.IsOpen)
            {
                // 菜单可见时点击视频展台按钮 = 仅关闭菜单（与右上角 X 一致），
                // 不退出视频展台模式。完全退出由菜单内"关闭"按钮（BtnExitVideoPresenter_Click）负责。
                AnimationsHelper.HidePopupWithSlideAndFade(BoothPopup);
                return;
            }

            // 菜单不可见：两种情况
            //  1. 完全没进入特殊模式（首次打开） -> 进入特殊模式 + 启动预览
            //  2. 已在特殊模式但菜单被关闭按钮折叠 -> 只展开菜单，不重启预览（避免设备抖动）
            if (_isVideoPresenterSpecialMode)
            {
                AnimationsHelper.ShowPopupWithSlideAndFade(BoothPopup);
                _popupManager?.BringToFront(BoothPopup);
                return;
            }

            AnimationsHelper.ShowPopupWithSlideAndFade(BoothPopup);
            _popupManager?.BringToFront(BoothPopup);
            EnsureCameraService();
            if (BtnCapturePhoto != null) BtnCapturePhoto.IsEnabled = false;

            // 先进入特殊模式（设置 _isVideoPresenterSpecialMode = true 并切到 Select），
            // 再刷新设备列表/启动预览 —— 否则 StartVideoPresenterPreview 会以为非特殊模式
            // 而走 _cameraService.StartPreviewAsync 路径，与 VideoCaptureElement 抢占摄像头。
            EnterVideoPresenterSpecialMode();

            RefreshVideoPresenterDeviceList();
            // ComboBox 会在 StartVideoPresenterPreview 完成后被填充
            RefreshBoothResolutionComboBox();

            if (ToggleBtnPhotoCorrection != null)
            {
                ToggleBtnPhotoCorrection.IsChecked = Settings?.Automation?.IsEnablePhotoCorrection ?? false;
            }
        }

        /// <summary>
        /// 视频展台虚拟分页页码显示覆盖。
        /// 在特殊模式下把白板页码显示从正常的"当前页/总页数"改为虚拟分页：
        ///   进入特殊模式(无照片)：0/0
        ///   拍照后(直播页)：0/1
        ///   切换到照片页：1/1
        ///   返回直播页：0/1
        /// 该方法直接覆盖 TextBlockWhiteBoardIndexInfo 和 board.pageInfo.* 三个 TextBlock 的文本，
        /// 不修改 CurrentWhiteboardIndex/WhiteboardTotalCount（避免破坏正常白板分页状态）。
        /// </summary>
        private void UpdateBoothPageInfoDisplay()
        {
            if (!_isVideoPresenterSpecialMode) return;

            int current = _boothCurrentPhotoIndex >= 0 ? _boothCurrentPhotoIndex + 1 : 0;
            int total = _capturedPhotos.Count;
            string text = $"{current}/{total}";

            // 立即同步更新一次（覆盖 UpdateIndexInfoDisplay 中 TextBlockWhiteBoardIndexInfo.Text = "x/x" 的设置）
            ApplyBoothPageText(text);

            // 特殊模式下隐藏"新页面/删除"按钮，按位置启用/禁用"上一页/下一页"
            UpdateBoothPagingButtonsState();

            // 异步再覆盖一次：UpdateBoardToolbarState() 内部用 Dispatcher.BeginInvoke 异步调用 UpdatePageInfo()，
            // 会把文本设回 "CurrentWhiteboardIndex/WhiteboardTotalCount"。
            // 这里用 DispatcherPriority.Background 确保在 UpdatePageInfo 之后执行，
            // 最终页码文本一定是虚拟分页格式（0/0、0/1、1/1）。
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ApplyBoothPageText(text);
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        /// <summary>把虚拟分页文本应用到所有页码 TextBlock（TextBlockWhiteBoardIndexInfo + board.pageInfo.*）。</summary>
        private void ApplyBoothPageText(string text)
        {
            if (TextBlockWhiteBoardIndexInfo != null)
            {
                TextBlockWhiteBoardIndexInfo.Text = text;
            }

            // board.pageInfo.* 在 BoardToolbarRegistry 中会被覆盖注册为 Border（不是 TextBlock），
            // 需要用 FindTextBlockInVisualTree 在视觉树中查找实际的 TextBlock（与 UpdatePageInfo 逻辑一致）
            foreach (var key in new[] { "board.pageInfo.left", "board.pageInfo.right", "board.pageInfo.center" })
            {
                var view = FindView(key);
                if (view == null) continue;
                if (view is System.Windows.Controls.TextBlock tb)
                {
                    tb.Text = text;
                }
                else
                {
                    var innerTb = FindTextBlockInVisualTree(view);
                    if (innerTb != null)
                    {
                        innerTb.Text = text;
                    }
                }
            }
        }

        /// <summary>视频展台特殊模式下更新翻页按钮状态：
        /// 隐藏"新页面"按钮（board.addNewPage），按位置启用/禁用"上一页/下一页"。
        /// 0/x(直播页)上一页灰色，x/x(最后一张照片)下一页灰色。删除按钮保留（0页由 ShowDeleteButton 控制）。</summary>
        private void UpdateBoothPagingButtonsState()
        {
            bool prevEnabled = _boothCurrentPhotoIndex >= 0; // 不在直播页才能上一页
            bool nextEnabled = _boothCurrentPhotoIndex < _capturedPhotos.Count - 1; // 不在最后一张才能下一页

            ApplyBoothPagingButtonsState(prevEnabled, nextEnabled);

            // 异步再覆盖一次：UpdateBoardToolbarState() 内部异步调 UpdatePageInfo() 会重置按钮 IsEnabled，
            // 这里用 DispatcherPriority.Background 确保在它之后执行
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ApplyBoothPagingButtonsState(prevEnabled, nextEnabled);
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void ApplyBoothPagingButtonsState(bool prevEnabled, bool nextEnabled)
        {
            // 获取正常前景画刷和禁用画刷（50%透明），与 UpdateIndexInfoDisplay 逻辑一致
            var iconBrush = Application.Current.FindResource("IconForeground") as SolidColorBrush;
            SolidColorBrush disabledBrush = null;
            if (iconBrush != null)
            {
                disabledBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(127, iconBrush.Color.R, iconBrush.Color.G, iconBrush.Color.B));
            }

            // 旧版 MainWindow 中的按钮
            if (BtnWhiteBoardSwitchPrevious != null) BtnWhiteBoardSwitchPrevious.IsEnabled = prevEnabled;
            if (BtnWhiteBoardSwitchNext != null) BtnWhiteBoardSwitchNext.IsEnabled = nextEnabled;
            if (BtnWhiteBoardAdd != null) BtnWhiteBoardAdd.Visibility = Visibility.Collapsed;

            // 新版 BoardToolbar 中的按钮
            // 用 IsEnabledBinding 触发 UpdateIconOpacity，同时恢复 IconGeometryDrawing.Brush
            // （UpdateIndexInfoDisplay 进特殊模式前可能把画刷设成了 disabledBrush，不恢复会残留半透明色）
            foreach (var key in new[] { "board.previousPage.left", "board.previousPage.right" })
            {
                if (FindView(key) is Controls.BoardToolbarButton btn)
                {
                    btn.IsEnabledBinding = prevEnabled;
                    if (btn.IconGeometryDrawing != null && iconBrush != null)
                        btn.IconGeometryDrawing.Brush = prevEnabled ? iconBrush : disabledBrush;
                }
            }
            foreach (var key in new[] { "board.nextPage.left", "board.nextPage.right" })
            {
                if (FindView(key) is Controls.BoardToolbarButton btn)
                {
                    btn.IsEnabledBinding = nextEnabled;
                    if (btn.IconGeometryDrawing != null && iconBrush != null)
                        btn.IconGeometryDrawing.Brush = nextEnabled ? iconBrush : disabledBrush;
                    // 强制文字为"下一页"，防止 UpdateIndexInfoDisplay 在 isLastPage 时改成"新页面"
                    if (btn.LabelTextBlockControl != null)
                        btn.LabelTextBlockControl.Text = Properties.FloatingBarStrings.Board_NextPage;
                }
            }
            // "新页面"组件 Id 是 board.addNewPage（不是 board.addPage）
            foreach (var key in new[] { "board.addNewPage.left", "board.addNewPage.right" })
            {
                if (FindView(key) is Controls.BoardToolbarButton btn) btn.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>退出特殊模式时恢复翻页按钮：显示"新页面"按钮，全部启用。</summary>
        private void RestoreBoothPagingButtons()
        {
            if (BtnWhiteBoardSwitchPrevious != null) BtnWhiteBoardSwitchPrevious.IsEnabled = true;
            if (BtnWhiteBoardSwitchNext != null) BtnWhiteBoardSwitchNext.IsEnabled = true;
            if (BtnWhiteBoardAdd != null) BtnWhiteBoardAdd.Visibility = Visibility.Visible;

            foreach (var key in new[] {
                "board.previousPage.left", "board.previousPage.right",
                "board.nextPage.left", "board.nextPage.right",
                "board.addNewPage.left", "board.addNewPage.right"
            })
            {
                if (FindView(key) is Controls.BoardToolbarButton btn)
                {
                    btn.IsEnabledBinding = true;
                    btn.Visibility = Visibility.Visible;
                }
            }
        }

        /// <summary>进入视频展台特殊模式：白板背景变 #333333，隐藏所有选择框，准备全屏预览。</summary>
        private void EnterVideoPresenterSpecialMode()
        {
            if (_isVideoPresenterSpecialMode) return;
            _isVideoPresenterSpecialMode = true;

            // 重置虚拟分页状态：进入特殊模式时默认回到直播页，但保留上次拍照列表
            _boothCurrentPhotoIndex = -1;

            try
            {
                // 保存当前 inkCanvas EditingMode，退出时恢复
                _inkEditingModeBeforeSpecialMode = inkCanvas?.EditingMode ?? InkCanvasEditingMode.Ink;

                // 显示深色背景容器
                if (VideoPresenterSpecialModeContainer != null)
                {
                    VideoPresenterSpecialModeContainer.Visibility = Visibility.Visible;
                }

                // 确保 GridBackgroundCover 可见（让其他深色背景元素也显示）
                if (GridBackgroundCover != null)
                {
                    GridBackgroundCover.Visibility = Visibility.Visible;
                }

                // 隐藏所有选择框（清空进入前的残留状态）
                HideAllSelectionOverlays();

                // 特殊模式下必须开启 IsManipulationEnabled，否则触摸不会被 WPF 提升为 Manipulation 事件，
                // VideoPresenterSpecialMode_ManipulationDelta 收不到拖动/缩放（BtnSelect_Click 在普通模式下
                // 会把 IsManipulationEnabled 设为 false，进入特殊模式时必须强制改回 true）。
                if (inkCanvas != null)
                {
                    inkCanvas.IsManipulationEnabled = true;
                }

                // 不强制切 EditingMode：保留用户当前模式。
                //   - 笔模式（Ink）：用户可在预览画面上绘制墨迹批注，触摸手势不会拖动预览
                //     （Main_Grid_ManipulationDelta 在 Ink 模式下走正常墨迹绘制路径，不走特殊模式缩放）
                //   - 选择模式（Select）：触摸手势用于拖动/缩放摄像头预览画面
                //     （Main_Grid_ManipulationDelta 在 Select 模式下走 VideoPresenterSpecialMode_ManipulationDelta）
                // 用户可以在视频展台期间随时切换笔/选择工具来改变行为。

                // 显示占位文字（直到摄像头找到并开始预览）
                // 重置文本（之前可能因没检测到摄像头改成"未检测到摄像头设备"）
                if (VideoPresenterSearchingText != null)
                {
                    VideoPresenterSearchingText.Text = "正在查找展台设备...";
                    VideoPresenterSearchingText.Visibility = Visibility.Visible;
                }
                // VideoCaptureElement 自己管理渲染（VMR9 + D3DImage），不需要清空 Source
                // 若之前的预览仍在运行，先停止避免设备占用
                try { VideoPresenterFullCanvasImage?.Stop(); } catch { }

                // 重置缩放
                _boothPreviewScale = 1.0;
                _boothPreviewTranslateX = 0;
                _boothPreviewTranslateY = 0;
                ApplyBoothPreviewTransform();

                // 虚拟分页：进入特殊模式时页码显示 0/0（无照片）
                UpdateBoothPageInfoDisplay();
                // 刷新侧栏页码列表：填充第 0 项（直播页，文字"再次点击返回直播画面"）
                RefreshBoothPageListView();

                // 若矫正开关已开启，启动实时识别框定时器
                if (Settings?.Automation?.IsEnablePhotoCorrection == true)
                {
                    StartPaperDetectTimer();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"EnterVideoPresenterSpecialMode 异常: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>退出视频展台特殊模式：恢复白板背景，保留展台拍照供下次进入继续查看。</summary>
        private void ExitVideoPresenterSpecialMode()
        {
            if (!_isVideoPresenterSpecialMode) return;
            _isVideoPresenterSpecialMode = false;

            // 停止 A4 纸实时识别定时器并隐藏覆盖层
            StopPaperDetectTimer();

            // 重置虚拟分页状态：退出后默认回到直播页，但保留照片列表
            _boothCurrentPhotoIndex = -1;

            try
            {
                // 隐藏特殊模式容器
                if (VideoPresenterSpecialModeContainer != null)
                {
                    VideoPresenterSpecialModeContainer.Visibility = Visibility.Collapsed;
                }

                // 清除冻结画面（恢复实时预览）
                ClearFrozenFrame();

                // 恢复 GridBackgroundCover 可见性（按原逻辑：黑/白板模式下会由其他逻辑控制，
                // 默认是隐藏的；这里只在白板模式时隐藏，黑板模式让原有逻辑接管）
                if (GridBackgroundCover != null && (Settings?.Canvas?.UsingWhiteboard ?? true))
                {
                    GridBackgroundCover.Visibility = Visibility.Collapsed;
                }

                // 丢弃特殊模式下绘制的墨迹
                if (inkCanvas != null)
                {
                    inkCanvas.Strokes.Clear();
                    // 同步清空 timeMachine 历史，避免 SaveStrokes 保存脏引用
                    // （strokes 已从画布移除但 history 中仍有引用）
                    timeMachine?.ClearStrokeHistory();
                    inkCanvas.EditingMode = _inkEditingModeBeforeSpecialMode;
                }
                // 重置旋转基准
                ResetRotationBaseline();
                // 同步清空直播页墨迹快照与每张照片的 Strokes，
                // 避免下次进入特殊模式时残留上一次的批注
                _liveStrokesSnapshot.Clear();
                foreach (var p in _capturedPhotos)
                {
                    try { p?.Strokes?.Clear(); } catch { }
                }

                // 清理鼠标拖动状态（防止退出时鼠标仍被捕获）
                if (_isBoothMouseDragging)
                {
                    _isBoothMouseDragging = false;
                    try { inkCanvas?.ReleaseMouseCapture(); } catch { }
                }
                _boothTouchSavedInkEditingMode = null;

                // 停止 VideoCaptureElement 预览，释放 DirectShow 图
                try { VideoPresenterFullCanvasImage?.Stop(); } catch { }
                // 释放亮度控制占用的常驻 FilterGraphNoThread 资源（下次进入会重新探测）
                try { _cameraService?.ReleaseBrightnessControl(); } catch { }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"ExitVideoPresenterSpecialMode 异常: {ex.Message}", LogHelper.LogType.Error);
            }

            // 退出特殊模式后恢复白板正常页码显示
            UpdateIndexInfoDisplay();
            // 恢复白板侧栏页码列表（覆盖视频展台虚拟分页项，重新填充实际白板页）
            RefreshBlackBoardSidePageListView();

            // 恢复翻页按钮（显示"新页面"按钮，全部启用）
            RestoreBoothPagingButtons();
        }

        /// <summary>隐藏所有选择框（墨迹选择框、图片选择框、图片缩放手柄）。</summary>
        private void HideAllSelectionOverlays()
        {
            if (GridInkCanvasSelectionCover != null)
            {
                GridInkCanvasSelectionCover.Visibility = Visibility.Collapsed;
            }
            if (BorderStrokeSelectionControl != null)
            {
                BorderStrokeSelectionControl.Visibility = Visibility.Collapsed;
            }
            if (BorderImageSelectionControl != null)
            {
                BorderImageSelectionControl.Visibility = Visibility.Collapsed;
            }
            HideImageResizeHandles();
            if (ImageSelectionOverlay != null)
            {
                ImageSelectionOverlay.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>把当前 _boothPreviewScale / Translate 应用到全屏预览 Image 的 RenderTransform。</summary>
        private void ApplyBoothPreviewTransform()
        {
            if (VideoPresenterFullCanvasScale == null || VideoPresenterFullCanvasTranslate == null) return;
            VideoPresenterFullCanvasScale.ScaleX = _boothPreviewScale;
            VideoPresenterFullCanvasScale.ScaleY = _boothPreviewScale;
            VideoPresenterFullCanvasTranslate.X = _boothPreviewTranslateX;
            VideoPresenterFullCanvasTranslate.Y = _boothPreviewTranslateY;

            // 同步冻结画面 Image 的变换（与 VideoCaptureElement 对齐，确保批注与画面一致）
            if (VideoPresenterFrozenFrameScale != null) VideoPresenterFrozenFrameScale.ScaleX = _boothPreviewScale;
            if (VideoPresenterFrozenFrameScale != null) VideoPresenterFrozenFrameScale.ScaleY = _boothPreviewScale;
            if (VideoPresenterFrozenFrameTranslate != null) VideoPresenterFrozenFrameTranslate.X = _boothPreviewTranslateX;
            if (VideoPresenterFrozenFrameTranslate != null) VideoPresenterFrozenFrameTranslate.Y = _boothPreviewTranslateY;
        }

        /// <summary>
        /// 把当前 inkCanvas.Strokes 保存到当前槽位：
        /// 若当前在照片预览页（_boothCurrentPhotoIndex >= 0），保存到对应 CapturedImage.Strokes；
        /// 若当前在直播页（_boothCurrentPhotoIndex == -1），保存到 _liveStrokesSnapshot。
        /// 切换页码前调用，避免墨迹串台。
        /// </summary>
        private void SaveCurrentBoothStrokesToSlot()
        {
            if (inkCanvas == null) return;
            try
            {
                var current = inkCanvas.Strokes;
                System.Windows.Ink.StrokeCollection target;
                if (_boothCurrentPhotoIndex >= 0 && _boothCurrentPhotoIndex < _capturedPhotos.Count)
                {
                    target = _capturedPhotos[_boothCurrentPhotoIndex].Strokes;
                }
                else
                {
                    target = _liveStrokesSnapshot;
                }
                target.Clear();
                if (current != null && current.Count > 0)
                {
                    // Clone 避免后续 inkCanvas.Strokes.Clear() 影响快照
                    foreach (var s in current) target.Add(s.Clone());
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"保存当前页墨迹失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        /// <summary>
        /// 从指定槽位恢复墨迹到 inkCanvas.Strokes：
        /// photoIndex >= 0 时从对应照片的 Strokes 恢复；&lt; 0 时从直播页快照恢复。
        /// 切换页码后调用，让墨迹跟着照片切换。
        /// </summary>
        private void RestoreBoothStrokesFromSlot(int photoIndex)
        {
            if (inkCanvas == null) return;
            try
            {
                System.Windows.Ink.StrokeCollection source;
                if (photoIndex >= 0 && photoIndex < _capturedPhotos.Count)
                {
                    source = _capturedPhotos[photoIndex].Strokes;
                }
                else
                {
                    source = _liveStrokesSnapshot;
                }
                inkCanvas.Strokes.Clear();
                if (source != null && source.Count > 0)
                {
                    // Clone 避免后续编辑影响快照
                    foreach (var s in source) inkCanvas.Strokes.Add(s.Clone());
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"恢复页墨迹失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        /// <summary>
        /// 计算指定角度下视频画面的变换矩阵（绕画面视觉中心旋转 + 90/270 缩小 fit 容器）。
        /// 画面视觉中心 = 容器中心经 RenderTransform 缩放和平移后的位置：
        ///   cx = containerW/2 * previewScale + previewTranslateX
        ///   cy = containerH/2 * previewScale + previewTranslateY
        /// 墨迹在 inkCanvas 坐标系（容器坐标），移动时已同步平移，所以旋转中心必须用画面视觉中心，
        /// 否则移动后旋转墨迹会绕容器中心转，与画面不同步。
        /// 0°/180°：scale=1.0；90°/270°：scale=min(H/imgW, W/imgH)/min(W/imgW, H/imgH)。
        /// 当 imgW/imgH=0 时回退到老公式 min(W/H, H/W)（适用于横屏视频，video=1920x1080）。
        /// </summary>
        private System.Windows.Media.Matrix GetBoothRotationMatrix(double angleDegrees, double containerW, double containerH, double imgW = 0, double imgH = 0)
        {
            // 画面视觉中心：考虑 RenderTransform 的缩放和平移
            double cx = (containerW / 2.0) * _boothPreviewScale + _boothPreviewTranslateX;
            double cy = (containerH / 2.0) * _boothPreviewScale + _boothPreviewTranslateY;
            double angle = angleDegrees % 360.0;
            if (angle < 0) angle += 360.0;
            bool rotated = Math.Abs(angle - 90.0) < 0.01 || Math.Abs(angle - 270.0) < 0.01;
            double scale = 1.0;
            if (rotated && containerW > 0 && containerH > 0)
            {
                if (imgW > 0 && imgH > 0)
                {
                    // 按图像实际比例算缩放：
                    // 0° 时 Stretch scale = min(W/iw, H/ih)
                    // 90° 时（容器交换 HxW）Stretch scale = min(H/iw, W/ih)
                    // delta scale = s_90 / s_0
                    double s0 = Math.Min(containerW / imgW, containerH / imgH);
                    double s90 = Math.Min(containerH / imgW, containerW / imgH);
                    if (s0 > 0) scale = s90 / s0;
                }
                else
                {
                    // 老公式：适用于横屏视频（image aspect ≈ container aspect）
                    scale = Math.Min(containerW / containerH, containerH / containerW);
                }
            }

            var m = System.Windows.Media.Matrix.Identity;
            m.RotateAt(angle, cx, cy);
            var s = System.Windows.Media.Matrix.Identity;
            s.ScaleAt(scale, scale, cx, cy);
            return m * s;
        }

        /// <summary>
        /// 在旋转前保存当前墨迹为基准（若尚未保存），并记录当前角度。
        /// 必须在修改 Rotation.Angle 之前调用，否则记录的基准角度会是目标角度导致 delta=Identity。
        /// </summary>
        private void EnsureRotationBaseline()
        {
            if (_rotationBaselineStrokes != null) return;
            if (inkCanvas == null || inkCanvas.Strokes.Count == 0) return;
            _rotationBaselineStrokes = inkCanvas.Strokes.Clone();
            // 记录墨迹当前对应的"实时画面视觉角度"：
            // - 实时画面：VideoPresenterFullCanvasRotation.Angle（= cameraRotation*90）
            // - 照片预览：照片内容是实时画面旋转 cameraRotation*90° 后的样子，
            //   Angle 被重置为 0，但墨迹对应实时 cameraRotation*90° 状态，
            //   所以视觉角度 = cameraRotation*90 + Angle（照片预览旋转的增量）
            _rotationBaselineAngle = GetBoothVisualAngle();
        }

        /// <summary>
        /// 获取墨迹当前对应的"实时画面视觉角度"。
        /// 实时画面：= VideoPresenterFullCanvasRotation.Angle。
        /// 照片预览：照片内容是实时画面旋转 cameraRotation*90° 后的样子，
        ///   Angle 从 0 开始（照片预览自身的旋转增量），视觉角度 = cameraRotation*90 + Angle。
        /// </summary>
        /// <summary>
        /// 获取墨迹当前对应的旋转角度。
        /// 照片预览页：照片位图在拍照时已 RotateFlip 旋转到正向，Angle=0 即"正向显示"，
        ///   所以缩放系数只取决于 Angle（LayoutTransform 旋转角）和照片实际比例，与 cameraService 无关。
        /// 实时画面：Angle = cameraService.RotationAngle*90（由旋转按钮同步设置）。
        /// 两种场景都直接返回 VideoPresenterFullCanvasRotation.Angle。
        /// </summary>
        private int GetBoothVisualAngle()
        {
            double angle = VideoPresenterFullCanvasRotation?.Angle ?? 0;
            return (int)(angle % 360.0 + 360.0) % 360;
        }

        /// <summary>
        /// 从基准墨迹重新变换到目标角度，避免 delta 累积导致越来越小。
        /// 基准墨迹在保存时记录对应角度，每次旋转从基准重新算：
        /// delta = Transform(baselineAngle)^(-1) * Transform(targetAngle)
        /// 用户画新墨迹后基准重置（StrokesChanged 触发 ResetRotationBaseline）。
        /// 调用前必须先 EnsureRotationBaseline()（在修改角度前）。
        /// </summary>
        private void RotateBoothStrokesFromBaseline(double targetAngleDegrees)
        {
            if (inkCanvas == null) return;
            if (inkCanvas.Strokes.Count == 0) return;
            try
            {
                double containerW = VideoPresenterSpecialModeContainer?.ActualWidth ?? inkCanvas.ActualWidth;
                double containerH = VideoPresenterSpecialModeContainer?.ActualHeight ?? inkCanvas.ActualHeight;
                if (containerW <= 0 || containerH <= 0) return;

                // 获取当前图像实际尺寸（照片预览页用照片位图尺寸，实时画面用 0 走老公式回退）
                double imgW = 0, imgH = 0;
                bool photoVisible = VideoPresenterFrozenFrameImage != null
                    && VideoPresenterFrozenFrameImage.Visibility == Visibility.Visible;
                if (photoVisible && VideoPresenterFrozenFrameImage.Source is BitmapSource bs)
                {
                    imgW = bs.PixelWidth;
                    imgH = bs.PixelHeight;
                }

                // 从当前角度变换到目标角度：delta = M_current^(-1) * M_target
                // _rotationBaselineAngle 在每次旋转后更新为当前画布墨迹对应的角度
                var currentMatrix = GetBoothRotationMatrix(_rotationBaselineAngle, containerW, containerH, imgW, imgH);
                var targetMatrix = GetBoothRotationMatrix(targetAngleDegrees, containerW, containerH, imgW, imgH);
                if (!currentMatrix.HasInverse) return;
                var currentInv = currentMatrix;
                currentInv.Invert();
                var delta = currentInv * targetMatrix;

                // 诊断日志：照片预览页时记录墨迹变换矩阵
                try
                {
                    if (photoVisible)
                    {
                        LogHelper.WriteLogToFile(
                            $"[BoothDiag] RotateStrokesFromBaseline: baselineAngle={_rotationBaselineAngle}, targetAngle={targetAngleDegrees}, " +
                            $"container={containerW}x{containerH}, imgSize={imgW}x{imgH}, scale={_boothPreviewScale}, translate=({_boothPreviewTranslateX},{_boothPreviewTranslateY}), " +
                            $"currentM22={currentMatrix.M22}, targetM22={targetMatrix.M22}, " +
                            $"delta=[{delta.M11},{delta.M12},{delta.M21},{delta.M22}|{delta.OffsetX},{delta.OffsetY}], " +
                            $"strokesCount={inkCanvas.Strokes.Count}",
                            LogHelper.LogType.Info);
                    }
                }
                catch { }

                _isApplyingRotationToStrokes = true;
                var prevCommitType = _currentCommitType;
                _currentCommitType = CommitReason.CodeInput;
                try
                {
                    // 旋转前墨迹 bbox（容器坐标）
                    System.Windows.Rect beforeBBox = inkCanvas.Strokes.GetBounds();
                    // 旋转前照片视觉中心
                    double cx = (containerW / 2.0) * _boothPreviewScale + _boothPreviewTranslateX;
                    double cy = (containerH / 2.0) * _boothPreviewScale + _boothPreviewTranslateY;

                    // 原地变换：保留 Stroke 对象引用，使 TimeMachine 历史中的 Stroke 引用与画布一致
                    inkCanvas.Strokes.Transform(delta, false);
                    // 同步变换历史中不在画布上的笔画（如形状识别的 ReplacedStroke）
                    timeMachine?.TransformStrokesInHistory(delta, inkCanvas.Strokes);

                    // 更新 StrokeInitialHistory 为旋转后的状态，避免后续用户操作（拖动/缩放墨迹）
                    // 时用旋转前的旧坐标作为初始值，导致撤销恢复到错误位置
                    foreach (var stroke in inkCanvas.Strokes)
                    {
                        StrokeInitialHistory[stroke] = stroke.StylusPoints.Clone();
                    }

                    // 旋转后墨迹 bbox
                    System.Windows.Rect afterBBox = inkCanvas.Strokes.GetBounds();
                    try
                    {
                        LogHelper.WriteLogToFile(
                            $"[BoothDiag] StrokesBBox: before=[{beforeBBox.X:F1},{beforeBBox.Y:F1},{beforeBBox.Width:F1}x{beforeBBox.Height:F1}], " +
                            $"after=[{afterBBox.X:F1},{afterBBox.Y:F1},{afterBBox.Width:F1}x{afterBBox.Height:F1}], " +
                            $"visualCenter=({cx:F1},{cy:F1}), beforeCenter=({beforeBBox.X+beforeBBox.Width/2:F1},{beforeBBox.Y+beforeBBox.Height/2:F1}), " +
                            $"afterCenter=({afterBBox.X+afterBBox.Width/2:F1},{afterBBox.Y+afterBBox.Height/2:F1})",
                            LogHelper.LogType.Info);
                    }
                    catch { }
                }
                finally
                {
                    _isApplyingRotationToStrokes = false;
                    _currentCommitType = prevCommitType;
                }

                // 更新当前角度，供下次旋转计算 delta
                _rotationBaselineAngle = (int)(targetAngleDegrees % 360.0 + 360.0) % 360;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"从基准变换墨迹失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        /// <summary>
        /// 重置旋转基准（用户在非 0° 画新墨迹后，或切换页面/退出展台时调用）。
        /// 下次旋转会重新保存基准。
        /// </summary>
        private void ResetRotationBaseline()
        {
            _rotationBaselineStrokes = null;
        }

        /// <summary>
        /// 在视频展台预览层上短暂显示一条提示消息（2.5s 后自动隐藏）。
        /// 复用 VideoPresenterSearchingText 元素：临时改写 Text 并显示，计时后恢复"正在查找展台设备..."文本并隐藏。
        /// 用于拍照失败等场景给用户可见反馈。
        /// </summary>
        private void ShowBoothTransientMessage(string message)
        {
            if (VideoPresenterSearchingText == null) return;
            try
            {
                VideoPresenterSearchingText.Text = message;
                VideoPresenterSearchingText.Visibility = Visibility.Visible;
                // 2.5s 后恢复并隐藏
                var timer = new DispatcherTimer(DispatcherPriority.Background)
                {
                    Interval = TimeSpan.FromMilliseconds(2500)
                };
                timer.Tick += (s, args) =>
                {
                    try
                    {
                        timer.Stop();
                        // 仅当仍显示同一消息时才隐藏，避免覆盖"未检测到摄像头设备"等持续状态
                        if (VideoPresenterSearchingText != null
                            && VideoPresenterSearchingText.Text == message)
                        {
                            VideoPresenterSearchingText.Visibility = Visibility.Collapsed;
                            VideoPresenterSearchingText.Text = "正在查找展台设备...";
                        }
                    }
                    catch { }
                };
                timer.Start();
            }
            catch { }
        }

        /// <summary>
        /// 底部"关闭"按钮：完全退出视频展台模式（退出特殊模式 + 关闭菜单 + 停止预览）。
        /// 调用后 _isVideoPresenterSpecialMode = false，白板恢复正常模式。
        /// </summary>
        private void BtnExitVideoPresenter_Click(object sender, RoutedEventArgs e)
        {
            ExitVideoPresenterSpecialMode();
            CloseVideoPresenterSidebarAndReleaseResources();
        }

        private void CloseVideoPresenterSidebarAndReleaseResources()
        {
            if (BoothPopup != null)
            {
                AnimationsHelper.HidePopupWithSlideAndFade(BoothPopup);
            }

            // 退出展台前 flush 防抖队列里待保存的摄像头属性设置，避免最后一次拖动丢失
            FlushBoothPropsSaveDebounced();

            StopVideoPresenterPreviewAndFrameCache(clearPreviewImage: true);
        }

        private void StopVideoPresenterPreviewAndFrameCache(bool clearPreviewImage)
        {
            if (BtnCapturePhoto != null)
            {
                BtnCapturePhoto.IsEnabled = false;
            }

            // 左上角侧栏预览已移除（VideoPresenterPreviewImage 已从 XAML 删除），
            // 不再需要清理 Image.Source。clearPreviewImage 参数保留用于调用方语义兼容。

            try { VideoPresenterFullCanvasImage?.Stop(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
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

            _cameraService = CameraServiceFactory.Create();
            _cameraService.FrameReceived += CameraService_FrameReceived;
            _cameraService.ErrorOccurred += CameraService_ErrorOccurred;
            SyncBoothResolutionToCameraService();
        }

        internal void SyncBoothResolutionToCameraService()
        {
            // 接口已改为通过 SelectedResolutionIndex 切换分辨率，
            // 这里把底层枚举的 native 分辨率同步到 UI ComboBox
            RefreshBoothResolutionComboBox();
        }

        /// <summary>把当前摄像头的所有 (W, H, FPS) 组合填充到分辨率 ComboBox。
        /// 单 ComboBox 显示 "1920×1080@60fps" 格式，替代之前的双 ComboBox 分开选择。</summary>
        private void RefreshBoothResolutionComboBox()
        {
            if (BoothResolutionComboBox == null) return;
            if (_cameraService == null) return;

            try
            {
                _isBoothComboBoxUpdating = true;
                BoothResolutionComboBox.Items.Clear();

                var combos = _cameraService.AllResolutionFpsCombos;
                if (combos == null || combos.Count == 0)
                {
                    // 回退到 UniqueResolutions（某些摄像头可能不区分帧率）
                    var unique = _cameraService.UniqueResolutions;
                    if (unique == null || unique.Count == 0)
                    {
                        BoothResolutionComboBox.Items.Add("加载中…");
                        BoothResolutionComboBox.SelectedIndex = 0;
                        LogHelper.WriteLogToFile(
                            "RefreshBoothResolutionComboBox: AllResolutionFpsCombos 和 UniqueResolutions 均为空，显示占位文本",
                            LogHelper.LogType.Warning);
                        return;
                    }

                    foreach (var r in unique)
                    {
                        BoothResolutionComboBox.Items.Add(r);
                    }
                    int selFallback = _cameraService.SelectedUniqueResolutionIndex;
                    if (selFallback < 0 || selFallback >= unique.Count) selFallback = 0;
                    BoothResolutionComboBox.SelectedIndex = selFallback;

                    var cur = unique[selFallback];
                    _boothResolutionWidth = cur.Width;
                    _boothResolutionHeight = cur.Height;
                    return;
                }

                foreach (var r in combos)
                {
                    BoothResolutionComboBox.Items.Add(r);
                }

                int sel = _cameraService.SelectedComboIndex;
                if (sel < 0 || sel >= combos.Count) sel = 0;
                BoothResolutionComboBox.SelectedIndex = sel;

                var current = combos[sel];
                _boothResolutionWidth = current.Width;
                _boothResolutionHeight = current.Height;

                LogHelper.WriteLogToFile(
                    $"RefreshBoothResolutionComboBox: 填充 {combos.Count} 项 (W,H,FPS) 组合，选中索引 {sel} ({current.Width}×{current.Height}@{current.FrameRate}fps)",
                    LogHelper.LogType.Info);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"RefreshBoothResolutionComboBox 异常: {ex.Message}", LogHelper.LogType.Error);
            }
            finally
            {
                _isBoothComboBoxUpdating = false;
            }
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
        /// <param name="e">来自摄像头服务的帧事件参数；Frame 为 null 时忽略。</param>
        /// <remarks>
        /// 通过 ICameraService.GetCurrentFrameAsBitmap 拉取一份 Bitmap 用于拍照缓存，
        /// 通过 e.Frame 更新预览与实时上屏。
        /// </remarks>
        private void CameraService_FrameReceived(object sender, FrameEventArgs e)
        {
            if (e?.Frame == null) return;

            try
            {
                // 拉取一份 GDI+ Bitmap 副本作为拍照缓存
                var photoCache = _cameraService?.GetCurrentFrameAsBitmap();
                if (photoCache != null)
                {
                    lock (_videoPresenterFrameLock)
                    {
                        _lastFrame?.Dispose();
                        _lastFrame = photoCache;
                    }
                }

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    // 左上角侧栏预览已移除（VideoPresenterPreviewImage 已从 XAML 删除），
                    // 不再需要把 e.Frame 同步到侧栏 Image。

                    // 特殊模式：全屏预览由 VideoCaptureElement 自己 D3D 渲染（VMR9 + D3DImage 零拷贝），
                    // 不再走 SetSource 路径（彻底解决 DUCE.Channel.SyncFlush OOM）。
                    // 这里只需要隐藏占位文字、启用拍照按钮
                    if (_isVideoPresenterSpecialMode)
                    {
                        if (VideoPresenterSearchingText != null
                            && VideoPresenterSearchingText.Visibility == Visibility.Visible)
                        {
                            VideoPresenterSearchingText.Visibility = Visibility.Collapsed;
                        }
                    }

                    if (BtnCapturePhoto != null)
                    {
                        BtnCapturePhoto.IsEnabled = true;
                    }

                    // 实时上屏：刷新当前页的画面元素
                    TryUpdateLiveFrameOnCanvas(e.Frame);
                }));
            }
            catch
            {
                // 忽略预览刷新异常
            }
        }

        /// <summary>
        /// VideoCaptureElement.MediaOpened 事件处理器：媒体（摄像头）成功打开时触发。
        /// 此时 D3DImage 即将开始渲染，可以安全隐藏"正在查找展台设备"占位文字。
        /// 比依赖 NewVideoSample 事件更可靠：某些 VMR9 Renderless 配置下 SampleGrabber 可能不连入图，
        /// 但 MediaOpened 一定会触发（除非设备打开失败，那时由 MediaFailed 兜底）。
        /// </summary>
        private void VideoPresenterFullCanvasImage_MediaOpened(object sender, RoutedEventArgs e)
        {
            try
            {
                if (VideoPresenterSearchingText != null
                    && VideoPresenterSearchingText.Visibility == Visibility.Visible)
                {
                    VideoPresenterSearchingText.Visibility = Visibility.Collapsed;
                }
                if (BtnCapturePhoto != null) BtnCapturePhoto.IsEnabled = true;
                LogHelper.WriteLogToFile(
                    "[VideoPresenter] MediaOpened: 摄像头已打开，隐藏占位文字",
                    LogHelper.LogType.Info);
            }
            catch { }
        }

        /// <summary>
        /// VideoCaptureElement.MediaFailed 事件处理器：摄像头打开失败或运行中出错时触发。
        /// 显示失败提示，便于用户和日志诊断。
        /// </summary>
        private void VideoPresenterFullCanvasImage_MediaFailed(object sender, WPFMediaKit.DirectShow.MediaPlayers.MediaFailedEventArgs e)
        {
            try
            {
                // WPFMediaKit 的 MediaFailedEventArgs 可能包含 Exception 字段（不同版本结构略有差异），
                // 用反射兜底避免编译时绑定到不存在的成员。
                string msg = "未知错误";
                if (e != null)
                {
                    var exProp = e.GetType().GetProperty("Exception");
                    var ex = exProp?.GetValue(e) as Exception;
                    if (ex != null) msg = ex.Message;
                    else msg = e.ToString();
                }
                LogHelper.WriteLogToFile(
                    $"[VideoPresenter] MediaFailed: {msg}",
                    LogHelper.LogType.Error);
                ErrorOccurredRelay?.Invoke(this, $"摄像头打开失败: {msg}");
            }
            catch { }
        }

        /// <summary>
        /// VideoCaptureElement.NewVideoSample 事件处理器：特殊模式下从 DirectShow 拿到 System.Drawing.Bitmap，
        /// 用于驱动侧栏预览、拍照缓存、隐藏占位文字。
        /// 注意：VideoCaptureElement 已通过 D3DImage 自己渲染全屏预览，这里只处理"额外"需求。
        /// </summary>
        private void VideoPresenterFullCanvasImage_NewVideoSample(object sender, VideoSampleArgs e)
        {
            if (!_isVideoPresenterSpecialMode) return;
            if (e?.VideoFrame == null) return;

            try
            {
                // 拍照缓存：克隆一份 Bitmap（VideoSampleArgs.VideoFrame 归 SampleGrabber 所有，事件后会被复用/释放）
                Bitmap photoCache;
                lock (_videoPresenterFrameLock)
                {
                    _lastFrame?.Dispose();
                    try
                    {
                        photoCache = (Bitmap)e.VideoFrame.Clone();
                        // 应用旋转：保证拍照出来的图像已正（VideoCaptureElement 的 LayoutTransform 只影响渲染，不影响帧内容）
                        if (photoCache != null && _cameraService != null)
                        {
                            int rot = _cameraService.RotationAngle;
                            if (rot != 0)
                            {
                                var rotationType = rot switch
                                {
                                    1 => System.Drawing.RotateFlipType.Rotate90FlipNone,
                                    2 => System.Drawing.RotateFlipType.Rotate180FlipNone,
                                    3 => System.Drawing.RotateFlipType.Rotate270FlipNone,
                                    _ => System.Drawing.RotateFlipType.RotateNoneFlipNone
                                };
                                photoCache.RotateFlip(rotationType);
                            }
                        }
                    }
                    catch { photoCache = null; }
                    _lastFrame = photoCache;
                }

                // 左上角侧栏预览已移除（VideoPresenterPreviewImage 已从 XAML 删除），
                // 不再需要把 Bitmap 转 BitmapSource 同步到侧栏 Image。
                // photoCache 仍然保留为 _lastFrame，供拍照使用。

                // 隐藏占位文字、启用拍照按钮
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (VideoPresenterSearchingText != null
                        && VideoPresenterSearchingText.Visibility == Visibility.Visible)
                    {
                        VideoPresenterSearchingText.Visibility = Visibility.Collapsed;
                    }
                    if (BtnCapturePhoto != null) BtnCapturePhoto.IsEnabled = true;
                }));
            }
            catch
            {
                // 忽略单帧异常
            }
        }

        /// <summary>System.Drawing.Bitmap 转 BitmapSource（Freeze 后可跨线程）。</summary>
        private static BitmapSource BitmapToBitmapSource(Bitmap bmp)
        {
            if (bmp == null) return null;
            IntPtr hBmp = IntPtr.Zero;
            try
            {
                hBmp = bmp.GetHbitmap();
                var bs = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hBmp, IntPtr.Zero, System.Windows.Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                bs.Freeze();
                return bs;
            }
            finally
            {
                if (hBmp != IntPtr.Zero) PInvoke.DeleteObject(new HGDIOBJ(hBmp));
            }
        }

        //[System.Runtime.InteropServices.DllImport("gdi32.dll")]
        //private static extern bool DeleteObject(IntPtr hObject);

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

                // WinRT 复用 WriteableBitmap：同一页每帧 Source 引用不变时跳过赋值，避免触发 Source 变更通知
                if (!ReferenceEquals(img.Source, preview))
                {
                    img.Source = preview;
                }
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
        private async void RefreshVideoPresenterDeviceList()
        {
            if (_cameraService == null) return;
            if (CameraDevicesComboBox == null) return;

            _isBoothCameraComboBoxUpdating = true;
            CameraDevicesComboBox.Items.Clear();
            CameraDevicesComboBox.Items.Add("正在检测摄像头…");
            CameraDevicesComboBox.SelectedIndex = 0;
            _isBoothCameraComboBoxUpdating = false;

            try
            {
                await _cameraService.RefreshCameraListAsync();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"RefreshVideoPresenterDeviceList: 刷新摄像头列表失败: {ex.Message}", LogHelper.LogType.Error);
            }

            _isBoothCameraComboBoxUpdating = true;
            CameraDevicesComboBox.Items.Clear();

            if (_cameraService.AvailableCameras == null || _cameraService.AvailableCameras.Count == 0)
            {
                CameraDevicesComboBox.Items.Add("未检测到摄像头设备");
                CameraDevicesComboBox.SelectedIndex = 0;
                _isBoothCameraComboBoxUpdating = false;
                // 没找到摄像头：把占位文字改成"未检测到摄像头设备"并保持显示，
                // 让用户清楚知道为什么预览没起来（而不是一直卡在"正在查找展台设备..."）
                if (VideoPresenterSearchingText != null)
                {
                    VideoPresenterSearchingText.Text = "未检测到摄像头设备";
                    VideoPresenterSearchingText.Visibility = Visibility.Visible;
                }
                return;
            }

            // 检测到摄像头：不在这里隐藏 SearchingText！
            // 之前立即隐藏会导致用户看到"正在查找..."闪一下消失，然后纯黑屏直到 MediaOpened 触发。
            // 现在改为：StartVideoCaptureElementPreviewAsync 会把文字改为"正在启动摄像头..."，
            // 直到 MediaOpened 事件触发（画面真的出来）才隐藏。
            // 只把文字从"正在查找..."改为"已找到，正在启动..."让用户知道进度。
            if (VideoPresenterSearchingText != null)
            {
                VideoPresenterSearchingText.Text = "正在启动摄像头...";
                VideoPresenterSearchingText.Visibility = Visibility.Visible;
            }

            for (int i = 0; i < _cameraService.AvailableCameras.Count; i++)
            {
                CameraDevicesComboBox.Items.Add(_cameraService.AvailableCameras[i].Name);
            }

            // 预选摄像头优先级：
            //   1. Settings.Canvas.VideoPresenterLastCameraName（跨会话持久化，用 DsDevice.Name 匹配）
            //   2. _cameraIndexByPage[当前页]（会话级按页保存的索引）
            //   3. 第一个摄像头（默认）
            int currentPage = GetCurrentPageIndex();
            int cameraToSelect = 0;
            string savedName = Settings?.Canvas?.VideoPresenterLastCameraName;
            if (!string.IsNullOrWhiteSpace(savedName))
            {
                for (int i = 0; i < _cameraService.AvailableCameras.Count; i++)
                {
                    if (string.Equals(_cameraService.AvailableCameras[i].Name, savedName, StringComparison.OrdinalIgnoreCase))
                    {
                        cameraToSelect = i;
                        break;
                    }
                }
            }
            if (cameraToSelect == 0
                && _cameraIndexByPage.TryGetValue(currentPage, out int savedIdx)
                && savedIdx >= 0 && savedIdx < _cameraService.AvailableCameras.Count)
            {
                cameraToSelect = savedIdx;
            }

            CameraDevicesComboBox.SelectedIndex = cameraToSelect;
            _isBoothCameraComboBoxUpdating = false;

            // 先独立枚举 native 分辨率（不启动预览、不抢占设备）：
            // 特殊模式下 StartVideoPresenterPreview 会启动 VideoCaptureElement（占用设备），
            // 此时 _cameraService.StartPreviewAsync 未被调用，NativeResolutions 仍为空，
            // 导致分辨率 ComboBox 显示"加载中…"、FindCapabilityIndex 返回 -1、ApplyBoothPreviewTransform 也拿不到尺寸。
            // 用 EnumerateResolutionsAsync（内部 FilterGraphNoThread 不调用 Run）先填充分辨率列表，
            // 再启动 VideoCaptureElement 预览。
            try
            {
                await _cameraService.EnumerateResolutionsAsync(cameraToSelect);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile(
                    $"RefreshVideoPresenterDeviceList: EnumerateResolutionsAsync 失败: {ex.Message}",
                    LogHelper.LogType.Warning);
            }

            // 枚举分辨率后探测亮度支持（FilterGraphNoThread 不抢占设备）并应用保存的亮度
            await ProbeAndApplyBoothBrightnessAsync();

            // 持久化恢复分辨率：从 Settings.Canvas.VideoPresenterLastResolutionKey 读取，
            // 用 "WxH@FPS" 格式匹配 NativeResolutions 中的项，找不到则保持默认（最接近 1920×1080）
            TryRestoreResolutionFromSettings();

            // 立即启动预览
            StartVideoPresenterPreview(cameraToSelect);
        }

        /// <summary>
        /// 从 Settings.Canvas.VideoPresenterLastResolutionKey 恢复上次选中的分辨率。
        /// 用 "WxH@FPS" 格式匹配 NativeResolutions，找到则通过 SetSelectedResolutionIndexSilent
        /// 更新 _cameraService.SelectedResolutionIndex（不触发 RestartWithNewResolutionAsync 抢占设备），
        /// 并同步 BoothResolutionComboBox 选中项。
        /// 找不到则保持 EnumerateResolutionsAsync 设置的默认值（最接近 1920×1080）。
        /// </summary>
        private void TryRestoreResolutionFromSettings()
        {
            try
            {
                string key = Settings?.Canvas?.VideoPresenterLastResolutionKey;
                if (string.IsNullOrWhiteSpace(key)) return;
                var resolutions = _cameraService?.NativeResolutions;
                if (resolutions == null || resolutions.Count == 0) return;

                // 解析 "WxH@FPS"
                int parsedW = 0, parsedH = 0, parsedFps = 0;
                int atIdx = key.IndexOf('@');
                string whPart = atIdx > 0 ? key.Substring(0, atIdx) : key;
                string fpsPart = atIdx > 0 && atIdx < key.Length - 1 ? key.Substring(atIdx + 1) : null;
                int xIdx = whPart.IndexOf('x', StringComparison.OrdinalIgnoreCase);
                if (xIdx <= 0 || xIdx >= whPart.Length - 1) return;
                if (!int.TryParse(whPart.Substring(0, xIdx), out parsedW) || parsedW <= 0) return;
                if (!int.TryParse(whPart.Substring(xIdx + 1), out parsedH) || parsedH <= 0) return;
                if (fpsPart != null && !int.TryParse(fpsPart, out parsedFps)) parsedFps = 0;

                // 在 NativeResolutions 中查找匹配项
                int matchedIdx = -1;
                for (int i = 0; i < resolutions.Count; i++)
                {
                    var r = resolutions[i];
                    if (r.Width == parsedW && r.Height == parsedH)
                    {
                        if (parsedFps <= 0 || r.FrameRate == parsedFps)
                        {
                            matchedIdx = i;
                            break;
                        }
                    }
                }
                if (matchedIdx < 0) return;

                // 用 Silent 版本更新索引（不触发 RestartWithNewResolutionAsync 抢占设备），
                // 因为特殊模式下预览由 VideoCaptureElement 接管，_cameraService 不应启动预览
                _cameraService.SetSelectedResolutionIndexSilent(matchedIdx);

                // 同步 BoothResolutionComboBox 选中项（在 _isBoothComboBoxUpdating 保护下，避免触发 SelectionChanged）
                if (BoothResolutionComboBox != null)
                {
                    bool prevUpdating = _isBoothComboBoxUpdating;
                    _isBoothComboBoxUpdating = true;
                    try
                    {
                        // BoothResolutionComboBox 的项是 ResolutionInfo，需要按 W/H/FPS 匹配
                        for (int i = 0; i < BoothResolutionComboBox.Items.Count; i++)
                        {
                            if (BoothResolutionComboBox.Items[i] is ResolutionInfo ri)
                            {
                                if (ri.Width == parsedW && ri.Height == parsedH
                                    && (parsedFps <= 0 || ri.FrameRate == parsedFps))
                                {
                                    BoothResolutionComboBox.SelectedIndex = i;
                                    break;
                                }
                            }
                        }
                    }
                    finally
                    {
                        _isBoothComboBoxUpdating = prevUpdating;
                    }
                }

                LogHelper.WriteLogToFile(
                    $"[VideoPresenter] TryRestoreResolutionFromSettings: 恢复 {key} 成功（matchedIdx={matchedIdx}）",
                    LogHelper.LogType.Info);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"TryRestoreResolutionFromSettings 异常: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        private async void CameraDevicesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isBoothCameraComboBoxUpdating) return;
            if (_cameraService == null) return;
            int idx = CameraDevicesComboBox.SelectedIndex;
            if (idx < 0 || idx >= _cameraService.AvailableCameras.Count) return;

            // 持久化：保存选中的摄像头设备名到 Settings，下次启动时自动恢复
            try
            {
                string camName = _cameraService.AvailableCameras[idx].Name;
                if (Settings?.Canvas != null && Settings.Canvas.VideoPresenterLastCameraName != camName)
                {
                    Settings.Canvas.VideoPresenterLastCameraName = camName;
                    SettingsManager.SaveSettingsToFile();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"保存摄像头设备名到 Settings 失败: {ex.Message}", LogHelper.LogType.Warning);
            }

            // 切换摄像头时先重新枚举新设备的分辨率（不抢占设备），再启动预览，
            // 确保分辨率 ComboBox 能立刻反映新设备的能力。
            try
            {
                await _cameraService.EnumerateResolutionsAsync(idx);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile(
                    $"CameraDevicesComboBox_SelectionChanged: EnumerateResolutionsAsync 失败: {ex.Message}",
                    LogHelper.LogType.Warning);
            }

            // 新设备亮度支持情况不同，重新探测并应用保存的亮度
            await ProbeAndApplyBoothBrightnessAsync();

            // 切换摄像头后，新设备的 NativeResolutions 不同，旧分辨率 key 不再适用，
            // 尝试用持久化的分辨率 key 在新设备上匹配；匹配不到则用默认（最接近 1920×1080）
            TryRestoreResolutionFromSettings();

            StartVideoPresenterPreview(idx);
        }

        /// <summary>Manipulation 起始事件：配置允许 平移 + 缩放 + 旋转。
        /// 之前只允许 Scale 导致单指拖动无法触发 Translation（dx/dy 始终为 0）。
        /// - 单指拖动：Translation（移动预览图 + 同步墨迹）
        /// - 双指捏合：Scale（缩放预览图 + 同步墨迹）
        /// - 双指旋转：Rotation（暂不处理，丢弃避免误操作）</summary>
        private void VideoPresenterSpecialMode_ManipulationStarting(object sender, ManipulationStartingEventArgs e)
        {
            // 必须 Scale + Translate 才能让单指拖动产生 DeltaManipulation.Translation
            // 不加 Rotation 避免双指误旋转
            e.Mode = ManipulationModes.Scale | ManipulationModes.Translate;
            // ManipulationContainer 默认是 e.Source，即 VideoPresenterSpecialModeContainer，
            // 这样 e.ManipulationOrigin 已经是容器坐标，可以直接用于缩放锚点。
            e.Handled = true;
        }

        /// <summary>视频展台特殊模式触摸手势：
        /// - 单指拖动：平移预览图（同步平移 inkCanvas 墨迹）
        /// - 双指捏合：以 2 指线段中心为锚点等比缩放预览图（同步缩放 inkCanvas 墨迹）
        /// 这是 WPF 图片查看器的常见行为（如 Windows 照片查看器、Photoshop），符合用户直觉。
        /// 无论 inkCanvas.EditingMode 是 Ink 还是 Select，特殊模式下触摸手势都用于操作预览图。</summary>
        private void VideoPresenterSpecialMode_ManipulationDelta(object sender, ManipulationDeltaEventArgs e)
        {
            if (!_isVideoPresenterSpecialMode) return;

            try
            {
                var delta = e.DeltaManipulation;
                int manipulatorCount = e.Manipulators?.Count() ?? 0;

                // 锚点：ManipulationOrigin（容器坐标）
                // - 单指时是手指位置
                // - 双指时是 2 指线段中心
                System.Windows.Point origin = e.ManipulationOrigin;

                // === 1. 处理平移（单指拖动 或 双指平移）===
                double dx = delta.Translation.X;
                double dy = delta.Translation.Y;
                if (Math.Abs(dx) > 0.001 || Math.Abs(dy) > 0.001)
                {
                    _boothPreviewTranslateX += dx;
                    _boothPreviewTranslateY += dy;
                    ApplyBoothPreviewTransform();

                    // 同步平移 inkCanvas 上的墨迹
                    var translateMatrix = new Matrix();
                    translateMatrix.Translate(dx, dy);
                    try
                    {
                        foreach (var stroke in inkCanvas.Strokes)
                        {
                            stroke.Transform(translateMatrix, false);
                        }
                        // 同步变换历史中不在画布上的笔画（如形状识别的 ReplacedStroke）
                        timeMachine?.TransformStrokesInHistory(translateMatrix, inkCanvas.Strokes);
                        // 墨迹位置已改变，旋转基准过期，重置以便下次旋转重新保存
                        ResetRotationBaseline();
                    }
                    catch { }
                }

                // === 2. 处理缩放（仅双指）===
                double scaleFactor = delta.Scale.Length > 0
                    ? (delta.Scale.X + delta.Scale.Y) / 2.0
                    : 1.0;
                if (manipulatorCount >= 2 && Math.Abs(scaleFactor - 1.0) >= 0.001)
                {
                    // 限制缩放范围 0.1 - 10.0
                    double newScale = Math.Max(0.1, Math.Min(10.0, _boothPreviewScale * scaleFactor));

                    // 以 origin 为锚点做缩放，需要同步调整 translate
                    // 数学：translate_new = origin - (origin - translate_old) * (newScale / oldScale)
                    double ratio = newScale / _boothPreviewScale;
                    double newTranslateX = origin.X - (origin.X - _boothPreviewTranslateX) * ratio;
                    double newTranslateY = origin.Y - (origin.Y - _boothPreviewTranslateY) * ratio;

                    _boothPreviewScale = newScale;
                    _boothPreviewTranslateX = newTranslateX;
                    _boothPreviewTranslateY = newTranslateY;
                    ApplyBoothPreviewTransform();

                    // 同步缩放 inkCanvas 上的墨迹（在像素坐标系下对每条 Stroke 做矩阵变换）
                    ScaleInkCanvasStrokes(origin, ratio);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"VideoPresenterSpecialMode_ManipulationDelta 异常: {ex.Message}", LogHelper.LogType.Error);
            }
            finally
            {
                e.Handled = true;
            }
        }

        /// <summary>鼠标滚轮缩放：以鼠标位置为锚点等比缩放预览图，同步缩放 inkCanvas 墨迹。
        /// 这是 WPF 图片查看器的常见行为（如 Windows 照片查看器、Photoshop），符合用户直觉。</summary>
        private void VideoPresenterSpecialMode_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!_isVideoPresenterSpecialMode) return;

            try
            {
                // 每 120 一个刻度，放大/缩小 1.1 倍
                double scaleFactor = e.Delta > 0 ? 1.1 : 1.0 / 1.1;

                double newScale = Math.Max(0.1, Math.Min(10.0, _boothPreviewScale * scaleFactor));

                // 锚点：鼠标位置（容器坐标）—— 符合"鼠标哪里就放大哪里"的直觉
                System.Windows.Point origin = e.GetPosition(VideoPresenterSpecialModeContainer);

                double ratio = newScale / _boothPreviewScale;
                double newTranslateX = origin.X - (origin.X - _boothPreviewTranslateX) * ratio;
                double newTranslateY = origin.Y - (origin.Y - _boothPreviewTranslateY) * ratio;

                _boothPreviewScale = newScale;
                _boothPreviewTranslateX = newTranslateX;
                _boothPreviewTranslateY = newTranslateY;
                ApplyBoothPreviewTransform();

                ScaleInkCanvasStrokes(origin, ratio);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"VideoPresenterSpecialMode_MouseWheel 异常: {ex.Message}", LogHelper.LogType.Error);
            }
            finally
            {
                e.Handled = true;
            }
        }

        /// <summary>鼠标拖动处理：在特殊模式 + 非 Ink 模式下，鼠标左键拖动移动摄像头预览画面。
        /// 触摸通过 Manipulation 事件处理，但鼠标事件不会提升为 Manipulation，需要单独处理。
        /// 此方法在 inkCanvas_PreviewMouseDown 中调用，设置 e.Handled=true 阻止 InkCanvas 内部框选。</summary>
        private bool VideoPresenterSpecialMode_HandleMouseDown(MouseButtonEventArgs e)
        {
            if (!_isVideoPresenterSpecialMode) return false;
            if (e.ChangedButton != MouseButton.Left) return false;
            if (inkCanvas == null) return false;
            // 橡皮擦模式让 InkCanvas 正常擦除，不启动预览拖动
            if (inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint
                || inkCanvas.EditingMode == InkCanvasEditingMode.EraseByStroke) return false;
            // 漫游模式交给漫游路径处理（会同步移动预览画面与墨迹）
            if (IsBoardRoamingMode) return false;
            // Ink 模式下让 InkCanvas 正常绘制墨迹
            if (inkCanvas.EditingMode == InkCanvasEditingMode.Ink) return false;
            // 图形绘制模式下让正常绘制流程处理（不拦截为拖动）
            if (drawingShapeMode != 0) return false;

            // 非 Ink/擦除/漫游模式（Select/Cursor 等）：阻止 InkCanvas 框选，启动鼠标拖动
            _isBoothMouseDragging = true;
            _boothMouseDragStartOrigin = e.GetPosition(VideoPresenterSpecialModeContainer);
            _boothMouseDragStartTranslateX = _boothPreviewTranslateX;
            _boothMouseDragStartTranslateY = _boothPreviewTranslateY;
            // 初始化上一帧 translate，用于增量计算
            _boothMouseLastTranslateX = _boothPreviewTranslateX;
            _boothMouseLastTranslateY = _boothPreviewTranslateY;

            // 临时切到 None 防止 MouseMove 触发 InkCanvas 内部框选
            if (inkCanvas.EditingMode != InkCanvasEditingMode.None)
            {
                _boothTouchSavedInkEditingMode = inkCanvas.EditingMode;
                inkCanvas.EditingMode = InkCanvasEditingMode.None;
            }
            // 捕获鼠标，确保 Move/Up 事件能收到
            inkCanvas.CaptureMouse();
            e.Handled = true;
            return true;
        }

        /// <summary>鼠标移动处理：每帧计算与上一帧的差值，同步移动预览画面和墨迹（增量方式）。</summary>
        private void VideoPresenterSpecialMode_HandleMouseMove(MouseEventArgs e)
        {
            if (!_isBoothMouseDragging) return;

            try
            {
                var currentPos = e.GetPosition(VideoPresenterSpecialModeContainer);
                double newTranslateX = _boothMouseDragStartTranslateX + (currentPos.X - _boothMouseDragStartOrigin.X);
                double newTranslateY = _boothMouseDragStartTranslateY + (currentPos.Y - _boothMouseDragStartOrigin.Y);

                // 计算增量
                double deltaX = newTranslateX - _boothMouseLastTranslateX;
                double deltaY = newTranslateY - _boothMouseLastTranslateY;

                _boothPreviewTranslateX = newTranslateX;
                _boothPreviewTranslateY = newTranslateY;
                _boothMouseLastTranslateX = newTranslateX;
                _boothMouseLastTranslateY = newTranslateY;
                ApplyBoothPreviewTransform();

                // 同步平移 inkCanvas 上的墨迹（增量方式）
                if (Math.Abs(deltaX) > 0.001 || Math.Abs(deltaY) > 0.001)
                {
                    var translateMatrix = new Matrix();
                    translateMatrix.Translate(deltaX, deltaY);
                    try
                    {
                        foreach (var stroke in inkCanvas.Strokes)
                        {
                            stroke.Transform(translateMatrix, false);
                        }
                        // 同步变换历史中不在画布上的笔画（如形状识别的 ReplacedStroke）
                        timeMachine?.TransformStrokesInHistory(translateMatrix, inkCanvas.Strokes);
                        // 墨迹位置已改变，旋转基准过期，重置以便下次旋转重新保存
                        ResetRotationBaseline();
                    }
                    catch { }
                }
            }
            catch { }
        }

        /// <summary>鼠标松开处理：结束拖动，恢复 EditingMode。</summary>
        private void VideoPresenterSpecialMode_HandleMouseUp(MouseButtonEventArgs e)
        {
            if (!_isBoothMouseDragging) return;

            _isBoothMouseDragging = false;
            inkCanvas?.ReleaseMouseCapture();

            // 恢复用户选择的 EditingMode
            if (_boothTouchSavedInkEditingMode.HasValue && inkCanvas != null)
            {
                try
                {
                    inkCanvas.EditingMode = _boothTouchSavedInkEditingMode.Value;
                }
                catch { }
                _boothTouchSavedInkEditingMode = null;
            }
            e.Handled = true;
        }

        /// <summary>
        /// 在 inkCanvas 像素坐标系下，以 origin 为锚点对每条 Stroke 应用 ratio 倍缩放。
        /// 注意：因为预览图和 inkCanvas 在同一容器内，origin 直接用容器坐标即可，
        /// inkCanvas 与容器尺寸一致时坐标无需转换。
        /// </summary>
        private void ScaleInkCanvasStrokes(System.Windows.Point origin, double ratio)
        {
            if (inkCanvas == null || inkCanvas.Strokes.Count == 0) return;
            try
            {
                // inkCanvas 与 VideoPresenterSpecialModeContainer 在同一尺寸下，直接转换坐标
                var inkOrigin = inkCanvas.PointFromScreen(VideoPresenterSpecialModeContainer.PointToScreen(origin));
                var matrix = new System.Windows.Media.Matrix();
                matrix.ScaleAt(ratio, ratio, inkOrigin.X, inkOrigin.Y);
                inkCanvas.Strokes.Transform(matrix, false);
                // 同步变换历史中不在画布上的笔画（如形状识别的 ReplacedStroke）
                timeMachine?.TransformStrokesInHistory(matrix, inkCanvas.Strokes);
                // 墨迹大小已改变，旋转基准过期，重置以便下次旋转重新保存
                ResetRotationBaseline();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ScaleInkCanvasStrokes 异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 为当前白板页开始指定摄像头的预览并保存该页的摄像头选择。
        /// </summary>
        /// <param name="cameraIndex">要启动的摄像头在设备列表中的索引。</param>
        /// <remarks>预览成功时会允许拍照按钮可用，并刷新分辨率 ComboBox。
        /// 特殊模式下用 VideoCaptureElement 直接 D3D 渲染（VMR9 + D3DImage 零拷贝）；
        /// 非特殊模式（如旧版"上屏"）回退到 _cameraService。</remarks>
        private async void StartVideoPresenterPreview(int cameraIndex)
        {
            try
            {
                EnsureCameraService();
                _cameraIndexByPage[GetCurrentPageIndex()] = cameraIndex;
                LogHelper.WriteLogToFile(
                    $"StartVideoPresenterPreview: cameraIndex={cameraIndex}，IsCapturing={_cameraService.IsCapturing}，SpecialMode={_isVideoPresenterSpecialMode}",
                    LogHelper.LogType.Info);

                if (_isVideoPresenterSpecialMode)
                {
                    // 特殊模式：直接启动 VideoCaptureElement（DirectShow + VMR9 + D3DImage 零拷贝）
                    bool ok = await StartVideoCaptureElementPreviewAsync(cameraIndex);
                    if (ok)
                    {
                        // 不在此处启用拍照按钮 —— Play() 返回 true 不代表 D3DImage 已分配表面，
                        // 此时 CopyBackBuffer 仍可能返回 null。改为在 MediaOpened 事件中启用按钮
                        // （VideoPresenterFullCanvasImage_MediaOpened 已实现）。
                        RefreshBoothResolutionComboBox();
                    }
                    else
                    {
                        LogHelper.WriteLogToFile(
                            "StartVideoPresenterPreview: VideoCaptureElement 启动失败",
                            LogHelper.LogType.Warning);
                    }
                }
                else
                {
                    // 非特殊模式：回退到 _cameraService（旧"上屏"路径）
                    if (await _cameraService.StartPreviewAsync(cameraIndex))
                    {
                        if (BtnCapturePhoto != null) BtnCapturePhoto.IsEnabled = true;
                        RefreshBoothResolutionComboBox();
                    }
                    else
                    {
                        LogHelper.WriteLogToFile(
                            "StartVideoPresenterPreview: StartPreview 返回 false",
                            LogHelper.LogType.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"启动视频展台预览失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 启动 VideoCaptureElement 预览（DirectShow + VMR9 Renderless + D3DImage 自动渲染）。
        /// 返回是否成功启动。所有调用必须在 UI 线程上。
        /// </summary>
        private Task<bool> StartVideoCaptureElementPreviewAsync(int cameraIndex)
        {
            // 不要在这里隐藏 SearchingText：
            // Play() 后到 MediaOpened 触发前有 500-1500ms 的 D3DImage 分配/FilterGraph 构建延迟，
            // 这段时间用户看到的是纯深色背景，没有任何提示，会以为卡住了。
            // 改为把提示文字改为"正在启动摄像头..."（设备已找到，只是启动预览），
            // 只在 MediaOpened 事件触发时（画面真的出来）才隐藏提示。
            if (VideoPresenterSearchingText != null)
            {
                VideoPresenterSearchingText.Text = "正在启动摄像头...";
                VideoPresenterSearchingText.Visibility = Visibility.Visible;
            }

            if (VideoPresenterFullCanvasImage == null)
            {
                LogHelper.WriteLogToFile("[VideoPresenter] StartVideoCaptureElementPreviewAsync: VideoPresenterFullCanvasImage 为 null", LogHelper.LogType.Warning);
                return Task.FromResult(false);
            }
            if (cameraIndex < 0 || cameraIndex >= _cameraService.AvailableCameras.Count)
            {
                LogHelper.WriteLogToFile(
                    $"[VideoPresenter] StartVideoCaptureElementPreviewAsync: cameraIndex={cameraIndex} 越界（AvailableCameras.Count={_cameraService.AvailableCameras.Count}）",
                    LogHelper.LogType.Warning);
                return Task.FromResult(false);
            }

            // 确保 VideoCaptureElement 可见：
            // InsertPhotoToCanvas 拍照后会把 VideoCaptureElement.Visibility 设为 Collapsed（替换为冻结照片）。
            // 如果用户在冻结状态下直接切换摄像头/分辨率，而不先点击侧栏缩略图返回实时画面，
            // VideoCaptureElement 仍是 Collapsed —— 即使 StartVideoCaptureElementPreviewAsync 成功启动预览、
            // MediaOpened 触发，用户也看不到画面（被 Collapsed 隐藏）。
            // 这里在启动预览前强制恢复可见性，并清除冻结状态，确保预览画面能显示。
            if (VideoPresenterFullCanvasImage.Visibility != Visibility.Visible)
            {
                LogHelper.WriteLogToFile(
                    "[VideoPresenter] StartVideoCaptureElementPreviewAsync: VideoCaptureElement 当前不可见，先清除冻结画面并恢复可见性",
                    LogHelper.LogType.Info);
                ClearFrozenFrame();
                VideoPresenterFullCanvasImage.Visibility = Visibility.Visible;
                // 重置缩放/平移为默认状态（照片上的缩放不继承到实时画面）
                _boothPreviewScale = 1.0;
                _boothPreviewTranslateX = 0;
                _boothPreviewTranslateY = 0;
                ApplyBoothPreviewTransform();
            }

            var tcs = new TaskCompletionSource<bool>();
            Dispatcher.BeginInvoke(new Action(() =>
            {
                // 再次设置提示文字（保证 BeginInvoke 排队期间不会被其他代码改回）
                if (VideoPresenterSearchingText != null)
                {
                    VideoPresenterSearchingText.Text = "正在启动摄像头...";
                    VideoPresenterSearchingText.Visibility = Visibility.Visible;
                }

                try
                {
                    // 用 MonikerString 匹配 MultimediaUtil 中的 DsDevice，避免两套枚举顺序不一致
                    // （_cameraService 用 DsDevice.GetDevicesOfCat，MultimediaUtil 也用同一 API，
                    //  但顺序不能假设一致；通过 MonikerString/DevicePath 精确匹配更稳）
                    var curCamera = _cameraService.AvailableCameras[cameraIndex];
                    var devices = MultimediaUtil.VideoInputDevices;
                    DsDevice device = null;
                    for (int i = 0; i < devices.Length; i++)
                    {
                        if (string.Equals(devices[i].DevicePath, curCamera.MonikerString, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(devices[i].Name, curCamera.Name, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(devices[i].Name, curCamera.MonikerString, StringComparison.OrdinalIgnoreCase))
                        {
                            device = devices[i];
                            break;
                        }
                    }
                    if (device == null && cameraIndex < devices.Length)
                    {
                        // 索引兜底
                        device = devices[cameraIndex];
                    }
                    if (device == null)
                    {
                        LogHelper.WriteLogToFile(
                            $"[VideoPresenter] StartVideoCaptureElementPreviewAsync: 找不到摄像头 {curCamera.Name}（MonikerString={curCamera.MonikerString}），MultimediaUtil 共 {devices.Length} 项",
                            LogHelper.LogType.Warning);
                        tcs.SetResult(false);
                        return;
                    }

                    // 若已在播放同一设备，先停止
                    try { VideoPresenterFullCanvasImage.Stop(); } catch { }
                    // 关键：显式将 VideoCaptureDevice 置为 null，强制依赖属性变化回调触发 CleanUp()。
                    // 否则后续 VideoCaptureDevice = device（同一个 DsDevice 引用）会被 WPF 依赖属性系统判定为
                    // "未变化"，OnVideoCaptureDeviceChanged 不触发，WPFMediaKit 不会重建 FilterGraph，
                    // 导致切换分辨率/帧率时 DesiredPixelWidth/Height/FPS 不生效（仍是旧分辨率）。
                    try { VideoPresenterFullCanvasImage.VideoCaptureDevice = null; } catch { }
                    // 等待 FilterGraph 完全释放设备占用：
                    // WPFMediaKit 的 Stop() + CleanUp() 是异步的，FilterGraph 释放需要时间。
                    // 立即 BeginInit/EndInit/Play 会导致设备仍被占用，新图构建失败，
                    // MediaOpened 不触发，SearchingText 卡在"正在启动摄像头..."。
                    // 用 DispatcherTimer 异步延迟 250ms，避免阻塞 UI 线程。
                    var stopDelayTimer = new System.Windows.Threading.DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(250)
                    };
                    stopDelayTimer.Tick += (senderDelay, argsDelay) =>
                    {
                        stopDelayTimer.Stop();
                        try
                        {
                            // ISupportInitialize 模式设置设备和参数
                            // 此时 VideoCaptureDevice 已为 null，下面 device 赋值会触发
                            // OnVideoCaptureDeviceChanged（null → device），强制 Setup() 重建 FilterGraph，
                            // 并使用当前 DesiredPixelWidth/Height/FPS 进行新图协商。
                            ((System.ComponentModel.ISupportInitialize)VideoPresenterFullCanvasImage).BeginInit();
                            VideoPresenterFullCanvasImage.VideoCaptureDevice = device;
                            // EnableSampleGrabbing=false：VMR9 Renderless 模式下 SampleGrabber 无法连入图，
                            //  启用反而可能导致图构建失败或 NewVideoSample 不触发。
                            //  拍照路径改用 CaptureCurrentFrame() 从 D3DImage.CopyBackBuffer 直接拿帧。
                            VideoPresenterFullCanvasImage.EnableSampleGrabbing = false;
                            VideoPresenterFullCanvasImage.UseYuv = false;
                            VideoPresenterFullCanvasImage.LoadedBehavior = WpfMediaKitMediaState.Manual;

                            // 应用当前选中的 native capability（W,H,FPS 组合）
                            int selIdx = _cameraService?.SelectedResolutionIndex ?? -1;
                            var resolutions = _cameraService?.NativeResolutions;
                            if (selIdx >= 0 && selIdx < (resolutions?.Count ?? 0))
                            {
                                var r = resolutions[selIdx];
                                VideoPresenterFullCanvasImage.DesiredPixelWidth = r.Width;
                                VideoPresenterFullCanvasImage.DesiredPixelHeight = r.Height;
                                VideoPresenterFullCanvasImage.FPS = r.FrameRate > 0 ? r.FrameRate : 30;
                            }
                            else
                            {
                                // 默认 1280×720@30
                                VideoPresenterFullCanvasImage.DesiredPixelWidth = 1280;
                                VideoPresenterFullCanvasImage.DesiredPixelHeight = 720;
                                VideoPresenterFullCanvasImage.FPS = 30;
                            }

                            // 同步旋转（LayoutTransform）—— 旋转角度跟随 _cameraService.RotationAngle
                            if (VideoPresenterFullCanvasRotation != null && _cameraService != null)
                            {
                                VideoPresenterFullCanvasRotation.Angle = _cameraService.RotationAngle * 90.0;
                            }

                            ((System.ComponentModel.ISupportInitialize)VideoPresenterFullCanvasImage).EndInit();
                            VideoPresenterFullCanvasImage.Play();

                            // 拍照按钮启用策略（双重保险）：
                            // 主路径：MediaOpened 事件在 D3D 表面分配完成时触发，立即启用按钮
                            // 兜底路径：Play() 后 1500ms 检查 —— 防止 MediaOpened 在某些场景不触发
                            // （WPFMediaKit 的 VideoCaptureElement 在 Stop→Play 快速切换、Visibility 变更、
                            //   设备抢占恢复等情况下可能跳过 MediaOpened 事件，导致按钮一直禁用 + SearchingText 卡住）
                            // 延迟时长 1500ms 足以让 D3DImage 完成分配（实测 < 500ms）+ 设备驱动恢复缓冲
                            // 兜底逻辑：
                            //   - 如果 SearchingText 已隐藏（MediaOpened 触发过）→ 只启用按钮
                            //   - 如果 SearchingText 仍可见（MediaOpened 没触发，预览可能卡住）→ 重试一次重启
                            var btnRef = BtnCapturePhoto;
                            if (btnRef != null)
                            {
                                var fallbackTimer = new System.Windows.Threading.DispatcherTimer
                                {
                                    Interval = TimeSpan.FromMilliseconds(1500)
                                };
                                fallbackTimer.Tick += (senderFallback, argsFallback) =>
                                {
                                    try
                                    {
                                        fallbackTimer.Stop();
                                        if (VideoPresenterFullCanvasImage == null
                                            || VideoPresenterFullCanvasImage.Visibility != Visibility.Visible)
                                            return;

                                        // MediaOpened 已触发：SearchingText 已隐藏，只需启用按钮
                                        if (VideoPresenterSearchingText != null
                                            && VideoPresenterSearchingText.Visibility != Visibility.Visible)
                                        {
                                            btnRef.IsEnabled = true;
                                            return;
                                        }

                                        // MediaOpened 没触发（SearchingText 仍可见）：重试一次重启预览
                                        // 首次 Stop→Play 可能因为设备占用/FILTER_GRAPH 状态不稳导致 MediaOpened 丢失
                                        LogHelper.WriteLogToFile(
                                            "[VideoPresenter] 兜底定时器：MediaOpened 1.5s 未触发，重试重启预览",
                                            LogHelper.LogType.Warning);
                                        int camIdx = FindCurrentCameraIndex();
                                        if (camIdx >= 0)
                                        {
                                            _ = StartVideoCaptureElementPreviewAsync(camIdx).ConfigureAwait(false);
                                        }
                                    }
                                    catch { }
                                };
                                fallbackTimer.Start();
                            }

                            LogHelper.WriteLogToFile(
                                $"[VideoPresenter] StartVideoCaptureElementPreviewAsync 成功: {curCamera.Name}, selIdx={selIdx}, resolutions={resolutions?.Count ?? 0}",
                                LogHelper.LogType.Info);

                            tcs.SetResult(true);
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLogToFile($"StartVideoCaptureElementPreviewAsync 异常: {ex.Message}",
                                LogHelper.LogType.Error);
                            ErrorOccurredRelay?.Invoke(this, $"VideoCaptureElement 启动失败: {ex.Message}");
                            tcs.SetResult(false);
                        }
                    };
                    stopDelayTimer.Start();
                    return; // stopDelayTimer.Tick 内会设置 tcs.SetResult
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"StartVideoCaptureElementPreviewAsync 异常: {ex.Message}",
                        LogHelper.LogType.Error);
                    ErrorOccurredRelay?.Invoke(this, $"VideoCaptureElement 启动失败: {ex.Message}");
                    tcs.SetResult(false);
                }
            }));
            return tcs.Task;
        }

        /// <summary>错误转发事件（供内部使用）。</summary>
        private event EventHandler<string> ErrorOccurredRelay;

        // 「上屏」按钮（BtnToggleVideoPresenterLiveOnCanvas）已移除：
        // 视频展台特殊模式（EnterVideoPresenterSpecialMode）会自动把预览铺满整张白板，
        // 不再需要手动在画布上添加一个小的实时画面 Image 元素。
        // 下列按页绑定字段（_liveFrameImageByPage / _liveEnabledPages / _liveFrameLayoutByPage / _cameraIndexByPage）
        // 及辅助方法（EnsureLiveFrameElementForPage / ApplyLiveFrameLayoutForPage /
        // VideoPresenter_BeforePageLeave / VideoPresenter_OnPageChanged 中的相关分支）保留为空运行：
        // _liveEnabledPages 永远为空集，对应分支自然不会执行，便于将来回滚或迁移。

        private async void StartVideoPresenterPreviewForCurrentPageIfNeeded()
        {
            try
            {
                EnsureCameraService();
                if (_cameraService == null || _cameraService.IsCapturing) return;
                // 特殊模式下预览由 VideoCaptureElement 接管，不应再启动 _cameraService（会抢占摄像头）
                if (_isVideoPresenterSpecialMode) return;

                int page = GetCurrentPageIndex();
                int idx = 0;
                if (_cameraIndexByPage.TryGetValue(page, out int savedIdx))
                {
                    idx = savedIdx;
                }

                if (_cameraService.AvailableCameras == null || _cameraService.AvailableCameras.Count == 0)
                {
                    await _cameraService.RefreshCameraListAsync();
                }

                if (_cameraService.AvailableCameras == null || _cameraService.AvailableCameras.Count == 0)
                {
                    return;
                }

                idx = Math.Max(0, Math.Min(idx, _cameraService.AvailableCameras.Count - 1));
                await _cameraService.StartPreviewAsync(idx);
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
        /// 「上屏」按钮已移除（特殊模式自动铺满画布）；这里只保留按页摄像头索引恢复逻辑：
        /// 在展台侧栏可见时，切页后自动切回该页保存的摄像头。
        /// </remarks>
        private void VideoPresenter_OnPageChanged()
        {
            try
            {
                int page = GetCurrentPageIndex();

                // 按页摄像头索引：仅在展台菜单可见时，切页后自动切回该页的摄像头
                if (BoothPopup?.IsOpen == true
                    && _cameraIndexByPage.TryGetValue(page, out int idx))
                {
                    EnsureCameraService();
                    _ = _cameraService?.StartPreviewAsync(idx);
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

                bool correctionEnabled = Settings?.Automation?.IsEnablePhotoCorrection == true;
                int rotationAngle = _cameraService?.RotationAngle ?? 0;
                LogHelper.WriteLogToFile(
                    $"视频展台拍照: 开始 (矫正开关={correctionEnabled}, 旋转角度={rotationAngle * 90}°, 特殊模式={_isVideoPresenterSpecialMode})",
                    LogHelper.LogType.Trace);

                // 两条拍照路径（互为兜底）：
                //   1. _lastFrame：由 NewVideoSample 事件（SampleGrabber）填充 —— 可能不触发
                //   2. CaptureCurrentFrame()：从 D3DImage.CopyBackBuffer 拿 BitmapSource（GPU 内存拷贝）
                // 优先用 _lastFrame（System.Drawing.Bitmap，已应用旋转）；
                // 若为 null（NewVideoSample 未触发），用 CaptureCurrentFrame 直接拿 BitmapSource。
                Bitmap frame = null;
                lock (_videoPresenterFrameLock)
                {
                    if (_lastFrame != null)
                        frame = (Bitmap)_lastFrame.Clone();
                }

                BitmapSource fallbackBitmapSource = null;
                if (frame == null && _isVideoPresenterSpecialMode)
                {
                    // SampleGrabber 路径未填充 _lastFrame，尝试直接从 D3DImage 拿帧
                    fallbackBitmapSource = VideoPresenterFullCanvasImage?.CaptureCurrentFrame();
                    if (fallbackBitmapSource == null)
                    {
                        LogHelper.WriteLogToFile(
                            "视频展台拍照: _lastFrame 为 null 且 D3DImage 不可用，无法拍照",
                            LogHelper.LogType.Warning);
                        // 给用户可见反馈（按钮可能被过早点击，或预览未真正就绪）
                        ShowBoothTransientMessage("预览未就绪，请稍后再试");
                        return;
                    }
                    LogHelper.WriteLogToFile(
                        $"视频展台拍照: 走路径2(D3DImage) BitmapSource={fallbackBitmapSource.PixelWidth}x{fallbackBitmapSource.PixelHeight}",
                        LogHelper.LogType.Trace);
                }
                else if (frame == null)
                {
                    return;
                }
                else
                {
                    LogHelper.WriteLogToFile(
                        $"视频展台拍照: 走路径1(_lastFrame) Bitmap={frame.Width}x{frame.Height}",
                        LogHelper.LogType.Trace);
                }

                Task.Run(() =>
                {
                    try
                    {
                        // 统一两条路径：都拿到一个 System.Drawing.Bitmap (toSave)，再做矫正，最后转 BitmapImage
                        Bitmap toSave;
                        bool ownsToSave; // toSave 是否需要 Dispose（frame 由调用方 Dispose，矫正产物需要 Dispose）

                        if (frame != null)
                        {
                            // 路径 1：使用 _lastFrame（System.Drawing.Bitmap，已应用旋转）
                            toSave = frame;
                            ownsToSave = false;
                        }
                        else
                        {
                            // 路径 2：D3DImage → BitmapSource → Bitmap
                            // 应用旋转（D3DImage 是预览状态，未经过 LayoutTransform 旋转）
                            BitmapSource directBitmapSource = fallbackBitmapSource;
                            if (rotationAngle != 0)
                            {
                                directBitmapSource = ApplyRotationToBitmapSource(directBitmapSource, rotationAngle);
                            }
                            if (directBitmapSource == null)
                            {
                                LogHelper.WriteLogToFile("视频展台拍照: 路径2 旋转后 BitmapSource 为 null", LogHelper.LogType.Warning);
                                return;
                            }
                            toSave = BitmapSourceToBitmap(directBitmapSource);
                            if (toSave == null)
                            {
                                LogHelper.WriteLogToFile("视频展台拍照: 路径2 BitmapSource→Bitmap 转换失败", LogHelper.LogType.Warning);
                                return;
                            }
                            ownsToSave = true;
                            LogHelper.WriteLogToFile(
                                $"视频展台拍照: 路径2 转换为 Bitmap={toSave.Width}x{toSave.Height}",
                                LogHelper.LogType.Trace);
                        }

                        // 照片矫正（两条路径统一）：检测纸张角点 → 透视矫正
                        bool corrected = false;
                        if (correctionEnabled)
                        {
                            if (TryDetectPaperCorners(toSave, out List<OpenCvSharp.Point> corners))
                            {
                                var correctedBitmap = ApplyPerspectiveCorrection(toSave, corners);
                                if (correctedBitmap != null)
                                {
                                    if (ownsToSave) toSave.Dispose();
                                    toSave = correctedBitmap;
                                    ownsToSave = true;
                                    corrected = true;
                                }
                                else
                                {
                                    LogHelper.WriteLogToFile("照片矫正: ApplyPerspectiveCorrection 返回 null，使用原图", LogHelper.LogType.Trace);
                                }
                            }
                            else
                            {
                                LogHelper.WriteLogToFile("照片矫正: TryDetectPaperCorners 未检测到纸张，使用原图", LogHelper.LogType.Trace);
                            }
                        }

                        var bmpImage = ConvertBitmapToBitmapImage(toSave);
                        if (ownsToSave) toSave.Dispose();
                        if (frame != null) frame.Dispose();

                        if (bmpImage == null)
                        {
                            LogHelper.WriteLogToFile("视频展台拍照: Bitmap→BitmapImage 转换失败", LogHelper.LogType.Warning);
                            return;
                        }

                        LogHelper.WriteLogToFile(
                            $"视频展台拍照: 完成 输出 BitmapImage={bmpImage.PixelWidth}x{bmpImage.PixelHeight} (矫正应用={corrected})",
                            LogHelper.LogType.Trace);

                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            var ci = new CapturedImage(bmpImage);
                            // 追加到列表末尾：让照片按拍摄时间顺序排列，
                            // 第一张拍 → 1/1，第二张拍 → 2/2（新照片在最后，符合自然页码递增直觉）
                            _capturedPhotos.Add(ci);

                            while (_capturedPhotos.Count > MaxCapturedPhotos)
                            {
                                _capturedPhotos.RemoveAt(0);
                            }

                            // 视频展台特殊模式：直接把照片插入到白板右下角页码预览（RefreshBoothPageListView），
                            // 不再走已废弃的侧栏照片列表（UpdateCapturedPhotosDisplay / CapturedPhotosStackPanel）
                            if (_isVideoPresenterSpecialMode)
                                InsertPhotoToCanvas(ci);
                        }));
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
        /// 对 BitmapSource 应用旋转（0/90/180/270）。
        /// 用于从 D3DImage 直接拍照时（绕过 LayoutTransform，需要手动应用旋转）。
        /// </summary>
        private static BitmapSource ApplyRotationToBitmapSource(BitmapSource src, int rotationAngle)
        {
            if (src == null) return null;
            try
            {
                var rotated = new TransformedBitmap(src, new RotateTransform(rotationAngle * 90.0));
                rotated.Freeze();
                return rotated;
            }
            catch { return src; }
        }

        /// <summary>
        /// 把 BitmapSource 转 BitmapImage（PNG 编码再解码，可跨线程冻结）。
        /// 用于 D3DImage 拍照路径：CopyBackBuffer 返回 BitmapSource，
        /// 但 CapturedImage 构造函数需要 BitmapImage。
        /// </summary>
        private static BitmapImage ConvertBitmapSourceToBitmapImage(BitmapSource src)
        {
            if (src == null) return null;
            try
            {
                using (var ms = new MemoryStream())
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(src));
                    encoder.Save(ms);
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
            catch { return null; }
        }

        /// <summary>
/// 把 WPF BitmapSource 转换为 System.Drawing.Bitmap（直接像素拷贝，无 PNG 编解码）。
        /// 用于 D3DImage 拍照路径需要做照片矫正时：OpenCvSharp 的 BitmapConverter.ToMat 只接受 System.Drawing.Bitmap。
        /// 性能：相比 PNG 编解码省 15-25ms（4K 帧从 ~30ms 降到 ~5ms），实时检测关键路径。
        /// 原理：BitmapSource.CopyPixels 直接拷贝 GPU→CPU 的像素数据，用 Stride 对齐构造 Bitmap。
        /// </summary>
        private static Bitmap BitmapSourceToBitmap(BitmapSource src)
        {
            if (src == null) return null;
            try
            {
                int w = src.PixelWidth;
                int h = src.PixelHeight;
                if (w <= 0 || h <= 0) return null;

                // 统一到 BGRA 32bpp（OpenCvSharp BitmapConverter.ToMat 期望的格式，支持 4 通道）
                var dstFormat = System.Drawing.Imaging.PixelFormat.Format32bppPArgb;
                int bytesPerPixel = 4;
                int stride = w * bytesPerPixel;
                byte[] pixels = new byte[stride * h];

                // CopyPixels 会按 src 的格式填充，需要用 BitmapSource.Create 转格式或直接用合适的 stride
                // 若 src 不是 32bpp，先转成 32bpp Bgra 再拷贝
                if (src.Format != System.Windows.Media.PixelFormats.Bgra32 &&
                    src.Format != System.Windows.Media.PixelFormats.Pbgra32)
                {
                    // 转格式：用 FormatConvertedBitmap（不拷贝大数据，只在读取时转换）
                    var converted = new FormatConvertedBitmap(src, System.Windows.Media.PixelFormats.Bgra32, null, 0);
                    converted.Freeze();
                    converted.CopyPixels(pixels, stride, 0);
                }
                else
                {
                    src.CopyPixels(pixels, stride, 0);
                }

                // 构造 Bitmap：直接用像素数组，不再走 PNG 编解码
                // 注意：PArgb 与 Bgra 在内存布局上一致，只是 alpha 预乘语义不同；
                //       OpenCvSharp ToMat 会按 Format32bppPArgb 处理，CvtColor BGRA2GRAY 能正确工作
                var bmp = new Bitmap(w, h, dstFormat);
                var data = bmp.LockBits(
                    new System.Drawing.Rectangle(0, 0, w, h),
                    System.Drawing.Imaging.ImageLockMode.WriteOnly,
                    dstFormat);
                try
                {
                    Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
                }
                finally
                {
                    bmp.UnlockBits(data);
                }
                return bmp;
            }
            catch { return null; }
        }

        /// <summary>
        /// 旋转按钮入口：把预览 LayoutTransform 与画布墨迹一起转到目标角度，
        /// 走基准重放管线避免持续缩小。冻结照片先回退到实时画面再旋转。
        /// </summary>
        private void HandleBoothRotation(double targetAngleDegrees)
        {
            if (!_isVideoPresenterSpecialMode || VideoPresenterFullCanvasRotation == null)
                return;

            // 冻结照片内容已被 RotateFlip 转正，再用 LayoutTransform 旋转会双重旋转；先回实时画面。
            if (VideoPresenterFrozenFrameImage != null
                && VideoPresenterFrozenFrameImage.Visibility == Visibility.Visible)
            {
                ClearFrozenFrame();
                if (VideoPresenterFullCanvasImage != null)
                {
                    VideoPresenterFullCanvasImage.Visibility = Visibility.Visible;
                    int page = GetCurrentPageIndex();
                    int camIdx = -1;
                    if (_cameraIndexByPage.TryGetValue(page, out int savedIdx)
                        && savedIdx >= 0 && savedIdx < _cameraService.AvailableCameras.Count)
                        camIdx = savedIdx;
                    if (camIdx < 0 && _cameraService.AvailableCameras.Count > 0)
                        camIdx = 0;
                    if (camIdx >= 0)
                        _ = StartVideoCaptureElementPreviewAsync(camIdx);
                }
            }

            EnsureRotationBaseline();
            VideoPresenterFullCanvasRotation.Angle = targetAngleDegrees;
            RotateBoothStrokesFromBaseline(targetAngleDegrees);

            if (VideoPresenterFrozenFrameRotation != null)
                VideoPresenterFrozenFrameRotation.Angle = 0;

            lock (_videoPresenterFrameLock)
            {
                _lastFrame?.Dispose();
                _lastFrame = null;
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
                // 图片预览状态：直接旋转冻结照片，不返回直播
                // （冻结照片内容已通过拍照时 RotateFlip 旋转到正向，这里仅旋转 LayoutTransform 即可）
                // 照片预览页已共享 VideoPresenterFullCanvasRotation，旋转它即可同时旋转照片。
                if (_isVideoPresenterSpecialMode
                    && VideoPresenterFrozenFrameImage != null
                    && VideoPresenterFrozenFrameImage.Visibility == Visibility.Visible
                    && VideoPresenterFullCanvasRotation != null)
                {
                    // 与直播页逻辑完全一致：只设 Rotation.Angle，不重置缩放/平移。
                    // 照片用 FillImage 填满容器，LayoutTransform 旋转后 WPF 自动用交换的 constraint 重算 fit，
                    // 行为与 VideoCaptureElement 一致。
                    double beforeAngle = VideoPresenterFullCanvasRotation.Angle;
                    EnsureRotationBaseline();
                    // 诊断日志：旋转前状态
                    try
                    {
                        double containerW = VideoPresenterSpecialModeContainer?.ActualWidth ?? 0;
                        double containerH = VideoPresenterSpecialModeContainer?.ActualHeight ?? 0;
                        double imgW = 0, imgH = 0;
                        if (VideoPresenterFrozenFrameImage.Source is BitmapSource bs)
                        { imgW = bs.PixelWidth; imgH = bs.PixelHeight; }
                        LogHelper.WriteLogToFile(
                            $"[BoothDiag] BtnRotate BEFORE: beforeAngle={beforeAngle}, " +
                            $"container={containerW}x{containerH}, imgSize={imgW}x{imgH}, " +
                            $"baselineAngle={_rotationBaselineAngle}, baselineStrokes={_rotationBaselineStrokes?.Count ?? 0}, " +
                            $"canvasStrokes={inkCanvas?.Strokes?.Count ?? 0}, " +
                            $"scale={_boothPreviewScale}, translate=({_boothPreviewTranslateX},{_boothPreviewTranslateY})",
                            LogHelper.LogType.Info);
                    }
                    catch { }
                    VideoPresenterFullCanvasRotation.Angle = (VideoPresenterFullCanvasRotation.Angle + 90.0) % 360.0;
                    // 传视觉角度（cameraRotation*90 + Angle）给墨迹变换，
                    // 因为 GetBoothRotationMatrix 按"实时画面 0° 基准"算缩放，照片预览需用视觉角度
                    RotateBoothStrokesFromBaseline(GetBoothVisualAngle());
                    // 诊断日志：旋转后状态
                    try
                    {
                        LogHelper.WriteLogToFile(
                            $"[BoothDiag] BtnRotate AFTER: afterAngle={VideoPresenterFullCanvasRotation.Angle}, " +
                            $"photo.ActualSize={VideoPresenterFrozenFrameImage.ActualWidth}x{VideoPresenterFrozenFrameImage.ActualHeight}",
                            LogHelper.LogType.Info);
                    }
                    catch { }
                    return;
                }

                EnsureCameraService();
                _cameraService.RotationAngle = (_cameraService.RotationAngle + 1) % 4;

                if (_isVideoPresenterSpecialMode)
                {
                    // 基准重放管线：墨迹与预览一起旋转/缩小，不会每转一次都叠加缩放。
                    HandleBoothRotation(_cameraService.RotationAngle * 90.0);
                    return;
                }

                // 旋转后清空 _lastFrame，下一帧会用新角度重新填充
                // （保证拍照时拿到的就是已旋转的帧）
                lock (_videoPresenterFrameLock)
                {
                    _lastFrame?.Dispose();
                    _lastFrame = null;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"视频展台旋转失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 在启用照片校正的切换按钮被选中时，将该偏好设置为开启并保存到设置文件。
        /// 同时启动实时识别框定时器，在直播画面上叠加 A4 纸检测框。
        /// </summary>
        private void ToggleBtnPhotoCorrection_Checked(object sender, RoutedEventArgs e)
        {
            if (Settings?.Automation == null) return;
            Settings.Automation.IsEnablePhotoCorrection = true;
            SaveSettingsToFile();
            StartPaperDetectTimer();
        }

        /// <summary>
        /// 关闭“相片校正”设置并将变更持久化到设置文件。
        /// 同时停止实时识别框定时器并隐藏覆盖层。
        /// </summary>
        private void ToggleBtnPhotoCorrection_Unchecked(object sender, RoutedEventArgs e)
        {
            if (Settings?.Automation == null) return;
            Settings.Automation.IsEnablePhotoCorrection = false;
            SaveSettingsToFile();
            StopPaperDetectTimer();
        }

        /// <summary>
        /// 从 Settings 同步加速模式 ComboBox 选中项。
        /// 因 WireUpBoothPopupContentEvents 早于 LoadSettings 执行，需在 BoothPopup 打开时补同步。
        /// SelectionChanged 处理函数内有 early return（Settings == newMode 时不保存），故设置 SelectedIndex 安全。
        /// </summary>
        private void SyncPhotoCorrectionAccelerationComboBox()
        {
            try
            {
                var cb = BoothPopupContent?.PhotoCorrectionAccelerationComboBox;
                if (cb == null) return;
                int accelIdx = (int)(Settings?.Automation?.PhotoCorrectionAcceleration ?? PhotoCorrectionAccelerationMode.Cpu);
                if (accelIdx >= 0 && accelIdx < cb.Items.Count && cb.SelectedIndex != accelIdx)
                    cb.SelectedIndex = accelIdx;
            }
            catch { }
        }

        // ====================================================================
        // 摄像头属性控制（手机专业模式）：IAMVideoProcAmp + IAMCameraControl
        // ====================================================================

        /// <summary>每个 BoothCameraProperty 对应的 (prop, 支持时的 tooltip key, 不支持时的 tooltip key)。</summary>
        private static readonly (BoothCameraProperty prop,
            string supportedTooltipKey, string notSupportedTooltipKey)[] BoothPropBindings =
        {
            (BoothCameraProperty.Brightness,   nameof(Ink_Canvas.Properties.BoothStrings.Brightness_Tooltip),   nameof(Ink_Canvas.Properties.BoothStrings.BrightnessNotSupported)),
            (BoothCameraProperty.Contrast,     nameof(Ink_Canvas.Properties.BoothStrings.Brightness_Tooltip),   nameof(Ink_Canvas.Properties.BoothStrings.PropNotSupported)),
            (BoothCameraProperty.Saturation,   nameof(Ink_Canvas.Properties.BoothStrings.Brightness_Tooltip),   nameof(Ink_Canvas.Properties.BoothStrings.PropNotSupported)),
            (BoothCameraProperty.WhiteBalance, nameof(Ink_Canvas.Properties.BoothStrings.WhiteBalance_Tooltip), nameof(Ink_Canvas.Properties.BoothStrings.PropNotSupported)),
            (BoothCameraProperty.Gain,         nameof(Ink_Canvas.Properties.BoothStrings.Gain_Tooltip),         nameof(Ink_Canvas.Properties.BoothStrings.PropNotSupported)),
            (BoothCameraProperty.Focus,        nameof(Ink_Canvas.Properties.BoothStrings.Focus_Tooltip),         nameof(Ink_Canvas.Properties.BoothStrings.PropNotSupported)),
            (BoothCameraProperty.Exposure,     nameof(Ink_Canvas.Properties.BoothStrings.Exposure_Tooltip),     nameof(Ink_Canvas.Properties.BoothStrings.PropNotSupported)),
        };

        /// <summary>取 BoothPopupContent 内指定属性对应的 Slider 控件。</summary>
        private Slider GetBoothPropSlider(BoothCameraProperty prop)
        {
            var content = BoothPopupContent;
            if (content == null) return null;
            switch (prop)
            {
                case BoothCameraProperty.Brightness:   return content.BrightnessSlider;
                case BoothCameraProperty.Contrast:     return content.ContrastSlider;
                case BoothCameraProperty.Saturation:   return content.SaturationSlider;
                case BoothCameraProperty.WhiteBalance: return content.WhiteBalanceSlider;
                case BoothCameraProperty.Gain:         return content.GainSlider;
                case BoothCameraProperty.Focus:        return content.FocusSlider;
                case BoothCameraProperty.Exposure:     return content.ExposureSlider;
            }
            return null;
        }

        /// <summary>取 BoothPopupContent 内指定属性对应的 ValueText 控件。</summary>
        private TextBlock GetBoothPropValueText(BoothCameraProperty prop)
        {
            var content = BoothPopupContent;
            if (content == null) return null;
            switch (prop)
            {
                case BoothCameraProperty.Brightness:   return content.BrightnessValueText;
                case BoothCameraProperty.Contrast:     return content.ContrastValueText;
                case BoothCameraProperty.Saturation:   return content.SaturationValueText;
                case BoothCameraProperty.WhiteBalance: return content.WhiteBalanceValueText;
                case BoothCameraProperty.Gain:         return content.GainValueText;
                case BoothCameraProperty.Focus:        return content.FocusValueText;
                case BoothCameraProperty.Exposure:     return content.ExposureValueText;
            }
            return null;
        }

        /// <summary>取 Settings.Canvas 中指定属性的当前值。</summary>
        private int GetBoothPropFromSettings(BoothCameraProperty prop)
        {
            var canvas = Settings?.Canvas;
            if (canvas == null) return 0;
            switch (prop)
            {
                case BoothCameraProperty.Brightness:   return canvas.VideoPresenterBrightness;
                case BoothCameraProperty.Contrast:     return canvas.VideoPresenterContrast;
                case BoothCameraProperty.Saturation:   return canvas.VideoPresenterSaturation;
                case BoothCameraProperty.WhiteBalance: return canvas.VideoPresenterWhiteBalance;
                case BoothCameraProperty.Gain:         return canvas.VideoPresenterGain;
                case BoothCameraProperty.Focus:        return canvas.VideoPresenterFocus;
                case BoothCameraProperty.Exposure:     return canvas.VideoPresenterExposure;
            }
            return 0;
        }

        /// <summary>写 Settings.Canvas 中指定属性的值。</summary>
        private void SetBoothPropToSettings(BoothCameraProperty prop, int value)
        {
            var canvas = Settings?.Canvas;
            if (canvas == null) return;
            switch (prop)
            {
                case BoothCameraProperty.Brightness:   canvas.VideoPresenterBrightness = value; break;
                case BoothCameraProperty.Contrast:     canvas.VideoPresenterContrast = value; break;
                case BoothCameraProperty.Saturation:   canvas.VideoPresenterSaturation = value; break;
                case BoothCameraProperty.WhiteBalance: canvas.VideoPresenterWhiteBalance = value; break;
                case BoothCameraProperty.Gain:         canvas.VideoPresenterGain = value; break;
                case BoothCameraProperty.Focus:        canvas.VideoPresenterFocus = value; break;
                case BoothCameraProperty.Exposure:     canvas.VideoPresenterExposure = value; break;
            }
        }

        /// <summary>
        /// 从 Settings 同步全部属性滑块值，并按摄像头支持情况启用/禁用滑块。
        /// 在 BoothPopup 打开时调用。
        /// </summary>
        private void SyncBoothBrightnessSlider()
        {
            try
            {
                _isBoothBrightnessSliderUpdating = true;
                try
                {
                    foreach (var b in BoothPropBindings)
                    {
                        var slider = GetBoothPropSlider(b.prop);
                        var valueText = GetBoothPropValueText(b.prop);
                        if (slider == null) continue;
                        int saved = GetBoothPropFromSettings(b.prop);
                        slider.Value = Math.Max(-100, Math.Min(100, saved));
                        if (valueText != null) valueText.Text = ((int)slider.Value).ToString();
                    }
                }
                finally
                {
                    _isBoothBrightnessSliderUpdating = false;
                }

                RefreshBoothPropSlidersEnabled();
            }
            catch { }
        }

        /// <summary>按 _cameraService 支持情况启用/禁用全部属性滑块并刷新 tooltip。</summary>
        private void RefreshBoothPropSlidersEnabled()
        {
            try
            {
                foreach (var b in BoothPropBindings)
                {
                    var slider = GetBoothPropSlider(b.prop);
                    if (slider == null) continue;
                    bool supported = _cameraService?.IsCameraPropertySupported(b.prop) ?? false;
                    slider.IsEnabled = supported;
                    slider.ToolTip = supported
                        ? (object)Ink_Canvas.Properties.BoothStrings.ResourceManager.GetString(b.supportedTooltipKey, Ink_Canvas.Properties.BoothStrings.Culture)
                        : Ink_Canvas.Properties.BoothStrings.ResourceManager.GetString(b.notSupportedTooltipKey, Ink_Canvas.Properties.BoothStrings.Culture);
                }
            }
            catch { }
        }

        /// <summary>
        /// 探测当前摄像头支持的属性，把保存的属性值应用到硬件，并刷新滑块启用状态。
        /// 在摄像头枚举/切换后调用。FilterGraphNoThread 不抢占设备，可与 VideoCaptureElement 流式预览共存。
        /// </summary>
        private async Task ProbeAndApplyBoothBrightnessAsync()
        {
            if (_cameraService == null) return;
            try
            {
                // 把保存的属性值同步到 service（探测成功后 setter 会触发应用；这里也显式 Apply 一次）
                foreach (var b in BoothPropBindings)
                {
                    int saved = GetBoothPropFromSettings(b.prop);
                    _cameraService.SetCameraPropertyValue(b.prop, saved);
                }

                await _cameraService.ProbeCameraPropertiesAsync();

                // 探测完成后 ProbeCameraPropertiesAsync 内部已应用所有已支持属性一次；
                // 这里对每个支持的属性再做一次幂等应用，确保切换摄像头后值落地
                foreach (var b in BoothPropBindings)
                {
                    if (_cameraService.IsCameraPropertySupported(b.prop))
                    {
                        await _cameraService.ApplyCameraPropertyAsync(b.prop);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"ProbeAndApplyBoothCameraProperties 异常: {ex.Message}", LogHelper.LogType.Warning);
            }

            // 回到 UI 线程刷新滑块启用状态与 tooltip
            _ = Dispatcher.BeginInvoke(new Action(RefreshBoothPropSlidersEnabled));
        }

        /// <summary>
        /// 亮度滑块值变化（保留旧名兼容）：更新数值文本，保存到设置，写入摄像头硬件。
        /// </summary>
        private void BoothBrightnessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isBoothBrightnessSliderUpdating) return;
            var valueText = BoothPopupContent?.BrightnessValueText;
            int value = (int)Math.Round(e.NewValue);
            if (valueText != null) valueText.Text = value.ToString();

            if (_cameraService != null && _cameraService.IsCameraPropertySupported(BoothCameraProperty.Brightness))
            {
                _cameraService.SetCameraPropertyValue(BoothCameraProperty.Brightness, value);
            }

            // 持久化用防抖：SaveSettingsToFile 同步序列化+写盘，拖动时高频触发会卡 UI
            try
            {
                if (Settings?.Canvas != null && Settings.Canvas.VideoPresenterBrightness != value)
                {
                    Settings.Canvas.VideoPresenterBrightness = value;
                    ScheduleBoothPropsSaveDebounced();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"保存亮度设置失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        /// <summary>
        /// 通用属性滑块值变化：更新数值文本，写入摄像头硬件，持久化到 Settings。
        /// 由 WireUpBoothPopupContentEvents 为每个 ProMode 滑块订阅。
        /// </summary>
        private void BoothCameraPropSlider_ValueChanged(BoothCameraProperty prop, double newValue, TextBlock valueText)
        {
            if (_isBoothBrightnessSliderUpdating) return;
            int value = (int)Math.Round(newValue);
            if (valueText != null) valueText.Text = value.ToString();

            if (_cameraService != null && _cameraService.IsCameraPropertySupported(prop))
            {
                _cameraService.SetCameraPropertyValue(prop, value);
            }

            // 持久化用防抖：避免拖动期间高频写盘卡 UI
            try
            {
                if (Settings?.Canvas != null && GetBoothPropFromSettings(prop) != value)
                {
                    SetBoothPropToSettings(prop, value);
                    ScheduleBoothPropsSaveDebounced();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"保存 {prop} 设置失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        /// <summary>
        /// 防抖调度一次 Settings 保存：拖动停止后 400ms 才真正写盘。
        /// 拖动期间反复触发只会重置定时器，不会堆积写盘操作。
        /// </summary>
        private void ScheduleBoothPropsSaveDebounced()
        {
            if (_boothPropsSaveDebounceTimer == null)
            {
                _boothPropsSaveDebounceTimer = new DispatcherTimer(DispatcherPriority.Background)
                {
                    Interval = TimeSpan.FromMilliseconds(400)
                };
                _boothPropsSaveDebounceTimer.Tick += (s, e) =>
                {
                    try
                    {
                        _boothPropsSaveDebounceTimer.Stop();
                        SaveSettingsToFile();
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"防抖保存摄像头属性设置失败: {ex.Message}", LogHelper.LogType.Warning);
                    }
                };
            }
            // 重置计时：每次 ValueChanged 都重新开始 400ms 倒计时
            _boothPropsSaveDebounceTimer.Stop();
            _boothPropsSaveDebounceTimer.Start();
        }

        /// <summary>立即 flush 防抖队列里的待保存设置（退出展台/关闭弹窗时调用，避免最后那次丢失）。</summary>
        private void FlushBoothPropsSaveDebounced()
        {
            if (_boothPropsSaveDebounceTimer != null && _boothPropsSaveDebounceTimer.IsEnabled)
            {
                try
                {
                    _boothPropsSaveDebounceTimer.Stop();
                    SaveSettingsToFile();
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"Flush 摄像头属性设置失败: {ex.Message}", LogHelper.LogType.Warning);
                }
            }
        }


        /// <summary>
        /// 加速模式 ComboBox 选择变化：保存到设置并立即应用（运行时检测可用性，不可用则回退）。
        /// ComboBox 索引与 <see cref="PhotoCorrectionAccelerationMode"/> 枚举值一致：0=CPU, 1=OpenCL, 2=CUDA。
        /// </summary>
        private void PhotoCorrectionAccelerationComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Settings?.Automation == null) return;
            // 初始化期间 ComboBox 还在绑定，跳过
            if (BoothPopupContent?.PhotoCorrectionAccelerationComboBox == null) return;
            int idx = BoothPopupContent.PhotoCorrectionAccelerationComboBox.SelectedIndex;
            if (idx < 0 || idx > 2) return;
            var newMode = (PhotoCorrectionAccelerationMode)idx;
            if (Settings.Automation.PhotoCorrectionAcceleration == newMode) return;
            Settings.Automation.PhotoCorrectionAcceleration = newMode;
            SaveSettingsToFile();

            // 立即检测可用性，不可用则回退（避免下次检测时才发现不可用导致卡顿）
            var effective = PaperDetectAccelerationResolver.ResolveEffective(newMode);
            if (effective != newMode)
            {
                LogHelper.WriteLogToFile(
                    $"照片矫正: 加速模式 {newMode} 不可用，回退到 {effective}",
                    LogHelper.LogType.Warning);
                // 把回退结果也写回设置，避免下次启动重复尝试
                Settings.Automation.PhotoCorrectionAcceleration = effective;
                SaveSettingsToFile();
                // 同步 ComboBox 显示（避免显示与实际不符）
                BoothPopupContent.PhotoCorrectionAccelerationComboBox.SelectedIndex = (int)effective;
                ShowBoothTransientMessage(
                    $"{newMode} 不可用，已回退到 {effective}" +
                    (newMode == PhotoCorrectionAccelerationMode.CUDA
                        ? "（未检测到 NVIDIA 显卡）"
                        : string.Empty));
            }
            else
            {
                LogHelper.WriteLogToFile(
                    $"照片矫正: 加速模式切换到 {newMode}",
                    LogHelper.LogType.Trace);
                // CUDA 模式下 OPENCV_OPENCL_DEVICE 环境变量必须进程启动时设置，
                // 运行时切换不会重置已初始化的 OpenCL 上下文。提示用户下次启动生效。
                if (newMode == PhotoCorrectionAccelerationMode.CUDA
                    || Settings.Automation.PhotoCorrectionAcceleration == PhotoCorrectionAccelerationMode.CUDA
                    || (effective == PhotoCorrectionAccelerationMode.CUDA && newMode != PhotoCorrectionAccelerationMode.CUDA))
                {
                    ShowBoothTransientMessage("加速模式切换将在下次启动后完全生效");
                }
            }
        }

        /// <summary>
        /// 启动 A4 纸实时识别定时器。仅在视频展台特殊模式 + 矫正开关开启 + 非冻结状态时有效。
        /// 定时器在后台线程做检测，结果回到 UI 线程画框，不影响直播渲染性能。
        /// </summary>
        private void StartPaperDetectTimer()
        {
            if (PaperDetectOverlayCanvas == null) return;
            // 显示覆盖层（即使还没检测到角点，也保持可见，检测到时再画框）
            PaperDetectOverlayCanvas.Visibility = Visibility.Visible;

            if (_paperDetectTimer == null)
            {
                _paperDetectTimer = new DispatcherTimer(DispatcherPriority.Background)
                {
                    Interval = TimeSpan.FromMilliseconds(PaperDetectIntervalMs)
                };
                _paperDetectTimer.Tick += PaperDetectTimer_Tick;
            }
            if (!_paperDetectTimer.IsEnabled)
                _paperDetectTimer.Start();
        }

        /// <summary>
        /// 停止 A4 纸实时识别定时器并清空覆盖层。
        /// </summary>
        private void StopPaperDetectTimer()
        {
            if (_paperDetectTimer != null && _paperDetectTimer.IsEnabled)
                _paperDetectTimer.Stop();
            StopOverlayAnim();
            if (PaperDetectOverlayCanvas != null)
            {
                PaperDetectOverlayCanvas.Children.Clear();
                PaperDetectOverlayCanvas.Visibility = Visibility.Collapsed;
            }
            _overlayPolygon = null;
            _overlayEllipses = null;
            _lastOverlayCorners = null;
            _lastOverlayDrawTime = null;
            _overlayAnimCompletedCount = 0;
            _overlayAnimInterruptedCount = 0;
        }

        /// <summary>
        /// 实时识别定时器 Tick：从 D3DImage 拿当前帧 → 后台线程检测角点 → UI 线程画框。
        /// 用 Interlocked 防重入（上一次检测未完成时跳过本次）。
        /// </summary>
        private void PaperDetectTimer_Tick(object sender, EventArgs e)
        {
            // 仅在特殊模式 + 非冻结状态 + VideoCaptureElement 可见时检测
            if (!_isVideoPresenterSpecialMode) return;
            if (VideoPresenterFullCanvasImage == null) return;
            if (VideoPresenterFullCanvasImage.Visibility != Visibility.Visible) return;
            // 冻结状态（拍照后显示冻结照片）：清空覆盖层，不检测
            if (VideoPresenterFrozenFrameImage != null
                && VideoPresenterFrozenFrameImage.Visibility == Visibility.Visible)
            {
                if (PaperDetectOverlayCanvas != null && PaperDetectOverlayCanvas.Children.Count > 0)
                {
                    StopOverlayAnim();
                    PaperDetectOverlayCanvas.Children.Clear();
                    _overlayPolygon = null;
                    _overlayEllipses = null;
                    _lastOverlayCorners = null;
                }
                return;
            }

            // 防重入：上一次后台检测还在跑就跳过
            if (System.Threading.Interlocked.CompareExchange(ref _paperDetectRunning, 1, 0) != 0) return;

            // 拿当前帧（D3DImage.CopyBackBuffer）
            BitmapSource frameSrc = null;
            try { frameSrc = VideoPresenterFullCanvasImage.CaptureCurrentFrame(); }
            catch { }
            if (frameSrc == null)
            {
                System.Threading.Interlocked.Exchange(ref _paperDetectRunning, 0);
                return;
            }

            // 应用旋转（与拍照路径一致：D3DImage 拿到的是原始帧，需要手动旋转）
            int rotation = _cameraService?.RotationAngle ?? 0;
            BitmapSource rotated = rotation != 0 ? ApplyRotationToBitmapSource(frameSrc, rotation) : frameSrc;
            if (rotated == null)
            {
                System.Threading.Interlocked.Exchange(ref _paperDetectRunning, 0);
                return;
            }

            int frameW = rotated.PixelWidth;
            int frameH = rotated.PixelHeight;

            // 后台线程做检测（TryDetectPaperCorners 内部会下采样，~30ms）
            System.Threading.Tasks.Task.Run(() =>
            {
                Bitmap bmp = null;
                List<OpenCvSharp.Point> corners = null;
                try
                {
                    bmp = BitmapSourceToBitmap(rotated);
                    if (bmp != null)
                    {
                        // verbose=false：实时检测不输出日志/不保存诊断图，避免每 400ms 刷屏
                        TryDetectPaperCorners(bmp, out corners, verbose: false);
                    }
                }
                catch { corners = null; }
                finally
                {
                    bmp?.Dispose();
                }

                // 映射角点到 overlay 坐标
                System.Windows.Point[] overlayPts = null;
                if (corners != null && corners.Count == 4)
                {
                    overlayPts = new System.Windows.Point[4];
                    for (int i = 0; i < 4; i++)
                    {
                        overlayPts[i] = MapFramePointToOverlay(
                            corners[i].X, corners[i].Y, frameW, frameH);
                    }
                }

                // 回到 UI 线程画框
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    DrawPaperDetectOverlay(overlayPts);
                    System.Threading.Interlocked.Exchange(ref _paperDetectRunning, 0);
                }));
            });
        }

        /// <summary>
        /// 把帧坐标 (frameX, frameY) 映射到 PaperDetectOverlayCanvas 的坐标空间。
        /// 需要考虑：VideoCaptureElement 的 Stretch=Uniform + LayoutTransform 旋转 + RenderTransform(缩放/平移)。
        /// 映射流程：帧坐标 → 旋转后帧坐标 → Uniform 缩放到 VideoCaptureElement 布局尺寸
        ///          → 应用 RenderTransform(Scale + Translate，含 RenderTransformOrigin=(0,0) 规则)
        ///          → 加 VideoCaptureElement 在 overlay 中的布局偏移
        ///          → overlay 坐标
        /// 注意：RenderTransformOrigin=(0,0)，缩放公式为 translate_new = origin - (origin - translate_old) * ratio，
        ///       在 origin=(0,0) 下化简为 translate_new = translate_old * ratio，即缩放是以"平移后的左上角"为锚点。
        ///       最终屏幕位置 = Scale * 布局坐标 + Translate。
        /// </summary>
        private System.Windows.Point MapFramePointToOverlay(double frameX, double frameY, int frameW, int frameH)
        {
            if (PaperDetectOverlayCanvas == null) return new System.Windows.Point(0, 0);
            double overlayW = PaperDetectOverlayCanvas.ActualWidth;
            double overlayH = PaperDetectOverlayCanvas.ActualHeight;
            if (overlayW <= 0 || overlayH <= 0) return new System.Windows.Point(0, 0);

            int rotation = _cameraService?.RotationAngle ?? 0;

            // 1. 帧坐标 → 旋转后坐标（旋转后帧的"显示"尺寸会宽高交换）
            double rx, ry, rotW, rotH;
            if (rotation == 1) // 90° 顺时针
            {
                rx = frameH - frameY;
                ry = frameX;
                rotW = frameH; rotH = frameW;
            }
            else if (rotation == 2) // 180°
            {
                rx = frameW - frameX;
                ry = frameH - frameY;
                rotW = frameW; rotH = frameH;
            }
            else if (rotation == 3) // 270°
            {
                rx = frameY;
                ry = frameW - frameX;
                rotW = frameH; rotH = frameW;
            }
            else // 0°
            {
                rx = frameX; ry = frameY;
                rotW = frameW; rotH = frameH;
            }

            // 2. VideoCaptureElement 的布局尺寸（经过 LayoutTransform 旋转后的实际占用尺寸）
            //    父容器用 Grid.Stretch=Fill 或类似布局，VideoCaptureElement 自身 Stretch=Uniform
            //    所以它的布局尺寸 = 父容器尺寸（overlay 尺寸），再在内部 Uniform 拉伸留黑边
            double elemW = VideoPresenterFullCanvasImage?.ActualWidth ?? overlayW;
            double elemH = VideoPresenterFullCanvasImage?.ActualHeight ?? overlayH;
            if (elemW <= 0 || elemH <= 0) { elemW = overlayW; elemH = overlayH; }

            // 3. Uniform 缩放：旋转后帧 → VideoCaptureElement 布局框（留黑边居中）
            double stretch = Math.Min(elemW / rotW, elemH / rotH);
            double uniformX = rx * stretch;   // VideoCaptureElement 内部坐标（左上角对齐）
            double uniformY = ry * stretch;
            // Uniform 居中偏移（黑边）
            double uniformOffsetX = (elemW - rotW * stretch) / 2.0;
            double uniformOffsetY = (elemH - rotH * stretch) / 2.0;
            double layoutX = uniformX + uniformOffsetX;   // VideoCaptureElement 布局坐标
            double layoutY = uniformY + uniformOffsetY;

            // 4. 应用 RenderTransform：Scale + Translate（RenderTransformOrigin=(0,0)）
            //    屏幕坐标 = Scale * 布局坐标 + Translate
            double scale = _boothPreviewScale;
            double tx = _boothPreviewTranslateX;
            double ty = _boothPreviewTranslateY;
            double screenX = layoutX * scale + tx;
            double screenY = layoutY * scale + ty;

            // 5. VideoCaptureElement 在 overlay 中的偏移（Grid 布局通常对齐到 overlay 左上角，偏移=0）
            //    若 Grid 有 Margin/非零对齐，需要加 VideoCaptureElement 相对 overlay 的偏移；
            //    这里用 TranslatePoint 从 VideoCaptureElement 坐标系转换到 overlay 坐标系，更鲁棒
            if (VideoPresenterFullCanvasImage != null && PaperDetectOverlayCanvas != null)
            {
                try
                {
                    var pt = VideoPresenterFullCanvasImage.TranslatePoint(
                        new System.Windows.Point(screenX, screenY), PaperDetectOverlayCanvas);
                    return pt;
                }
                catch { /* 转换失败时退回到直接返回 screenX/screenY（视为偏移=0） */ }
            }

            return new System.Windows.Point(screenX, screenY);
        }

        /// <summary>
        /// 在覆盖 Canvas 上绘制 A4 纸检测框（红色四边形 + 4 个角点圆）。
        /// overlayPts 为 null 时清空覆盖层。
        /// 有上一帧角点时，启动移动动画从上一帧位置平滑过渡到当前帧，避免框跳变；
        /// 动画进行中又来新目标时，以当前中间位置为新起点继续追赶，自然衔接。
        /// </summary>
        private void DrawPaperDetectOverlay(System.Windows.Point[] overlayPts)
        {
            if (PaperDetectOverlayCanvas == null) return;

            if (overlayPts == null || overlayPts.Length != 4)
            {
                // 丢失检测：停止动画，清空覆盖层与复用元素引用
                StopOverlayAnim();
                PaperDetectOverlayCanvas.Children.Clear();
                _overlayPolygon = null;
                _overlayEllipses = null;
                _lastOverlayCorners = null;
                _lastOverlayDrawTime = null;
                // 若有动画统计，输出汇总后重置
                if (_overlayAnimCompletedCount > 0 || _overlayAnimInterruptedCount > 0)
                {
                    LogHelper.WriteLogToFile(
                        $"矫正框: 丢失检测 本次统计 完成={_overlayAnimCompletedCount} 打断={_overlayAnimInterruptedCount}",
                        LogHelper.LogType.Trace);
                    _overlayAnimCompletedCount = 0;
                    _overlayAnimInterruptedCount = 0;
                }
                return;
            }

            // 诊断：记录新角点到来间隔（验证实际间隔 ≠ 16ms 定时器间隔，受检测耗时限制约 30-50ms）
            var now = DateTime.UtcNow;
            if (_lastOverlayDrawTime.HasValue)
            {
                double intervalMs = (now - _lastOverlayDrawTime.Value).TotalMilliseconds;
                bool animRunning = _overlayAnimTimer != null && _overlayAnimTimer.IsEnabled;
                LogHelper.WriteLogToFile(
                    $"矫正框: 新角点到来 间隔={intervalMs:F1}ms 动画运行中={animRunning} (完成={_overlayAnimCompletedCount},打断={_overlayAnimInterruptedCount})",
                    LogHelper.LogType.Trace);
            }
            _lastOverlayDrawTime = now;

            if (_lastOverlayCorners == null)
            {
                // 首次检测到（或丢失后重新检测到）：直接绘制，无动画
                StopOverlayAnim();
                EnsureOverlayElements();
                ApplyOverlayPoints(overlayPts);
                _lastOverlayCorners = (System.Windows.Point[])overlayPts.Clone();
            }
            else
            {
                // 有上一帧：启动动画从 _lastOverlayCorners（当前显示位置）过渡到 overlayPts
                StartOverlayAnim(_lastOverlayCorners, overlayPts);
            }
        }

        /// <summary>确保复用的 Polygon 与 4 个 Ellipse 已创建并加入覆盖层（动画中只更新属性，不重建）。</summary>
        private void EnsureOverlayElements()
        {
            if (_overlayPolygon == null)
            {
                _overlayPolygon = new System.Windows.Shapes.Polygon
                {
                    Stroke = System.Windows.Media.Brushes.Red,
                    StrokeThickness = 3,
                    Fill = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromArgb(40, 255, 0, 0)) // 半透明红色填充
                };
                PaperDetectOverlayCanvas.Children.Add(_overlayPolygon);
            }
            if (_overlayEllipses == null)
            {
                _overlayEllipses = new System.Windows.Shapes.Ellipse[4];
                for (int i = 0; i < 4; i++)
                {
                    _overlayEllipses[i] = new System.Windows.Shapes.Ellipse
                    {
                        Width = 12,
                        Height = 12,
                        Fill = System.Windows.Media.Brushes.Red,
                        Stroke = System.Windows.Media.Brushes.White,
                        StrokeThickness = 1
                    };
                    PaperDetectOverlayCanvas.Children.Add(_overlayEllipses[i]);
                }
            }
        }

        /// <summary>把给定 4 点应用到复用的 Polygon.Points 与 4 个 Ellipse 的位置。</summary>
        private void ApplyOverlayPoints(System.Windows.Point[] pts)
        {
            if (_overlayPolygon == null || _overlayEllipses == null) return;
            _overlayPolygon.Points = new System.Windows.Media.PointCollection(pts);
            for (int i = 0; i < 4; i++)
            {
                System.Windows.Controls.Canvas.SetLeft(_overlayEllipses[i], pts[i].X - 6);
                System.Windows.Controls.Canvas.SetTop(_overlayEllipses[i], pts[i].Y - 6);
            }
        }

        /// <summary>启动矫正框移动动画：from→to，时长 OverlayAnimDurationMs，ease-out 缓动。</summary>
        private void StartOverlayAnim(System.Windows.Point[] from, System.Windows.Point[] to)
        {
            EnsureOverlayElements();

            // 诊断：若动画正在运行，说明上一次还没跑完就被新目标打断，记录打断进度
            if (_overlayAnimTimer != null && _overlayAnimTimer.IsEnabled
                && _animFromCorners != null && _animToCorners != null)
            {
                double prevT = (DateTime.UtcNow - _animStartTime).TotalMilliseconds / OverlayAnimDurationMs;
                if (prevT > 1.0) prevT = 1.0;
                double prevEased = 1.0 - (1.0 - prevT) * (1.0 - prevT);
                _overlayAnimInterruptedCount++;
                LogHelper.WriteLogToFile(
                    $"矫正框: 动画被打断重启 上次进度={prevT:F2}(eased={prevEased:F2}) 时长={OverlayAnimDurationMs}ms",
                    LogHelper.LogType.Trace);
            }

            _animFromCorners = (System.Windows.Point[])from.Clone();
            _animToCorners = (System.Windows.Point[])to.Clone();
            _animStartTime = DateTime.UtcNow;
            // _lastOverlayCorners 由 OverlayAnimTimer_Tick 实时更新为当前插值位置，
            // 动画未结束又来新目标时，新一轮 from 即此中间位置，自然衔接。

            if (_overlayAnimTimer == null)
            {
                _overlayAnimTimer = new DispatcherTimer(DispatcherPriority.Render)
                {
                    Interval = TimeSpan.FromMilliseconds(16)
                };
                _overlayAnimTimer.Tick += OverlayAnimTimer_Tick;
            }
            if (!_overlayAnimTimer.IsEnabled)
                _overlayAnimTimer.Start();
            // 立即触发一次，避免起点停顿
            OverlayAnimTimer_Tick(null, EventArgs.Empty);
        }

        /// <summary>停止矫正框移动动画并清空起止角点。</summary>
        private void StopOverlayAnim()
        {
            if (_overlayAnimTimer != null && _overlayAnimTimer.IsEnabled)
                _overlayAnimTimer.Stop();
            _animFromCorners = null;
            _animToCorners = null;
        }

        /// <summary>动画定时器 Tick：按 progress 插值 4 个角点并更新覆盖层，progress≥1 时停止。</summary>
        private void OverlayAnimTimer_Tick(object sender, EventArgs e)
        {
            if (_animFromCorners == null || _animToCorners == null)
            {
                StopOverlayAnim();
                return;
            }
            double t = (DateTime.UtcNow - _animStartTime).TotalMilliseconds / OverlayAnimDurationMs;
            if (t > 1.0) t = 1.0;
            // ease-out：起步快、末段慢，贴合检测框"追上纸张"的直觉
            double eased = 1.0 - (1.0 - t) * (1.0 - t);

            var pts = new System.Windows.Point[4];
            for (int i = 0; i < 4; i++)
            {
                pts[i] = new System.Windows.Point(
                    _animFromCorners[i].X + (_animToCorners[i].X - _animFromCorners[i].X) * eased,
                    _animFromCorners[i].Y + (_animToCorners[i].Y - _animFromCorners[i].Y) * eased);
            }
            ApplyOverlayPoints(pts);
            // 实时记录当前显示位置：动画未结束时新目标到来，以此为新起点
            _lastOverlayCorners = (System.Windows.Point[])pts.Clone();

            if (t >= 1.0)
            {
                _overlayAnimCompletedCount++;
                LogHelper.WriteLogToFile(
                    $"矫正框: 动画正常完成 实际耗时={(DateTime.UtcNow - _animStartTime).TotalMilliseconds:F1}ms (累计完成={_overlayAnimCompletedCount},打断={_overlayAnimInterruptedCount})",
                    LogHelper.LogType.Trace);
                StopOverlayAnim();
            }
        }

        /// <summary>
        /// 将选定的捕获图片插入到画布或全屏预览（特殊模式下）。
        /// </summary>
        /// <param name="photo">要插入的捕获图片；若为 null 或其 Image 为 null，则不进行任何操作。</param>
        /// <remarks>
        /// 视频展台特殊模式下：将照片显示在 VideoPresenterFrozenFrameImage 上，覆盖实时预览，
        ///   让用户在冻结画面上批注（而不是把图片当作 inkCanvas 子元素插入，避免被当成普通图片元素选择/缩放）。
        /// 普通模式下：在画布上创建并配置一个 Image 元素（设置 Source、Stretch、默认宽度及位置），
        ///   初始化其变换与事件绑定，提交插入历史记录，添加到 inkCanvas，并将当前工具切换为“选择”。
        /// </remarks>
        private void InsertPhotoToCanvas(CapturedImage photo)
        {
            if (photo?.Image == null) return;

            // 特殊模式：虚拟分页 - 拍照后不立即显示冻结照片，继续显示直播画面。
            // 照片已由拍照流程加入 _capturedPhotos，这里只刷新页码列表（白板右下角预览）。
            // 用户点击页码列表中对应照片项才切换到照片预览页。
            if (_isVideoPresenterSpecialMode)
            {
                try
                {
                    // 拍照后仍在直播页，页码显示 0/N（N=照片数）
                    UpdateBoothPageInfoDisplay();
                    // 刷新页码列表：第 0 项（直播页文字）+ 第 1..N 项（各照片缩略图）
                    RefreshBoothPageListView();
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"拍照后刷新页码列表失败: {ex.Message}", LogHelper.LogType.Error);
                }
                return;
            }

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

        /// <summary>清除冻结画面（恢复实时预览）。</summary>
        private void ClearFrozenFrame()
        {
            if (VideoPresenterFrozenFrameImage == null) return;
            try
            {
                VideoPresenterFrozenFrameImage.Source = null;
                VideoPresenterFrozenFrameImage.Visibility = Visibility.Collapsed;
                // 清除 InsertPhotoToCanvas 设置的 Width/Height 和对齐方式，
                // 恢复为 Stretch="Uniform" + Stretch 对齐（默认行为）
                VideoPresenterFrozenFrameImage.Width = double.NaN;
                VideoPresenterFrozenFrameImage.Height = double.NaN;
                VideoPresenterFrozenFrameImage.HorizontalAlignment = HorizontalAlignment.Stretch;
                VideoPresenterFrozenFrameImage.VerticalAlignment = VerticalAlignment.Stretch;
                // 解除与 VideoCaptureElement 的变换共享，恢复照片 Image 自己的变换实例
                if (VideoPresenterFrozenFrameRotation != null)
                    VideoPresenterFrozenFrameImage.LayoutTransform = VideoPresenterFrozenFrameRotation;
                if (_frozenFrameOriginalRenderTransform != null)
                    VideoPresenterFrozenFrameImage.RenderTransform = _frozenFrameOriginalRenderTransform;
                // VideoPresenterFrozenOverlay（旧全屏蒙版）和 VideoPresenterFrozenThumbnail（侧栏小预览）均已从 XAML 移除
            }
            catch { }
        }

        /// <summary>
        /// 从直播页切换到照片预览页。
        /// 显示指定照片、停止实时预览、页码 (photoIndex+1)/N、拍照按钮变灰。
        /// </summary>
        /// <param name="photoIndex">_capturedPhotos 中的索引（0-based）。</param>
        private void SwitchBoothToPhotoPage(int photoIndex)
        {
            if (photoIndex < 0 || photoIndex >= _capturedPhotos.Count)
            {
                ShowBoothTransientMessage("还没有可查看的照片");
                return;
            }

            var photo = _capturedPhotos[photoIndex];
            if (photo?.Image == null)
            {
                ShowBoothTransientMessage("照片数据异常");
                return;
            }

            // 切换前保存当前页墨迹到当前槽位（直播页快照或旧照片的 Strokes），
            // 避免新照片显示旧墨迹或当前墨迹丢失。
            SaveCurrentBoothStrokesToSlot();
            // 切换页面重置旋转基准（新页面的墨迹需要新的基准）
            ResetRotationBaseline();
            _boothCurrentPhotoIndex = photoIndex;

            // 照片预览页直接复用实时画面的变换管线：把 FillImage 的 LayoutTransform/RenderTransform
            // 替换为 VideoCaptureElement 的同名实例（VideoPresenterFullCanvasRotation/Scale/Translate）。
            // FillImage 重写了 MeasureOverride 返回 availableSize，与 VideoCaptureElement 行为一致
            // （填满容器，Stretch=Uniform 内部按比例缩放居中），所以旋转/缩放/移动与实时画面走完全相同代码路径。
            if (VideoPresenterFrozenFrameImage != null)
            {
                // 诊断日志：进入照片预览页前的状态
                try
                {
                    double containerW = VideoPresenterSpecialModeContainer?.ActualWidth ?? 0;
                    double containerH = VideoPresenterSpecialModeContainer?.ActualHeight ?? 0;
                    double imgW = 0, imgH = 0;
                    if (photo.Image is BitmapSource bs) { imgW = bs.PixelWidth; imgH = bs.PixelHeight; }
                    LogHelper.WriteLogToFile(
                        $"[BoothDiag] SwitchBoothToPhotoPage ENTER: photoIndex={photoIndex}, " +
                        $"container={containerW}x{containerH}, imgSize={imgW}x{imgH}, " +
                        $"liveRotation.Angle={VideoPresenterFullCanvasRotation?.Angle ?? -1}, " +
                        $"cameraService.RotationAngle={_cameraService?.RotationAngle ?? -1}, " +
                        $"strokesCount={inkCanvas?.Strokes?.Count ?? 0}",
                        LogHelper.LogType.Info);
                }
                catch { }

                VideoPresenterFrozenFrameImage.Source = photo.Image;
                VideoPresenterFrozenFrameImage.Visibility = Visibility.Visible;
                // FillImage 填满容器，不需要显式 Width/Height 和对齐方式
                VideoPresenterFrozenFrameImage.Width = double.NaN;
                VideoPresenterFrozenFrameImage.Height = double.NaN;
                VideoPresenterFrozenFrameImage.HorizontalAlignment = HorizontalAlignment.Stretch;
                VideoPresenterFrozenFrameImage.VerticalAlignment = VerticalAlignment.Stretch;

                // 保存照片 Image 原始的 RenderTransform（仅首次），返回直播页时恢复
                if (_frozenFrameOriginalRenderTransform == null)
                    _frozenFrameOriginalRenderTransform = VideoPresenterFrozenFrameImage.RenderTransform as System.Windows.Media.TransformGroup;
                // 共享实时画面的变换实例：LayoutTransform + RenderTransform 都指向 VideoCaptureElement 的同名对象
                if (VideoPresenterFullCanvasRotation != null)
                    VideoPresenterFrozenFrameImage.LayoutTransform = VideoPresenterFullCanvasRotation;
                if (VideoPresenterFullCanvasImage?.RenderTransform != null)
                    VideoPresenterFrozenFrameImage.RenderTransform = VideoPresenterFullCanvasImage.RenderTransform;

                // 重置变换：与直播页初始状态一致（Scale=1, Translate=0，Stretch 自动 fit 居中）
                _boothPreviewScale = 1.0;
                _boothPreviewTranslateX = 0;
                _boothPreviewTranslateY = 0;

                // 照片内容已在拍照时旋转到正向，预览旋转从 0° 开始
                // （共享的 VideoPresenterFullCanvasRotation 同时影响已 Collapsed 的 VideoCaptureElement，不影响显示）
                if (VideoPresenterFullCanvasRotation != null)
                    VideoPresenterFullCanvasRotation.Angle = 0;

                ApplyBoothPreviewTransform();

                // 诊断日志：共享变换后的状态
                try
                {
                    LogHelper.WriteLogToFile(
                        $"[BoothDiag] SwitchBoothToPhotoPage SHARED: " +
                        $"photo.LayoutTransform={VideoPresenterFrozenFrameImage.LayoutTransform?.GetType().Name ?? "null"}, " +
                        $"photo.RenderTransform={VideoPresenterFrozenFrameImage.RenderTransform?.GetType().Name ?? "null"}, " +
                        $"isSharedWithLive={(VideoPresenterFrozenFrameImage.RenderTransform == VideoPresenterFullCanvasImage?.RenderTransform)}, " +
                        $"photo.ActualSize={VideoPresenterFrozenFrameImage.ActualWidth}x{VideoPresenterFrozenFrameImage.ActualHeight}",
                        LogHelper.LogType.Info);
                }
                catch { }
            }

            // 停止并隐藏 VideoCaptureElement，只显示冻结照片
            // （Stretch=Uniform 留黑边处会看到底层实时画面，必须停止+隐藏）
            if (VideoPresenterFullCanvasImage != null)
            {
                try { VideoPresenterFullCanvasImage.Stop(); } catch { }
                VideoPresenterFullCanvasImage.Visibility = Visibility.Collapsed;
            }

            // 拍照按钮变灰（在照片预览页不允许拍照）
            if (BtnCapturePhoto != null)
            {
                BtnCapturePhoto.IsEnabled = false;
            }

            // 页码显示 (photoIndex+1)/N
            UpdateBoothPageInfoDisplay();
            // 同步页码列表 SelectedIndex 到照片项（index=photoIndex+1）
            int selectedIndex = photoIndex + 1;
            var leftPageListView = FindView("board.pageList.left") as System.Windows.Controls.ListView;
            var rightPageListView = FindView("board.pageList.right") as System.Windows.Controls.ListView;
            if (leftPageListView != null) leftPageListView.SelectedIndex = selectedIndex;
            if (rightPageListView != null) rightPageListView.SelectedIndex = selectedIndex;

            // 切换后恢复该照片的墨迹（每张照片独立保存，互不干扰）
            RestoreBoothStrokesFromSlot(photoIndex);
        }

        /// <summary>
        /// 从照片预览页返回直播页。
        /// 清除冻结照片、恢复实时预览、页码 0/1、拍照按钮恢复。
        /// </summary>
        private void SwitchBoothToLivePage()
        {
            // 返回直播前保存当前照片的墨迹到该照片槽位，避免丢失批注
            SaveCurrentBoothStrokesToSlot();
            // 切换页面重置旋转基准
            ResetRotationBaseline();
            _boothCurrentPhotoIndex = -1;

            // 清除冻结照片，并恢复照片 Image 原始的变换实例（解除与 VideoCaptureElement 的共享）
            if (VideoPresenterFrozenFrameImage != null)
            {
                VideoPresenterFrozenFrameImage.Visibility = Visibility.Collapsed;
                // 恢复照片 Image 自己的 LayoutTransform / RenderTransform，
                // 避免后续直播页操作通过共享实例意外影响照片 Image
                if (VideoPresenterFrozenFrameRotation != null)
                    VideoPresenterFrozenFrameImage.LayoutTransform = VideoPresenterFrozenFrameRotation;
                if (_frozenFrameOriginalRenderTransform != null)
                    VideoPresenterFrozenFrameImage.RenderTransform = _frozenFrameOriginalRenderTransform;
            }

            // 恢复 VideoCaptureElement 实时预览
            if (VideoPresenterFullCanvasImage != null && _cameraService != null)
            {
                VideoPresenterFullCanvasImage.Visibility = Visibility.Visible;
                // 照片预览页共享了 VideoPresenterFullCanvasRotation，退出时恢复直播画面角度
                // （_cameraService.RotationAngle 在照片预览期间未变，直接用它的角度）
                if (VideoPresenterFullCanvasRotation != null)
                    VideoPresenterFullCanvasRotation.Angle = _cameraService.RotationAngle * 90.0;
                // 重置缩放/平移为默认状态（直播页与照片页缩放状态不通用）
                _boothPreviewScale = 1.0;
                _boothPreviewTranslateX = 0;
                _boothPreviewTranslateY = 0;
                ApplyBoothPreviewTransform();

                int page = GetCurrentPageIndex();
                int camIdx = -1;
                if (_cameraIndexByPage.TryGetValue(page, out int savedIdx)
                    && savedIdx >= 0 && savedIdx < _cameraService.AvailableCameras.Count)
                {
                    camIdx = savedIdx;
                }
                if (camIdx < 0 && _cameraService.AvailableCameras.Count > 0)
                {
                    camIdx = 0;
                }
                if (camIdx >= 0)
                {
                    _ = StartVideoCaptureElementPreviewAsync(camIdx);
                }
            }

            // 拍照按钮恢复可用（MediaOpened 事件会再次设置 IsEnabled=true，这里先恢复）
            if (BtnCapturePhoto != null && _cameraService != null)
            {
                BtnCapturePhoto.IsEnabled = true;
            }

            // 页码显示 0/1
            UpdateBoothPageInfoDisplay();
            // 同步侧栏页码列表 SelectedIndex 到直播页项（index=0）
            var leftPageListView = FindView("board.pageList.left") as System.Windows.Controls.ListView;
            var rightPageListView = FindView("board.pageList.right") as System.Windows.Controls.ListView;
            if (leftPageListView != null) leftPageListView.SelectedIndex = 0;
            if (rightPageListView != null) rightPageListView.SelectedIndex = 0;

            // 恢复直播页墨迹快照（与照片页墨迹相互独立）
            RestoreBoothStrokesFromSlot(-1);
        }

        /// <summary>
        /// 在离开白板模式时关闭并清理视频呈现器相关的 UI 与运行状态。
        /// </summary>
        /// <remarks>
        /// 隐藏视频呈现侧栏、从画布中移除并隐藏所有每页的实时帧图像实例（历史遗留；
        /// 「上屏」按钮移除后 _liveFrameImageByPage 已不再有新增项，这里仅作幂等清理），
        /// 并尝试停止相机预览。该方法在执行过程中会吞并内部异常以避免抛出至调用方。
        /// </remarks>
        private void VideoPresenter_OnExitWhiteboardMode()
        {
            try
            {
                // 退出白板时必须调用 ExitVideoPresenterSpecialMode，否则：
                //  - _isVideoPresenterSpecialMode 仍为 true
                //  - VideoPresenterSpecialModeContainer / GridBackgroundCover 仍可见（白板背景持续显示 #333333）
                //  - 重新打开白板时 ToggleVideoPresenterSidebar 会以为"已在特殊模式"，
                //    只展开侧栏而不重新启动预览，导致用户进入"空黑屏 + 占位文字"状态
                ExitVideoPresenterSpecialMode();
                CloseVideoPresenterSidebarAndReleaseResources();

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

        // 照片矫正：检测下采样目标宽度（性能与精度平衡，>500px 缩放后检测，角点再按比例还原）
        // 500 而非 640：x86 进程下 4K 帧 BitmapConverter.ToMat + MedianBlur 易 OOM，更小的下采样尺寸可显著降低峰值内存
        private const int PaperDetectTargetWidth = 500;
        // 矫正后输出尺寸上限（避免角点检测异常时生成超大位图导致 OOM）
        private const int CorrectedPaperMaxSide = 2400;

        /// <summary>
        /// 在给定帧中尝试检测纸张（四边形）角点，按 (左上、右上、右下、左下) 顺序返回。
        /// 实现按用户选择的加速模式（CPU/OpenCL/CUDA）由 <see cref="Helpers.PaperDetectorFactory"/> 实例化对应检测器。
        /// 算法参考 CSDN 博客思路：灰度 → 高斯模糊 → Canny → dilate → findContours → convexHull → approxPolyDP → 角度筛选 → boundingRect 最大。
        /// </summary>
        /// <param name="frame">要检测的输入位图帧。</param>
        /// <param name="cornersOut">检测到的四个角点（按顺序：左上、右上、右下、左下），坐标以输入帧的像素空间为准；检测失败时为 null。</param>
        /// <param name="verbose">true 时输出详细日志（含角点、Canny 阈值、候选数）；实时检测传 false 避免刷屏。</param>
        /// <returns>true 如果成功检测到四个角点并填充 cornersOut，false 否则。</returns>
        private static bool TryDetectPaperCorners(Bitmap frame, out List<OpenCvSharp.Point> cornersOut, bool verbose = true)
        {
            cornersOut = null;
            try
            {
                if (frame == null) return false;
                var mode = Settings?.Automation?.PhotoCorrectionAcceleration ?? PhotoCorrectionAccelerationMode.Cpu;
                var detector = Helpers.PaperDetectorFactory.Get(mode);
                if (detector.TryDetect(frame, out var pts, verbose))
                {
                    cornersOut = pts.Select(p => new OpenCvSharp.Point(p.X, p.Y)).ToList();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                if (verbose) LogHelper.WriteLogToFile($"照片矫正: 调度异常 {ex.Message}", LogHelper.LogType.Warning);
                return false;
            }
        }

        /// <summary>
        /// 将源图像中由四个角点定义的纸张区域进行透视矫正并裁切。输出尺寸由四个角点的几何关系决定（取上下边宽、左右边高的平均值）以保持原纸张比例，
        /// 并限制最大边长 CorrectedPaperMaxSide 以防角点异常时生成超大位图导致 OOM。
        /// </summary>
        /// <param name="frame">包含待矫正纸张的源位图。</param>
        /// <param name="corners">纸张在源图像中的四个角点，按顺序提供：左上 (top-left)、右上 (top-right)、右下 (bottom-right)、左下 (bottom-left)。坐标为图像像素坐标系。</param>
        /// <returns>透视矫正并裁切后的位图；在输入无效或矫正失败时返回 <see langword="null"/>。</returns>
        private static Bitmap ApplyPerspectiveCorrection(Bitmap frame, List<OpenCvSharp.Point> corners)
        {
            try
            {
                if (frame == null || corners == null || corners.Count != 4) return null;
                using var src = BitmapConverter.ToMat(frame);
                if (src.Empty()) return null;

                // c[0]=tl, c[1]=tr, c[2]=br, c[3]=bl
                Point[] c = corners.ToArray();
                Point2f[] srcPts = c.Select(p => new Point2f(p.X, p.Y)).ToArray();

                double topW = Dist(c[0], c[1]);
                double bottomW = Dist(c[2], c[3]);
                double leftH = Dist(c[0], c[3]);
                double rightH = Dist(c[1], c[2]);
                int w = (int)Math.Round((topW + bottomW) / 2.0);
                int h = (int)Math.Round((leftH + rightH) / 2.0);
                if (w < 1) w = 1;
                if (h < 1) h = 1;

                // 输出尺寸上限：防止角点检测异常（如散落很远）时生成超大位图
                double maxSide = Math.Max(w, h);
                if (maxSide > CorrectedPaperMaxSide)
                {
                    double r = CorrectedPaperMaxSide / maxSide;
                    w = (int)Math.Round(w * r);
                    h = (int)Math.Round(h * r);
                    if (w < 1) w = 1;
                    if (h < 1) h = 1;
                }

                Point2f[] dstPts =
                {
                    new Point2f(0, 0),
                    new Point2f(w, 0),
                    new Point2f(w, h),
                    new Point2f(0, h),
                };
                using var m = Cv2.GetPerspectiveTransform(srcPts, dstPts);
                using var dst = new Mat();
                Cv2.WarpPerspective(src, dst, m, new Size(w, h),
                    InterpolationFlags.Linear, BorderTypes.Constant, Scalar.Black);

                LogHelper.WriteLogToFile(
                    $"照片矫正: 透视矫正完成 输出尺寸 {w}x{h} (上边 {topW:F0}, 下边 {bottomW:F0}, 左 {leftH:F0}, 右 {rightH:F0})",
                    LogHelper.LogType.Trace);
                return BitmapConverter.ToBitmap(dst);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"照片矫正: 透视矫正异常 {ex.Message}", LogHelper.LogType.Warning);
                return null;
            }
        }

        /// <summary>
        /// 将无序的 4 个角点排序为 左上 (tl)、右上 (tr)、右下 (br)、左下 (bl)。
        /// 直接用四对角点的几何特征判定，对纸张倾斜/旋转场景比按 Y 排序更鲁棒：
        ///   tl = (x+y) 最小      br = (x+y) 最大
        ///   tr = (y-x) 最小（x 大 y 小，右上角）   bl = (y-x) 最大（x 小 y 大，左下角）
        /// 注意：OrderBy 是升序（取最小），OrderByDescending 是降序（取最大）。
        /// 之前版本 tr/bl 的查询方向写反了，导致输出水平镜像。
        /// </summary>
        private static OpenCvSharp.Point[] OrderCorners(OpenCvSharp.Point[] pts)
        {
            var tl = pts.OrderBy(p => p.X + p.Y).First();              // 左上：x+y 最小
            var br = pts.OrderByDescending(p => p.X + p.Y).First();    // 右下：x+y 最大
            var tr = pts.OrderBy(p => p.Y - p.X).First();              // 右上：y-x 最小（x-y 最大）
            var bl = pts.OrderByDescending(p => p.Y - p.X).First();    // 左下：y-x 最大
            return new[] { tl, tr, br, bl };
        }

        private static double Dist(OpenCvSharp.Point a, OpenCvSharp.Point b)
            => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    }
}
