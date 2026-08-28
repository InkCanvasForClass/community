using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using WpfUiCompat.Common;

namespace WpfUiCompat.Controls
{
    /// <summary>内容对话框结果（兼容 iNKORE ContentDialogResult）。</summary>
    public enum ContentDialogResult
    {
        None = 0,
        Primary = 1,
        Secondary = 2,
    }

    /// <summary>内容对话框按钮（兼容 iNKORE ContentDialogButton）。</summary>
    public enum ContentDialogButton
    {
        None = 0,
        Primary = 1,
        Secondary = 2,
        Close = 3,
    }

    /// <summary>按钮点击事件参数（兼容 iNKORE ContentDialogButtonClickEventArgs）。</summary>
    public class ContentDialogButtonClickEventArgs : RoutedEventArgs
    {
        public ContentDialogButtonClickEventArgs(RoutedEvent routedEvent, object source)
            : base(routedEvent, source) { }

        public ContentDialogButton Button { get; set; }

        public bool Cancel { get; set; }

        /// <summary>
        /// 获取延迟关闭对象（兼容 iNKORE API）。立即完成，无需等待。
        /// </summary>
        public ContentDialogDeferral GetDeferral()
        {
            return new ContentDialogDeferral();
        }
    }

    /// <summary>对话框延迟关闭对象（兼容 iNKORE ContentDialogDeferral）。</summary>
    public class ContentDialogDeferral
    {
        /// <summary>通知系统操作已完成。兼容实现中为空操作。</summary>
        public void Complete() { }
    }

    /// <summary>
    /// 兼容 iNKORE ContentDialog API 的内容对话框，基于 WPF-UI ContentDialog 实现。
    /// 提供 iNKORE 风格的无主机的 ShowAsync()（自动挂载到当前活动窗口）、
    /// GetOpenDialog 查询与 Primary/Secondary/CloseButtonClick 事件。
    /// </summary>
    public class ContentDialog : Wpf.Ui.Controls.ContentDialog
    {
        private static readonly Dictionary<Window, Wpf.Ui.Controls.ContentDialogHost> _hosts = new();

        public ContentDialog()
        {
            CompatStyleHelper.AttachBaseStyle(this, typeof(Wpf.Ui.Controls.ContentDialog));
            ButtonClicked += OnButtonClicked;
            Initialized += OnInitialized;
        }

        private void OnInitialized(object sender, EventArgs e)
        {
            // 兼容 iNKORE 通过资源键控制对话框最大尺寸的方式
            if (TryFindResource("ContentDialogMaxWidth") is double maxWidth)
            {
                DialogMaxWidth = maxWidth;
            }
            if (TryFindResource("ContentDialogMaxHeight") is double maxHeight)
            {
                DialogMaxHeight = maxHeight;
            }
        }

        #region 兼容事件

        public new event TypedEventHandler<ContentDialog, ContentDialogButtonClickEventArgs> PrimaryButtonClick;
        public new event TypedEventHandler<ContentDialog, ContentDialogButtonClickEventArgs> SecondaryButtonClick;
        public new event TypedEventHandler<ContentDialog, ContentDialogButtonClickEventArgs> CloseButtonClick;

        private void OnButtonClicked(object sender, Wpf.Ui.Controls.ContentDialogButtonClickEventArgs args)
        {
            var compatArgs = new ContentDialogButtonClickEventArgs(ButtonClickedEvent, this)
            {
                Button = args.Button switch
                {
                    Wpf.Ui.Controls.ContentDialogButton.Primary => ContentDialogButton.Primary,
                    Wpf.Ui.Controls.ContentDialogButton.Secondary => ContentDialogButton.Secondary,
                    Wpf.Ui.Controls.ContentDialogButton.Close => ContentDialogButton.Close,
                    _ => ContentDialogButton.Close
                }
            };

            switch (compatArgs.Button)
            {
                case ContentDialogButton.Primary:
                    PrimaryButtonClick?.Invoke(this, compatArgs);
                    break;
                case ContentDialogButton.Secondary:
                    SecondaryButtonClick?.Invoke(this, compatArgs);
                    break;
                case ContentDialogButton.Close:
                    CloseButtonClick?.Invoke(this, compatArgs);
                    break;
            }
        }

        #endregion

        #region DefaultButton / 结果映射

        public new ContentDialogButton DefaultButton
        {
            get => base.DefaultButton switch
            {
                Wpf.Ui.Controls.ContentDialogButton.Primary => ContentDialogButton.Primary,
                Wpf.Ui.Controls.ContentDialogButton.Secondary => ContentDialogButton.Secondary,
                Wpf.Ui.Controls.ContentDialogButton.Close => ContentDialogButton.Close,
                _ => ContentDialogButton.Close
            };
            set => base.DefaultButton = value switch
            {
                ContentDialogButton.Primary => Wpf.Ui.Controls.ContentDialogButton.Primary,
                ContentDialogButton.Secondary => Wpf.Ui.Controls.ContentDialogButton.Secondary,
                ContentDialogButton.Close => Wpf.Ui.Controls.ContentDialogButton.Close,
                _ => Wpf.Ui.Controls.ContentDialogButton.Close
            };
        }

