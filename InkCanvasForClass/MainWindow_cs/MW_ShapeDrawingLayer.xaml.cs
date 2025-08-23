using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Ink_Canvas {
    public partial class ShapeDrawingLayer : UserControl {

        public ShapeDrawingLayer() {
            InitializeComponent();

            // ToolbarMoveHandle
            ToolbarMoveHandle.MouseDown += ToolbarMoveHandle_MouseDown;
            ToolbarMoveHandle.MouseUp += ToolbarMoveHandle_MouseUp;
            ToolbarMoveHandle.MouseMove += ToolbarMoveHandle_MouseMove;
            UpdateToolbarPosition(ToolbarNowPosition);

            // Update ToolBtns
            ToolButtons = new Border[] {
                CursorButton,
                UndoButton,
                RedoButton,
                ClearButton,
                GridLineButton,
                SnapButton,
                MultiPointButton,
                InfoButton,
                MoreButton,
            };
            foreach (var tb in ToolButtons) {
                tb.MouseDown += ToolButton_MouseDown;
                tb.MouseUp += ToolButton_MouseUp;
                tb.MouseLeave += ToolButton_MouseLeave;
            }

            Toolbar.Visibility = Visibility.Collapsed;
            AngleTooltip.Visibility = Visibility.Collapsed;
            LengthTooltip.Visibility = Visibility.Collapsed;

            FullscreenGrid.MouseDown += FullscreenGrid_MouseDown;
            FullscreenGrid.MouseUp += FullscreenGrid_MouseUp;
            FullscreenGrid.MouseMove += FullscreenGrid_MouseMove;
        }

        private Point CaculateCenteredToolbarPosition() {
            var aw = Toolbar.Width;
            var ah = Toolbar.Height;
            var left = (MainWindow.ActualWidth - aw) / 2;
            var top = MainWindow.ActualHeight - ah - 128;
            return new Point(left, top);
        }

        public MainWindow MainWindow { get; set; }

        public Border[] ToolButtons = new Border[] { };
        public Point ToolbarNowPosition = new Point(0, 0);
        public bool IsToolbarMoveHandleDown = false;
        public Point MouseDownPointInHandle;
        public Border ToolButtonMouseDownBorder = null;

        public void UpdateToolbarPosition(Point? position) {
            if (position == null) {
                Toolbar.RenderTransform = null;
                return;
            }
            Toolbar.RenderTransform = new TranslateTransform(((Point)position).X, ((Point)position).Y);
        }

        private void ToolbarMoveHandle_MouseDown(object sender, MouseButtonEventArgs e) {
            if (IsToolbarMoveHandleDown) return;
            MouseDownPointInHandle = FullscreenGrid.TranslatePoint(e.GetPosition(null),ToolbarMoveHandle);
            IsToolbarMoveHandleDown = true;
            ToolbarMoveHandle.CaptureMouse();
        }

        private void ToolbarMoveHandle_MouseUp(object sender, MouseButtonEventArgs e) {
            if (!IsToolbarMoveHandleDown) return;
            IsToolbarMoveHandleDown = false;
            ToolbarMoveHandle.ReleaseMouseCapture();
        }

        private void ToolbarMoveHandle_MouseMove(object sender, MouseEventArgs e) {
            if (!IsToolbarMoveHandleDown) return;
            var ptInScreen = e.GetPosition(null);
            ToolbarNowPosition = new Point(ptInScreen.X - MouseDownPointInHandle.X, ptInScreen.Y - MouseDownPointInHandle.Y);
            UpdateToolbarPosition(ToolbarNowPosition);
        }

        private MainWindow.ShapeDrawingType? _shapeType;

        public bool IsInShapeDrawingMode {
            get => _shapeType != null;
        }

        public void StartShapeDrawing(MainWindow.ShapeDrawingType type, string name) {
            _shapeType = type;
            FullscreenGrid.Background = new SolidColorBrush(Color.FromArgb(1,0,0,0));
            FullscreenGrid.Visibility = Visibility.Visible;
            Toolbar.Visibility = Visibility.Visible;
            var pt = CaculateCenteredToolbarPosition();
            ToolbarNowPosition = pt;
            UpdateToolbarPosition(pt);
            ShapeDrawingTypeText.Text = name ?? "未知形状";
            PenSizeText.Text = $"{(MainWindow.inkCanvas.DefaultDrawingAttributes.Width + MainWindow.inkCanvas.DefaultDrawingAttributes.Height) / 2} 像素";
        }

        private void DoneButtonClicked(object sender, RoutedEventArgs e) {
            EndShapeDrawing();
        }

        public void EndShapeDrawing() {
            _shapeType = null;
            FullscreenGrid.Background = null;
            FullscreenGrid.Visibility = Visibility.Collapsed;
            Toolbar.Visibility = Visibility.Collapsed;
        }

        private void ToolButton_MouseDown(object sender, MouseButtonEventArgs e) {
            if (ToolButtonMouseDownBorder != null) return;
            ToolButtonMouseDownBorder = (Border)sender;
            ToolButtonMouseDownBorder.Background = new SolidColorBrush(Color.FromRgb(228, 228, 231));
        }

        private void ToolButton_MouseUp(object sender, MouseButtonEventArgs e) {
            if (ToolButtonMouseDownBorder == null || ToolButtonMouseDownBorder != sender) return;
            
            ToolButton_MouseLeave(sender, null);
        }

        private void ToolButton_MouseLeave(object sender, MouseEventArgs e) {
            if (ToolButtonMouseDownBorder == null || ToolButtonMouseDownBorder != sender) return;
            ToolButtonMouseDownBorder.Background = new SolidColorBrush(Colors.Transparent);
            ToolButtonMouseDownBorder = null;
        }

        private bool isFullscreenGridDown = false;
        public PointCollection points = new PointCollection();

        private void FullscreenGrid_MouseDown(object sender, MouseButtonEventArgs e) {
            if (isFullscreenGridDown) return;
            points.Clear();
            points.Add(e.GetPosition(null));
            isFullscreenGridDown = true;
            FullscreenGrid.CaptureMouse();
        }

        private void FullscreenGrid_MouseUp(object sender, MouseButtonEventArgs e) {
            if (!isFullscreenGridDown) return;
            FullscreenGrid.ReleaseMouseCapture();
            isFullscreenGridDown = false;
            if (_shapeType == null) return;
            using (DrawingContext dc = DrawingVisualCanvas.DrawingVisual.RenderOpen()) {}

            if (points.Count >= 2)
                MainWindow.inkCanvas.Strokes.Add(MainWindow.DrawShapeCore(points, (MainWindow.ShapeDrawingType)_shapeType,false,false));
            points.Clear();
            AngleTooltip.Visibility = Visibility.Collapsed;
            LengthTooltip.Visibility = Visibility.Collapsed;
        }

        private void FullscreenGrid_MouseMove(object sender, MouseEventArgs e) {
            if (!isFullscreenGridDown) return;
            if (_shapeType == null) return;
            if (points.Count >= 2) points[1] = e.GetPosition(null); 
                else points.Add(e.GetPosition(null));
            
            using (DrawingContext dc = DrawingVisualCanvas.DrawingVisual.RenderOpen()) {
                if ((_shapeType == MainWindow.ShapeDrawingType.Line ||
                       _shapeType == MainWindow.ShapeDrawingType.DashedLine ||
                       _shapeType == MainWindow.ShapeDrawingType.DottedLine ||
                       _shapeType == MainWindow.ShapeDrawingType.ArrowOneSide ||
                       _shapeType == MainWindow.ShapeDrawingType.ArrowTwoSide) && points.Count >= 2) {
                    MainWindow.DrawShapeCore(points, (MainWindow.ShapeDrawingType)_shapeType,true,true).Draw(dc);
                    var angle = MainWindow.ShapeDrawingHelper.CaculateRotateAngleByGivenTwoPoints(points[0], points[1]);
                    if (AngleTooltip.Visibility == Visibility.Collapsed) AngleTooltip.Visibility = Visibility.Visible;
                    AngleText.Text = $"{angle}°";
                    if (LengthTooltip.Visibility == Visibility.Collapsed) LengthTooltip.Visibility = Visibility.Visible;
                    LengthText.Text = $"{Math.Round(Math.Sqrt(Math.Pow((points[1].Y - points[0].Y), 2) + Math.Pow((points[1].X - points[0].X), 2)),2)} 像素";
                }
            }
        }
    }
}
