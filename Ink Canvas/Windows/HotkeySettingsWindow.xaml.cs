using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using Ink_Canvas.Helpers;

namespace Ink_Canvas.Windows
{
    /// <summary>
    /// 快捷键设置窗口
    /// </summary>
    public partial class HotkeySettingsWindow : Window
    {
        #region Private Fields
        private readonly MainWindow _mainWindow;
        private readonly GlobalHotkeyManager _hotkeyManager;
        private readonly Dictionary<string, HotkeyItem> _hotkeyItems;
        #endregion

        #region Constructor
        public HotkeySettingsWindow(MainWindow mainWindow, GlobalHotkeyManager hotkeyManager)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            _hotkeyManager = hotkeyManager;
            _hotkeyItems = new Dictionary<string, HotkeyItem>();

            // 隐藏主窗口的设置页面
            HideMainWindowSettings();
            InitializeHotkeyItems();
            
            // 延迟加载快捷键，确保快捷键管理器已完全初始化
            Loaded += (s, e) => 
            {
                try
                {
                    // 不启用快捷键注册功能，只读取配置文件中的快捷键信息用于显示
                    // 这样用户可以看到配置文件中保存的快捷键，但不会自动注册
                    
                    // 加载当前快捷键（包括配置文件中的）
                    LoadCurrentHotkeys();
                    SetupEventHandlers();
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"快捷键设置窗口初始化时出错: {ex.Message}", LogHelper.LogType.Error);
                }
            };

