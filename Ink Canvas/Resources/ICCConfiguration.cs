using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace Ink_Canvas.Resources.ICCConfiguration {
    public enum InitialPositionTypes {
        TopLeft, TopRight, BottomLeft, BottomRight, TopCenter, BottomCenter, Custom
    }
    public enum ElementCornerRadiusTypes {
        SuperEllipse, Circle, Custom, None
    }
    public class NearSnapAreaSize {
        public double[] TopLeft { get; set; } = {24,24};
        public double[] TopRight { get; set; } = {24,24};
        public double[] BottomLeft { get; set; } = {24,24};
        public double[] BottomRight { get; set; } = {24,24};
        public double TopCenter { get; set; } = 24;
        public double BottomCenter { get; set; } = 24;
    }
    public class ICCFloatingBarConfiguration {
        public bool SemiTransparent { get; set; } = false;
        public bool NearSnap { get; set; } = true;
        public InitialPositionTypes InitialPosition { get; set; } = InitialPositionTypes.BottomCenter;
        public Point InitialPositionPoint { get; set; } = new Point(0, 0);
        public double ElementCornerRadiusValue = 0;
        public ElementCornerRadiusTypes ElementCornerRadiusType { get; set; } = ElementCornerRadiusTypes.SuperEllipse;

        public bool ParallaxEffect { get; set; } = true;
        public bool MiniMode { get; set; } = false;
        public Color ClearButtonColor { get; set; } = Color.FromRgb(224, 27, 36);
        public Color ClearButtonPressColor { get; set; } = Color.FromRgb(254, 226, 226);
        public Color ToolButtonSelectedBgColor { get; set; } = Color.FromRgb(37, 99, 235);
        public double MovingLimitationNoSnap { get; set; } = 12;
        public double MovingLimitationSnapped { get; set; } = 24;

        public NearSnapAreaSize NearSnapAreaSize { get; set; } = new NearSnapAreaSize() {
            TopLeft = new double[] { 24, 24 },
            TopRight = new double[] { 24, 24 },
            BottomLeft = new double[] { 24, 24 },
            BottomRight = new double[] { 24, 24 },
        };

        public string[] ToolBarItemsInCursorMode { get; set; } = new string[] {
            "Cursor", "Pen", "Clear", "Separator", "Whiteboard", "Gesture", "Menu", "Fold"
        };
        public string[] ToolBarItemsInMiniMode { get; set; } = new string[] {
            "Cursor", "Pen", "Clear"
        };
        public string[] ToolBarItemsInAnnotationMode { get; set; } = new string[] {
            "Cursor", "Pen", "Clear", "Separator", "Eraser", "ShapeDrawing", "Select", "Separator", "Undo", "Redo", "Separator", "Whiteboard", "Gesture", "Menu", "Fold"
        };
    }

    public class ICCConfiguration {
        public ICCFloatingBarConfiguration FloatingBar { get; set; } = new ICCFloatingBarConfiguration();
    }
}
