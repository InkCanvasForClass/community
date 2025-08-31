using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Ink_Canvas.Helpers;
using Microsoft.VisualBasic;
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
            BorderBtnHelp.Visibility = !settings.RandSettings.DisplayRandWindowNamesInputBtn ? Visibility.Collapsed : Visibility.Visible;
            RandMaxPeopleOneTime = settings.RandSettings.RandWindowOnceMaxStudents;
            RandDoneAutoCloseWaitTime = (int)settings.RandSettings.RandWindowOnceCloseLatency * 1000;

            // 加载背景
            LoadBackground(settings);
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

        public RandWindow(Settings settings, bool IsAutoClose)
        {
            InitializeComponent();
            isAutoClose = IsAutoClose;
            PeopleControlPane.Opacity = 0.4;
            PeopleControlPane.IsHitTestVisible = false;
            BorderBtnHelp.Visibility = !settings.RandSettings.DisplayRandWindowNamesInputBtn ? Visibility.Collapsed : Visibility.Visible;
            RandMaxPeopleOneTime = settings.RandSettings.RandWindowOnceMaxStudents;
            RandDoneAutoCloseWaitTime = (int)settings.RandSettings.RandWindowOnceCloseLatency * 1000;

            // 加载背景
            LoadBackground(settings);

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
            FontIconStart.Glyph = "&#xE779;";
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
                FontIconStart.Glyph = "&#xE779;";
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
            List<int> rands = new List<int>();

            LabelOutput2.Visibility = Visibility.Collapsed;
            LabelOutput3.Visibility = Visibility.Collapsed;

            new Thread(() =>
            {
                for (int i = 0; i < RandWaitingTimes; i++)
                {
                    int rand = random.Next(1, PeopleCount + 1);
                    while (rands.Contains(rand))
                    {
                        rand = random.Next(1, PeopleCount + 1);
                    }
                    rands.Add(rand);
                    if (rands.Count >= PeopleCount) rands = new List<int>();
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (Names.Count != 0)
                        {
                            LabelOutput.Content = Names[rand - 1];
                        }
                        else
                        {
                            LabelOutput.Content = rand.ToString();
                        }
                    });

                    Thread.Sleep(RandWaitingThreadSleepTime);
                }

                rands = new List<int>();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    for (int i = 0; i < TotalCount; i++)
                    {
                        int rand = random.Next(1, PeopleCount + 1);
                        while (rands.Contains(rand))
                        {
                            rand = random.Next(1, PeopleCount + 1);
                        }
                        rands.Add(rand);
                        if (rands.Count >= PeopleCount) rands = new List<int>();

                        if (Names.Count != 0)
                        {
                            outputs.Add(Names[rand - 1]);
                            outputString += Names[rand - 1] + Environment.NewLine;
                        }
                        else
                        {
                            outputs.Add(rand.ToString());
                            outputString += rand + Environment.NewLine;
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
    }
}