            // 注册窗口关闭事件
            Closed += HotkeySettingsWindow_Closed;
        }
        #endregion

        #region Private Methods
        private void InitializeHotkeyItems()
        {
            try
            {
                LogHelper.WriteLogToFile("开始初始化快捷键项");
                
                // 初始化快捷键项并设置HotkeyName
                _hotkeyItems["Undo"] = UndoHotkey;
                UndoHotkey.HotkeyName = "Undo";
                
                _hotkeyItems["Redo"] = RedoHotkey;
                RedoHotkey.HotkeyName = "Redo";
                
                _hotkeyItems["Clear"] = ClearHotkey;
                ClearHotkey.HotkeyName = "Clear";
                
                _hotkeyItems["Paste"] = PasteHotkey;
                PasteHotkey.HotkeyName = "Paste";
                
                _hotkeyItems["SelectTool"] = SelectToolHotkey;
                SelectToolHotkey.HotkeyName = "SelectTool";
                
                _hotkeyItems["DrawTool"] = DrawToolHotkey;
                DrawToolHotkey.HotkeyName = "DrawTool";
                
                _hotkeyItems["EraserTool"] = EraserToolHotkey;
                EraserToolHotkey.HotkeyName = "EraserTool";
                
                _hotkeyItems["BlackboardTool"] = BlackboardToolHotkey;
                BlackboardToolHotkey.HotkeyName = "BlackboardTool";
                
                _hotkeyItems["QuitDrawTool"] = QuitDrawToolHotkey;
                QuitDrawToolHotkey.HotkeyName = "QuitDrawTool";
                
                _hotkeyItems["Pen1"] = Pen1Hotkey;
                Pen1Hotkey.HotkeyName = "Pen1";
                
                _hotkeyItems["Pen2"] = Pen2Hotkey;
                Pen2Hotkey.HotkeyName = "Pen2";
                
                _hotkeyItems["Pen3"] = Pen3Hotkey;
                Pen3Hotkey.HotkeyName = "Pen3";
                
                _hotkeyItems["Pen4"] = Pen4Hotkey;
                Pen4Hotkey.HotkeyName = "Pen4";
                
                _hotkeyItems["Pen5"] = Pen5Hotkey;
                Pen5Hotkey.HotkeyName = "Pen5";
                
                _hotkeyItems["DrawLine"] = DrawLineHotkey;
                DrawLineHotkey.HotkeyName = "DrawLine";
                
                _hotkeyItems["Screenshot"] = ScreenshotHotkey;
                ScreenshotHotkey.HotkeyName = "Screenshot";
                
                _hotkeyItems["Hide"] = HideHotkey;
                HideHotkey.HotkeyName = "Hide";
                
                _hotkeyItems["Exit"] = ExitHotkey;
                ExitHotkey.HotkeyName = "Exit";
                
                LogHelper.WriteLogToFile($"成功初始化 {_hotkeyItems.Count} 个快捷键项");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"初始化快捷键项时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void LoadCurrentHotkeys()
        {
            try
            {
                // 首先尝试从配置文件获取快捷键信息
                var configHotkeys = _hotkeyManager.GetHotkeysFromConfigFile();
                LogHelper.WriteLogToFile($"配置文件中的快捷键数量: {configHotkeys.Count}");
                
                // 显示配置文件中的快捷键
                foreach (var hotkey in configHotkeys)
                {
                    if (_hotkeyItems.TryGetValue(hotkey.Name, out var hotkeyItem))
                    {
                        hotkeyItem.SetCurrentHotkey(hotkey.Key, hotkey.Modifiers);
                        LogHelper.WriteLogToFile($"从配置文件设置快捷键项: {hotkey.Name} -> {hotkey.Modifiers}+{hotkey.Key}");
                    }
                }
                
                // 为没有快捷键的项目设置默认显示值（仅用于UI显示，不实际注册）
                foreach (var kvp in _hotkeyItems)
                {
                    var hotkeyItem = kvp.Value;
                    if (hotkeyItem.GetCurrentHotkey().key == Key.None)
                    {
                        // 根据DefaultKey和DefaultModifiers设置默认显示值
                        SetDefaultHotkeyForItem(hotkeyItem);
                        LogHelper.WriteLogToFile($"设置默认显示值: {hotkeyItem.HotkeyName}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"加载当前快捷键时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 为快捷键项设置默认值
        /// </summary>
        private void SetDefaultHotkeyForItem(HotkeyItem hotkeyItem)
        {
            try
            {
                // 根据HotkeyName设置默认快捷键
                switch (hotkeyItem.HotkeyName)
                {
                    case "Undo":
                        hotkeyItem.SetCurrentHotkey(Key.Z, ModifierKeys.Control);
                        break;
                    case "Redo":
                        hotkeyItem.SetCurrentHotkey(Key.Y, ModifierKeys.Control);
                        break;
                    case "Clear":
                        hotkeyItem.SetCurrentHotkey(Key.E, ModifierKeys.Control);
                        break;
                    case "Paste":
                        hotkeyItem.SetCurrentHotkey(Key.V, ModifierKeys.Control);
                        break;
                    case "SelectTool":
                        hotkeyItem.SetCurrentHotkey(Key.S, ModifierKeys.Alt);
                        break;
                    case "DrawTool":
                        hotkeyItem.SetCurrentHotkey(Key.D, ModifierKeys.Alt);
                        break;
                    case "EraserTool":
                        hotkeyItem.SetCurrentHotkey(Key.E, ModifierKeys.Alt);
                        break;
                    case "BlackboardTool":
                        hotkeyItem.SetCurrentHotkey(Key.B, ModifierKeys.Alt);
                        break;
                    case "QuitDrawTool":
                        hotkeyItem.SetCurrentHotkey(Key.Q, ModifierKeys.Alt);
                        break;
                    case "Pen1":
                        hotkeyItem.SetCurrentHotkey(Key.D1, ModifierKeys.Alt);
                        break;
                    case "Pen2":
                        hotkeyItem.SetCurrentHotkey(Key.D2, ModifierKeys.Alt);
                        break;
                    case "Pen3":
                        hotkeyItem.SetCurrentHotkey(Key.D3, ModifierKeys.Alt);
                        break;
                    case "Pen4":
                        hotkeyItem.SetCurrentHotkey(Key.D4, ModifierKeys.Alt);
                        break;
                    case "Pen5":
                        hotkeyItem.SetCurrentHotkey(Key.D5, ModifierKeys.Alt);
                        break;
                    case "DrawLine":
                        hotkeyItem.SetCurrentHotkey(Key.L, ModifierKeys.Alt);
                        break;
                    case "Screenshot":
                        hotkeyItem.SetCurrentHotkey(Key.C, ModifierKeys.Alt);
                        break;
                    case "Hide":
                        hotkeyItem.SetCurrentHotkey(Key.V, ModifierKeys.Alt);
                        break;
                    case "Exit":
                        hotkeyItem.SetCurrentHotkey(Key.Escape, ModifierKeys.None);
                        break;
                }
            }
            catch (Exception)
            {
                // 设置默认快捷键时出错，忽略
            }
        }

        private void SetupEventHandlers()
        {
            // 为每个快捷键项设置事件处理器
            foreach (var hotkeyItem in _hotkeyItems.Values)
            {
                hotkeyItem.HotkeyChanged += OnHotkeyChanged;
            }
        }

        private void OnHotkeyChanged(object sender, HotkeyChangedEventArgs e)
        {
            try
            {
                LogHelper.WriteLogToFile($"收到快捷键变更事件: {e.HotkeyName} -> {e.Modifiers}+{e.Key}");
                
                // 检查快捷键冲突
                if (IsHotkeyConflict(e.Key, e.Modifiers, e.HotkeyName))
                {
                    MessageBox.Show($"快捷键 {e.Modifiers}+{e.Key} 已被其他功能使用，请选择其他组合。", 
                                  "快捷键冲突", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 更新快捷键管理器
                UpdateHotkeyInManager(e.HotkeyName, e.Key, e.Modifiers);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理快捷键变更时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private bool IsHotkeyConflict(Key key, ModifierKeys modifiers, string excludeHotkeyName)
        {
            // 检查是否与已注册的快捷键冲突
            var registeredHotkeys = _hotkeyManager.GetRegisteredHotkeys();
            foreach (var hotkey in registeredHotkeys)
            {
                if (hotkey.Name != excludeHotkeyName && 
                    hotkey.Key == key && 
                    hotkey.Modifiers == modifiers)
                {
                    return true;
                }
            }
            
            // 检查是否与默认快捷键冲突（如果当前快捷键项还没有注册）
            if (excludeHotkeyName != null && _hotkeyItems.TryGetValue(excludeHotkeyName, out var currentItem))
            {
                var currentHotkey = currentItem.GetCurrentHotkey();
                if (currentHotkey.key == Key.None)
                {
                    // 如果当前项还没有快捷键，检查是否与其他默认快捷键冲突
                    foreach (var kvp in _hotkeyItems)
                    {
                        if (kvp.Key != excludeHotkeyName)
                        {
                            var item = kvp.Value;
                            var itemHotkey = item.GetCurrentHotkey();
                            if (itemHotkey.key == key && itemHotkey.modifiers == modifiers)
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            
            return false;
        }

        private void UpdateHotkeyInManager(string hotkeyName, Key key, ModifierKeys modifiers)
        {
            try
            {
                LogHelper.WriteLogToFile($"开始更新快捷键: {hotkeyName} -> {modifiers}+{key}");
                
                // 先注销原有的快捷键（如果存在）
                _hotkeyManager.UnregisterHotkey(hotkeyName);
                LogHelper.WriteLogToFile($"已注销原有快捷键: {hotkeyName}");
                
                // 根据快捷键名称获取对应的动作
                var action = GetActionForHotkey(hotkeyName);
                if (action != null)
                {
                    LogHelper.WriteLogToFile($"找到快捷键动作: {hotkeyName}");
                    
                    // 直接注册新的快捷键
                    if (_hotkeyManager.RegisterHotkey(hotkeyName, key, modifiers, action))
                    {
                        LogHelper.WriteLogToFile($"成功注册新快捷键: {hotkeyName} -> {modifiers}+{key}");
                        
                        // 立即保存到配置文件
                        _hotkeyManager.SaveHotkeysToSettings();
                        LogHelper.WriteLogToFile($"已保存快捷键配置");
                        
                        // 更新UI显示
                        LoadCurrentHotkeys();
                        LogHelper.WriteLogToFile($"已更新UI显示");
                        
                        LogHelper.WriteLogToFile($"快捷键 {hotkeyName} 已更新为 {modifiers}+{key} 并保存", LogHelper.LogType.Event);
                    }
                    else
                    {
                        LogHelper.WriteLogToFile($"更新快捷键 {hotkeyName} 失败", LogHelper.LogType.Error);
                    }
                }
                else
                {
                    LogHelper.WriteLogToFile($"未找到快捷键 {hotkeyName} 对应的动作", LogHelper.LogType.Warning);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"更新快捷键管理器时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private Action GetActionForHotkey(string hotkeyName)
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
                    return () => _mainWindow.CursorIcon_Click(null, null);
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
                    return null;
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

        #region MainWindow Settings Management
        /// <summary>
        /// 隐藏主窗口的设置页面
        /// </summary>
        private void HideMainWindowSettings()
        {
            try
            {
                // 通过反射访问主窗口的设置面板
                var settingsBorder = _mainWindow.GetType().GetField("BorderSettings", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(_mainWindow) as System.Windows.Controls.Border;
                
                if (settingsBorder != null)
                {
                    settingsBorder.Visibility = Visibility.Collapsed;
                }

                // 隐藏设置蒙版
                var settingsMask = _mainWindow.GetType().GetField("BorderSettingsMask", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(_mainWindow) as System.Windows.Controls.Border;
                
                if (settingsMask != null)
                {
                    settingsMask.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"隐藏主窗口设置页面时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 显示主窗口的设置页面
        /// </summary>
        private void ShowMainWindowSettings()
        {
            try
            {
                // 通过反射访问主窗口的设置面板
                var settingsBorder = _mainWindow.GetType().GetField("BorderSettings", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(_mainWindow) as System.Windows.Controls.Border;
                
                if (settingsBorder != null)
                {
                    settingsBorder.Visibility = Visibility.Visible;
                }

                // 显示设置蒙版
                var settingsMask = _mainWindow.GetType().GetField("BorderSettingsMask", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(_mainWindow) as System.Windows.Controls.Border;
                
                if (settingsMask != null)
                {
                    settingsMask.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"显示主窗口设置页面时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }
        #endregion

        #region Window Event Handlers
        /// <summary>
        /// 窗口关闭事件处理
        /// </summary>
        private void HotkeySettingsWindow_Closed(object sender, EventArgs e)
        {
            // 恢复主窗口设置页面的显示
            ShowMainWindowSettings();
        }
        #endregion

        #region Event Handlers
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnResetToDefault_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show("确定要重置所有快捷键为默认设置吗？", 
                                           "确认重置", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    // 先注销所有现有快捷键
                    _hotkeyManager.UnregisterAllHotkeys();
                    
                    // 重置为默认快捷键
                    _hotkeyManager.RegisterDefaultHotkeys();
                    
                    // 立即保存到配置文件
                    _hotkeyManager.SaveHotkeysToSettings();
                    
                    // 更新UI显示
                    LoadCurrentHotkeys();
                    
                    MessageBox.Show("快捷键已重置为默认设置。", "重置完成", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"重置快捷键时出错: {ex.Message}", LogHelper.LogType.Error);
                MessageBox.Show($"重置快捷键时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 保存快捷键配置
                _hotkeyManager.SaveHotkeysToSettings();
                
                MessageBox.Show("快捷键设置已保存。", "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"保存快捷键设置时出错: {ex.Message}", LogHelper.LogType.Error);
                MessageBox.Show($"保存快捷键设置时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion
    }

    #region Hotkey Changed Event Args
    /// <summary>
    /// 快捷键变更事件参数
    /// </summary>
    public class HotkeyChangedEventArgs : EventArgs
    {
        public string HotkeyName { get; set; }
        public Key Key { get; set; }
        public ModifierKeys Modifiers { get; set; }
    }
    #endregion
} 