using Ink_Canvas.Helpers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;

namespace Ink_Canvas
{
    /// <summary>
    /// 最近点名记录数据模型
    /// </summary>
    public class RecentRollCallData
    {
        public List<string> RecentNames { get; set; } = new List<string>();
        public DateTime LastUpdate { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// 点名历史记录数据模型
    /// </summary>
    public class RollCallHistoryData
    {
        public List<string> History { get; set; } = new List<string>();
        public Dictionary<string, int> NameFrequency { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, double> NameProbabilities { get; set; } = new Dictionary<string, double>();
        public DateTime LastUpdate { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// 新点名UI风格的窗口
    /// </summary>
    public partial class NewStyleRollCallWindow : Window
    {
        public NewStyleRollCallWindow()
        {
            InitializeComponent();
            AnimationsHelper.ShowWithSlideFromBottomAndFade(this, 0.25);

            InitializeUI();
            ApplyTheme(MainWindow.Settings);

            // 初始化点名相关变量
            InitializeRollCallData();
        }

        public NewStyleRollCallWindow(bool isSingleDraw)
        {
            InitializeComponent();
            AnimationsHelper.ShowWithSlideFromBottomAndFade(this, 0.25);

            // 设置单次抽模式
            isSingleDrawMode = isSingleDraw;

            InitializeUI();
            ApplyTheme(MainWindow.Settings);

            // 初始化点名相关变量
            InitializeRollCallData();

            if (isSingleDrawMode)
            {
                if (ControlOptionsGrid != null)
                {
                    ControlOptionsGrid.Opacity = 0.4;
                    ControlOptionsGrid.IsHitTestVisible = false;
                }
                if (StartRollCallBtn != null)
                {
                    StartRollCallBtn.Opacity = 0.4;
                    StartRollCallBtn.IsEnabled = false;
                }
                if (ResetBtn != null)
                {
                    ResetBtn.Opacity = 0.4;
                    ResetBtn.IsEnabled = false;
                }
            }

            // 单次抽模式：自动开始抽选
            if (isSingleDrawMode)
            {
                // 延迟100ms后自动开始抽选
                new System.Threading.Thread(() =>
                {
                    System.Threading.Thread.Sleep(100);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        StartSingleDraw();
                    });
                }).Start();
            }
        }

        public NewStyleRollCallWindow(Settings settings, bool isSingleDraw = false)
        {
            InitializeComponent();
            AnimationsHelper.ShowWithSlideFromBottomAndFade(this, 0.25);

            // 保存设置
            this.settings = settings;

            // 设置单次抽模式
            isSingleDrawMode = isSingleDraw;

            InitializeUI();
            ApplyTheme(settings);

            // 初始化设置
            InitializeSettings();

            // 初始化点名相关变量
            InitializeRollCallData();

            // 单次抽模式：禁用控制面板，阻止用户点击按钮
            if (isSingleDrawMode)
            {
                if (ControlOptionsGrid != null)
                {
                    ControlOptionsGrid.Opacity = 0.4;
                    ControlOptionsGrid.IsHitTestVisible = false;
                }
                // 禁用开始点名和重置按钮
                if (StartRollCallBtn != null)
                {
                    StartRollCallBtn.Opacity = 0.4;
                    StartRollCallBtn.IsEnabled = false;
                }
                if (ResetBtn != null)
                {
                    ResetBtn.Opacity = 0.4;
                    ResetBtn.IsEnabled = false;
                }
            }

            // 单次抽模式：自动开始抽选
            if (isSingleDrawMode)
            {
                // 延迟100ms后自动开始抽选
                new System.Threading.Thread(() =>
                {
                    System.Threading.Thread.Sleep(100);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        StartSingleDraw();
                    });
                }).Start();
            }
        }

        #region 私有字段
        private List<string> nameList = new List<string>();
        private List<string> recentNames = new List<string>();
        private int currentCount = 1;
        private bool isRollCalling = false;
        private Timer rollCallTimer;
        private Random random = new Random();
        private DateTime lastActivityTime = DateTime.Now;

        // 机器学习相关
        private static RollCallHistoryData historyData = null;
        private static readonly object historyLock = new object();
        private static int maxRecentHistory = 20;
        private static double avoidanceWeight = 0.8;
        private const double FREQUENCY_WEIGHT = 0.2;

        // 概率相关
        private const double DEFAULT_PROBABILITY = 1.0;
        private const double BASE_PROBABILITY_DECAY_FACTOR = 0.5;
        private const double MIN_PROBABILITY = 0.01;
        private const double PROBABILITY_RECOVERY_RATE = 0.2;
        private const double FREQUENCY_BOOST_FACTOR = 2.0;

        // 单次抽相关
        private bool isSingleDrawMode = false;
        private Random singleDrawRandom = new Random();

        // 设置相关
        private Settings settings;
        private int autoCloseWaitTime = 2500; // 自动关闭等待时间（毫秒）

        // 点名模式
        private string selectedRollCallMode = "Random"; // 默认随机点名

        // 外部点名相关
        private string selectedExternalCaller = "ClassIsland";

        // 开始点名按钮的数据
        private string originalStartBtnIconData = "M5 7C5 8.06087 5.42143 9.07828 6.17157 9.82843C6.92172 10.5786 7.93913 11 9 11C10.0609 11 11.0783 10.5786 11.8284 9.82843C12.5786 9.07828 13 8.06087 13 7C13 5.93913 12.5786 4.92172 11.8284 4.17157C11.0783 3.42143 10.0609 3 9 3C7.93913 3 6.92172 3.42143 6.17157 4.17157C5.42143 4.92172 5 5.93913 5 7Z M3 21V19C3 17.9391 3.42143 16.9217 4.17157 16.1716C4.92172 15.4214 5.93913 15 7 15H11C12.0609 15 13.0783 15.4214 13.8284 16.1716C14.5786 16.9217 15 17.9391 15 19V21 M16 3.13C16.8604 3.35031 17.623 3.85071 18.1676 4.55232C18.7122 5.25392 19.0078 6.11683 19.0078 7.005C19.0078 7.89318 18.7122 8.75608 18.1676 9.45769C17.623 10.1593 16.8604 10.6597 16 10.88 M21 21V19C20.9949 18.1172 20.6979 17.2608 20.1553 16.5644C19.6126 15.868 18.8548 15.3707 18 15.15";
        private string originalStartBtnText = "开始点名";

        // 外部点名按钮的数据
        private string externalCallerBtnIconData = "M9 15L15 9 M11 6L11.463 5.464C12.4008 4.52633 13.6727 3.9996 14.9989 3.99969C16.325 3.99979 17.5968 4.52669 18.5345 5.4645C19.4722 6.40231 19.9989 7.67419 19.9988 9.00035C19.9987 10.3265 19.4718 11.5983 18.534 12.536L18 13 M13.0001 18L12.6031 18.534C11.6544 19.4722 10.3739 19.9984 9.03964 19.9984C7.70535 19.9984 6.42489 19.4722 5.47614 18.534C5.0085 18.0716 4.63724 17.521 4.38385 16.9141C4.13047 16.3073 4 15.6561 4 14.9985C4 14.3408 4.13047 13.6897 4.38385 13.0829C4.63724 12.476 5.0085 11.9254 5.47614 11.463L6.00014 11";
        private string externalCallerBtnText = "外部点名";

        // JSON文件路径
        private static readonly string ConfigsFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs");
        private static readonly string RollCallHistoryJsonPath = System.IO.Path.Combine(ConfigsFolder, "RollCallHistory.json");
        #endregion

        #region 初始化方法
        private void InitializeUI()
        {
            UpdateCountDisplay();
            LoadNamesFromFile();
            UpdateListCountDisplay();
            LoadRollCallHistory();
            LoadSettings();
            InitializeSingleDrawMode();
            InitializeModeSelection();
            InitializeExternalCallerSelection();
        }

        private void InitializeSingleDrawMode()
        {
            if (isSingleDrawMode)
            {
                // 单次抽模式：使用60个数字
                MainResultDisplay.Text = "准备抽选...";
                StatusDisplay.Text = "单次抽模式 - 60个数字";
                CountDisplay.Text = "1";
                CountMinusBtn.IsEnabled = true;
                CountPlusBtn.IsEnabled = true;
            }
            else
            {
                // 普通点名模式
                MainResultDisplay.Text = "点击开始点名";
                StatusDisplay.Text = "准备就绪";
                CountDisplay.Text = "1";
                CountMinusBtn.IsEnabled = true;
                CountPlusBtn.IsEnabled = true;
            }
        }

        private void InitializeModeSelection()
        {
            try
            {
                SetModeSelection("Random");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"初始化点名模式选择控件时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void InitializeExternalCallerSelection()
        {
            try
            {
                // 初始化下拉框选择
                if (ExternalCallerTypeComboBox != null)
                {
                    ExternalCallerTypeComboBox.SelectedIndex = 0; // 默认选择ClassIsland
                    selectedExternalCaller = "ClassIsland";
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"初始化外部点名选择控件时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void InitializeSettings()
        {
            try
            {
                if (settings?.RandSettings != null)
                {
                    // 使用老点名UI的设置
                    autoCloseWaitTime = (int)settings.RandSettings.RandWindowOnceCloseLatency * 1000;
                    maxRecentHistory = settings.RandSettings.MLAvoidanceHistoryCount;
                    avoidanceWeight = settings.RandSettings.MLAvoidanceWeight;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"初始化点名设置时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void LoadSettings()
        {
            try
            {
                if (MainWindow.Settings?.RandSettings != null)
                {
                    maxRecentHistory = MainWindow.Settings.RandSettings.MLAvoidanceHistoryCount;
                    avoidanceWeight = MainWindow.Settings.RandSettings.MLAvoidanceWeight;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"加载点名设置时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void InitializeRollCallData()
        {
            // 初始化点名定时器
            rollCallTimer = new Timer(100);
            rollCallTimer.Elapsed += RollCallTimer_Elapsed;
        }

        private void ApplyTheme()
        {
            try
            {
                // 应用主题设置
                if (MainWindow.Settings != null)
                {
                    ApplyTheme(MainWindow.Settings);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"应用新点名UI窗口主题出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void ApplyTheme(Settings settings)
        {
            try
            {
                if (settings.Appearance.Theme == 0) // 浅色主题
                {
                    iNKORE.UI.WPF.Modern.ThemeManager.SetRequestedTheme(this, iNKORE.UI.WPF.Modern.ElementTheme.Light);
                    ApplyThemeResources("Light");
                }
                else if (settings.Appearance.Theme == 1) // 深色主题
                {
                    iNKORE.UI.WPF.Modern.ThemeManager.SetRequestedTheme(this, iNKORE.UI.WPF.Modern.ElementTheme.Dark);
                    ApplyThemeResources("Dark");
                    SetDarkThemeBorder();
                }
                else // 跟随系统主题
                {
                    bool isSystemLight = IsSystemThemeLight();
                    if (isSystemLight)
                    {
                        iNKORE.UI.WPF.Modern.ThemeManager.SetRequestedTheme(this, iNKORE.UI.WPF.Modern.ElementTheme.Light);
                        ApplyThemeResources("Light");
                    }
                    else
                    {
                        iNKORE.UI.WPF.Modern.ThemeManager.SetRequestedTheme(this, iNKORE.UI.WPF.Modern.ElementTheme.Dark);
                        ApplyThemeResources("Dark");
                        SetDarkThemeBorder();
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"应用新点名UI窗口主题出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 应用主题资源
        /// </summary>
        /// <param name="theme">主题类型（Light或Dark）</param>
        private void ApplyThemeResources(string theme)
        {
            try
            {
                // 更新窗口资源
                var resources = this.Resources;

                if (theme == "Light")
                {
                    // 应用浅色主题资源
                    resources["NewRollCallWindowBackground"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                    resources["NewRollCallWindowBorderBrush"] = new SolidColorBrush(Color.FromRgb(228, 228, 231));
                    resources["NewRollCallWindowTitleForeground"] = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                    resources["NewRollCallWindowDigitForeground"] = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                    resources["NewRollCallWindowButtonBackground"] = new SolidColorBrush(Color.FromRgb(244, 244, 245));
                    resources["NewRollCallWindowButtonForeground"] = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                    resources["NewRollCallWindowPrimaryButtonBackground"] = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // #4CAF50
                    resources["NewRollCallWindowPrimaryButtonForeground"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                    resources["NewRollCallWindowSecondaryTextForeground"] = new SolidColorBrush(Color.FromRgb(113, 113, 122));
                }
                else
                {
                    // 应用深色主题资源 - 与新计时器窗口统一
                    resources["NewRollCallWindowBackground"] = new SolidColorBrush(Color.FromRgb(31, 31, 31)); // #1f1f1f
                    resources["NewRollCallWindowBorderBrush"] = new SolidColorBrush(Color.FromRgb(224, 224, 224)); // #E0E0E0
                    resources["NewRollCallWindowTitleForeground"] = new SolidColorBrush(Colors.White);
                    resources["NewRollCallWindowDigitForeground"] = new SolidColorBrush(Colors.White);
                    resources["NewRollCallWindowButtonBackground"] = new SolidColorBrush(Color.FromRgb(42, 42, 42)); // #2a2a2a
                    resources["NewRollCallWindowButtonForeground"] = new SolidColorBrush(Colors.White);
                    resources["NewRollCallWindowPrimaryButtonBackground"] = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // #4CAF50
                    resources["NewRollCallWindowPrimaryButtonForeground"] = new SolidColorBrush(Colors.White);
                    resources["NewRollCallWindowSecondaryTextForeground"] = new SolidColorBrush(Color.FromRgb(156, 163, 175)); // #9ca3af
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"应用新点名UI窗口主题资源出错: {ex.Message}", LogHelper.LogType.Error);
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
                light = true;
            }
            return light;
        }
        #endregion

        #region 点名模式逻辑
        /// <summary>
        /// 根据选择的模式进行点名
        /// </summary>
        private List<string> SelectNamesByMode(List<string> availableNames, int count)
        {
            switch (selectedRollCallMode)
            {
                case "Random":
                    return SelectNamesWithML(availableNames, count, random);
                case "Sequential":
                    return SelectNamesSequentially(availableNames, count);
                case "Group":
                    return SelectNamesInGroups(availableNames, count);
                default:
                    return SelectNamesWithML(availableNames, count, random);
            }
        }

        /// <summary>
        /// 顺序点名：按名单顺序选择
        /// </summary>
        private List<string> SelectNamesSequentially(List<string> availableNames, int count)
        {
            if (availableNames.Count == 0) return new List<string>();

            var selectedNames = new List<string>();
            int startIndex = 0;

            // 从历史记录中找到上次选择的位置
            if (historyData.History != null && historyData.History.Count > 0)
            {
                string lastSelected = historyData.History.LastOrDefault();
                if (!string.IsNullOrEmpty(lastSelected))
                {
                    int lastIndex = availableNames.IndexOf(lastSelected);
                    if (lastIndex >= 0)
                    {
                        startIndex = (lastIndex + 1) % availableNames.Count;
                    }
                }
            }

            for (int i = 0; i < count && i < availableNames.Count; i++)
            {
                int index = (startIndex + i) % availableNames.Count;
                selectedNames.Add(availableNames[index]);
            }

            return selectedNames;
        }

        /// <summary>
        /// 分组点名：将名单分成若干组，每组选择一个人
        /// </summary>
        private List<string> SelectNamesInGroups(List<string> availableNames, int count)
        {
            if (availableNames.Count == 0) return new List<string>();

            var selectedNames = new List<string>();
            int groupSize = Math.Max(1, availableNames.Count / count);

            for (int i = 0; i < count && i * groupSize < availableNames.Count; i++)
            {
                int startIndex = i * groupSize;
                int endIndex = Math.Min(startIndex + groupSize, availableNames.Count);

                // 从当前组中随机选择一个人
                var group = availableNames.GetRange(startIndex, endIndex - startIndex);
                if (group.Count > 0)
                {
                    int randomIndex = random.Next(0, group.Count);
                    selectedNames.Add(group[randomIndex]);
                }
            }

            return selectedNames;
        }
        #endregion

        #region 机器学习避免重复逻辑
        /// <summary>
        /// 使用机器学习算法选择点名人员，避免最近重复
        /// </summary>
        /// <param name="availableNames">可用名单</param>
        /// <param name="count">需要选择的人数</param>
        /// <param name="random">随机数生成器</param>
        /// <returns>选择的人员名单</returns>
        public static List<string> SelectNamesWithML(List<string> availableNames, int count, Random random)
        {
            if (availableNames == null || availableNames.Count == 0)
                return new List<string>();

            // 确保历史数据已初始化
            if (historyData == null)
            {
                LoadRollCallHistory();
            }

            // 检查是否启用机器学习避免重复
            bool enableML = MainWindow.Settings?.RandSettings?.EnableMLAvoidance ?? true;
            if (!enableML)
            {
                // 如果禁用机器学习，使用简单不放回随机选择
                return SelectNamesRandomly(availableNames, count, random);
            }

            var candidatePool = new List<string>(availableNames);
            var selectedNames = new List<string>();
            if (count >= candidatePool.Count)
            {
                return new List<string>(candidatePool);
            }

            for (int i = 0; i < count && candidatePool.Count > 0; i++)
            {
                string selectedName = SelectSingleNameWithMLWithoutReplacement(candidatePool, selectedNames, random);
                if (!string.IsNullOrEmpty(selectedName))
                {
                    selectedNames.Add(selectedName);
                    candidatePool.Remove(selectedName);
                }
                else
                {
                    if (candidatePool.Count > 0)
                    {
                        int randomIndex = random.Next(0, candidatePool.Count);
                        selectedName = candidatePool[randomIndex];
                        selectedNames.Add(selectedName);
                        candidatePool.RemoveAt(randomIndex);
                    }
                }
            }

            return selectedNames;
        }

        /// <summary>
        /// 简单不放回随机选择点名人员
        /// </summary>
        private static List<string> SelectNamesRandomly(List<string> availableNames, int count, Random random)
        {
            if (availableNames == null || availableNames.Count == 0)
                return new List<string>();

            // 如果请求的数量大于或等于可用名单大小，返回所有名单
            if (count >= availableNames.Count)
            {
                return new List<string>(availableNames);
            }

            var candidatePool = new List<string>(availableNames);
            var selectedNames = new List<string>();

            for (int i = 0; i < count && candidatePool.Count > 0; i++)
            {
                int randomIndex = random.Next(0, candidatePool.Count);
                
                selectedNames.Add(candidatePool[randomIndex]);
                
                int lastIndex = candidatePool.Count - 1;
                if (randomIndex != lastIndex)
                {
                    candidatePool[randomIndex] = candidatePool[lastIndex];
                }
                candidatePool.RemoveAt(lastIndex);
            }

            return selectedNames;
        }

        /// <summary>
        /// 使用概率算法选择单个人员
        /// </summary>
        private static string SelectSingleNameWithMLWithoutReplacement(List<string> candidatePool, List<string> alreadySelected, Random random)
        {
            if (candidatePool.Count == 0) return null;
            if (candidatePool.Count == 1) return candidatePool[0];

            // 确保历史数据已初始化
            if (historyData == null)
            {
                LoadRollCallHistory();
            }

            // 初始化概率字典
            if (historyData.NameProbabilities == null)
            {
                historyData.NameProbabilities = new Dictionary<string, double>();
            }

            // 过滤掉已选择的人员
            var validCandidates = candidatePool.Where(name => !alreadySelected.Contains(name)).ToList();
            if (validCandidates.Count == 0) return null;
            if (validCandidates.Count == 1) return validCandidates[0];

            // 检查极差：当极差达到3时，从被抽选次数最少的人中抽选
            if (historyData.NameFrequency != null && historyData.NameFrequency.Count > 0)
            {
                // 获取所有候选人员的被抽选次数
                var candidateFrequencies = new Dictionary<string, int>();
                foreach (string name in validCandidates)
                {
                    int count = historyData.NameFrequency.ContainsKey(name) ? historyData.NameFrequency[name] : 0;
                    candidateFrequencies[name] = count;
                }

                // 计算极差（最大值 - 最小值）
                if (candidateFrequencies.Count > 0)
                {
                    int maxCount = candidateFrequencies.Values.Max();
                    int minCount = candidateFrequencies.Values.Min();
                    int range = maxCount - minCount;

                    // 当极差达到3时，只从被抽选次数最少的人中抽选
                    if (range >= 3)
                    {
                        var leastSelectedNames = candidateFrequencies
                            .Where(kvp => kvp.Value == minCount)
                            .Select(kvp => kvp.Key)
                            .ToList();

                        if (leastSelectedNames.Count > 0)
                        {
                            // 只从被抽选次数最少的人中不放回随机选择
                            int randomIndex = random.Next(0, leastSelectedNames.Count);
                            return leastSelectedNames[randomIndex];
                        }
                    }
                }
            }

            // 获取每个候选人员的概率
            var nameProbabilities = new Dictionary<string, double>();

            foreach (string name in validCandidates)
            {
                // 获取基础概率
                double baseProbability = GetNameProbability(name);

                // 根据最近历史记录调整概率
                double adjustedProbability = AdjustProbabilityByRecentHistory(name, baseProbability);

                double finalProbability = AdjustProbabilityByFrequency(name, adjustedProbability);

                nameProbabilities[name] = finalProbability;
            }

            // 使用概率进行加权随机选择
            return ProbabilityBasedRandomSelection(nameProbabilities, random);
        }


        /// <summary>
        /// 获取人员的概率
        /// </summary>
        private static double GetNameProbability(string name)
        {
            if (historyData == null || historyData.NameProbabilities == null)
                return DEFAULT_PROBABILITY;

            if (historyData.NameProbabilities.ContainsKey(name))
            {
                return historyData.NameProbabilities[name];
            }
            else
            {
                // 新人员，初始化默认概率
                historyData.NameProbabilities[name] = DEFAULT_PROBABILITY;
                return DEFAULT_PROBABILITY;
            }
        }

        /// <summary>
        /// 根据最近历史记录调整概率
        /// </summary>
        private static double AdjustProbabilityByRecentHistory(string name, double baseProbability)
        {
            if (historyData == null || historyData.History == null || historyData.History.Count == 0)
                return baseProbability;

            // 获取最近记录
            var recentHistory = historyData.History.Skip(Math.Max(0, historyData.History.Count - maxRecentHistory)).ToList();
            int recentCount = recentHistory.Count(n => n == name);

            if (recentCount == 0)
                return baseProbability;

            double recentFrequency = (double)recentCount / Math.Min(recentHistory.Count, maxRecentHistory);

            double reductionFactor = 1.0 - (recentFrequency * avoidanceWeight);
            reductionFactor = Math.Max(reductionFactor, MIN_PROBABILITY / DEFAULT_PROBABILITY); // 确保不会降得太低

            return baseProbability * reductionFactor;
        }

        private static double AdjustProbabilityByFrequency(string name, double baseProbability)
        {
            if (historyData == null || historyData.NameFrequency == null || historyData.NameFrequency.Count == 0)
                return baseProbability;

            // 计算总选中次数
            int totalSelections = historyData.NameFrequency.Values.Sum();
            if (totalSelections == 0)
                return baseProbability;

            // 获取该名字的选中次数
            int nameCount = historyData.NameFrequency.ContainsKey(name) ? historyData.NameFrequency[name] : 0;

            // 计算该名字的选中频率
            double nameFrequency = (double)nameCount / totalSelections;

            // 计算平均频率（假设有N个不同的人）
            int uniqueNamesCount = historyData.NameFrequency.Keys.Count;
            if (uniqueNamesCount == 0)
                return baseProbability;

            double averageFrequency = 1.0 / uniqueNamesCount;

            // 如果该名字的频率低于平均频率，则增加概率
            if (nameFrequency < averageFrequency)
            {
                // 计算频率差异比例
                double frequencyRatio = nameFrequency / averageFrequency;

                double frequencyGap = 1.0 - frequencyRatio;
                double boostFactor = FREQUENCY_BOOST_FACTOR * frequencyGap * frequencyGap;

                // 增加概率
                double boostedProbability = baseProbability * (1.0 + boostFactor);

                return Math.Min(boostedProbability, DEFAULT_PROBABILITY * 10.0);
            }
            else if (nameFrequency > averageFrequency)
            {
                double frequencyRatio = nameFrequency / averageFrequency;

                double reductionFactor = 1.0 - (frequencyRatio - 1.0) * 0.3;
                reductionFactor = Math.Max(reductionFactor, MIN_PROBABILITY / DEFAULT_PROBABILITY);

                return baseProbability * reductionFactor;
            }

            return baseProbability;
        }

        /// <summary>
        /// 根据频率统计更新保存的概率
        /// </summary>
        private static void UpdateProbabilitiesByFrequency()
        {
            if (historyData == null || historyData.NameFrequency == null || historyData.NameFrequency.Count == 0)
                return;

            if (historyData.NameProbabilities == null)
                historyData.NameProbabilities = new Dictionary<string, double>();

            // 计算总选中次数
            int totalSelections = historyData.NameFrequency.Values.Sum();
            if (totalSelections == 0)
                return;

            // 计算平均频率
            int uniqueNamesCount = historyData.NameFrequency.Keys.Count;
            if (uniqueNamesCount == 0)
                return;

            double averageFrequency = 1.0 / uniqueNamesCount;

            // 遍历所有在频率统计中的人员
            foreach (var kvp in historyData.NameFrequency)
            {
                string name = kvp.Key;
                int nameCount = kvp.Value;

                // 获取当前保存的概率（如果不存在则使用默认值）
                double currentProbability = historyData.NameProbabilities.ContainsKey(name)
                    ? historyData.NameProbabilities[name]
                    : DEFAULT_PROBABILITY;

                // 计算该名字的选中频率
                double nameFrequency = (double)nameCount / totalSelections;

                // 如果该名字的频率低于平均频率，则增加概率并保存
                if (nameFrequency < averageFrequency)
                {
                    // 计算频率差异比例
                    double frequencyRatio = nameFrequency / averageFrequency;
                    double frequencyGap = 1.0 - frequencyRatio;
                    double boostFactor = FREQUENCY_BOOST_FACTOR * frequencyGap * frequencyGap;

                    // 增加概率
                    double boostedProbability = currentProbability * (1.0 + boostFactor);

                    // 限制最大概率，避免过高
                    boostedProbability = Math.Min(boostedProbability, DEFAULT_PROBABILITY * 10.0);

                    // 保存更新后的概率
                    historyData.NameProbabilities[name] = boostedProbability;
                }
                else if (nameFrequency > averageFrequency)
                {
                    double frequencyRatio = nameFrequency / averageFrequency;

                    double reductionFactor = 1.0 - (frequencyRatio - 1.0) * 0.3;
                    reductionFactor = Math.Max(reductionFactor, MIN_PROBABILITY / DEFAULT_PROBABILITY);

                    double reducedProbability = currentProbability * reductionFactor;
                    historyData.NameProbabilities[name] = reducedProbability;
                }
            }
        }

        /// <summary>
        /// 基于概率的随机选择
        /// </summary>
        private static string ProbabilityBasedRandomSelection(Dictionary<string, double> nameProbabilities, Random random)
        {
            if (nameProbabilities.Count == 0) return null;

            double totalProbability = nameProbabilities.Values.Sum();
            if (totalProbability <= 0) return nameProbabilities.Keys.First();

            double randomValue = random.NextDouble() * totalProbability;
            double currentProbability = 0;

            foreach (var kvp in nameProbabilities)
            {
                currentProbability += kvp.Value;
                if (randomValue <= currentProbability)
                {
                    return kvp.Key;
                }
            }

            return nameProbabilities.Keys.Last();
        }

        /// <summary>
        /// 计算避免最近重复的权重
        /// </summary>
        private static double CalculateRecentAvoidanceWeight(string name)
        {
            if (historyData == null || historyData.History == null || historyData.History.Count == 0)
                return 0.0;

            // 获取最近记录
            var recentHistory = historyData.History.Skip(Math.Max(0, historyData.History.Count - maxRecentHistory)).ToList();
            int recentCount = recentHistory.Count(n => n == name);

            // 计算权重：最近出现次数越多，权重越高（越应该避免）
            return (double)recentCount / Math.Min(recentHistory.Count, maxRecentHistory);
        }

        /// <summary>
        /// 计算频率平衡权重
        /// </summary>
        private static double CalculateFrequencyWeight(string name)
        {
            if (historyData == null || historyData.NameFrequency == null || !historyData.NameFrequency.ContainsKey(name))
                return 0.5; // 如果从未被选中，给予中等权重

            int totalSelections = historyData.NameFrequency.Values.Sum();
            if (totalSelections == 0) return 0.5;

            int nameCount = historyData.NameFrequency[name];
            double frequency = (double)nameCount / totalSelections;

            // 频率越低，权重越高（越应该被选中）
            return 1.0 - frequency;
        }


        /// <summary>
        /// 更新点名历史记录
        /// </summary>
        public static void UpdateRollCallHistory(List<string> selectedNames)
        {
            if (selectedNames == null || selectedNames.Count == 0) return;

            // 确保历史数据已初始化
            if (historyData == null)
            {
                LoadRollCallHistory();
            }

            lock (historyLock)
            {
                // 初始化概率字典
                if (historyData.NameProbabilities == null)
                {
                    historyData.NameProbabilities = new Dictionary<string, double>();
                }

                // 更新历史记录
                if (historyData.History == null)
                    historyData.History = new List<string>();

                historyData.History.AddRange(selectedNames);

                // 保持历史记录不超过100条
                if (historyData.History.Count > 100)
                {
                    historyData.History = historyData.History.Skip(historyData.History.Count - 100).ToList();
                }

                // 更新频率统计
                if (historyData.NameFrequency == null)
                    historyData.NameFrequency = new Dictionary<string, int>();

                // 更新概率：降重机制
                foreach (string name in selectedNames)
                {
                    // 更新频率统计
                    if (historyData.NameFrequency.ContainsKey(name))
                        historyData.NameFrequency[name]++;
                    else
                        historyData.NameFrequency[name] = 1;

                    // 降重：被选中的人员概率降低
                    double currentProbability = GetNameProbability(name);

                    double frequencyBasedDecay = 1.0;
                    if (historyData.NameFrequency != null && historyData.NameFrequency.ContainsKey(name))
                    {
                        int totalSelections = historyData.NameFrequency.Values.Sum();
                        if (totalSelections > 0)
                        {
                            int uniqueNamesCount = historyData.NameFrequency.Keys.Count;
                            if (uniqueNamesCount > 0)
                            {
                                double nameFrequency = (double)historyData.NameFrequency[name] / totalSelections;
                                double averageFrequency = 1.0 / uniqueNamesCount;

                                if (nameFrequency > averageFrequency)
                                {
                                    double frequencyRatio = nameFrequency / averageFrequency;
                                    frequencyBasedDecay = 1.0 - (frequencyRatio - 1.0) * 0.2;
                                }
                            }
                        }
                    }

                    double decayFactor = BASE_PROBABILITY_DECAY_FACTOR * (1.0 + avoidanceWeight) * frequencyBasedDecay;
                    decayFactor = Math.Min(decayFactor, 0.85);

                    double newProbability = currentProbability * decayFactor;
                    newProbability = Math.Max(newProbability, MIN_PROBABILITY); // 确保不低于最小概率
                    historyData.NameProbabilities[name] = newProbability;
                }

                if (historyData.History != null && historyData.History.Count > 0)
                {
                    int historyCount = historyData.History.Count;
                    int skipCount = Math.Max(0, historyCount - maxRecentHistory);
                    var recentHistory = historyData.History.Skip(skipCount).ToList();
                    var recentNames = new HashSet<string>(recentHistory);

                    var allNames = historyData.NameProbabilities.Keys.ToList();
                    foreach (string name in allNames)
                    {
                        if (!recentNames.Contains(name))
                        {
                            double currentProbability = historyData.NameProbabilities[name];
                            if (currentProbability < DEFAULT_PROBABILITY)
                            {
                                double newProbability = Math.Min(
                                    currentProbability + PROBABILITY_RECOVERY_RATE,
                                    DEFAULT_PROBABILITY
                                );
                                historyData.NameProbabilities[name] = newProbability;
                            }
                        }
                    }
                }

                // 根据频率统计更新概率
                UpdateProbabilitiesByFrequency();

                historyData.LastUpdate = DateTime.Now;

                // 保存到文件
                SaveRollCallHistory();
            }
        }

        #endregion

        #region 数据持久化
        /// <summary>
        /// 加载点名历史记录
        /// </summary>
        private static void LoadRollCallHistory()
        {
            try
            {
                if (!Directory.Exists(ConfigsFolder))
                {
                    Directory.CreateDirectory(ConfigsFolder);
                }

                if (!File.Exists(RollCallHistoryJsonPath))
                {
                    historyData = new RollCallHistoryData();
                    return;
                }

                string jsonContent = File.ReadAllText(RollCallHistoryJsonPath);
                var data = JsonConvert.DeserializeObject<RollCallHistoryData>(jsonContent);

                if (data != null)
                {
                    historyData = data;
                    // 确保概率字典已初始化
                    if (historyData.NameProbabilities == null)
                    {
                        historyData.NameProbabilities = new Dictionary<string, double>();
                    }
                }
                else
                {
                    historyData = new RollCallHistoryData();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"加载点名历史记录失败: {ex.Message}", LogHelper.LogType.Error);
                historyData = new RollCallHistoryData();
            }
        }

        /// <summary>
        /// 保存点名历史记录
        /// </summary>
        private static void SaveRollCallHistory()
        {
            try
            {
                if (!Directory.Exists(ConfigsFolder))
                {
                    Directory.CreateDirectory(ConfigsFolder);
                }

                string jsonContent = JsonConvert.SerializeObject(historyData, Formatting.Indented);
                File.WriteAllText(RollCallHistoryJsonPath, jsonContent);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"保存点名历史记录失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }
        #endregion

        #region UI更新方法
        private void UpdateCountDisplay()
        {
            CountDisplay.Text = currentCount.ToString();
        }

        private void UpdateListCountDisplay()
        {
            ListCountDisplay.Text = $"名单人数: {nameList.Count}";
        }

        private void UpdateStatusDisplay(string status)
        {
            StatusDisplay.Text = status;
        }

        private void ShowResults(List<string> results)
        {
            if (results == null || results.Count == 0)
            {
                MainResultDisplay.Text = "无结果";
                MultiResultScrollViewer.Visibility = Visibility.Collapsed;
                return;
            }

            if (results.Count == 1)
            {
                MainResultDisplay.Text = results[0];
                MainResultDisplay.Visibility = Visibility.Visible;
                MultiResultScrollViewer.Visibility = Visibility.Collapsed;
            }
            else
            {
                // 多个结果时，隐藏主显示区域，显示多结果区域
                MainResultDisplay.Text = "";
                MainResultDisplay.Visibility = Visibility.Collapsed;
                MultiResultScrollViewer.Visibility = Visibility.Visible;

                // 显示所有结果（最多20个）
                Result1Display.Text = results.Count > 0 ? results[0] : "";
                Result2Display.Text = results.Count > 1 ? results[1] : "";
                Result3Display.Text = results.Count > 2 ? results[2] : "";
                Result4Display.Text = results.Count > 3 ? results[3] : "";
                Result5Display.Text = results.Count > 4 ? results[4] : "";
                Result6Display.Text = results.Count > 5 ? results[5] : "";
                Result7Display.Text = results.Count > 6 ? results[6] : "";
                Result8Display.Text = results.Count > 7 ? results[7] : "";
                Result9Display.Text = results.Count > 8 ? results[8] : "";
                Result10Display.Text = results.Count > 9 ? results[9] : "";
                Result11Display.Text = results.Count > 10 ? results[10] : "";
                Result12Display.Text = results.Count > 11 ? results[11] : "";
                Result13Display.Text = results.Count > 12 ? results[12] : "";
                Result14Display.Text = results.Count > 13 ? results[13] : "";
                Result15Display.Text = results.Count > 14 ? results[14] : "";
                Result16Display.Text = results.Count > 15 ? results[15] : "";
                Result17Display.Text = results.Count > 16 ? results[16] : "";
                Result18Display.Text = results.Count > 17 ? results[17] : "";
                Result19Display.Text = results.Count > 18 ? results[18] : "";
                Result20Display.Text = results.Count > 19 ? results[19] : "";
            }
        }
        #endregion

        #region 事件处理方法
        private void CountPlus_Click(object sender, RoutedEventArgs e)
        {
            if (isRollCalling) return;

            // 获取老点名UI的设置
            int maxPeopleLimit = settings?.RandSettings?.RandWindowOnceMaxStudents ?? 10;

            if (isSingleDrawMode)
            {
                // 单次抽模式：最多选择60个数字，但受设置限制
                int maxCount = Math.Min(maxPeopleLimit, 60);
                currentCount = Math.Min(currentCount + 1, maxCount);
            }
            else
            {
                // 普通点名模式：根据是否有名单决定上限
                int maxCount;
                if (nameList.Count == 0)
                {
                    // 没有名单时，使用60个数字
                    maxCount = Math.Min(maxPeopleLimit, 60);
                }
                else
                {
                    // 有名单时，使用名单人数
                    maxCount = Math.Min(maxPeopleLimit, nameList.Count);
                }
                currentCount = Math.Min(currentCount + 1, maxCount);
            }

            UpdateCountDisplay();
        }

        private void CountMinus_Click(object sender, RoutedEventArgs e)
        {
            if (isRollCalling) return;
            currentCount = Math.Max(currentCount - 1, 1);
            UpdateCountDisplay();
        }

        private void ImportList_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 打开名单导入窗口，与老点名UI保持一致
                var namesInputWindow = new NamesInputWindow();
                namesInputWindow.ShowDialog();

                // 重新加载名单
                LoadNamesFromFile();
                UpdateListCountDisplay();
                UpdateStatusDisplay($"已导入 {nameList.Count} 个名字");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导入名单失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                LogHelper.WriteLogToFile($"导入名单失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void ViewHistory_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 打开历史记录查看窗口
                var historyWindow = new RollCallHistoryWindow();
                historyWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开历史记录失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                LogHelper.WriteLogToFile($"打开历史记录失败: {ex.Message}", LogHelper.LogType.Error);
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

        private void ClearList_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 清空名单
                nameList.Clear();
                UpdateListCountDisplay();
                
                // 清空点名历史记录
                lock (historyLock)
                {
                    // 重置历史记录数据
                    historyData = new RollCallHistoryData();
                    
                    // 保存到文件
                    SaveRollCallHistory();
                }
                
                UpdateStatusDisplay("名单和历史记录已清空");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"清空名单和历史记录失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                LogHelper.WriteLogToFile($"清空名单和历史记录失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void SetModeSelection(string mode)
        {
            try
            {
                // 存储选择的模式
                selectedRollCallMode = mode;

                // 重置所有按钮状态
                RandomModeText.FontWeight = FontWeights.Normal;
                RandomModeText.Opacity = 0.6;
                RandomModeText.Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102));
                SequentialModeText.FontWeight = FontWeights.Normal;
                SequentialModeText.Opacity = 0.6;
                SequentialModeText.Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102));
                GroupModeText.FontWeight = FontWeights.Normal;
                GroupModeText.Opacity = 0.6;
                GroupModeText.Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102));

                // 重置外部点名模式按钮状态
                ExternalCallerModeText.FontWeight = FontWeights.Normal;
                ExternalCallerModeText.Opacity = 0.6;
                ExternalCallerModeText.Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102));
                ExternalCallerModeIndicator.Visibility = Visibility.Collapsed;
                SegmentedIndicator.Visibility = Visibility.Visible;

                // 设置选中状态和动画
                switch (mode)
                {
                    case "Random":
                        RandomModeText.FontWeight = FontWeights.Bold;
                        RandomModeText.Opacity = 1.0;
                        RandomModeText.Foreground = new SolidColorBrush(Colors.White);
                        SegmentedIndicator.HorizontalAlignment = HorizontalAlignment.Left;
                        SegmentedIndicator.CornerRadius = new CornerRadius(7.5, 0, 0, 7.5);

                        // 添加动画效果
                        var randomAnimation = new System.Windows.Media.Animation.ThicknessAnimation(
                            new Thickness(0, 0, 0, 0),
                            TimeSpan.FromMilliseconds(200));
                        SegmentedIndicator.BeginAnimation(Border.MarginProperty, randomAnimation);

                        // 恢复开始点名按钮的原始图标和文字
                        RestoreStartRollCallButton();
                        UpdateStatusDisplay("已选择点名模式: 随机点名");
                        break;
                    case "Sequential":
                        SequentialModeText.FontWeight = FontWeights.Bold;
                        SequentialModeText.Opacity = 1.0;
                        SequentialModeText.Foreground = new SolidColorBrush(Colors.White);
                        SegmentedIndicator.HorizontalAlignment = HorizontalAlignment.Left;
                        SegmentedIndicator.CornerRadius = new CornerRadius(0, 0, 0, 0);

                        // 添加动画效果 - 移动到中间位置
                        var sequentialAnimation = new System.Windows.Media.Animation.ThicknessAnimation(
                            new Thickness(100, 0, 0, 0),
                            TimeSpan.FromMilliseconds(200));
                        SegmentedIndicator.BeginAnimation(Border.MarginProperty, sequentialAnimation);

                        // 恢复开始点名按钮的原始图标和文字
                        RestoreStartRollCallButton();
                        UpdateStatusDisplay("已选择点名模式: 顺序点名");
                        break;
                    case "Group":
                        GroupModeText.FontWeight = FontWeights.Bold;
                        GroupModeText.Opacity = 1.0;
                        GroupModeText.Foreground = new SolidColorBrush(Colors.White);
                        SegmentedIndicator.HorizontalAlignment = HorizontalAlignment.Left;
                        SegmentedIndicator.CornerRadius = new CornerRadius(0, 7.5, 7.5, 0);

                        // 添加动画效果 - 移动到右侧位置
                        var groupAnimation = new System.Windows.Media.Animation.ThicknessAnimation(
                            new Thickness(200, 0, 0, 0),
                            TimeSpan.FromMilliseconds(200));
                        SegmentedIndicator.BeginAnimation(Border.MarginProperty, groupAnimation);

                        // 恢复开始点名按钮的原始图标和文字
                        RestoreStartRollCallButton();
                        UpdateStatusDisplay("已选择点名模式: 分组点名");
                        break;
                    case "External":
                        // 外部点名模式
                        ExternalCallerModeText.FontWeight = FontWeights.Bold;
                        ExternalCallerModeText.Opacity = 1.0;
                        ExternalCallerModeText.Foreground = new SolidColorBrush(Colors.White);
                        ExternalCallerModeIndicator.Visibility = Visibility.Visible;

                        // 隐藏其他模式的指示器
                        SegmentedIndicator.Visibility = Visibility.Collapsed;

                        // 切换到外部点名按钮的图标和文字
                        UpdateStartRollCallButtonForExternal();
                        UpdateStatusDisplay($"已选择点名模式: 外部点名 ({selectedExternalCaller})");
                        break;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"设置点名模式选择时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 更新开始点名按钮为外部点名样式
        /// </summary>
        private void UpdateStartRollCallButtonForExternal()
        {
            try
            {
                // 更新图标
                if (StartRollCallBtnIcon != null)
                {
                    StartRollCallBtnIcon.Data = Geometry.Parse(externalCallerBtnIconData);
                    // 外部点名使用按钮前景色而不是主按钮前景色
                    StartRollCallBtnIcon.Stroke = (Brush)FindResource("NewRollCallWindowButtonForeground");
                }

                // 更新文字
                if (StartRollCallBtnText != null)
                {
                    StartRollCallBtnText.Text = externalCallerBtnText;
                    StartRollCallBtnText.Foreground = (Brush)FindResource("NewRollCallWindowButtonForeground");
                }

                // 更新按钮背景色为普通按钮背景
                StartRollCallBtn.Background = (Brush)FindResource("NewRollCallWindowButtonBackground");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"更新开始点名按钮为外部点名样式时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 恢复开始点名按钮的原始样式
        /// </summary>
        private void RestoreStartRollCallButton()
        {
            try
            {
                // 恢复图标
                if (StartRollCallBtnIcon != null)
                {
                    StartRollCallBtnIcon.Data = Geometry.Parse(originalStartBtnIconData);
                    StartRollCallBtnIcon.Stroke = (Brush)FindResource("NewRollCallWindowPrimaryButtonForeground");
                }

                // 恢复文字
                if (StartRollCallBtnText != null)
                {
                    StartRollCallBtnText.Text = originalStartBtnText;
                    StartRollCallBtnText.Foreground = (Brush)FindResource("NewRollCallWindowPrimaryButtonForeground");
                }

                // 恢复按钮背景色为主按钮背景
                StartRollCallBtn.Background = (Brush)FindResource("NewRollCallWindowPrimaryButtonBackground");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"恢复开始点名按钮原始样式时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void RandomMode_Click(object sender, RoutedEventArgs e)
        {
            SetModeSelection("Random");
        }

        private void SequentialMode_Click(object sender, RoutedEventArgs e)
        {
            SetModeSelection("Sequential");
        }

        private void GroupMode_Click(object sender, RoutedEventArgs e)
        {
            SetModeSelection("Group");
        }

        private void ExternalCallerMode_Click(object sender, RoutedEventArgs e)
        {
            SetModeSelection("External");
        }

        private void ExternalCallerTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (ExternalCallerTypeComboBox.SelectedItem is ComboBoxItem selectedItem)
                {
                    selectedExternalCaller = selectedItem.Content.ToString();

                    if (selectedRollCallMode == "External")
                    {
                        UpdateStatusDisplay($"已选择外部点名: {selectedExternalCaller}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"外部点名类型选择更改时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private static bool isExternalCallerFirstClick = true;

        private void ExternalCaller_Click(object sender, RoutedEventArgs e)
        {
            if (isExternalCallerFirstClick)
            {
                MessageBox.Show(
                    "首次使用外部点名功能，请确保已安装相应的点名软件。\n" +
                    "如未安装，请前往官网下载并安装后再使用。如果已安装请再次点击此按钮。",
                    "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                isExternalCallerFirstClick = false;
                return;
            }

            try
            {
                string protocol = "";
                switch (selectedExternalCaller)
                {
                    case "ClassIsland":
                        protocol = "classisland://plugins/IslandCaller/Simple/1";
                        break;
                    case "SecRandom":
                        protocol = "secrandom://direct_extraction";
                        break;
                    case "NamePicker":
                        protocol = "namepicker://";
                        break;
                    default:
                        protocol = "classisland://plugins/IslandCaller/Simple/1";
                        break;
                }

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = protocol,
                    UseShellExecute = true
                });

                UpdateStatusDisplay($"已启动外部点名: {selectedExternalCaller}");
            }
            catch (Exception ex)
            {
                MessageBox.Show("无法调用外部点名：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                LogHelper.WriteLogToFile($"外部点名调用失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }


        private void StartRollCall_Click(object sender, RoutedEventArgs e)
        {
            // 如果是外部点名模式，调用外部点名
            if (selectedRollCallMode == "External")
            {
                ExternalCaller_Click(sender, e);
                return;
            }

            if (isSingleDrawMode)
            {
                // 单次抽模式：直接开始抽选
                StartSingleDraw();
            }
            else
            {
                // 普通点名模式：检查名单或使用60个数字
                if (nameList.Count == 0)
                {
                    // 没有导入名单时，使用60个数字
                    StartRollCallWithNumbers();
                }
                else
                {
                    // 有名单时，使用名单
                    if (currentCount > nameList.Count)
                    {
                        MessageBox.Show($"点名人数不能超过名单人数！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    StartRollCall();
                }
            }
        }

        private void StopRollCall_Click(object sender, RoutedEventArgs e)
        {
            StopRollCall();
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            if (isRollCalling)
            {
                StopRollCall();
            }

            MainResultDisplay.Text = "点击开始点名";
            MainResultDisplay.Visibility = Visibility.Visible;
            MultiResultScrollViewer.Visibility = Visibility.Collapsed;
            UpdateStatusDisplay("准备就绪");
        }

        private void StartRollCall()
        {
            isRollCalling = true;
            StartRollCallBtn.Visibility = Visibility.Collapsed;
            StopRollCallBtn.Visibility = Visibility.Visible;
            UpdateStatusDisplay("正在点名...");

            // 启动点名动画
            StartRollCallAnimation();
        }

        /// <summary>
        /// 点名动画
        /// </summary>
        private void StartRollCallAnimation()
        {
            const int animationTimes = 100; // 动画次数
            const int sleepTime = 5; // 每次动画间隔（毫秒）

            new System.Threading.Thread(() =>
            {
                List<string> usedNames = new List<string>();

                // 确保动画期间主显示区域可见
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MainResultDisplay.Visibility = Visibility.Visible;
                    MultiResultScrollViewer.Visibility = Visibility.Collapsed;
                });

                for (int i = 0; i < animationTimes; i++)
                {
                    // 随机选择一个名字进行动画显示
                    if (nameList.Count > 0)
                    {
                        int randomIndex = new Random().Next(0, nameList.Count);
                        string displayName = nameList[randomIndex];

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            // 确保主显示区域在动画期间保持可见
                            MainResultDisplay.Visibility = Visibility.Visible;
                            MainResultDisplay.Text = displayName;
                        });
                    }

                    System.Threading.Thread.Sleep(sleepTime);
                }

                // 动画结束，显示最终结果
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // 根据选择的模式进行不同的点名逻辑
                    var selectedNames = SelectNamesByMode(nameList, currentCount);

                    // 更新历史记录
                    UpdateRollCallHistory(selectedNames);

                    // 显示结果
                    ShowResults(selectedNames);
                    UpdateStatusDisplay($"点名完成，共选择 {selectedNames.Count} 人");

                    // 停止点名状态
                    isRollCalling = false;
                    StartRollCallBtn.Visibility = Visibility.Visible;
                    StopRollCallBtn.Visibility = Visibility.Collapsed;
                });
            }).Start();
        }

        private void StartRollCallWithNumbers()
        {
            isRollCalling = true;
            StartRollCallBtn.Visibility = Visibility.Collapsed;
            StopRollCallBtn.Visibility = Visibility.Visible;
            UpdateStatusDisplay("正在抽选...");

            // 启动数字抽选动画
            StartNumberRollCallAnimation();
        }

        /// <summary>
        /// 数字抽选动画
        /// </summary>
        private void StartNumberRollCallAnimation()
        {
            const int animationTimes = 100; // 动画次数
            const int sleepTime = 5; // 每次动画间隔（毫秒）

            new System.Threading.Thread(() =>
            {
                List<int> usedNumbers = new List<int>();

                // 确保动画期间主显示区域可见
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MainResultDisplay.Visibility = Visibility.Visible;
                    MultiResultScrollViewer.Visibility = Visibility.Collapsed;
                });

                for (int i = 0; i < animationTimes; i++)
                {
                    // 随机选择一个数字进行动画显示
                    int randomNumber = new Random().Next(1, 61); // 1-60

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // 确保主显示区域在动画期间保持可见
                        MainResultDisplay.Visibility = Visibility.Visible;
                        MainResultDisplay.Text = randomNumber.ToString();
                    });

                    System.Threading.Thread.Sleep(sleepTime);
                }

                // 动画结束，显示最终结果
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // 根据选择的模式进行不同的抽选逻辑
                    var numberList = Enumerable.Range(1, 60).Select(n => n.ToString()).ToList();
                    var selectedNumbers = SelectNamesByMode(numberList, currentCount);

                    // 更新历史记录
                    UpdateRollCallHistory(selectedNumbers);

                    // 显示结果（这里会根据结果数量决定显示主显示区域还是多结果区域）
                    ShowResults(selectedNumbers);
                    UpdateStatusDisplay($"抽选完成，共选择 {selectedNumbers.Count} 个数字");

                    // 停止点名状态
                    isRollCalling = false;
                    StartRollCallBtn.Visibility = Visibility.Visible;
                    StopRollCallBtn.Visibility = Visibility.Collapsed;
                });
            }).Start();
        }

        private void StopRollCall()
        {
            isRollCalling = false;
            StartRollCallBtn.Visibility = Visibility.Visible;
            StopRollCallBtn.Visibility = Visibility.Collapsed;
            UpdateStatusDisplay("已停止点名");
        }

        /// <summary>
        /// 开始单次抽选
        /// </summary>
        private void StartSingleDraw()
        {
            isRollCalling = true;
            StartRollCallBtn.Visibility = Visibility.Collapsed;
            StopRollCallBtn.Visibility = Visibility.Visible;
            UpdateStatusDisplay("正在抽选...");

            // 启动抽选动画
            StartSingleDrawAnimation();
        }

        /// <summary>
        /// 单次抽选动画
        /// </summary>
        private void StartSingleDrawAnimation()
        {
            const int animationTimes = 100; // 动画次数
            const int sleepTime = 5; // 每次动画间隔（毫秒），参考老点名窗口

            new System.Threading.Thread(() =>
            {
                if (nameList.Count > 0)
                {
                    // 有名单时，从名单中抽选
                    StartSingleDrawNameAnimation(animationTimes, sleepTime);
                }
                else
                {
                    // 没有名单时，从1-60数字中抽选
                    StartSingleDrawNumberAnimation(animationTimes, sleepTime);
                }
            }).Start();
        }

        /// <summary>
        /// 单次抽选名单动画
        /// </summary>
        private void StartSingleDrawNameAnimation(int animationTimes, int sleepTime)
        {
            List<string> usedNames = new List<string>();

            for (int i = 0; i < animationTimes; i++)
            {
                // 随机选择一个名字进行动画显示，避免立即重复
                string randomName;
                do
                {
                    randomName = nameList[singleDrawRandom.Next(0, nameList.Count)];
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
                // 根据选择的模式进行不同的抽选逻辑
                var selectedNames = SelectNamesByMode(nameList, currentCount);

                // 更新历史记录
                UpdateRollCallHistory(selectedNames);

                // 显示结果
                ShowResults(selectedNames);
                UpdateStatusDisplay($"抽选完成，共选择 {selectedNames.Count} 人");

                // 停止点名状态
                isRollCalling = false;
                StartRollCallBtn.Visibility = Visibility.Visible;
                StopRollCallBtn.Visibility = Visibility.Collapsed;

                if (isSingleDrawMode)
                {
                    new System.Threading.Thread(() =>
                    {
                        System.Threading.Thread.Sleep(autoCloseWaitTime);
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (ControlOptionsGrid != null)
                            {
                                ControlOptionsGrid.Opacity = 1;
                                ControlOptionsGrid.IsHitTestVisible = true;
                            }
                            if (StartRollCallBtn != null)
                            {
                                StartRollCallBtn.Opacity = 1;
                                StartRollCallBtn.IsEnabled = true;
                            }
                            if (ResetBtn != null)
                            {
                                ResetBtn.Opacity = 1;
                                ResetBtn.IsEnabled = true;
                            }
                            Close();
                        });
                    }).Start();
                }
            });
        }

        /// <summary>
        /// 单次抽选数字动画
        /// </summary>
        private void StartSingleDrawNumberAnimation(int animationTimes, int sleepTime)
        {
            List<int> usedNumbers = new List<int>();

            for (int i = 0; i < animationTimes; i++)
            {
                // 随机选择一个数字进行动画显示，避免立即重复
                int randomNumber;
                do
                {
                    randomNumber = singleDrawRandom.Next(1, 61); // 1-60
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
                // 根据选择的模式进行不同的抽选逻辑
                var numberList = Enumerable.Range(1, 60).Select(n => n.ToString()).ToList();
                var selectedNumbers = SelectNamesByMode(numberList, currentCount);

                // 更新历史记录
                UpdateRollCallHistory(selectedNumbers);

                if (selectedNumbers.Count == 1)
                {
                    MainResultDisplay.Text = selectedNumbers[0];
                    UpdateStatusDisplay($"抽选完成：{selectedNumbers[0]}");
                }
                else
                {
                    MainResultDisplay.Text = "抽选结果";
                    MultiResultPanel.Visibility = Visibility.Visible;

                    Result1Display.Text = selectedNumbers.Count > 0 ? selectedNumbers[0] : "";
                    Result2Display.Text = selectedNumbers.Count > 1 ? selectedNumbers[1] : "";
                    Result3Display.Text = selectedNumbers.Count > 2 ? selectedNumbers[2] : "";

                    UpdateStatusDisplay($"抽选完成，共选择 {selectedNumbers.Count} 个数字");
                }

                // 停止点名状态
                isRollCalling = false;
                StartRollCallBtn.Visibility = Visibility.Visible;
                StopRollCallBtn.Visibility = Visibility.Collapsed;

                if (isSingleDrawMode)
                {
                    new System.Threading.Thread(() =>
                    {
                        System.Threading.Thread.Sleep(autoCloseWaitTime);
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (ControlOptionsGrid != null)
                            {
                                ControlOptionsGrid.Opacity = 1;
                                ControlOptionsGrid.IsHitTestVisible = true;
                            }
                            // 恢复开始点名和重置按钮
                            if (StartRollCallBtn != null)
                            {
                                StartRollCallBtn.Opacity = 1;
                                StartRollCallBtn.IsEnabled = true;
                            }
                            if (ResetBtn != null)
                            {
                                ResetBtn.Opacity = 1;
                                ResetBtn.IsEnabled = true;
                            }
                            Close();
                        });
                    }).Start();
                }
            });
        }

        /// <summary>
        /// 选择多个数字
        /// </summary>
        private List<string> SelectMultipleNumbers(int count)
        {
            var selectedNumbers = new List<string>();
            var usedNumbers = new List<int>();

            for (int i = 0; i < count && usedNumbers.Count < 60; i++)
            {
                int randomNumber = singleDrawRandom.Next(1, 61); // 1-60

                // 避免重复选择
                while (usedNumbers.Contains(randomNumber))
                {
                    randomNumber = singleDrawRandom.Next(1, 61);
                }

                usedNumbers.Add(randomNumber);
                selectedNumbers.Add(randomNumber.ToString());
            }

            return selectedNumbers;
        }

        private void RollCallTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            // 这里可以实现点名动画效果
            Application.Current.Dispatcher.Invoke(() =>
            {
                // 动画逻辑可以在这里实现
            });
        }
        #endregion

        #region 窗口事件处理
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 窗口加载时的初始化
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            rollCallTimer?.Stop();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void WindowDragMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            lastActivityTime = DateTime.Now;
        }

        private void Window_MouseEnter(object sender, MouseEventArgs e)
        {
            lastActivityTime = DateTime.Now;
        }

        private void SetDarkThemeBorder()
        {
            try
            {
                if (MainBorder != null)
                {
                    MainBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(64, 64, 64));
                }
            }
            catch
            {
                // 忽略错误
            }
        }
        #endregion

        #region Win32 API 声明和置顶管理
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentProcessId();

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOPMOST = 0x00000008;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_NOOWNERZORDER = 0x0200;

        /// <summary>
        /// 应用点名窗口置顶
        /// </summary>
        private void ApplyRollCallWindowTopmost()
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;

                // 强制激活窗口
                Activate();
                Focus();

                // 设置WPF的Topmost属性
                Topmost = true;

                // 使用Win32 API强制置顶
                // 1. 设置窗口样式为置顶
                int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOPMOST);

                // 2. 使用SetWindowPos确保窗口在最顶层
                SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW | SWP_NOOWNERZORDER);

                LogHelper.WriteLogToFile("点名窗口已应用置顶", LogHelper.LogType.Trace);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"应用点名窗口置顶失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 窗口加载事件处理，确保置顶
        /// </summary>
        private void RollCallWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 使用延迟确保窗口完全加载后再应用置顶
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ApplyRollCallWindowTopmost();
            }), DispatcherPriority.Loaded);
        }
        #endregion
    }
}

