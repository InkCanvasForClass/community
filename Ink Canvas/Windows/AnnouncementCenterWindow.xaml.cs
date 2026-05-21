using Ink_Canvas.Helpers;
using Ink_Canvas.Models;
using iNKORE.UI.WPF.Modern.Controls;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;

namespace Ink_Canvas.Windows
{
    public partial class AnnouncementCenterWindow : Window
    {
        public AnnouncementCenterWindow()
        {
            InitializeComponent();
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

            AnnouncementListBox.ItemsSource = items;
            EmptyTextBlock.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void AnnouncementListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
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

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
