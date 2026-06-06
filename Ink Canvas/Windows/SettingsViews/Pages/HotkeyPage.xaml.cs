using Ink_Canvas.Helpers;
using Ink_Canvas.Properties;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class HotkeyPage : Page
    {
        private bool _isLoaded;
        private readonly Dictionary<string, HotkeyItem> _hotkeyItems = new Dictionary<string, HotkeyItem>();
        private GlobalHotkeyManager _hotkeyManager;
        private MainWindow _mainWindow;

        public HotkeyPage()
        {
            InitializeComponent();
            Loaded += HotkeyPage_Loaded;
            Unloaded += HotkeyPage_Unloaded;
        }

        private void HotkeyPage_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _mainWindow = Application.Current.MainWindow as MainWindow;
                _hotkeyManager = GetHotkeyManager(_mainWindow);

                InitializeHotkeyItems();
                LoadCurrentHotkeys();
                SetupEventHandlers();
                LoadMouseModeSetting();
                _isLoaded = true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"快捷键页面初始化时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void HotkeyPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = false;
            foreach (var item in _hotkeyItems.Values)
            {
                item.HotkeyChanged -= OnHotkeyChanged;
            }
            _hotkeyItems.Clear();
        }

        private static GlobalHotkeyManager GetHotkeyManager(MainWindow mw)
        {
            if (mw == null) return null;
            var field = typeof(MainWindow).GetField("_globalHotkeyManager",
                BindingFlags.NonPublic | BindingFlags.Instance);
            return field?.GetValue(mw) as GlobalHotkeyManager;
        }

        private void InitializeHotkeyItems()
        {
            _hotkeyItems["Undo"] = UndoHotkey; UndoHotkey.HotkeyName = "Undo";
            _hotkeyItems["Redo"] = RedoHotkey; RedoHotkey.HotkeyName = "Redo";
            _hotkeyItems["Clear"] = ClearHotkey; ClearHotkey.HotkeyName = "Clear";
            _hotkeyItems["Paste"] = PasteHotkey; PasteHotkey.HotkeyName = "Paste";
            _hotkeyItems["SelectTool"] = SelectToolHotkey; SelectToolHotkey.HotkeyName = "SelectTool";
            _hotkeyItems["DrawTool"] = DrawToolHotkey; DrawToolHotkey.HotkeyName = "DrawTool";
            _hotkeyItems["EraserTool"] = EraserToolHotkey; EraserToolHotkey.HotkeyName = "EraserTool";
            _hotkeyItems["BlackboardTool"] = BlackboardToolHotkey; BlackboardToolHotkey.HotkeyName = "BlackboardTool";
            _hotkeyItems["QuitDrawTool"] = QuitDrawToolHotkey; QuitDrawToolHotkey.HotkeyName = "QuitDrawTool";
            _hotkeyItems["Pen1"] = Pen1Hotkey; Pen1Hotkey.HotkeyName = "Pen1";
            _hotkeyItems["Pen2"] = Pen2Hotkey; Pen2Hotkey.HotkeyName = "Pen2";
            _hotkeyItems["Pen3"] = Pen3Hotkey; Pen3Hotkey.HotkeyName = "Pen3";
            _hotkeyItems["Pen4"] = Pen4Hotkey; Pen4Hotkey.HotkeyName = "Pen4";
            _hotkeyItems["Pen5"] = Pen5Hotkey; Pen5Hotkey.HotkeyName = "Pen5";
            _hotkeyItems["DrawLine"] = DrawLineHotkey; DrawLineHotkey.HotkeyName = "DrawLine";
            _hotkeyItems["Screenshot"] = ScreenshotHotkey; ScreenshotHotkey.HotkeyName = "Screenshot";
            _hotkeyItems["QuickDraw"] = QuickDrawHotkey; QuickDrawHotkey.HotkeyName = "QuickDraw";
            _hotkeyItems["Hide"] = HideHotkey; HideHotkey.HotkeyName = "Hide";
            _hotkeyItems["Exit"] = ExitHotkey; ExitHotkey.HotkeyName = "Exit";
        }

        private void LoadCurrentHotkeys()
        {
            try
            {
                foreach (var hotkeyItem in _hotkeyItems.Values)
                {
                    hotkeyItem.SetCurrentHotkey(Key.None, ModifierKeys.None);
                }

                if (_hotkeyManager != null)
                {
                    var configHotkeys = _hotkeyManager.GetHotkeysFromConfigFile();
                    foreach (var hotkey in configHotkeys)
                    {
                        if (_hotkeyItems.TryGetValue(hotkey.Name, out var hotkeyItem))
                        {
                            hotkeyItem.SetCurrentHotkey(hotkey.Key, hotkey.Modifiers);
                        }
                    }
                }

                foreach (var kvp in _hotkeyItems)
                {
                    var hotkeyItem = kvp.Value;
                    if (hotkeyItem.GetCurrentHotkey().key == Key.None)
                    {
                        SetDefaultHotkeyForItem(hotkeyItem);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"加载当前快捷键时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private static void SetDefaultHotkeyForItem(HotkeyItem hotkeyItem)
        {
            switch (hotkeyItem.HotkeyName)
            {
                case "Undo": hotkeyItem.SetCurrentHotkey(Key.Z, ModifierKeys.Control); break;
                case "Redo": hotkeyItem.SetCurrentHotkey(Key.Y, ModifierKeys.Control); break;
                case "Clear": hotkeyItem.SetCurrentHotkey(Key.E, ModifierKeys.Control); break;
                case "Paste": hotkeyItem.SetCurrentHotkey(Key.V, ModifierKeys.Control); break;
                case "SelectTool": hotkeyItem.SetCurrentHotkey(Key.S, ModifierKeys.Alt); break;
                case "DrawTool": hotkeyItem.SetCurrentHotkey(Key.D, ModifierKeys.Alt); break;
                case "EraserTool": hotkeyItem.SetCurrentHotkey(Key.E, ModifierKeys.Alt); break;
                case "BlackboardTool": hotkeyItem.SetCurrentHotkey(Key.B, ModifierKeys.Alt); break;
                case "QuitDrawTool": hotkeyItem.SetCurrentHotkey(Key.Q, ModifierKeys.Alt); break;
                case "Pen1": hotkeyItem.SetCurrentHotkey(Key.D1, ModifierKeys.Alt); break;
                case "Pen2": hotkeyItem.SetCurrentHotkey(Key.D2, ModifierKeys.Alt); break;
                case "Pen3": hotkeyItem.SetCurrentHotkey(Key.D3, ModifierKeys.Alt); break;
                case "Pen4": hotkeyItem.SetCurrentHotkey(Key.D4, ModifierKeys.Alt); break;
                case "Pen5": hotkeyItem.SetCurrentHotkey(Key.D5, ModifierKeys.Alt); break;
                case "DrawLine": hotkeyItem.SetCurrentHotkey(Key.L, ModifierKeys.Alt); break;
                case "Screenshot": hotkeyItem.SetCurrentHotkey(Key.C, ModifierKeys.Alt); break;
                case "QuickDraw": hotkeyItem.SetCurrentHotkey(Key.K, ModifierKeys.Alt); break;
                case "Hide": hotkeyItem.SetCurrentHotkey(Key.V, ModifierKeys.Alt); break;
                case "Exit": hotkeyItem.SetCurrentHotkey(Key.Escape, ModifierKeys.None); break;
            }
        }

        private void SetupEventHandlers()
        {
            foreach (var hotkeyItem in _hotkeyItems.Values)
            {
                hotkeyItem.HotkeyChanged += OnHotkeyChanged;
            }
        }

        private void LoadMouseModeSetting()
        {
            CardEnableHotkeysInMouseMode.IsOn = SettingsManager.Settings.Appearance.EnableHotkeysInMouseMode;
        }

        private void ToggleSwitchEnableHotkeysInMouseMode_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            try
            {
                bool newState = CardEnableHotkeysInMouseMode.IsOn;
                SettingsManager.Settings.Appearance.EnableHotkeysInMouseMode = newState;
                SettingsManager.SaveSettingsToFile();

                if (_hotkeyManager != null && _mainWindow != null)
                {
                    bool isCurrentlyMouseMode = _mainWindow.inkCanvas.EditingMode == InkCanvasEditingMode.None;
                    if (isCurrentlyMouseMode && !newState)
                    {
                        _hotkeyManager.DisableHotkeyRegistration();
                    }
                    else
                    {
                        _hotkeyManager.UpdateHotkeyStateForToolMode(isCurrentlyMouseMode);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"更新鼠标模式快捷键设置时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void OnHotkeyChanged(object sender, HotkeyChangedEventArgs e)
        {
            try
            {
                if (_hotkeyManager == null)
                {
                    MessageBox.Show(HotkeyStrings.Hotkey_ManagerNotInit, HotkeyStrings.Hotkey_Error,
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (IsHotkeyConflict(e.Key, e.Modifiers, e.HotkeyName))
                {
                    MessageBox.Show(string.Format(HotkeyStrings.Hotkey_ConflictMessage, $"{e.Modifiers}+{e.Key}"),
                        HotkeyStrings.Hotkey_ConflictTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

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

            if (excludeHotkeyName != null && _hotkeyItems.TryGetValue(excludeHotkeyName, out var currentItem))
            {
                var currentHotkey = currentItem.GetCurrentHotkey();
                if (currentHotkey.key == Key.None)
                {
                    foreach (var kvp in _hotkeyItems)
                    {
                        if (kvp.Key != excludeHotkeyName)
                        {
                            var itemHotkey = kvp.Value.GetCurrentHotkey();
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
                _hotkeyManager.UnregisterHotkey(hotkeyName);

                var action = GetActionForHotkey(hotkeyName);
                if (action == null)
                {
                    LogHelper.WriteLogToFile($"未找到快捷键 {hotkeyName} 对应的动作", LogHelper.LogType.Warning);
                    return;
                }

                if (_hotkeyManager.RegisterHotkey(hotkeyName, key, modifiers, action))
                {
                    _hotkeyManager.SaveHotkeysToSettings();
                    LoadCurrentHotkeys();
                    LogHelper.WriteLogToFile($"快捷键 {hotkeyName} 已更新为 {modifiers}+{key} 并保存", LogHelper.LogType.Event);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"更新快捷键管理器时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private Action GetActionForHotkey(string hotkeyName)
        {
            if (_mainWindow == null) return null;
            switch (hotkeyName)
            {
                case "Undo": return () => _mainWindow.SymbolIconUndo_MouseUp(null, null);
                case "Redo": return () => _mainWindow.SymbolIconRedo_MouseUp(null, null);
                case "Clear": return () => _mainWindow.SymbolIconDelete_MouseUp(null, null);
                case "Paste": return () => _mainWindow.HandleGlobalPaste(null, null);
                case "SelectTool": return () => _mainWindow.SwitchToSelectFromHotkey();
                case "DrawTool": return () => _mainWindow.PenIcon_Click(null, null);
                case "EraserTool": return () => _mainWindow.SwitchToEraserFromHotkey();
                case "BlackboardTool": return () => _mainWindow.ImageBlackboard_MouseUp(null, null);
                case "QuitDrawTool": return () => _mainWindow.CursorIcon_Click(null, null);
                case "Pen1": return () => SwitchToPenType(0);
                case "Pen2": return () => SwitchToPenType(1);
                case "Pen3": return () => SwitchToPenType(2);
                case "Pen4": return () => SwitchToPenType(3);
                case "Pen5": return () => SwitchToPenType(4);
                case "DrawLine": return () => _mainWindow.DrawLineFromHotkey();
                case "Screenshot": return () => _mainWindow.SaveScreenShotToDesktop();
                case "QuickDraw": return () => _mainWindow.OpenQuickDrawFromHotkey();
                case "Hide": return () => _mainWindow.SymbolIconEmoji_MouseUp(null, null);
                case "Exit": return () => _mainWindow.KeyExit(null, null);
                default: return null;
            }
        }

        private void SwitchToPenType(int penTypeIndex)
        {
            try
            {
                if (_mainWindow == null) return;
                var switchMethod = _mainWindow.GetType().GetMethod(
                    penTypeIndex == 2 ? "SwitchToLaserPen" : (penTypeIndex == 1 ? "SwitchToHighlighterPen" : "SwitchToDefaultPen"),
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (switchMethod != null)
                {
                    switchMethod.Invoke(_mainWindow, new object[] { null, null });
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"切换到笔类型{penTypeIndex}时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void BtnResetToDefault_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_hotkeyManager == null) return;

                var result = MessageBox.Show(HotkeyStrings.Hotkey_ConfirmResetMessage, HotkeyStrings.Hotkey_ConfirmResetTitle,
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes) return;

                _hotkeyManager.UnregisterAllHotkeys();
                _hotkeyManager.RegisterDefaultHotkeys();
                _hotkeyManager.SaveHotkeysToSettings();
                LoadCurrentHotkeys();

                MessageBox.Show(HotkeyStrings.Hotkey_ResetCompleteMessage, HotkeyStrings.Hotkey_ResetCompleteTitle,
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"重置快捷键时出错: {ex.Message}", LogHelper.LogType.Error);
                MessageBox.Show(string.Format(HotkeyStrings.Hotkey_ResetErrorMessage, ex.Message), HotkeyStrings.Hotkey_Error,
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_hotkeyManager == null) return;
                _hotkeyManager.SaveHotkeysToSettings();
                MessageBox.Show(HotkeyStrings.Hotkey_SaveSuccessMessage, HotkeyStrings.Hotkey_SaveSuccessTitle,
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"保存快捷键设置时出错: {ex.Message}", LogHelper.LogType.Error);
                MessageBox.Show(string.Format(HotkeyStrings.Hotkey_SaveErrorMessage, ex.Message), HotkeyStrings.Hotkey_Error,
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}