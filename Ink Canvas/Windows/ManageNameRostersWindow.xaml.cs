using Ink_Canvas.Helpers;
using Ink_Canvas.Properties;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using iNKORE.UI.WPF.Modern.Common;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ContentDialog = iNKORE.UI.WPF.Modern.Controls.ContentDialog;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;

namespace Ink_Canvas
{
    /// <summary>
    /// ManageNameRostersWindow.xaml 的交互逻辑 — 管理随机点名的"选择方案"（学生档案）
    /// </summary>
    public partial class ManageNameRostersWindow : UserControl
    {
        public ObservableCollection<RosterDisplayItem> Rosters { get; set; }

        public ManageNameRostersWindow()
        {
            InitializeComponent();
            ReloadRosters();
            RostersListView.ItemsSource = Rosters;
        }

        private void ReloadRosters()
        {
            var settings = SettingsManager.Settings?.RandSettings;
            Rosters = new ObservableCollection<RosterDisplayItem>();
            if (settings?.NameRosters == null) return;

            string selectedGuid = settings.SelectedNameRosterGuid ?? "";
            foreach (var r in settings.NameRosters)
            {
                Rosters.Add(new RosterDisplayItem(r, string.Equals(r.Guid, selectedGuid, StringComparison.OrdinalIgnoreCase)));
            }
        }

        /// <summary>
        /// 根据 Settings 中的方案列表刷新 UI（增删改后由本控件或外部对话框调用）。
        /// </summary>
        public void RefreshList()
        {
            var settings = SettingsManager.Settings?.RandSettings;
            if (settings?.NameRosters == null) { Rosters.Clear(); return; }

            string selectedGuid = settings.SelectedNameRosterGuid ?? "";
            for (int i = 0; i < settings.NameRosters.Count || i < Rosters.Count; i++)
            {
                if (i < settings.NameRosters.Count && i < Rosters.Count)
                {
                    Rosters[i].UpdateFrom(settings.NameRosters[i],
                        string.Equals(settings.NameRosters[i].Guid, selectedGuid, StringComparison.OrdinalIgnoreCase));
                }
                else if (i < settings.NameRosters.Count)
                {
                    Rosters.Add(new RosterDisplayItem(settings.NameRosters[i],
                        string.Equals(settings.NameRosters[i].Guid, selectedGuid, StringComparison.OrdinalIgnoreCase)));
                }
                else
                {
                    Rosters.RemoveAt(Rosters.Count - 1);
                    i--;
                }
            }
        }