        private static ContentDialogResult MapResult(Wpf.Ui.Controls.ContentDialogResult result)
        {
            return result switch
            {
                Wpf.Ui.Controls.ContentDialogResult.Primary => ContentDialogResult.Primary,
                Wpf.Ui.Controls.ContentDialogResult.Secondary => ContentDialogResult.Secondary,
                _ => ContentDialogResult.None
            };
        }

        #endregion

        #region ShowAsync（无主机，自动挂载到当前窗口）

        /// <summary>
        /// 显示对话框并返回结果（iNKORE 风格：自动在当前活动窗口中挂载宿主）。
        /// </summary>
        public new async Task<ContentDialogResult> ShowAsync(CancellationToken cancellationToken = default)
        {
            var host = EnsureDialogHost();
            DialogHostEx = host;
            var result = await base.ShowAsync(cancellationToken);
            return MapResult(result);
        }


        private Wpf.Ui.Controls.ContentDialogHost EnsureDialogHost(Window owner = null)
        {
            var window = owner ?? _owner ?? (Window.GetWindow(this) ?? TryGetActiveWindow());

            if (window == null)
            {
                throw new InvalidOperationException("未能确定 ContentDialog 的宿主窗口。");
            }

            if (_hosts.TryGetValue(window, out var existing) && IsHostAttached(existing, window))
            {
                return existing;
            }

            // 在窗口可视树中寻找现有 ContentDialogHost
            var found = FindContentDialogHost(window);
            if (found != null)
            {
                _hosts[window] = found;
                return found;
            }

            // 将窗口内容包裹为 Grid 并添加覆盖式宿主
            var host = new Wpf.Ui.Controls.ContentDialogHost();
            AttachHostToWindow(window, host);
            _hosts[window] = host;
            return host;
        }

        private static Window TryGetActiveWindow()
        {
            Window active = null;
            foreach (Window w in Application.Current.Windows)
            {
                if (w.IsActive) { active = w; break; }
            }
            return active ?? (Application.Current.MainWindow is Window mw && mw.IsLoaded ? mw : Application.Current.Windows.Count > 0 ? Application.Current.Windows[0] : null);
        }

        private static bool IsHostAttached(Wpf.Ui.Controls.ContentDialogHost host, Window window)
        {
            return host != null && host.Parent != null;
        }

        private static Wpf.Ui.Controls.ContentDialogHost FindContentDialogHost(DependencyObject root)
        {
            if (root == null) return null;

            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is Wpf.Ui.Controls.ContentDialogHost h)
                {
                    return h;
                }
                var inner = FindContentDialogHost(child);
                if (inner != null) return inner;
            }
            return null;
        }

        private static void AttachHostToWindow(Window window, Wpf.Ui.Controls.ContentDialogHost host)
        {
            if (window.Content is Grid grid)
            {
                grid.Children.Add(host);
                return;
            }

            var original = window.Content;
            var wrapper = new Grid();
            if (original is UIElement element)
            {
                wrapper.Children.Add(element);
            }
            wrapper.Children.Add(host);
            window.Content = wrapper;
        }

        #endregion

        #region GetOpenDialog

        /// <summary>
        /// 获取指定窗口中当前打开的 ContentDialog（兼容 iNKORE 静态方法）。
        /// </summary>
        public static ContentDialog GetOpenDialog(Window window)
        {
            if (window?.Content == null) return null;
            return FindOpenDialog(window);
        }

        private static ContentDialog FindOpenDialog(DependencyObject root)
        {
            if (root == null) return null;

            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is ContentDialog cd && cd.IsVisible)
                {
                    return cd;
                }
                var inner = FindOpenDialog(child);
                if (inner != null) return inner;
            }
            return null;
        }

        #endregion

        /// <summary>
        /// 获取延迟关闭对象（兼容 iNKORE API）。立即完成，无需等待。
        /// </summary>
        public ContentDialogDeferral GetDeferral()
        {
            return new ContentDialogDeferral();
        }
        #region Owner（兼容 iNKORE）

        /// <summary>
        /// 获取或设置对话框的宿主窗口（兼容 iNKORE 属性）。
        /// </summary>
        public Window Owner
        {
            get => _owner;
            set => _owner = value;
        }

        private Window _owner;

        #endregion

        #region 其他兼容属性

        public bool FullSizeDesired
        {
            get => DialogMaxWidth == double.PositiveInfinity;
            set
            {
                if (value)
                {
                    DialogMaxWidth = double.PositiveInfinity;
                    DialogMaxHeight = double.PositiveInfinity;
                }
            }
        }

        public bool IsShadowEnabled
        {
            get => true;
            set { }
        }

        #endregion
    }
}