using Ink_Canvas.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Ink_Canvas
{
    /// <summary>
    /// 快抽窗口
    /// </summary>
    public partial class QuickDrawWindow : Window
    {
        private Random random = new Random();
        private int autoCloseWaitTime = 2500; // 自动关闭等待时间（毫秒）
        private List<string> nameList = new List<string>(); // 名单列表 

        public QuickDrawWindow()
        {
            InitializeComponent();
            this.Focusable = false;
            this.ShowInTaskbar = false;
            InitializeSettings();
            LoadNamesFromFile();
            StartQuickDraw();
        }

        private void InitializeSettings()
        {
            try
            {
                if (MainWindow.Settings?.RandSettings != null)
                {
                    autoCloseWaitTime = (int)MainWindow.Settings.RandSettings.RandWindowOnceCloseLatency * 1000;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"初始化快抽窗口设置失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void LoadNamesFromFile()
        {
            try
            {
                string namesFilePath = App.RootPath + "Names.txt";
                if (File.Exists(namesFilePath))
                {
                    string content = File.ReadAllText(namesFilePath);
                    nameList = content.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                                   .Select(name => name.Trim())
                                   .Where(name => !string.IsNullOrEmpty(name))
                                   .ToList();
                }
                else
                {
                    nameList.Clear();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"加载名单文件失败: {ex.Message}", LogHelper.LogType.Error);
                nameList.Clear();
            }
        }

        private void StartQuickDraw()
        {
            try
            {
                // 延迟100ms后开始抽选动画
                new System.Threading.Thread(() =>
                {
                    System.Threading.Thread.Sleep(100);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        StartQuickDrawAnimation();
                    });
                }).Start();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"开始快抽失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 快抽动画
        /// </summary>
        private void StartQuickDrawAnimation()
        {
            const int animationTimes = 100; // 动画次数
            const int sleepTime = 5; // 每次动画间隔（毫秒）

            new System.Threading.Thread(() =>
            {
                if (nameList.Count > 0)
                {
                    // 有名单时，从名单中抽选
                    StartNameDrawAnimation(animationTimes, sleepTime);
                }
                else
                {
                    // 没有名单时，从1-60数字中抽选
                    StartNumberDrawAnimation(animationTimes, sleepTime);
                }
            }).Start();
        }

        /// <summary>
        /// 名单抽选动画
        /// </summary>
        private void StartNameDrawAnimation(int animationTimes, int sleepTime)
        {
            List<string> usedNames = new List<string>();

            for (int i = 0; i < animationTimes; i++)
            {
                // 随机选择一个名字进行动画显示，避免立即重复
                string randomName;
                do
                {
                    randomName = nameList[random.Next(0, nameList.Count)];
                } while (usedNames.Count > 0 && usedNames[usedNames.Count - 1] == randomName);

                usedNames.Add(randomName);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    MainResultDisplay.Text = randomName;
                });

                System.Threading.Thread.Sleep(sleepTime);
            }

            // 动画结束，显示最终结果
            Application.Current.Dispatcher.Invoke(() =>
            {
                // 使用降重抽选方法选择最终名字
                var selectedNames = NewStyleRollCallWindow.SelectNamesWithML(nameList, 1, random);
                string finalName = selectedNames.Count > 0 ? selectedNames[0] : nameList[random.Next(0, nameList.Count)];
                MainResultDisplay.Text = finalName;

                // 更新历史记录
                NewStyleRollCallWindow.UpdateRollCallHistory(new List<string> { finalName });
            });

            // 显示结果后，等待一段时间让用户看到结果，然后关闭窗口
            new System.Threading.Thread(() =>
            {
                System.Threading.Thread.Sleep(autoCloseWaitTime);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Close();
                });
            }).Start();
        }

        /// <summary>
        /// 数字抽选动画
        /// </summary>
        private void StartNumberDrawAnimation(int animationTimes, int sleepTime)
        {
            List<int> usedNumbers = new List<int>();

            for (int i = 0; i < animationTimes; i++)
            {
                // 随机选择一个数字进行动画显示，避免立即重复
                int randomNumber;
                do
                {
                    randomNumber = random.Next(1, 61); // 1-60
                } while (usedNumbers.Count > 0 && usedNumbers[usedNumbers.Count - 1] == randomNumber);

                usedNumbers.Add(randomNumber);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    MainResultDisplay.Text = randomNumber.ToString();
                });

                System.Threading.Thread.Sleep(sleepTime);
            }

            // 动画结束，显示最终结果
            Application.Current.Dispatcher.Invoke(() =>
            {
                // 使用降重抽选方法选择最终数字
                var numberList = Enumerable.Range(1, 60).Select(n => n.ToString()).ToList();
                var selectedNumbers = NewStyleRollCallWindow.SelectNamesWithML(numberList, 1, random);
                string finalNumber = selectedNumbers.Count > 0 ? selectedNumbers[0] : random.Next(1, 61).ToString();
                MainResultDisplay.Text = finalNumber;

                // 更新历史记录
                NewStyleRollCallWindow.UpdateRollCallHistory(new List<string> { finalNumber });
            });

            // 显示结果后，等待一段时间让用户看到结果，然后关闭窗口
            new System.Threading.Thread(() =>
            {
                System.Threading.Thread.Sleep(autoCloseWaitTime);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Close();
                });
            }).Start();
        }



        private void WindowDragMove(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }


        #region Win32 API 声明和置顶管理
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOPMOST = 0x00000008;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_NOOWNERZORDER = 0x0200;

        /// <summary>
        /// 应用快抽窗口置顶
        /// </summary>
        private void ApplyQuickDrawWindowTopmost()
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;

                // 设置WPF的Topmost属性
                Topmost = true;

                // 使用Win32 API强制置顶
                // 1. 设置窗口样式为置顶
                int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOPMOST);

                // 2. 使用SetWindowPos确保窗口在最顶层
                SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW | SWP_NOOWNERZORDER);

                LogHelper.WriteLogToFile("快抽窗口已应用置顶", LogHelper.LogType.Trace);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"应用快抽窗口置顶失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 窗口加载事件处理，确保置顶
        /// </summary>
        private void QuickDrawWindow_Loaded(object sender, RoutedEventArgs e)
        {
            MainWindow mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow != null)
            {
                mainWindow.PauseTopmostMaintenance();
            }

            // 使用延迟确保窗口完全加载后再应用置顶
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ApplyQuickDrawWindowTopmost();
            }), DispatcherPriority.Loaded);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            MainWindow mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow != null)
            {
                mainWindow.ResumeTopmostMaintenance();
            }
        }
        #endregion
    }
}
