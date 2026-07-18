using GongSolutions.Wpf.DragDrop;
using Ink_Canvas.Controls.Toolbar.FloatingToolbar;
using Ink_Canvas.Helpers;
using Ink_Canvas.Plugins;
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
using System.Windows.Media;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class ToolbarPage : Page, IDropTarget
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
            page.SelectedGroupChild = null;
            page.UpdatePropertiesPanel();
            page.RefreshGroupChildren();
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
            page.UpdatePropertiesPanel();
        }

        public static readonly DependencyProperty SettingsTabIndexProperty =
            DependencyProperty.Register(nameof(SettingsTabIndex), typeof(int), typeof(ToolbarPage),
                new PropertyMetadata(0));

        public int SettingsTabIndex
        {
            get => (int)GetValue(SettingsTabIndexProperty);
            set => SetValue(SettingsTabIndexProperty, value);
        }

        private ToolbarComponentEntry ActiveEntry => SelectedGroupChild ?? SelectedEntry;

        private void UpdatePropertiesPanel()
        {
            var entry = ActiveEntry;
            if (entry == null) return;
            _suppressSave = true;
            CheckBoxShowSeparateBorder.IsChecked = entry.ShowSeparateBorder;
            CheckBoxUseRedStyle.IsChecked = entry.GetSettingBool(ComponentSettingKeys.UseRedStyle);

            // 组件自定义设置：动态生成设置面板（内置组件和插件组件共用）
            UpdatePluginCustomSettingsPanel(entry);

            var ruleset = ToolbarRegistry.GetEffectiveRuleset(entry);
            ComboBoxRulesetMode.SelectedIndex = (int)ruleset.Mode;
            CheckBoxRulesetReversed.IsChecked = ruleset.IsReversed;

            // 评估规则集并更新所有层级的状态
            UpdateRulesetStateIndicator(ruleset);

            // 设置 ItemsSource（评估后设置，确保 Ellipse 绑定到最新的 State）
            ItemsControlGroups.ItemsSource = null;
            ItemsControlGroups.ItemsSource = ruleset.Groups;

            _suppressSave = false;
        }

        private void UpdateRulesetStateIndicator(ToolbarRuleset ruleset)
        {
            if (ruleset == null)
            {
                EllipseRulesetState.Fill = Brushes.DarkGray;
                return;
            }

            // 获取当前上下文状态
            var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
            bool isAnnotating = mainWindow?.IsAnnotating ?? false;
            bool isPPTMode = mainWindow?.IsInPPTPresentationMode ?? false;

            var context = new Dictionary<string, bool>
            {
                ["isAnnotating"] = isAnnotating,
                ["isPPTMode"] = isPPTMode,
                ["isContentCollapsedByUser"] = ToolbarRegistry.IsContentCollapsedByUser
            };

            // 评估规则集并更新所有层级的状态
            ToolbarRegistry.EvaluateRuleset(ruleset, context);

            EllipseRulesetState.Fill = ruleset.State switch
            {
                2 => Brushes.Green,
                1 => Brushes.IndianRed,
                _ => Brushes.DarkGray
            };
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
            var dialog = new InputDialog(FloatingBarStrings.ToolbarPage_EnterConfigName, FloatingBarStrings.ToolbarPage_NewConfig, "")
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
                MessageBox.Show(FloatingBarStrings.ToolbarPage_DuplicateConfigExists, FloatingBarStrings.ToolbarPage_Hint, MessageBoxButton.OK, MessageBoxImage.Warning);
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

            var dialog = new InputDialog(FloatingBarStrings.ToolbarPage_EnterNewConfigName, FloatingBarStrings.ToolbarPage_CopyConfig, currentName + "_copy")
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
                MessageBox.Show(FloatingBarStrings.ToolbarPage_DuplicateConfigExists, FloatingBarStrings.ToolbarPage_Hint, MessageBoxButton.OK, MessageBoxImage.Warning);
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
                MessageBox.Show(FloatingBarStrings.ToolbarPage_AtLeastOneConfig, FloatingBarStrings.ToolbarPage_Hint, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show($"{FloatingBarStrings.ToolbarPage_ConfirmDeleteConfig} \"{name}\"?", FloatingBarStrings.ToolbarPage_ConfirmDelete,
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

        private void ButtonRefreshConfig_Click(object sender, RoutedEventArgs e)
        {
            RefreshConfigFileList();
            LoadSettings();
            RebuildMainWindowToolbar();
        }

        private void ButtonOpenConfigFolder_Click(object sender, RoutedEventArgs e)
        {
            var dir = ToolbarRegistry.GetConfigDirectory();
            if (!System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = dir,
                UseShellExecute = true
            });
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
            var entry = ActiveEntry;
            if (entry == null) return;
            if (entry.HidingRuleset == null)
            {
                entry.HidingRuleset = ToolbarRegistry.GetEffectiveRuleset(entry);
                entry.HidingRule = ToolbarHidingRule.AlwaysShow;
            }
        }

        private static ToolbarComponentEntry CloneEntry(ToolbarComponentEntry source)
        {
            var clone = new ToolbarComponentEntry
            {
                Id = source.Id,
                InstanceId = Guid.NewGuid().ToString(),
                HidingRule = source.HidingRule,
                ShowSeparateBorder = source.ShowSeparateBorder,
                PreventHideOnDragClick = source.PreventHideOnDragClick,
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

        private void AddLibraryItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is IToolbarItem item)
            {
                var entry = new ToolbarComponentEntry
                {
                    Id = item.Id,
                    InstanceId = Guid.NewGuid().ToString(),
                    HidingRuleset = item.DefaultHidingRuleset?.Clone(),
                    ShowSeparateBorder = item.DefaultShowSeparateBorder,
                    PreventHideOnDragClick = item.DefaultPreventHideOnDragClick
                };
                if (item.Id == "builtin.group")
                {
                    entry.Children = new List<ToolbarComponentEntry>();
                }
                AddedComponents.Add(entry);
                SelectedEntry = entry;
                SaveSettings();
            }
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
                    InstanceId = Guid.NewGuid().ToString(),
                    HidingRuleset = item.DefaultHidingRuleset?.Clone(),
                    ShowSeparateBorder = item.DefaultShowSeparateBorder,
                    PreventHideOnDragClick = item.DefaultPreventHideOnDragClick
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

        private void AddedList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SettingsListItemHelper.UpdateRemoveButtonVisibility(AddedList, "BtnRemoveItem");
        }

        private void GroupChildrenListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SettingsListItemHelper.UpdateRemoveButtonVisibility(GroupChildrenListBox, "BtnRemoveItem");
        }


        private void RemoveItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is ToolbarComponentEntry entry)
            {
                AddedComponents.Remove(entry);
                if (SelectedEntry == entry) SelectedEntry = null;
                SaveSettings();
            }
        }

        private void ItemsList_ButtonClick(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is not Button btn) return;
            if (btn.Tag?.ToString() == "RemoveItem")
            {
                RemoveItem_Click(btn, e);
                e.Handled = true;
            }
        }

        private void GroupChildrenList_ButtonClick(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is not Button btn) return;
            if (btn.Tag?.ToString() == "RemoveItem")
            {
                RemoveGroupChildItem_Click(btn, e);
                e.Handled = true;
            }
        }

        private void LibraryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ItemsControl itemsControl)
                SettingsListItemHelper.UpdateButtonVisibility(itemsControl, "BtnAddItem");
        }

        private void CheckBoxShowSeparateBorder_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded || ActiveEntry == null) return;
            ActiveEntry.ShowSeparateBorder = CheckBoxShowSeparateBorder.IsChecked == true;
            SaveSettings();
        }

        private void CheckBoxUseRedStyle_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded || ActiveEntry == null || _suppressSave) return;
            if (CheckBoxUseRedStyle.IsChecked == true)
                ActiveEntry.SetSetting(ComponentSettingKeys.UseRedStyle, true);
            else
                ActiveEntry.Settings?.Remove(ComponentSettingKeys.UseRedStyle);
            SaveSettings();
        }

        private void UpdatePluginCustomSettingsPanel(ToolbarComponentEntry entry)
        {
            PanelPluginCustomSettings.Visibility = Visibility.Collapsed;
            PanelPluginCustomSettings.Children.Clear();

            // 优先检查内置项是否提供 CustomSettingsPanelFactory（完全自定义 UI，如小白板的全局设置）
            var builtinItem = AvailableItems.FirstOrDefault(i => i.Id == entry.Id);
            if (builtinItem?.CustomSettingsPanelFactory != null)
            {
                PanelPluginCustomSettings.Visibility = Visibility.Visible;
                PanelPluginCustomSettings.Children.Add(builtinItem.CustomSettingsPanelFactory());
                return;
            }

            // 否则通过 CustomSettings 声明式生成（插件项或内置项均可）
            var pluginItems = ToolbarRegistry.GetPluginItems();
            var pluginItem = pluginItems.FirstOrDefault(p => p.Id == entry.Id);
            IReadOnlyList<PluginToolbarSettingInfo> customSettings = pluginItem?.CustomSettings;
            if (customSettings == null || customSettings.Count == 0)
            {
                customSettings = builtinItem?.CustomSettings;
            }
            if (customSettings == null || customSettings.Count == 0) return;

            PanelPluginCustomSettings.Visibility = Visibility.Visible;

            foreach (var setting in customSettings)
            {
                var card = new iNKORE.UI.WPF.Modern.Controls.SettingsCard
                {
                    Header = setting.DisplayName,
                    Description = setting.Description
                };

                switch (setting.Type)
                {
                    case PluginToolbarSettingType.ComboBox:
                        var comboBox = new ComboBox { Tag = setting.Key };
                        // 若 OptionValues 数量与 Options 一致，则 Options 用作显示文本、OptionValues 用作保存值
                        bool hasOptionValues = setting.OptionValues != null
                            && setting.OptionValues.Count == setting.Options.Count
                            && setting.OptionValues.Count > 0;
                        for (int i = 0; i < setting.Options.Count; i++)
                        {
                            var display = setting.Options[i];
                            var value = hasOptionValues ? setting.OptionValues[i] : display;
                            comboBox.Items.Add(new ComboBoxItem { Content = display, Tag = value });
                        }
                        // 恢复已保存的值
                        var savedValue = entry.GetSettingString(setting.Key) ?? setting.DefaultValue;
                        for (int i = 0; i < comboBox.Items.Count; i++)
                        {
                            if ((comboBox.Items[i] as ComboBoxItem)?.Tag?.ToString() == savedValue)
                            {
                                comboBox.SelectedIndex = i;
                                break;
                            }
                        }
                        comboBox.SelectionChanged += PluginCustomSetting_ComboBox_SelectionChanged;
                        card.Content = comboBox;
                        break;

                    case PluginToolbarSettingType.Toggle:
                        var toggle = new iNKORE.UI.WPF.Modern.Controls.ToggleSwitch
                        {
                            Tag = setting.Key,
                            MinWidth = 0,
                            OnContent = "",
                            OffContent = ""
                        };
                        var boolValue = entry.GetSettingBool(setting.Key);
                        if (setting.DefaultValue == "true") toggle.IsOn = boolValue || !entry.Settings.ContainsKey(setting.Key);
                        else toggle.IsOn = boolValue;
                        toggle.Toggled += PluginCustomSetting_Toggle_Toggled;
                        card.Content = toggle;
                        break;

                    case PluginToolbarSettingType.Slider:
                        var slider = new Slider
                        {
                            Tag = setting.Key,
                            Width = 150,
                            Minimum = 0,
                            Maximum = 100,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        var numValue = entry.GetSettingDouble(setting.Key);
                        if (numValue.HasValue) slider.Value = numValue.Value;
                        else if (double.TryParse(setting.DefaultValue, out var dv)) slider.Value = dv;
                        slider.ValueChanged += PluginCustomSetting_Slider_ValueChanged;
                        card.Content = slider;
                        break;
                }

                PanelPluginCustomSettings.Children.Add(card);
            }
        }

        private void PluginCustomSetting_ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded || ActiveEntry == null || _suppressSave) return;
            var comboBox = sender as ComboBox;
            var key = comboBox?.Tag as string;
            var tag = (comboBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(tag))
            {
                ActiveEntry.SetSetting(key, tag);
            }
            SaveSettings();
        }

        private void PluginCustomSetting_Toggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded || ActiveEntry == null || _suppressSave) return;
            var toggle = sender as iNKORE.UI.WPF.Modern.Controls.ToggleSwitch;
            var key = toggle?.Tag as string;
            if (!string.IsNullOrEmpty(key))
            {
                ActiveEntry.SetSetting(key, toggle.IsOn);
            }
            SaveSettings();
        }

        private void PluginCustomSetting_Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isLoaded || ActiveEntry == null || _suppressSave) return;
            var slider = sender as Slider;
            var key = slider?.Tag as string;
            if (!string.IsNullOrEmpty(key))
            {
                ActiveEntry.SetSetting(key, slider.Value);
            }
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

        private void EnsureRulesetOwned()
        {
            var entry = ActiveEntry;
            if (entry == null) return;
            if (entry.HidingRuleset == null)
            {
                entry.HidingRuleset = ToolbarRegistry.GetEffectiveRuleset(entry);
                entry.HidingRule = ToolbarHidingRule.AlwaysShow;
            }
        }

        private void ComboBoxRulesetMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded || ActiveEntry == null || _suppressSave) return;
            EnsureRulesetOwned();
            ActiveEntry.HidingRuleset.Mode = (ToolbarLogicalMode)ComboBoxRulesetMode.SelectedIndex;
            SaveSettings();
        }

        private void CheckBoxRulesetReversed_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded || ActiveEntry == null || _suppressSave) return;
            EnsureRulesetOwned();
            ActiveEntry.HidingRuleset.IsReversed = CheckBoxRulesetReversed.IsChecked == true;
            SaveSettings();
        }

        private void ButtonAddGroup_Click(object sender, RoutedEventArgs e)
        {
            if (ActiveEntry == null) return;
            EnsureRulesetOwned();
            var ruleset = ActiveEntry.HidingRuleset;
            ruleset.Groups.Add(new ToolbarRuleGroup { Rules = new List<ToolbarRule> { new ToolbarRule() } });
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
                EnsureRulesetOwned();
                group.Rules.Add(new ToolbarRule());
                var ruleset = ActiveEntry.HidingRuleset;
                ItemsControlGroups.ItemsSource = null;
                ItemsControlGroups.ItemsSource = ruleset.Groups;
                SaveSettings();
            }
        }

        private void ButtonDuplicateGroup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is ToolbarRuleGroup group)
            {
                EnsureRulesetOwned();
                var ruleset = ActiveEntry.HidingRuleset;
                ruleset.Groups.Add(group.Clone());
                ItemsControlGroups.ItemsSource = null;
                ItemsControlGroups.ItemsSource = ruleset.Groups;
                SaveSettings();
            }
        }

        private void ButtonDeleteGroup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is ToolbarRuleGroup group)
            {
                EnsureRulesetOwned();
                var ruleset = ActiveEntry.HidingRuleset;
                ruleset.Groups.Remove(group);
                ItemsControlGroups.ItemsSource = null;
                ItemsControlGroups.ItemsSource = ruleset.Groups;
                SaveSettings();
            }
        }

        private void ButtonDeleteRule_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is ToolbarRule rule)
            {
                EnsureRulesetOwned();
                var ruleset = ActiveEntry.HidingRuleset;
                foreach (var group in ruleset.Groups)
                {
                    if (group.Rules.Contains(rule))
                    {
                        group.Rules.Remove(rule);
                        break;
                    }
                }
                ItemsControlGroups.ItemsSource = null;
                ItemsControlGroups.ItemsSource = ruleset.Groups;
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
                    InstanceId = Guid.NewGuid().ToString(),
                    HidingRuleset = item.DefaultHidingRuleset?.Clone(),
                    ShowSeparateBorder = item.DefaultShowSeparateBorder,
                    PreventHideOnDragClick = item.DefaultPreventHideOnDragClick
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

    public class IdToIconGeometryConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string id)
            {
                var items = ToolbarRegistry.Discover();
                var item = items.FirstOrDefault(i => i.Id == id);
                return item?.IconGeometry;
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class IdToIconKeyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string id)
            {
                var items = ToolbarRegistry.Discover();
                var item = items.FirstOrDefault(i => i.Id == id);
                return item?.IconKey;
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class IdToIconVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string id)
            {
                var items = ToolbarRegistry.Discover();
                var item = items.FirstOrDefault(i => i.Id == id);
                var mode = parameter?.ToString();
                if (mode == "fontIcon")
                    return item?.IconKey != null ? Visibility.Visible : Visibility.Collapsed;
                return !string.IsNullOrEmpty(item?.IconGeometry) ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// 将组件 Id 直接转换为 Path 可用的 Geometry 对象（组合 IdToIconGeometry + StringToGeometry 两步）。
    /// </summary>
    public class IdToPathDataConverter : IdToPathDataConverterBase
    {
        protected override string ConvertIdToGeometryString(string id)
        {
            var items = ToolbarRegistry.Discover();
            var item = items.FirstOrDefault(i => i.Id == id);
            return item?.IconGeometry;
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
            var okBtn = new Button { Content = FloatingBarStrings.ToolbarPage_OK, Padding = new Thickness(20, 6, 20, 6), IsDefault = true };
            okBtn.Click += (s, e) => { InputText = _textBox.Text; DialogResult = true; };
            var cancelBtn = new Button { Content = FloatingBarStrings.ToolbarPage_Cancel, Padding = new Thickness(20, 6, 20, 6), Margin = new Thickness(8, 0, 0, 0), IsCancel = true };
            btnPanel.Children.Add(okBtn);
            btnPanel.Children.Add(cancelBtn);
            panel.Children.Add(btnPanel);

            Content = panel;
        }
    }

    #endregion
}
