using iNKORE.UI.WPF.Modern.Controls;
using System.Windows.Controls;

namespace Ink_Canvas.Controls
{
    public partial class ShapeDrawPopupContent : UserControl
    {
        public GeometryButton DrawLineBtn => BoardImageDrawLine;
        public GeometryButton DrawDashedLineBtn => BoardImageDrawDashedLine;
        public GeometryButton DrawDotLineBtn => BoardImageDrawDotLine;
        public GeometryButton DrawArrowBtn => BoardImageDrawArrow;
        public GeometryButton DrawParallelLineBtn => BoardImageDrawParallelLine;
        public GeometryButton DrawRectangleCenterBtn => BoardImageDrawRectangleCenter;
        public GeometryButton DrawCircleBtn => BoardImageDrawCircle;
        public GeometryButton DrawDashedCircleBtn => BoardImageDrawDashedCircle;
        public GeometryButton DrawEllipseCenterBtn => BoardImageDrawEllipseCenter;
        public GeometryButton DrawCuboidBtn => BoardImageDrawCuboid;
        public GeometryButton DrawRectangleBtn => BoardImageDrawRectangle;
        public GeometryButton DrawCylinderBtn => BoardImageDrawCylinder;
        public GeometryButton DrawConeBtn => BoardImageDrawCone;

        public FontIcon CloseFontIcon => TitleBar?.CloseFontIcon;

        public ShapeDrawPopupContent()
        {
            InitializeComponent();
        }
    }
}
