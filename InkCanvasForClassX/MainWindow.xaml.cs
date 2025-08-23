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
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using InkCanvasForClassX.Libraries;
using InkCanvasForClassX.Libraries.Stroke;

namespace InkCanvasForClassX
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {

        

        public static StylusPointCollection GenerateStylusPoints(int numberOfPoints, int maxX, int maxY)
        {
            Random random = new Random();
            StylusPointCollection points = new StylusPointCollection();

            for (int i = 0; i < numberOfPoints; i++)
            {
                double x = random.NextDouble() * maxX;
                double y = random.NextDouble() * maxY;
                float pressureFactor = 0.5F;

                StylusPoint point = new StylusPoint(x, y, pressureFactor);
                points.Add(point);
            }

            return points;
        }

        private bool isDragging = false;
        private Point startPoint;
        public MainWindow()
        {
            InitializeComponent();
            InkC.StrokeCollected += (object sender, InkCanvasStrokeCollectedEventArgs e) => {
                inkCanvas.InkStrokes = InkC.Strokes;
                //var stylusPtsList = new List<PerfectFreehandJint.StylusPointLite>();
                //foreach (var strokeStylusPoint in e.Stroke.StylusPoints) {
                //    stylusPtsList.Add(new PerfectFreehandJint.StylusPointLite()
                //    {
                //        x = Math.Round(strokeStylusPoint.X,2) ,
                //        y = Math.Round(strokeStylusPoint.Y,2),
                //        pressure = strokeStylusPoint.PressureFactor,
                //    });
                //}
                //var aaa = new PerfectFreehandJint();
                //var ccc = aaa.GetSVGPathStroke(stylusPtsList.ToArray(), new PerfectFreehandJint.StrokeOptions() {
                //    size = 16,
                //    thinning = 0.5,
                //    smoothing = 0.5,
                //    streamline = 0.5,
                //    simulatePressure = true,
                //    easing = (t)=>t,
                //    last = true,
                //    start = new PerfectFreehandJint.StrokeCapOptions() {
                //        cap = true,
                //        taper = 0,
                //        easing = (t)=>t,
                //    },
                //    end = new PerfectFreehandJint.StrokeCapOptions()
                //    {
                //        cap = true,
                //        taper = 0,
                //        easing = (t) => t,
                //    },
                //});
                //Trace.WriteLine(ccc);
            };

            InkC.MouseRightButtonDown += Inkcanv_MouseRightButtonDown;
            InkC.MouseRightButtonUp += Inkcanv_MouseRightButtonUp;
            InkC.MouseMove += Inkcanv_MouseMove;
            var a = GenerateStylusPoints(10, 1920, 1080);
            foreach (var strokePoint in a)
            {
                Trace.WriteLine($"point x:{strokePoint.X} point y:{strokePoint.Y}");
            }
            var c = PerfectFreehand.GetStrokePoints(a, new StrokeOptions() {
                Size = 16,
                Thinning = 0.5,
                Smoothing = 0.5,
                SimulatePressure = true,
            });
            var s = PerfectFreehand.GetStrokeOutlinePointsVectors(c, new StrokeOptions() {
                Size = 16,
                Thinning = 0.5,
                Smoothing = 0.5,
                SimulatePressure = true,
            });
            foreach (var strokePoint in c) {
                Trace.WriteLine($"point x:{strokePoint.Vector.X.ToString()} point y:{strokePoint.Vector.Y.ToString()}");
            }
            Trace.WriteLine(PerfectFreehand.ConvertVectorsToSVGPath(s));
        }

        private void Inkcanv_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            isDragging = true;
            startPoint = e.GetPosition(InkC);
            InkC.CaptureMouse();
        }

        private void Inkcanv_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            isDragging = false;
            InkC.ReleaseMouseCapture();
        }

        private void Inkcanv_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging && e.RightButton == MouseButtonState.Pressed)
            {
                Point currentPoint = e.GetPosition(InkC);
                System.Windows.Vector delta = currentPoint - startPoint;

                foreach (Stroke stroke in InkC.Strokes)
                {
                    stroke.Transform(new Matrix(1, 0, 0, 1, delta.X, delta.Y), false);
                }
                inkCanvas.InkStrokes = InkC.Strokes;

                startPoint = currentPoint;
            }
        }

        private void ButtonBase1_OnClick(object sender, RoutedEventArgs e) {
            InkC.EditingMode = InkCanvasEditingMode.None;
        }

        private void ButtonBase2_OnClick(object sender, RoutedEventArgs e) {
            InkC.EditingMode = InkCanvasEditingMode.Ink;
        }
    }
}
