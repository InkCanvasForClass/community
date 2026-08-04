using Ink_Canvas.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;

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

            // 设置窗口置顶
            Topmost = true;

            // 暂停主窗口置顶维护（快抽窗口自己管理）
            (Application.Current.MainWindow as MainWindow)?.PauseTopmostMaintenance();

            RefreshTheme();
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
        /// 调用所选外部点名器，成功返回 true
        /// </summary>
        internal static bool TryLaunchExternalCaller()
        {
            try
            {
                var protocols = ExternalCallerLauncher.GetProtocolsByType(MainWindow.Settings.RandSettings.ExternalCallerType);

                if (!ExternalCallerLauncher.TryLaunch(protocols, out Exception lastException))
                {
                    throw lastException ?? new InvalidOperationException("external caller protocols are unavailable");
                }

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(Properties.RandomStrings.Random_RollCall_ExternalCallerFailedFormat, ex.Message), Properties.RandomStrings.Random_Error, MessageBoxButton.OK, MessageBoxImage.Error);
                LogHelper.WriteLogToFile($"快抽外部点名调用失败: {ex.Message}", LogHelper.LogType.Error);
                return false;
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

        /// <summary>
        /// 刷新主题，当主窗口主题切换时调用
        /// </summary>
        public void RefreshTheme()
        {
            try
            {
                ThemeHelper.ApplyTheme(this, MainWindow.Settings);
                InvalidateVisual();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"刷新快抽窗口主题出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 注册到中央置顶管理器，确保窗口立即获得置顶状态
        /// </summary>
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            WindowTopmostManager.RegisterWindow(this);
        }

        protected override void OnClosed(EventArgs e)
        {
            // 恢复主窗口置顶维护
            (Application.Current.MainWindow as MainWindow)?.ResumeTopmostMaintenance();
            base.OnClosed(e);
        }
    }
}
