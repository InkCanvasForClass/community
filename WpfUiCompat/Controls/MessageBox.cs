using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using WpfButton = Wpf.Ui.Controls.Button;
using WpfFluentWindow = Wpf.Ui.Controls.FluentWindow;
using WpfControlAppearance = Wpf.Ui.Controls.ControlAppearance;
using WpfWindowBackdropType = Wpf.Ui.Controls.WindowBackdropType;

namespace WpfUiCompat.Controls
{
    /// <summary>
    /// 兼容 iNKORE MessageBox.Show 静态 API 的消息框。基于 WPF-UI 控件样式，
    /// 同步阻塞（ShowDialog）并返回 <see cref="System.Windows.MessageBoxResult"/>。
    /// </summary>
    public class MessageBox : WpfFluentWindow
    {
        private System.Windows.MessageBoxResult _result = System.Windows.MessageBoxResult.None;
        private System.Windows.MessageBoxImage _icon = System.Windows.MessageBoxImage.None;

        internal MessageBox()
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight;
            ShowInTaskbar = false;
            MinWidth = 320;
            MaxWidth = 480;
            WindowBackdropType = WpfWindowBackdropType.None;

            BuildContent();
            SourceInitialized += (s, e) =>
            {
                var dark = ThemeManager.GetActualTheme(this) == ElementTheme.Dark;
                Helpers.BackdropHelper.ApplyDarkMode(this);
                if (!dark) Helpers.BackdropHelper.RemoveDarkMode(this);
            };
        }

        internal System.Windows.MessageBoxResult Result => _result;

        internal System.Windows.MessageBoxImage IconType {
            get => _icon;
            set
            {
                _icon = value;
                if (_iconPresenter != null)
                {
                    _iconPresenter.Icon = _icon switch
                    {
                        System.Windows.MessageBoxImage.Information => Common.IconKeys.SegoeFluentIcons.Info,
                        System.Windows.MessageBoxImage.Warning => Common.IconKeys.SegoeFluentIcons.Warning,
                        System.Windows.MessageBoxImage.Error => Common.IconKeys.SegoeFluentIcons.Error,
                        System.Windows.MessageBoxImage.Question => Common.IconKeys.SegoeFluentIcons.Help,
                        _ => null
                    };
                    _iconPresenter.Visibility = _icon == System.Windows.MessageBoxImage.None ? Visibility.Collapsed : Visibility.Visible;
                }
            }
        }

        private FontIcon _iconPresenter;
        private TextBlock _messageText;

        private void BuildContent()
        {
            var root = new Grid { Margin = new Thickness(24, 20, 24, 20) };
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var contentRow = new Grid();
            contentRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            contentRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _iconPresenter = new FontIcon { FontSize = 28, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 0, 16, 0) };
            _iconPresenter.SetResourceReference(System.Windows.Controls.Control.ForegroundProperty, "AccentFillColorDefaultBrush");

