using Ink_Canvas.Controls;
using Ink_Canvas.Properties;
using System.Windows;
using System.Windows.Controls.Primitives;

namespace Ink_Canvas
{
    /// <summary>
    /// MainWindow 的小白板相关功能 partial class
    /// 管理浮窗小白板的打开、关闭、状态，以及白板按钮右键二级菜单
    /// </summary>
    public partial class MainWindow
    {
        /// <summary>
        /// 小白板窗口实例
        /// </summary>
        internal MiniWhiteboardWindow _miniWhiteboardWindow;

        /// <summary>
        /// 小白板浮动工具栏按钮引用
        /// </summary>
        internal ToolbarImageButton MiniWhiteboardToolBtn { get; private set; }

        /// <summary>
        /// 绑定小白板浮动工具栏按钮
        /// </summary>
        internal void AttachMiniWhiteboardBtn(ToolbarImageButton btn)
        {
            MiniWhiteboardToolBtn = btn;
        }

        /// <summary>
        /// 绑定白板二级菜单事件（由 MW 初始化流程调用）
        /// </summary>
        internal void WireUpWhiteboardModeSelectionEvents()
        {
            WhiteboardModeSelectionContent.FullWhiteboardClick += (s, e) =>
            {
                WhiteboardModeSelectionPopup.IsOpen = false;
                ImageBlackboard_MouseUp(null, null);
            };
            WhiteboardModeSelectionContent.MiniWhiteboardClick += (s, e) =>
            {
                WhiteboardModeSelectionPopup.IsOpen = false;
                ToggleMiniWhiteboard();
            };

            // 墨迹选择栏「插入白板」菜单事件
            InsertToWhiteboardContent.FullWhiteboardClick += (s, e) =>
            {
                InsertToWhiteboardPopup.IsOpen = false;
                ExecuteInsertStrokesToRegularWhiteboard();
            };
            InsertToWhiteboardContent.MiniWhiteboardClick += (s, e) =>
            {
                InsertToWhiteboardPopup.IsOpen = false;
                ExecuteInsertStrokesToMiniWhiteboard();
            };
        }

        /// <summary>
        /// 切换小白板窗口的显示/隐藏状态
        /// </summary>
        internal void ToggleMiniWhiteboard()
        {
            Settings.MiniWhiteboard ??= new MiniWhiteboardSettings();
            if (!Settings.MiniWhiteboard.IsEnabled)
            {
                ShowNotification(FloatingBarStrings.MiniWhiteboard_Settings_Enable);
                return;
            }

            if (_miniWhiteboardWindow != null && _miniWhiteboardWindow.IsLoaded)
            {
                if (_miniWhiteboardWindow.IsVisible)
                {
                    _miniWhiteboardWindow.Hide();
                }
                else
                {
                    _miniWhiteboardWindow.Show();
                    _miniWhiteboardWindow.Activate();
                }
            }
            else
            {
                OpenMiniWhiteboard();
            }
        }

        /// <summary>
        /// 打开小白板窗口
        /// </summary>
        internal void OpenMiniWhiteboard()
        {
            if (_miniWhiteboardWindow != null && _miniWhiteboardWindow.IsLoaded)
            {
                _miniWhiteboardWindow.Show();
                _miniWhiteboardWindow.Activate();
                return;
            }

            _miniWhiteboardWindow = new MiniWhiteboardWindow();
            _miniWhiteboardWindow.Owner = this;
            _miniWhiteboardWindow.Show();
        }

        /// <summary>
        /// 关闭小白板窗口
        /// </summary>
        internal void CloseMiniWhiteboard()
        {
            if (_miniWhiteboardWindow != null && _miniWhiteboardWindow.IsLoaded)
            {
                _miniWhiteboardWindow.Close();
            }
            _miniWhiteboardWindow = null;
        }

        /// <summary>
        /// 显示白板按钮的右键二级菜单
        /// </summary>
        internal void ShowWhiteboardModeSelectionPopup(FrameworkElement placementTarget)
        {
            WhiteboardModeSelectionPopup.PlacementTarget = placementTarget;
            WhiteboardModeSelectionPopup.Placement = PlacementMode.Bottom;
            WhiteboardModeSelectionPopup.IsOpen = true;
        }
    }
}
