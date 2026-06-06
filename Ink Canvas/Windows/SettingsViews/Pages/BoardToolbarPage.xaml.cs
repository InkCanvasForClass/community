using GongSolutions.Wpf.DragDrop;
using Ink_Canvas.Controls.Toolbar.BoardToolbar;
using Ink_Canvas.Helpers;
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
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class BoardToolbarPage : Page
    {
        private static readonly string LogTag = "BoardToolbarPage";
        private bool _isLoaded;
        private bool _suppressConfigChange;
        private bool _suppressSave;

        private BoardToolbarLayoutSettings _currentLayout;
        private string _currentAreaId = "center";

        public ObservableCollection<BoardToolbarGroupEntry> AreaGroups { get; } = new();
        public BoardGroupChildrenDropHandler GroupDropHandler { get; }

        public IReadOnlyList<IBoardToolbarItem> AvailableItems => BoardToolbarRegistry.Discover();

        public static readonly DependencyProperty SelectedEntryProperty =
            DependencyProperty.Register(nameof(SelectedEntry), typeof(BoardToolbarComponentEntry), typeof(BoardToolbarPage),
                new PropertyMetadata(null, OnSelectedEntryChanged));

        public BoardToolbarComponentEntry SelectedEntry
        {
            get => (BoardToolbarComponentEntry)GetValue(SelectedEntryProperty);
            set => SetValue(SelectedEntryProperty, value);
        }

        private static void OnSelectedEntryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var page = (BoardToolbarPage)d;
            page.UpdatePropertiesPanel();
        }

        public static readonly DependencyProperty SettingsTabIndexProperty =
            DependencyProperty.Register(nameof(SettingsTabIndex), typeof(int), typeof(BoardToolbarPage),
                new PropertyMetadata(0));

        public int SettingsTabIndex
        {
            get => (int)GetValue(SettingsTabIndexProperty);
            set => SetValue(SettingsTabIndexProperty, value);
        }

        public BoardToolbarPage()
        {
            GroupDropHandler = new BoardGroupChildrenDropHandler(this);
            InitializeComponent();
            DataContext = this;
            Loaded += OnPageLoaded;
        }

        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                LogHelper.WriteLogToFile($"{LogTag}: OnPageLoaded 开始", LogHelper.LogType.Info);
                LoadSettings();
                RadioAreaCenter.IsChecked = true;
                _currentAreaId = "center";
                RefreshAreaPanel();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"{LogTag}: OnPageLoaded 异常: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}", LogHelper.LogType.Error);
            }
            _isLoaded = true;
        }

        #region Config file management

        private void RefreshConfigFileList()
        {
            _suppressConfigChange = true;
            ComboBoxConfigFile.Items.Clear();
            var files = BoardToolbarRegistry.ListConfigFiles();
            foreach (var name in files)
                ComboBoxConfigFile.Items.Add(name);

            var activeName = SettingsManager.Settings?.BoardToolbarConfigName ?? "default";
            var idx = files.IndexOf(activeName);
            ComboBoxConfigFile.SelectedIndex = idx >= 0 ? idx : 0;
            _suppressConfigChange = false;
        }

        private void ComboBoxConfigFile_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressConfigChange || !_isLoaded) return;
            var name = ComboBoxConfigFile.SelectedItem as string;
            if (string.IsNullOrEmpty(name)) return;

            SettingsManager.Settings.BoardToolbarConfigName = name;
            SettingsManager.SaveSettingsToFile();
            LoadSettings();
            RebuildMainWindowBoardToolbar();
        }

        private void ButtonNewConfig_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new InputDialog(FloatingBarStrings.ToolbarPage_EnterConfigName, FloatingBarStrings.ToolbarPage_NewConfig, "")
            {
                Owner = Window.GetWindow(this)
            };
            if (dialog.ShowDialog() != true) return;
            var name = dialog.InputText?.Trim();
            if (string.IsNullOrEmpty(name)) return;

            foreach (var c in System.IO.Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');

            var existing = BoardToolbarRegistry.ListConfigFiles();
            if (existing.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                MessageBox.Show(FloatingBarStrings.ToolbarPage_DuplicateConfigExists, FloatingBarStrings.ToolbarPage_Hint, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BoardToolbarRegistry.SaveConfigFile(name, BoardToolbarLayoutSettings.CreateDefault());
            SettingsManager.Settings.BoardToolbarConfigName = name;
            SettingsManager.SaveSettingsToFile();
            RefreshConfigFileList();
            LoadSettings();
            RebuildMainWindowBoardToolbar();
        }

        private void ButtonDuplicateConfig_Click(object sender, RoutedEventArgs e)
        {
            var currentName = ComboBoxConfigFile.SelectedItem as string;
            if (string.IsNullOrEmpty(currentName)) return;

            var dialog = new InputDialog(FloatingBarStrings.ToolbarPage_EnterNewConfigName, FloatingBarStrings.ToolbarPage_CopyConfig, currentName + "_copy")
            {
                Owner = Window.GetWindow(this)
            };
            if (dialog.ShowDialog() != true) return;
            var name = dialog.InputText?.Trim();
            if (string.IsNullOrEmpty(name)) return;

            foreach (var c in System.IO.Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');

            var existing = BoardToolbarRegistry.ListConfigFiles();
            if (existing.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                MessageBox.Show(FloatingBarStrings.ToolbarPage_DuplicateConfigExists, FloatingBarStrings.ToolbarPage_Hint, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var layout = BoardToolbarRegistry.LoadConfigFile(currentName) ?? BoardToolbarLayoutSettings.CreateDefault();
            BoardToolbarRegistry.SaveConfigFile(name, layout);
            SettingsManager.Settings.BoardToolbarConfigName = name;
            SettingsManager.SaveSettingsToFile();
            RefreshConfigFileList();
            LoadSettings();
            RebuildMainWindowBoardToolbar();
        }

        private void ButtonDeleteConfig_Click(object sender, RoutedEventArgs e)
        {
            var name = ComboBoxConfigFile.SelectedItem as string;
            if (string.IsNullOrEmpty(name)) return;

            var files = BoardToolbarRegistry.ListConfigFiles();
            if (files.Count <= 1)
            {
                MessageBox.Show(FloatingBarStrings.ToolbarPage_AtLeastOneConfig, FloatingBarStrings.ToolbarPage_Hint, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show($"{FloatingBarStrings.ToolbarPage_ConfirmDeleteConfig} \"{name}\"?", FloatingBarStrings.ToolbarPage_ConfirmDelete,
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            BoardToolbarRegistry.DeleteConfigFile(name);
            if (SettingsManager.Settings.BoardToolbarConfigName == name)
            {
                SettingsManager.Settings.BoardToolbarConfigName = "default";
                SettingsManager.SaveSettingsToFile();
            }
            RefreshConfigFileList();
            LoadSettings();
            RebuildMainWindowBoardToolbar();
        }

        #endregion

        #region Settings load/save

        private void LoadSettings()
        {
            LogHelper.WriteLogToFile($"{LogTag}: LoadSettings 开始", LogHelper.LogType.Info);
            SelectedEntry = null;
            AreaGroups.Clear();

            RefreshConfigFileList();

            _currentLayout = BoardToolbarRegistry.LoadActiveConfig();
            RefreshAreaPanel();

            LogHelper.WriteLogToFile($"{LogTag}: LoadSettings 完成 Areas={_currentLayout?.Areas?.Count ?? 0}", LogHelper.LogType.Info);
        }

        internal void SaveSettings()
        {
            if (!_isLoaded || _suppressSave) return;
            try
            {
                SyncAreaBack();
                var configName = SettingsManager.Settings?.BoardToolbarConfigName ?? "default";
                BoardToolbarRegistry.SaveConfigFile(configName, _currentLayout);
                LogHelper.WriteLogToFile($"{LogTag}: 配置已保存到 [{configName}]", LogHelper.LogType.Info);
                RebuildMainWindowBoardToolbar();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"{LogTag}: SaveSettings 异常: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}", LogHelper.LogType.Error);
            }
        }

        private void SyncAreaBack()
        {
            if (_currentLayout == null) return;
            var area = _currentLayout.Areas.FirstOrDefault(a =>
                string.Equals(a.Id, _currentAreaId, StringComparison.OrdinalIgnoreCase));
            if (area == null) return;

            area.Groups = new List<BoardToolbarGroupEntry>(AreaGroups.Select(g =>
            {
                var ng = new BoardToolbarGroupEntry { Id = g.Id };
                ng.Components = new List<BoardToolbarComponentEntry>(g.Components.Select(CloneEntry));
                return ng;
            }));
        }

        private static BoardToolbarComponentEntry CloneEntry(BoardToolbarComponentEntry source)
        {
            var clone = new BoardToolbarComponentEntry
            {
                Id = source.Id
            };
            if (source.Settings != null && source.Settings.Count > 0)
                clone.Settings = new Dictionary<string, object>(source.Settings);
            return clone;
        }

        private void RebuildMainWindowBoardToolbar()
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                    mainWindow?.RebuildBoardToolbar();
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"{LogTag}: RebuildBoardToolbar 异常: {ex.Message}", LogHelper.LogType.Error);
                }
            }));
        }

        #endregion

        #region Area management

        private void AreaRadioButton_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SyncAreaBack();

            if (RadioAreaLeft.IsChecked == true) _currentAreaId = "left";
            else if (RadioAreaCenter.IsChecked == true) _currentAreaId = "center";
            else if (RadioAreaRight.IsChecked == true) _currentAreaId = "right";

            RefreshAreaPanel();
        }

        private void RefreshAreaPanel()
        {
            AreaGroups.Clear();
            SelectedEntry = null;

            var area = _currentLayout?.Areas?.FirstOrDefault(a =>
                string.Equals(a.Id, _currentAreaId, StringComparison.OrdinalIgnoreCase));
            if (area == null) return;

            foreach (var group in area.Groups)
            {
                var ng = new BoardToolbarGroupEntry { Id = group.Id };
                ng.Components = new List<BoardToolbarComponentEntry>(group.Components.Select(CloneEntry));
                AreaGroups.Add(ng);
            }

            AreaGroupsControl.ItemsSource = AreaGroups;
        }

        internal void RefreshGroupsDisplay()
        {
            AreaGroupsControl.ItemsSource = null;
            AreaGroupsControl.ItemsSource = AreaGroups;
        }

        private BoardToolbarAreaEntry FindOrCreateArea(string areaId)
        {
            var area = _currentLayout?.Areas?.FirstOrDefault(a =>
                string.Equals(a.Id, areaId, StringComparison.OrdinalIgnoreCase));
            if (area == null && _currentLayout != null)
            {
                area = new BoardToolbarAreaEntry { Id = areaId };
                _currentLayout.Areas.Add(area);
            }
            return area;
        }

        #endregion

        #region Item management

        private void RemoveGroupChildItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is BoardToolbarComponentEntry entry)
            {
                foreach (var group in AreaGroups)
                {
                    if (group.Components.Remove(entry))
                    {
                        if (SelectedEntry == entry) SelectedEntry = null;
                        RefreshGroupsDisplay();
                        SaveSettings();
                        return;
                    }
                }
            }
        }

        private void AddLibraryItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is IBoardToolbarItem item)
            {
                if (AreaGroups.Count == 0)
                {
                    var group = new BoardToolbarGroupEntry { Id = "default" };
                    AreaGroups.Add(group);
                }
                var entry = new BoardToolbarComponentEntry
                {
                    Id = item.Id
                };
                AreaGroups.Last().Components.Add(entry);
                SelectedEntry = entry;
                RefreshGroupsDisplay();
                SaveSettings();
            }
        }

        private void ButtonAddGroup_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new InputDialog(
                FloatingBarStrings.BoardToolbarPage_NewGroupName,
                FloatingBarStrings.BoardToolbarPage_AddGroup2,
                "newGroup")
            { Owner = Window.GetWindow(this) };
            if (dialog.ShowDialog() != true) return;

            var name = dialog.InputText?.Trim();
            if (string.IsNullOrWhiteSpace(name)) return;

            var group = new BoardToolbarGroupEntry { Id = name };
            AreaGroups.Add(group);
            SaveSettings();
        }

        private void AddComponentToGroup_Click(object sender, RoutedEventArgs e)
        {
            var group = (sender as FrameworkElement)?.DataContext as BoardToolbarGroupEntry;
            if (group == null) return;

            var available = AvailableItems.Where(i => !group.Components.Any(c => c.Id == i.Id)).ToList();
            var dialog = new BoardToolbarComponentPickerDialog(available)
            {
                Owner = Window.GetWindow(this)
            };
            if (dialog.ShowDialog() != true) return;

            var selectedId = dialog.SelectedId;
            if (string.IsNullOrEmpty(selectedId)) return;

            var item = AvailableItems.FirstOrDefault(i => i.Id == selectedId);
            var entry = new BoardToolbarComponentEntry
            {
                Id = selectedId
            };
            group.Components.Add(entry);
            SelectedEntry = entry;
            RefreshGroupsDisplay();
            SaveSettings();
        }

        private void RemoveGroup_Click(object sender, RoutedEventArgs e)
        {
            var group = (sender as FrameworkElement)?.DataContext as BoardToolbarGroupEntry;
            if (group == null) return;
            AreaGroups.Remove(group);
            SaveSettings();
        }

        private void MoveGroupUp_Click(object sender, RoutedEventArgs e)
        {
            var group = (sender as FrameworkElement)?.DataContext as BoardToolbarGroupEntry;
            if (group == null) return;
            var index = AreaGroups.IndexOf(group);
            if (index <= 0) return;
            AreaGroups.Move(index, index - 1);
            SaveSettings();
        }

        private void MoveGroupDown_Click(object sender, RoutedEventArgs e)
        {
            var group = (sender as FrameworkElement)?.DataContext as BoardToolbarGroupEntry;
            if (group == null) return;
            var index = AreaGroups.IndexOf(group);
            if (index < 0 || index >= AreaGroups.Count - 1) return;
            AreaGroups.Move(index, index + 1);
            SaveSettings();
        }

        #endregion

        #region Properties panel

        private void UpdatePropertiesPanel()
        {
            var entry = SelectedEntry;
            if (entry == null) return;
            _suppressSave = true;

            TextBoxFixedWidth.Text = entry.GetSettingDouble("fixedWidth")?.ToString() ?? "";
            TextBoxFixedHeight.Text = entry.GetSettingDouble("fixedHeight")?.ToString() ?? "";
            TextBoxMinWidth.Text = entry.GetSettingDouble("minWidth")?.ToString() ?? "";
            TextBoxMinHeight.Text = entry.GetSettingDouble("minHeight")?.ToString() ?? "";
            TextBoxFontSize.Text = entry.GetSettingDouble("fontSize")?.ToString() ?? "";
            TextBoxOpacity.Text = entry.GetSettingDouble("opacity")?.ToString() ?? "";
            TextBoxMarginLeft.Text = entry.GetSettingDouble("marginLeft")?.ToString() ?? "";
            TextBoxMarginTop.Text = entry.GetSettingDouble("marginTop")?.ToString() ?? "";
            TextBoxMarginRight.Text = entry.GetSettingDouble("marginRight")?.ToString() ?? "";
            TextBoxMarginBottom.Text = entry.GetSettingDouble("marginBottom")?.ToString() ?? "";

            _suppressSave = false;
        }

        private void ComponentSetting_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isLoaded || SelectedEntry == null || _suppressSave) return;
            WriteComponentSettingsFromUI(SelectedEntry);
            SaveSettings();
        }

        private void WriteComponentSettingsFromUI(BoardToolbarComponentEntry entry)
        {
            WriteDoubleIfNotEmpty(entry, "fixedWidth", TextBoxFixedWidth.Text);
            WriteDoubleIfNotEmpty(entry, "fixedHeight", TextBoxFixedHeight.Text);
            WriteDoubleIfNotEmpty(entry, "minWidth", TextBoxMinWidth.Text);
            WriteDoubleIfNotEmpty(entry, "minHeight", TextBoxMinHeight.Text);
            WriteDoubleIfNotEmpty(entry, "fontSize", TextBoxFontSize.Text);
            WriteDoubleIfNotEmpty(entry, "opacity", TextBoxOpacity.Text);
            WriteDoubleIfNotEmpty(entry, "marginLeft", TextBoxMarginLeft.Text);
            WriteDoubleIfNotEmpty(entry, "marginTop", TextBoxMarginTop.Text);
            WriteDoubleIfNotEmpty(entry, "marginRight", TextBoxMarginRight.Text);
            WriteDoubleIfNotEmpty(entry, "marginBottom", TextBoxMarginBottom.Text);
        }

        private static void WriteDoubleIfNotEmpty(BoardToolbarComponentEntry entry, string key, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                entry.Settings?.Remove(key);
                return;
            }
            if (double.TryParse(text, out var val))
                entry.SetSetting(key, val);
        }

        private void ButtonResetComponentSettings_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedEntry == null) return;
            SelectedEntry.Settings?.Clear();
            UpdatePropertiesPanel();
            SaveSettings();
        }

        private void ButtonReset_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var configName = SettingsManager.Settings?.BoardToolbarConfigName ?? "default";
                BoardToolbarRegistry.SaveConfigFile(configName, BoardToolbarLayoutSettings.CreateDefault());
                SettingsManager.SaveSettingsToFile();
                RebuildMainWindowBoardToolbar();
                LoadSettings();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"{LogTag}: ButtonReset 异常: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}", LogHelper.LogType.Error);
            }
        }

        #endregion
    }

    public class BoardGroupChildrenDropHandler : IDropTarget
    {
        private readonly BoardToolbarPage _page;

        public BoardGroupChildrenDropHandler(BoardToolbarPage page)
        {
            _page = page;
        }

        public void DragOver(IDropInfo dropInfo)
        {
            if (dropInfo.Data is IBoardToolbarItem)
            {
                dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
                dropInfo.Effects = DragDropEffects.Copy;
            }
            else if (dropInfo.Data is BoardToolbarComponentEntry)
            {
                dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
                dropInfo.Effects = DragDropEffects.Move;
            }
        }

        public void Drop(IDropInfo dropInfo)
        {
            var group = (dropInfo.VisualTarget as FrameworkElement)?.DataContext as BoardToolbarGroupEntry;
            if (group == null) return;

            if (dropInfo.Data is IBoardToolbarItem item)
            {
                var entry = new BoardToolbarComponentEntry
                {
                    Id = item.Id
                };
                var insertIndex = dropInfo.UnfilteredInsertIndex;
                if (insertIndex < 0 || insertIndex > group.Components.Count)
                    insertIndex = group.Components.Count;
                group.Components.Insert(insertIndex, entry);
                _page.SelectedEntry = entry;
                _page.RefreshGroupsDisplay();
                _page.SaveSettings();
            }
            else if (dropInfo.Data is BoardToolbarComponentEntry vm)
            {
                var oldIndex = group.Components.IndexOf(vm);
                if (oldIndex == -1) return;

                var newIndex = dropInfo.UnfilteredInsertIndex;
                if (oldIndex < newIndex) newIndex--;
                newIndex = Math.Max(0, Math.Min(newIndex, group.Components.Count - 1));

                if (oldIndex != newIndex)
                {
                    group.Components.RemoveAt(oldIndex);
                    group.Components.Insert(newIndex, vm);
                }
                _page.RefreshGroupsDisplay();
                _page.SaveSettings();
            }
        }
    }

    #region Converters

    public class BoardIdToDisplayNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string id)
            {
                var items = BoardToolbarRegistry.Discover();
                var item = items.FirstOrDefault(i => i.Id == id);
                return item?.DisplayName ?? id;
            }
            return value ?? "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class BoardAreaNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value?.ToString()?.ToLower() switch
            {
                "left" => FloatingBarStrings.BoardToolbarPage_LeftArea,
                "center" => FloatingBarStrings.BoardToolbarPage_CenterArea,
                "right" => FloatingBarStrings.BoardToolbarPage_RightArea,
                _ => value?.ToString()
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class BoardGroupNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value?.ToString()?.ToLower() switch
            {
                "navigation" => FloatingBarStrings.BoardToolbarPage_GroupNavigation,
                "videobooth" => FloatingBarStrings.BoardToolbarPage_GroupVideoBooth,
                "gesture" => FloatingBarStrings.BoardToolbarPage_GroupGesture,
                "tools" => FloatingBarStrings.BoardToolbarPage_GroupTools,
                "system" => FloatingBarStrings.BoardToolbarPage_GroupSystem,
                "addpage" => FloatingBarStrings.BoardToolbarPage_GroupAddPage,
                _ => value?.ToString()
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class BoardPositionNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value?.ToString()?.ToLower() switch
            {
                "first" => "[首]",
                "last" => "[末]",
                "single" => "[独立]",
                _ => ""
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class BoardNullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value == null ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    #endregion
}
