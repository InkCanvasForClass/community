using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Ink_Canvas;
using Ink_Canvas.Controls;
using Ink_Canvas.Helpers;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using WpfInkCanvas = System.Windows.Controls.InkCanvas;

namespace InkCanvas.Calibration.Tests
{
    internal static class BoardRoamingTests
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static T Field<T>(object target, string name) =>
            (T)target.GetType().GetField(name, Members).GetValue(target);
        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, Members).SetValue(target, value);
        private static object Call(object target, string name, params object[] args) =>
            target.GetType().GetMethods(Members).Single(method => method.Name == name &&
                method.GetParameters().Length == args.Length).Invoke(target, args);
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class Contact : TouchDevice
        {
            public Point Position { get; set; }
            public WpfInkCanvas Canvas { get; set; }
            public Contact(int id, Point position) : base(id) => Position = position;
            public override TouchPoint GetTouchPoint(IInputElement relativeTo)
            {
                var point = ReferenceEquals(relativeTo, Canvas) ? InputPoint(Canvas, Position) : Position;
                return new TouchPoint(this, point, new Rect(point, new Size(1, 1)), TouchAction.Move);
            }
            public override TouchPointCollection GetIntermediateTouchPoints(IInputElement relativeTo) =>
                new TouchPointCollection { GetTouchPoint(relativeTo) };
        }

        private static ManipulationDeltaEventArgs ManipulationDeltaFor(WpfInkCanvas canvas, Vector translation)
        {
            var delta = new ManipulationDelta(translation, 0, new Vector(1, 1), new Vector());
            var velocities = new ManipulationVelocities(new Vector(), 0, new Vector());
            var args = (ManipulationDeltaEventArgs)typeof(ManipulationDeltaEventArgs)
                .GetConstructors(Members).Single().Invoke(new object[]
                { null, 0, canvas, new Point(), delta, delta, velocities, false });
            Set(args, "_manipulators", new IManipulator[] { new Contact(1, new Point(100, 100)) });
            return args;
        }

        public static void RunAll()
        {
            int failed = 0;
            foreach (var test in new Action[] { PreviewWithNegativeInk, PreviewWithEmptyVisual, PreviewWithPendingLayout,
                PreviewAfterImmediatePan, PreviewRejectsInvalidBounds, PreviewRetainsTransformedImages,
                PanDefersPointMutation, PanCommitsOneHistory, PanPreservesOriginalTransform,
                PanWithoutMovementHasNoHistory, PanCompletesOnCaptureLoss, PopupJumpBelongsToPan,
                TouchpadMovesInkWithoutChangingTool, TouchpadZoomKeepsAnchor, TouchpadRespectsFrozenPage,
                TouchpadCompletionCommitsHistory, WheelCompatibilityHandlesBothAxes,
                NativeTouchpadInitializesAndDisposes, NativeTouchpadCumulativeTransformBecomesDelta,
                TouchpadPreservesPreviouslyPannedImage, TouchpadWheelRespectsGestureSettings,
                TouchpadZoomRefreshesOpenNavigation, SharedMediaTransformPreservesCanvasCoordinates,
                SharedMediaTransformPreservesOtherElements, RoamingContinuesAfterImageTransformFailure,
                RepeatedCanvasTransformsKeepImagesEditable,
                PopupRepeatedPanKeepsInkVisible, PopupNearEdgeExpandsPreview,
                LateManipulationDoesNotStartRoaming, PipelineRebindPreservesRoamingMode })
            {
                try { test(); Console.WriteLine("PASS Roaming " + test.Method.Name); }
                catch (Exception ex)
                {
                    failed++;
                    Console.WriteLine("FAIL Roaming " + test.Method.Name + ": " + ex.GetBaseException());
                }
            }
            Check(failed == 0, $"Roaming: {failed} failures");
        }

        // Avoid the application's hardware, plugins and background services, but run its actual methods.
        private static MainWindow CreateWindow(bool popupOpen = true)
        {
            var window = (MainWindow)FormatterServices.GetUninitializedObject(typeof(MainWindow));
            var canvas = new WpfInkCanvas
            {
                Width = 800, Height = 450, Background = Brushes.Transparent,
                EditingMode = InkCanvasEditingMode.None, ClipToBounds = false
            };
            Set(window, "inkCanvas", canvas);
            Set(window, "GridBackgroundCover", new Grid { Background = Brushes.White });
            Set(window, "BoardRoamingPopupContent", new BoardRoamingPopupContent());
            Set(window, "BoardRoamingPopup", new Popup { IsOpen = popupOpen });
            Set(window, "_currentMode", 1);
            Set(window, "_currentToolMode", "roaming");
            Set(window, "_currentWhiteboardIndex", 1);
            Set(window, "frozenPages", new bool[101]);
            Set(window, "pageLastUserInkMutationUtc", new DateTime[101]);
            Set(window, "StrokeInitialHistory", new Dictionary<Stroke, StylusPointCollection>());
            Set(window, "timeMachine", new TimeMachine());
            foreach (string name in new[] { "dec", "_boardRoamingContactIds", "_boardRoamingContacts", "circles" })
            {
                var field = typeof(MainWindow).GetField(name, Members);
                field.SetValue(window, Activator.CreateInstance(field.FieldType));
            }
            Set(window, "_boardRoamingPrimaryContactId", -1);
            Set(window, "_boardRoamingWorldBounds", Rect.Empty);
            canvas.Measure(new Size(800, 450));
            canvas.Arrange(new Rect(0, 0, 800, 450));
            canvas.UpdateLayout();
            return window;
        }

        private static void AddInk(MainWindow window, Stroke stroke, bool trackHistory = false)
        {
            Field<WpfInkCanvas>(window, "inkCanvas").Strokes.Add(stroke);
            Field<Dictionary<Stroke, StylusPointCollection>>(window, "StrokeInitialHistory")[stroke] = stroke.StylusPoints.Clone();
            if (trackHistory)
            {
                var handler = (EventHandler)Delegate.CreateDelegate(typeof(EventHandler), window,
                    typeof(MainWindow).GetMethod("Stroke_StylusPointsChanged", Members));
                stroke.StylusPointsChanged += handler;
            }
        }

        private static Stroke StrokeAt(double x, double y) => new Stroke(new StylusPointCollection
        {
            new StylusPoint(x, y), new StylusPoint(x + 40, y + 20)
        }, new DrawingAttributes { Color = Colors.Black, Width = 16, Height = 16 });

        private static BitmapSource Refresh(MainWindow window, bool updateBounds = true)
        {
            Exception renderException = null;
            EventHandler<FirstChanceExceptionEventArgs> handler = (sender, e) =>
            {
                if (e.Exception.StackTrace?.Contains("RenderBoardRoamingPreview", StringComparison.Ordinal) == true)
                    renderException = e.Exception;
            };
            AppDomain.CurrentDomain.FirstChanceException += handler;
            try
            {
                Call(window, "RefreshBoardRoamingPopup", updateBounds);
                var bitmap = Field<BoardRoamingPopupContent>(window, "BoardRoamingPopupContent")
                    .PreviewImageControl.Source as BitmapSource;
                if (bitmap == null && renderException != null)
                    Console.WriteLine("Roaming render exception: " + renderException);
                Check(bitmap != null, "Roaming preview was cleared or could not be generated");
                Check(bitmap.PixelWidth == 352 && bitmap.PixelHeight == 198, "Roaming preview has unexpected dimensions");
                return bitmap;
            }
            finally { AppDomain.CurrentDomain.FirstChanceException -= handler; }
        }

        private static bool HasDarkPixel(BitmapSource bitmap, Point position)
        {
            int stride = bitmap.PixelWidth * 4;
            var pixels = new byte[stride * bitmap.PixelHeight];
            bitmap.CopyPixels(pixels, stride, 0);
            int x = (int)Math.Round(position.X);
            int y = (int)Math.Round(position.Y);
            for (int py = Math.Max(0, y - 3); py <= Math.Min(bitmap.PixelHeight - 1, y + 3); py++)
            for (int px = Math.Max(0, x - 3); px <= Math.Min(bitmap.PixelWidth - 1, x + 3); px++)
            {
                int index = py * stride + px * 4;
                if (pixels[index + 3] > 128 && pixels[index] < 160 && pixels[index + 1] < 160 && pixels[index + 2] < 160)
                    return true;
            }
            return false;
        }

        private static Point PreviewPoint(MainWindow window, Point canvasPoint)
        {
            var world = Field<Rect>(window, "_boardRoamingWorldBounds");
            var viewport = Field<Point>(window, "_boardRoamingViewportWorldPosition");
            var offset = Field<Point>(window, "_boardRoamingPreviewOffset");
            double scale = Field<double>(window, "_boardRoamingPreviewScale");
            return new Point(offset.X + (canvasPoint.X + viewport.X - world.X) * scale,
                offset.Y + (canvasPoint.Y + viewport.Y - world.Y) * scale);
        }

        private static void PreviewWithNegativeInk()
        {
            foreach (var position in new[] { new Point(200, 150), new Point(-600, -400),
                new Point(-2000, -1500), new Point(1200, 900) })
            {
                var window = CreateWindow();
                var stroke = StrokeAt(position.X, position.Y);
                AddInk(window, stroke);
                var bitmap = Refresh(window);
                Check(HasDarkPixel(bitmap, PreviewPoint(window, position)),
                    $"Preview lost ink at {position}, world={Field<Rect>(window, "_boardRoamingWorldBounds")}");
                Field<Popup>(window, "BoardRoamingPopup").IsOpen = false;
            }
        }

        private static void PreviewWithEmptyVisual()
        {
            foreach (var visibility in new[] { Visibility.Visible, Visibility.Hidden, Visibility.Collapsed })
            foreach (bool clipEmpty in new[] { false, true })
            {
                var window = CreateWindow();
                var canvas = Field<WpfInkCanvas>(window, "inkCanvas");
                Refresh(window);
                canvas.Background = null;
                canvas.Visibility = visibility;
                if (clipEmpty) canvas.Clip = new RectangleGeometry(Rect.Empty);
                Refresh(window);
                Field<Popup>(window, "BoardRoamingPopup").IsOpen = false;
            }
        }

        private static void PreviewWithPendingLayout()
        {
            foreach (double x in new[] { -2000d, 2000d, double.NaN, double.PositiveInfinity })
            {
                var window = CreateWindow();
                var canvas = Field<WpfInkCanvas>(window, "inkCanvas");
                AddInk(window, StrokeAt(200, 150));
                var child = new Border { Width = 80, Height = 40, Background = Brushes.Red,
                    RenderTransform = new TranslateTransform(x, -100) };
                canvas.Children.Add(child);
                canvas.InvalidateMeasure();
                Refresh(window);
                canvas.UpdateLayout();
                Refresh(window);
                Field<Popup>(window, "BoardRoamingPopup").IsOpen = false;
            }
        }

        private static void PreviewAfterImmediatePan()
        {
            var window = CreateWindow();
            var stroke = StrokeAt(200, 150);
            AddInk(window, stroke);
            Refresh(window);
            Call(window, "BeginBoardRoaming", new Point(100, 100));
            var canvas = Field<WpfInkCanvas>(window, "inkCanvas");
            for (int i = 1; i <= 12; i++)
            {
                Call(window, "MoveBoardRoaming", InputPoint(canvas, new Point(100 - i * 100, 100 - i * 80)));
                var bitmap = Refresh(window, false);
                Check(HasDarkPixel(bitmap, PreviewPoint(window, canvas.RenderTransform.Value.Transform(stroke.StylusPoints[0].ToPoint()))),
                    $"Preview lost ink during immediate pan step {i}");
            }
            Call(window, "EndBoardRoaming");
            Field<Popup>(window, "BoardRoamingPopup").IsOpen = false;
        }

        private static Point InputPoint(WpfInkCanvas canvas, Point hostPoint)
        {
            var matrix = VisualTreeHelper.GetTransform(canvas)?.Value ?? Matrix.Identity;
            var offset = VisualTreeHelper.GetOffset(canvas);
            matrix.Translate(offset.X, offset.Y);
            matrix.Invert();
            return matrix.Transform(hostPoint);
        }

        private static void PanDefersPointMutation()
        {
            var window = CreateWindow(false);
            var stroke = StrokeAt(200, 150);
            AddInk(window, stroke);
            int changed = 0;
            stroke.StylusPointsChanged += (sender, e) => changed++;
            var canvas = Field<WpfInkCanvas>(window, "inkCanvas");
            Call(window, "BeginBoardRoaming", InputPoint(canvas, new Point(100, 100)));
            for (int i = 1; i <= 30; i++)
            {
                Call(window, "MoveBoardRoaming", InputPoint(canvas, new Point(100 + i * 2, 100 + i)));
                if (i % 3 == 0) canvas.UpdateLayout();
            }
            Check(changed == 0, $"Move rewrote stroke points {changed} times instead of translating only the display layer");
            Check(stroke.StylusPoints[0].X == 200 && stroke.StylusPoints[0].Y == 150, "Move changed persisted ink before commit");
            Call(window, "EndBoardRoaming");
            Check(stroke.StylusPoints[0].X == 260 && stroke.StylusPoints[0].Y == 180, "Pan did not commit the final accumulated delta");
            Check(canvas.RenderTransform.Value.IsIdentity, "Pan left a temporary display transform behind");
        }

        private static void PanCommitsOneHistory()
        {
            var window = CreateWindow(false);
            AddInk(window, StrokeAt(200, 150), true);
            AddInk(window, StrokeAt(400, 250), true);
            var canvas = Field<WpfInkCanvas>(window, "inkCanvas");
            Call(window, "BeginBoardRoaming", InputPoint(canvas, new Point(100, 100)));
            for (int i = 1; i <= 3; i++)
                Call(window, "MoveBoardRoaming", InputPoint(canvas, new Point(100 + i * 20, 100 + i * 10)));
            Call(window, "EndBoardRoaming");
            var history = Field<TimeMachine>(window, "timeMachine").ExportTimeMachineHistory();
            Check(history.Length == 1, $"A single pan produced {history.Length} undo records");
            Check(history[0].StylusPointDictionary.Count == 2, "Pan history omitted strokes");
            var machine = Field<TimeMachine>(window, "timeMachine");
            Call(window, "ApplyHistoryToCanvas", machine.Undo(), null, null);
            Check(canvas.Strokes[0].StylusPoints[0].X == 200 && canvas.Strokes[0].StylusPoints[0].Y == 150,
                "One undo did not restore the entire pan");
            Call(window, "ApplyHistoryToCanvas", machine.Redo(), null, null);
            Check(canvas.Strokes[0].StylusPoints[0].X == 260 && canvas.Strokes[0].StylusPoints[0].Y == 180,
                "One redo did not restore the entire pan");
        }

        private static void PreviewRejectsInvalidBounds()
        {
            var window = CreateWindow();
            var previous = Refresh(window);
            foreach (var bounds in new[] { Rect.Empty, new Rect(), new Rect(double.NaN, 0, 100, 100),
                new Rect(0, 0, double.PositiveInfinity, 100) })
                Check(Call(window, "RenderBoardRoamingPreview", bounds, 352d, 198d) == null,
                    "Invalid world bounds were passed into the WPF renderer");
            foreach (double width in new[] { 0d, -1d, double.NaN, double.PositiveInfinity })
                Check(Call(window, "RenderBoardRoamingPreview", new Rect(0, 0, 800, 450), width, 198d) == null,
                    "Invalid preview dimensions were passed into the WPF renderer");
            var canvas = Field<WpfInkCanvas>(window, "inkCanvas");
            canvas.Visibility = Visibility.Collapsed;
            Call(window, "RefreshBoardRoamingPopup", true);
            Check(ReferenceEquals(previous, Field<BoardRoamingPopupContent>(window, "BoardRoamingPopupContent")
                .PreviewImageControl.Source), "Unavailable layout cleared the last valid preview");
            Field<Popup>(window, "BoardRoamingPopup").IsOpen = false;
        }

        private static void PreviewRetainsTransformedImages()
        {
            var window = CreateWindow();
            var canvas = Field<WpfInkCanvas>(window, "inkCanvas");
            var image = new Image
            {
                Width = 160, Height = 80,
                Source = new DrawingImage(new GeometryDrawing(Brushes.Black, null,
                    new RectangleGeometry(new Rect(0, 0, 160, 80)))),
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new TransformGroup
                {
                    Children = new TransformCollection { new ScaleTransform(1.4, 0.8),
                        new RotateTransform(30), new TranslateTransform(-600, -200) }
                }
            };
            WpfInkCanvas.SetLeft(image, 100);
            WpfInkCanvas.SetTop(image, 80);
            canvas.Children.Add(image);
            canvas.UpdateLayout();
            var matrix = image.RenderTransform.Value;
            var center = image.TransformToAncestor(canvas).Transform(new Point(80, 40));
            Check(HasDarkPixel(Refresh(window), PreviewPoint(window, center)),
                "Preview lost or double-transformed an offscreen rotated image");
            Check(image.RenderTransform.Value == matrix, "Preview rewrote the image transform");
            Call(window, "BeginBoardRoaming", new Point(100, 100));
            Call(window, "MoveBoardRoaming", InputPoint(canvas, new Point(160, 130)));
            Call(window, "EndBoardRoaming");
            canvas.UpdateLayout();
            var movedCenter = image.TransformToAncestor(canvas).Transform(new Point(80, 40));
            Check((movedCenter - center - new Vector(60, 30)).Length < 0.001, "Pan lost the image's original transform");
            Check(HasDarkPixel(Refresh(window), PreviewPoint(window, movedCenter)), "Preview lost the image after pan commit");
            Field<Popup>(window, "BoardRoamingPopup").IsOpen = false;
        }

        private static void PanPreservesOriginalTransform()
        {
            var window = CreateWindow(false);
            var canvas = Field<WpfInkCanvas>(window, "inkCanvas");
            var original = new ScaleTransform(1.5, 1.5);
            canvas.RenderTransform = original;
            canvas.UpdateLayout();
            var stroke = StrokeAt(200, 150);
            AddInk(window, stroke);
            Call(window, "BeginBoardRoaming", InputPoint(canvas, new Point(150, 150)));
            for (int i = 1; i <= 3; i++)
                Call(window, "MoveBoardRoaming", InputPoint(canvas, new Point(150 + i * 20, 150 + i * 10)));
            Call(window, "EndBoardRoaming");
            Check(Math.Abs(stroke.StylusPoints[0].X - 240) < 0.001 && Math.Abs(stroke.StylusPoints[0].Y - 170) < 0.001,
                "A pre-existing display transform caused coordinate feedback during pan");
            Check(ReferenceEquals(canvas.RenderTransform, original), "Pan did not restore the original display transform");
        }

        private static void PanWithoutMovementHasNoHistory()
        {
            var window = CreateWindow(false);
            AddInk(window, StrokeAt(200, 150), true);
            Call(window, "BeginBoardRoaming", new Point(100, 100));
            Call(window, "EndBoardRoaming");
            Check(Field<TimeMachine>(window, "timeMachine").ExportTimeMachineHistory().Length == 0,
                "A click without movement created undo history");
            Check(Field<WpfInkCanvas>(window, "inkCanvas").RenderTransform.Value.IsIdentity,
                "A click without movement retained a temporary transform");
        }

        private static void PanCompletesOnCaptureLoss()
        {
            var window = CreateWindow(false);
            var canvas = Field<WpfInkCanvas>(window, "inkCanvas");
            var stroke = StrokeAt(200, 150);
            AddInk(window, stroke, true);
            Call(window, "BeginBoardRoaming", new Point(100, 100));
            Call(window, "MoveBoardRoaming", InputPoint(canvas, new Point(160, 130)));
            canvas.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0)
                { RoutedEvent = Mouse.LostMouseCaptureEvent, Source = canvas });
            Check(!Field<bool>(window, "_isBoardRoamingPointerDown") && canvas.RenderTransform.Value.IsIdentity,
                "Capture loss left an active pan or temporary display transform");
            Check(stroke.StylusPoints[0].X == 260 && stroke.StylusPoints[0].Y == 180, "Capture loss discarded the visible pan");
        }

        private static void PopupJumpBelongsToPan()
        {
            var window = CreateWindow();
            AddInk(window, StrokeAt(200, 150), true);
            Refresh(window);
            Call(window, "AttachBoardRoamingPopupEvents");
            var popup = Field<BoardRoamingPopupContent>(window, "BoardRoamingPopupContent");
            var inputDevice = Enum.Parse(typeof(BoardRoamingPopupContent).GetNestedType("DragInputDevice", BindingFlags.NonPublic), "Mouse");
            Call(popup, "BeginDrag", new Point(5, 5), inputDevice);
            Call(popup, "EndDrag");
            Check(Field<TimeMachine>(window, "timeMachine").ExportTimeMachineHistory().Length == 1,
                "Clicking outside the viewport was omitted from the pan's undo history");
            Check(Field<WpfInkCanvas>(window, "inkCanvas").RenderTransform.Value.IsIdentity,
                "Popup pan left a display transform behind");
            Field<Popup>(window, "BoardRoamingPopup").IsOpen = false;
        }

        private static void TouchpadMovesInkWithoutChangingTool()
        {
            var window = CreateWindow(false);
            Set(window, "_currentToolMode", "pen");
            var stroke = StrokeAt(200, 150);
            AddInk(window, stroke, true);
            Call(window, "ApplyBoardTouchpadDelta", new Matrix(1, 0, 0, 1, 30, -20));
            Check(stroke.StylusPoints[0].X == 230 && stroke.StylusPoints[0].Y == 130,
                "Touchpad must pan both axes while the pen tool is selected");
            Check(Field<string>(window, "_currentToolMode") == "pen", "Touchpad changed the selected tool");
            Call(window, "EndBoardTouchpadGesture");
        }

        private static void NativeTouchpadInitializesAndDisposes()
        {
            using var source = new HwndSource(new HwndSourceParameters("ICC touchpad regression")
                { Width = 800, Height = 450, WindowStyle = 0 });
            var options = (Translate: true, Zoom: true);
            using var input = new TouchpadManipulation(source, () => false, () => options,
                delta => throw new InvalidOperationException("Idle viewport emitted a gesture"), () => { });
            foreach (var enabled in new[] { (true, false), (false, true), (true, true), (false, false) })
            {
                options = enabled;
                Check(input.ConfigureGestures() == (enabled.Item1 || enabled.Item2),
                    "Native viewport failed to configure enabled gestures or accepted all-disabled input");
            }
            input.Stop();
            input.Stop();
        }

        private static void NativeTouchpadCumulativeTransformBecomesDelta()
        {
            var delta = TouchpadManipulation.GetDelta(new Matrix(2, 0, 0, 2, -100, -100),
                new Matrix(4, 0, 0, 4, -300, -300));
            var result = delta.Transform(new Point(300, 200));
            Check(result == new Point(500, 300), "Native cumulative transform was applied twice");
        }

        private static void TouchpadPreservesPreviouslyPannedImage()
        {
            var window = CreateWindow(false);
            var canvas = Field<WpfInkCanvas>(window, "inkCanvas");
            var image = new Image { Width = 100, Height = 80,
                RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = new RotateTransform(20) };
            WpfInkCanvas.SetLeft(image, 200);
            WpfInkCanvas.SetTop(image, 100);
            canvas.Children.Add(image);
            canvas.UpdateLayout();
            Call(window, "BeginBoardRoaming", new Point(100, 100));
            Call(window, "MoveBoardRoaming", InputPoint(canvas, new Point(160, 130)));
            Call(window, "EndBoardRoaming");
            canvas.UpdateLayout();
            var before = image.TransformToAncestor(canvas).Transform(new Point(50, 40));
            Call(window, "ApplyBoardTouchpadDelta", new Matrix(2, 0, 0, 2, -100, -100));
            canvas.UpdateLayout();
            var after = image.TransformToAncestor(canvas).Transform(new Point(50, 40));
            Check((after - new Point(before.X * 2 - 100, before.Y * 2 - 100)).Length < 0.001,
                "Touchpad discarded the image's prior pan/rotation or scaled around the wrong origin");
            Call(window, "EndBoardTouchpadGesture");
        }

        private static void SharedMediaTransformPreservesCanvasCoordinates()
        {
            var window = CreateWindow(false);
            var canvas = Field<WpfInkCanvas>(window, "inkCanvas");
            foreach (var element in new FrameworkElement[] { new Image(), new MediaElement(), new CanvasMediaControl() })
            {
                element.Width = 100;
                element.Height = 80;
                element.RenderTransformOrigin = new Point(0.5, 0.5);
                element.RenderTransform = new RotateTransform(20);
                WpfInkCanvas.SetLeft(element, 200);
                WpfInkCanvas.SetTop(element, 100);
                canvas.Children.Add(element);
                canvas.UpdateLayout();
                var points = new[] { new Point(), new Point(50, 40), new Point(100, 80) };
                var before = points.Select(p => element.TransformToAncestor(canvas).Transform(p)).ToArray();

                // In canvas coordinates: translate (30,-20), scale 2x at (100,100), then rotate 90 degrees there.
                Call(window, "TransformCanvasImages", new Matrix(1, 0, 0, 1, 30, -20));
                Call(window, "TransformCanvasImages", new Matrix(2, 0, 0, 2, -100, -100));
                Call(window, "TransformCanvasImages", new Matrix(0, 1, -1, 0, 200, 0));
                canvas.UpdateLayout();
                for (int i = 0; i < points.Length; i++)
                {
                    var after = element.TransformToAncestor(canvas).Transform(points[i]);
                    Check((after - new Point(340 - 2 * before[i].Y, 2 * before[i].X - 40)).Length < 0.001,
                        $"{element.GetType().Name} lost its previous transform or used the wrong canvas origin");
                }
                canvas.Children.Remove(element);
            }
        }

        private static void SharedMediaTransformPreservesOtherElements()
        {
            var window = CreateWindow(false);
            var canvas = Field<WpfInkCanvas>(window, "inkCanvas");
            var shared = new TransformGroup();
            shared.Children.Add(new RotateTransform(20));
            shared.Children.Add(new TranslateTransform(10, 5));
            var image = new Image { RenderTransform = shared };
            var unrelated = new Border { RenderTransform = shared };
            canvas.Children.Add(image);
            canvas.Children.Add(unrelated);
            var original = shared.Value;
            Call(window, "TransformCanvasImages", new Matrix(1, 0, 0, 1, 30, -20));
            Check(unrelated.RenderTransform.Value == original,
                "Transforming a media element mutated a transform shared with another element");
            Check(image.RenderTransform.Value.OffsetX == original.OffsetX + 30
                && image.RenderTransform.Value.OffsetY == original.OffsetY - 20,
                "Shared transform prevented the image from moving");
        }

        private static void RepeatedCanvasTransformsKeepImagesEditable()
        {
            foreach (bool roaming in new[] { false, true })
            {
                var window = CreateWindow(false);
                var canvas = Field<WpfInkCanvas>(window, "inkCanvas");
                var group = new TransformGroup();
                group.Children.Add(new ScaleTransform());
                group.Children.Add(new TranslateTransform());
                group.Children.Add(new RotateTransform());
                var image = new Image { Width = 100, Height = 80, RenderTransform = group };
                canvas.Children.Add(image);
                canvas.UpdateLayout();
                for (int i = 0; i < 100; i++)
                {
                    var delta = new Matrix(1, 0, 0, 1, 1, -1);
                    if (roaming) Call(window, "TransformBoardRoamingContent", delta, new Vector(1, -1), 1.0);
                    else Call(window, "TransformCanvasImages", delta);
                }
                Call(window, "ApplyTranslateTransform", image, 10.0, 5.0);
                Check(image.RenderTransform.Value.OffsetX == 110 && image.RenderTransform.Value.OffsetY == -95,
                    "Canvas navigation prevented subsequent image editing");
                Check(image.RenderTransform is TransformGroup result && result.Children.Count <= 4,
                    "Repeated canvas navigation grew the transform list per input frame");
            }
        }

        private sealed class RejectingTransformImage : Image
        {
            public bool RejectTransform { get; set; }
            public int Rejections { get; private set; }

            static RejectingTransformImage()
            {
                RenderTransformProperty.OverrideMetadata(typeof(RejectingTransformImage),
                    new FrameworkPropertyMetadata(null, null, (target, value) =>
                    {
                        var image = (RejectingTransformImage)target;
                        if (image.RejectTransform)
                        {
                            image.Rejections++;
                            throw new InvalidOperationException("Simulated image transform failure");
                        }
                        return value;
                    }));
            }
        }

        private static void RoamingContinuesAfterImageTransformFailure()
        {
            var window = CreateWindow(false);
            var canvas = Field<WpfInkCanvas>(window, "inkCanvas");
            var first = new Image { Width = 100, Height = 80 };
            var broken = new RejectingTransformImage { Width = 100, Height = 80 };
            var last = new Image { Width = 100, Height = 80 };
            canvas.Children.Add(first);
            canvas.Children.Add(broken);
            canvas.Children.Add(last);
            canvas.UpdateLayout();
            broken.RejectTransform = true;
            try
            {
                Call(window, "TransformBoardRoamingContent", new Matrix(1, 0, 0, 1, 30, -20), new Vector(30, -20), 1.0);
                Check(broken.Rejections == 1, "Test did not exercise a failed element transform");
                foreach (var image in new[] { first, last })
                    Check(image.RenderTransform.Value.OffsetX == 30 && image.RenderTransform.Value.OffsetY == -20,
                        "A failing image prevented another image from moving");
                Check(Field<Point>(window, "_boardRoamingViewportWorldPosition") == new Point(-30, 20),
                    "A failing image interrupted viewport bookkeeping");
            }
            finally { broken.RejectTransform = false; }
        }

        private static void TouchpadZoomKeepsAnchor()
        {
            var window = CreateWindow(false);
            var stroke = StrokeAt(200, 150);
            AddInk(window, stroke);
            // Two successive 2x deltas around (100, 100): (200,150) -> (300,200) -> (500,300).
            Call(window, "ApplyBoardTouchpadDelta", new Matrix(2, 0, 0, 2, -100, -100));
            Call(window, "ApplyBoardTouchpadDelta", new Matrix(2, 0, 0, 2, -100, -100));
            Check(stroke.StylusPoints[0].X == 500 && stroke.StylusPoints[0].Y == 300,
                "Successive pinch deltas drifted from the zoom anchor");
            Call(window, "EndBoardTouchpadGesture");
        }

        private static void TouchpadRespectsFrozenPage()
        {
            var window = CreateWindow(false);
            var stroke = StrokeAt(200, 150);
            AddInk(window, stroke);
            Field<bool[]>(window, "frozenPages")[1] = true;
            Call(window, "ApplyBoardTouchpadDelta", new Matrix(2, 0, 0, 2, 30, 20));
            Check(stroke.StylusPoints[0].X == 200 && stroke.StylusPoints[0].Y == 150,
                "Touchpad mutated a frozen page");
            Check(Field<TimeMachine>(window, "timeMachine").ExportTimeMachineHistory().Length == 0,
                "Frozen-page gesture created history");
        }

        private static void TouchpadCompletionCommitsHistory()
        {
            var window = CreateWindow(false);
            var stroke = StrokeAt(200, 150);
            AddInk(window, stroke, true);
            Call(window, "ApplyBoardTouchpadDelta", new Matrix(1, 0, 0, 1, 10, 0));
            Call(window, "ApplyBoardTouchpadDelta", new Matrix(1, 0, 0, 1, 20, 0));
            Call(window, "EndBoardTouchpadGesture");
            var machine = Field<TimeMachine>(window, "timeMachine");
            Check(machine.ExportTimeMachineHistory().Length == 1, "One touchpad gesture must commit one pan history");
            Call(window, "ApplyHistoryToCanvas", machine.Undo(), null, null);
            Check(stroke.StylusPoints[0].X == 200, "Undo did not restore the start of the gesture");
        }

        private static void WheelCompatibilityHandlesBothAxes()
        {
            var window = CreateWindow(false);
            Set(window, "_currentToolMode", "pen");
            var stroke = StrokeAt(200, 150);
            AddInk(window, stroke);
            Check((bool)Call(window, "HandleBoardTouchpadWheel", 120, false, false, new Point(100, 100)),
                "Whiteboard vertical wheel was not consumed");
            Check(stroke.StylusPoints[0].Y > 150, "Positive vertical wheel must move content down");
            Check((bool)Call(window, "HandleBoardTouchpadWheel", 120, true, false, new Point(100, 100)),
                "Whiteboard horizontal wheel was not consumed");
            Check(stroke.StylusPoints[0].X < 200, "Positive horizontal wheel must move content left");
            Call(window, "EndBoardTouchpadGesture");
            Set(window, "_currentMode", 0);
            Check(!(bool)Call(window, "HandleBoardTouchpadWheel", 120, false, true, new Point()),
                "Desktop annotation wheel route was intercepted");
        }

        private static void TouchpadWheelRespectsGestureSettings()
        {
            var previous = MainWindow.Settings;
            try
            {
                foreach (var tool in new[] { "pen", "eraser", "roaming" })
                foreach (bool translate in new[] { false, true })
                foreach (bool zoom in new[] { false, true })
                foreach (bool control in new[] { false, true })
                foreach (bool horizontal in new[] { false, true })
                {
                    MainWindow.Settings = new Settings();
                    var gestures = MainWindow.Settings.Gesture;
                    gestures.IsEnableTwoFingerTranslateBoard = translate;
                    // Opposite values catch accidentally reading the other tool's settings.
                    gestures.IsEnableTwoFingerZoomBoard = tool == "roaming" ? !zoom : zoom;
                    gestures.IsEnableTwoFingerZoomRoaming = tool == "roaming" ? zoom : !zoom;
                    var window = CreateWindow(false);
                    Set(window, "_currentToolMode", tool);
                    var stroke = StrokeAt(200, 150);
                    AddInk(window, stroke, true);
                    bool allowed = control ? zoom : tool == "roaming" || translate;
                    Check((bool)Call(window, "HandleBoardTouchpadWheel", 120, horizontal, control, new Point(100, 100)),
                        "Disabled gestures must still consume wheel input to prevent passthrough");
                    bool moved = stroke.StylusPoints[0].ToPoint() != new Point(200, 150);
                    Check(moved == allowed, $"{tool}: translate={translate}, zoom={zoom}, pinch={control} ignored settings");
                    Call(window, "EndBoardTouchpadGesture");
                    if (!allowed)
                        Check(Field<TimeMachine>(window, "timeMachine").ExportTimeMachineHistory().Length == 0,
                            "Disabled gesture created undo history");
                }
            }
            finally { MainWindow.Settings = previous; }
        }

        private static void TouchpadZoomRefreshesOpenNavigation()
        {
            var window = CreateWindow();
            AddInk(window, StrokeAt(1200, 150));
            Refresh(window);
            var popup = Field<BoardRoamingPopupContent>(window, "BoardRoamingPopupContent");
            var hint = Field<TextBlock>(popup, "ScaleHintText");
            var before = hint.Text;
            try
            {
                Call(window, "ApplyBoardTouchpadDelta", new Matrix(2, 0, 0, 2, 0, 0));
                var liveHint = hint.Text;
                Check(liveHint != before, "Open navigation scale did not update during pinch");
                Refresh(window);
                Check(hint.Text == liveHint, "Navigation scale only becomes correct after reopening/rebuilding bounds");
            }
            finally
            {
                Call(window, "EndBoardTouchpadGesture");
                Field<Popup>(window, "BoardRoamingPopup").IsOpen = false;
            }
        }

        private static void CheckInkInPreview(MainWindow window, string step)
        {
            var canvas = Field<WpfInkCanvas>(window, "inkCanvas");
            var popup = Field<BoardRoamingPopupContent>(window, "BoardRoamingPopupContent");
            var world = Field<Rect>(window, "_boardRoamingWorldBounds");
            var viewport = Field<Point>(window, "_boardRoamingViewportWorldPosition");
            var pending = Field<Vector>(window, "_boardRoamingPendingTranslation");
            var bitmap = (BitmapSource)popup.PreviewImageControl.Source;
            foreach (var stroke in canvas.Strokes)
            {
                var bounds = stroke.GetBounds();
                bounds.Offset(viewport.X + pending.X, viewport.Y + pending.Y);
                Check(world.Contains(bounds), $"{step}: ink left the popup preview bounds; ink={bounds}, world={world}");
                var point = stroke.StylusPoints[0].ToPoint() + pending;
                Check(HasDarkPixel(bitmap, PreviewPoint(window, point)), $"{step}: popup preview lost ink pixels");
            }
        }

        private static void PopupRepeatedPanKeepsInkVisible()
        {
            var window = CreateWindow();
            AddInk(window, StrokeAt(200, 150), true);
            Refresh(window);
            Call(window, "AttachBoardRoamingPopupEvents");
            var popup = Field<BoardRoamingPopupContent>(window, "BoardRoamingPopupContent");
            var popupHost = Field<Popup>(window, "BoardRoamingPopup");
            popupHost.Child = popup;
            popup.UpdateLayout();
            var inputDevice = Enum.Parse(typeof(BoardRoamingPopupContent)
                .GetNestedType("DragInputDevice", BindingFlags.NonPublic), "Mouse");
            try
            {
                for (int round = 1; round <= 3; round++)
                {
                    var viewport = Field<Rect>(window, "_boardRoamingViewportInPreview");
                    var point = new Point(viewport.X + viewport.Width / 2, viewport.Y + viewport.Height / 2);
                    Call(popup, "BeginDrag", point, inputDevice);
                    Check(Field<bool>(popup, "_isDragging"), "Popup test could not capture the mouse for a real drag");
                    Call(popup, "MoveDrag", point + new Vector(352, 198));
                    CheckInkInPreview(window, $"Popup drag {round} before release");
                    Call(popup, "EndDrag");
                    // Run the same refresh as the queued post-drag callback, without timing dependencies.
                    Call(window, "RefreshBoardRoamingPopup", false, true);
                    CheckInkInPreview(window, $"Popup drag {round} after release");
                }
            }
            finally
            {
                Call(popup, "EndDrag");
                popupHost.IsOpen = false;
            }
        }

        private static void PopupNearEdgeExpandsPreview()
        {
            var window = CreateWindow();
            AddInk(window, StrokeAt(200, 150));
            Refresh(window);
            Call(window, "AttachBoardRoamingPopupEvents");
            var popup = Field<BoardRoamingPopupContent>(window, "BoardRoamingPopupContent");
            var popupHost = Field<Popup>(window, "BoardRoamingPopup");
            popupHost.Child = popup;
            popup.UpdateLayout();
            var inputDevice = Enum.Parse(typeof(BoardRoamingPopupContent)
                .GetNestedType("DragInputDevice", BindingFlags.NonPublic), "Mouse");
            var world = Field<Rect>(window, "_boardRoamingWorldBounds");
            var viewport = Field<Rect>(window, "_boardRoamingViewportInPreview");
            var movement = Field<Rect>(window, "_boardRoamingPreviewMovementBounds");
            try
            {
                Call(popup, "BeginDrag", new Point(viewport.X + viewport.Width / 2,
                    viewport.Y + viewport.Height / 2), inputDevice);
                Check(Field<bool>(popup, "_isDragging"), "Popup expansion test could not start a drag");
                Call(popup, "SetViewportPosition", movement.Right - viewport.Width * 1.05, viewport.Y);
                Call(window, "RefreshBoardRoamingPopup", false, true);
                var expanded = Field<Rect>(window, "_boardRoamingWorldBounds");
                Check(expanded.Right > world.Right && expanded.Width > world.Width,
                    $"Dragging near the popup edge did not expand its display area: before={world}, after={expanded}");
                Check(Field<bool>(popup, "_isDragging"), "Expanding the preview ended the drag");
                CheckInkInPreview(window, "Popup edge expansion");
            }
            finally
            {
                Call(popup, "EndDrag");
                popupHost.IsOpen = false;
            }
        }

        private static void LateManipulationDoesNotStartRoaming()
        {
            var window = CreateWindow(false);
            var canvas = Field<WpfInkCanvas>(window, "inkCanvas");
            AddInk(window, StrokeAt(200, 150));
            Set(window, "isInMultiTouchMode", true);
            var args = ManipulationDeltaFor(canvas, new Vector(800, 450));
            try
            {
                Call(window, "Main_Grid_ManipulationDelta", canvas, args);
                Check(!Field<bool>(window, "_isBoardRoamingPointerDown") && canvas.RenderTransform.Value.IsIdentity,
                    $"A remaining multi-touch manipulation started an unowned pan: {canvas.RenderTransform.Value}");
                Check(canvas.Strokes[0].StylusPoints[0].X == 200, "A late manipulation rewrote committed ink");
            }
            finally { Call(window, "EndBoardRoaming"); }
        }

        private static void PipelineRebindPreservesRoamingMode()
        {
            var previous = SettingsManager.Settings;
            SettingsManager.Settings = new Settings();
            SettingsManager.Settings.Canvas.InkStyle = 0;
            var window = CreateWindow(false);
            var canvas = Field<WpfInkCanvas>(window, "inkCanvas");
            try
            {
                foreach (bool multiTouch in new[] { true, false })
                {
                    Set(window, "isInMultiTouchMode", multiTouch);
                    Call(window, "EnsureRealtimeStylusPipelineBinding");
                    Check(canvas.EditingMode == InkCanvasEditingMode.None,
                        $"Rebinding the multi-touch pipeline changed roaming to {canvas.EditingMode}, multiTouch={multiTouch}");
                }
            }
            finally { SettingsManager.Settings = previous; }
        }

        public static void Benchmark()
        {
            foreach (int count in new[] { 1000, 5000, 10000 })
            foreach (bool popupOpen in new[] { false, true })
            {
                var window = CreateWindow(popupOpen);
                var canvas = Field<WpfInkCanvas>(window, "inkCanvas");
                int changes = 0;
                for (int i = 0; i < count; i++)
                {
                    var points = new StylusPointCollection();
                    double x = i % 100 * 6;
                    double y = i / 100 % 50 * 6;
                    for (int j = 0; j < 64; j++)
                        points.Add(new StylusPoint(x + j * 0.3, y + Math.Sin(j * 0.2) * 2));
                    var stroke = new Stroke(points);
                    AddInk(window, stroke, true);
                    stroke.StylusPointsChanged += (sender, e) => changes++;
                }
                canvas.UpdateLayout();
                if (popupOpen) Refresh(window);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                long allocated = GC.GetAllocatedBytesForCurrentThread();
                var stopwatch = Stopwatch.StartNew();
                Call(window, "BeginBoardRoaming", InputPoint(canvas, new Point(100, 100)));
                double beginMs = stopwatch.Elapsed.TotalMilliseconds;
                var samples = new double[30];
                for (int i = 1; i <= samples.Length; i++)
                {
                    stopwatch.Restart();
                    Call(window, "MoveBoardRoaming", InputPoint(canvas, new Point(100 + i * 2, 100 + i)));
                    samples[i - 1] = stopwatch.Elapsed.TotalMilliseconds;
                }
                long moveAllocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
                int moveChanges = changes;
                stopwatch.Restart();
                Call(window, "EndBoardRoaming");
                double endMs = stopwatch.Elapsed.TotalMilliseconds;
                int histories = Field<TimeMachine>(window, "timeMachine").ExportTimeMachineHistory().Length;
                Array.Sort(samples);
                Console.WriteLine($"BENCH Roaming strokes={count} popup={popupOpen} begin={beginMs:F2}ms " +
                    $"move-p50={samples[15]:F2}ms move-p95={samples[28]:F2}ms move-max={samples[29]:F2}ms " +
                    $"begin+move-alloc={moveAllocated / 1024.0 / 1024.0:F2}MiB move-changes={moveChanges} " +
                    $"total-changes={changes} end={endMs:F2}ms histories={histories}");
                Field<Popup>(window, "BoardRoamingPopup").IsOpen = false;
            }
        }
    }
}
