using System.Windows.Controls;

namespace WpfUiCompat.Controls
{

    /// <summary>
    /// 兼容 iNKORE Frame：补充 UWP 风格的 SourcePageType 与 BackStackDepth。
    /// </summary>
    public class Frame : System.Windows.Controls.Frame
    {
        /// <summary>当前内容页的类型（兼容 iNKORE / UWP API）。</summary>
        public System.Type SourcePageType
        {
            get => Content?.GetType();
            set
            {
                if (value != null && (Content == null || Content.GetType() != value))
                {
                    try
                    {
                        var instance = System.Activator.CreateInstance(value);
                        Navigate(instance);
                    }
                    catch { }
                }
            }
        }

        /// <summary>后退栈深度（兼容 iNKORE / UWP API）。日记不可用时返回 0。</summary>
        public new int BackStackDepth
        {
            get
            {
                try
                {
                    if (BackStack == null) return 0;
                    int count = 0;
                    foreach (var entry in BackStack) count++;
                    return count;
                }
                catch { return 0; }
            }
        }

        /// <summary>
        /// 是否可后退（兼容 iNKORE / UWP 语义）。日记不可用或为空时安全返回 false。
        /// </summary>
        public new bool CanGoBack
        {
            get
            {
                try { return base.CanGoBack; }
                catch { return false; }
            }
        }

        /// <summary>
        /// 移除最近一条后退记录（兼容 iNKORE / UWP 语义：无日记或空栈时为安全空操作）。
        /// WPF 的 Frame 在尚未拥有导航日记（如首次导航前）调用会抛
        /// "仅当 Frame 有其自身的日记时，此操作才可用"（InvalidOperationException）。
        /// </summary>
        public new System.Windows.Navigation.JournalEntry RemoveBackEntry()
        {
            try
            {
                if (base.CanGoBack)
                {
                    return base.RemoveBackEntry();
                }
            }
            catch (System.InvalidOperationException)
            {
            }
            return null;
        }

        /// <summary>后退导航（兼容语义：不可后退或日记不可用时为安全空操作）。</summary>
        public new void GoBack()
        {
            try
            {
                if (base.CanGoBack)
                {
                    base.GoBack();
                }
            }
            catch (System.InvalidOperationException)
            {
            }
        }

    }

    /// <summary>兼容 iNKORE HyperlinkButton，基于 WPF-UI HyperlinkButton。</summary>
    public class HyperlinkButton : Wpf.Ui.Controls.HyperlinkButton
    {
        public HyperlinkButton() { CompatStyleHelper.AttachBaseStyle(this, typeof(Wpf.Ui.Controls.HyperlinkButton)); }
    }

    /// <summary>兼容 iNKORE ImageIcon，基于 WPF-UI ImageIcon。</summary>
    public class ImageIcon : Wpf.Ui.Controls.ImageIcon
    {
        public ImageIcon() { CompatStyleHelper.AttachBaseStyle(this, typeof(Wpf.Ui.Controls.ImageIcon)); }
    }

    /// <summary>兼容 iNKORE InfoBadge，基于 WPF-UI InfoBadge。</summary>
    public class InfoBadge : Wpf.Ui.Controls.InfoBadge
    {
        public InfoBadge() { CompatStyleHelper.AttachBaseStyle(this, typeof(Wpf.Ui.Controls.InfoBadge)); }
    }

    /// <summary>兼容 iNKORE ListViewItem，获取 WPF-UI 的 ListViewItem 样式。</summary>
    public class ListViewItem : System.Windows.Controls.ListViewItem
    {
        public ListViewItem() { CompatStyleHelper.AttachBaseStyle(this, typeof(System.Windows.Controls.ListViewItem)); }
    }

    /// <summary>兼容 iNKORE ProgressBar，获取 WPF-UI 的 ProgressBar 样式。</summary>
    public class ProgressBar : System.Windows.Controls.ProgressBar
    {
        public ProgressBar() { CompatStyleHelper.AttachBaseStyle(this, typeof(System.Windows.Controls.ProgressBar)); }
    }

    /// <summary>兼容 iNKORE SymbolIcon，基于 WPF-UI SymbolIcon（Fluent System Icons）。</summary>
    public class SymbolIcon : Wpf.Ui.Controls.SymbolIcon
    {
        public SymbolIcon() { CompatStyleHelper.AttachBaseStyle(this, typeof(Wpf.Ui.Controls.SymbolIcon)); }
    }

    /// <summary>兼容 iNKORE NavigationViewItemSeparator，基于 WPF-UI NavigationViewItemSeparator。</summary>
    public class NavigationViewItemSeparator : Wpf.Ui.Controls.NavigationViewItemSeparator
    {
        public NavigationViewItemSeparator() { CompatStyleHelper.AttachBaseStyle(this, typeof(Wpf.Ui.Controls.NavigationViewItemSeparator)); }
    }
    /// <summary>
    /// 兼容 iNKORE ScrollViewerEx（基于 ScrollViewer）。
    /// </summary>
    public class ScrollViewerEx : System.Windows.Controls.ScrollViewer
    {
        /// <summary>
        /// 与系统 MouseWheelScrollLines 无关的平滑滚动步进（兼容 iNKORE 属性）。
        /// </summary>
        public double MouseWheelScrollingDelta
        {
            get => (double)GetValue(MouseWheelScrollingDeltaProperty);
            set => SetValue(MouseWheelScrollingDeltaProperty, value);
        }

        public static readonly System.Windows.DependencyProperty MouseWheelScrollingDeltaProperty =
            System.Windows.DependencyProperty.Register(
                nameof(MouseWheelScrollingDelta),
                typeof(double),
                typeof(ScrollViewerEx),
                new System.Windows.PropertyMetadata(48d));
    }
}
