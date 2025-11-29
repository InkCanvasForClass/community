using Newtonsoft.Json;
using NHotkey.Wpf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 全局快捷键管理器 - 使用NHotkey库实现全局快捷键功能
    /// </summary>
    public class GlobalHotkeyManager : IDisposable
    {
        #region Private Fields
        private readonly Dictionary<string, HotkeyInfo> _registeredHotkeys;
        private readonly MainWindow _mainWindow;
        private bool _isDisposed;
        private bool _hotkeysShouldBeRegistered = true; // 启动时注册热键

        // 多屏幕支持相关字段
        private Screen _currentScreen;
        private bool _isMultiScreenMode = false;
        private bool _enableScreenSpecificHotkeys = true; // 是否启用基于屏幕的热键注册

        // 智能热键管理相关字段
        private bool _isWindowFocused = false;
        private bool _isMouseOverWindow = false;
        private System.Windows.Threading.DispatcherTimer _mousePositionTimer;

        // 配置文件路径
        private static readonly string HotkeyConfigFile = Path.Combine(App.RootPath, "Configs", "HotkeyConfig.json");
        #endregion

        #region Constructor
        public GlobalHotkeyManager(MainWindow mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _registeredHotkeys = new Dictionary<string, HotkeyInfo>();
            _hotkeysShouldBeRegistered = true; // 启动时注册热键

            // 初始化多屏幕支持
            InitializeMultiScreenSupport();

            // 启动时确保配置文件存在
            EnsureConfigFileExists();
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// 注册全局快捷键
        /// </summary>
        /// <param name="hotkeyName">快捷键名称</param>
        /// <param name="key">按键</param>
        /// <param name="modifiers">修饰键</param>
        /// <param name="action">执行动作</param>
        /// <returns>是否注册成功</returns>
        public bool RegisterHotkey(string hotkeyName, Key key, ModifierKeys modifiers, Action action)
        {
            try
            {
                if (_isDisposed)
                    return false;

                // 检查是否应该注册热键（基于屏幕和模式）
                if (!ShouldRegisterHotkeys())
                {
                    return false;
                }

                // 如果快捷键已存在，先注销
                if (_registeredHotkeys.ContainsKey(hotkeyName))
                {
                    UnregisterHotkey(hotkeyName);
                }
                else
                {
                    try
                    {
                        HotkeyManager.Current.Remove(hotkeyName);
                    }
                    catch
                    {
                    }
                }

                // 创建快捷键信息
                var hotkeyInfo = new HotkeyInfo
                {
                    Name = hotkeyName,
                    Key = key,
                    Modifiers = modifiers,
                    Action = action
                };

                // 注册快捷键
                HotkeyManager.Current.AddOrReplace(hotkeyName, key, modifiers, (sender, e) =>
                {
                    try
                    {
                        // 确保在主线程中执行
                        _mainWindow.Dispatcher.Invoke(() =>
                        {
                            action?.Invoke();
                        });
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"执行快捷键 {hotkeyName} 时出错: {ex.Message}", LogHelper.LogType.Error);
                    }
                });

                _registeredHotkeys[hotkeyName] = hotkeyInfo;

                // 记录注册信息
                var screenInfo = _isMultiScreenMode ? $" (屏幕: {_currentScreen?.DeviceName})" : "";

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 注销指定快捷键
        /// </summary>
        /// <param name="hotkeyName">快捷键名称</param>
        /// <returns>是否注销成功</returns>
        public bool UnregisterHotkey(string hotkeyName)
        {
            try
            {
                if (_isDisposed || !_registeredHotkeys.ContainsKey(hotkeyName))
                    return false;

                HotkeyManager.Current.Remove(hotkeyName);
                _registeredHotkeys.Remove(hotkeyName);
                // 成功注销全局快捷键
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"注销全局快捷键 {hotkeyName} 失败: {ex.Message}", LogHelper.LogType.Error);
                return false;
            }
        }

        /// <summary>
        /// 注销所有快捷键
        /// </summary>
        public void UnregisterAllHotkeys()
        {
            try
            {
                if (_isDisposed)
                    return;

                foreach (var hotkeyName in _registeredHotkeys.Keys)
                {
                    try
                    {
                        HotkeyManager.Current.Remove(hotkeyName);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"注销快捷键 {hotkeyName} 时出错: {ex.Message}", LogHelper.LogType.Warning);
                    }
                }

                _registeredHotkeys.Clear();
                // 已注销所有全局快捷键，集合已清空
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"注销所有快捷键时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 检查快捷键是否已注册
        /// </summary>
        /// <param name="hotkeyName">快捷键名称</param>
        /// <returns>是否已注册</returns>
        public bool IsHotkeyRegistered(string hotkeyName)
        {
            return _registeredHotkeys.ContainsKey(hotkeyName);
        }

        /// <summary>
        /// 获取已注册的快捷键列表
        /// </summary>
        /// <returns>快捷键信息列表</returns>
        public List<HotkeyInfo> GetRegisteredHotkeys()
        {
            return new List<HotkeyInfo>(_registeredHotkeys.Values);
        }

        /// <summary>
        /// 获取配置文件中的快捷键信息（不注册，仅用于显示）
        /// </summary>
        /// <returns>配置文件中的快捷键列表</returns>
        public List<HotkeyInfo> GetHotkeysFromConfigFile()
        {
            try
            {
                if (!File.Exists(HotkeyConfigFile))
                {
                    return new List<HotkeyInfo>();
                }

                // 读取配置文件内容
                string jsonContent = File.ReadAllText(HotkeyConfigFile, Encoding.UTF8);
                if (string.IsNullOrEmpty(jsonContent))
                {
                    LogHelper.WriteLogToFile("快捷键配置文件为空", LogHelper.LogType.Warning);
                    return new List<HotkeyInfo>();
                }

                // 反序列化配置
                var config = JsonConvert.DeserializeObject<HotkeyConfig>(jsonContent);
                if (config?.Hotkeys == null || config.Hotkeys.Count == 0)
                {
                    LogHelper.WriteLogToFile("快捷键配置为空或格式错误", LogHelper.LogType.Warning);
                    return new List<HotkeyInfo>();
                }

                // 转换为HotkeyInfo列表（不注册，仅用于显示）
                var hotkeyList = new List<HotkeyInfo>();
                foreach (var hotkeyConfig in config.Hotkeys)
                {
                    hotkeyList.Add(new HotkeyInfo
                    {
                        Name = hotkeyConfig.Name,
                        Key = hotkeyConfig.Key,
                        Modifiers = hotkeyConfig.Modifiers,
                        Action = null // 不设置动作，仅用于显示
                    });
                }

                return hotkeyList;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"从配置文件读取快捷键信息时出错: {ex.Message}", LogHelper.LogType.Error);
                return new List<HotkeyInfo>();
            }
        }

        /// <summary>
        /// 注册默认快捷键集合
        /// </summary>
        public void RegisterDefaultHotkeys()
        {
            try
            {
                // 开始注册默认快捷键集合

                // 基本操作快捷键
                RegisterHotkey("Undo", Key.Z, ModifierKeys.Control, () => _mainWindow.SymbolIconUndo_MouseUp(null, null));
                RegisterHotkey("Redo", Key.Y, ModifierKeys.Control, () => _mainWindow.SymbolIconRedo_MouseUp(null, null));
                RegisterHotkey("Clear", Key.E, ModifierKeys.Control, () => _mainWindow.SymbolIconDelete_MouseUp(null, null));
                RegisterHotkey("Paste", Key.V, ModifierKeys.Control, () => _mainWindow.HandleGlobalPaste(null, null));

                // 工具切换快捷键
                RegisterHotkey("SelectTool", Key.S, ModifierKeys.Alt, () => _mainWindow.SymbolIconSelect_MouseUp(null, null));
                RegisterHotkey("DrawTool", Key.D, ModifierKeys.Alt, () => _mainWindow.PenIcon_Click(null, null));
                RegisterHotkey("EraserTool", Key.E, ModifierKeys.Alt, () => _mainWindow.EraserIcon_Click(null, null));
                RegisterHotkey("BlackboardTool", Key.B, ModifierKeys.Alt, () => _mainWindow.ImageBlackboard_MouseUp(null, null));
                RegisterHotkey("QuitDrawTool", Key.Q, ModifierKeys.Alt, () => _mainWindow.KeyChangeToQuitDrawTool(null, null));

                // 画笔快捷键 - 使用反射访问penType字段
                RegisterHotkey("Pen1", Key.D1, ModifierKeys.Alt, () => SwitchToPenType(0));
                RegisterHotkey("Pen2", Key.D2, ModifierKeys.Alt, () => SwitchToPenType(1));
                RegisterHotkey("Pen3", Key.D3, ModifierKeys.Alt, () => SwitchToPenType(2));
                RegisterHotkey("Pen4", Key.D4, ModifierKeys.Alt, () => SwitchToPenType(3));
                RegisterHotkey("Pen5", Key.D5, ModifierKeys.Alt, () => SwitchToPenType(4));

                // 功能快捷键
                RegisterHotkey("DrawLine", Key.L, ModifierKeys.Alt, () => _mainWindow.BtnDrawLine_Click(null, null));
                RegisterHotkey("Screenshot", Key.C, ModifierKeys.Alt, () => _mainWindow.SaveScreenShotToDesktop());
                RegisterHotkey("Hide", Key.V, ModifierKeys.Alt, () => _mainWindow.SymbolIconEmoji_MouseUp(null, null));

                // 退出快捷键
                RegisterHotkey("Exit", Key.Escape, ModifierKeys.None, () => _mainWindow.KeyExit(null, null));

                // 已注册默认全局快捷键集合
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"注册默认快捷键时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 从配置文件加载快捷键
        /// </summary>
        public void LoadHotkeysFromSettings()
        {
            try
            {
                // 开始从配置文件加载快捷键设置

                // 检查是否应该注册快捷键
                if (!_hotkeysShouldBeRegistered)
                {
                    // 当前状态不允许注册快捷键，跳过加载
                    return;
                }

                // 如果配置文件不存在，先创建默认配置文件
                if (!File.Exists(HotkeyConfigFile))
                {
                    LogHelper.WriteLogToFile($"快捷键配置文件不存在: {HotkeyConfigFile}", LogHelper.LogType.Warning);
                    CreateDefaultConfigFile();
                    RegisterDefaultHotkeys();
                    _hotkeysShouldBeRegistered = true;
                    return;
                }

                // 尝试从配置文件加载
                if (LoadHotkeysFromConfigFile())
                {
                    // 成功从配置文件加载快捷键设置
                    _hotkeysShouldBeRegistered = true;
                }
                else
                {
                    LogHelper.WriteLogToFile("配置文件存在但加载失败，回退到默认快捷键", LogHelper.LogType.Warning);
                    RegisterDefaultHotkeys();
                    _hotkeysShouldBeRegistered = true;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"从设置加载快捷键时出错: {ex.Message}", LogHelper.LogType.Error);
                // 出错时不自动使用默认快捷键，保持当前状态
            }
        }

        /// <summary>
        /// 保存快捷键配置到设置
        /// </summary>
        public void SaveHotkeysToSettings()
        {
            try
            {

                if (SaveHotkeysToConfigFile())
                {
                }
                else
                {
                    LogHelper.WriteLogToFile("保存快捷键配置失败", LogHelper.LogType.Error);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"保存快捷键配置时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 启用快捷键注册功能
        /// 调用此方法后，快捷键将被允许注册
        /// </summary>
        public void EnableHotkeyRegistration()
        {
            try
            {
                if (!_hotkeysShouldBeRegistered)
                {
                    _hotkeysShouldBeRegistered = true;

                    // 启动鼠标位置监控定时器
                    if (_isMultiScreenMode && _enableScreenSpecificHotkeys && _mousePositionTimer != null)
                    {
                        _mousePositionTimer.Start();
                    }

                    // 根据上下文决定是否立即加载快捷键
                    if (ShouldEnableHotkeysBasedOnContext())
                    {
                        LoadHotkeysFromSettings();
                    }
                }
                else
                {
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"启用快捷键注册功能时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 禁用快捷键注册功能
        /// 调用此方法后，快捷键将被注销
        /// </summary>
        public void DisableHotkeyRegistration()
        {
            try
            {
                if (_hotkeysShouldBeRegistered)
                {
                    _hotkeysShouldBeRegistered = false;

                    // 停止鼠标位置监控定时器
                    if (_mousePositionTimer != null && _mousePositionTimer.IsEnabled)
                    {
                        _mousePositionTimer.Stop();
                    }

                    // 注销所有快捷键
                    UnregisterAllHotkeys();
                }
                else
                {
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"禁用快捷键注册功能时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 根据当前工具模式更新快捷键状态
        /// 在工具切换时调用此方法
        /// </summary>
        /// <param name="isMouseMode">是否为鼠标模式（选择模式）</param>
        public void UpdateHotkeyStateForToolMode(bool isMouseMode)
        {
            try
            {
                if (isMouseMode)
                {
                    // 检查设置中是否允许在鼠标模式下启用快捷键
                    if (MainWindow.Settings.Appearance.EnableHotkeysInMouseMode)
                    {
                        // 如果设置允许，则在鼠标模式下也启用快捷键
                        EnableHotkeyRegistration();
                    }
                    else
                    {
                        // 鼠标模式下禁用快捷键，让键盘操作放行
                        DisableHotkeyRegistration();
                    }
                }
                else
                {
                    // 非鼠标模式下启用快捷键
                    EnableHotkeyRegistration();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"更新快捷键状态时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 更新快捷键配置
        /// </summary>
        /// <param name="hotkeyName">快捷键名称</param>
        /// <param name="key">新按键</param>
        /// <param name="modifiers">新修饰键</param>
        /// <returns>是否更新成功</returns>
        public bool UpdateHotkey(string hotkeyName, Key key, ModifierKeys modifiers)
        {
            try
            {
                if (!_registeredHotkeys.ContainsKey(hotkeyName))
                {
                    LogHelper.WriteLogToFile($"快捷键 {hotkeyName} 不存在，无法更新", LogHelper.LogType.Warning);
                    return false;
                }

                // 获取原有的动作
                var originalAction = _registeredHotkeys[hotkeyName].Action;

                // 注销原有快捷键
                UnregisterHotkey(hotkeyName);

                // 注册新的快捷键
                var success = RegisterHotkey(hotkeyName, key, modifiers, originalAction);

                if (success)
                {
                    // 自动保存配置
                    SaveHotkeysToSettings();
                }

                return success;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"更新快捷键 {hotkeyName} 时出错: {ex.Message}", LogHelper.LogType.Error);
                return false;
            }
        }

        /// <summary>
        /// 启用基于屏幕的热键注册
        /// </summary>
        public void EnableScreenSpecificHotkeys()
        {
            try
            {
                _enableScreenSpecificHotkeys = true;

                // 如果当前在多屏幕环境下，刷新热键注册
                if (_isMultiScreenMode)
                {
                    RefreshHotkeysForCurrentScreen();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"启用基于屏幕的热键注册时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 禁用基于屏幕的热键注册
        /// </summary>
        public void DisableScreenSpecificHotkeys()
        {
            try
            {
                _enableScreenSpecificHotkeys = false;

                // 重新注册热键（全局模式）
                if (_hotkeysShouldBeRegistered)
                {
                    RefreshHotkeysForCurrentScreen();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"禁用基于屏幕的热键注册时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 获取当前屏幕信息
        /// </summary>
        /// <returns>当前屏幕信息</returns>
        public string GetCurrentScreenInfo()
        {
            try
            {
                if (_isMultiScreenMode && _currentScreen != null)
                {
                    return $"多屏幕环境 - 当前屏幕: {_currentScreen.DeviceName} ({_currentScreen.Bounds.Width}x{_currentScreen.Bounds.Height})";
                }
                else
                {
                    return "单屏幕环境";
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"获取当前屏幕信息时出错: {ex.Message}", LogHelper.LogType.Error);
                return "无法获取屏幕信息";
            }
        }

        /// <summary>
        /// 检查是否启用了基于屏幕的热键注册
        /// </summary>
        /// <returns>是否启用</returns>
        public bool IsScreenSpecificHotkeysEnabled()
        {
            return _enableScreenSpecificHotkeys && _isMultiScreenMode;
        }

        /// <summary>
        /// 手动刷新当前屏幕的热键注册
        /// </summary>
        public void RefreshCurrentScreenHotkeys()
        {
            try
            {
                RefreshHotkeysForCurrentScreen();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"刷新当前屏幕热键时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }
        #endregion

        #region Private Helper Methods
        /// <summary>
        /// 初始化多屏幕支持
        /// </summary>
        private void InitializeMultiScreenSupport()
        {
            try
            {
                // 检测是否有多个屏幕
                _isMultiScreenMode = ScreenDetectionHelper.HasMultipleScreens();

                if (_isMultiScreenMode)
                {
                    // 获取当前窗口所在的屏幕
                    _currentScreen = ScreenDetectionHelper.GetWindowScreen(_mainWindow);

                    // 监听窗口位置变化事件
                    _mainWindow.LocationChanged += OnWindowLocationChanged;

                    // 初始化智能热键管理
                    InitializeSmartHotkeyManagement();
                }
                else
                {
                    _currentScreen = ScreenDetectionHelper.GetPrimaryScreen();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"初始化多屏幕支持时出错: {ex.Message}", LogHelper.LogType.Error);
                _isMultiScreenMode = false;
                _currentScreen = ScreenDetectionHelper.GetPrimaryScreen();
            }
        }

        /// <summary>
        /// 初始化智能热键管理
        /// </summary>
        private void InitializeSmartHotkeyManagement()
        {
            try
            {
                // 监听窗口焦点事件
                _mainWindow.GotFocus += OnWindowGotFocus;
                _mainWindow.LostFocus += OnWindowLostFocus;

                // 监听鼠标进入/离开事件
                _mainWindow.MouseEnter += OnMouseEnterWindow;
                _mainWindow.MouseLeave += OnMouseLeaveWindow;

                // 初始化鼠标位置监控定时器
                _mousePositionTimer = new System.Windows.Threading.DispatcherTimer();
                _mousePositionTimer.Interval = TimeSpan.FromMilliseconds(500); // 每500ms检查一次
                _mousePositionTimer.Tick += OnMousePositionTimerTick;

            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"初始化热键管理时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 窗口位置变化事件处理
        /// </summary>
        private void OnWindowLocationChanged(object sender, EventArgs e)
        {
            try
            {
                if (!_isMultiScreenMode || !_enableScreenSpecificHotkeys)
                    return;

                var newScreen = ScreenDetectionHelper.GetWindowScreen(_mainWindow);
                if (newScreen != null && newScreen != _currentScreen)
                {
                    _currentScreen = newScreen;

                    // 重新注册热键以适应新屏幕
                    RefreshHotkeysForCurrentScreen();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理窗口位置变化时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 为当前屏幕刷新热键注册
        /// </summary>
        private void RefreshHotkeysForCurrentScreen()
        {
            try
            {
                if (!_hotkeysShouldBeRegistered)
                    return;

                // 注销所有现有热键
                UnregisterAllHotkeys();

                // 重新注册热键
                LoadHotkeysFromSettings();

            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"刷新当前屏幕热键时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 窗口获得焦点事件处理
        /// </summary>
        private void OnWindowGotFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                _isWindowFocused = true;
                UpdateHotkeyStateBasedOnContext();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理窗口获得焦点事件时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 窗口失去焦点事件处理
        /// </summary>
        private void OnWindowLostFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                _isWindowFocused = false;
                UpdateHotkeyStateBasedOnContext();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理窗口失去焦点事件时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 鼠标进入窗口事件处理
        /// </summary>
        private void OnMouseEnterWindow(object sender, System.Windows.Input.MouseEventArgs e)
        {
            try
            {
                _isMouseOverWindow = true;
                UpdateHotkeyStateBasedOnContext();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理鼠标进入窗口事件时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 鼠标离开窗口事件处理
        /// </summary>
        private void OnMouseLeaveWindow(object sender, System.Windows.Input.MouseEventArgs e)
        {
            try
            {
                _isMouseOverWindow = false;
                UpdateHotkeyStateBasedOnContext();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理鼠标离开窗口事件时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 鼠标位置定时器事件处理
        /// </summary>
        private void OnMousePositionTimerTick(object sender, EventArgs e)
        {
            try
            {
                if (!_isMultiScreenMode || !_enableScreenSpecificHotkeys)
                    return;

                // 检查鼠标是否在当前窗口所在的屏幕上
                var mousePosition = Control.MousePosition;
                var currentScreen = Screen.FromPoint(mousePosition);

                // 无论屏幕是否变化，都检查热键状态
                // 这样可以确保热键状态始终与当前上下文保持一致
                bool shouldEnableHotkeys = ShouldEnableHotkeysBasedOnContext();
                bool currentlyHasHotkeys = _registeredHotkeys.Count > 0;

                if (shouldEnableHotkeys && !currentlyHasHotkeys)
                {
                    UpdateHotkeyStateBasedOnContext();
                }
                else if (!shouldEnableHotkeys && currentlyHasHotkeys)
                {
                    UpdateHotkeyStateBasedOnContext();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理鼠标位置定时器事件时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 根据上下文更新热键状态
        /// </summary>
        private void UpdateHotkeyStateBasedOnContext()
        {
            try
            {
                if (!_hotkeysShouldBeRegistered)
                    return;

                bool shouldEnableHotkeys = ShouldEnableHotkeysBasedOnContext();
                bool currentlyHasHotkeys = _registeredHotkeys.Count > 0;

                if (shouldEnableHotkeys && !currentlyHasHotkeys)
                {
                    // 需要注册快捷键
                    LoadHotkeysFromSettings();
                }
                else if (!shouldEnableHotkeys && currentlyHasHotkeys)
                {
                    // 需要注销快捷键
                    UnregisterAllHotkeys();
                }
                // 如果状态没有变化，则不进行任何操作
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"根据上下文更新热键状态时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 检查是否应该注册热键（基于屏幕和模式）
        /// </summary>
        /// <returns>是否应该注册热键</returns>
        private bool ShouldRegisterHotkeys()
        {
            try
            {
                // 如果禁用热键注册，则不注册
                if (!_hotkeysShouldBeRegistered)
                    return false;

                // 如果启用基于屏幕的热键注册
                if (_enableScreenSpecificHotkeys && _isMultiScreenMode)
                {
                    return ShouldEnableHotkeysBasedOnContext();
                }

                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"检查是否应该注册热键时出错: {ex.Message}", LogHelper.LogType.Error);
                return true; // 出错时默认注册
            }
        }

        /// <summary>
        /// 根据上下文检查是否应该启用热键
        /// </summary>
        /// <returns>是否应该启用热键</returns>
        private bool ShouldEnableHotkeysBasedOnContext()
        {
            try
            {
                // 检查当前是否处于鼠标模式
                bool isMouseMode = IsInSelectMode();

                if (isMouseMode)
                {
                    // 鼠标模式下，根据设置决定是否启用快捷键
                    return MainWindow.Settings.Appearance.EnableHotkeysInMouseMode;
                }
                else
                {
                    // 非鼠标模式下，需要检查焦点和屏幕位置

                    // 策略1：鼠标在窗口上时启用热键（最高优先级）
                    if (_isMouseOverWindow)
                    {
                        return true;
                    }

                    // 策略2：在多屏幕环境下，检查鼠标是否在当前窗口所在的屏幕上
                    if (_isMultiScreenMode && _enableScreenSpecificHotkeys)
                    {
                        var mousePosition = Control.MousePosition;
                        var mouseScreen = Screen.FromPoint(mousePosition);

                        if (mouseScreen == _currentScreen)
                        {
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }

                    // 策略3：单屏幕环境下，窗口有焦点时启用热键
                    if (_isWindowFocused)
                    {
                        return true;
                    }

                    // 策略4：如果以上都不满足，但在非鼠标模式下，仍然启用快捷键
                    // 这样可以确保在批注模式下快捷键始终可用
                    return true;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"检查是否应该启用热键时出错: {ex.Message}", LogHelper.LogType.Error);
                return true; // 出错时默认启用
            }
        }

        /// <summary>
        /// 切换到指定笔类型
        /// </summary>
        /// <param name="penTypeIndex">笔类型索引</param>
        private void SwitchToPenType(int penTypeIndex)
        {
            try
            {
                // 通过反射访问主窗口的penType字段
                var penTypeField = _mainWindow.GetType().GetField("penType",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (penTypeField != null)
                {
                    penTypeField.SetValue(_mainWindow, penTypeIndex);

                    // 调用CheckPenTypeUIState方法更新UI状态
                    var checkPenTypeMethod = _mainWindow.GetType().GetMethod("CheckPenTypeUIState",
                        BindingFlags.NonPublic | BindingFlags.Instance);

                    if (checkPenTypeMethod != null)
                    {
                        checkPenTypeMethod.Invoke(_mainWindow, null);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"切换到笔类型{penTypeIndex}时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 确保配置文件存在，如果不存在则创建
        /// </summary>
        private void EnsureConfigFileExists()
        {
            try
            {
                // 如果配置文件不存在，创建默认配置文件
                if (!File.Exists(HotkeyConfigFile))
                {
                    CreateDefaultConfigFile();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"确保快捷键配置文件存在时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 创建默认的快捷键配置文件
        /// </summary>
        private void CreateDefaultConfigFile()
        {
            try
            {
                // 确保配置目录存在
                string configDir = Path.GetDirectoryName(HotkeyConfigFile);
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }

                // 创建默认配置对象
                var config = new HotkeyConfig
                {
                    Version = "1.0",
                    LastModified = DateTime.Now,
                    Hotkeys = new List<HotkeyConfigItem>()
                };

                // 添加默认快捷键配置
                config.Hotkeys.AddRange(new[]
                {
                    new HotkeyConfigItem { Name = "Undo", Key = Key.Z, Modifiers = ModifierKeys.Control },
                    new HotkeyConfigItem { Name = "Redo", Key = Key.Y, Modifiers = ModifierKeys.Control },
                    new HotkeyConfigItem { Name = "Clear", Key = Key.E, Modifiers = ModifierKeys.Control },
                    new HotkeyConfigItem { Name = "Paste", Key = Key.V, Modifiers = ModifierKeys.Control },
                    new HotkeyConfigItem { Name = "SelectTool", Key = Key.S, Modifiers = ModifierKeys.Alt },
                    new HotkeyConfigItem { Name = "DrawTool", Key = Key.D, Modifiers = ModifierKeys.Alt },
                    new HotkeyConfigItem { Name = "EraserTool", Key = Key.E, Modifiers = ModifierKeys.Alt },
                    new HotkeyConfigItem { Name = "BlackboardTool", Key = Key.B, Modifiers = ModifierKeys.Alt },
                    new HotkeyConfigItem { Name = "QuitDrawTool", Key = Key.Q, Modifiers = ModifierKeys.Alt },
                    new HotkeyConfigItem { Name = "Pen1", Key = Key.D1, Modifiers = ModifierKeys.Alt },
                    new HotkeyConfigItem { Name = "Pen2", Key = Key.D2, Modifiers = ModifierKeys.Alt },
                    new HotkeyConfigItem { Name = "Pen3", Key = Key.D3, Modifiers = ModifierKeys.Alt },
                    new HotkeyConfigItem { Name = "Pen4", Key = Key.D4, Modifiers = ModifierKeys.Alt },
                    new HotkeyConfigItem { Name = "Pen5", Key = Key.D5, Modifiers = ModifierKeys.Alt },
                    new HotkeyConfigItem { Name = "DrawLine", Key = Key.L, Modifiers = ModifierKeys.Alt },
                    new HotkeyConfigItem { Name = "Screenshot", Key = Key.C, Modifiers = ModifierKeys.Alt },
                    new HotkeyConfigItem { Name = "Hide", Key = Key.V, Modifiers = ModifierKeys.Alt },
                    new HotkeyConfigItem { Name = "Exit", Key = Key.Escape, Modifiers = ModifierKeys.None }
                });

                // 序列化为JSON
                var settings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented
                };

                string jsonContent = JsonConvert.SerializeObject(config, settings);

                // 写入配置文件
                File.WriteAllText(HotkeyConfigFile, jsonContent, Encoding.UTF8);

            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"创建默认快捷键配置文件时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 从配置文件加载快捷键设置
        /// </summary>
        /// <returns>是否加载成功</returns>
        private bool LoadHotkeysFromConfigFile()
        {
            try
            {
                if (!File.Exists(HotkeyConfigFile))
                {
                    LogHelper.WriteLogToFile($"快捷键配置文件不存在: {HotkeyConfigFile}", LogHelper.LogType.Warning);
                    return false;
                }

                // 读取配置文件内容
                string jsonContent = File.ReadAllText(HotkeyConfigFile, Encoding.UTF8);
                if (string.IsNullOrEmpty(jsonContent))
                {
                    LogHelper.WriteLogToFile("快捷键配置文件为空", LogHelper.LogType.Warning);
                    return false;
                }

                // 反序列化配置
                var config = JsonConvert.DeserializeObject<HotkeyConfig>(jsonContent);
                if (config?.Hotkeys == null || config.Hotkeys.Count == 0)
                {
                    LogHelper.WriteLogToFile("快捷键配置为空或格式错误", LogHelper.LogType.Warning);
                    return false;
                }

                // 注册配置中的快捷键
                int successCount = 0;
                foreach (var hotkeyConfig in config.Hotkeys)
                {
                    try
                    {
                        // 根据快捷键名称获取对应的动作
                        var action = GetActionByName(hotkeyConfig.Name);
                        if (action != null)
                        {
                            if (RegisterHotkey(hotkeyConfig.Name, hotkeyConfig.Key, hotkeyConfig.Modifiers, action))
                            {
                                successCount++;
                            }
                        }
                        else
                        {
                            LogHelper.WriteLogToFile($"未找到快捷键 {hotkeyConfig.Name} 对应的动作", LogHelper.LogType.Warning);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"注册快捷键 {hotkeyConfig.Name} 时出错: {ex.Message}", LogHelper.LogType.Error);
                    }
                }

                if (successCount > 0)
                {
                    _hotkeysShouldBeRegistered = true;
                }
                return successCount > 0;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"从配置文件加载快捷键时出错: {ex.Message}", LogHelper.LogType.Error);
                return false;
            }
        }

        /// <summary>
        /// 保存快捷键配置到配置文件
        /// </summary>
        /// <returns>是否保存成功</returns>
        private bool SaveHotkeysToConfigFile()
        {
            try
            {
                // 确保配置目录存在
                string configDir = Path.GetDirectoryName(HotkeyConfigFile);
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }

                // 创建配置对象
                var config = new HotkeyConfig
                {
                    Version = "1.0",
                    LastModified = DateTime.Now,
                    Hotkeys = new List<HotkeyConfigItem>()
                };

                // 添加所有已注册的快捷键
                foreach (var hotkey in _registeredHotkeys.Values)
                {
                    config.Hotkeys.Add(new HotkeyConfigItem
                    {
                        Name = hotkey.Name,
                        Key = hotkey.Key,
                        Modifiers = hotkey.Modifiers
                    });
                }

                // 序列化为JSON
                var settings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented
                };

                string jsonContent = JsonConvert.SerializeObject(config, settings);

                // 直接写入原文件，覆盖原有内容
                File.WriteAllText(HotkeyConfigFile, jsonContent, Encoding.UTF8);

                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"保存快捷键配置到配置文件时出错: {ex.Message}", LogHelper.LogType.Error);
                return false;
            }
        }

        /// <summary>
        /// 根据快捷键名称获取对应的动作
        /// </summary>
        /// <param name="hotkeyName">快捷键名称</param>
        /// <returns>对应的动作，如果不存在则返回null</returns>
        private Action GetActionByName(string hotkeyName)
        {
            try
            {
                switch (hotkeyName)
                {
                    case "Undo":
                        return () => _mainWindow.SymbolIconUndo_MouseUp(null, null);
                    case "Redo":
                        return () => _mainWindow.SymbolIconRedo_MouseUp(null, null);
                    case "Clear":
                        return () => _mainWindow.SymbolIconDelete_MouseUp(null, null);
                    case "Paste":
                        return () => _mainWindow.HandleGlobalPaste(null, null);
                    case "SelectTool":
                        return () => _mainWindow.SymbolIconSelect_MouseUp(null, null);
                    case "DrawTool":
                        return () => _mainWindow.PenIcon_Click(null, null);
                    case "EraserTool":
                        return () => _mainWindow.EraserIcon_Click(null, null);
                    case "BlackboardTool":
                        return () => _mainWindow.ImageBlackboard_MouseUp(null, null);
                    case "QuitDrawTool":
                        return () => _mainWindow.KeyChangeToQuitDrawTool(null, null);
                    case "Pen1":
                        return () => SwitchToPenType(0);
                    case "Pen2":
                        return () => SwitchToPenType(1);
                    case "Pen3":
                        return () => SwitchToPenType(2);
                    case "Pen4":
                        return () => SwitchToPenType(3);
                    case "Pen5":
                        return () => SwitchToPenType(4);
                    case "DrawLine":
                        return () => _mainWindow.BtnDrawLine_Click(null, null);
                    case "Screenshot":
                        return () => _mainWindow.SaveScreenShotToDesktop();
                    case "Hide":
                        return () => _mainWindow.SymbolIconEmoji_MouseUp(null, null);
                    case "Exit":
                        return () => _mainWindow.KeyExit(null, null);
                    default:
                        LogHelper.WriteLogToFile($"未知的快捷键名称: {hotkeyName}", LogHelper.LogType.Warning);
                        return null;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"获取快捷键 {hotkeyName} 对应动作时出错: {ex.Message}", LogHelper.LogType.Error);
                return null;
            }
        }

        /// <summary>
        /// 检查当前是否处于鼠标模式（选择模式）
        /// </summary>
        /// <returns>如果处于鼠标模式则返回true（不应该注册快捷键），否则返回false（应该注册快捷键）</returns>
        private bool IsInSelectMode()
        {
            try
            {
                // 通过反射访问主窗口的FloatingbarSelectionBG字段
                var floatingbarSelectionBGField = _mainWindow.GetType().GetField("FloatingbarSelectionBG",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (floatingbarSelectionBGField != null)
                {
                    var floatingbarSelectionBG = floatingbarSelectionBGField.GetValue(_mainWindow);
                    if (floatingbarSelectionBG != null)
                    {
                        // 检查高光是否可见
                        var visibilityProperty = floatingbarSelectionBG.GetType().GetProperty("Visibility");
                        if (visibilityProperty != null)
                        {
                            var visibility = visibilityProperty.GetValue(floatingbarSelectionBG);
                            if (visibility != null && visibility.ToString() == "Hidden")
                            {
                                // 高光隐藏，说明没有选中任何工具，此时应该注销快捷键以释放系统快捷键
                                return true; // 返回true表示应该注销快捷键
                            }
                        }

                        // 通过反射访问Canvas.GetLeft方法来获取高光位置
                        var canvasType = Type.GetType("System.Windows.Controls.Canvas, PresentationFramework");
                        if (canvasType != null)
                        {
                            var getLeftMethod = canvasType.GetMethod("GetLeft", BindingFlags.Public | BindingFlags.Static);
                            if (getLeftMethod != null)
                            {
                                var leftPosition = getLeftMethod.Invoke(null, new[] { floatingbarSelectionBG });
                                if (leftPosition != null)
                                {
                                    var position = Convert.ToDouble(leftPosition);

                                    // 根据高光位置判断当前选中的工具
                                    // 位置计算基于SetFloatingBarHighlightPosition方法中的逻辑
                                    bool isMouseMode;

                                    // 简化判断：如果位置接近0，说明是鼠标模式
                                    // 如果位置接近28，说明是批注模式
                                    // 如果位置更大，说明是其他工具
                                    if (position < 5) // 鼠标模式：marginOffset + (cursorWidth - actualHighlightWidth) / 2 ≈ 0
                                    {
                                        isMouseMode = true;
                                    }
                                    else if (position < 35) // 批注模式：marginOffset + cursorWidth + (penWidth - actualHighlightWidth) / 2 ≈ 28
                                    {
                                        isMouseMode = false;
                                    }
                                    else // 其他工具（橡皮擦、选择等）
                                    {
                                        isMouseMode = false;
                                    }

                                    return isMouseMode;
                                }
                            }
                        }
                    }
                }

                // 如果无法获取高光状态，则回退到inkCanvas.EditingMode判断

                // 通过反射访问主窗口的inkCanvas字段
                var inkCanvasField = _mainWindow.GetType().GetField("inkCanvas",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (inkCanvasField != null)
                {
                    var inkCanvas = inkCanvasField.GetValue(_mainWindow);
                    if (inkCanvas != null)
                    {
                        // 通过反射访问inkCanvas的EditingMode属性
                        var editingModeProperty = inkCanvas.GetType().GetProperty("EditingMode");
                        if (editingModeProperty != null)
                        {
                            var editingMode = editingModeProperty.GetValue(inkCanvas);
                            if (editingMode != null)
                            {
                                // 检查是否为批注模式
                                var isInkMode = editingMode.ToString().Contains("Ink");
                                var isSelectMode = editingMode.ToString().Contains("Select");

                                // 如果是批注模式或选择模式，则应该注册快捷键（返回false）
                                // 如果是橡皮擦模式或其他模式，则不应该注册快捷键（返回true）
                                var shouldNotRegisterHotkeys = !isInkMode && !isSelectMode;

                                return shouldNotRegisterHotkeys;
                            }
                        }
                    }
                }

                // 如果无法获取任何状态信息，则回退到原来的判断逻辑

                // 通过反射访问主窗口的currentMode字段（作为最后的备用方案）
                var currentModeField = _mainWindow.GetType().GetField("currentMode",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (currentModeField != null)
                {
                    var currentMode = currentModeField.GetValue(_mainWindow);
                    if (currentMode != null)
                    {
                        var modeValue = currentMode.ToString();
                        // 注意：这里的逻辑需要修正
                        // currentMode == 0 表示屏幕模式（PPT放映），此时应该允许快捷键
                        // currentMode == 1 表示黑板/白板模式，此时也应该允许快捷键
                        var isSelectMode = false; // 修正：所有模式都应该允许快捷键
                        return isSelectMode;
                    }
                }

                return false; // 默认允许快捷键
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"检查鼠标模式状态时出错: {ex.Message}", LogHelper.LogType.Warning);
                return false; // 出错时默认允许快捷键
            }
        }


        #endregion

        #region IDisposable Implementation
        public void Dispose()
        {
            if (!_isDisposed)
            {
                // 注销所有快捷键
                UnregisterAllHotkeys();

                // 停止定时器
                if (_mousePositionTimer != null)
                {
                    _mousePositionTimer.Stop();
                    _mousePositionTimer = null;
                }

                // 移除事件监听器
                if (_mainWindow != null)
                {
                    if (_isMultiScreenMode)
                    {
                        _mainWindow.LocationChanged -= OnWindowLocationChanged;
                    }

                    _mainWindow.GotFocus -= OnWindowGotFocus;
                    _mainWindow.LostFocus -= OnWindowLostFocus;
                    _mainWindow.MouseEnter -= OnMouseEnterWindow;
                    _mainWindow.MouseLeave -= OnMouseLeaveWindow;
                }

                _isDisposed = true;
            }
        }
        #endregion

        #region Nested Classes
        /// <summary>
        /// 快捷键信息类
        /// </summary>
        public class HotkeyInfo
        {
            public string Name { get; set; }
            public Key Key { get; set; }
            public ModifierKeys Modifiers { get; set; }
            public Action Action { get; set; }

            public override string ToString()
            {
                var modifiersText = Modifiers == ModifierKeys.None ? "" : $"{Modifiers}+";
                return $"{modifiersText}{Key}";
            }
        }

        /// <summary>
        /// 快捷键配置类
        /// </summary>
        private class HotkeyConfig
        {
            public string Version { get; set; }
            public DateTime LastModified { get; set; }
            public List<HotkeyConfigItem> Hotkeys { get; set; }
        }

        /// <summary>
        /// 快捷键配置项类
        /// </summary>
        private class HotkeyConfigItem
        {
            public string Name { get; set; }
            public Key Key { get; set; }
            public ModifierKeys Modifiers { get; set; }
        }
        #endregion
    }
}
