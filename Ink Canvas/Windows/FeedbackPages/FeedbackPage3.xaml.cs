using System;
using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas.Windows.FeedbackPages
{
    /// <summary>
    /// 反馈页面3：反馈提交页面。
    /// 提供Markdown模板并允许用户复制或直接打开GitHub Issue页面。
    /// </summary>
    public partial class FeedbackPage3 : UserControl
    {
        public event EventHandler<RoutedEventArgs> BtnOpenGitHubIssueClick;
        public event EventHandler<RoutedEventArgs> CardCopyIssueUrlClick;
        public event EventHandler<RoutedEventArgs> BtnCopyMarkdownClick;

        public string MarkdownTemplate => TextBoxMarkdownTemplate.Text;

        public FeedbackPage3()
        {
            InitializeComponent();
            BtnOpenGitHubIssue.Click += (s, e) => BtnOpenGitHubIssueClick?.Invoke(this, e);
            CardCopyIssueUrl.Click += (s, e) => CardCopyIssueUrlClick?.Invoke(this, e);
            BtnCopyMarkdown.Click += (s, e) => BtnCopyMarkdownClick?.Invoke(this, e);
        }
    }
}
