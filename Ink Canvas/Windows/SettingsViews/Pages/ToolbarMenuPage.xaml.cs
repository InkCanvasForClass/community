using GongSolutions.Wpf.DragDrop;
using Ink_Canvas.Controls.Toolbar;
using Ink_Canvas.Properties;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class ToolbarMenuPage : Page, IDropTarget
    {
        private void ListViewItem_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Control control)
            {
                control.ApplyTemplate();
                if (control.Template.FindName("PressedBackground", control) is FrameworkElement indicator)
                {
                    indicator.Width = 3;
                }
            }
        }

        private bool _suppressSave;

        public ObservableCollection<string> AddedItems { get; } = new ObservableCollection<string>();

        public ToolbarMenuPage()
        {
            InitializeComponent();
            AddedList.ItemsSource = AddedItems;
            LibraryList.ItemsSource = ToolsMenuRegistry.FloatingBarAvailableItems;
            LoadSettings();
        }

        private void LoadSettings()
        {
            var layout = ToolsMenuRegistry.LoadFloatingBarConfig();
            AddedItems.Clear();
            foreach (var id in layout.FloatingBarItems)
                AddedItems.Add(id);
            RefreshLibraryList();
        }

        private void SaveSettings()
        {
            if (_suppressSave) return;
            var layout = new ToolsMenuLayoutSettings
            {
                FloatingBarItems = AddedItems.ToList()
            };
            ToolsMenuRegistry.SaveFloatingBarConfig(layout);
        }

        private const int MaxMenuItems = 9;

        private void RefreshLibraryList()
        {
            var addedSet = new HashSet<string>(AddedItems);
            var available = ToolsMenuRegistry.FloatingBarAvailableItems
                .Where(i => !addedSet.Contains(i.Id))
                .ToList();
            LibraryList.ItemsSource = available;
            var atLimit = AddedItems.Count >= MaxMenuItems;
            LibraryList.IsEnabled = !atLimit;
            if (LibraryLimitHint != null)
            {
                LibraryLimitHint.Visibility = atLimit ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void AddLibraryItem_Click(object sender, RoutedEventArgs e)
        {
            // 防御性守卫：库列表在达到上限时已被禁用，正常路径不会触发；
            // 此处保留是为了在禁用 UI 失效（拖拽等）时静默拒绝，避免打断用户。
            if (AddedItems.Count >= MaxMenuItems) return;
            if (sender is FrameworkElement fe && fe.DataContext is ToolsMenuItemInfo item)
            {
                AddedItems.Add(item.Id);
                RefreshLibraryList();
                SaveSettings();
            }
        }

        private void RemoveItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is string id)
            {
                AddedItems.Remove(id);
                RefreshLibraryList();
                SaveSettings();
            }
        }

        private void AddedList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SettingsListItemHelper.UpdateRemoveButtonVisibility(AddedList, "BtnRemoveItem");
        }

        private void LibraryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SettingsListItemHelper.UpdateButtonVisibility(LibraryList, "BtnAddItem");
        }

        private void ButtonReset_Click(object sender, RoutedEventArgs e)
        {
            var msg = NavStrings.Menu_ResetConfirmMsg;
            var title = NavStrings.Menu_ResetConfirmTitle;
            if (System.Windows.MessageBox.Show(msg, title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            var layout = ToolsMenuRegistry.CreateDefaultFloatingBarLayout();
            _suppressSave = true;
            AddedItems.Clear();
            foreach (var id in layout.FloatingBarItems)
                AddedItems.Add(id);
            _suppressSave = false;
            RefreshLibraryList();
            SaveSettings();

            var mw = Application.Current?.Windows.OfType<MainWindow>().FirstOrDefault();
            mw?.ShowNotification(NavStrings.Menu_ResetDone);
        }

        #region Drag-drop

        public new void DragOver(IDropInfo dropInfo)
        {
            if (dropInfo.Data is ToolsMenuItemInfo)
            {
                dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
                dropInfo.Effects = DragDropEffects.Copy;
            }
            else if (dropInfo.Data is string)
            {
                dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
                dropInfo.Effects = DragDropEffects.Move;
            }
        }

        public new void Drop(IDropInfo dropInfo)
        {
            if (dropInfo.Data is ToolsMenuItemInfo item)
            {
                if (AddedItems.Count >= 9) return;
                var insertIndex = dropInfo.UnfilteredInsertIndex;
                if (insertIndex < 0 || insertIndex > AddedItems.Count)
                    insertIndex = AddedItems.Count;
                AddedItems.Insert(insertIndex, item.Id);
                RefreshLibraryList();
                SaveSettings();
            }
            else if (dropInfo.Data is string id)
            {
                var oldIndex = AddedItems.IndexOf(id);
                if (oldIndex == -1) return;
                var newIndex = dropInfo.UnfilteredInsertIndex;
                if (oldIndex < newIndex) newIndex--;
                newIndex = Math.Max(0, Math.Min(newIndex, AddedItems.Count - 1));
                if (oldIndex != newIndex)
                    AddedItems.Move(oldIndex, newIndex);
                SaveSettings();
            }
        }

        #endregion
    }

    public class MenuItemIdToNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string id)
            {
                var item = ToolsMenuRegistry.FindItem(id);
                return item?.DisplayName ?? id;
            }
            return value ?? "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class MenuItemIdToPathDataConverter : IdToPathDataConverterBase
    {
        protected override string ConvertIdToGeometryString(string id)
        {
            var item = ToolsMenuRegistry.FindItem(id);
            return item?.IconGeometry;
        }
    }
}
