using Ink_Canvas.Helpers;
using Ink_Canvas.Models;
using Ink_Canvas.Properties;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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
            var template = AnnouncementStrings.ItemCount;
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
            DetailContentTextBlock.Text = hasItem ? (string.IsNullOrWhiteSpace(item.Content) ? item.Summary : item.Content) : string.Empty;

            if (hasItem)
            {
                AnnouncementService.MarkAsRead(SettingsManager.Settings, item.Id);
                item.IsRead = true;
                item.IsNew = false;
                (Window.GetWindow(this) as SettingsWindow)?.UpdateAnnouncementUnreadBadge();
            }
        }

        private string GetTypeText(NotificationMessageType type)
        {
            return type switch
            {
                NotificationMessageType.Update => NotificationStrings.Type_Update,
                NotificationMessageType.Urgent => NotificationStrings.Type_Urgent,
                NotificationMessageType.Important => NotificationStrings.Type_Important,
                NotificationMessageType.Reminder => NotificationStrings.Type_Reminder,
                NotificationMessageType.Other => NotificationStrings.Type_Other,
                _ => type.ToString()
            };
        }

        private void ClearHistoryButton_Click(object sender, RoutedEventArgs e)
        {
            AnnouncementService.ClearAnnouncementHistory();
            NotificationCenterService.ClearHistory("announcement");
            LoadAnnouncements();
        }

        private void MarkAllAsReadButton_Click(object sender, RoutedEventArgs e)
        {
            AnnouncementService.MarkAllAsRead(SettingsManager.Settings);
            (Window.GetWindow(this) as SettingsWindow)?.UpdateAnnouncementUnreadBadge();
            LoadAnnouncements();
        }
    }
}
