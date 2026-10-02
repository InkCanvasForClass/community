using Ink_Canvas.Properties;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using Wpf.Ui.Controls;
using Button = System.Windows.Controls.Button;
using StackPanel = System.Windows.Controls.StackPanel;
using TextBox = System.Windows.Controls.TextBox;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using WinForms = System.Windows.Forms;

namespace Ink_Canvas.Controls.Toolbar
{
    /// <summary>
    /// 截图组件的组件设置面板构建器。
    /// 这些均为全局设置（Settings.Automation），非 per-component 设置，
    /// 因此通过工厂返回完全自定义的面板，供浮动工具栏/白板工具栏/菜单页面的组件设置共用。
    /// </summary>
    internal static class ScreenshotSettingsPanelBuilder
    {
        public static FrameworkElement Build()
        {
            var panel = new StackPanel();

            var auto = SettingsManager.Settings.Automation;

            // 1. 截图保存位置（开关 + 浏览）
            // 开关关闭时不使用自定义位置（退回桌面默认），并禁用路径与浏览按钮使其变灰。
            var locationTextBox = new TextBox
            {
                IsReadOnly = true,
                MinWidth = 220,
                Text = string.IsNullOrEmpty(auto.ScreenshotSaveLocation)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
                    : auto.ScreenshotSaveLocation
            };

            var browseButton = new Button
            {
                Content = StorageStrings.Storage_PathBrowse,
                Padding = new Thickness(12, 4, 12, 4),
                Margin = new Thickness(8, 0, 0, 0)
            };
            browseButton.Click += (s, e) =>
            {
                using (var dialog = new WinForms.FolderBrowserDialog())
                {
                    var current = locationTextBox.Text;
                    if (Directory.Exists(current))
                    {
                        dialog.SelectedPath = current;
                    }
                    if (dialog.ShowDialog() == WinForms.DialogResult.OK)
                    {
                        var path = dialog.SelectedPath;
                        locationTextBox.Text = path;
                        SettingsManager.Settings.Automation.ScreenshotSaveLocation = path;
                        SettingsManager.SaveSettingsToFile();
                    }
                }
            };

            var locationToggle = new Wpf.Ui.Controls.ToggleSwitch
            {
                IsChecked = auto.IsSaveScreenshotToCustomLocation,
                MinWidth = 0,
                OnContent = "",
                OffContent = "",
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            var pathRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children = { locationTextBox, browseButton }
            };

            var locationCard = new Wpf.Ui.Controls.CardControl
            {
                Header = BuildCardHeader(StorageStrings.Storage_ScreenshotSaveLocation, StorageStrings.Storage_ScreenshotSaveLocationDesc),
                Icon = new SymbolIcon { Symbol = SymbolRegular.Folder24 },
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children = { pathRow, locationToggle }
                }
            };

            // 同步启用状态：开关关时路径与浏览按钮变灰
            void UpdateLocationRowEnabled()
            {
                var enabled = locationToggle.IsChecked == true;
                locationTextBox.IsEnabled = enabled;
                browseButton.IsEnabled = enabled;
                pathRow.Opacity = enabled ? 1.0 : 0.4;
            }
            UpdateLocationRowEnabled();

            void OnLocationToggled(object s, RoutedEventArgs e)
            {
                SettingsManager.Settings.Automation.IsSaveScreenshotToCustomLocation = locationToggle.IsChecked == true;
                SettingsManager.SaveSettingsToFile();
                UpdateLocationRowEnabled();
            }
            locationToggle.Checked += OnLocationToggled;
            locationToggle.Unchecked += OnLocationToggled;

            // 2. 截图后复制到剪贴板
            var clipboardCard = new Ink_Canvas.Controls.LabeledSettingsCard
            {
                Header = StorageStrings.Storage_CopyScreenshotToClipboard,
                Icon = SymbolRegular.Copy24,
                IsChecked = auto.IsCopyScreenshotToClipboard
            };
            clipboardCard.Toggled += (s, e) =>
            {
                SettingsManager.Settings.Automation.IsCopyScreenshotToClipboard = clipboardCard.IsChecked == true;
                SettingsManager.SaveSettingsToFile();
            };

            // 3. 截图时自动保存墨迹（从自动化页面迁移）
            var autoSaveStrokesCard = new Ink_Canvas.Controls.LabeledSettingsCard
            {
                Header = StorageStrings.Storage_AutoSaveInkOnScreenshot,
                Icon = SymbolRegular.Save24,
                IsChecked = auto.IsAutoSaveStrokesAtScreenshot
            };
            autoSaveStrokesCard.Toggled += (s, e) =>
            {
                SettingsManager.Settings.Automation.IsAutoSaveStrokesAtScreenshot = autoSaveStrokesCard.IsChecked == true;
                SettingsManager.SaveSettingsToFile();
            };

            // 4. 截图分日期文件夹保存（从自动化页面迁移）
            var dateFolderCard = new Ink_Canvas.Controls.LabeledSettingsCard
            {
                Header = StorageStrings.Storage_ScreenshotsByDateFolder,
                Icon = SymbolRegular.Folder24,
                IsChecked = auto.IsSaveScreenshotsInDateFolders
            };
            dateFolderCard.Toggled += (s, e) =>
            {
                SettingsManager.Settings.Automation.IsSaveScreenshotsInDateFolders = dateFolderCard.IsChecked == true;
                SettingsManager.SaveSettingsToFile();
            };

            panel.Children.Add(locationCard);
            panel.Children.Add(clipboardCard);
            panel.Children.Add(autoSaveStrokesCard);
            panel.Children.Add(dateFolderCard);

            return panel;
        }

        /// <summary>
        /// 构造「标题 + 说明」两行卡片头（与 XAML 里 CardControl.Header 的 StackPanel 结构一致）。
        /// </summary>
        private static System.Windows.Controls.StackPanel BuildCardHeader(string header, string description)
        {
            var panel = new System.Windows.Controls.StackPanel();
            panel.Children.Add(new System.Windows.Controls.TextBlock { Text = header });
            if (!string.IsNullOrEmpty(description))
            {
                var descriptionText = new System.Windows.Controls.TextBlock
                {
                    Text = description,
                    Margin = new Thickness(0, 2, 0, 0),
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap
                };
                descriptionText.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
                panel.Children.Add(descriptionText);
            }
            return panel;
        }
    }
}
