using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Controls.Primitives;

namespace WpfUiCompat.Controls
{
    /// <summary>
    /// 设置卡内容对齐方式（兼容 iNKORE 取值）。
    /// </summary>
    public enum SettingsCardContentAlignment
    {
        Left = 0,
        Right = 1,
        Vertical = 2,
    }

    /// <summary>
    /// 兼容 iNKORE SettingsCard 的设置卡片控件。视觉样式使用 WPF-UI 主题画笔，
    /// API（Header/Description/HeaderIcon/Content/ContentAlignment/IsClickEnabled）与 iNKORE 一致。
    /// </summary>
    [System.Windows.Markup.ContentProperty("Content")]
    public class SettingsCard : ButtonBase
    {
        static SettingsCard()
        {
            // 必须在静态构造器中重写元数据：OverrideMetadata 对同一类型只能调用一次，
            // 放在实例构造器里会导致第二个实例抛"元数据已注册"异常。
            DefaultStyleKeyProperty.OverrideMetadata(typeof(SettingsCard), new FrameworkPropertyMetadata(typeof(SettingsCard)));
        }

        public SettingsCard()
        {
        }

        #region Header

        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register(nameof(Header), typeof(object), typeof(SettingsCard), new PropertyMetadata(null));

        public object Header
        {
            get => GetValue(HeaderProperty);
            set => SetValue(HeaderProperty, value);
        }

        public static readonly DependencyProperty HeaderTemplateProperty =
            DependencyProperty.Register(nameof(HeaderTemplate), typeof(System.Windows.DataTemplate), typeof(SettingsCard), new PropertyMetadata(null));

        public System.Windows.DataTemplate HeaderTemplate
        {
            get => (System.Windows.DataTemplate)GetValue(HeaderTemplateProperty);
            set => SetValue(HeaderTemplateProperty, value);
        }

        #endregion

        #region Description

        public static readonly DependencyProperty DescriptionProperty =
            DependencyProperty.Register(nameof(Description), typeof(object), typeof(SettingsCard), new PropertyMetadata(null));

        public object Description
        {
            get => GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }

        #endregion

        #region HeaderIcon

        public static readonly DependencyProperty HeaderIconProperty =
            DependencyProperty.Register(nameof(HeaderIcon), typeof(object), typeof(SettingsCard), new PropertyMetadata(null));

        public object HeaderIcon
        {
            get => GetValue(HeaderIconProperty);
            set => SetValue(HeaderIconProperty, value);
        }

        #endregion

        #region ContentAlignment

        public static readonly DependencyProperty ContentAlignmentProperty =
            DependencyProperty.Register(nameof(ContentAlignment), typeof(SettingsCardContentAlignment), typeof(SettingsCard), new PropertyMetadata(SettingsCardContentAlignment.Left));

        public SettingsCardContentAlignment ContentAlignment
        {
            get => (SettingsCardContentAlignment)GetValue(ContentAlignmentProperty);
            set => SetValue(ContentAlignmentProperty, value);
        }

        #endregion


        #region ActionIcon

        public static readonly DependencyProperty ActionIconProperty =
            DependencyProperty.Register(nameof(ActionIcon), typeof(object), typeof(SettingsCard), new PropertyMetadata(null));

        public object ActionIcon
        {
            get => GetValue(ActionIconProperty);
            set => SetValue(ActionIconProperty, value);
        }

        #endregion
        #region IsClickEnabled

        public static readonly DependencyProperty IsClickEnabledProperty =
            DependencyProperty.Register(nameof(IsClickEnabled), typeof(bool), typeof(SettingsCard), new PropertyMetadata(false, OnIsClickEnabledChanged));

        public bool IsClickEnabled
        {
            get => (bool)GetValue(IsClickEnabledProperty);
            set => SetValue(IsClickEnabledProperty, value);
        }

