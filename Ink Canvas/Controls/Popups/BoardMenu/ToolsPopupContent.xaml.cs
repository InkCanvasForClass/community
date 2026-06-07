using Ink_Canvas.Controls.Toolbar;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas.Controls
{
    public partial class BoardToolsPopupContent : ToolsPopupContent
    {
        public override ToolMenuButton TimerBtn => TimerToolBtn;
        public override ToolMenuButton RandomDrawBtn => RandomDrawToolBtn;
        public override ToolMenuButton SingleDrawBtn => SingleDrawToolBtn;
        public override ToolMenuButton SaveBtn => SaveToolBtn;
        public override ToolMenuButton OpenBtn => OpenToolBtn;
        public override ToolMenuButton ReplayBtn => ReplayToolBtn;
        public override ToolMenuButton ScreenshotBtn => ScreenshotToolBtn;
        public override ToolMenuButton ShapeDrawBtn => ShapeDrawToolBtn;
        public override ToolMenuButton RedoBtn => RedoToolBtn;
        public override ToolMenuButton ManualBtn => ManualToolBtn;
        public override ToolMenuButton SettingsBtn => SettingsToolBtn;

        public override Button CloseButtonControl => Shell?.CloseButtonControl;

        public BoardToolsPopupContent()
        {
            InitializeComponent();
            Shell.InnerContent = InnerContentHost.Content;
            ApplyMenuLayout();
        }

        private Dictionary<string, ToolMenuButton> GetButtonMap()
        {
            return new Dictionary<string, ToolMenuButton>
            {
                { "timer", TimerToolBtn },
                { "randomDraw", RandomDrawToolBtn },
                { "singleDraw", SingleDrawToolBtn },
                { "save", SaveToolBtn },
                { "open", OpenToolBtn },
                { "replay", ReplayToolBtn },
                { "screenshot", ScreenshotToolBtn },
                { "shapeDraw", ShapeDrawToolBtn },
                { "redo", RedoToolBtn },
                { "manual", ManualToolBtn },
                { "settings", SettingsToolBtn },
            };
        }

        public void ApplyMenuLayout()
        {
            var layout = ToolsMenuRegistry.LoadBoardConfig();
            var items = layout.BoardItems;
            if (items == null || items.Count == 0) return;

            var buttonMap = GetButtonMap();

            // Hide all buttons and detach from current parent
            foreach (var kvp in buttonMap)
            {
                kvp.Value.Visibility = Visibility.Collapsed;
                if (kvp.Value.Parent is Panel panel)
                    panel.Children.Remove(kvp.Value);
            }

            // Clear existing rows from MenuPanel
            MenuPanel.Children.Clear();

            // Build rows of 3
            for (int i = 0; i < items.Count; i += 3)
            {
                var row = new iNKORE.UI.WPF.Controls.SimpleStackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 3
                };
                for (int j = 0; j < 3 && i + j < items.Count; j++)
                {
                    if (buttonMap.TryGetValue(items[i + j], out var btn))
                    {
                        btn.Visibility = Visibility.Visible;
                        row.Children.Add(btn);
                    }
                }
                MenuPanel.Children.Add(row);
            }
        }
    }
}
