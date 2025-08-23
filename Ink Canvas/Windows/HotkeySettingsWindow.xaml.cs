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
            LoadCurrentHotkeys();
            SetupEventHandlers();

            // 注册窗口关闭事件
            this.Closed += HotkeySettingsWindow_Closed;
        }
        #endregion

        #region Private Methods
        private void InitializeHotkeyItems()
        {
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
        }

        private void LoadCurrentHotkeys()
        {
            try
            {
                var registeredHotkeys = _hotkeyManager.GetRegisteredHotkeys();
                foreach (var hotkey in registeredHotkeys)
                {
                    if (_hotkeyItems.TryGetValue(hotkey.Name, out var hotkeyItem))
                    {
                        hotkeyItem.SetCurrentHotkey(hotkey.Key, hotkey.Modifiers);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"加载当前快捷键时出错: {ex.Message}", LogHelper.LogType.Error);
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
            return false;
        }

        private void UpdateHotkeyInManager(string hotkeyName, Key key, ModifierKeys modifiers)
        {
            try
            {
                // 根据快捷键名称获取对应的动作
                var action = GetActionForHotkey(hotkeyName);
                if (action != null)
                {
                    // 使用快捷键管理器的UpdateHotkey方法，这会自动保存配置
                    if (_hotkeyManager.UpdateHotkey(hotkeyName, key, modifiers))
                    {
                        LogHelper.WriteLogToFile($"快捷键 {hotkeyName} 已更新为 {modifiers}+{key} 并自动保存", LogHelper.LogType.Event);
                    }
                    else
                    {
                        LogHelper.WriteLogToFile($"更新快捷键 {hotkeyName} 失败", LogHelper.LogType.Error);
                    }
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
                    settingsBorder.Visibility = System.Windows.Visibility.Collapsed;
                }

                // 隐藏设置蒙版
                var settingsMask = _mainWindow.GetType().GetField("BorderSettingsMask", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(_mainWindow) as System.Windows.Controls.Border;
                
                if (settingsMask != null)
                {
                    settingsMask.Visibility = System.Windows.Visibility.Collapsed;
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
                    settingsBorder.Visibility = System.Windows.Visibility.Visible;
                }

                // 显示设置蒙版
                var settingsMask = _mainWindow.GetType().GetField("BorderSettingsMask", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(_mainWindow) as System.Windows.Controls.Border;
                
                if (settingsMask != null)
                {
                    settingsMask.Visibility = System.Windows.Visibility.Visible;
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
                    // 重置为默认快捷键
                    _hotkeyManager.RegisterDefaultHotkeys();
                    
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