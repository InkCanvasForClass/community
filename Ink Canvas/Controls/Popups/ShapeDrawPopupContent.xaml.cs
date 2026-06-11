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
        public GeometryButton DrawEllipseCenterWithFocalPointBtn => BoardImageDrawEllipseCenterWithFocalPoint;
        public GeometryButton DrawCuboidBtn => BoardImageDrawCuboid;
        public GeometryButton DrawRectangleBtn => BoardImageDrawRectangle;
        public GeometryButton DrawCylinderBtn => BoardImageDrawCylinder;
        public GeometryButton DrawConeBtn => BoardImageDrawCone;

        public GeometryButton DrawCoordinate1Btn => ImageDrawCoordinate1;
        public GeometryButton DrawCoordinate2Btn => ImageDrawCoordinate2;
        public GeometryButton DrawCoordinate3Btn => ImageDrawCoordinate3;
        public GeometryButton DrawCoordinate4Btn => ImageDrawCoordinate4;
        public GeometryButton DrawCoordinate5Btn => ImageDrawCoordinate5;
        public GeometryButton DrawHyperbolaBtn => ImageDrawHyperbola;
        public GeometryButton DrawHyperbolaWithFocalPointBtn => ImageDrawHyperbolaWithFocalPoint;
        public GeometryButton DrawParabola1Btn => ImageDrawParabola1;
        public GeometryButton DrawParabolaWithFocalPointBtn => ImageDrawParabolaWithFocalPoint;
        public GeometryButton DrawParabola2Btn => ImageDrawParabola2;

        public ToggleSwitch ShowCircleCenterToggle => ToggleSwitchShowCircleCenter;

        public Button CloseButtonControl => Shell?.CloseButtonControl;

        public ShapeDrawPopupContent()
        {
            InitializeComponent();
            Shell.InnerContent = InnerContentHost.Content;
        }
    }
}
