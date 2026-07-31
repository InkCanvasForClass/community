using Ink_Canvas.Properties;
using Ink_Canvas.Mathematics.Models;
using System.Windows.Controls;
using System.Windows;

namespace Ink_Canvas.Controls
{
    public partial class MathInsertPopupContent : UserControl
    {
        public Button CoordinatePlaneButtonControl => CoordinatePlaneButton;

        public Button PointButtonControl => PointButton;

        public Button SegmentButtonControl => SegmentButton;

        public Button TriangleButtonControl => TriangleButton;

        public Button LineButtonControl => LineButton;

        public Button RayButtonControl => RayButton;

        public Button CircleButtonControl => CircleButton;

        public Button LabelButtonControl => LabelButton;

        public Button AngleButtonControl => AngleButton;

        public Button SelectButtonControl => SelectButton;

        public Button DeleteButtonControl => DeleteButton;

        public Button HorizontalButtonControl => HorizontalButton;

        public Button VerticalButtonControl => VerticalButton;

        public Button ParallelButtonControl => ParallelButton;

        public Button PerpendicularButtonControl => PerpendicularButton;

        public Button EqualLengthButtonControl => EqualLengthButton;

        public Button CollinearButtonControl => CollinearButton;

        public Button PointOnLineButtonControl => PointOnLineButton;

        public Button PointOnCircleButtonControl => PointOnCircleButton;

        public Button FunctionButtonControl => FunctionButton;

        public Button EditFunctionButtonControl => EditFunctionButton;

        public TextBox FunctionExpressionInput => FunctionExpressionTextBox;

        public TextBox FunctionDomainMinInput => FunctionDomainMinTextBox;

        public TextBox FunctionDomainMaxInput => FunctionDomainMaxTextBox;

        public Button CubeButtonControl => CubeButton;

        public Button CuboidButtonControl => CuboidButton;

        public Button PrismButtonControl => PrismButton;

        public Button PyramidButtonControl => PyramidButton;

        public Button CylinderButtonControl => CylinderButton;

        public Button ConeButtonControl => ConeButton;

        public Button SphereButtonControl => SphereButton;

        public Button RotateSolidButtonControl => RotateSolidButton;

        public TextBox SolidLengthInput => SolidLengthTextBox;

        public TextBox SolidWidthInput => SolidWidthTextBox;

        public TextBox SolidHeightInput => SolidHeightTextBox;

        public TextBlock SolidLengthLabelControl => SolidLengthLabel;

        public TextBlock SolidWidthLabelControl => SolidWidthLabel;

        public TextBlock SolidHeightLabelControl => SolidHeightLabel;

        public FrameworkElement SolidLengthFieldControl => SolidLengthField;

        public FrameworkElement SolidWidthFieldControl => SolidWidthField;

        public FrameworkElement SolidHeightFieldControl => SolidHeightField;

        public Button SolidInsertConfirmButtonControl => SolidInsertConfirmButton;

        public Button SolidInsertCancelButtonControl => SolidInsertCancelButton;

        public Button CloseButtonControl => Shell.CloseButtonControl;

