using Ink_Canvas.Helpers;
using Microsoft.Office.Interop.PowerPoint;
using System;
using System.Diagnostics;
using System.IO;
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

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        public static Microsoft.Office.Interop.PowerPoint.Application pptApplication = null;
        public static Presentation presentation = null;
        public static Slides slides = null;
        public static Slide slide = null;
        public static int slidescount = 0;

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
                    if (hr == 0x800401E3 || hr == 0x80004005 || hr == 0x800706B5)
                    {
                        // 可选：LogHelper.WriteLogToFile($"忽略已知COM异常: {hr:X}", LogHelper.LogType.Trace);
                        Application.Current.Dispatcher.Invoke(() => { BtnPPTSlideShow.Visibility = Visibility.Collapsed; });
                        timerCheckPPT.Start();
                        return;
                    }
                }
                LogHelper.WriteLogToFile($"检查PPT状态失败: {ex.ToString()}", LogHelper.LogType.Error);
                //StackPanelPPTControls.Visibility = Visibility.Collapsed;
                Application.Current.Dispatcher.Invoke(() => { BtnPPTSlideShow.Visibility = Visibility.Collapsed; });
                timerCheckPPT.Start();
            }
        }

        private void PptApplication_PresentationOpen(Presentation Pres) {
            // 跳转到上次播放页
            if (Settings.PowerPointSettings.IsNotifyPreviousPage)
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
                
                if (Settings.Automation.IsAutoFoldInPPTSlideShow && !isFloatingBarFolded)
                    await FoldFloatingBar(new object());
                else if (isFloatingBarFolded) await UnFoldFloatingBar(new object());

                isStopInkReplay = true;

                LogHelper.WriteLogToFile("PowerPoint Application Slide Show Begin", LogHelper.LogType.Event);

                await Application.Current.Dispatcher.InvokeAsync(() => {

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

                    if (!isFloatingBarFolded) {
                        new Thread(new ThreadStart(() => {
                            Thread.Sleep(100);
                            Application.Current.Dispatcher.Invoke(() => {
                                ViewboxFloatingBarMarginAnimation(60);
                            });
                        })).Start();
                    }
                });
            }
            catch (Exception ex) {
                LogHelper.WriteLogToFile(ex.ToString(), LogHelper.LogType.Error);
            }
        }

        private bool isEnteredSlideShowEndEvent = false; //防止重复调用本函数导致墨迹保存失效

        private async void PptApplication_SlideShowEnd(Presentation Pres) {
            try {
                if (isFloatingBarFolded) await UnFoldFloatingBar(new object());

                // 记录 WPP 进程 ID，用于后续检测未关闭的进程
                if (pptApplication != null && pptApplication.Path.Contains("Kingsoft\\WPS Office\\"))
                {
                    uint processId;
                    GetWindowThreadProcessId((IntPtr)pptApplication.HWND, out processId);
                    wppProcess = Process.GetProcessById((int)processId);
                    hasWppProcessID = true;
                    wppProcessRecordTime = DateTime.Now;
                    wppProcessCheckCount = 0;
                    LogHelper.WriteLogToFile($"记录 WPP 进程 ID: {processId}", LogHelper.LogType.Trace);
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
            if (wppProcessCheckTimer != null)
            {
                wppProcessCheckTimer.Stop();
                wppProcessCheckTimer.Dispose();
            }

            wppProcessCheckTimer = new System.Timers.Timer(2000); // 2秒检查一次
            wppProcessCheckTimer.Elapsed += WppProcessCheckTimer_Elapsed;
            wppProcessCheckTimer.Start();
            LogHelper.WriteLogToFile("启动 WPP 进程检测定时器", LogHelper.LogType.Trace);
        }

        private void WppProcessCheckTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
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
                }
                else
                {
                    // 检查是否有其他WPS窗口打开
                    bool hasOtherWpsWindows = CheckForOtherWpsWindows();
                    
                    if (hasOtherWpsWindows)
                    {
                        LogHelper.WriteLogToFile("检测到其他WPS窗口打开，停止强制关闭进程", LogHelper.LogType.Trace);
                        StopWppProcessCheckTimer();
                        return;
                    }

                    // 计算从记录进程开始的时间
                    var timeSinceRecord = DateTime.Now - wppProcessRecordTime;
                    
                    // 更保守的关闭策略：只有在超过0.5秒且检查次数超过2次时才强制关闭
                    if (timeSinceRecord.TotalSeconds > 0.5 && wppProcessCheckCount >= 2)
                    {
                        // 新增：检查所有WPS文档是否已保存
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
                        LogHelper.WriteLogToFile($"检测到长时间未关闭的 WPP 进程（已运行{timeSinceRecord.TotalSeconds:F1}秒，检查{wppProcessCheckCount}次），开始强制关闭", LogHelper.LogType.Event);
                        wppProcess.Kill();
                        LogHelper.WriteLogToFile("强制关闭 WPP 进程成功", LogHelper.LogType.Event);
                        StopWppProcessCheckTimer();
                    }
                    else
                    {
                        LogHelper.WriteLogToFile($"WPP 进程仍在运行，已检查{wppProcessCheckCount}次，运行时间{timeSinceRecord.TotalSeconds:F1}秒", LogHelper.LogType.Trace);
                    }
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
                    bool hasVisibleWppWindow = false;
                    EnumWindows((hWnd, lParam) =>
                    {
                        try
                        {
                            uint windowProcessId;
                            GetWindowThreadProcessId(hWnd, out windowProcessId);
                            if ((int)windowProcessId == wppProcess.Id)
                            {
                                if (IsWindowVisible(hWnd))
                                {
                                    var windowTitle = new System.Text.StringBuilder(256);
                                    GetWindowText(hWnd, windowTitle, 256);
                                    var title = windowTitle.ToString().Trim();
                                    if (!string.IsNullOrEmpty(title))
                                    {
                                        hasVisibleWppWindow = true;
                                        return false; // 找到一个就停止枚举
                                    }
                                }
                            }
                        }
                        catch { }
                        return true;
                    }, IntPtr.Zero);

                    // 只要当前wpp进程没有可见窗口，就允许Kill
                    return hasVisibleWppWindow;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"检查WPP窗口失败: {ex.ToString()}", LogHelper.LogType.Error);
            }
            return false; // 出错时，默认允许Kill
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
    }
}
