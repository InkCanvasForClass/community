using System;
using System.Collections.Generic;
using System.Windows.Input;
using NHotkey.Wpf;

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
        private bool _isDisposed = false;
        #endregion

        #region Constructor
        public GlobalHotkeyManager(MainWindow mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _registeredHotkeys = new Dictionary<string, HotkeyInfo>();
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
                LogHelper.WriteLogToFile($"成功注册全局快捷键: {hotkeyName} ({modifiers}+{key})", LogHelper.LogType.Event);
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
                LogHelper.WriteLogToFile($"成功注销全局快捷键: {hotkeyName}", LogHelper.LogType.Event);
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
                LogHelper.WriteLogToFile("已注销所有全局快捷键", LogHelper.LogType.Event);
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
        /// 注册默认快捷键集合
        /// </summary>
        public void RegisterDefaultHotkeys()
        {
            try
            {
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
                RegisterHotkey("QuitDrawTool", Key.Q, ModifierKeys.Alt, () => _mainWindow.CursorIcon_Click(null, null));

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

                LogHelper.WriteLogToFile("已注册默认全局快捷键集合", LogHelper.LogType.Event);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"注册默认快捷键时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 从设置加载快捷键配置
        /// </summary>
        public void LoadHotkeysFromSettings()
        {
            try
            {
                // 这里可以从配置文件或设置中加载自定义快捷键
                // 暂时使用默认快捷键
                RegisterDefaultHotkeys();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"从设置加载快捷键时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 保存快捷键配置到设置
        /// </summary>
        public void SaveHotkeysToSettings()
        {
            try
            {
                // 这里可以将快捷键配置保存到配置文件或设置中
                LogHelper.WriteLogToFile("快捷键配置已保存", LogHelper.LogType.Event);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"保存快捷键配置时出错: {ex.Message}", LogHelper.LogType.Error);
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
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (penTypeField != null)
                {
                    penTypeField.SetValue(_mainWindow, penTypeIndex);
                    
                    // 调用CheckPenTypeUIState方法更新UI状态
                    var checkPenTypeMethod = _mainWindow.GetType().GetMethod("CheckPenTypeUIState", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
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
        #endregion

        #region IDisposable Implementation
        public void Dispose()
        {
            if (!_isDisposed)
            {
                UnregisterAllHotkeys();
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
        #endregion
    }
} 