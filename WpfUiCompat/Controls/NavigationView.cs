using System;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;

using WpfUiCompat.Common;

namespace WpfUiCompat.Controls
{
    /// <summary>
    /// 导航视图的窗格显示模式（兼容 iNKORE 取值，含 Auto / LeftCompact）。
    /// </summary>
    public enum NavigationViewPaneDisplayMode
    {
        Auto = 0,
        Left = 1,
        Top = 2,
        LeftCompact = 3,
        LeftMinimal = 4,
    }

    /// <summary>
    /// 导航视图的自适应显示状态（兼容 iNKORE NavigationViewDisplayMode）。
    /// </summary>
    public enum NavigationViewDisplayMode
    {
        Minimal = 0,
        Compact = 1,
        Expanded = 2,
        Top = 3,
    }

    // 事件参数基于 EventArgs（而非 RoutedEventArgs）：这些参数只经由 TypedEventHandler
    // 普通委托传递，不参与 WPF 路由事件系统，继承 RoutedEventArgs 会强制要求
    // 非空 RoutedEvent/Source 语义，在构造时机不当时抛 InvalidOperationException。
    public class NavigationViewSelectionChangedEventArgs : EventArgs
    {
        public object SelectedItem { get; internal set; }
        public object ItemContainer { get; internal set; }
        public bool IsSettingsSelected { get; internal set; }
    }

    public class NavigationViewBackRequestedEventArgs : EventArgs
    {
    }

    public class NavigationViewDisplayModeChangedEventArgs : EventArgs
    {
        public NavigationViewDisplayMode DisplayMode { get; internal set; }
    }

    /// <summary>
    /// 兼容 iNKORE NavigationView API 的导航视图，基于 WPF-UI NavigationView 实现。
    /// 通过影子事件保持 iNKORE 风格的强类型事件参数，并提供 SettingsItem、可写 SelectedItem、
    /// Auto/LeftCompact 显示模式、DisplayMode 自适应状态等 iNKORE 专有能力。
    /// </summary>
    [System.Windows.Markup.ContentProperty(nameof(Content))]
    public class NavigationView : Wpf.Ui.Controls.NavigationView
    {
        public NavigationView()
        {
            // 挂接 WPF-UI 基类隐式样式（派生类不会被隐式样式匹配到）
            CompatStyleHelper.AttachBaseStyle(this, typeof(Wpf.Ui.Controls.NavigationView));
            base.SelectionChanged += OnBaseSelectionChanged;
            base.BackRequested += OnBaseBackRequested;
            SizeChanged += OnNavigationViewSizeChanged;
        }

        #region 影子事件（iNKORE 强类型参数）

        public new event WpfUiCompat.Common.TypedEventHandler<NavigationView, NavigationViewSelectionChangedEventArgs> SelectionChanged;

        public new event WpfUiCompat.Common.TypedEventHandler<NavigationView, NavigationViewBackRequestedEventArgs> BackRequested;

        public event WpfUiCompat.Common.TypedEventHandler<NavigationView, NavigationViewDisplayModeChangedEventArgs> DisplayModeChanged;

        private void OnBaseSelectionChanged(Wpf.Ui.Controls.NavigationView sender, RoutedEventArgs args)
        {
            var compatArgs = new NavigationViewSelectionChangedEventArgs
            {
                SelectedItem = base.SelectedItem,
                IsSettingsSelected = base.SelectedItem == _settingsItem
            };
            SelectionChanged?.Invoke(this, compatArgs);
        }

        private void OnBaseBackRequested(Wpf.Ui.Controls.NavigationView sender, RoutedEventArgs args)
        {
            BackRequested?.Invoke(this, new NavigationViewBackRequestedEventArgs());
        }

        #endregion

        #region PaneDisplayMode（影子，含 Auto）

