using System;
using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas.Windows.FeedbackPages
{
    /// <summary>
    /// 反馈页面3：反馈提交页面。
    /// 提供 Pastebin 上传、GitHub Issue 跳转、Markdown 模板复制。
    /// </summary>
    public partial class FeedbackPage3 : UserControl
    {
        public event EventHandler<RoutedEventArgs> BtnOpenGitHubIssueClick;
        public event EventHandler<RoutedEventArgs> CardCopyIssueUrlClick;
        public event EventHandler<RoutedEventArgs> BtnCopyMarkdownClick;
        public event EventHandler<RoutedEventArgs> BtnUploadPastebinClick;
        public event EventHandler<RoutedEventArgs> BtnCopyPasteUrlClick;

        public string MarkdownTemplate => TextBoxMarkdownTemplate.Text;
        public string PastebinUrl => TextBoxPastebinUrl.Text?.Trim();

        public FeedbackPage3()
        {
            InitializeComponent();
            BtnOpenGitHubIssue.Click += (s, e) => BtnOpenGitHubIssueClick?.Invoke(this, e);
            CardCopyIssueUrl.Click += (s, e) => CardCopyIssueUrlClick?.Invoke(this, e);
            BtnCopyMarkdown.Click += (s, e) => BtnCopyMarkdownClick?.Invoke(this, e);
            BtnUploadPastebin.Click += (s, e) => BtnUploadPastebinClick?.Invoke(this, e);
            BtnCopyPasteUrl.Click += (s, e) => BtnCopyPasteUrlClick?.Invoke(this, e);
        }
    }
}
