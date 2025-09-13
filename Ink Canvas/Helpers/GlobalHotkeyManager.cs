using Newtonsoft.Json;
using NHotkey.Wpf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
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

        // 配置文件路径
        private static readonly string HotkeyConfigFile = Path.Combine(App.RootPath, "HotkeyConfig.json");
        #endregion

        #region Constructor
        public GlobalHotkeyManager(MainWindow mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _registeredHotkeys = new Dictionary<string, HotkeyInfo>();
            _hotkeysShouldBeRegistered = true; // 启动时注册热键
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

                // 如果快捷键已存在，先注销
                if (_registeredHotkeys.ContainsKey(hotkeyName))
                {
                    UnregisterHotkey(hotkeyName);
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
                // 成功注册全局快捷键
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"注册全局快捷键 {hotkeyName} 失败: {ex.Message}", LogHelper.LogType.Error);
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
                    LogHelper.WriteLogToFile("快捷键配置文件不存在");
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

                LogHelper.WriteLogToFile($"从配置文件读取到 {hotkeyList.Count} 个快捷键信息");
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

                // 尝试从配置文件加载
                if (LoadHotkeysFromConfigFile())
                {
                    // 成功从配置文件加载快捷键设置
                    _hotkeysShouldBeRegistered = true;
                    LogHelper.WriteLogToFile("成功从配置文件加载快捷键设置");
                }
                else
                {
                    // 如果配置文件不存在或加载失败，使用默认快捷键
                    if (!File.Exists(HotkeyConfigFile))
                    {
                        LogHelper.WriteLogToFile("配置文件不存在，注册默认快捷键");
                        RegisterDefaultHotkeys();
                        _hotkeysShouldBeRegistered = true;
                    }
                    else
                    {
                        LogHelper.WriteLogToFile("配置文件存在但加载失败，回退到默认快捷键", LogHelper.LogType.Warning);
                        RegisterDefaultHotkeys();
                        _hotkeysShouldBeRegistered = true;
                    }
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
                LogHelper.WriteLogToFile("开始保存快捷键配置到配置文件", LogHelper.LogType.Event);

                if (SaveHotkeysToConfigFile())
                {
                    LogHelper.WriteLogToFile("快捷键配置已成功保存到配置文件", LogHelper.LogType.Event);
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
                    LogHelper.WriteLogToFile("启用快捷键注册功能");

                    // 立即加载快捷键设置
                    LoadHotkeysFromSettings();
                }
                else
                {
                    LogHelper.WriteLogToFile("快捷键注册功能已经启用，重新加载快捷键设置");
                    // 即使已经启用，也要重新加载快捷键设置以确保快捷键正常工作
                    LoadHotkeysFromSettings();
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
                    LogHelper.WriteLogToFile("禁用快捷键注册功能");

                    // 注销所有快捷键
                    UnregisterAllHotkeys();
                }
                else
                {
                    LogHelper.WriteLogToFile("快捷键注册功能已经禁用");
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
                        LogHelper.WriteLogToFile("切换到鼠标模式，但根据设置保持快捷键启用");
                    }
                    else
                    {
                        // 鼠标模式下禁用快捷键，让键盘操作放行
                        DisableHotkeyRegistration();
                        LogHelper.WriteLogToFile("切换到鼠标模式，禁用快捷键以放行键盘操作");
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
                    LogHelper.WriteLogToFile($"成功更新快捷键 {hotkeyName}: {modifiers}+{key}", LogHelper.LogType.Event);
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
        #endregion

        #region Private Helper Methods
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

                LogHelper.WriteLogToFile($"成功加载 {successCount}/{config.Hotkeys.Count} 个快捷键配置", LogHelper.LogType.Event);
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

                LogHelper.WriteLogToFile($"快捷键配置已保存到: {HotkeyConfigFile}", LogHelper.LogType.Event);
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
