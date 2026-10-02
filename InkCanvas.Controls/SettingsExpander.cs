using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Wpf.Ui.Controls;

namespace Ink_Canvas.Controls
{
    /// <summary>
    /// 可展开的设置卡片组（迁移兼容封装）：基于 WPF-UI 的 <see cref="CardExpander"/>，
    /// 补上 iNKORE SettingsExpander 独有的 <see cref="Description"/>（分组标题下方的说明文字）。
    ///
    /// 关键：Expander/CardExpander 的默认内容属性只能承载**一个**子元素，而原设置页的
    /// <c>&lt;ui:SettingsExpander&gt;</c> 结构里同时存在多个直接子元素（例如一个 ToggleSwitch
    /// 加上一组 SettingsCard）。因此这里用 <see cref="ContentPropertyAttribute"/> 把默认内容
    /// 属性改到 <see cref="Items"/> 集合上 —— XAML 里的多个直接子元素会自动进 Items，
    /// 再由模板用 ItemsControl 统一呈现，结构与原 iuwm 版本一致。
    /// </summary>
    [ContentProperty(nameof(Items))]
    public class SettingsExpander : CardExpander
    {
        static SettingsExpander()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(SettingsExpander), new FrameworkPropertyMetadata(typeof(SettingsExpander)));
        }

        /// <summary>分组标题下方的说明文字；为空时不显示。</summary>
        public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
            nameof(Description), typeof(string), typeof(SettingsExpander), new PropertyMetadata(null));

        public string Description
        {
            get => (string)GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }

        /// <summary>左侧图标（兼容 iNKORE HeaderIcon；等价于基类的 Icon）。</summary>
        public static readonly DependencyProperty HeaderIconProperty = DependencyProperty.Register(
            nameof(HeaderIcon), typeof(object), typeof(SettingsExpander), new PropertyMetadata(null, OnHeaderIconChanged));

        public object HeaderIcon
        {
            get => GetValue(HeaderIconProperty);
            set => SetValue(HeaderIconProperty, value);
        }

        private static void OnHeaderIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not SettingsExpander expander) return;

            expander.Icon = e.NewValue as IconElement;
        }

        /// <summary>
        /// 展开区域内的子项集合（对应 iuwm SettingsExpander 的隐式内容与 Items）。
        /// XAML 里的多个直接子元素会自动加入此集合。
        /// </summary>
        public static readonly DependencyPropertyKey ItemsPropertyKey = DependencyProperty.RegisterReadOnly(
            nameof(Items), typeof(IList), typeof(SettingsExpander), new PropertyMetadata(null));

        public static readonly DependencyProperty ItemsProperty = ItemsPropertyKey.DependencyProperty;

        public IList Items => (IList)GetValue(ItemsProperty);

        public SettingsExpander()
        {
            SetValue(ItemsPropertyKey, new System.Collections.ObjectModel.ObservableCollection<object>());
        }

        /// <summary>每个子项的呈现模板（兼容 iNKORE SettingsExpander.ItemTemplate）。</summary>
        public static readonly DependencyProperty ItemTemplateProperty = DependencyProperty.Register(
            nameof(ItemTemplate), typeof(DataTemplate), typeof(SettingsExpander), new PropertyMetadata(null));

        public DataTemplate ItemTemplate
        {
            get => (DataTemplate)GetValue(ItemTemplateProperty);
            set => SetValue(ItemTemplateProperty, value);
        }

        /// <summary>每个子项的呈现模板选择器（兼容 iNKORE SettingsExpander.ItemTemplateSelector）。</summary>
        public static readonly DependencyProperty ItemTemplateSelectorProperty = DependencyProperty.Register(
            nameof(ItemTemplateSelector), typeof(DataTemplateSelector), typeof(SettingsExpander), new PropertyMetadata(null));

        public DataTemplateSelector ItemTemplateSelector
        {
            get => (DataTemplateSelector)GetValue(ItemTemplateSelectorProperty);
            set => SetValue(ItemTemplateSelectorProperty, value);
        }
    }
}