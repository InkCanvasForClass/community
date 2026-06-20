using iNKORE.UI.WPF.Modern.Common.IconKeys;
using Ink_Canvas.Properties;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas.Controls.Toolbar.BoardToolbar.Items
{
    internal sealed class BoardPageInfoToolItem : IBoardToolbarItem
    {
        public string Id => "board.pageInfo";
        public string LocalizationKey => "Board_Page";
        public string DisplayName => Strings.GetString(LocalizationKey) ?? "页码";
        public string Description => "页码";
        public string IconGeometry => XamlGraphicsIconGeometries.PageInfoIconGeometry;
        public FontIconData? IconKey => null;
        public ButtonPosition DefaultPosition => ButtonPosition.Middle;

        public FrameworkElement BuildView(IBoardToolbarHost host)
        {
            return BuildPageInfoView(host, null);
        }

        internal static FrameworkElement BuildPageInfoView(IBoardToolbarHost host, string areaId)
        {
            var pageInfoTextBlock = new TextBlock
            {
                Text = "1/1",
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, -1, 0, 0),
                FontSize = 17,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center
            };
            if (areaId != null)
                host.RegisterView($"board.pageInfo.{areaId}", pageInfoTextBlock);
            else
                host.RegisterView("board.pageInfo", pageInfoTextBlock);

            var pageLabel = new TextBlock
            {
                Text = FloatingBarStrings.Board_Page,
                Foreground = (Brush)Application.Current.TryFindResource("FloatBarForeground"),
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Center,
                FontSize = 12
            };

            var grid = new Grid { Margin = new Thickness(6, 6, 6, 4) };
            grid.Children.Add(pageInfoTextBlock);
            grid.Children.Add(pageLabel);

            var pageInfoBorder = new Border
            {
                Width = 75,
                Height = 50,
                BorderThickness = new Thickness(0),
                Background = (Brush)Application.Current.TryFindResource("BoardFloatBarBackground"),
                Opacity = 1,
                Child = grid,
                Cursor = Cursors.Hand
            };
            if (areaId != null)
                host.RegisterView($"board.pageList.{areaId}Btn", pageInfoBorder);
            else
                host.RegisterView("board.pageListBtn", pageInfoBorder);
            return pageInfoBorder;
        }

        public void ApplyPosition(FrameworkElement view, ButtonPosition position)
        {
            if (view is Border border)
            {
                border.CornerRadius = position switch
                {
                    ButtonPosition.First => new CornerRadius(5, 0, 0, 5),
                    ButtonPosition.Last => new CornerRadius(0, 5, 5, 0),
                    ButtonPosition.Single => new CornerRadius(5),
                    _ => new CornerRadius(0)
                };
            }
        }
    }
}