        /// <summary>
        /// 编辑指定方案的名单：临时把该方案写入 Names.txt，打开名单编辑窗口，关闭后写回方案；
        /// 若编辑的不是当前选中方案，则恢复原先的当前名单文件。
        /// </summary>
        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.Tag is RosterDisplayItem item)) return;

            var settings = SettingsManager.Settings?.RandSettings;
            var roster = settings?.NameRosters?.FirstOrDefault(r =>
                !string.IsNullOrEmpty(r.Guid) && string.Equals(r.Guid, item.Guid, StringComparison.OrdinalIgnoreCase));
            if (roster == null) return;

            string selectedGuid = settings.SelectedNameRosterGuid ?? "";
            bool isCurrent = string.Equals(selectedGuid, item.Guid, StringComparison.OrdinalIgnoreCase);
            var (prevNames, prevReplace) = NameRosterManager.ReadCurrentFiles();

            try
            {
                // 让 NamesInputWindow 加载到该方案内容
                NameRosterManager.ApplyRoster(roster);

                var namesInputWindow = new NamesInputWindow
                {
                    Owner = Window.GetWindow(this)
                };
                namesInputWindow.ShowDialog();

                // 将编辑结果（无论是否改动）同步回该方案
                NameRosterManager.SaveCurrentFilesToRoster(item.Guid);

                if (!isCurrent)
                {
                    // 恢复原先生效的名单，避免“编辑非当前方案”副作用改掉当前点名列表
                    if (!string.IsNullOrEmpty(selectedGuid))
                    {
                        var selected = settings.NameRosters?.FirstOrDefault(r =>
                            !string.IsNullOrEmpty(r.Guid) &&
                            string.Equals(r.Guid, selectedGuid, StringComparison.OrdinalIgnoreCase));
                        if (selected != null)
                            NameRosterManager.ApplyRoster(selected);
                        else
                            NameRosterManager.WriteCurrentFiles(prevNames, prevReplace);
                    }
                    else
                    {
                        NameRosterManager.WriteCurrentFiles(prevNames, prevReplace);
                    }
                }

                RefreshList();
            }
            catch (Exception ex)
            {
                // 尽量恢复原先文件
                try
                {
                    if (!isCurrent)
                        NameRosterManager.WriteCurrentFiles(prevNames, prevReplace);
                }
                catch { /* ignore restore errors */ }

                MessageBox.Show(string.Format(RandomStrings.Random_Roster_OperationFailedFormat, ex.Message),
                    RandomStrings.Random_Error, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void RenameButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.Tag is RosterDisplayItem item)) return;

            Window owner = Window.GetWindow(this);
            string newName = await PromptRosterNameAsync(
                owner,
                RandomStrings.Random_Roster_RenameTitle,
                RandomStrings.Random_Roster_RenamePrompt,
                item.Name);

            if (newName == null) return;

            if (string.IsNullOrEmpty(newName))
            {
                MessageBox.Show(RandomStrings.Random_Roster_EmptyName,
                    RandomStrings.Random_Roster_RenameTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.Equals(newName, item.Name, StringComparison.Ordinal))
            {
                RefreshList();
                return;
            }

            var settings = SettingsManager.Settings?.RandSettings;
            if (settings?.NameRosters != null &&
                settings.NameRosters.Any(r => !string.Equals(r.Guid, item.Guid, StringComparison.OrdinalIgnoreCase)
                                              && string.Equals(r.Name, newName, StringComparison.Ordinal)))
            {
                MessageBox.Show(string.Format(RandomStrings.Random_Roster_DuplicateNameFormat, newName),
                    RandomStrings.Random_Roster_RenameTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                NameRosterManager.RenameRoster(item.Guid, newName);
                RefreshList();
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(RandomStrings.Random_Roster_OperationFailedFormat, ex.Message),
                    RandomStrings.Random_Error, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.Tag is RosterDisplayItem item)) return;

            if (MessageBox.Show(string.Format(RandomStrings.Random_Roster_DeleteConfirmFormat, item.Name),
                    RandomStrings.Random_Roster_DeleteConfirmTitle,
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            try
            {
                NameRosterManager.DeleteRoster(item.Guid);
                RefreshList();
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(RandomStrings.Random_Roster_DeleteFailedFormat, ex.Message),
                    RandomStrings.Random_Error, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ButtonAddRoster_Click(object sender, RoutedEventArgs e)
        {
            Window owner = Window.GetWindow(this);
            string guid = await AddNewRosterDialogAsync(owner);
            if (string.IsNullOrEmpty(guid)) return;

            try
            {
                NameRosterManager.SelectAndApply(guid);
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(RandomStrings.Random_Roster_OperationFailedFormat, ex.Message),
                    RandomStrings.Random_Error, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            RefreshList();
        }

        /// <summary>
        /// 新建方案。调用方（设置页/点名窗口）通过此方法弹出名称输入框。
        /// 返回新方案的 Guid，用户取消则返回 null。
        /// </summary>
        public static async Task<string> AddNewRosterDialogAsync(Window owner)
        {
            string name = await PromptRosterNameAsync(
                owner,
                RandomStrings.Random_Roster_AddTitle,
                RandomStrings.Random_Roster_AddPrompt,
                "");

            if (name == null) return null;

            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show(RandomStrings.Random_Roster_EmptyName,
                    RandomStrings.Random_Roster_AddTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            var settings = SettingsManager.Settings?.RandSettings;
            if (settings?.NameRosters != null &&
                settings.NameRosters.Any(r => string.Equals(r.Name, name, StringComparison.Ordinal)))
            {
                MessageBox.Show(string.Format(RandomStrings.Random_Roster_DuplicateNameFormat, name),
                    RandomStrings.Random_Roster_AddTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            return NameRosterManager.AddRoster(name);
        }

        /// <summary>
        /// 使用 iNKORE ContentDialog 输入方案名称。
        /// 若当前 Owner 上已有 ContentDialog（如管理列表），则在该对话框内就地切换内容，避免“同时只能打开一个 ContentDialog”的限制。
        /// 返回 trim 后的名称；用户取消返回 null。
        /// </summary>
        public static async Task<string> PromptRosterNameAsync(
            Window owner, string title, string prompt, string defaultValue)
        {
            var nameBox = new TextBox
            {
                Text = defaultValue ?? "",
                MinWidth = 320,
                Margin = new Thickness(0, 8, 0, 0)
            };
            var panel = new StackPanel { MinWidth = 320 };
            panel.Children.Add(new TextBlock
            {
                Text = prompt,
                TextWrapping = TextWrapping.Wrap
            });
            panel.Children.Add(nameBox);

            void FocusNameBox()
            {
                nameBox.Focus();
                nameBox.SelectAll();
            }
            nameBox.Loaded += (s, e) => FocusNameBox();

            ContentDialog existing = null;
            if (owner != null)
            {
                try { existing = ContentDialog.GetOpenDialog(owner); }
                catch { existing = null; }
            }

            if (existing != null)
            {
                return await PromptOnExistingDialogAsync(existing, title, panel, nameBox);
            }

            var dialog = new ContentDialog
            {
                Title = title,
                Content = panel,
                PrimaryButtonText = CommonStrings.Common_OK,
                CloseButtonText = CommonStrings.Common_Cancel,
                Owner = owner,
                DefaultButton = ContentDialogButton.Primary
            };

            string acceptedName = null;
            dialog.PrimaryButtonClick += (s, args) =>
            {
                var name = (nameBox.Text ?? "").Trim();
                if (string.IsNullOrEmpty(name))
                {
                    args.Cancel = true;
                    MessageBox.Show(RandomStrings.Random_Roster_EmptyName, title,
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                acceptedName = name;
            };

            // 等布局完成后再聚焦
            dialog.Opened += (s, e) => FocusNameBox();

            var result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary ? acceptedName : null;
        }

        /// <summary>
        /// 在已打开的 ContentDialog 上临时切换为名称输入界面，完成后恢复原内容。
        /// </summary>
        private static Task<string> PromptOnExistingDialogAsync(
            ContentDialog existing, string title, StackPanel panel, TextBox nameBox)
        {
            var tcs = new TaskCompletionSource<string>();

            object oldContent = existing.Content;
            object oldTitle = existing.Title;
            string oldPrimary = existing.PrimaryButtonText;
            string oldSecondary = existing.SecondaryButtonText;
            string oldClose = existing.CloseButtonText;
            ContentDialogButton oldDefault = existing.DefaultButton;
            bool oldPrimaryEnabled = existing.IsPrimaryButtonEnabled;
            bool oldSecondaryEnabled = existing.IsSecondaryButtonEnabled;

            TypedEventHandler<ContentDialog, ContentDialogButtonClickEventArgs> primaryHandler = null;
            TypedEventHandler<ContentDialog, ContentDialogButtonClickEventArgs> closeHandler = null;
            TypedEventHandler<ContentDialog, ContentDialogButtonClickEventArgs> secondaryHandler = null;

            void Restore()
            {
                if (primaryHandler != null) existing.PrimaryButtonClick -= primaryHandler;
                if (closeHandler != null) existing.CloseButtonClick -= closeHandler;
                if (secondaryHandler != null) existing.SecondaryButtonClick -= secondaryHandler;

                existing.Content = oldContent;
                existing.Title = oldTitle;
                existing.PrimaryButtonText = oldPrimary;
                existing.SecondaryButtonText = oldSecondary;
                existing.CloseButtonText = oldClose;
                existing.DefaultButton = oldDefault;
                existing.IsPrimaryButtonEnabled = oldPrimaryEnabled;
                existing.IsSecondaryButtonEnabled = oldSecondaryEnabled;
            }

            primaryHandler = (s, args) =>
            {
                // 始终取消，避免关闭外层“管理”对话框
                args.Cancel = true;
                var name = (nameBox.Text ?? "").Trim();
                if (string.IsNullOrEmpty(name))
                {
                    MessageBox.Show(RandomStrings.Random_Roster_EmptyName, title,
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                Restore();
                tcs.TrySetResult(name);
            };

            closeHandler = (s, args) =>
            {
                args.Cancel = true;
                Restore();
                tcs.TrySetResult(null);
            };

            secondaryHandler = (s, args) =>
            {
                args.Cancel = true;
                Restore();
                tcs.TrySetResult(null);
            };

            existing.Title = title;
            existing.Content = panel;
            existing.PrimaryButtonText = CommonStrings.Common_OK;
            existing.CloseButtonText = CommonStrings.Common_Cancel;
            existing.SecondaryButtonText = null;
            existing.DefaultButton = ContentDialogButton.Primary;
            existing.IsPrimaryButtonEnabled = true;

            existing.PrimaryButtonClick += primaryHandler;
            existing.CloseButtonClick += closeHandler;
            existing.SecondaryButtonClick += secondaryHandler;

            // 下一帧聚焦
            panel.Dispatcher.BeginInvoke(new Action(() =>
            {
                nameBox.Focus();
                nameBox.SelectAll();
            }), System.Windows.Threading.DispatcherPriority.Input);

            return tcs.Task;
        }
    }

    /// <summary>
    /// ListView 展示行：把 NameRoster 的内容与"是否当前"状态合并展示。
    /// </summary>
    public class RosterDisplayItem : DependencyObject
    {
        public string Guid { get; private set; }

        public string Name
        {
            get { return (string)GetValue(NameProperty); }
            set { SetValue(NameProperty, value); }
        }
        public static readonly DependencyProperty NameProperty =
            DependencyProperty.Register(nameof(Name), typeof(string), typeof(RosterDisplayItem), new PropertyMetadata(""));

        public string PeopleCountText
        {
            get { return (string)GetValue(PeopleCountTextProperty); }
            set { SetValue(PeopleCountTextProperty, value); }
        }
        public static readonly DependencyProperty PeopleCountTextProperty =
            DependencyProperty.Register(nameof(PeopleCountText), typeof(string), typeof(RosterDisplayItem), new PropertyMetadata(""));

        public RosterDisplayItem(NameRoster roster, bool isCurrent)
        {
            UpdateFrom(roster, isCurrent);
        }

        public void UpdateFrom(NameRoster roster, bool isCurrent)
        {
            Guid = roster?.Guid ?? "";
            Name = roster?.Name ?? "";
            int count = CountNames(roster?.NamesContent);
            string prefix = isCurrent ? "✓ " : "";
            PeopleCountText = prefix + count;
        }

        private static int CountNames(string namesContent)
        {
            if (string.IsNullOrEmpty(namesContent)) return 0;
            return namesContent.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                                .Count(s => !string.IsNullOrWhiteSpace(s));
        }
    }
}