        public MathInsertPopupContent()
        {
            InitializeComponent();
            Shell.Title = Strings.GetString("Board_Math") ?? "Board_Math";
            CoordinatePlaneButton.Content = Strings.GetString("Math_CoordinatePlane") ?? "Math_CoordinatePlane";
            PointButton.Content = Strings.GetString("Math_Point") ?? "Math_Point";
            SegmentButton.Content = Strings.GetString("Math_Segment") ?? "Math_Segment";
            TriangleButton.Content = Strings.GetString("Math_Triangle") ?? "Math_Triangle";
            LineButton.Content = Strings.GetString("Math_Line") ?? "Math_Line";
            RayButton.Content = Strings.GetString("Math_Ray") ?? "Math_Ray";
            CircleButton.Content = Strings.GetString("Math_Circle") ?? "Math_Circle";
            LabelButton.Content = Strings.GetString("Math_Label") ?? "Math_Label";
            AngleButton.Content = Strings.GetString("Math_Angle") ?? "Math_Angle";
            SelectButton.Content = Strings.GetString("Math_Select") ?? "Math_Select";
            DeleteButton.Content = Strings.GetString("Math_Delete") ?? "Math_Delete";
            HorizontalButton.Content = Strings.GetString("Math_Horizontal") ?? "Math_Horizontal";
            VerticalButton.Content = Strings.GetString("Math_Vertical") ?? "Math_Vertical";
            ParallelButton.Content = Strings.GetString("Math_Parallel") ?? "Math_Parallel";
            PerpendicularButton.Content = Strings.GetString("Math_Perpendicular") ?? "Math_Perpendicular";
            EqualLengthButton.Content = Strings.GetString("Math_EqualLength") ?? "Math_EqualLength";
            CollinearButton.Content = Strings.GetString("Math_Collinear") ?? "Math_Collinear";
            PointOnLineButton.Content = Strings.GetString("Math_PointOnLine") ?? "Math_PointOnLine";
            PointOnCircleButton.Content = Strings.GetString("Math_PointOnCircle") ?? "Math_PointOnCircle";
            FunctionButton.Content = Strings.GetString("Math_Function") ?? "Math_Function";
            EditFunctionButton.Content = Strings.GetString("Math_EditFunction") ?? "Math_EditFunction";
            FunctionExpressionLabel.Text = Strings.GetString("Math_Expression") ?? "Math_Expression";
            CubeButton.Content = Strings.GetString("Math_Cube") ?? "Math_Cube";
            CuboidButton.Content = Strings.GetString("Math_Cuboid") ?? "Math_Cuboid";
            PrismButton.Content = Strings.GetString("Math_Prism") ?? "Math_Prism";
            PyramidButton.Content = Strings.GetString("Math_Pyramid") ?? "Math_Pyramid";
            CylinderButton.Content = Strings.GetString("Math_Cylinder") ?? "Math_Cylinder";
            ConeButton.Content = Strings.GetString("Math_Cone") ?? "Math_Cone";
            SphereButton.Content = Strings.GetString("Math_Sphere") ?? "Math_Sphere";
            RotateSolidButton.Content = Strings.GetString("Math_RotateSolid") ?? "Math_RotateSolid";
            GeometrySectionLabel.Text = Strings.GetString("Math_GroupGeometry") ?? "Math_GroupGeometry";
            ConstraintSectionLabel.Text = Strings.GetString("Math_GroupConstraints") ?? "Math_GroupConstraints";
            SolidSectionLabel.Text = Strings.GetString("Math_GroupSolids") ?? "Math_GroupSolids";
            SolidDimensionsTitle.Text = Strings.GetString("Math_SolidDimensions") ?? "立体尺寸";
            SolidLengthLabel.Text = Strings.GetString("Math_Length") ?? "长";
            SolidWidthLabel.Text = Strings.GetString("Math_Width") ?? "宽";
            SolidHeightLabel.Text = Strings.GetString("Math_Height") ?? "高";
            SolidInsertConfirmButton.Content = Strings.GetString("Math_Insert") ?? "插入";
            SolidInsertCancelButton.Content = Strings.GetString("Math_Cancel") ?? "取消";
            var innerContent = InnerContentHost.Content;
            InnerContentHost.Content = null;
            Shell.InnerContent = innerContent;
        }

        public void ShowSolidDimensions(SolidType solidType)
        {
            ConfigureSolidDimensions(solidType);
            SolidDimensionsPanel.Visibility = Visibility.Visible;
            SolidLengthTextBox.SelectAll();
            SolidLengthTextBox.Focus();
        }

        public void HideSolidDimensions()
        {
            SolidDimensionsPanel.Visibility = Visibility.Collapsed;
        }

        private void ConfigureSolidDimensions(SolidType solidType)
        {
            SolidDimensionsTitle.Text = Strings.GetString("Math_SolidDimensions") ?? "立体尺寸";
            switch (solidType)
            {
                case SolidType.Cube:
                    ConfigureDimensionField(SolidLengthField, SolidLengthLabel, SolidLengthTextBox, "Math_EdgeLength", "3");
                    HideDimensionField(SolidWidthField);
                    HideDimensionField(SolidHeightField);
                    break;
                case SolidType.Prism:
                    ConfigureDimensionField(SolidLengthField, SolidLengthLabel, SolidLengthTextBox, "Math_BaseLength", "3");
                    ConfigureDimensionField(SolidWidthField, SolidWidthLabel, SolidWidthTextBox, "Math_BaseHeight", "3");
                    ConfigureDimensionField(SolidHeightField, SolidHeightLabel, SolidHeightTextBox, "Math_PrismLength", "3");
                    break;
                case SolidType.Cylinder:
                case SolidType.Cone:
                    ConfigureDimensionField(SolidLengthField, SolidLengthLabel, SolidLengthTextBox, "Math_Radius", "1.5");
                    ConfigureDimensionField(SolidWidthField, SolidWidthLabel, SolidWidthTextBox, "Math_Height", "3");
                    HideDimensionField(SolidHeightField);
                    break;
                case SolidType.Sphere:
                    ConfigureDimensionField(SolidLengthField, SolidLengthLabel, SolidLengthTextBox, "Math_Radius", "1.5");
                    HideDimensionField(SolidWidthField);
                    HideDimensionField(SolidHeightField);
                    break;
                default:
                    ConfigureDimensionField(SolidLengthField, SolidLengthLabel, SolidLengthTextBox, "Math_Length", "3");
                    ConfigureDimensionField(SolidWidthField, SolidWidthLabel, SolidWidthTextBox, "Math_Width", "3");
                    ConfigureDimensionField(SolidHeightField, SolidHeightLabel, SolidHeightTextBox, "Math_Height", "3");
                    break;
            }
        }

        private static void HideDimensionField(UIElement field)
        {
            field.Visibility = Visibility.Collapsed;
        }

        private static void ConfigureDimensionField(
            UIElement field,
            TextBlock label,
            TextBox input,
            string labelKey,
            string defaultValue)
        {
            field.Visibility = Visibility.Visible;
            label.Text = Strings.GetString(labelKey) ?? labelKey;
            input.Text = defaultValue;
        }
    }
}