        public new NavigationViewPaneDisplayMode PaneDisplayMode
        {
            get => _paneDisplayMode;
            set
            {
                _paneDisplayMode = value;
                base.PaneDisplayMode = value switch
                {
                    NavigationViewPaneDisplayMode.Left => Wpf.Ui.Controls.NavigationViewPaneDisplayMode.Left,
                    NavigationViewPaneDisplayMode.LeftMinimal => Wpf.Ui.Controls.NavigationViewPaneDisplayMode.LeftMinimal,
                    NavigationViewPaneDisplayMode.LeftCompact => Wpf.Ui.Controls.NavigationViewPaneDisplayMode.LeftFluent,
                    NavigationViewPaneDisplayMode.Top => Wpf.Ui.Controls.NavigationViewPaneDisplayMode.Top,
                    _ => Wpf.Ui.Controls.NavigationViewPaneDisplayMode.Left
                };
            }
        }

        private NavigationViewPaneDisplayMode _paneDisplayMode = NavigationViewPaneDisplayMode.Auto;

        #endregion

        #region DisplayMode（自适应状态）

        public NavigationViewDisplayMode DisplayMode
        {
            get => _displayMode;
            private set
            {
                if (_displayMode != value)
                {
                    _displayMode = value;
                    DisplayModeChanged?.Invoke(this, new NavigationViewDisplayModeChangedEventArgs { DisplayMode = value });
                }
            }
        }

        private NavigationViewDisplayMode _displayMode = NavigationViewDisplayMode.Expanded;

        private void OnNavigationViewSizeChanged(object sender, SizeChangedEventArgs e)
        {
            // 近似 iNKORE 的自适应阈值
            double width = e.NewSize.Width;
            if (PaneDisplayMode == NavigationViewPaneDisplayMode.Auto)
            {
                if (width < OpenPaneLength * 2 - 20)
                {
                    DisplayMode = NavigationViewDisplayMode.Minimal;
                }
                else if (width < OpenPaneLength * 2 + 180)
                {
                    DisplayMode = NavigationViewDisplayMode.Compact;
                }
                else
                {
                    DisplayMode = NavigationViewDisplayMode.Expanded;
                }
            }
            else if (PaneDisplayMode == NavigationViewPaneDisplayMode.LeftMinimal)
            {
                DisplayMode = NavigationViewDisplayMode.Minimal;
            }
            else
            {
                DisplayMode = NavigationViewDisplayMode.Expanded;
            }
        }

        #endregion

        #region SelectedItem（可写影子）

        public new object SelectedItem
        {
            get => base.SelectedItem;
            set
            {
                if (value is NavigationViewItem item)
                {
                    if (item == _settingsItem)
                    {
                        // 内设置项选中
                        item.IsActive = true;
                    }
                    else
                    {
                        item.IsActive = true;
                    }
                    _pendingSelectedItem = item;
                    RaiseSelectionChangedForItem(item);
                }
            }
        }

        private object _pendingSelectedItem;

        private void RaiseSelectionChangedForItem(NavigationViewItem item)
        {
            var compatArgs = new NavigationViewSelectionChangedEventArgs
            {
                SelectedItem = item,
                IsSettingsSelected = item == _settingsItem
            };
            SelectionChanged?.Invoke(this, compatArgs);
        }

        #endregion
        #region Content（直接内容，注入到内容呈现区）

        /// <summary>
        /// 获取或设置导航视图的内容（兼容 iNKORE 直接内容写法）。
        /// 加载后将内容注入到 WPF-UI 的内容呈现器中。
        /// </summary>
        public object Content
        {
            get => GetValue(ContentProperty);
            set => SetValue(ContentProperty, value);
        }

        public static readonly DependencyProperty ContentProperty =
            DependencyProperty.Register(nameof(Content), typeof(object), typeof(NavigationView), new PropertyMetadata(null, OnCompatContentChanged));

