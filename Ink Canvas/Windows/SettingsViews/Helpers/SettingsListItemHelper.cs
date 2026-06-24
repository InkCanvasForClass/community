using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Ink_Canvas.Windows.SettingsViews.Helpers
{
    /// <summary>
    /// 设置页面列表项共享辅助方法。
    /// </summary>
    public static class SettingsListItemHelper
    {
        /// <summary>
        /// 根据列表项选中状态更新指定按钮的可见性。
        /// </summary>
        public static void UpdateButtonVisibility(ListView listView, string buttonName)
        {
            foreach (var item in listView.Items)
            {
                if (listView.ItemContainerGenerator.ContainerFromItem(item) is ListViewItem container)
                {
                    var btn = FindVisualChild<Button>(container, buttonName);
                    if (btn != null)
                        btn.Visibility = container.IsSelected ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        public static void UpdateButtonVisibility(ItemsControl itemsControl, string buttonName)
        {
            foreach (var item in itemsControl.Items)
            {
                if (itemsControl.ItemContainerGenerator.ContainerFromItem(item) is FrameworkElement container)
                {
                    var isSelected = container is ListBoxItem lbi ? lbi.IsSelected :
                                     container is ListViewItem lvi ? lvi.IsSelected : false;
                    var btn = FindVisualChild<Button>(container, buttonName);
                    if (btn != null)
                        btn.Visibility = isSelected ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        /// <summary>
        /// 根据列表项选中状态更新删除按钮的可见性。
        /// </summary>
        public static void UpdateRemoveButtonVisibility(ListView listView, string buttonName)
        {
            UpdateButtonVisibility(listView, buttonName);
        }

        /// <summary>
        /// 在可视树中查找指定名称的子元素。
        /// </summary>
        public static T FindVisualChild<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result && result.Name == name) return result;
                var found = FindVisualChild<T>(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
