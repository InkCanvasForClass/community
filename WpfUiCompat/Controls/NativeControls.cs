namespace WpfUiCompat.Controls
{
    /// <summary>兼容 iNKORE Page（提供一致的基类型），基于 WPF-UI Page。</summary>
    public class Page : System.Windows.Controls.Page
    {
        public Page() { CompatStyleHelper.AttachBaseStyle(this, typeof(System.Windows.Controls.Page)); }
    }

    /// <summary>兼容 iNKORE ListView，基于 WPF-UI ListView。</summary>
    public class ListView : Wpf.Ui.Controls.ListView
    {
        public ListView() { CompatStyleHelper.AttachBaseStyle(this, typeof(Wpf.Ui.Controls.ListView)); }
    }

    /// <summary>
    /// 兼容 iNKORE InfoBar，基于 WPF-UI InfoBar，补充 IsIconVisible 属性。
    /// </summary>
    public class InfoBar : Wpf.Ui.Controls.InfoBar
    {
        public InfoBar()
        {
            CompatStyleHelper.AttachBaseStyle(this, typeof(Wpf.Ui.Controls.InfoBar));
        }

        public static readonly System.Windows.DependencyProperty IsIconVisibleProperty =
            System.Windows.DependencyProperty.Register(
                nameof(IsIconVisible),
                typeof(bool),
                typeof(InfoBar),
                new System.Windows.PropertyMetadata(true, OnIsIconVisibleChanged));

        public bool IsIconVisible
        {
            get => (bool)GetValue(IsIconVisibleProperty);
            set => SetValue(IsIconVisibleProperty, value);
        }

        private static void OnIsIconVisibleChanged(System.Windows.DependencyObject d, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (d is InfoBar infoBar)
            {
                // WPF-UI 的 InfoBar 图标随 Severity 自动显示；此属性仅作 XAML 兼容
                infoBar.UpdateIconVisibility();
            }
        }

        private void UpdateIconVisibility()
        {
            // 在可视树中查找并控制 Severity 图标的可见性（尽力而为）
            if (!IsLoaded)
            {
                Loaded -= OnLoadedUpdateIcon;
                Loaded += OnLoadedUpdateIcon;
                return;
            }
            ApplyIconVisibility(this);
        }

        private void OnLoadedUpdateIcon(object sender, System.Windows.RoutedEventArgs e)
        {
            ApplyIconVisibility(this);
        }

        private void ApplyIconVisibility(System.Windows.DependencyObject root)
        {
            int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
                if (child is System.Windows.Controls.TextBlock { Text: { } } iconText && IsIconGlyphText(iconText.Text))
                {
                    iconText.Visibility = IsIconVisible ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
                }
                ApplyIconVisibility(child);
            }
        }

        private static bool IsIconGlyphText(string text)
        {
            // Segoe/Fluent 图标字形大多位于 PUA 区
            return text.Length <= 2 && text.Length > 0 && char.ConvertToUtf32(text, 0) >= 0xE000;
        }
    }

    /// <summary>
    /// 兼容 iNKORE AutoSuggestBox，基于 WPF-UI AutoSuggestBox，补充 QueryIcon 属性。
    /// </summary>
    public class AutoSuggestBox : Wpf.Ui.Controls.AutoSuggestBox
    {
        public AutoSuggestBox()
        {
            CompatStyleHelper.AttachBaseStyle(this, typeof(Wpf.Ui.Controls.AutoSuggestBox));
        }

        /// <summary>
        /// 兼容 iNKORE 的 QueryIcon 属性（WPF-UI 中对应 Icon）。
        /// </summary>
        public object QueryIcon
        {
            get => GetValue(QueryIconProperty);
            set => SetValue(QueryIconProperty, value);
        }

        public static readonly System.Windows.DependencyProperty QueryIconProperty =
            System.Windows.DependencyProperty.Register(
                nameof(QueryIcon),
                typeof(object),
                typeof(AutoSuggestBox),
                new System.Windows.PropertyMetadata(null, OnQueryIconChanged));

        private static void OnQueryIconChanged(System.Windows.DependencyObject d, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (d is AutoSuggestBox box)
            {
                if (e.NewValue is Wpf.Ui.Controls.IconElement iconElement)
                {
                    box.Icon = iconElement;
                }
            }
        }
    }

    /// <summary>
    /// 兼容 iNKORE NavigationViewItem，基于 WPF-UI NavigationViewItem，补充 SelectsOnInvoked 属性。
    /// </summary>
    public class NavigationViewItem : Wpf.Ui.Controls.NavigationViewItem
    {
        public NavigationViewItem()
        {
            CompatStyleHelper.AttachBaseStyle(this, typeof(Wpf.Ui.Controls.NavigationViewItem));
        }

        /// <summary>
        /// 兼容 iNKORE 的 SelectsOnInvoked 属性。WPF-UI 无对应概念，此属性仅作 XAML 兼容。
        /// </summary>
        public bool SelectsOnInvoked
        {
            get => (bool)GetValue(SelectsOnInvokedProperty);
            set => SetValue(SelectsOnInvokedProperty, value);
        }

        public static readonly System.Windows.DependencyProperty SelectsOnInvokedProperty =
            System.Windows.DependencyProperty.Register(
                nameof(SelectsOnInvoked),
                typeof(bool),
                typeof(NavigationViewItem),
                new System.Windows.PropertyMetadata(true));
    }
    /// <summary>
    /// 兼容 iNKORE NavigationViewItemHeader，基于 WPF-UI NavigationViewItemHeader，补充 Content 属性（映射到 Text）。
    /// </summary>
    public class NavigationViewItemHeader : Wpf.Ui.Controls.NavigationViewItemHeader
    {
        public NavigationViewItemHeader()
        {
            CompatStyleHelper.AttachBaseStyle(this, typeof(Wpf.Ui.Controls.NavigationViewItemHeader));
        }

        public object Content
        {
            get => GetValue(ContentProperty);
            set => SetValue(ContentProperty, value);
        }

        public static readonly System.Windows.DependencyProperty ContentProperty =
            System.Windows.DependencyProperty.Register(
                nameof(Content),
                typeof(object),
                typeof(NavigationViewItemHeader),
                new System.Windows.PropertyMetadata(null, OnContentChanged));

        private static void OnContentChanged(System.Windows.DependencyObject d, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (d is NavigationViewItemHeader header)
            {
                header.Text = e.NewValue?.ToString() ?? string.Empty;
            }
        }
    }
}