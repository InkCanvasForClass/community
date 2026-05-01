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
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(HotkeyItem),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty DescriptionProperty =
            DependencyProperty.Register(nameof(Description), typeof(string), typeof(HotkeyItem),
                new PropertyMetadata(string.Empty));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public string Description
        {
            get => (string)GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }

        public string DefaultKey { get; set; }
        public string DefaultModifiers { get; set; }

        /// <summary>
        /// 快捷键名称（用于标识，如"Undo"）
        /// </summary>
        public string HotkeyName { get; set; }

        /// <summary>
        /// 快捷键变更事件
        /// </summary>
        public event EventHandler<HotkeyChangedEventArgs> HotkeyChanged;

        private Key _currentKey = Key.None;
        private ModifierKeys _currentModifiers = ModifierKeys.None;

        public HotkeyItem()
        {
            InitializeComponent();
            UpdateHotkeyDisplay();
        }

        public void SetCurrentHotkey(Key key, ModifierKeys modifiers)
        {
            _currentKey = key;
            _currentModifiers = modifiers;
            UpdateHotkeyDisplay();
        }

        public (Key key, ModifierKeys modifiers) GetCurrentHotkey()
        {
            return (_currentKey, _currentModifiers);
        }

        private void UpdateHotkeyDisplay()
        {
            if (_currentKey == Key.None)
            {
                CurrentHotkeyTextBlock.Text = "未设置";
            }
            else
            {
                var modifiersText = _currentModifiers == ModifierKeys.None ? "" : $"{_currentModifiers}+";
                CurrentHotkeyTextBlock.Text = $"{modifiersText}{_currentKey}";
            }
        }

        private static HotkeyItem _activeCaptureItem;

        private void StartHotkeyCapture()
        {
            if (_activeCaptureItem != null && _activeCaptureItem != this)
            {
                _activeCaptureItem.StopHotkeyCapture();
            }
            _activeCaptureItem = this;

            CurrentHotkeyTextBlock.Text = "请按键...";
            Focus();
            KeyDown += HotkeyItem_KeyDown;
            KeyUp += HotkeyItem_KeyUp;
            LostFocus += HotkeyItem_LostFocus;
        }

        private void StopHotkeyCapture()
        {
            UpdateHotkeyDisplay();
            KeyDown -= HotkeyItem_KeyDown;
            KeyUp -= HotkeyItem_KeyUp;
            LostFocus -= HotkeyItem_LostFocus;
            if (_activeCaptureItem == this)
            {
                _activeCaptureItem = null;
            }
        }

        private void HotkeyItem_LostFocus(object sender, RoutedEventArgs e)
        {
            StopHotkeyCapture();
        }

        private void HotkeyItem_KeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;

            if (e.Key == Key.LeftShift || e.Key == Key.RightShift ||
                e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
                e.Key == Key.LeftAlt || e.Key == Key.RightAlt ||
                e.Key == Key.LWin || e.Key == Key.RWin)
            {
                return;
            }

            var modifiers = ModifierKeys.None;
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                modifiers |= ModifierKeys.Control;
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                modifiers |= ModifierKeys.Shift;
            if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
                modifiers |= ModifierKeys.Alt;
            if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin))
                modifiers |= ModifierKeys.Windows;

            _currentKey = e.Key;
            _currentModifiers = modifiers;

            UpdateHotkeyDisplay();
            StopHotkeyCapture();

            HotkeyChanged?.Invoke(this, new HotkeyChangedEventArgs
            {
                HotkeyName = HotkeyName ?? Title,
                Key = _currentKey,
                Modifiers = _currentModifiers
            });
        }

        private void HotkeyItem_KeyUp(object sender, KeyEventArgs e)
        {
            e.Handled = true;
        }

        private void BtnSetHotkey_Click(object sender, RoutedEventArgs e)
        {
            StartHotkeyCapture();
        }
    }

    /// <summary>
    /// 快捷键变更事件参数
    /// </summary>
    public class HotkeyChangedEventArgs : EventArgs
    {
        public string HotkeyName { get; set; }
        public Key Key { get; set; }
        public ModifierKeys Modifiers { get; set; }
    }
}