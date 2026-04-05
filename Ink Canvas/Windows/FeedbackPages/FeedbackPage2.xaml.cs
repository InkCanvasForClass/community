using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas.Windows.FeedbackPages
{
    /// <summary>
    /// 反馈页面2：环境信息预览页面。
    /// 展示用户选择要包含在反馈中的系统环境信息。
    /// </summary>
    /// <remarks>
    /// 显示以下信息：
    /// - 软件版本信息
    /// - 系统信息（操作系统、.NET版本、触控支持）
    /// - 设备信息（设备ID、遥测ID）
    /// - 软件配置信息（PPT联动设置、墨迹识别设置）
    /// </remarks>
    public partial class FeedbackPage2 : UserControl
    {
        public FeedbackPage2()
        {
            InitializeComponent();
        }
    }
}
