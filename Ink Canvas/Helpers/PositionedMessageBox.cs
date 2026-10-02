using System;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using ControlAppearance = Wpf.Ui.Controls.ControlAppearance;
using FluentWindow = Wpf.Ui.Controls.FluentWindow;
using SymbolIcon = Wpf.Ui.Controls.SymbolIcon;
using SymbolRegular = Wpf.Ui.Controls.SymbolRegular;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 可定位的消息框（基于 WPF-UI 的 FluentWindow）。
    ///
    /// 存在的理由：WPF-UI 与 Violeta 提供的 MessageBox 都是**静态类**（只能居中模态显示），
    /// 而 ICC-CE 的「批注中」提示气泡需要一个**可实例化**的窗口，支持：
    /// 指定屏幕坐标、非模态显示、主动 Close(result) 关闭、自动关闭计时。
    /// 这些能力在 WPF-UI/Violeta 中没有对应实现，故在此以 FluentWindow 为基础补齐。
    ///
    /// API 表面与项目原先使用的 iNKORE MessageBox 保持一致，调用方无需改动。
    /// </summary>
    public class PositionedMessageBox : FluentWindow
    {
        private MessageBoxResult _result = MessageBoxResult.None;
        private readonly TextBlock _messageText;
        private readonly SymbolIcon _iconPresenter;
        private readonly StackPanel _buttonPanel;

        public PositionedMessageBox()
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight;
            ShowInTaskbar = false;
            MinWidth = 280;
            MaxWidth = 460;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var root = new Grid { Margin = new Thickness(20, 16, 20, 16) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var contentRow = new Grid();
            contentRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            contentRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _iconPresenter = new SymbolIcon { FontSize = 26, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 0, 14, 0) };
            Grid.SetColumn(_iconPresenter, 0);

            _messageText = new TextBlock { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(_messageText, 1);

            contentRow.Children.Add(_iconPresenter);
            contentRow.Children.Add(_messageText);
            Grid.SetRow(contentRow, 0);
            root.Children.Add(contentRow);

            _buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 18, 0, 0)
            };
            Grid.SetRow(_buttonPanel, 1);
            root.Children.Add(_buttonPanel);

            Content = root;
            Loaded += (_, _) => BuildButtons();
        }

        /// <summary>最终结果；外部关闭（非按钮点击）时为 <see cref="MessageBoxResult.None"/>。</summary>
        public MessageBoxResult Result => _result;

        /// <summary>标题栏文字。</summary>
        public string Caption
        {
            get => Title;
            set => Title = value ?? string.Empty;
        }

        /// <summary>消息正文。</summary>
        public string MessageText
        {
            get => _messageText?.Text;
            set { if (_messageText != null) _messageText.Text = value ?? string.Empty; }
        }

        /// <summary>按钮组合（原生 <see cref="MessageBoxButton"/>）。</summary>
        public MessageBoxButton MessageBoxButtons { get; set; } = MessageBoxButton.OK;

        /// <summary>图标类型（原生 <see cref="MessageBoxImage"/>）。</summary>
        public MessageBoxImage MessageBoxImage { get; set; } = MessageBoxImage.None;

        /// <summary>窗口显示时播放的系统提示音（可为 null）。</summary>
        public SystemSound SystemSoundOnLoaded { get; set; }

        /// <summary>自定义主按钮文案（批注气泡的"保留"按钮）。</summary>
        public string PrimaryButtonText { get; set; }

        /// <summary>自定义次按钮文案（批注气泡的"退出"按钮）。</summary>
        public string SecondaryButtonText { get; set; }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            try { SystemSoundOnLoaded?.Play(); } catch { /* 提示音失败不影响弹窗 */ }
        }

        private void BuildButtons()
        {
            if (_buttonPanel == null) return;
            _buttonPanel.Children.Clear();

            void AddButton(MessageBoxResult result, string label, bool primary)
            {
                var button = new Wpf.Ui.Controls.Button
                {
                    Content = label,
                    MinWidth = 84,
                    Margin = new Thickness(8, 0, 0, 0),
                    Appearance = primary ? ControlAppearance.Primary : ControlAppearance.Secondary
                };
                button.Click += (_, _) => Close(result);
                _buttonPanel.Children.Add(button);
            }

            switch (MessageBoxButtons)
            {
                case MessageBoxButton.OK:
                    AddButton(MessageBoxResult.OK, "OK", true);
                    break;
                case MessageBoxButton.OKCancel:
                    AddButton(MessageBoxResult.OK, "OK", true);
                    AddButton(MessageBoxResult.Cancel, "Cancel", false);
                    break;
                case MessageBoxButton.YesNo:
                    AddButton(MessageBoxResult.Yes, PrimaryButtonText ?? "Yes", true);
                    AddButton(MessageBoxResult.No, SecondaryButtonText ?? "No", false);
                    break;
                case MessageBoxButton.YesNoCancel:
                    AddButton(MessageBoxResult.Yes, PrimaryButtonText ?? "Yes", true);
                    AddButton(MessageBoxResult.No, SecondaryButtonText ?? "No", false);
                    AddButton(MessageBoxResult.Cancel, "Cancel", false);
                    break;
            }
        }

        /// <summary>按 <see cref="MessageBoxImage"/> 设置图标。</summary>
        public void ApplyImage(MessageBoxImage image)
        {
            if (_iconPresenter == null) return;

            SymbolRegular? symbol = image switch
            {
                MessageBoxImage.Error => SymbolRegular.ErrorCircle24,
                MessageBoxImage.Information => SymbolRegular.Info24,
                MessageBoxImage.Warning => SymbolRegular.Warning24,
                MessageBoxImage.Question => SymbolRegular.QuestionCircle24,
                _ => null
            };

            if (symbol.HasValue)
            {
                _iconPresenter.Symbol = symbol.Value;
                _iconPresenter.Visibility = Visibility.Visible;
            }
            else
            {
                _iconPresenter.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>设置消息正文。</summary>
        public void SetMessage(string text)
        {
            MessageText = text;
        }

        /// <summary>主动关闭并记录结果。</summary>
        public void Close(MessageBoxResult result)
        {
            _result = result;
            Close();
        }
    }
}
