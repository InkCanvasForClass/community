using GongSolutions.Wpf.DragDrop;
using Ink_Canvas.Controls.Toolbar;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class BoardMenuPage : Page, IDropTarget
    {
        private bool _suppressSave;

        public ObservableCollection<string> AddedItems { get; } = new ObservableCollection<string>();

        public BoardMenuPage()
        {
            InitializeComponent();
            AddedList.ItemsSource = AddedItems;
            LibraryList.ItemsSource = ToolsMenuRegistry.BoardAvailableItems;
            LoadSettings();
        }

        private void LoadSettings()
        {
            var layout = ToolsMenuRegistry.LoadBoardConfig();
            AddedItems.Clear();
            foreach (var id in layout.BoardItems)
                AddedItems.Add(id);
            RefreshLibraryList();
        }

        private void SaveSettings()
        {
            if (_suppressSave) return;
            var layout = new ToolsMenuLayoutSettings
            {
                BoardItems = AddedItems.ToList()
            };
            ToolsMenuRegistry.SaveBoardConfig(layout);
        }

        private void RefreshLibraryList()
        {
            var addedSet = new HashSet<string>(AddedItems);
            var available = ToolsMenuRegistry.BoardAvailableItems
                .Where(i => !addedSet.Contains(i.Id))
                .ToList();
            LibraryList.ItemsSource = available;
        }

        private void AddLibraryItem_Click(object sender, RoutedEventArgs e)
        {
            if (AddedItems.Count >= 9)
            {
                System.Windows.MessageBox.Show("最多只能添加 9 个菜单项。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
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

        private void ButtonReset_Click(object sender, RoutedEventArgs e)
        {
            var layout = ToolsMenuRegistry.CreateDefaultBoardLayout();
            _suppressSave = true;
            AddedItems.Clear();
            foreach (var id in layout.BoardItems)
                AddedItems.Add(id);
            _suppressSave = false;
            RefreshLibraryList();
            SaveSettings();
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

    public class BoardMenuItemIdToNameConverter : IValueConverter
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
}