        private static void OnCompatContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is NavigationView nav)
            {
                nav.ApplyCompatContent();
            }
        }

        private void ApplyCompatContent()
        {
            void Apply()
            {
                var presenter = FindContentPresenter(this);
                if (presenter != null)
                {
                    presenter.Content = Content;
                }
            }

            if (IsLoaded)
            {
                Apply();
            }
            else
            {
                Loaded -= OnLoadedApplyCompatContent;
                Loaded += OnLoadedApplyCompatContent;
            }
        }

        private void OnLoadedApplyCompatContent(object sender, RoutedEventArgs e)
        {
            var presenter = FindContentPresenter(this);
            if (presenter != null)
            {
                presenter.Content = Content;
            }
        }

        private Wpf.Ui.Controls.NavigationViewContentPresenter FindContentPresenter(DependencyObject root)
        {
            int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
                if (child is Wpf.Ui.Controls.NavigationViewContentPresenter p)
                {
                    return p;
                }
                var inner = FindContentPresenter(child);
                if (inner != null) return inner;
            }
            return null;
        }

        #endregion
        #region IsBackEnabled（可写影子）

        public new bool IsBackEnabled
        {
            get => _isBackEnabled;
            set
            {
                _isBackEnabled = value;
                // WPF-UI 的后退按钮由内部导航日志控制启用状态；
                // 应用自行管理 Frame 导航，这里强制同步按钮可用性
                if (IsLoaded)
                {
                    ForceBackButtonEnabled(value);
                }
                else
                {
                    Loaded -= OnLoadedForceBackButton;
                    Loaded += OnLoadedForceBackButton;
                }
            }
        }

        private bool _isBackEnabled = true;

        private void OnLoadedForceBackButton(object sender, System.Windows.RoutedEventArgs e)
        {
            ForceBackButtonEnabled(_isBackEnabled);
        }

        private void ForceBackButtonEnabled(bool enabled)
        {
            try
            {
                var button = FindDescendantButton(this);
                if (button != null && button.Name == "PART_BackButton")
                {
                    button.IsEnabled = enabled;
                }
            }
            catch { }
        }

        private static System.Windows.Controls.Button FindDescendantButton(System.Windows.DependencyObject root)
        {
            int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
                if (child is System.Windows.Controls.Button b && b.Name == "PART_BackButton")
                {
                    return b;
                }
                var inner = FindDescendantButton(child);
                if (inner != null) return inner;
            }
            return null;
        }

        #endregion

        #region SettingsItem / IsSettingsVisible

        public NavigationViewItem SettingsItem
        {
            get
            {
                if (_settingsItem == null)
                {
                    _settingsItem = new NavigationViewItem
                    {
                        Content = "Settings",
                        Icon = new FontIcon(Common.IconKeys.SegoeFluentIcons.Settings)
                    };
                    if (!IsSettingsVisible)
                    {
                        _settingsItem.Visibility = Visibility.Collapsed;
                    }
                }
                return _settingsItem;
            }
        }

        private NavigationViewItem _settingsItem;

        public bool IsSettingsVisible
        {
            get => _isSettingsVisible;
            set
            {
                _isSettingsVisible = value;
                if (_settingsItem != null)
                {
                    _settingsItem.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        private bool _isSettingsVisible = false;

        #endregion

        #region 兼容 iNKORE 的其余属性

        public bool IsTitleBarAutoPaddingEnabled
        {
            get => _isTitleBarAutoPaddingEnabled;
            set => _isTitleBarAutoPaddingEnabled = value;
        }

        private bool _isTitleBarAutoPaddingEnabled = false;

        public DataTemplate HeaderTemplate
        {
            get => _headerTemplate;
            set
            {
                _headerTemplate = value;
                ApplyHeaderTemplate();
            }
        }

        private DataTemplate _headerTemplate;

        private void ApplyHeaderTemplate()
        {
            if (_headerTemplate == null) return;
            Loaded -= OnLoadedApplyHeader;
            Loaded += OnLoadedApplyHeader;
            if (IsLoaded)
            {
                OnLoadedApplyHeader(null, null);
            }
        }

        private void OnLoadedApplyHeader(object sender, RoutedEventArgs e)
        {
            if (_headerTemplate == null) return;
            // 在可视树中查找呈现 Header 的 ContentPresenter 并套用模板
            ApplyHeaderTemplateCore(this);
        }

        private void ApplyHeaderTemplateCore(DependencyObject root)
        {
            int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
                if (child is ContentPresenter presenter && Equals(presenter.Content, Header))
                {
                    presenter.ContentTemplate = _headerTemplate;
                }
                else
                {
                    ApplyHeaderTemplateCore(child);
                }
            }
        }

        #endregion
    }
}