        private static void OnIsClickEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SettingsCard card)
            {
                if (!(bool)e.NewValue)
                {
                    card.Focusable = false;
                }
            }
        }

        #endregion

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            if (IsClickEnabled)
            {
                base.OnMouseLeftButtonDown(e);
            }
            else
            {
                e.Handled = false;
            }
        }
    }

    /// <summary>
    /// 兼容 iNKORE SettingsExpander 的可展开设置卡片控件。
    /// Header/Description/HeaderIcon 与 SettingsCard 一致，Items 为展开区域内的子设置卡集合。
    /// </summary>
    [TemplatePart(Name = "PART_ExpanderToggle", Type = typeof(ToggleButton))]
    [System.Windows.Markup.ContentProperty("Content")]
    public class SettingsExpander : Control
    {
        static SettingsExpander()
        {
            // 必须在静态构造器中重写元数据（同 SettingsCard）。
            DefaultStyleKeyProperty.OverrideMetadata(typeof(SettingsExpander), new FrameworkPropertyMetadata(typeof(SettingsExpander)));
        }

        public SettingsExpander()
        {
            Items = new System.Collections.ObjectModel.ObservableCollection<object>();
        }

        #region Header / Description / HeaderIcon

        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register(nameof(Header), typeof(object), typeof(SettingsExpander), new PropertyMetadata(null));

        public object Header
        {
            get => GetValue(HeaderProperty);
            set => SetValue(HeaderProperty, value);
        }

        public static readonly DependencyProperty DescriptionProperty =
            DependencyProperty.Register(nameof(Description), typeof(object), typeof(SettingsExpander), new PropertyMetadata(null));

        public object Description
        {
            get => GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }

        public static readonly DependencyProperty HeaderIconProperty =
            DependencyProperty.Register(nameof(HeaderIcon), typeof(object), typeof(SettingsExpander), new PropertyMetadata(null));

        public object HeaderIcon
        {
            get => GetValue(HeaderIconProperty);
            set => SetValue(HeaderIconProperty, value);
        }

        #endregion

        #region Content

        public static readonly DependencyProperty ContentProperty =
            DependencyProperty.Register(nameof(Content), typeof(object), typeof(SettingsExpander), new PropertyMetadata(null));

        public object Content
        {
            get => GetValue(ContentProperty);
            set => SetValue(ContentProperty, value);
        }

        #endregion


        public static readonly DependencyProperty ItemTemplateProperty =
            DependencyProperty.Register(nameof(ItemTemplate), typeof(System.Windows.DataTemplate), typeof(SettingsExpander), new PropertyMetadata(null));

        public System.Windows.DataTemplate ItemTemplate
        {
            get => (System.Windows.DataTemplate)GetValue(ItemTemplateProperty);
            set => SetValue(ItemTemplateProperty, value);
        }
        #region Items

        public System.Collections.ObjectModel.ObservableCollection<object> Items { get; }

        private static readonly DependencyPropertyKey ItemsHostPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(ItemsHost), typeof(object), typeof(SettingsExpander), new PropertyMetadata(null));

        public static readonly DependencyProperty ItemsHostProperty = ItemsHostPropertyKey.DependencyProperty;

        public object ItemsHost
        {
            get => GetValue(ItemsHostProperty);
            private set => SetValue(ItemsHostPropertyKey, value);
        }

        #endregion

        #region IsExpanded

        public static readonly DependencyProperty IsExpandedProperty =
            DependencyProperty.Register(nameof(IsExpanded), typeof(bool), typeof(SettingsExpander), new PropertyMetadata(false));

        public bool IsExpanded
        {
            get => (bool)GetValue(IsExpandedProperty);
            set => SetValue(IsExpandedProperty, value);
        }

        #endregion


        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(System.Collections.IEnumerable), typeof(SettingsExpander), new PropertyMetadata(null, OnItemsSourceChanged));

        public System.Collections.IEnumerable ItemsSource
        {
            get => (System.Collections.IEnumerable)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SettingsExpander expander && expander.ItemsHost is System.Windows.Controls.ItemsControl itemsControl)
            {
                itemsControl.ItemsSource = e.NewValue as System.Collections.IEnumerable;
            }
        }
        public event RoutedEventHandler Expanded;
        public event RoutedEventHandler Collapsed;

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (GetTemplateChild("PART_ExpanderToggle") is ToggleButton toggle)
            {
                toggle.Checked -= OnToggleChecked;
                toggle.Checked += OnToggleChecked;
                toggle.Unchecked -= OnToggleUnchecked;
                toggle.Unchecked += OnToggleUnchecked;
            }

            // 将 Items 同步到模板中的宿主容器
            if (GetTemplateChild("PART_ItemsHost") is ItemsControl itemsControl)
            {
                ItemsHost = itemsControl;
                itemsControl.ItemsSource = Items;
            }
            else if (GetTemplateChild("PART_ItemsHost") is Panel panel)
            {
                ItemsHost = panel;
                foreach (var item in Items)
                {
                    panel.Children.Add(item as UIElement ?? new ContentControl { Content = item });
                }
            }
        }

        private void OnToggleChecked(object sender, RoutedEventArgs e)
        {
            SetCurrentValue(IsExpandedProperty, true);
            Expanded?.Invoke(this, e);
        }

        private void OnToggleUnchecked(object sender, RoutedEventArgs e)
        {
            SetCurrentValue(IsExpandedProperty, false);
            Collapsed?.Invoke(this, e);
        }
    }
}