using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Ink_Canvas.Windows
{
    /// <summary>
    /// 快捷键项控件
    /// </summary>
    public partial class HotkeyItem : UserControl
    {
        #region Events
        /// <summary>
        /// 快捷键变更事件
        /// </summary>
        public event EventHandler<HotkeyChangedEventArgs> HotkeyChanged;
        #endregion

        #region Properties
        public string Title
        {
            get => TitleTextBlock.Text;
            set => TitleTextBlock.Text = value;
        }

        public string Description
        {
            get => DescriptionTextBlock.Text;
            set => DescriptionTextBlock.Text = value;
        }

        public string DefaultKey { get; set; }
        public string DefaultModifiers { get; set; }
        
        /// <summary>
        /// 快捷键名称（用于标识，如"Undo"）
        /// </summary>
        public string HotkeyName { get; set; }

        private Key _currentKey = Key.None;
        private ModifierKeys _currentModifiers = ModifierKeys.None;
        #endregion

        #region Constructor
        public HotkeyItem()
        {
            InitializeComponent();
            UpdateHotkeyDisplay();
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// 设置当前快捷键
        /// </summary>
        /// <param name="key">按键</param>
        /// <param name="modifiers">修饰键</param>
        public void SetCurrentHotkey(Key key, ModifierKeys modifiers)
        {
            _currentKey = key;
            _currentModifiers = modifiers;
            UpdateHotkeyDisplay();
        }

        /// <summary>
        /// 获取当前快捷键
        /// </summary>
        /// <returns>快捷键信息</returns>
        public (Key key, ModifierKeys modifiers) GetCurrentHotkey()
        {
            return (_currentKey, _currentModifiers);
        }
        #endregion

        #region Private Methods
        private void UpdateHotkeyDisplay()
        {
            if (_currentKey == Key.None)
            {
                CurrentHotkeyTextBlock.Text = "未设置";
                CurrentHotkeyTextBlock.Foreground = System.Windows.Media.Brushes.Gray;
            }
            else
            {
                var modifiersText = _currentModifiers == ModifierKeys.None ? "" : $"{_currentModifiers}+";
                CurrentHotkeyTextBlock.Text = $"{modifiersText}{_currentKey}";
                CurrentHotkeyTextBlock.Foreground = System.Windows.Media.Brushes.Black;
            }
        }

        private void StartHotkeyCapture()
        {
            BtnSetHotkey.Content = "请按键...";
            BtnSetHotkey.Background = System.Windows.Media.Brushes.Orange;
            
            // 设置焦点以捕获键盘事件
            Focus();
            
            // 添加键盘事件处理器
            KeyDown += HotkeyItem_KeyDown;
            KeyUp += HotkeyItem_KeyUp;
        }

        private void StopHotkeyCapture()
        {
            BtnSetHotkey.Content = "设置";
            BtnSetHotkey.Background = System.Windows.Media.Brushes.DodgerBlue;
            
            // 移除键盘事件处理器
            KeyDown -= HotkeyItem_KeyDown;
            KeyUp -= HotkeyItem_KeyUp;
        }

        private void HotkeyItem_KeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
            
            // 忽略某些特殊键
            if (e.Key == Key.LeftShift || e.Key == Key.RightShift ||
                e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
                e.Key == Key.LeftAlt || e.Key == Key.RightAlt ||
                e.Key == Key.LWin || e.Key == Key.RWin)
            {
                return;
            }

            // 获取修饰键
            var modifiers = ModifierKeys.None;
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                modifiers |= ModifierKeys.Control;
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                modifiers |= ModifierKeys.Shift;
            if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
                modifiers |= ModifierKeys.Alt;
            if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin))
                modifiers |= ModifierKeys.Windows;

            // 设置新的快捷键
            var oldKey = _currentKey;
            var oldModifiers = _currentModifiers;
            
            _currentKey = e.Key;
            _currentModifiers = modifiers;
            
            UpdateHotkeyDisplay();
            StopHotkeyCapture();

            // 触发快捷键变更事件
            HotkeyChanged?.Invoke(this, new HotkeyChangedEventArgs
            {
                HotkeyName = HotkeyName ?? Title, // 优先使用HotkeyName，如果没有则使用Title
                Key = _currentKey,
                Modifiers = _currentModifiers
            });
        }

        private void HotkeyItem_KeyUp(object sender, KeyEventArgs e)
        {
            e.Handled = true;
        }
        #endregion

        #region Event Handlers
        private void BtnSetHotkey_Click(object sender, RoutedEventArgs e)
        {
            StartHotkeyCapture();
        }
        #endregion
    }
} 