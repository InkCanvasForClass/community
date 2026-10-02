using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace Ink_Canvas.Controls
{
    /// <summary>
    /// 设置卡片：迁移自 iNKORE SettingsCard，视觉基于 WPF-UI 的 Fluent 卡片语言。
    ///
    /// 结构：左侧图标（可选） + 标题/说明两行 + 右侧内容（对齐方式由 ContentAlignment 决定）。
    ///
    /// 之所以不用 WPF-UI 的 <see cref="CardControl"/>：它继承 ButtonBase（整卡可点击），
    /// 且没有 Header 下方的「说明文字」与「内容对齐」概念，而这两项在设置页被大量使用
    /// （Header 236 处、Description 44 处、ContentAlignment 16 处）。
    /// 继承 ButtonBase 还会让内部的 ComboBox/CheckBox 等交互控件难以正常工作。
    /// </summary>
    public class SettingsCard : ContentControl
    {
        static SettingsCard()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(SettingsCard), new FrameworkPropertyMetadata(typeof(SettingsCard)));
        }

        /// <summary>标题文字。</summary>
        public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
            nameof(Header), typeof(object), typeof(SettingsCard), new PropertyMetadata(null));

        public object Header
        {
            get => GetValue(HeaderProperty);
            set => SetValue(HeaderProperty, value);
        }

        /// <summary>标题下方的说明文字；为空时不占位。</summary>
        public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
            nameof(Description), typeof(string), typeof(SettingsCard), new PropertyMetadata(null));

        public string Description
        {
            get => (string)GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }

        /// <summary>左侧图标（IconElement：SymbolIcon / ImageIcon 等）。</summary>
        public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
            nameof(Icon), typeof(IconElement), typeof(SettingsCard), new PropertyMetadata(null));

        public IconElement Icon
        {
            get => (IconElement)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        /// <summary>兼容 iNKORE：图标以任意对象形式给出（HeaderIcon）。</summary>
        public static readonly DependencyProperty HeaderIconProperty = DependencyProperty.Register(
            nameof(HeaderIcon), typeof(object), typeof(SettingsCard), new PropertyMetadata(null, OnHeaderIconChanged));

        public object HeaderIcon
        {
            get => GetValue(HeaderIconProperty);
            set => SetValue(HeaderIconProperty, value);
        }

        private static void OnHeaderIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SettingsCard card && e.NewValue is IconElement iconElement)
            {
                card.Icon = iconElement;
            }
        }

        /// <summary>卡片右侧的快捷操作图标（兼容 iNKORE ActionIcon）。</summary>
        public static readonly DependencyProperty ActionIconProperty = DependencyProperty.Register(
            nameof(ActionIcon), typeof(IconElement), typeof(SettingsCard), new PropertyMetadata(null));

        public IconElement ActionIcon
        {
            get => (IconElement)GetValue(ActionIconProperty);
            set => SetValue(ActionIconProperty, value);
        }

        /// <summary>右侧内容的水平对齐方式（兼容 iNKORE ContentAlignment）。</summary>
        public static readonly DependencyProperty ContentAlignmentProperty = DependencyProperty.Register(
            nameof(ContentAlignment), typeof(HorizontalAlignment), typeof(SettingsCard),
            new PropertyMetadata(HorizontalAlignment.Right));

        public HorizontalAlignment ContentAlignment
        {
            get => (HorizontalAlignment)GetValue(ContentAlignmentProperty);
            set => SetValue(ContentAlignmentProperty, value);
        }

        /// <summary>卡片整体是否响应点击（兼容 iNKORE；为 false 时内容区仍可交互）。</summary>
        public static readonly DependencyProperty IsClickEnabledProperty = DependencyProperty.Register(
            nameof(IsClickEnabled), typeof(bool), typeof(SettingsCard), new PropertyMetadata(true));

        public bool IsClickEnabled
        {
            get => (bool)GetValue(IsClickEnabledProperty);
            set => SetValue(IsClickEnabledProperty, value);
        }

        /// <summary>卡片点击事件（兼容 iNKORE 的 Click）：整卡左键点击时触发。</summary>
        public static readonly RoutedEvent ClickEvent = EventManager.RegisterRoutedEvent(
            nameof(Click), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(SettingsCard));

        public event RoutedEventHandler Click
        {
            add => AddHandler(ClickEvent, value);
            remove => RemoveHandler(ClickEvent, value);
        }

        protected override void OnMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            // 内容区是可交互控件（ComboBox / CheckBox / TextBox 等）时，点击交给它们处理，
            // 卡片自身不触发 Click，避免点开下拉框同时又触发卡片动作。
            if (!IsClickEnabled || Content is System.Windows.Controls.Primitives.ButtonBase
                or System.Windows.Controls.Primitives.ToggleButton
                or System.Windows.Controls.Primitives.Selector
                or System.Windows.Controls.Primitives.TextBoxBase
                or ComboBox
                or Slider)
            {
                return;
            }

            RaiseEvent(new RoutedEventArgs(ClickEvent, this));
        }
    }
}