using Ink_Canvas.Helpers;
using Ink_Canvas.Models;
using iNKORE.UI.WPF.Modern.Controls;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class AnnouncementCenterPage : Page
    {
        public AnnouncementCenterPage()
        {
            InitializeComponent();
            Loaded += AnnouncementCenterPage_Loaded;
        }

        private void AnnouncementCenterPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadAnnouncements();
        }

        private void LoadAnnouncements()
        {
            var items = AnnouncementService.GetAnnouncementHistory();
            if (items.Count == 0)
            {
                items = NotificationCenterService.GetHistory("announcement")
                    .Select(x => new AnnouncementCenterItem
                    {
                        Id = string.IsNullOrWhiteSpace(x.AnnouncementId) ? x.Id : x.AnnouncementId,
                        Type = x.Type,
                        Level = x.Level,
                        Title = x.Title,
                        Summary = x.Summary,
                        Content = x.Content,
                        ActionUrl = x.ActionUrl,
                        CreatedAt = x.CreatedAt
                    })
                    .ToList();
            }

            var list = items.OrderByDescending(x => x.CreatedAt).ToList();
            AnnouncementListBox.ItemsSource = list;
            AnnouncementCountTextBlock.Text = GetCountText(list.Count);
            EmptyTextBlock.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            AnnouncementListBox.Visibility = list.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            AnnouncementListBox.SelectedIndex = list.Count == 0 ? -1 : 0;
            UpdateDetails(AnnouncementListBox.SelectedItem as AnnouncementCenterItem);
        }

        private string GetCountText(int count)
        {
            var template = Ink_Canvas.Properties.Strings.GetString("Announcement_ItemCount") ?? "共 {0} 条公告";
            return string.Format(template, count);
        }

        private void AnnouncementListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateDetails(AnnouncementListBox.SelectedItem as AnnouncementCenterItem);
        }

        private void UpdateDetails(AnnouncementCenterItem item)
        {
            var hasItem = item != null;
            DetailTitleTextBlock.Text = hasItem ? item.Title : string.Empty;
            DetailTypeTextBlock.Text = hasItem ? GetTypeText(item.Type) : string.Empty;
            DetailTimeTextBlock.Text = hasItem ? item.CreatedAt.ToString("yyyy-MM-dd HH:mm") : string.Empty;
            DetailSummaryTextBlock.Text = hasItem ? item.Summary : string.Empty;
            DetailContentTextBlock.Text = hasItem ? (string.IsNullOrWhiteSpace(item.Content) ? item.Summary : item.Content) : string.Empty;
        }

        private string GetTypeText(NotificationMessageType type)
        {
            var key = "Notification_Type_" + type;
            return Ink_Canvas.Properties.Strings.GetString(key) ?? type.ToString();
        }

        private void ViewDetailsButton_Click(object sender, RoutedEventArgs e)
        {
            if (AnnouncementListBox.SelectedItem is not AnnouncementCenterItem item) return;

            if (!string.IsNullOrWhiteSpace(item.ActionUrl))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(item.ActionUrl) { UseShellExecute = true });
                    return;
                }
                catch
                {
                }
            }

            MessageBox.Show(string.IsNullOrWhiteSpace(item.Content) ? item.Summary : item.Content, item.Title);
        }

        private void ClearHistoryButton_Click(object sender, RoutedEventArgs e)
        {
            AnnouncementService.ClearAnnouncementHistory();
            NotificationCenterService.ClearHistory("announcement");
            LoadAnnouncements();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadAnnouncements();
        }
    }
}
