using Ink_Canvas.Properties;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using iNKORE.UI.WPF.Modern.Common.IconKeys;
using iNKORE.UI.WPF.Modern.Controls;
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

            // 1. 截图保存位置（浏览）
            var locationCard = new SettingsCard
            {
                Header = StorageStrings.Storage_ScreenshotSaveLocation,
                Description = StorageStrings.Storage_ScreenshotSaveLocationDesc,
                HeaderIcon = new FontIcon(SegoeFluentIcons.Folder)
            };

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

            locationCard.Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children = { locationTextBox, browseButton }
            };

            // 2. 截图后复制到剪贴板
            var clipboardCard = new Ink_Canvas.Controls.LabeledSettingsCard
            {
                Header = StorageStrings.Storage_CopyScreenshotToClipboard,
                Icon = SegoeFluentIcons.Copy,
                IsOn = auto.IsCopyScreenshotToClipboard
            };
            clipboardCard.Toggled += (s, e) =>
            {
                SettingsManager.Settings.Automation.IsCopyScreenshotToClipboard = clipboardCard.IsOn;
                SettingsManager.SaveSettingsToFile();
            };

            // 3. 截图时自动保存墨迹（从自动化页面迁移）
            var autoSaveStrokesCard = new Ink_Canvas.Controls.LabeledSettingsCard
            {
                Header = StorageStrings.Storage_AutoSaveInkOnScreenshot,
                Icon = SegoeFluentIcons.Save,
                IsOn = auto.IsAutoSaveStrokesAtScreenshot
            };
            autoSaveStrokesCard.Toggled += (s, e) =>
            {
                SettingsManager.Settings.Automation.IsAutoSaveStrokesAtScreenshot = autoSaveStrokesCard.IsOn;
                SettingsManager.SaveSettingsToFile();
            };

            // 4. 截图分日期文件夹保存（从自动化页面迁移）
            var dateFolderCard = new Ink_Canvas.Controls.LabeledSettingsCard
            {
                Header = StorageStrings.Storage_ScreenshotsByDateFolder,
                Icon = SegoeFluentIcons.Folder,
                IsOn = auto.IsSaveScreenshotsInDateFolders
            };
            dateFolderCard.Toggled += (s, e) =>
            {
                SettingsManager.Settings.Automation.IsSaveScreenshotsInDateFolders = dateFolderCard.IsOn;
                SettingsManager.SaveSettingsToFile();
            };

            panel.Children.Add(locationCard);
            panel.Children.Add(clipboardCard);
            panel.Children.Add(autoSaveStrokesCard);
            panel.Children.Add(dateFolderCard);

            return panel;
        }
    }
}
