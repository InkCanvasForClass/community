using Ink_Canvas.Helpers;
using Microsoft.Office.Interop.PowerPoint;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Application = System.Windows.Application;
using File = System.IO.File;
using MessageBox = System.Windows.MessageBox;
using iNKORE.UI.WPF.Modern;
using Microsoft.Office.Core;

namespace Ink_Canvas {
    public partial class MainWindow : Window {
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId, out uint lpdwThreadId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsZoomed(IntPtr hWnd);



        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out ForegroundWindowInfo.RECT lpRect);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        private const int GWL_STYLE = -16;
        private const int WS_VISIBLE = 0x10000000;
        private const int WS_MINIMIZE = 0x20000000;
        private const uint GW_HWNDNEXT = 2;
        private const uint GW_HWNDPREV = 3;



        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        public static Microsoft.Office.Interop.PowerPoint.Application pptApplication = null;
        public static Presentation presentation = null;
        public static Slides slides = null;
        public static Slide slide = null;
        public static int slidescount = 0;

        // 在类中添加字段
        private bool wasFloatingBarFoldedWhenEnterSlideShow = false;

        private void BtnCheckPPT_Click(object sender, RoutedEventArgs e) {
            try {
                pptApplication =
                    (Microsoft.Office.Interop.PowerPoint.Application)Marshal.GetActiveObject("kwpp.Application");
                //pptApplication.SlideShowWindows[1].View.Next();
                if (pptApplication != null) {
                    //获得演示文稿对象
                    presentation = pptApplication.ActivePresentation;
                    pptApplication.SlideShowBegin += PptApplication_SlideShowBegin;
                    pptApplication.SlideShowNextSlide += PptApplication_SlideShowNextSlide;
                    pptApplication.SlideShowEnd += PptApplication_SlideShowEnd;
                    // 获得幻灯片对象集合
                    slides = presentation.Slides;
                    // 获得幻灯片的数量
                    slidescount = slides.Count;
                    memoryStreams = new MemoryStream[slidescount + 2];
                    // 获得当前选中的幻灯片
                    try {
                        // 在普通视图下这种方式可以获得当前选中的幻灯片对象
                        // 然而在阅读模式下，这种方式会出现异常
                        slide = slides[pptApplication.ActiveWindow.Selection.SlideRange.SlideNumber];
                    }
                    catch {
                        // 在阅读模式下出现异常时，通过下面的方式来获得当前选中的幻灯片对象
                        try {
                            if (pptApplication.SlideShowWindows != null && pptApplication.SlideShowWindows.Count >= 1)
                            {
                                slide = pptApplication.SlideShowWindows[1].View.Slide;
                            }
                        }
                        catch (Exception ex) {
                            LogHelper.WriteLogToFile($"获取当前幻灯片失败: {ex.ToString()}", LogHelper.LogType.Error);
                        }
                    }
                }

                if (pptApplication == null) throw new Exception();
                //BtnCheckPPT.Visibility = Visibility.Collapsed;
                StackPanelPPTControls.Visibility = Visibility.Visible;
            }
            catch (Exception ex) {
                LogHelper.WriteLogToFile($"检查PPT应用程序失败: {ex.ToString()}", LogHelper.LogType.Error);
                //BtnCheckPPT.Visibility = Visibility.Visible;
                StackPanelPPTControls.Visibility = Visibility.Collapsed;
                LeftBottomPanelForPPTNavigation.Visibility = Visibility.Collapsed;
                RightBottomPanelForPPTNavigation.Visibility = Visibility.Collapsed;
                LeftSidePanelForPPTNavigation.Visibility = Visibility.Collapsed;
                RightSidePanelForPPTNavigation.Visibility = Visibility.Collapsed;
                MessageBox.Show("未找到幻灯片");
            }
        }

        private void ToggleSwitchSupportWPS_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;

