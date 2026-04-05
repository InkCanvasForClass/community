using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas.Windows.FeedbackPages
{
    /// <summary>
    /// 反馈页面1：环境信息选择页面。
    /// 允许用户选择要包含在反馈中的系统环境信息。
    /// </summary>
    /// <remarks>
    /// 用户可以选择包含以下信息：
    /// - 软件版本和更新通道
    /// - 操作系统版本、.NET版本和触控支持
    /// - 设备ID和遥测ID
    /// - PPT联动设置和墨迹识别设置
    /// </remarks>
    public partial class FeedbackPage1 : UserControl
    {
        public FeedbackPage1()
        {
            InitializeComponent();
        }
    }
}
