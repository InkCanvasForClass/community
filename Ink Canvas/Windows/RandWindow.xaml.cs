using Ink_Canvas.Helpers;
using iNKORE.UI.WPF.Modern.Common.IconKeys;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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

            // 注册到中央置顶管理器，确保立即生效
            SourceInitialized += (s, e) => WindowTopmostManager.RegisterWindow(this);
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
            ThemeHelper.ApplyTheme(this, settings, theme =>
            {
                if (settings.RandSettings.SelectedBackgroundIndex <= 0)
                {
                    if (Application.Current.FindResource("RandWindowBackground") is SolidColorBrush backgroundBrush)
                    {
                        MainBorder.Background = backgroundBrush;
                    }
                }
            });
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

            // 注册到中央置顶管理器，确保立即生效
            SourceInitialized += (s, e) => WindowTopmostManager.RegisterWindow(this);

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
            FontIconStart.Icon = SegoeFluentIcons.People;
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
                FontIconStart.Icon = SegoeFluentIcons.Contact;
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
                    TextBlockPeopleCount.Text = Properties.RandomStrings.Random_Rand_ClickToImport;
                }
            }
        }

        private async void BorderBtnHelp_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (SecurityManager.IsPasswordRequiredForModifyOrClearNameList(MainWindow.Settings))
            {
                bool ok = await SecurityManager.PromptAndVerifyPasswordOrTotpAsync(
                    MainWindow.Settings,
                    this,
                    Properties.RandomStrings.Random_RollCall_NameListVerifyTitle,
                    Properties.RandomStrings.Random_RollCall_NameListVerifyMessage);
                if (!ok) return;
            }

            var namesInputWindow = new NamesInputWindow();
            namesInputWindow.Owner = this;
            namesInputWindow.ShowDialog();
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
                    Properties.RandomStrings.Random_RollCall_ExternalCallerFirstUse,
                    Properties.RandomStrings.Random_Hint, MessageBoxButton.OK, MessageBoxImage.Information);
                isIslandCallerFirstClick = false;
                return;
            }

            try
            {
                string[] protocols;
                switch (ComboBoxCallerType.SelectedIndex)
                {
                    case 0: // ClassIsland点名
                        protocols = ExternalCallerLauncher.GetProtocolsByType(0);
                        break;
                    case 1: // SecRandom点名
                        protocols = ExternalCallerLauncher.GetProtocolsByType(1);
                        break;
                    case 2: // NamePicker点名
                        protocols = ExternalCallerLauncher.GetProtocolsByType(2);
                        break;
                    default:
                        protocols = ExternalCallerLauncher.GetProtocolsByType(0);
                        break;
                }

                if (!ExternalCallerLauncher.TryLaunch(protocols, out Exception lastException))
                {
                    throw lastException ?? new InvalidOperationException("external caller protocols are unavailable");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(Properties.RandomStrings.Random_RollCall_ExternalCallerFailedFormat, ex.Message));
            }
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

    }
}