            Settings.PowerPointSettings.IsSupportWPS = ToggleSwitchSupportWPS.IsOn;
            SaveSettingsToFile();
        }

        private static bool isWPSSupportOn => Settings.PowerPointSettings.IsSupportWPS;

        public static bool IsShowingRestoreHiddenSlidesWindow = false;
        private static bool IsShowingAutoplaySlidesWindow = false;

        // WPP 相关变量
        private static Process wppProcess = null;
        private static bool hasWppProcessID = false;
        private static System.Timers.Timer wppProcessCheckTimer = null;
        private static DateTime wppProcessRecordTime = DateTime.MinValue; // 记录进程时间
        private static int wppProcessCheckCount = 0; // 检查次数计数器
        private static WpsWindowInfo lastForegroundWpsWindow = null; // 记录上次检测到的前台WPS窗口
        private static DateTime lastWindowCheckTime = DateTime.MinValue; // 记录上次窗口检查时间


        private void TimerCheckPPT_Elapsed(object sender, ElapsedEventArgs e) {
            if (IsShowingRestoreHiddenSlidesWindow || IsShowingAutoplaySlidesWindow) return;
            try {
                
                pptApplication =
                    (Microsoft.Office.Interop.PowerPoint.Application)Marshal.GetActiveObject("PowerPoint.Application");

                if (pptApplication != null) {
                    timerCheckPPT.Stop();
                    //获得演示文稿对象
                    presentation = pptApplication.ActivePresentation;

                    // 获得幻灯片对象集合
                    slides = presentation.Slides;

                    // 获得幻灯片的数量
                    slidescount = slides.Count;
                    memoryStreams = new MemoryStream[slidescount + 2];
                    // 获得当前选中的幻灯片
                    try {
                        // 在普通视图下这种方式可以获得当前选中的幻灯片对象
                        // 然而在阅读模式下，这种方式会出现异常
                        slide = slides[pptApplication.ActiveWindow.Selection.SlideRange.SlideNumber];
                    }
                    catch {
                        // 在阅读模式下出现异常时，通过下面的方式来获得当前选中的幻灯片对象
                        try {
                            if (pptApplication.SlideShowWindows != null && pptApplication.SlideShowWindows.Count >= 1)
                            {
                                slide = pptApplication.SlideShowWindows[1].View.Slide;
                            }
                        }
                        catch (Exception ex) {
                            LogHelper.WriteLogToFile($"获取当前幻灯片失败: {ex.ToString()}", LogHelper.LogType.Error);
                        }
                    }

                    pptApplication.PresentationOpen += PptApplication_PresentationOpen;
                    pptApplication.PresentationClose += PptApplication_PresentationClose;
                    pptApplication.SlideShowBegin += PptApplication_SlideShowBegin;
                    pptApplication.SlideShowNextSlide += PptApplication_SlideShowNextSlide;
                    pptApplication.SlideShowEnd += PptApplication_SlideShowEnd;
                }

                if (pptApplication == null) return;
                //BtnCheckPPT.Visibility = Visibility.Collapsed;

                // 此处是已经开启了
                PptApplication_PresentationOpen(null);

                //如果检测到已经开始放映，则立即进入画板模式
                if (pptApplication.SlideShowWindows != null && pptApplication.SlideShowWindows.Count >= 1)
                {
                    try {
                        PptApplication_SlideShowBegin(pptApplication.SlideShowWindows[1]);
                    }
                    catch (Exception ex) {
                        LogHelper.WriteLogToFile($"启动幻灯片放映失败: {ex.ToString()}", LogHelper.LogType.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                // 忽略常见的COM对象失效错误
                if (ex is System.Runtime.InteropServices.COMException comEx)
                {
                    uint hr = (uint)comEx.HResult;
                    // 0x800401E3: 操作无法使用
                    // 0x80004005: 未指定错误（常见于PPT已关闭）
                    // 0x800706B5: RPC服务器不可用
                    // 0x80048240: 没有活动的演示文稿
                    // 0x800706BE: 远程过程调用失败
                    if (hr == 0x800401E3 || hr == 0x80004005 || hr == 0x800706B5 || hr == 0x80048240 || hr == 0x800706BE)
                    {
                        Application.Current.Dispatcher.Invoke(() => { BtnPPTSlideShow.Visibility = Visibility.Collapsed; });
                        timerCheckPPT.Start();
                        return;
                    }
                }
                LogHelper.WriteLogToFile($"检查PPT状态失败: {ex.ToString()}", LogHelper.LogType.Error);
                Application.Current.Dispatcher.Invoke(() => { BtnPPTSlideShow.Visibility = Visibility.Collapsed; });
                timerCheckPPT.Start();
            }
        }

        private void PptApplication_PresentationOpen(Presentation Pres) {
            // 新增逻辑：如果开启"重新进入放映时回到首页"，则直接跳转第一页
            if (Settings.PowerPointSettings.IsAlwaysGoToFirstPageOnReenter)
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                    try
                    {
                        if (presentation == null)
                        {
                            LogHelper.WriteLogToFile("演示文稿为空，无法跳转到首页", LogHelper.LogType.Warning);
                            return;
                        }
                        if (pptApplication != null && pptApplication.SlideShowWindows != null && pptApplication.SlideShowWindows.Count >= 1)
                            presentation.SlideShowWindow.View.GotoSlide(1);
                        else if (presentation.Windows != null && presentation.Windows.Count >= 1)
                            presentation.Windows[1].View.GotoSlide(1);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"跳转到首页失败: {ex.ToString()}", LogHelper.LogType.Error);
                    }
                }), DispatcherPriority.Normal);
            }
            else if (Settings.PowerPointSettings.IsNotifyPreviousPage)
                Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                    try {
                        // 添加安全检查
                        if (presentation == null)
                        {
                            LogHelper.WriteLogToFile("演示文稿为空，无法跳转到上次播放页", LogHelper.LogType.Warning);
                            return;
                        }

                        // 使用更精确的文件标识符：文件名_页数_文件路径哈希值
                        string presentationPath = presentation.FullName;
                        string fileHash = GetFileHash(presentationPath);
                        string folderName = presentation.Name + "_" + presentation.Slides.Count + "_" + fileHash;
                        var folderPath = Settings.Automation.AutoSavedStrokesLocation +
                                         @"\Auto Saved - Presentations\" + folderName;
                        try {
                            if (!File.Exists(folderPath + "/Position")) return;
                            if (!int.TryParse(File.ReadAllText(folderPath + "/Position"), out var page)) return;
                            if (page <= 0) return;
                            new YesOrNoNotificationWindow($"上次播放到了第 {page} 页, 是否立即跳转", () => {
                                try {
                                    if (pptApplication != null && pptApplication.SlideShowWindows != null && pptApplication.SlideShowWindows.Count >= 1)
                                        // 如果已经播放了的话, 跳转
                                        presentation.SlideShowWindow.View.GotoSlide(page);
                                    else if (presentation.Windows != null && presentation.Windows.Count >= 1)
                                        presentation.Windows[1].View.GotoSlide(page);
                                }
                                catch (Exception ex) {
                                    LogHelper.WriteLogToFile($"跳转到指定页面失败: {ex.ToString()}", LogHelper.LogType.Error);
                                }
                            }).ShowDialog();
                        }
                        catch (Exception ex) {
                            LogHelper.WriteLogToFile($"读取上次播放位置失败: {ex.ToString()}", LogHelper.LogType.Error);
                        }
                    }
                    catch (Exception ex) {
                        LogHelper.WriteLogToFile($"处理上次播放页跳转失败: {ex.ToString()}", LogHelper.LogType.Error);
                    }
                }), DispatcherPriority.Normal);


            //检查是否有隐藏幻灯片
            if (Settings.PowerPointSettings.IsNotifyHiddenPage) {
                try {
                    var isHaveHiddenSlide = false;
                    if (slides != null)
                    {
                        foreach (Slide slide in slides)
                            if (slide.SlideShowTransition.Hidden == Microsoft.Office.Core.MsoTriState.msoTrue) {
                                isHaveHiddenSlide = true;
                                break;
                            }
                    }

                    Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                        if (isHaveHiddenSlide && !IsShowingRestoreHiddenSlidesWindow) {
                            IsShowingRestoreHiddenSlidesWindow = true;
                            new YesOrNoNotificationWindow("检测到此演示文档中包含隐藏的幻灯片，是否取消隐藏？",
                                () => {
                                    try {
                                        if (slides != null)
                                        {
                                            foreach (Slide slide in slides)
                                                if (slide.SlideShowTransition.Hidden ==
                                                    Microsoft.Office.Core.MsoTriState.msoTrue)
                                                    slide.SlideShowTransition.Hidden =
                                                        Microsoft.Office.Core.MsoTriState.msoFalse;
                                        }
                                    }
                                    catch (Exception ex) {
                                        LogHelper.WriteLogToFile($"取消隐藏幻灯片失败: {ex.ToString()}", LogHelper.LogType.Error);
                                    }
                                    finally {
                                        IsShowingRestoreHiddenSlidesWindow = false;
                                    }
                                }, () => { IsShowingRestoreHiddenSlidesWindow = false; },
                                () => { IsShowingRestoreHiddenSlidesWindow = false; }).ShowDialog();
                        }

                        BtnPPTSlideShow.Visibility = Visibility.Visible;
                    }), DispatcherPriority.Normal);
                }
                catch (Exception ex) {
                    LogHelper.WriteLogToFile($"检查隐藏幻灯片失败: {ex.ToString()}", LogHelper.LogType.Error);
                }
            }

            //检测是否有自动播放
            if (Settings.PowerPointSettings.IsNotifyAutoPlayPresentation
                // && presentation.SlideShowSettings.AdvanceMode == PpSlideShowAdvanceMode.ppSlideShowUseSlideTimings
                && BtnPPTSlideShowEnd.Visibility != Visibility.Visible) {
                try {
                    bool hasSlideTimings = false;
                    if (presentation != null && presentation.Slides != null)
                    {
                        foreach (Slide slide in presentation.Slides) {
                            if (slide.SlideShowTransition.AdvanceOnTime == MsoTriState.msoTrue &&
                                slide.SlideShowTransition.AdvanceTime > 0) {
                                hasSlideTimings = true;
                                break;
                            }
                        }
                    }

                    if (hasSlideTimings) {
                        Application.Current.Dispatcher.BeginInvoke((Action)(() => {
                            if (hasSlideTimings && !IsShowingAutoplaySlidesWindow) {
                                IsShowingAutoplaySlidesWindow = true;
                                new YesOrNoNotificationWindow("检测到此演示文档中自动播放或排练计时已经启用，可能导致幻灯片自动翻页，是否取消？",
                                    () => {
                                        try {
                                            if (presentation != null)
                                            {
                                                presentation.SlideShowSettings.AdvanceMode =
                                                    PpSlideShowAdvanceMode.ppSlideShowManualAdvance;
                                            }
                                        }
                                        catch (Exception ex) {
                                            LogHelper.WriteLogToFile($"设置手动播放模式失败: {ex.ToString()}", LogHelper.LogType.Error);
                                        }
                                        finally {
                                            IsShowingAutoplaySlidesWindow = false;
                                        }
                                    }, () => { IsShowingAutoplaySlidesWindow = false; },
                                    () => { IsShowingAutoplaySlidesWindow = false; }).ShowDialog();
                            }
                        }));
                        try {
                            if (presentation != null)
                            {
                                presentation.SlideShowSettings.AdvanceMode = PpSlideShowAdvanceMode.ppSlideShowManualAdvance;
                            }
                        }
                        catch (Exception ex) {
                            LogHelper.WriteLogToFile($"设置演示文稿播放模式失败: {ex.ToString()}", LogHelper.LogType.Error);
                        }
                    }
                }
                catch (Exception ex) {
                    LogHelper.WriteLogToFile($"检查自动播放设置失败: {ex.ToString()}", LogHelper.LogType.Error);
                }
            }
        }

        private void PptApplication_PresentationClose(Presentation Pres) {
            try {
                pptApplication.PresentationOpen -= PptApplication_PresentationOpen;
                pptApplication.PresentationClose -= PptApplication_PresentationClose;
                pptApplication.SlideShowBegin -= PptApplication_SlideShowBegin;
                pptApplication.SlideShowNextSlide -= PptApplication_SlideShowNextSlide;
                pptApplication.SlideShowEnd -= PptApplication_SlideShowEnd;
                
                
                timerCheckPPT.Start();
                
                Application.Current.Dispatcher.Invoke(() => {
                    BtnPPTSlideShow.Visibility = Visibility.Collapsed;
                    BtnPPTSlideShowEnd.Visibility = Visibility.Collapsed;
                });
            }
            catch (Exception ex) {
                LogHelper.WriteLogToFile(ex.ToString(), LogHelper.LogType.Error);
            }
        }

        private bool isPresentationHaveBlackSpace = false;
        private string pptName = null;

        private void UpdatePPTBtnStyleSettingsStatus() {
            try {
                var sopt = Settings.PowerPointSettings.PPTSButtonsOption.ToString();
                char[] soptc = sopt.ToCharArray();
                if (soptc[0] == '2')
                {
                    PPTLSPageButton.Visibility = Visibility.Visible;
                    PPTRSPageButton.Visibility = Visibility.Visible;
                }
                else
                {
                    PPTLSPageButton.Visibility = Visibility.Collapsed;
                    PPTRSPageButton.Visibility = Visibility.Collapsed;
                }
                if (soptc[2] == '2')
                {
                    // 这里先堆一点屎山，没空用Resources了
                    PPTBtnLSBorder.Background = new SolidColorBrush(Color.FromRgb(39, 39, 42));
                    PPTBtnRSBorder.Background = new SolidColorBrush(Color.FromRgb(39, 39, 42));
                    PPTBtnLSBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(82, 82, 91));
                    PPTBtnRSBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(82, 82, 91));
                    PPTLSPreviousButtonGeometry.Brush = new SolidColorBrush(Colors.White);
                    PPTRSPreviousButtonGeometry.Brush = new SolidColorBrush(Colors.White);
                    PPTLSNextButtonGeometry.Brush = new SolidColorBrush(Colors.White);
                    PPTRSNextButtonGeometry.Brush = new SolidColorBrush(Colors.White);
                    PPTLSPreviousButtonFeedbackBorder.Background = new SolidColorBrush(Colors.White);
                    PPTRSPreviousButtonFeedbackBorder.Background = new SolidColorBrush(Colors.White);
                    PPTLSPageButtonFeedbackBorder.Background = new SolidColorBrush(Colors.White);
                    PPTRSPageButtonFeedbackBorder.Background = new SolidColorBrush(Colors.White);
                    PPTLSNextButtonFeedbackBorder.Background = new SolidColorBrush(Colors.White);
                    PPTRSNextButtonFeedbackBorder.Background = new SolidColorBrush(Colors.White);
                    TextBlock.SetForeground(PPTLSPageButton, new SolidColorBrush(Colors.White));
                    TextBlock.SetForeground(PPTRSPageButton, new SolidColorBrush(Colors.White));
                }
                else
                {
                    PPTBtnLSBorder.Background = new SolidColorBrush(Color.FromRgb(244, 244, 245));
                    PPTBtnRSBorder.Background = new SolidColorBrush(Color.FromRgb(244, 244, 245));
                    PPTBtnLSBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(161, 161, 170));
                    PPTBtnRSBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(161, 161, 170));
                    PPTLSPreviousButtonGeometry.Brush = new SolidColorBrush(Color.FromRgb(39, 39, 42));
                    PPTRSPreviousButtonGeometry.Brush = new SolidColorBrush(Color.FromRgb(39, 39, 42));
                    PPTLSNextButtonGeometry.Brush = new SolidColorBrush(Color.FromRgb(39, 39, 42));
                    PPTRSNextButtonGeometry.Brush = new SolidColorBrush(Color.FromRgb(39, 39, 42));
                    PPTLSPreviousButtonFeedbackBorder.Background = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                    PPTRSPreviousButtonFeedbackBorder.Background = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                    PPTLSPageButtonFeedbackBorder.Background = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                    PPTRSPageButtonFeedbackBorder.Background = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                    PPTLSNextButtonFeedbackBorder.Background = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                    PPTRSNextButtonFeedbackBorder.Background = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                    TextBlock.SetForeground(PPTLSPageButton, new SolidColorBrush(Color.FromRgb(24, 24, 27)));
                    TextBlock.SetForeground(PPTRSPageButton, new SolidColorBrush(Color.FromRgb(24, 24, 27)));
                }
                if (soptc[1] == '2')
                {
                    PPTBtnLSBorder.Opacity = 0.5;
                    PPTBtnRSBorder.Opacity = 0.5;
                }
                else
                {
                    PPTBtnLSBorder.Opacity = 1;
                    PPTBtnRSBorder.Opacity = 1;
                }

                var bopt = Settings.PowerPointSettings.PPTBButtonsOption.ToString();
                char[] boptc = bopt.ToCharArray();
                if (boptc[0] == '2')
                {
                    PPTLBPageButton.Visibility = Visibility.Visible;
                    PPTRBPageButton.Visibility = Visibility.Visible;
                }
                else
                {
                    PPTLBPageButton.Visibility = Visibility.Collapsed;
                    PPTRBPageButton.Visibility = Visibility.Collapsed;
                }
                if (boptc[2] == '2')
                {
                    // 这里先堆一点屎山，没空用Resources了
                    PPTBtnLBBorder.Background = new SolidColorBrush(Color.FromRgb(39, 39, 42));
                    PPTBtnRBBorder.Background = new SolidColorBrush(Color.FromRgb(39, 39, 42));
                    PPTBtnLBBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(82, 82, 91));
                    PPTBtnRBBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(82, 82, 91));
                    PPTLBPreviousButtonGeometry.Brush = new SolidColorBrush(Colors.White);
                    PPTRBPreviousButtonGeometry.Brush = new SolidColorBrush(Colors.White);
                    PPTLBNextButtonGeometry.Brush = new SolidColorBrush(Colors.White);
                    PPTRBNextButtonGeometry.Brush = new SolidColorBrush(Colors.White);
                    PPTLBPreviousButtonFeedbackBorder.Background = new SolidColorBrush(Colors.White);
                    PPTRBPreviousButtonFeedbackBorder.Background = new SolidColorBrush(Colors.White);
                    PPTLBPageButtonFeedbackBorder.Background = new SolidColorBrush(Colors.White);
                    PPTRBPageButtonFeedbackBorder.Background = new SolidColorBrush(Colors.White);
                    PPTLBNextButtonFeedbackBorder.Background = new SolidColorBrush(Colors.White);
                    PPTRBNextButtonFeedbackBorder.Background = new SolidColorBrush(Colors.White);
                    TextBlock.SetForeground(PPTLBPageButton, new SolidColorBrush(Colors.White));
                    TextBlock.SetForeground(PPTRBPageButton, new SolidColorBrush(Colors.White));
                }
                else
                {
                    PPTBtnLBBorder.Background = new SolidColorBrush(Color.FromRgb(244, 244, 245));
                    PPTBtnRBBorder.Background = new SolidColorBrush(Color.FromRgb(244, 244, 245));
                    PPTBtnLBBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(161, 161, 170));
                    PPTBtnRBBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(161, 161, 170));
                    PPTLBPreviousButtonGeometry.Brush = new SolidColorBrush(Color.FromRgb(39, 39, 42));
                    PPTRBPreviousButtonGeometry.Brush = new SolidColorBrush(Color.FromRgb(39, 39, 42));
                    PPTLBNextButtonGeometry.Brush = new SolidColorBrush(Color.FromRgb(39, 39, 42));
                    PPTRBNextButtonGeometry.Brush = new SolidColorBrush(Color.FromRgb(39, 39, 42));
                    PPTLBPreviousButtonFeedbackBorder.Background = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                    PPTRBPreviousButtonFeedbackBorder.Background = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                    PPTLBPageButtonFeedbackBorder.Background = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                    PPTRBPageButtonFeedbackBorder.Background = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                    PPTLBNextButtonFeedbackBorder.Background = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                    PPTRBNextButtonFeedbackBorder.Background = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                    TextBlock.SetForeground(PPTLBPageButton, new SolidColorBrush(Color.FromRgb(24, 24, 27)));
                    TextBlock.SetForeground(PPTRBPageButton, new SolidColorBrush(Color.FromRgb(24, 24, 27)));
                }
                if (boptc[1] == '2')
                {
                    PPTBtnLBBorder.Opacity = 0.5;
                    PPTBtnRBBorder.Opacity = 0.5;
                }
                else
                {
                    PPTBtnLBBorder.Opacity = 1;
                    PPTBtnRBBorder.Opacity = 1;
                }
            }
            catch (Exception ex) {
                LogHelper.WriteLogToFile(ex.ToString(), LogHelper.LogType.Error);
            }
        }

        private void UpdatePPTBtnDisplaySettingsStatus() {
            try {
                // 检查是否应该显示PPT按钮
                bool shouldShowButtons = Settings.PowerPointSettings.ShowPPTButton && 
                    (BtnPPTSlideShowEnd.Visibility == Visibility.Visible || 
                    (pptApplication != null && pptApplication.SlideShowWindows != null && pptApplication.SlideShowWindows.Count > 0));

                if (!shouldShowButtons)
                {
                    LeftBottomPanelForPPTNavigation.Visibility = Visibility.Collapsed;
                    RightBottomPanelForPPTNavigation.Visibility = Visibility.Collapsed;
                    LeftSidePanelForPPTNavigation.Visibility = Visibility.Collapsed;
                    RightSidePanelForPPTNavigation.Visibility = Visibility.Collapsed;
                    return;
                }

                var lsp = Settings.PowerPointSettings.PPTLSButtonPosition;
                LeftSidePanelForPPTNavigation.Margin = new Thickness(0, 0, 0, lsp*2);
                var rsp = Settings.PowerPointSettings.PPTRSButtonPosition;
                RightSidePanelForPPTNavigation.Margin = new Thickness(0, 0, 0, rsp*2);

                var dopt = Settings.PowerPointSettings.PPTButtonsDisplayOption.ToString();
                char[] doptc = dopt.ToCharArray();
                if (doptc[0] == '2') AnimationsHelper.ShowWithFadeIn(LeftBottomPanelForPPTNavigation);
                else LeftBottomPanelForPPTNavigation.Visibility = Visibility.Collapsed;
                if (doptc[1] == '2') AnimationsHelper.ShowWithFadeIn(RightBottomPanelForPPTNavigation);
                else RightBottomPanelForPPTNavigation.Visibility = Visibility.Collapsed;
                if (doptc[2] == '2') AnimationsHelper.ShowWithFadeIn(LeftSidePanelForPPTNavigation);
                else LeftSidePanelForPPTNavigation.Visibility = Visibility.Collapsed;
                if (doptc[3] == '2') AnimationsHelper.ShowWithFadeIn(RightSidePanelForPPTNavigation);
                else RightSidePanelForPPTNavigation.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex) {
                LogHelper.WriteLogToFile($"更新PPT按钮显示状态失败: {ex.ToString()}", LogHelper.LogType.Error);
            }
        }

        private async void PptApplication_SlideShowBegin(SlideShowWindow Wn) {
            try {
                // 记录进入放映时浮动栏收纳状态
                wasFloatingBarFoldedWhenEnterSlideShow = isFloatingBarFolded;
                
                if (Settings.Automation.IsAutoFoldInPPTSlideShow && !isFloatingBarFolded)
                    FoldFloatingBar_MouseUp(new object(), null);
                else if (isFloatingBarFolded) await UnFoldFloatingBar(new object());

                isStopInkReplay = true;

                LogHelper.WriteLogToFile("PowerPoint Application Slide Show Begin", LogHelper.LogType.Event);

                await Application.Current.Dispatcher.InvokeAsync(() => {
                    // 新增：如果设置开启，进入放映时强制跳转到第一页
                    if (Settings.PowerPointSettings.IsAlwaysGoToFirstPageOnReenter)
                    {
                        try
                        {
                            if (Wn != null && Wn.Presentation != null && Wn.View != null)
                            {
                                Wn.View.GotoSlide(1);
                            }
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLogToFile($"放映开始时跳转首页失败: {ex.ToString()}", LogHelper.LogType.Error);
                        }
                    }

                    //调整颜色
                    var screenRatio = SystemParameters.PrimaryScreenWidth / SystemParameters.PrimaryScreenHeight;
                    if (Math.Abs(screenRatio - 16.0 / 9) <= -0.01) {
                        if (Wn.Presentation.PageSetup.SlideWidth / Wn.Presentation.PageSetup.SlideHeight < 1.65) {
                            isPresentationHaveBlackSpace = true;

                            if (BtnSwitchTheme.Content.ToString() == "深色") {
                                //Light
                                BtnExit.Foreground = Brushes.White;
                                ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;
                            } else {
                                //Dark
                            }
                        }
                    } else if (screenRatio == -256 / 135) { }

                    lastDesktopInkColor = 1;

                    slidescount = Wn.Presentation.Slides.Count;
                    previousSlideID = 0;
                    memoryStreams = new MemoryStream[slidescount + 2];

                    pptName = Wn.Presentation.Name;
                    LogHelper.NewLog("Name: " + Wn.Presentation.Name);
                    LogHelper.NewLog("Slides Count: " + slidescount.ToString());

                    //检查是否有已有墨迹，并加载
                    if (Settings.PowerPointSettings.IsAutoSaveStrokesInPowerPoint)
                    {
                        // 使用更精确的文件标识符：文件名_页数_文件路径哈希值
                        string presentationPath = Wn.Presentation.FullName;
                        string fileHash = GetFileHash(presentationPath);
                        string folderName = Wn.Presentation.Name + "_" + Wn.Presentation.Slides.Count + "_" + fileHash;
                        
                        if (Directory.Exists(Settings.Automation.AutoSavedStrokesLocation +
                                             @"\Auto Saved - Presentations\" + folderName)) {
                            LogHelper.WriteLogToFile("Found saved strokes", LogHelper.LogType.Trace);
                            var files = new DirectoryInfo(Settings.Automation.AutoSavedStrokesLocation +
                                                          @"\Auto Saved - Presentations\" + folderName).GetFiles();
                            var count = 0;
                            foreach (var file in files)
                                if (file.Name != "Position") {
                                    var i = -1;
                                    try {
                                        i = int.Parse(Path.GetFileNameWithoutExtension(file.Name));
                                        memoryStreams[i] = new MemoryStream(File.ReadAllBytes(file.FullName));
                                        memoryStreams[i].Position = 0;
                                        count++;
                                    }
                                    catch (Exception ex) {
                                        LogHelper.WriteLogToFile(
                                            $"Failed to load strokes on Slide {i}\n{ex.ToString()}",
                                            LogHelper.LogType.Error);
                                    }
                                }

                            LogHelper.WriteLogToFile($"Loaded {count.ToString()} saved strokes");
                        }
                    }

                    StackPanelPPTControls.Visibility = Visibility.Visible;
                    UpdatePPTBtnDisplaySettingsStatus();
                    UpdatePPTBtnStyleSettingsStatus();

                    BtnPPTSlideShow.Visibility = Visibility.Collapsed;
                    BtnPPTSlideShowEnd.Visibility = Visibility.Visible;
                    ViewBoxStackPanelMain.Margin = new Thickness(10, 10, 10, 10);
                    ViewboxFloatingBar.Opacity = Settings.Appearance.ViewboxFloatingBarOpacityInPPTValue;

                    if (Settings.PowerPointSettings.IsShowCanvasAtNewSlideShow &&
                        !Settings.Automation.IsAutoFoldInPPTSlideShow &&
                        GridTransparencyFakeBackground.Background == Brushes.Transparent && !isFloatingBarFolded) {
                        BtnHideInkCanvas_Click(BtnHideInkCanvas, null);
                    }

                    if (currentMode != 0)
                    {
                        ImageBlackboard_MouseUp(null,null);
                        BtnHideInkCanvas_Click(BtnHideInkCanvas, null);
                    }

                    BorderFloatingBarMainControls.Visibility = Visibility.Visible;

                    if (Settings.PowerPointSettings.IsShowCanvasAtNewSlideShow &&
                        !Settings.Automation.IsAutoFoldInPPTSlideShow)
                        BtnColorRed_Click(null, null);

                    isEnteredSlideShowEndEvent = false;
                    PPTBtnPageNow.Text = $"{Wn.View.CurrentShowPosition}";
                    PPTBtnPageTotal.Text = $"/ {Wn.Presentation.Slides.Count}";
                    LogHelper.NewLog("PowerPoint Slide Show Loading process complete");

                    // 新增：主动加载当前页墨迹，解决首次放映时当前页墨迹不显示的问题
                    try
                    {
                        var currentPage = Wn.View.CurrentShowPosition;
                        if (memoryStreams != null && currentPage < memoryStreams.Length && memoryStreams[currentPage] != null && memoryStreams[currentPage].Length > 0)
                        {
                            memoryStreams[currentPage].Position = 0;
                            inkCanvas.Strokes.Clear();
                            inkCanvas.Strokes.Add(new StrokeCollection(memoryStreams[currentPage]));
                        }
                        else
                        {
                            inkCanvas.Strokes.Clear();
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"加载当前页墨迹失败: {ex.ToString()}", LogHelper.LogType.Error);
                    }

                    if (!isFloatingBarFolded) {
                        new Thread(new ThreadStart(() => {
                            Thread.Sleep(100);
                            Application.Current.Dispatcher.Invoke(() => {
                                ViewboxFloatingBarMarginAnimation(60);
                            });
                        })).Start();
                    }
                });
                await Application.Current.Dispatcher.InvokeAsync(() => {
                    if (BtnExitPptFromSidebarLeft != null)
                        BtnExitPptFromSidebarLeft.Visibility = Visibility.Visible;
                    if (BtnExitPptFromSidebarRight != null)
                        BtnExitPptFromSidebarRight.Visibility = Visibility.Visible;
                });
            }
            catch (Exception ex) {
                LogHelper.WriteLogToFile("PowerPoint Application Slide Show Begin Error: " + ex.ToString(), LogHelper.LogType.Error);
                LogHelper.WriteLogToFile(ex.ToString(), LogHelper.LogType.Error);
            }
        }

        private bool isEnteredSlideShowEndEvent = false; //防止重复调用本函数导致墨迹保存失效

        private async void PptApplication_SlideShowEnd(Presentation Pres) {
            try {
                // 新增逻辑：如果设置开启且进入PPT放映时浮动栏是收纳的，退出时也自动收纳；否则自动展开
                if (Settings.Automation.IsAutoFoldAfterPPTSlideShow && wasFloatingBarFoldedWhenEnterSlideShow) {
                    if (!isFloatingBarFolded) FoldFloatingBar_MouseUp(new object(), null);
                } else {
                    if (isFloatingBarFolded) await UnFoldFloatingBar(new object());
                }

                // 记录 WPP 进程 ID，用于后续检测未关闭的进程
                if (pptApplication != null)
                {
                    try
                    {
                        // 尝试多种方式获取WPS进程
                        Process wpsProcess = null;
                        
                        // 方法1：通过应用程序路径检测
                        if (pptApplication.Path.Contains("Kingsoft\\WPS Office\\") || 
                            pptApplication.Path.Contains("WPS Office\\"))
                {
                    uint processId;
                    GetWindowThreadProcessId((IntPtr)pptApplication.HWND, out processId);
                            wpsProcess = Process.GetProcessById((int)processId);
                            LogHelper.WriteLogToFile($"通过路径检测到WPS进程: {processId}", LogHelper.LogType.Trace);
                        }
                        
                        // 方法2：通过前台窗口检测
                        if (wpsProcess == null)
                        {
                            var foregroundWpsWindow = GetForegroundWpsWindow();
                            if (foregroundWpsWindow != null)
                            {
                                wpsProcess = Process.GetProcessById((int)foregroundWpsWindow.ProcessId);
                                LogHelper.WriteLogToFile($"通过前台窗口检测到WPS进程: {foregroundWpsWindow.ProcessId}", LogHelper.LogType.Trace);
                            }
                        }
                        
                        // 方法3：通过进程名检测
                        if (wpsProcess == null)
                        {
                            var wpsProcesses = GetWpsProcesses();
                            if (wpsProcesses.Count > 0)
                            {
                                wpsProcess = wpsProcesses.First();
                                LogHelper.WriteLogToFile($"通过进程名检测到WPS进程: {wpsProcess.Id}", LogHelper.LogType.Trace);
                            }
                        }
                        
                        if (wpsProcess != null)
                        {
                            wppProcess = wpsProcess;
                    hasWppProcessID = true;
                    wppProcessRecordTime = DateTime.Now;
                    wppProcessCheckCount = 0;
                            LogHelper.WriteLogToFile($"成功记录 WPP 进程 ID: {wpsProcess.Id}", LogHelper.LogType.Trace);
                        }
                        else
                        {
                            LogHelper.WriteLogToFile("未能检测到WPS进程", LogHelper.LogType.Warning);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"记录WPS进程失败: {ex.ToString()}", LogHelper.LogType.Error);
                    }
                } 

                LogHelper.WriteLogToFile(string.Format("PowerPoint Slide Show End"), LogHelper.LogType.Event);
                if (isEnteredSlideShowEndEvent) {
                    LogHelper.WriteLogToFile("Detected previous entrance, returning");
                    return;
                }

                isEnteredSlideShowEndEvent = true;
                if (Settings.PowerPointSettings.IsAutoSaveStrokesInPowerPoint) {
                   // 使用更精确的文件标识符：文件名_页数_文件路径哈希值
                   string presentationPath = Pres.FullName;
                   string fileHash = GetFileHash(presentationPath);
                   string folderName = Pres.Name + "_" + Pres.Slides.Count + "_" + fileHash;
                   var folderPath = Settings.Automation.AutoSavedStrokesLocation + @"\Auto Saved - Presentations\" + folderName;
                    if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);
                    try {
                        File.WriteAllText(folderPath + "/Position", previousSlideID.ToString());
                    }
                    catch (Exception ex) {
                        LogHelper.WriteLogToFile(ex.ToString(), LogHelper.LogType.Error);
                    }

                    for (var i = 1; i <= Pres.Slides.Count; i++)
                        if (memoryStreams[i] != null)
                            try {
                                if (memoryStreams[i].Length > 8) {
                                    var srcBuf = new byte[memoryStreams[i].Length];
                                    memoryStreams[i].Position = 0;
                                    var byteLength = memoryStreams[i].Read(srcBuf, 0, srcBuf.Length);
                                    // 使用Path.Combine构建文件路径
                                     File.WriteAllBytes(folderPath + @"\" + i.ToString("0000") + ".icstk", srcBuf);
                                    LogHelper.WriteLogToFile(string.Format(
                                        "Saved strokes for Slide {0}, size={1}, byteLength={2}", i.ToString(),
                                        memoryStreams[i].Length, byteLength));
                                } else {
                                     if (File.Exists(folderPath + @"\" + i.ToString("0000") + ".icstk"))
                                         File.Delete(folderPath + @"\" + i.ToString("0000") + ".icstk");
                                }
                            }
                            catch (Exception ex) {
                                LogHelper.WriteLogToFile(
                                    $"Failed to save strokes for Slide {i}\n{ex.ToString()}",
                                    LogHelper.LogType.Error);
                                if (File.Exists(folderPath + @"\" + i.ToString("0000") + ".icstk"))
                                    File.Delete(folderPath + @"\" + i.ToString("0000") + ".icstk");
                            }
                }

                await Application.Current.Dispatcher.InvokeAsync(() => {
                    try {
                        isPresentationHaveBlackSpace = false;

                        if (BtnSwitchTheme.Content.ToString() == "深色") {
                            //Light
                            BtnExit.Foreground = Brushes.Black;
                            ThemeManager.Current.ApplicationTheme = ApplicationTheme.Light;
                        } else {
                            //Dark
                        }

                        BtnPPTSlideShow.Visibility = Visibility.Visible;
                        BtnPPTSlideShowEnd.Visibility = Visibility.Collapsed;
                        StackPanelPPTControls.Visibility = Visibility.Collapsed;
                        LeftBottomPanelForPPTNavigation.Visibility = Visibility.Collapsed;
                        RightBottomPanelForPPTNavigation.Visibility = Visibility.Collapsed;
                        LeftSidePanelForPPTNavigation.Visibility = Visibility.Collapsed;
                        RightSidePanelForPPTNavigation.Visibility = Visibility.Collapsed;

                        ViewBoxStackPanelMain.Margin = new Thickness(10, 10, 10, 55);

                        if (currentMode != 0) {
                            CloseWhiteboardImmediately();
                            currentMode = 0;
                        }

                        ClearStrokes(true);

                        if (GridTransparencyFakeBackground.Background != Brushes.Transparent)
                            BtnHideInkCanvas_Click(BtnHideInkCanvas, null);

                        ViewboxFloatingBar.Opacity = Settings.Appearance.ViewboxFloatingBarOpacityValue;
                    }
                    catch (Exception ex) {
                        LogHelper.WriteLogToFile(ex.ToString(), LogHelper.LogType.Error);
                    }
                });

                await Task.Delay(150);

                await Application.Current.Dispatcher.InvokeAsync(() => {
                    ViewboxFloatingBarMarginAnimation(100, true);
                });

                // 启动 WPP 进程检测定时器
                if (hasWppProcessID && wppProcess != null)
                {
                    StartWppProcessCheckTimer();
                }
            }
            catch (Exception ex) {
                LogHelper.WriteLogToFile(ex.ToString(), LogHelper.LogType.Error);
            }
        }

        private int previousSlideID = 0;
        private MemoryStream[] memoryStreams = new MemoryStream[50];

        private void PptApplication_SlideShowNextSlide(SlideShowWindow Wn) {
            try {
                // 添加安全检查
                if (Wn == null || Wn.View == null)
                {
                    LogHelper.WriteLogToFile("幻灯片放映窗口或视图为空", LogHelper.LogType.Warning);
                    return;
                }

                LogHelper.WriteLogToFile($"PowerPoint Next Slide (Slide {Wn.View.CurrentShowPosition})",
                    LogHelper.LogType.Event);
                if (Wn.View.CurrentShowPosition == previousSlideID) return;
                
                Application.Current.Dispatcher.Invoke(() => {
                    try {
                        var ms = new MemoryStream();
                        inkCanvas.Strokes.Save(ms);
                        ms.Position = 0;
                        memoryStreams[previousSlideID] = ms;

                        if (inkCanvas.Strokes.Count > Settings.Automation.MinimumAutomationStrokeNumber &&
                            Settings.PowerPointSettings.IsAutoSaveScreenShotInPowerPoint && !_isPptClickingBtnTurned)
                            SaveScreenShot(true, Wn.Presentation.Name + "/" + Wn.View.CurrentShowPosition);
                        _isPptClickingBtnTurned = false;

                        ClearStrokes(true);
                        timeMachine.ClearStrokeHistory();

                        if (memoryStreams[Wn.View.CurrentShowPosition] != null &&
                            memoryStreams[Wn.View.CurrentShowPosition].Length > 0) {
                            memoryStreams[Wn.View.CurrentShowPosition].Position = 0;
                            inkCanvas.Strokes.Add(new StrokeCollection(memoryStreams[Wn.View.CurrentShowPosition]));
                        }

                        PPTBtnPageNow.Text = $"{Wn.View.CurrentShowPosition}";
                        PPTBtnPageTotal.Text = $"/ {Wn.Presentation.Slides.Count}";
                    }
                    catch (Exception ex) {
                        LogHelper.WriteLogToFile($"处理幻灯片切换时出错: {ex.ToString()}", LogHelper.LogType.Error);
                    }
                });
                
                previousSlideID = Wn.View.CurrentShowPosition;
            }
            catch (Exception ex) {
                LogHelper.WriteLogToFile($"幻灯片切换事件处理失败: {ex.ToString()}", LogHelper.LogType.Error);
            }
        }

        private bool _isPptClickingBtnTurned = false;

       private void BtnPPTSlidesUp_Click(object sender, RoutedEventArgs e) {
            if (currentMode == 1) {
                GridBackgroundCover.Visibility = Visibility.Collapsed;
                AnimationsHelper.HideWithSlideAndFade(BlackboardLeftSide);
                AnimationsHelper.HideWithSlideAndFade(BlackboardCenterSide);
                AnimationsHelper.HideWithSlideAndFade(BlackboardRightSide);
                currentMode = 0;
            }

            // 切换前同步保存墨迹
            Application.Current.Dispatcher.Invoke(() => {
                SaveCurrentSlideInkStrokes();
            });

            _isPptClickingBtnTurned = true;

            // 添加安全检查
            if (pptApplication == null)
            {
                LogHelper.WriteLogToFile("PPT应用程序为空，无法执行上一页操作", LogHelper.LogType.Warning);
                return;
            }

            try
            {
                // 检查SlideShowWindows是否存在且有效
                if (pptApplication.SlideShowWindows == null || pptApplication.SlideShowWindows.Count == 0)
                {
                    LogHelper.WriteLogToFile("PPT放映窗口不存在，无法执行上一页操作", LogHelper.LogType.Warning);
                    return;
                }

                // 安全访问当前幻灯片信息
                if (pptApplication.SlideShowWindows.Count >= 1)
                {
                    var slideShowWindow = pptApplication.SlideShowWindows[1];
                    if (slideShowWindow != null && slideShowWindow.View != null)
                    {
                        if (inkCanvas.Strokes.Count > Settings.Automation.MinimumAutomationStrokeNumber &&
                            Settings.PowerPointSettings.IsAutoSaveScreenShotInPowerPoint)
                            SaveScreenShot(true,
                                slideShowWindow.Presentation.Name + "/" +
                                slideShowWindow.View.CurrentShowPosition);
                    }
                }

                new Thread(new ThreadStart(() => {
                    try {
                        // 安全访问SlideShowWindows[1]
                        if (pptApplication.SlideShowWindows.Count >= 1)
                        {
                            var slideShowWindow = pptApplication.SlideShowWindows[1];
                            if (slideShowWindow != null)
                            {
                                slideShowWindow.Activate();
                            }
                        }
                    }
                    catch (Exception ex) {
                        LogHelper.WriteLogToFile($"激活PPT放映窗口失败: {ex.ToString()}", LogHelper.LogType.Error);
                    }

                    try {
                        // 安全访问SlideShowWindows[1]
                        if (pptApplication.SlideShowWindows.Count >= 1)
                        {
                            var slideShowWindow = pptApplication.SlideShowWindows[1];
                            if (slideShowWindow != null && slideShowWindow.View != null)
                            {
                                slideShowWindow.View.Previous();
                            }
                        }
                    }
                    catch (Exception ex) {
                        LogHelper.WriteLogToFile($"PPT上一页操作失败: {ex.ToString()}", LogHelper.LogType.Error);
                    } // Without this catch{}, app will crash when click the pre-page button in the fir page in some special env.
                })).Start();
            }
            catch (Exception ex) {
                LogHelper.WriteLogToFile($"PPT上一页操作异常: {ex.ToString()}", LogHelper.LogType.Error);
                StackPanelPPTControls.Visibility = Visibility.Collapsed;
                LeftBottomPanelForPPTNavigation.Visibility = Visibility.Collapsed;
                RightBottomPanelForPPTNavigation.Visibility = Visibility.Collapsed;
                LeftSidePanelForPPTNavigation.Visibility = Visibility.Collapsed;
                RightSidePanelForPPTNavigation.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnPPTSlidesDown_Click(object sender, RoutedEventArgs e) {
            if (currentMode == 1) {
                GridBackgroundCover.Visibility = Visibility.Collapsed;
                AnimationsHelper.HideWithSlideAndFade(BlackboardLeftSide);
                AnimationsHelper.HideWithSlideAndFade(BlackboardCenterSide);
                AnimationsHelper.HideWithSlideAndFade(BlackboardRightSide);
                currentMode = 0;
            }

            // 切换前同步保存墨迹
            Application.Current.Dispatcher.Invoke(() => {
                SaveCurrentSlideInkStrokes();
            });

            _isPptClickingBtnTurned = true;
            
            // 添加安全检查
            if (pptApplication == null)
            {
                LogHelper.WriteLogToFile("PPT应用程序为空，无法执行下一页操作", LogHelper.LogType.Warning);
                return;
            }

            try
            {
                // 检查SlideShowWindows是否存在且有效
                if (pptApplication.SlideShowWindows == null || pptApplication.SlideShowWindows.Count == 0)
                {
                    LogHelper.WriteLogToFile("PPT放映窗口不存在，无法执行下一页操作", LogHelper.LogType.Warning);
                    return;
                }

                // 安全访问当前幻灯片信息
                if (pptApplication.SlideShowWindows.Count >= 1)
                {
                    var slideShowWindow = pptApplication.SlideShowWindows[1];
                    if (slideShowWindow != null && slideShowWindow.View != null)
                    {
                        if (inkCanvas.Strokes.Count > Settings.Automation.MinimumAutomationStrokeNumber &&
                            Settings.PowerPointSettings.IsAutoSaveScreenShotInPowerPoint)
                            SaveScreenShot(true,
                                slideShowWindow.Presentation.Name + "/" +
                                slideShowWindow.View.CurrentShowPosition);
                    }
                }
                
                new Thread(new ThreadStart(() => {
                    try {
                        // 安全访问SlideShowWindows[1]
                        if (pptApplication.SlideShowWindows.Count >= 1)
                        {
                            var slideShowWindow = pptApplication.SlideShowWindows[1];
                            if (slideShowWindow != null)
                            {
                                slideShowWindow.Activate();
                            }
                        }
                    }
                    catch (Exception ex) {
                        LogHelper.WriteLogToFile($"激活PPT放映窗口失败: {ex.ToString()}", LogHelper.LogType.Error);
                    }

                    try {
                        // 安全访问SlideShowWindows[1]
                        if (pptApplication.SlideShowWindows.Count >= 1)
                        {
                            var slideShowWindow = pptApplication.SlideShowWindows[1];
                            if (slideShowWindow != null && slideShowWindow.View != null)
                            {
                                slideShowWindow.View.Next();
                            }
                        }
                    }
                    catch (Exception ex) {
                        LogHelper.WriteLogToFile($"PPT下一页操作失败: {ex.ToString()}", LogHelper.LogType.Error);
                    }
                })).Start();
            }
            catch (Exception ex) {
                LogHelper.WriteLogToFile($"PPT下一页操作异常: {ex.ToString()}", LogHelper.LogType.Error);
                StackPanelPPTControls.Visibility = Visibility.Collapsed;
                LeftBottomPanelForPPTNavigation.Visibility = Visibility.Collapsed;
                RightBottomPanelForPPTNavigation.Visibility = Visibility.Collapsed;
                LeftSidePanelForPPTNavigation.Visibility = Visibility.Collapsed;
                RightSidePanelForPPTNavigation.Visibility = Visibility.Collapsed;
            }
        }

        private async void PPTNavigationBtn_MouseDown(object sender, MouseButtonEventArgs e)
        {
            lastBorderMouseDownObject = sender;
            if (!Settings.PowerPointSettings.EnablePPTButtonPageClickable) return;
            if (sender == PPTLSPageButton)
            {
                PPTLSPageButtonFeedbackBorder.Opacity = 0.15;
            }
            else if (sender == PPTRSPageButton)
            {
                PPTRSPageButtonFeedbackBorder.Opacity = 0.15;
            }
            else if (sender == PPTLBPageButton)
            {
                PPTLBPageButtonFeedbackBorder.Opacity = 0.15;
            }
            else if (sender == PPTRBPageButton)
            {
                PPTRBPageButtonFeedbackBorder.Opacity = 0.15;
            }
        }

        private async void PPTNavigationBtn_MouseLeave(object sender, MouseEventArgs e)
        {
            lastBorderMouseDownObject = null;
            if (sender == PPTLSPageButton)
            {
                PPTLSPageButtonFeedbackBorder.Opacity = 0;
            }
            else if (sender == PPTRSPageButton)
            {
                PPTRSPageButtonFeedbackBorder.Opacity = 0;
            }
            else if (sender == PPTLBPageButton)
            {
                PPTLBPageButtonFeedbackBorder.Opacity = 0;
            }
            else if (sender == PPTRBPageButton)
            {
                PPTRBPageButtonFeedbackBorder.Opacity = 0;
            }
        }

        private async void PPTNavigationBtn_MouseUp(object sender, MouseButtonEventArgs e) {
            if (lastBorderMouseDownObject != sender) return;

            if (sender == PPTLSPageButton)
            {
                PPTLSPageButtonFeedbackBorder.Opacity = 0;
            }
            else if (sender == PPTRSPageButton)
            {
                PPTRSPageButtonFeedbackBorder.Opacity = 0;
            }
            else if (sender == PPTLBPageButton)
            {
                PPTLBPageButtonFeedbackBorder.Opacity = 0;
            }
            else if (sender == PPTRBPageButton)
            {
                PPTRBPageButtonFeedbackBorder.Opacity = 0;
            }

            if (!Settings.PowerPointSettings.EnablePPTButtonPageClickable) return;

            // 添加安全检查
            if (pptApplication == null)
            {
                LogHelper.WriteLogToFile("PPT应用程序为空，无法执行翻页操作", LogHelper.LogType.Warning);
                return;
            }

            try
            {
                // 检查SlideShowWindows是否存在且有效
                if (pptApplication.SlideShowWindows == null || pptApplication.SlideShowWindows.Count == 0)
                {
                    LogHelper.WriteLogToFile("PPT放映窗口不存在，无法执行翻页操作", LogHelper.LogType.Warning);
                    return;
                }

                GridTransparencyFakeBackground.Opacity = 1;
                GridTransparencyFakeBackground.Background = new SolidColorBrush(StringToColor("#01FFFFFF"));
                CursorIcon_Click(null, null);
                
                try {
                    // 安全访问SlideShowWindows[1]
                    if (pptApplication.SlideShowWindows.Count >= 1)
                    {
                        var slideShowWindow = pptApplication.SlideShowWindows[1];
                        if (slideShowWindow != null)
                        {
                            slideShowWindow.SlideNavigation.Visible = true;
                        }
                    }
                }
                catch (Exception ex) {
                    LogHelper.WriteLogToFile($"设置PPT导航可见性失败: {ex.ToString()}", LogHelper.LogType.Error);
                }

                // 控制居中
                if (!isFloatingBarFolded) {
                    await Task.Delay(100);
                    ViewboxFloatingBarMarginAnimation(60);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"PPT翻页控件操作失败: {ex.ToString()}", LogHelper.LogType.Error);
            }
        }

        private void BtnPPTSlideShow_Click(object sender, RoutedEventArgs e) {
            new Thread(new ThreadStart(() => {
                try {
                    presentation.SlideShowSettings.Run();
                }
                catch { }
            })).Start();
        }

        private async void BtnPPTSlideShowEnd_Click(object sender, RoutedEventArgs e) {
            // 添加安全检查
            if (pptApplication == null)
            {
                LogHelper.WriteLogToFile("PPT应用程序为空，无法结束放映", LogHelper.LogType.Warning);
                return;
            }

            // 切换前同步保存墨迹
            Application.Current.Dispatcher.Invoke(() => {
                SaveCurrentSlideInkStrokes();
            });

            try
            {
                // 检查SlideShowWindows是否存在且有效
                if (pptApplication.SlideShowWindows == null || pptApplication.SlideShowWindows.Count == 0)
                {
                    LogHelper.WriteLogToFile("PPT放映窗口不存在，无法结束放映", LogHelper.LogType.Warning);
                    return;
                }

                Application.Current.Dispatcher.Invoke(() => {
                    try {
                        // 安全访问SlideShowWindows[1]
                        if (pptApplication.SlideShowWindows.Count >= 1)
                        {
                            var slideShowWindow = pptApplication.SlideShowWindows[1];
                            if (slideShowWindow != null && slideShowWindow.View != null)
                            {
                                var ms = new MemoryStream();
                                inkCanvas.Strokes.Save(ms);
                                ms.Position = 0;
                                memoryStreams[slideShowWindow.View.CurrentShowPosition] = ms;
                                timeMachine.ClearStrokeHistory();
                            }
                        }
                    }
                    catch (Exception ex) {
                        LogHelper.WriteLogToFile($"保存当前页面墨迹失败: {ex.ToString()}", LogHelper.LogType.Error);
                    }
                });
                
                new Thread(new ThreadStart(() => {
                    try {
                        // 安全访问SlideShowWindows[1]
                        if (pptApplication.SlideShowWindows.Count >= 1)
                        {
                            var slideShowWindow = pptApplication.SlideShowWindows[1];
                            if (slideShowWindow != null && slideShowWindow.View != null)
                            {
                                slideShowWindow.View.Exit();
                            }
                        }
                    }
                    catch (Exception ex) {
                        LogHelper.WriteLogToFile($"退出PPT放映失败: {ex.ToString()}", LogHelper.LogType.Error);
                    }
                })).Start();

                HideSubPanels("cursor");
                await Task.Delay(150);
                ViewboxFloatingBarMarginAnimation(100, true);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"结束PPT放映操作异常: {ex.ToString()}", LogHelper.LogType.Error);
            }
            await Application.Current.Dispatcher.InvokeAsync(() => {
                if (BtnExitPptFromSidebarLeft != null)
                    BtnExitPptFromSidebarLeft.Visibility = Visibility.Collapsed;
                if (BtnExitPptFromSidebarRight != null)
                    BtnExitPptFromSidebarRight.Visibility = Visibility.Collapsed;
            });
        }

        private void GridPPTControlPrevious_MouseDown(object sender, MouseButtonEventArgs e)
        {
            lastBorderMouseDownObject = sender;
            if (sender == PPTLSPreviousButtonBorder) {
                PPTLSPreviousButtonFeedbackBorder.Opacity = 0.15;
            } else if (sender == PPTRSPreviousButtonBorder) {
                PPTRSPreviousButtonFeedbackBorder.Opacity = 0.15;
            } else if (sender == PPTLBPreviousButtonBorder)
            {
                PPTLBPreviousButtonFeedbackBorder.Opacity = 0.15;
            }
            else if (sender == PPTRBPreviousButtonBorder)
            {
                PPTRBPreviousButtonFeedbackBorder.Opacity = 0.15;
            }
        }
        private void GridPPTControlPrevious_MouseLeave(object sender, MouseEventArgs e)
        {
            lastBorderMouseDownObject = null;
            if (sender == PPTLSPreviousButtonBorder) {
                PPTLSPreviousButtonFeedbackBorder.Opacity = 0;
            } else if (sender == PPTRSPreviousButtonBorder) {
                PPTRSPreviousButtonFeedbackBorder.Opacity = 0;
            } else if (sender == PPTLBPreviousButtonBorder)
            {
                PPTLBPreviousButtonFeedbackBorder.Opacity = 0;
            }
            else if (sender == PPTRBPreviousButtonBorder)
            {
                PPTRBPreviousButtonFeedbackBorder.Opacity = 0;
            }
        }
        private void GridPPTControlPrevious_MouseUp(object sender, MouseButtonEventArgs e) {
            if (lastBorderMouseDownObject != sender) return;
            if (sender == PPTLSPreviousButtonBorder) {
                PPTLSPreviousButtonFeedbackBorder.Opacity = 0;
            } else if (sender == PPTRSPreviousButtonBorder) {
                PPTRSPreviousButtonFeedbackBorder.Opacity = 0;
            } else if (sender == PPTLBPreviousButtonBorder)
            {
                PPTLBPreviousButtonFeedbackBorder.Opacity = 0;
            }
            else if (sender == PPTRBPreviousButtonBorder)
            {
                PPTRBPreviousButtonFeedbackBorder.Opacity = 0;
            }
            BtnPPTSlidesUp_Click(BtnPPTSlidesUp, null);
        }


        private void GridPPTControlNext_MouseDown(object sender, MouseButtonEventArgs e) {
            lastBorderMouseDownObject = sender;
            if (sender == PPTLSNextButtonBorder) {
                PPTLSNextButtonFeedbackBorder.Opacity = 0.15;
            } else if (sender == PPTRSNextButtonBorder) {
                PPTRSNextButtonFeedbackBorder.Opacity = 0.15;
            } else if (sender == PPTLBNextButtonBorder)
            {
                PPTLBNextButtonFeedbackBorder.Opacity = 0.15;
            }
            else if (sender == PPTRBNextButtonBorder)
            {
                PPTRBNextButtonFeedbackBorder.Opacity = 0.15;
            }
        }
        private void GridPPTControlNext_MouseLeave(object sender, MouseEventArgs e)
        {
            lastBorderMouseDownObject = null;
            if (sender == PPTLSNextButtonBorder) {
                PPTLSNextButtonFeedbackBorder.Opacity = 0;
            } else if (sender == PPTRSNextButtonBorder) {
                PPTRSNextButtonFeedbackBorder.Opacity = 0;
            } else if (sender == PPTLBNextButtonBorder)
            {
                PPTLBNextButtonFeedbackBorder.Opacity = 0;
            }
            else if (sender == PPTRBNextButtonBorder)
            {
                PPTRBNextButtonFeedbackBorder.Opacity = 0;
            }
        }
        private void GridPPTControlNext_MouseUp(object sender, MouseButtonEventArgs e) {
            if (lastBorderMouseDownObject != sender) return;
            if (sender == PPTLSNextButtonBorder) {
                PPTLSNextButtonFeedbackBorder.Opacity = 0;
            } else if (sender == PPTRSNextButtonBorder) {
                PPTRSNextButtonFeedbackBorder.Opacity = 0;
            } else if (sender == PPTLBNextButtonBorder)
            {
                PPTLBNextButtonFeedbackBorder.Opacity = 0;
            }
            else if (sender == PPTRBNextButtonBorder)
            {
                PPTRBNextButtonFeedbackBorder.Opacity = 0;
            }
            BtnPPTSlidesDown_Click(BtnPPTSlidesDown, null);
        }

        private void ImagePPTControlEnd_MouseUp(object sender, MouseButtonEventArgs e) {
            BtnPPTSlideShowEnd_Click(BtnPPTSlideShowEnd, null);
        }

        private void StartWppProcessCheckTimer()
        {
            // 新增：WPS联动未启用时不查杀wpp进程
            if (!Settings.PowerPointSettings.IsSupportWPS)
            {
                LogHelper.WriteLogToFile("WPS联动未启用，跳过WPP进程查杀", LogHelper.LogType.Trace);
                return;
            }

            if (wppProcessCheckTimer != null)
            {
                wppProcessCheckTimer.Stop();
                wppProcessCheckTimer.Dispose();
            }

            wppProcessCheckTimer = new System.Timers.Timer(500); // 改为500ms检查一次，提高响应速度
            wppProcessCheckTimer.Elapsed += WppProcessCheckTimer_Elapsed;
            wppProcessCheckTimer.Start();
            LogHelper.WriteLogToFile("启动 WPP 进程检测定时器（前台窗口监控模式）", LogHelper.LogType.Trace);
        }

        private void WppProcessCheckTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            // 新增：WPS联动未启用时不查杀wpp进程
            if (!Settings.PowerPointSettings.IsSupportWPS)
            {
                LogHelper.WriteLogToFile("WPS联动未启用，跳过WPP进程查杀", LogHelper.LogType.Trace);
                StopWppProcessCheckTimer();
                return;
            }

            try
            {
                if (wppProcess == null || hasWppProcessID == false)
                {
                    StopWppProcessCheckTimer();
                    return;
                }

                if (!Settings.PowerPointSettings.EnableWppProcessKill)
                {
                    LogHelper.WriteLogToFile("WPP进程查杀功能已被关闭，跳过查杀", LogHelper.LogType.Trace);
                    StopWppProcessCheckTimer();
                    return;
                }

                // 刷新进程状态
                wppProcess.Refresh();
                wppProcessCheckCount++;

                if (wppProcess.HasExited)
                {
                    LogHelper.WriteLogToFile("WPP 进程已正常关闭", LogHelper.LogType.Trace);
                    StopWppProcessCheckTimer();
                    return;
                }

                // 检查前台WPS窗口是否存在
                bool isForegroundWpsWindowActive = IsForegroundWpsWindowStillActive();
                
                if (isForegroundWpsWindowActive)
                {
                    // 前台窗口仍然存在，继续监控
                    if (wppProcessCheckCount % 10 == 0) // 每5秒记录一次日志，避免日志过多
                    {
                        LogHelper.WriteLogToFile($"前台WPS窗口仍然存在，继续监控（已检查{wppProcessCheckCount}次）", LogHelper.LogType.Trace);
                    }
                    return;
                }
                
                // 前台窗口已消失，立即结束WPP进程
                LogHelper.WriteLogToFile("检测到前台WPS窗口已消失，立即结束WPP进程", LogHelper.LogType.Event);
                
                // 检查所有WPS文档是否已保存
                bool allSaved = true;
                try
                {
                    if (pptApplication != null)
                    {
                        foreach (Presentation pres in pptApplication.Presentations)
                        {
                            if (pres.Saved == MsoTriState.msoFalse)
                            {
                                allSaved = false;
                                break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"检查WPS文档保存状态失败: {ex.ToString()}", LogHelper.LogType.Error);
                    allSaved = false; // 出错时默认不安全
                }

                if (!allSaved)
                {
                    // 弹窗提示用户
                    bool userContinue = false;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var result = MessageBox.Show(
                            "检测到有未保存的WPS文档，强制关闭可能导致数据丢失。是否继续？",
                            "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                        userContinue = (result == MessageBoxResult.Yes);
                    });
                    if (!userContinue)
                    {
                        LogHelper.WriteLogToFile("用户取消了强制关闭WPS进程", LogHelper.LogType.Trace);
                        StopWppProcessCheckTimer();
                        return;
                    }
                }

                // 立即结束WPP进程
                try
                {
                    LogHelper.WriteLogToFile("前台窗口消失，开始结束WPP进程", LogHelper.LogType.Event);
                    
                    // 尝试优雅地结束进程
                    if (!wppProcess.HasExited)
                    {
                        wppProcess.Kill();
                        LogHelper.WriteLogToFile("前台窗口消失，成功结束WPP进程", LogHelper.LogType.Event);
                    }
                    else
                    {
                        LogHelper.WriteLogToFile("WPP进程已经自然结束", LogHelper.LogType.Trace);
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"结束WPP进程失败: {ex.ToString()}", LogHelper.LogType.Error);
                    
                    // 如果常规方法失败，尝试强制结束
                    try
                    {
                        var processes = Process.GetProcessesByName(wppProcess.ProcessName);
                        foreach (var process in processes)
                        {
                            if (process.Id == wppProcess.Id)
                            {
                                process.Kill();
                                LogHelper.WriteLogToFile("强制结束WPP进程成功", LogHelper.LogType.Event);
                                break;
                            }
                        }
                    }
                    catch (Exception forceKillEx)
                    {
                        LogHelper.WriteLogToFile($"强制结束WPP进程也失败: {forceKillEx.ToString()}", LogHelper.LogType.Error);
                    }
                }
                finally
                {
                    StopWppProcessCheckTimer();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"WPP 进程检测失败: {ex.ToString()}", LogHelper.LogType.Error);
                StopWppProcessCheckTimer();
            }
        }

        private bool CheckForOtherWpsWindows()
        {
            try
            {
                if (wppProcess != null)
                {
                    var wpsWindows = GetWpsWindowsByProcess(wppProcess.Id);
                    LogHelper.WriteLogToFile($"检测到{wpsWindows.Count}个WPS窗口", LogHelper.LogType.Trace);
                    
                    foreach (var window in wpsWindows)
                    {
                        LogHelper.WriteLogToFile($"WPS窗口: 标题='{window.Title}', 类名='{window.ClassName}', 可见={window.IsVisible}, 最小化={window.IsMinimized}", LogHelper.LogType.Trace);
                    }
                    
                    // 只要当前wpp进程没有可见窗口，就允许Kill
                    return wpsWindows.Any(w => w.IsVisible && !w.IsMinimized);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"检查WPP窗口失败: {ex.ToString()}", LogHelper.LogType.Error);
            }
            return false; // 出错时，默认允许Kill
        }

        /// <summary>
        /// WPS窗口信息结构
        /// </summary>
        private class WpsWindowInfo
        {
            public IntPtr Handle { get; set; }
            public string Title { get; set; }
            public string ClassName { get; set; }
            public bool IsVisible { get; set; }
            public bool IsMinimized { get; set; }
            public bool IsMaximized { get; set; }
            public ForegroundWindowInfo.RECT Rect { get; set; }
            public uint ProcessId { get; set; }
        }

        /// <summary>
        /// 获取指定进程的所有WPS窗口
        /// </summary>
        private List<WpsWindowInfo> GetWpsWindowsByProcess(int processId)
        {
            var wpsWindows = new List<WpsWindowInfo>();
            
            try
            {
                    EnumWindows((hWnd, lParam) =>
                    {
                        try
                        {
                        if (!IsWindow(hWnd)) return true;
                        
                            uint windowProcessId;
                            GetWindowThreadProcessId(hWnd, out windowProcessId);
                        
                        if ((int)windowProcessId == processId)
                        {
                            var windowInfo = GetWindowInfo(hWnd);
                            if (IsWpsWindow(windowInfo))
                            {
                                wpsWindows.Add(windowInfo);
                                LogHelper.WriteLogToFile($"发现WPS窗口: {windowInfo.Title} ({windowInfo.ClassName})", LogHelper.LogType.Trace);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"枚举窗口时出错: {ex.ToString()}", LogHelper.LogType.Error);
                    }
                    return true;
                }, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"获取WPS窗口失败: {ex.ToString()}", LogHelper.LogType.Error);
            }
            
            return wpsWindows;
        }

        /// <summary>
        /// 获取窗口详细信息
        /// </summary>
        private WpsWindowInfo GetWindowInfo(IntPtr hWnd)
        {
            var windowInfo = new WpsWindowInfo
            {
                Handle = hWnd,
                IsVisible = IsWindowVisible(hWnd),
                IsMinimized = IsIconic(hWnd),
                IsMaximized = IsZoomed(hWnd)
            };

            // 获取窗口标题
            var windowTitle = new System.Text.StringBuilder(256);
            GetWindowText(hWnd, windowTitle, 256);
            windowInfo.Title = windowTitle.ToString().Trim();

            // 获取窗口类名
            var className = new System.Text.StringBuilder(256);
            GetClassName(hWnd, className, 256);
            windowInfo.ClassName = className.ToString().Trim();

            // 获取窗口位置
            GetWindowRect(hWnd, out ForegroundWindowInfo.RECT rect);
            windowInfo.Rect = rect;

            // 获取进程ID
            uint processId;
            GetWindowThreadProcessId(hWnd, out processId);
            windowInfo.ProcessId = processId;

            return windowInfo;
        }

        /// <summary>
        /// 判断是否为WPS窗口
        /// </summary>
        private bool IsWpsWindow(WpsWindowInfo windowInfo)
        {
            if (string.IsNullOrEmpty(windowInfo.Title) && string.IsNullOrEmpty(windowInfo.ClassName))
                return false;

            // 检查窗口标题
            var title = windowInfo.Title.ToLower();
            var className = windowInfo.ClassName.ToLower();

            // WPS相关关键词（扩展版）
            var wpsKeywords = new[]
            {
                "wps", "演示文稿", "presentation", "powerpoint", "ppt", "pptx",
                "kingsoft", "金山", "office", "幻灯片", "slide", "presentation",
                "wpp", "wps演示", "wps presentation", "wps office", "kingsoft office"
            };

            // 检查标题是否包含WPS相关关键词
            bool hasWpsTitle = wpsKeywords.Any(keyword => title.Contains(keyword));
            
            // 检查类名是否包含WPS相关关键词
            bool hasWpsClass = wpsKeywords.Any(keyword => className.Contains(keyword));

            // 检查是否为WPS特有的窗口类名
            bool isWpsClass = className.Contains("wps") || 
                             className.Contains("kingsoft") || 
                             className.Contains("presentation") ||
                             className.Contains("powerpoint") ||
                             className.Contains("wpp") ||
                             className.Contains("office");

            // 检查窗口是否有有效尺寸（排除0尺寸窗口）
            bool hasValidSize = (windowInfo.Rect.Right - windowInfo.Rect.Left) > 0 && 
                               (windowInfo.Rect.Bottom - windowInfo.Rect.Top) > 0;

            // 检查窗口是否可见且不是最小化状态
            bool isActiveWindow = windowInfo.IsVisible && !windowInfo.IsMinimized;

            // 检查是否为前台窗口
            bool isForegroundWindow = windowInfo.Handle == GetForegroundWindow();

            // 综合判断是否为WPS窗口
            bool isWpsWindow = (hasWpsTitle || hasWpsClass || isWpsClass) && hasValidSize;

            // 如果是前台窗口且包含相关关键词，更可能是WPS窗口
            if (isForegroundWindow && (hasWpsTitle || hasWpsClass))
            {
                isWpsWindow = true;
            }

            if (isWpsWindow)
            {
                var windowType = isForegroundWindow ? "前台" : (isActiveWindow ? "活跃" : "后台");
                LogHelper.WriteLogToFile($"确认WPS窗口: 标题='{windowInfo.Title}', 类名='{windowInfo.ClassName}', 类型={windowType}, 尺寸={windowInfo.Rect.Right - windowInfo.Rect.Left}x{windowInfo.Rect.Bottom - windowInfo.Rect.Top}", LogHelper.LogType.Trace);
            }

            return isWpsWindow;
        }

        /// <summary>
        /// 获取前台WPS窗口
        /// </summary>
        private WpsWindowInfo GetForegroundWpsWindow()
        {
            try
            {
                var foregroundHwnd = GetForegroundWindow();
                if (foregroundHwnd != IntPtr.Zero && IsWindow(foregroundHwnd))
                {
                    var windowInfo = GetWindowInfo(foregroundHwnd);
                    if (IsWpsWindow(windowInfo))
                    {
                        LogHelper.WriteLogToFile($"前台WPS窗口: {windowInfo.Title}", LogHelper.LogType.Trace);
                        return windowInfo;
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"获取前台WPS窗口失败: {ex.ToString()}", LogHelper.LogType.Error);
            }
            return null;
        }

        /// <summary>
        /// 检查前台WPS窗口是否仍然存在（更精确的检测）
        /// </summary>
        private bool IsForegroundWpsWindowStillActive()
        {
            try
            {
                var currentTime = DateTime.Now;
                var currentForegroundWindow = GetForegroundWpsWindow();
                
                // 检查窗口状态是否发生变化
                bool windowStateChanged = false;
                if (lastForegroundWpsWindow != null && currentForegroundWindow != null)
                {
                    // 检查窗口是否发生了变化
                    if (lastForegroundWpsWindow.Handle != currentForegroundWindow.Handle ||
                        lastForegroundWpsWindow.Title != currentForegroundWindow.Title)
                    {
                        windowStateChanged = true;
                        LogHelper.WriteLogToFile($"前台WPS窗口发生变化: {lastForegroundWpsWindow.Title} -> {currentForegroundWindow.Title}", LogHelper.LogType.Trace);
                    }
                }
                else if (lastForegroundWpsWindow == null && currentForegroundWindow != null)
                {
                    // 从无窗口变为有窗口
                    windowStateChanged = true;
                    LogHelper.WriteLogToFile($"检测到新的前台WPS窗口: {currentForegroundWindow.Title}", LogHelper.LogType.Trace);
                }
                else if (lastForegroundWpsWindow != null && currentForegroundWindow == null)
                {
                    // 从有窗口变为无窗口
                    windowStateChanged = true;
                    LogHelper.WriteLogToFile($"前台WPS窗口已消失: {lastForegroundWpsWindow.Title}", LogHelper.LogType.Trace);
                }

                // 更新记录
                lastForegroundWpsWindow = currentForegroundWindow;
                lastWindowCheckTime = currentTime;

                if (currentForegroundWindow != null)
                {
                    // 验证窗口仍然有效
                    if (IsWindow(currentForegroundWindow.Handle) && IsWindowVisible(currentForegroundWindow.Handle))
                    {
                        return true;
                    }
                }

                // 方法2：检查所有WPS进程的活跃窗口
                var wpsProcesses = GetWpsProcesses();
                foreach (var process in wpsProcesses)
                {
                    var windows = GetWpsWindowsByProcess(process.Id);
                    if (windows.Any(w => w.IsVisible && !w.IsMinimized && w.Handle == GetForegroundWindow()))
                    {
                        return true;
                    }
                }

                // 方法3：检查顶级WPS窗口
                var topLevelWindows = GetTopLevelWpsWindows();
                if (topLevelWindows.Any(w => w.IsVisible && !w.IsMinimized))
                {
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"检查前台WPS窗口状态失败: {ex.ToString()}", LogHelper.LogType.Error);
                return false;
            }
        }

        /// <summary>
        /// 获取顶级WPS窗口（包括前台窗口和最近的活跃窗口）
        /// </summary>
        private List<WpsWindowInfo> GetTopLevelWpsWindows()
        {
            var topLevelWindows = new List<WpsWindowInfo>();
            
            try
            {
                // 获取前台窗口
                var foregroundWindow = GetForegroundWpsWindow();
                if (foregroundWindow != null)
                {
                    topLevelWindows.Add(foregroundWindow);
                }

                // 获取所有可见的WPS窗口，按层级排序
                var allWpsWindows = new List<WpsWindowInfo>();
                var wpsProcesses = GetWpsProcesses();
                
                foreach (var process in wpsProcesses)
                {
                    var windows = GetWpsWindowsByProcess(process.Id);
                    allWpsWindows.AddRange(windows.Where(w => w.IsVisible && !w.IsMinimized));
                }

                // 按窗口位置排序，优先选择屏幕中央的窗口
                var screenCenter = new System.Drawing.Point(
                    System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width / 2,
                    System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height / 2
                );

                var sortedWindows = allWpsWindows
                    .Where(w => !topLevelWindows.Any(t => t.Handle == w.Handle)) // 排除已添加的前台窗口
                    .OrderBy(w => Math.Abs((w.Rect.Left + w.Rect.Right) / 2 - screenCenter.X) + 
                                   Math.Abs((w.Rect.Top + w.Rect.Bottom) / 2 - screenCenter.Y))
                    .Take(3); // 取最近的3个窗口

                topLevelWindows.AddRange(sortedWindows);
                
                LogHelper.WriteLogToFile($"找到{topLevelWindows.Count}个顶级WPS窗口", LogHelper.LogType.Trace);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"获取顶级WPS窗口失败: {ex.ToString()}", LogHelper.LogType.Error);
            }
            
            return topLevelWindows;
        }

        /// <summary>
        /// 检查是否有活跃的WPS窗口（包括前台窗口）
        /// </summary>
        private bool HasActiveWpsWindows()
        {
            return HasActiveWpsWindowsWithRetry(3); // 重试3次
        }

        /// <summary>
        /// 带重试机制的WPS窗口检测
        /// </summary>
        private bool HasActiveWpsWindowsWithRetry(int maxRetries)
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    LogHelper.WriteLogToFile($"第{attempt}次尝试检测WPS窗口", LogHelper.LogType.Trace);
                    
                    // 首先检查前台窗口
                    var foregroundWpsWindow = GetForegroundWpsWindow();
                    if (foregroundWpsWindow != null)
                    {
                        LogHelper.WriteLogToFile($"第{attempt}次尝试检测到前台WPS窗口", LogHelper.LogType.Trace);
                        return true;
                    }

                    // 检查顶级WPS窗口
                    var topLevelWindows = GetTopLevelWpsWindows();
                    if (topLevelWindows.Any())
                    {
                        LogHelper.WriteLogToFile($"第{attempt}次尝试检测到{topLevelWindows.Count}个顶级WPS窗口", LogHelper.LogType.Trace);
                        return true;
                    }

                    // 然后检查所有WPS进程的窗口
                    var wpsProcesses = GetWpsProcesses();
                    foreach (var process in wpsProcesses)
                    {
                        var windows = GetWpsWindowsByProcess(process.Id);
                        if (windows.Any(w => w.IsVisible && !w.IsMinimized))
                        {
                            LogHelper.WriteLogToFile($"第{attempt}次尝试检测到进程{process.Id}的活跃WPS窗口", LogHelper.LogType.Trace);
                            return true;
                        }
                    }

                    // 如果还有重试机会，等待一小段时间再重试
                    if (attempt < maxRetries)
                    {
                        Thread.Sleep(100); // 等待100ms
                }
            }
            catch (Exception ex)
            {
                    LogHelper.WriteLogToFile($"第{attempt}次尝试检查活跃WPS窗口失败: {ex.ToString()}", LogHelper.LogType.Error);
                    
                    // 如果还有重试机会，等待一小段时间再重试
                    if (attempt < maxRetries)
                    {
                        Thread.Sleep(100); // 等待100ms
                    }
                }
            }
            
            LogHelper.WriteLogToFile($"经过{maxRetries}次尝试，未检测到活跃的WPS窗口", LogHelper.LogType.Trace);
            return false;
        }

        /// <summary>
        /// 获取所有WPS相关进程
        /// </summary>
        private List<Process> GetWpsProcesses()
        {
            var wpsProcesses = new List<Process>();
            try
            {
                var allProcesses = Process.GetProcesses();
                foreach (var process in allProcesses)
                {
                    try
                    {
                        var pname = process.ProcessName.ToLower();
                        if ((pname.Contains("wps") || pname.Contains("wpp") || pname.Contains("presentation"))
                            // 排除PowerPoint官方进程
                            && !pname.Contains("powerpnt"))
                        {
                            wpsProcesses.Add(process);
                            LogHelper.WriteLogToFile($"发现WPS进程: {process.ProcessName} (PID: {process.Id})", LogHelper.LogType.Trace);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"检查进程{process.ProcessName}失败: {ex.ToString()}", LogHelper.LogType.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"获取WPS进程失败: {ex.ToString()}", LogHelper.LogType.Error);
            }
            return wpsProcesses;
        }

        /// <summary>
        /// 调试方法：输出所有窗口信息
        /// </summary>
        private void DebugAllWindows()
        {
            try
            {
                LogHelper.WriteLogToFile("开始调试所有窗口信息", LogHelper.LogType.Trace);
                var windowCount = 0;
                
                EnumWindows((hWnd, lParam) =>
                {
                    try
                    {
                        if (!IsWindow(hWnd)) return true;
                        
                        var windowInfo = GetWindowInfo(hWnd);
                        if (!string.IsNullOrEmpty(windowInfo.Title) || !string.IsNullOrEmpty(windowInfo.ClassName))
                        {
                            windowCount++;
                            LogHelper.WriteLogToFile($"窗口{windowCount}: 标题='{windowInfo.Title}', 类名='{windowInfo.ClassName}', 进程ID={windowInfo.ProcessId}, 可见={windowInfo.IsVisible}, 最小化={windowInfo.IsMinimized}", LogHelper.LogType.Trace);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"调试窗口时出错: {ex.ToString()}", LogHelper.LogType.Error);
                    }
                    return true;
                }, IntPtr.Zero);
                
                LogHelper.WriteLogToFile($"调试完成，共发现{windowCount}个有效窗口", LogHelper.LogType.Trace);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"调试窗口失败: {ex.ToString()}", LogHelper.LogType.Error);
            }
        }

        private bool CheckForWpsWindowsByEnumeration()
        {
            try
            {
                var wpsWindowCount = 0;
                var currentProcessId = wppProcess?.Id ?? 0;

                EnumWindows((IntPtr hWnd, IntPtr lParam) =>
                {
                    try
                    {
                        uint windowProcessId;
                        GetWindowThreadProcessId(hWnd, out windowProcessId);
                        
                        // 检查是否是WPP进程的窗口
                        if (windowProcessId == currentProcessId)
                        {
                            var windowTitle = new System.Text.StringBuilder(256);
                            GetWindowText(hWnd, windowTitle, 256);
                            var title = windowTitle.ToString().Trim();
                            
                            // 检查窗口标题是否包含WPS相关标识
                            if (!string.IsNullOrEmpty(title) && 
                                (title.Contains("WPS") || title.Contains("演示文稿") || title.Contains(".ppt") || title.Contains(".pptx")))
                            {
                                wpsWindowCount++;
                                LogHelper.WriteLogToFile($"发现WPS窗口: {title}", LogHelper.LogType.Trace);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"枚举窗口时出错: {ex.ToString()}", LogHelper.LogType.Error);
                    }
                    
                    return true; // 继续枚举
                }, IntPtr.Zero);

                if (wpsWindowCount > 1)
                {
                    LogHelper.WriteLogToFile($"检测到{wpsWindowCount}个WPS窗口，可能存在多个文档", LogHelper.LogType.Trace);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"通过枚举检查WPS窗口失败: {ex.ToString()}", LogHelper.LogType.Error);
                return false;
            }
        }

        private void StopWppProcessCheckTimer()
        {
            if (wppProcessCheckTimer != null)
            {
                wppProcessCheckTimer.Stop();
                wppProcessCheckTimer.Dispose();
                wppProcessCheckTimer = null;
            }
            
            wppProcess = null;
            hasWppProcessID = false;
            wppProcessRecordTime = DateTime.MinValue;
            wppProcessCheckCount = 0;
            lastForegroundWpsWindow = null;
            lastWindowCheckTime = DateTime.MinValue;
            LogHelper.WriteLogToFile("停止 WPP 进程检测定时器", LogHelper.LogType.Trace);
        }

        /// <summary>
        /// 计算文件路径的哈希值，用于生成唯一的文件夹标识符
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>文件路径的哈希值字符串</returns>
        private string GetFileHash(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath))
                    return "unknown";
                
                // 使用文件路径的哈希值作为唯一标识符
                using (var md5 = System.Security.Cryptography.MD5.Create())
                {
                    byte[] hashBytes = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(filePath));
                    return BitConverter.ToString(hashBytes).Replace("-", "").Substring(0, 8);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"计算文件哈希值失败: {ex.ToString()}", LogHelper.LogType.Error);
                return "error";
            }
        }

        // 新增：同步保存当前页面墨迹的方法
        private void SaveCurrentSlideInkStrokes()
        {
            try
            {
                if (pptApplication != null && pptApplication.SlideShowWindows != null && pptApplication.SlideShowWindows.Count >= 1)
                {
                    var slideShowWindow = pptApplication.SlideShowWindows[1];
                    if (slideShowWindow != null && slideShowWindow.View != null)
                    {
                        var ms = new MemoryStream();
                        inkCanvas.Strokes.Save(ms);
                        ms.Position = 0;
                        memoryStreams[slideShowWindow.View.CurrentShowPosition] = ms;
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"保存当前页面墨迹失败: {ex.ToString()}", LogHelper.LogType.Error);
            }
        }
    }
}