            _messageText = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Top
            };
            _messageText.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
            _messageText.SetResourceReference(TextBlock.FontSizeProperty, "ControlContentThemeFontSize");

            Grid.SetColumn(_iconPresenter, 0);
            Grid.SetColumn(_messageText, 1);
            contentRow.Children.Add(_iconPresenter);
            contentRow.Children.Add(_messageText);

            Grid.SetRow(contentRow, 0);
            root.Children.Add(contentRow);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 24, 0, 0)
            };
            Grid.SetRow(buttonPanel, 1);
            root.Children.Add(buttonPanel);

            Content = root;
        }

        internal void SetContent(string text, System.Windows.MessageBoxButton buttons, System.Windows.MessageBoxResult defaultResult)
        {
            _messageText.Text = text ?? string.Empty;

            var panel = (StackPanel)((Grid)Content).Children[1];
            panel.Children.Clear();

            void AddButton(System.Windows.MessageBoxResult result, string label, bool isDefault)
            {
                var button = new WpfButton
                {
                    Content = label,
                    MinWidth = 100,
                    Margin = new Thickness(8, 0, 0, 0),
                    Appearance = isDefault ? WpfControlAppearance.Primary : WpfControlAppearance.Secondary
                };
                if (isDefault)
                {
                    button.Loaded += (s, e) => button.Focus();
                }
                button.Click += (s, e) =>
                {
                    _result = result;
                    DialogResult = true;
                };
                panel.Children.Add(button);
            }

            switch (buttons)
            {
                case System.Windows.MessageBoxButton.OK:
                    AddButton(System.Windows.MessageBoxResult.OK, "OK", defaultResult == System.Windows.MessageBoxResult.OK || defaultResult == System.Windows.MessageBoxResult.None);
                    break;
                case System.Windows.MessageBoxButton.OKCancel:
                    AddButton(System.Windows.MessageBoxResult.OK, "OK", defaultResult == System.Windows.MessageBoxResult.OK || defaultResult == System.Windows.MessageBoxResult.None);
                    AddButton(System.Windows.MessageBoxResult.Cancel, "Cancel", defaultResult == System.Windows.MessageBoxResult.Cancel);
                    break;
                case System.Windows.MessageBoxButton.YesNo:
                    AddButton(System.Windows.MessageBoxResult.Yes, "Yes", defaultResult == System.Windows.MessageBoxResult.Yes || defaultResult == System.Windows.MessageBoxResult.None);
                    AddButton(System.Windows.MessageBoxResult.No, "No", defaultResult == System.Windows.MessageBoxResult.No);
                    break;
                case System.Windows.MessageBoxButton.YesNoCancel:
                    AddButton(System.Windows.MessageBoxResult.Yes, "Yes", defaultResult == System.Windows.MessageBoxResult.Yes || defaultResult == System.Windows.MessageBoxResult.None);
                    AddButton(System.Windows.MessageBoxResult.No, "No", defaultResult == System.Windows.MessageBoxResult.No);
                    AddButton(System.Windows.MessageBoxResult.Cancel, "Cancel", defaultResult == System.Windows.MessageBoxResult.Cancel);
                    break;
            }

            if (buttons == System.Windows.MessageBoxButton.OKCancel || buttons == System.Windows.MessageBoxButton.YesNoCancel)
            {
                Closing += (s, e) =>
                {
                    if (DialogResult != true)
                    {
                        _result = System.Windows.MessageBoxResult.Cancel;
                    }
                };
            }
            else if (buttons == System.Windows.MessageBoxButton.YesNo)
            {
                Closing += (s, e) =>
                {
                    if (DialogResult != true)
                    {
                        _result = System.Windows.MessageBoxResult.No;
                    }
                };
            }
        }

        #region 静态 Show

        public static System.Windows.MessageBoxResult Show(string messageBoxText, string caption)
        {
            return Show(null, messageBoxText, caption, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.None, System.Windows.MessageBoxResult.None);
        }

        public static System.Windows.MessageBoxResult Show(string messageBoxText, string caption, System.Windows.MessageBoxButton button)
        {
            return Show(null, messageBoxText, caption, button, System.Windows.MessageBoxImage.None, System.Windows.MessageBoxResult.None);
        }

        public static System.Windows.MessageBoxResult Show(string messageBoxText, string caption, System.Windows.MessageBoxButton button, System.Windows.MessageBoxImage icon)
        {
            return Show(null, messageBoxText, caption, button, icon, System.Windows.MessageBoxResult.None);
        }

        public static System.Windows.MessageBoxResult Show(string messageBoxText, string caption, System.Windows.MessageBoxButton button, System.Windows.MessageBoxImage icon, System.Windows.MessageBoxResult defaultResult)
        {
            return Show(null, messageBoxText, caption, button, icon, defaultResult);
        }

        public static System.Windows.MessageBoxResult Show(Window owner, string messageBoxText, string caption, System.Windows.MessageBoxButton button, System.Windows.MessageBoxImage icon, System.Windows.MessageBoxResult defaultResult)
        {
            var messageBox = new MessageBox
            {
                Title = caption ?? string.Empty,
                IconType = icon
            };
            messageBox.SetContent(messageBoxText, button, defaultResult);

            if (owner != null && owner.IsLoaded)
            {
                messageBox.Owner = owner;
            }
            else
            {
                try
                {
                    var active = TryGetActiveWindow();
                    if (active != null && !Equals(active, messageBox))
                    {
                        messageBox.Owner = active;
                    }
                }
                catch { }
            }

            messageBox.ShowDialog();
            return messageBox._result;
        }

        private static Window TryGetActiveWindow()
        {
            if (Application.Current == null) return null;
            foreach (Window w in Application.Current.Windows)
            {
                if (w.IsActive)
                {
                    return w;
                }
            }
            return Application.Current.Windows.Count > 0 ? Application.Current.Windows[0] : null;
        }

        #endregion

                public static System.Windows.MessageBoxResult Show(string messageBoxText)
        {
            return Show(null, messageBoxText, string.Empty, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.None, System.Windows.MessageBoxResult.None);
        }

        public static System.Windows.MessageBoxResult Show(Window owner, string messageBoxText, string caption, System.Windows.MessageBoxButton button, System.Windows.MessageBoxImage icon)
        {
            return Show(owner, messageBoxText, caption, button, icon, System.Windows.MessageBoxResult.None);
        }

        public static System.Threading.Tasks.Task<System.Windows.MessageBoxResult> ShowAsync(
            string messageBoxText,
            string caption = "",
            System.Windows.MessageBoxButton button = System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage icon = System.Windows.MessageBoxImage.None,
            System.Windows.MessageBoxResult defaultResult = System.Windows.MessageBoxResult.None)
        {
            return System.Threading.Tasks.Task.FromResult(Show(null, messageBoxText, caption, button, icon, defaultResult));
        }

        public static System.Threading.Tasks.Task<System.Windows.MessageBoxResult> ShowAsync(
            Window owner,
            string messageBoxText,
            string caption = "",
            System.Windows.MessageBoxButton button = System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage icon = System.Windows.MessageBoxImage.None,
            System.Windows.MessageBoxResult defaultResult = System.Windows.MessageBoxResult.None)
        {
            return System.Threading.Tasks.Task.FromResult(Show(owner, messageBoxText, caption, button, icon, defaultResult));
        }
    }
}