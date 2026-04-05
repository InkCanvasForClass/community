using System;
using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas.Windows.FeedbackPages
{
    /// <summary>
    /// 反馈页面3：反馈提交页面。
    /// 提供Markdown模板并允许用户复制或直接打开GitHub Issue页面。
    /// </summary>
    /// <remarks>
    /// 页面提供以下功能：
    /// - 显示生成的Markdown格式环境信息模板
    /// - 复制Markdown模板到剪贴板
    /// - 复制预填的GitHub Issue URL到剪贴板
    /// - 直接在浏览器中打开GitHub Issue创建页面
    /// </remarks>
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
