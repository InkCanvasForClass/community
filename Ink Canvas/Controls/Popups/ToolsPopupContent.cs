using System.Windows.Controls;

namespace Ink_Canvas.Controls
{
    public abstract class ToolsPopupContent : UserControl
    {
        public abstract ToolMenuButton TimerBtn { get; }
        public abstract ToolMenuButton RandomDrawBtn { get; }
        public abstract ToolMenuButton SingleDrawBtn { get; }
        public abstract ToolMenuButton SaveBtn { get; }
        public abstract ToolMenuButton OpenBtn { get; }
        public abstract ToolMenuButton ReplayBtn { get; }
        public abstract ToolMenuButton ScreenshotBtn { get; }
        public abstract ToolMenuButton ShapeDrawBtn { get; }
        public abstract ToolMenuButton RedoBtn { get; }
        public abstract ToolMenuButton ManualBtn { get; }
        public abstract ToolMenuButton SettingsBtn { get; }

        public abstract Button CloseButtonControl { get; }

        public ToolMenuButton GetButtonByItemId(string itemId)
        {
            return itemId switch
            {
                "timer" => TimerBtn,
                "randomDraw" => RandomDrawBtn,
                "singleDraw" => SingleDrawBtn,
                "save" => SaveBtn,
                "open" => OpenBtn,
                "replay" => ReplayBtn,
                "screenshot" => ScreenshotBtn,
                "shapeDraw" => ShapeDrawBtn,
                "redo" => RedoBtn,
                "manual" => ManualBtn,
                "settings" => SettingsBtn,
                _ => null
            };
        }
    }
}
