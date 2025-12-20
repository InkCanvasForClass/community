using Ink_Canvas.Helpers;
using iNKORE.UI.WPF.Modern.Controls;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;

namespace Ink_Canvas
{
    /// <summary>
    /// Interaction logic for RandWindow.xaml
    /// </summary>
    public partial class RandWindow : Window
    {
        public RandWindow(Settings settings)
        {
            InitializeComponent();
            AnimationsHelper.ShowWithSlideFromBottomAndFade(this, 0.25);
            BorderBtnHelp.Visibility = settings.RandSettings.DisplayRandWindowNamesInputBtn == false ? Visibility.Collapsed : Visibility.Visible;
            RandMaxPeopleOneTime = settings.RandSettings.RandWindowOnceMaxStudents;
            RandDoneAutoCloseWaitTime = (int)settings.RandSettings.RandWindowOnceCloseLatency * 1000;

            // 加载背景
            LoadBackground(settings);

            // 应用主题
            ApplyTheme(settings);

            // 设置窗口为置顶
            Topmost = true;

            // 添加窗口关闭事件处理
            Closed += RandWindow_Closed;

            // 添加窗口显示事件处理，确保置顶
            Loaded += RandWindow_Loaded;
        }

        private void LoadBackground(Settings settings)
        {
            try
            {
                int selectedIndex = settings.RandSettings.SelectedBackgroundIndex;
                if (selectedIndex <= 0)
                {
                    // 默认背景（无背景）
                    BackgroundImage.ImageSource = null;
                    MainBorder.Background = new SolidColorBrush(Color.FromRgb(240, 243, 249));
                }
                else if (selectedIndex <= settings.RandSettings.CustomPickNameBackgrounds.Count)
                {
                    // 自定义背景
                    var customBackground = settings.RandSettings.CustomPickNameBackgrounds[selectedIndex - 1];
                    if (File.Exists(customBackground.FilePath))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(customBackground.FilePath);
                        bitmap.EndInit();
                        BackgroundImage.ImageSource = bitmap;
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"加载点名背景出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void ApplyTheme(Settings settings)
        {
            try
            {
                if (settings.Appearance.Theme == 0) // 浅色主题
                {
                    iNKORE.UI.WPF.Modern.ThemeManager.SetRequestedTheme(this, iNKORE.UI.WPF.Modern.ElementTheme.Light);
                }
                else if (settings.Appearance.Theme == 1) // 深色主题
                {
                    iNKORE.UI.WPF.Modern.ThemeManager.SetRequestedTheme(this, iNKORE.UI.WPF.Modern.ElementTheme.Dark);
                }
                else // 跟随系统主题
                {
                    bool isSystemLight = IsSystemThemeLight();
                    if (isSystemLight)
                    {
                        iNKORE.UI.WPF.Modern.ThemeManager.SetRequestedTheme(this, iNKORE.UI.WPF.Modern.ElementTheme.Light);
                    }
                    else
                    {
                        iNKORE.UI.WPF.Modern.ThemeManager.SetRequestedTheme(this, iNKORE.UI.WPF.Modern.ElementTheme.Dark);
                    }
                }

                // 根据主题设置窗口背景
                if (settings.RandSettings.SelectedBackgroundIndex <= 0)
                {
                    // 没有自定义背景时，使用主题背景色
                    var backgroundBrush = Application.Current.FindResource("RandWindowBackground") as SolidColorBrush;
                    if (backgroundBrush != null)
                    {
                        MainBorder.Background = backgroundBrush;
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"应用点名窗口主题出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private bool IsSystemThemeLight()
        {
            var light = false;
            try
            {
                var registryKey = Microsoft.Win32.Registry.CurrentUser;
                var themeKey =
                    registryKey.OpenSubKey("software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize");
                var keyValue = 0;
                if (themeKey != null) keyValue = (int)themeKey.GetValue("SystemUsesLightTheme");
                if (keyValue == 1) light = true;
            }
            catch { }

            return light;
        }

        public RandWindow(Settings settings, bool IsAutoClose)
        {
            InitializeComponent();
            isAutoClose = IsAutoClose;
            PeopleControlPane.Opacity = 0.4;
            PeopleControlPane.IsHitTestVisible = false;
            BorderBtnHelp.Visibility = settings.RandSettings.DisplayRandWindowNamesInputBtn == false ? Visibility.Collapsed : Visibility.Visible;
            RandMaxPeopleOneTime = settings.RandSettings.RandWindowOnceMaxStudents;
            RandDoneAutoCloseWaitTime = (int)settings.RandSettings.RandWindowOnceCloseLatency * 1000;

            // 加载背景
            LoadBackground(settings);

            // 应用主题
            ApplyTheme(settings);

            // 设置窗口为置顶
            Topmost = true;

            // 添加窗口关闭事件处理
            Closed += RandWindow_Closed;

            // 添加窗口显示事件处理，确保置顶
            Loaded += RandWindow_Loaded;

            new Thread(() =>
            {
                Thread.Sleep(100);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    BorderBtnRand_MouseUp(BorderBtnRand, null);
                });
            }).Start();
        }

        public static int randSeed = 0;
        public bool isAutoClose;
        public bool isNotRepeatName = false;

        public int TotalCount = 1;
        public int PeopleCount = 60;
        public List<string> Names = new List<string>();

        private void BorderBtnAdd_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (RandMaxPeopleOneTime == -1 && TotalCount >= PeopleCount) return;
            if (RandMaxPeopleOneTime != -1 && TotalCount >= RandMaxPeopleOneTime) return;
            TotalCount++;
            LabelNumberCount.Text = TotalCount.ToString();
            SymbolIconStart.Symbol = Symbol.People;
            BorderBtnAdd.Opacity = 1;
            BorderBtnMinus.Opacity = 1;
        }

        private void BorderBtnMinus_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (TotalCount < 2) return;
            TotalCount--;
            LabelNumberCount.Text = TotalCount.ToString();
            if (TotalCount == 1)
            {
                SymbolIconStart.Symbol = Symbol.Contact;
            }
        }

        public int RandWaitingTimes = 100;
        public int RandWaitingThreadSleepTime = 5;
        public int RandMaxPeopleOneTime = 10;
        public int RandDoneAutoCloseWaitTime = 2500;

        private void BorderBtnRand_MouseUp(object sender, MouseButtonEventArgs e)
        {
            Random random = new Random();// randSeed + DateTime.Now.Millisecond / 10 % 10);
            string outputString = "";
            List<string> outputs = new List<string>();

            LabelOutput2.Visibility = Visibility.Collapsed;
            LabelOutput3.Visibility = Visibility.Collapsed;

            new Thread(() =>
            {
                var animationPool = new List<int>();
                for (int num = 1; num <= PeopleCount; num++)
                {
                    animationPool.Add(num);
                }
                int lastDisplayedIndex = -1;

                for (int i = 0; i < RandWaitingTimes; i++)
                {
                    if (animationPool.Count == 0)
                    {
                        animationPool.Clear();
                        for (int num = 1; num <= PeopleCount; num++)
                        {
                            animationPool.Add(num);
                        }
                    }

                    int randomIndex = random.Next(0, animationPool.Count);
                    int selectedNumber = animationPool[randomIndex];
                    
                    int lastIndex = animationPool.Count - 1;
                    if (randomIndex != lastIndex)
                    {
                        animationPool[randomIndex] = animationPool[lastIndex];
                    }
                    animationPool.RemoveAt(lastIndex);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (Names.Count != 0)
                        {
                            LabelOutput.Content = Names[selectedNumber - 1];
                        }
                        else
                        {
                            LabelOutput.Content = selectedNumber.ToString();
                        }
                    });

                    Thread.Sleep(RandWaitingThreadSleepTime);
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    var candidatePool = new List<int>();
                    for (int num = 1; num <= PeopleCount; num++)
                    {
                        candidatePool.Add(num);
                    }

                    for (int i = 0; i < TotalCount && candidatePool.Count > 0; i++)
                    {
                        int randomIndex = random.Next(0, candidatePool.Count);
                        int selectedNumber = candidatePool[randomIndex];
                        
                        int lastIndex = candidatePool.Count - 1;
                        if (randomIndex != lastIndex)
                        {
                            candidatePool[randomIndex] = candidatePool[lastIndex];
                        }
                        candidatePool.RemoveAt(lastIndex);

                        if (Names.Count != 0)
                        {
                            outputs.Add(Names[selectedNumber - 1]);
                            outputString += Names[selectedNumber - 1] + Environment.NewLine;
                        }
                        else
                        {
                            outputs.Add(selectedNumber.ToString());
                            outputString += selectedNumber + Environment.NewLine;
                        }
                    }
                    if (TotalCount <= 5)
                    {
                        LabelOutput.Content = outputString.Trim();
                    }
                    else if (TotalCount <= 10)
                    {
                        LabelOutput2.Visibility = Visibility.Visible;
                        outputString = "";
                        for (int i = 0; i < (outputs.Count + 1) / 2; i++)
                        {
                            outputString += outputs[i] + Environment.NewLine;
                        }
                        LabelOutput.Content = outputString.Trim();
                        outputString = "";
                        for (int i = (outputs.Count + 1) / 2; i < outputs.Count; i++)
                        {
                            outputString += outputs[i] + Environment.NewLine;
                        }
                        LabelOutput2.Content = outputString.Trim();
                    }
                    else
                    {
                        LabelOutput2.Visibility = Visibility.Visible;
                        LabelOutput3.Visibility = Visibility.Visible;
                        outputString = "";
                        for (int i = 0; i < (outputs.Count + 1) / 3; i++)
                        {
                            outputString += outputs[i] + Environment.NewLine;
                        }
                        LabelOutput.Content = outputString.Trim();
                        outputString = "";
                        for (int i = (outputs.Count + 1) / 3; i < (outputs.Count + 1) * 2 / 3; i++)
                        {
                            outputString += outputs[i] + Environment.NewLine;
                        }
                        LabelOutput2.Content = outputString.Trim();
                        outputString = "";
                        for (int i = (outputs.Count + 1) * 2 / 3; i < outputs.Count; i++)
                        {
                            outputString += outputs[i] + Environment.NewLine;
                        }
                        LabelOutput3.Content = outputString.Trim();
                    }

                    if (isAutoClose)
                    {
                        new Thread(() =>
                        {
                            Thread.Sleep(RandDoneAutoCloseWaitTime);
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                PeopleControlPane.Opacity = 1;
                                PeopleControlPane.IsHitTestVisible = true;
                                Close();
                            });
                        }).Start();
                    }
                });
            }).Start();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Names = new List<string>();
            if (File.Exists(App.RootPath + "Names.txt"))
            {
                string[] fileNames = File.ReadAllLines(App.RootPath + "Names.txt");
                string[] replaces = new string[0];

                if (File.Exists(App.RootPath + "Replace.txt"))
                {
                    replaces = File.ReadAllLines(App.RootPath + "Replace.txt");
                }

                //Fix emtpy lines
                foreach (string str in fileNames)
                {
                    string s = str;
                    //Make replacement
                    foreach (string replace in replaces)
                    {
                        if (s == Strings.Left(replace, replace.IndexOf("-->")))
                        {
                            s = Strings.Mid(replace, replace.IndexOf("-->") + 4);
                        }
                    }

                    if (s != "") Names.Add(s);
                }

                PeopleCount = Names.Count();
                TextBlockPeopleCount.Text = PeopleCount.ToString();
                if (PeopleCount == 0)
                {
                    PeopleCount = 60;
                    TextBlockPeopleCount.Text = "点击此处以导入名单";
                }
            }
        }

        private void BorderBtnHelp_MouseUp(object sender, MouseButtonEventArgs e)
        {
            new NamesInputWindow().ShowDialog();
            Window_Loaded(this, null);
        }

        private void BtnClose_MouseUp(object sender, MouseButtonEventArgs e)
        {
            Close();
        }

        // 将 isIslandCallerFirstClick 设为静态字段，实现全局记录
        private static bool isIslandCallerFirstClick = true;

        private void BorderBtnExternalCaller_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (isIslandCallerFirstClick)
            {
                MessageBox.Show(
                    "首次使用外部点名功能，请确保已安装相应的点名软件。\n" +
                    "如未安装，请前往官网下载并安装后再使用。如果已安装请再次点击此按钮。",
                    "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                isIslandCallerFirstClick = false;
                return;
            }

            try
            {
                string protocol = "";
                switch (ComboBoxCallerType.SelectedIndex)
                {
                    case 0: // ClassIsland点名
                        protocol = "classisland://plugins/IslandCaller/Simple/1";
                        break;
                    case 1: // SecRandom点名
                        protocol = "secrandom://direct_extraction";
                        break;
                    case 2: // NamePicker点名
                        protocol = "namepicker://";
                        break;
                    default:
                        protocol = "classisland://plugins/IslandCaller/Simple/1";
                        break;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = protocol,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("无法调用外部点名：" + ex.Message);
            }
        }

        /// <summary>
        /// 窗口加载事件处理
        /// </summary>
        private void RandWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 使用延迟确保窗口完全加载后再应用置顶
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    // 强制激活窗口
                    Activate();
                    Focus();

                    // 设置置顶
                    Topmost = true;

                    // 使用Win32 API强制置顶
                    var hwnd = new WindowInteropHelper(this).Handle;
                    if (hwnd != IntPtr.Zero)
                    {
                        const int WS_EX_TOPMOST = 0x00000008;
                        const int GWL_EXSTYLE = -20;
                        const int SWP_NOMOVE = 0x0002;
                        const int SWP_NOSIZE = 0x0001;
                        const int SWP_SHOWWINDOW = 0x0040;
                        const int SWP_NOOWNERZORDER = 0x0200;
                        var HWND_TOPMOST = new IntPtr(-1);

                        // 设置窗口样式为置顶
                        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOPMOST);

                        // 强制置顶
                        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0,
                            SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW | SWP_NOOWNERZORDER);
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"RandWindow置顶失败: {ex.Message}", LogHelper.LogType.Error);
                }
            }), DispatcherPriority.Loaded);
        }

        /// <summary>
        /// 窗口关闭事件处理
        /// </summary>
        private void RandWindow_Closed(object sender, EventArgs e)
        {
            // 窗口关闭时的清理工作
            // 这里可以添加必要的清理代码
        }

        /// <summary>
        /// 刷新主题，当主窗口主题切换时调用
        /// </summary>
        public void RefreshTheme()
        {
            try
            {
                // 重新应用主题
                ApplyTheme(MainWindow.Settings);

                // 强制刷新UI
                InvalidateVisual();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"刷新点名窗口主题出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        #region Win32 API 声明
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        #endregion
    }
}
