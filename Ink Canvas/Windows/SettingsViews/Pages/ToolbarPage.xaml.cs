using GongSolutions.Wpf.DragDrop;
using Ink_Canvas.Controls.Toolbar;
using Ink_Canvas.Helpers;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class ToolbarPage : Page, IDropTarget
    {
        private static readonly string LogTag = "ToolbarPage";
        private bool _isLoaded;
        private bool _suppressConfigChange;
        private bool _suppressSave;

        public ObservableCollection<ToolbarComponentEntry> AddedComponents { get; } = new();
        public ObservableCollection<ToolbarComponentEntry> GroupChildren { get; } = new();
        public GroupChildrenDropHandler GroupDropHandler { get; }

        public IReadOnlyList<IToolbarItem> AvailableItems => ToolbarRegistry.Discover();

        public static readonly DependencyProperty SelectedEntryProperty =
            DependencyProperty.Register(nameof(SelectedEntry), typeof(ToolbarComponentEntry), typeof(ToolbarPage),
                new PropertyMetadata(null, OnSelectedEntryChanged));

        public ToolbarComponentEntry SelectedEntry
        {
            get => (ToolbarComponentEntry)GetValue(SelectedEntryProperty);
            set => SetValue(SelectedEntryProperty, value);
        }

        private static void OnSelectedEntryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var page = (ToolbarPage)d;
            page.UpdatePropertiesPanel();
            page.SelectedGroupChild = null;
        }

        public static readonly DependencyProperty SelectedGroupChildProperty =
            DependencyProperty.Register(nameof(SelectedGroupChild), typeof(ToolbarComponentEntry), typeof(ToolbarPage),
                new PropertyMetadata(null, OnSelectedGroupChildChanged));

        public ToolbarComponentEntry SelectedGroupChild
        {
            get => (ToolbarComponentEntry)GetValue(SelectedGroupChildProperty);
            set => SetValue(SelectedGroupChildProperty, value);
        }

        private static void OnSelectedGroupChildChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var page = (ToolbarPage)d;
            page.UpdateGroupChildPropertiesPanel();
        }

        public static readonly DependencyProperty SettingsTabIndexProperty =
            DependencyProperty.Register(nameof(SettingsTabIndex), typeof(int), typeof(ToolbarPage),
                new PropertyMetadata(0));

        public int SettingsTabIndex
        {
            get => (int)GetValue(SettingsTabIndexProperty);
            set => SetValue(SettingsTabIndexProperty, value);
        }

        private void UpdatePropertiesPanel()
        {
            if (SelectedEntry == null) return;
            _suppressSave = true;
            CheckBoxShowSeparateBorder.IsChecked = SelectedEntry.ShowSeparateBorder;

            TextBoxFixedWidth.Text = SelectedEntry.GetSettingDouble(ComponentSettingKeys.FixedWidth)?.ToString() ?? "";
            TextBoxFixedHeight.Text = SelectedEntry.GetSettingDouble(ComponentSettingKeys.FixedHeight)?.ToString() ?? "";
            TextBoxMinWidth.Text = SelectedEntry.GetSettingDouble(ComponentSettingKeys.MinWidth)?.ToString() ?? "";
            TextBoxMaxWidth.Text = SelectedEntry.GetSettingDouble(ComponentSettingKeys.MaxWidth)?.ToString() ?? "";
            TextBoxMinHeight.Text = SelectedEntry.GetSettingDouble(ComponentSettingKeys.MinHeight)?.ToString() ?? "";
            TextBoxMaxHeight.Text = SelectedEntry.GetSettingDouble(ComponentSettingKeys.MaxHeight)?.ToString() ?? "";
            TextBoxFontSize.Text = SelectedEntry.GetSettingDouble(ComponentSettingKeys.FontSize)?.ToString() ?? "";
            TextBoxIconSize.Text = SelectedEntry.GetSettingDouble(ComponentSettingKeys.IconSize)?.ToString() ?? "";
            TextBoxOpacity.Text = SelectedEntry.GetSettingDouble(ComponentSettingKeys.Opacity)?.ToString() ?? "";
            TextBoxMarginLeft.Text = SelectedEntry.GetSettingDouble(ComponentSettingKeys.MarginLeft)?.ToString() ?? "";
            TextBoxMarginTop.Text = SelectedEntry.GetSettingDouble(ComponentSettingKeys.MarginTop)?.ToString() ?? "";
            TextBoxMarginRight.Text = SelectedEntry.GetSettingDouble(ComponentSettingKeys.MarginRight)?.ToString() ?? "";
            TextBoxMarginBottom.Text = SelectedEntry.GetSettingDouble(ComponentSettingKeys.MarginBottom)?.ToString() ?? "";

            var hAlign = SelectedEntry.GetSettingString(ComponentSettingKeys.HorizontalAlignment) ?? "";
            ComboBoxHAlign.SelectedIndex = hAlign switch { "Left" => 1, "Center" => 2, "Right" => 3, "Stretch" => 4, _ => 0 };
            var vAlign = SelectedEntry.GetSettingString(ComponentSettingKeys.VerticalAlignment) ?? "";
            ComboBoxVAlign.SelectedIndex = vAlign switch { "Top" => 1, "Center" => 2, "Bottom" => 3, "Stretch" => 4, _ => 0 };

            var ruleset = ToolbarRegistry.GetEffectiveRuleset(SelectedEntry);
            ComboBoxRulesetMode.SelectedIndex = (int)ruleset.Mode;
            CheckBoxRulesetReversed.IsChecked = ruleset.IsReversed;
            ItemsControlGroups.ItemsSource = ruleset.Groups;

            _suppressSave = false;
            RefreshGroupChildren();
        }

        public ToolbarPage()
        {
            GroupDropHandler = new GroupChildrenDropHandler(this);
            InitializeComponent();
            DataContext = this;
            Loaded += OnPageLoaded;
        }

        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            try { LoadSettings(); }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"{LogTag}: LoadSettings 异常: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}", LogHelper.LogType.Error);
            }
            _isLoaded = true;
        }

        #region Config file management

        private void RefreshConfigFileList()
        {
            _suppressConfigChange = true;
            ComboBoxConfigFile.Items.Clear();
            var files = ToolbarRegistry.ListConfigFiles();
            foreach (var name in files)
                ComboBoxConfigFile.Items.Add(name);

            var activeName = SettingsManager.Settings?.ToolbarConfigName ?? "default";
            var idx = files.IndexOf(activeName);
            ComboBoxConfigFile.SelectedIndex = idx >= 0 ? idx : 0;
            _suppressConfigChange = false;
        }

        private void ComboBoxConfigFile_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressConfigChange || !_isLoaded) return;
            var name = ComboBoxConfigFile.SelectedItem as string;
            if (string.IsNullOrEmpty(name)) return;

            SettingsManager.Settings.ToolbarConfigName = name;
            SettingsManager.SaveSettingsToFile();
            LoadSettings();
            RebuildMainWindowToolbar();
        }

        private void ButtonNewConfig_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new InputDialog("请输入配置文件名称：", "新建配置", "")
            {
                Owner = Window.GetWindow(this)
            };
            if (dialog.ShowDialog() != true) return;
            var name = dialog.InputText?.Trim();
            if (string.IsNullOrEmpty(name)) return;

            foreach (var c in System.IO.Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');

            var existing = ToolbarRegistry.ListConfigFiles();
            if (existing.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                MessageBox.Show("同名配置文件已存在。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ToolbarRegistry.SaveConfigFile(name, ToolbarRegistry.CreateDefaultLayout());
            SettingsManager.Settings.ToolbarConfigName = name;
            SettingsManager.SaveSettingsToFile();
            RefreshConfigFileList();
            LoadSettings();
            RebuildMainWindowToolbar();
        }

        private void ButtonDuplicateConfig_Click(object sender, RoutedEventArgs e)
        {
            var currentName = ComboBoxConfigFile.SelectedItem as string;
            if (string.IsNullOrEmpty(currentName)) return;

            var dialog = new InputDialog("请输入新配置文件名称：", "复制配置", currentName + "_copy")
            {
                Owner = Window.GetWindow(this)
            };
            if (dialog.ShowDialog() != true) return;
            var name = dialog.InputText?.Trim();
            if (string.IsNullOrEmpty(name)) return;

            foreach (var c in System.IO.Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');

            var existing = ToolbarRegistry.ListConfigFiles();
            if (existing.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                MessageBox.Show("同名配置文件已存在。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var layout = ToolbarRegistry.LoadConfigFile(currentName) ?? ToolbarRegistry.CreateDefaultLayout();
            ToolbarRegistry.SaveConfigFile(name, layout);
            SettingsManager.Settings.ToolbarConfigName = name;
            SettingsManager.SaveSettingsToFile();
            RefreshConfigFileList();
            LoadSettings();
            RebuildMainWindowToolbar();
        }

        private void ButtonDeleteConfig_Click(object sender, RoutedEventArgs e)
        {
            var name = ComboBoxConfigFile.SelectedItem as string;
            if (string.IsNullOrEmpty(name)) return;

            var files = ToolbarRegistry.ListConfigFiles();
            if (files.Count <= 1)
            {
                MessageBox.Show("至少需要保留一个配置文件。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show($"确定要删除配置 \"{name}\" 吗？", "确认删除",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            ToolbarRegistry.DeleteConfigFile(name);
            if (SettingsManager.Settings.ToolbarConfigName == name)
            {
                SettingsManager.Settings.ToolbarConfigName = "default";
                SettingsManager.SaveSettingsToFile();
            }
            RefreshConfigFileList();
            LoadSettings();
            RebuildMainWindowToolbar();
        }

        #endregion

        #region Settings load/save

        private void LoadSettings()
        {
            LogHelper.WriteLogToFile($"{LogTag}: LoadSettings 开始", LogHelper.LogType.Info);
            AddedComponents.Clear();
            SelectedEntry = null;
            GroupChildren.Clear();

            RefreshConfigFileList();

            var layout = ToolbarRegistry.LoadActiveConfig();
            foreach (var entry in layout.Components)
            {
                AddedComponents.Add(CloneEntry(entry));
            }

            LogHelper.WriteLogToFile($"{LogTag}: LoadSettings 完成 Count={AddedComponents.Count}", LogHelper.LogType.Info);
        }

        internal void SaveSettings()
        {
            if (!_isLoaded || _suppressSave) return;
            try
            {
                SyncGroupChildrenBack();
                SyncRulesetBack();
                var configName = SettingsManager.Settings?.ToolbarConfigName ?? "default";
                var layout = new ToolbarLayoutSettings();
                foreach (var entry in AddedComponents)
                {
                    layout.Components.Add(CloneEntry(entry));
                }

                ToolbarRegistry.SaveConfigFile(configName, layout);
                LogHelper.WriteLogToFile($"{LogTag}: 配置已保存到 [{configName}]", LogHelper.LogType.Info);

                RebuildMainWindowToolbar();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"{LogTag}: SaveSettings 异常: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}", LogHelper.LogType.Error);
            }
        }

        internal void SyncGroupChildrenBack()
        {
            if (SelectedEntry != null && SelectedEntry.IsGroup)
            {
                SelectedEntry.Children = new List<ToolbarComponentEntry>(GroupChildren.Select(c => CloneEntry(c)));
            }
        }

        private void SyncRulesetBack()
        {
            if (SelectedEntry == null) return;
            var ruleset = ToolbarRegistry.GetEffectiveRuleset(SelectedEntry);
            SelectedEntry.HidingRuleset = ruleset;
            SelectedEntry.HidingRule = ToolbarHidingRule.AlwaysShow;
        }

        private static ToolbarComponentEntry CloneEntry(ToolbarComponentEntry source)
        {
            var clone = new ToolbarComponentEntry
            {
                Id = source.Id,
                HidingRule = source.HidingRule,
                ShowSeparateBorder = source.ShowSeparateBorder,
                HidingRuleset = source.HidingRuleset?.Clone()
            };
            if (source.Settings != null && source.Settings.Count > 0)
                clone.Settings = new Dictionary<string, object>(source.Settings);
            if (source.Children != null && source.Children.Count > 0)
            {
                clone.Children = new List<ToolbarComponentEntry>(
                    source.Children.Select(c => CloneEntry(c)));
            }
            return clone;
        }

        private void RebuildMainWindowToolbar()
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                    mainWindow?.RebuildToolbar();
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"{LogTag}: RebuildToolbar 异常: {ex.Message}", LogHelper.LogType.Error);
                }
            }));
        }

        #endregion

        #region Drag-drop (main AddedList)

        public new void DragOver(IDropInfo dropInfo)
        {
            if (dropInfo.Data is IToolbarItem)
            {
                dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
                dropInfo.Effects = DragDropEffects.Copy;
            }
            else if (dropInfo.Data is ToolbarComponentEntry)
            {
                dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
                dropInfo.Effects = DragDropEffects.Move;
            }
        }

        public new void Drop(IDropInfo dropInfo)
        {
            if (dropInfo.Data is IToolbarItem item)
            {
                var entry = new ToolbarComponentEntry
                {
                    Id = item.Id,
                    HidingRuleset = item.DefaultHidingRuleset?.Clone(),
                    ShowSeparateBorder = item.DefaultShowSeparateBorder
                };
                if (item.Id == "builtin.group")
                {
                    entry.Children = new List<ToolbarComponentEntry>();
                }
                var insertIndex = dropInfo.UnfilteredInsertIndex;
                if (insertIndex < 0 || insertIndex > AddedComponents.Count)
                    insertIndex = AddedComponents.Count;
                AddedComponents.Insert(insertIndex, entry);
                SelectedEntry = entry;
                SaveSettings();
            }
            else if (dropInfo.Data is ToolbarComponentEntry vm)
            {
                var oldIndex = AddedComponents.IndexOf(vm);
                if (oldIndex == -1) return;

                var newIndex = dropInfo.UnfilteredInsertIndex;
                if (oldIndex < newIndex) newIndex--;
                newIndex = Math.Max(0, Math.Min(newIndex, AddedComponents.Count - 1));

                if (oldIndex != newIndex)
                {
                    AddedComponents.Move(oldIndex, newIndex);
                }
                SaveSettings();
            }
        }

        #endregion

        #region Item management

        private void RemoveItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is ToolbarComponentEntry entry)
            {
                AddedComponents.Remove(entry);
                if (SelectedEntry == entry) SelectedEntry = null;
                SaveSettings();
            }
        }

        private void CheckBoxShowSeparateBorder_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded || SelectedEntry == null) return;
            SelectedEntry.ShowSeparateBorder = CheckBoxShowSeparateBorder.IsChecked == true;
            SaveSettings();
        }

        private void ComponentSetting_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isLoaded || SelectedEntry == null || _suppressSave) return;
            WriteComponentSettingsFromUI(SelectedEntry);
            SaveSettings();
        }

        private void ComponentAlignment_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded || SelectedEntry == null || _suppressSave) return;
            WriteComponentSettingsFromUI(SelectedEntry);
            SaveSettings();
        }

        private void WriteComponentSettingsFromUI(ToolbarComponentEntry entry)
        {
            WriteDoubleIfNotEmpty(entry, ComponentSettingKeys.FixedWidth, TextBoxFixedWidth.Text);
            WriteDoubleIfNotEmpty(entry, ComponentSettingKeys.FixedHeight, TextBoxFixedHeight.Text);
            WriteDoubleIfNotEmpty(entry, ComponentSettingKeys.MinWidth, TextBoxMinWidth.Text);
            WriteDoubleIfNotEmpty(entry, ComponentSettingKeys.MaxWidth, TextBoxMaxWidth.Text);
            WriteDoubleIfNotEmpty(entry, ComponentSettingKeys.MinHeight, TextBoxMinHeight.Text);
            WriteDoubleIfNotEmpty(entry, ComponentSettingKeys.MaxHeight, TextBoxMaxHeight.Text);
            WriteDoubleIfNotEmpty(entry, ComponentSettingKeys.FontSize, TextBoxFontSize.Text);
            WriteDoubleIfNotEmpty(entry, ComponentSettingKeys.IconSize, TextBoxIconSize.Text);
            WriteDoubleIfNotEmpty(entry, ComponentSettingKeys.Opacity, TextBoxOpacity.Text);
            WriteDoubleIfNotEmpty(entry, ComponentSettingKeys.MarginLeft, TextBoxMarginLeft.Text);
            WriteDoubleIfNotEmpty(entry, ComponentSettingKeys.MarginTop, TextBoxMarginTop.Text);
            WriteDoubleIfNotEmpty(entry, ComponentSettingKeys.MarginRight, TextBoxMarginRight.Text);
            WriteDoubleIfNotEmpty(entry, ComponentSettingKeys.MarginBottom, TextBoxMarginBottom.Text);

            var hAlignTag = (ComboBoxHAlign.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            if (!string.IsNullOrEmpty(hAlignTag)) entry.SetSetting(ComponentSettingKeys.HorizontalAlignment, hAlignTag);
            else entry.Settings?.Remove(ComponentSettingKeys.HorizontalAlignment);

            var vAlignTag = (ComboBoxVAlign.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            if (!string.IsNullOrEmpty(vAlignTag)) entry.SetSetting(ComponentSettingKeys.VerticalAlignment, vAlignTag);
            else entry.Settings?.Remove(ComponentSettingKeys.VerticalAlignment);
        }

        private static void WriteDoubleIfNotEmpty(ToolbarComponentEntry entry, string key, string text)
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
                var configName = SettingsManager.Settings?.ToolbarConfigName ?? "default";
                ToolbarRegistry.SaveConfigFile(configName, ToolbarRegistry.CreateDefaultLayout());
                SettingsManager.SaveSettingsToFile();
                RebuildMainWindowToolbar();
                LoadSettings();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"{LogTag}: ButtonReset 异常: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}", LogHelper.LogType.Error);
            }
        }

        #endregion

        #region Ruleset editing

        private void ComboBoxRulesetMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded || SelectedEntry == null || _suppressSave) return;
            var ruleset = ToolbarRegistry.GetEffectiveRuleset(SelectedEntry);
            ruleset.Mode = (ToolbarLogicalMode)ComboBoxRulesetMode.SelectedIndex;
            SelectedEntry.HidingRuleset = ruleset;
            SelectedEntry.HidingRule = ToolbarHidingRule.AlwaysShow;
            SaveSettings();
        }

        private void CheckBoxRulesetReversed_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded || SelectedEntry == null || _suppressSave) return;
            var ruleset = ToolbarRegistry.GetEffectiveRuleset(SelectedEntry);
            ruleset.IsReversed = CheckBoxRulesetReversed.IsChecked == true;
            SelectedEntry.HidingRuleset = ruleset;
            SelectedEntry.HidingRule = ToolbarHidingRule.AlwaysShow;
            SaveSettings();
        }

        private void ButtonAddGroup_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedEntry == null) return;
            var ruleset = ToolbarRegistry.GetEffectiveRuleset(SelectedEntry);
            ruleset.Groups.Add(new ToolbarRuleGroup { Rules = new List<ToolbarRule> { new ToolbarRule() } });
            SelectedEntry.HidingRuleset = ruleset;
            SelectedEntry.HidingRule = ToolbarHidingRule.AlwaysShow;
            ItemsControlGroups.ItemsSource = null;
            ItemsControlGroups.ItemsSource = ruleset.Groups;
            SaveSettings();
        }

        private void ComboBoxGroupMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded || _suppressSave) return;
            if (sender is FrameworkElement fe && fe.Tag is ToolbarRuleGroup group)
            {
                group.Mode = (ToolbarLogicalMode)((ComboBox)sender).SelectedIndex;
                SaveSettings();
            }
        }

        private void CheckBoxGroupReversed_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded || _suppressSave) return;
            SaveSettings();
        }

        private void CheckBoxGroupEnabled_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded || _suppressSave) return;
            SaveSettings();
        }

        private void ButtonAddRule_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is ToolbarRuleGroup group)
            {
                group.Rules.Add(new ToolbarRule());
                SaveSettings();
            }
        }

        private void ButtonDuplicateGroup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is ToolbarRuleGroup group)
            {
                var ruleset = ToolbarRegistry.GetEffectiveRuleset(SelectedEntry);
                ruleset.Groups.Add(group.Clone());
                SelectedEntry.HidingRuleset = ruleset;
                SelectedEntry.HidingRule = ToolbarHidingRule.AlwaysShow;
                ItemsControlGroups.ItemsSource = null;
                ItemsControlGroups.ItemsSource = ruleset.Groups;
                SaveSettings();
            }
        }

        private void ButtonDeleteGroup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is ToolbarRuleGroup group)
            {
                var ruleset = ToolbarRegistry.GetEffectiveRuleset(SelectedEntry);
                ruleset.Groups.Remove(group);
                SelectedEntry.HidingRuleset = ruleset;
                SelectedEntry.HidingRule = ToolbarHidingRule.AlwaysShow;
                ItemsControlGroups.ItemsSource = null;
                ItemsControlGroups.ItemsSource = ruleset.Groups;
                SaveSettings();
            }
        }

        private void ButtonDeleteRule_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is ToolbarRule rule)
            {
                var ruleset = ToolbarRegistry.GetEffectiveRuleset(SelectedEntry);
                foreach (var group in ruleset.Groups)
                {
                    if (group.Rules.Contains(rule))
                    {
                        group.Rules.Remove(rule);
                        break;
                    }
                }
                SaveSettings();
            }
        }

        private void CheckBoxRuleReversed_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded || _suppressSave) return;
            SaveSettings();
        }

        private void ComboBoxRuleCondition_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded || _suppressSave) return;
            SaveSettings();
        }

        #endregion

        #region Group children

        private void RefreshGroupChildren()
        {
            GroupChildren.Clear();
            SelectedGroupChild = null;
            if (SelectedEntry == null || !SelectedEntry.IsGroup) return;

            if (SelectedEntry.Children != null)
            {
                foreach (var child in SelectedEntry.Children)
                {
                    GroupChildren.Add(CloneEntry(child));
                }
            }
        }

        private void UpdateGroupChildPropertiesPanel()
        {
            if (SelectedGroupChild == null) return;
            _suppressSave = true;
            CheckBoxGroupChildShowSeparateBorder.IsChecked = SelectedGroupChild.ShowSeparateBorder;

            var ruleset = ToolbarRegistry.GetEffectiveRuleset(SelectedGroupChild);
            ComboBoxGroupChildRulesetMode.SelectedIndex = (int)ruleset.Mode;
            CheckBoxGroupChildRulesetReversed.IsChecked = ruleset.IsReversed;
            ItemsControlGroupChildGroups.ItemsSource = ruleset.Groups;
            _suppressSave = false;
        }

        private void RemoveGroupChildItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is ToolbarComponentEntry entry)
            {
                GroupChildren.Remove(entry);
                if (SelectedGroupChild == entry) SelectedGroupChild = null;
                SyncGroupChildrenBack();
                SaveSettings();
            }
        }

        private void CheckBoxGroupChildShowSeparateBorder_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded || SelectedGroupChild == null) return;
            SelectedGroupChild.ShowSeparateBorder = CheckBoxGroupChildShowSeparateBorder.IsChecked == true;
            SyncGroupChildrenBack();
            SaveSettings();
        }

        private void ComboBoxGroupChildRulesetMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded || SelectedGroupChild == null || _suppressSave) return;
            var ruleset = ToolbarRegistry.GetEffectiveRuleset(SelectedGroupChild);
            ruleset.Mode = (ToolbarLogicalMode)ComboBoxGroupChildRulesetMode.SelectedIndex;
            SelectedGroupChild.HidingRuleset = ruleset;
            SelectedGroupChild.HidingRule = ToolbarHidingRule.AlwaysShow;
            SyncGroupChildrenBack();
            SaveSettings();
        }

        private void CheckBoxGroupChildRulesetReversed_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded || SelectedGroupChild == null || _suppressSave) return;
            var ruleset = ToolbarRegistry.GetEffectiveRuleset(SelectedGroupChild);
            ruleset.IsReversed = CheckBoxGroupChildRulesetReversed.IsChecked == true;
            SelectedGroupChild.HidingRuleset = ruleset;
            SelectedGroupChild.HidingRule = ToolbarHidingRule.AlwaysShow;
            SyncGroupChildrenBack();
            SaveSettings();
        }

        private void ButtonAddGroupChildGroup_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedGroupChild == null) return;
            var ruleset = ToolbarRegistry.GetEffectiveRuleset(SelectedGroupChild);
            ruleset.Groups.Add(new ToolbarRuleGroup { Rules = new List<ToolbarRule> { new ToolbarRule() } });
            SelectedGroupChild.HidingRuleset = ruleset;
            SelectedGroupChild.HidingRule = ToolbarHidingRule.AlwaysShow;
            ItemsControlGroupChildGroups.ItemsSource = null;
            ItemsControlGroupChildGroups.ItemsSource = ruleset.Groups;
            SyncGroupChildrenBack();
            SaveSettings();
        }

        private void ComboBoxGroupChildGroupMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded || _suppressSave) return;
            if (sender is FrameworkElement fe && fe.Tag is ToolbarRuleGroup group)
            {
                group.Mode = (ToolbarLogicalMode)((ComboBox)sender).SelectedIndex;
                SyncGroupChildrenBack();
                SaveSettings();
            }
        }

        private void CheckBoxGroupChildGroupReversed_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded || _suppressSave) return;
            SyncGroupChildrenBack();
            SaveSettings();
        }

        private void CheckBoxGroupChildGroupEnabled_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded || _suppressSave) return;
            SyncGroupChildrenBack();
            SaveSettings();
        }

        private void ButtonAddGroupChildRule_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is ToolbarRuleGroup group)
            {
                group.Rules.Add(new ToolbarRule());
                SyncGroupChildrenBack();
                SaveSettings();
            }
        }

        private void ButtonDuplicateGroupChildGroup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is ToolbarRuleGroup group)
            {
                var ruleset = ToolbarRegistry.GetEffectiveRuleset(SelectedGroupChild);
                ruleset.Groups.Add(group.Clone());
                SelectedGroupChild.HidingRuleset = ruleset;
                SelectedGroupChild.HidingRule = ToolbarHidingRule.AlwaysShow;
                ItemsControlGroupChildGroups.ItemsSource = null;
                ItemsControlGroupChildGroups.ItemsSource = ruleset.Groups;
                SyncGroupChildrenBack();
                SaveSettings();
            }
        }

        private void ButtonDeleteGroupChildGroup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is ToolbarRuleGroup group)
            {
                var ruleset = ToolbarRegistry.GetEffectiveRuleset(SelectedGroupChild);
                ruleset.Groups.Remove(group);
                SelectedGroupChild.HidingRuleset = ruleset;
                SelectedGroupChild.HidingRule = ToolbarHidingRule.AlwaysShow;
                ItemsControlGroupChildGroups.ItemsSource = null;
                ItemsControlGroupChildGroups.ItemsSource = ruleset.Groups;
                SyncGroupChildrenBack();
                SaveSettings();
            }
        }

        private void ButtonDeleteGroupChildRule_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is ToolbarRule rule)
            {
                var ruleset = ToolbarRegistry.GetEffectiveRuleset(SelectedGroupChild);
                foreach (var group in ruleset.Groups)
                {
                    if (group.Rules.Contains(rule))
                    {
                        group.Rules.Remove(rule);
                        break;
                    }
                }
                SyncGroupChildrenBack();
                SaveSettings();
            }
        }

        private void CheckBoxGroupChildRuleReversed_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded || _suppressSave) return;
            SyncGroupChildrenBack();
            SaveSettings();
        }

        private void ComboBoxGroupChildRuleCondition_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded || _suppressSave) return;
            SyncGroupChildrenBack();
            SaveSettings();
        }

        #endregion
    }

    public class GroupChildrenDropHandler : IDropTarget
    {
        private readonly ToolbarPage _page;

        public GroupChildrenDropHandler(ToolbarPage page)
        {
            _page = page;
        }

        public void DragOver(IDropInfo dropInfo)
        {
            if (_page.SelectedEntry == null || !_page.SelectedEntry.IsGroup) return;

            if (dropInfo.Data is IToolbarItem item)
            {
                if (item.Id == "builtin.group" || item.Id == "builtin.separator") return;
                dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
                dropInfo.Effects = DragDropEffects.Copy;
            }
            else if (dropInfo.Data is ToolbarComponentEntry)
            {
                dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
                dropInfo.Effects = DragDropEffects.Move;
            }
        }

        public void Drop(IDropInfo dropInfo)
        {
            if (_page.SelectedEntry == null || !_page.SelectedEntry.IsGroup) return;

            if (dropInfo.Data is IToolbarItem item)
            {
                if (item.Id == "builtin.group" || item.Id == "builtin.separator") return;

                var entry = new ToolbarComponentEntry
                {
                    Id = item.Id,
                    HidingRuleset = item.DefaultHidingRuleset?.Clone(),
                    ShowSeparateBorder = item.DefaultShowSeparateBorder
                };
                var insertIndex = dropInfo.UnfilteredInsertIndex;
                if (insertIndex < 0 || insertIndex > _page.GroupChildren.Count)
                    insertIndex = _page.GroupChildren.Count;
                _page.GroupChildren.Insert(insertIndex, entry);
                _page.SyncGroupChildrenBack();
                _page.SaveSettings();
            }
            else if (dropInfo.Data is ToolbarComponentEntry vm)
            {
                var oldIndex = _page.GroupChildren.IndexOf(vm);
                if (oldIndex == -1) return;

                var newIndex = dropInfo.UnfilteredInsertIndex;
                if (oldIndex < newIndex) newIndex--;
                newIndex = Math.Max(0, Math.Min(newIndex, _page.GroupChildren.Count - 1));

                if (oldIndex != newIndex)
                {
                    _page.GroupChildren.Move(oldIndex, newIndex);
                }
                _page.SyncGroupChildrenBack();
                _page.SaveSettings();
            }
        }
    }

    #region Converters

    public class IdToDisplayNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string id)
            {
                var items = ToolbarRegistry.Discover();
                var item = items.FirstOrDefault(i => i.Id == id);
                return item?.DisplayName ?? id;
            }
            return value ?? "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ConditionIdToNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string id)
            {
                var cond = ToolbarRegistry.AvailableConditions.FirstOrDefault(c => c.Key == id);
                return cond.Value ?? id;
            }
            return value ?? "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StateToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int state)
            {
                return state switch
                {
                    2 => Brushes.Green,
                    1 => Brushes.IndianRed,
                    _ => Brushes.DarkGray
                };
            }
            return Brushes.DarkGray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class LogicalModeToIntConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ToolbarLogicalMode mode)
                return (int)mode;
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int i)
                return (ToolbarLogicalMode)i;
            return ToolbarLogicalMode.Or;
        }
    }

    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value == null ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class InputDialog : Window
    {
        public string InputText { get; private set; }

        private TextBox _textBox;

        public InputDialog(string prompt, string title, string defaultValue)
        {
            Title = title;
            Width = 400;
            Height = 180;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;

            var panel = new StackPanel { Margin = new Thickness(16) };
            panel.Children.Add(new TextBlock { Text = prompt, Margin = new Thickness(0, 0, 0, 8), TextWrapping = TextWrapping.Wrap });
            _textBox = new TextBox { Text = defaultValue, Margin = new Thickness(0, 0, 0, 12) };
            _textBox.SelectAll();
            _textBox.Focus();
            panel.Children.Add(_textBox);

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var okBtn = new Button { Content = "确定", Padding = new Thickness(20, 6, 20, 6), IsDefault = true };
            okBtn.Click += (s, e) => { InputText = _textBox.Text; DialogResult = true; };
            var cancelBtn = new Button { Content = "取消", Padding = new Thickness(20, 6, 20, 6), Margin = new Thickness(8, 0, 0, 0), IsCancel = true };
            btnPanel.Children.Add(okBtn);
            btnPanel.Children.Add(cancelBtn);
            panel.Children.Add(btnPanel);

            Content = panel;
        }
    }

    #endregion
}
