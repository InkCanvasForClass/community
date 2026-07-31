using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Ink_Canvas.Controls;
using Ink_Canvas.Mathematics.Models;
using Ink_Canvas.Mathematics.Rendering;
using Ink_Canvas.Mathematics.Services;
using Ink_Canvas.Properties;

namespace InkCanvas.Math.UiSmoke.Tests
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            _ = new Application
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown
            };
            MathPopupHostsInteractiveContent();
            SolidDimensionsUseTypeAppropriateFields();
            MathMenuSourceWiresEveryButton();
            MathInteractionStringsResolve();
            MathInputHostPreemptsInkCanvas();
            CoordinatePlaneRendersBehindLaterObjects();
            StructuredSceneProjectsToGroupedNativeStrokes();
            FrontViewSkipsDegenerateSolidStrokes();
            SphereUsesContinuousOuterContour();
            ConeUsesProjectedSilhouetteGenerators();
            FunctionMarkersRenderCoordinateLabelsAndIntersections();
            RepeatedFunctionRenderingUsesCachedGeometry();
            Console.WriteLine("Math WPF UI smoke tests passed: 11.");
        }

        private static void MathInteractionStringsResolve()
        {
            var keys = new[]
            {
                "Math_EditObject",
                "Math_MeasureObject",
                "Math_ResetView",
                "Math_FunctionZero",
                "Math_FunctionExtremum",
                "Math_FunctionIntersection",
                "Math_SnapHint"
            };
            foreach (var key in keys)
            {
                var value = Strings.GetString(key);
                True(
                    !string.IsNullOrWhiteSpace(value) &&
                    !value.StartsWith("#key:", StringComparison.Ordinal),
                    $"Math interaction string {key} is unresolved.");
            }
        }

        private static void MathPopupHostsInteractiveContent()
        {
            var popup = new MathInsertPopupContent();
            var window = ShowOffscreen(popup, new Size(900, 600));
            try
            {
                var shell = FindVisualChildren<PopupShellContent>(popup).Single();
                True(shell.InnerContent != null, "Math popup shell has no inner content.");

                var buttons = FindVisualChildren<Button>(popup)
                    .Where(button => ReferenceEquals(
                        button.Style,
                        popup.FindResource("MathToolButtonStyle")))
                    .ToList();
                Equal(29, buttons.Count, "Math popup button count changed unexpectedly.");
                for (var i = 0; i < buttons.Count; i++)
                {
                    var button = buttons[i];
                    True(button.IsHitTestVisible, $"Math button {i} is not hit-test visible.");
                    True(button.ActualWidth > 0 && button.ActualHeight > 0, $"Math button {i} was not laid out.");
                    var center = button.TranslatePoint(
                        new Point(button.ActualWidth / 2, button.ActualHeight / 2),
                        popup);
                    var hit = popup.InputHitTest(center) as DependencyObject;
                    True(
                        IsSelfOrDescendant(hit, button),
                        $"Math button {i} cannot be hit at {center}; hit={hit?.GetType().FullName ?? "null"}.");

                    var label = FindVisualChildren<TextBlock>(button).Single();
                    var labelLeft = label.TranslatePoint(new Point(0, 0), button).X;
                    var labelRight = label.TranslatePoint(
                        new Point(label.ActualWidth, 0),
                        button).X;
                    True(
                        labelLeft >= 0 && labelRight <= button.ActualWidth,
                        $"Math button {i} label renders outside its hit area.");
                    Equal(
                        TextTrimming.CharacterEllipsis,
                        label.TextTrimming,
                        $"Math button {i} label no longer trims inside its hit area.");

                    var clickCount = 0;
                    button.Click += (_, _) => clickCount++;
                    button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
                    Equal(1, clickCount, $"Math button {i} does not raise a native Click event.");
                }
            }
            finally
            {
                window.Close();
            }
        }

        private static void SolidDimensionsUseTypeAppropriateFields()
        {
            var popup = new MathInsertPopupContent();
            var window = ShowOffscreen(popup, new Size(900, 600));
            try
            {
                popup.ShowSolidDimensions(SolidType.Cube);
                Equal(Strings.GetString("Math_EdgeLength"), popup.SolidLengthLabelControl.Text, "Cube does not show edge length.");
                Equal(Visibility.Collapsed, popup.SolidWidthFieldControl.Visibility, "Cube unexpectedly shows a second dimension.");
                Equal(Visibility.Collapsed, popup.SolidHeightFieldControl.Visibility, "Cube unexpectedly shows a third dimension.");

                popup.ShowSolidDimensions(SolidType.Cylinder);
                Equal(Strings.GetString("Math_Radius"), popup.SolidLengthLabelControl.Text, "Cylinder does not show radius.");
                Equal(Strings.GetString("Math_Height"), popup.SolidWidthLabelControl.Text, "Cylinder does not show height.");
                Equal(Visibility.Visible, popup.SolidWidthFieldControl.Visibility, "Cylinder height is hidden.");
                Equal(Visibility.Collapsed, popup.SolidHeightFieldControl.Visibility, "Cylinder unexpectedly shows a third dimension.");

                popup.ShowSolidDimensions(SolidType.Prism);
                Equal(Strings.GetString("Math_BaseLength"), popup.SolidLengthLabelControl.Text, "Prism does not show base length.");
                Equal(Strings.GetString("Math_BaseHeight"), popup.SolidWidthLabelControl.Text, "Prism does not show base height.");
                Equal(Strings.GetString("Math_PrismLength"), popup.SolidHeightLabelControl.Text, "Prism does not show prism length.");
            }
            finally
            {
                window.Close();
            }
        }

        private static void MathInputHostPreemptsInkCanvas()
        {
            var host = new Grid();
            var mathInkCanvas = new System.Windows.Controls.InkCanvas
            {
                Background = Brushes.Transparent,
                EditingMode = InkCanvasEditingMode.None,
                IsHitTestVisible = false
            };
            var inkCanvas = new System.Windows.Controls.InkCanvas
            {
                Background = Brushes.Transparent
            };
            host.Children.Add(mathInkCanvas);
            host.Children.Add(inkCanvas);

            var hostPreviewCount = 0;
            var inkPreviewCount = 0;
            host.AddHandler(
                Mouse.PreviewMouseDownEvent,
                new MouseButtonEventHandler((_, e) =>
                {
                    hostPreviewCount++;
                    e.Handled = true;
                }));
            inkCanvas.AddHandler(
                Mouse.PreviewMouseDownEvent,
                new MouseButtonEventHandler((_, _) => inkPreviewCount++));

            var window = ShowOffscreen(host, new Size(800, 600));
            try
            {
                inkCanvas.RaiseEvent(new MouseButtonEventArgs(
                    Mouse.PrimaryDevice,
                    Environment.TickCount,
                    MouseButton.Left)
                {
                    RoutedEvent = Mouse.PreviewMouseDownEvent,
                    Source = inkCanvas
                });
                Equal(1, hostPreviewCount, "Math input host did not receive preview input.");
                Equal(0, inkPreviewCount, "Handled math input still reached the InkCanvas.");
            }
            finally
            {
                window.Close();
            }

            var root = FindRepositoryRoot();
            var mainWindowCode = File.ReadAllText(Path.Combine(
                root,
                "Ink Canvas",
                "MainWindow_cs",
                "MW_Math.cs"));
            var requiredRoutes = new[]
            {
                "InkCanvasGridForInkReplay.PreviewMouseLeftButtonDown += MathCanvas_MouseLeftButtonDown;",
                "InkCanvasGridForInkReplay.PreviewMouseMove += MathCanvas_MouseMove;",
                "InkCanvasGridForInkReplay.PreviewMouseLeftButtonUp += MathCanvas_MouseLeftButtonUp;",
                "InkCanvasGridForInkReplay.PreviewTouchDown += MathCanvas_PreviewTouchDown;",
                "InkCanvasGridForInkReplay.PreviewTouchMove += MathCanvas_PreviewTouchMove;",
                "InkCanvasGridForInkReplay.PreviewTouchUp += MathCanvas_PreviewTouchUp;"
            };
            for (var i = 0; i < requiredRoutes.Length; i++)
            {
                True(
                    mainWindowCode.Contains(requiredRoutes[i], StringComparison.Ordinal),
                    $"Math input route is missing: {requiredRoutes[i]}");
            }
            True(
                !mainWindowCode.Contains(
                    "MathCanvas.MouseLeftButtonDown += MathCanvas_MouseLeftButtonDown;",
                    StringComparison.Ordinal),
                "Math input regressed to the isolated MathCanvas event route.");
        }

        private static void MathMenuSourceWiresEveryButton()
        {
            var root = FindRepositoryRoot();
            var xaml = File.ReadAllText(Path.Combine(
                root,
                "Ink Canvas",
                "Controls",
                "Popups",
                "MathInsertPopupContent.xaml"));
            var popupCode = File.ReadAllText(Path.Combine(
                root,
                "Ink Canvas",
                "Controls",
                "Popups",
                "MathInsertPopupContent.xaml.cs"));
            var mainWindowCode = File.ReadAllText(Path.Combine(
                root,
                "Ink Canvas",
                "MainWindow_cs",
                "MW_Math.cs"));
            var mainWindowXaml = File.ReadAllText(Path.Combine(
                root,
                "Ink Canvas",
                "MainWindow.xaml"));
            var nativeInkCode = File.ReadAllText(Path.Combine(
                root,
                "Ink Canvas",
                "MainWindow_cs",
                "MW_NativeWetInk.cs"));
            var floatingBarCode = File.ReadAllText(Path.Combine(
                root,
                "Ink Canvas",
                "MainWindow_cs",
                "MW_FloatingBarIcons.cs"));
            var buttonNames = Regex.Matches(
                    xaml,
                "<Button x:Name=\"([^\"]+)\"\\s+Style=\"\\{StaticResource MathToolButtonStyle\\}\"")
                .Cast<Match>()
                .Select(match => match.Groups[1].Value)
                .ToList();

            Equal(29, buttonNames.Count, "Math menu source button count changed unexpectedly.");
            for (var i = 0; i < buttonNames.Count; i++)
            {
                var buttonName = buttonNames[i];
                True(
                    popupCode.Contains($"=> {buttonName};", StringComparison.Ordinal),
                    $"Math popup does not expose {buttonName}.");
                var controlName = buttonName.Replace("Button", "ButtonControl", StringComparison.Ordinal);
                True(
                    mainWindowCode.Contains($".{controlName}.Click +=", StringComparison.Ordinal),
                    $"Main window does not wire {controlName}.");
            }
            True(
                mainWindowCode.Contains("BoardMathInsertPopup.IsOpen = true;", StringComparison.Ordinal),
                "Math menu command no longer opens the popup.");
            True(
                mainWindowCode.Contains(
                    "CoordinatePlaneButtonControl.Click += (_, _) => InsertCoordinatePlaneAtCanvasCenter();",
                    StringComparison.Ordinal),
                "Coordinate plane insertion regressed to a second canvas click.");
            True(
                mainWindowCode.Contains(
                    "FunctionButtonControl.Click += (_, _) => BeginFunctionInsertOrApplyEdit();",
                    StringComparison.Ordinal) &&
                mainWindowCode.Contains("AddMathObjects(objects);", StringComparison.Ordinal),
                "Function insertion no longer adds the graph directly.");
            True(
                mainWindowCode.Contains(
                    "CubeButtonControl.Click += (_, _) => BeginSolidInsert(SolidType.Cube);",
                    StringComparison.Ordinal),
                "Solid insertion no longer opens dimension setup first.");
            True(
                mainWindowCode.Contains("SolidInsertConfirmButtonControl.Click += (_, _) => ConfirmSolidInsert();", StringComparison.Ordinal) &&
                mainWindowCode.Contains("TryReadSolidDimensions", StringComparison.Ordinal),
                "Solid dimension confirmation is not wired.");
            True(
                mainWindowCode.Contains("ParallelButtonControl.Click += (_, _) => ActivateMathInsertMode(MathInsertMode.ParallelConstraint);", StringComparison.Ordinal) &&
                mainWindowCode.Contains("PerpendicularButtonControl.Click += (_, _) => ActivateMathInsertMode(MathInsertMode.PerpendicularConstraint);", StringComparison.Ordinal) &&
                mainWindowCode.Contains("FormatFunctionAnalysis(function)", StringComparison.Ordinal),
                "Plane constraints or function property analysis are no longer wired.");
            True(
                mainWindowCode.Contains("mathObject.StrokeColor = GetMathStrokeColor();", StringComparison.Ordinal),
                "New math objects no longer adapt their stroke color to the board background.");
            True(
                mainWindowXaml.Contains("<InkPresenter x:Name=\"MathInkPresenter\"", StringComparison.Ordinal) &&
                mainWindowCode.Contains("_mathStrokeRenderer.Render(", StringComparison.Ordinal),
                "Structured math objects are no longer projected to a native InkPresenter stroke layer.");
            True(
                mainWindowCode.Contains(
                    "var shouldShow = Settings.Canvas.EnableMathCanvas && currentMode == 1;",
                    StringComparison.Ordinal) &&
                mainWindowCode.Contains(
                    "MathInkPresenter.Visibility = shouldShow",
                    StringComparison.Ordinal),
                "Math refresh no longer repairs stale presenter visibility after mode changes.");
            True(
                mainWindowCode.Contains("WindowSettingsHelper.IsTemporarilyDisablingNoFocusMode = true;", StringComparison.Ordinal) &&
                mainWindowCode.Contains("FunctionExpressionInput.Focus();", StringComparison.Ordinal),
                "Function input no longer obtains keyboard focus in no-focus mode.");
            True(
                nativeInkCode.Contains("BoardMathInsertPopup?.IsOpen == true", StringComparison.Ordinal) &&
                nativeInkCode.Contains("MathObjectActionsPopup?.Visibility == Visibility.Visible", StringComparison.Ordinal) &&
                nativeInkCode.Contains("return LogicalInkTool.Math;", StringComparison.Ordinal),
                "Native input routing no longer defers math popup and math tool input to WPF.");
            True(
                mainWindowXaml.Contains("x:Name=\"MathPreviewPresenter\"", StringComparison.Ordinal) &&
                mainWindowXaml.Contains("x:Name=\"MathAnnotationOverlay\"", StringComparison.Ordinal) &&
                mainWindowXaml.Contains("x:Name=\"MathInteractionOverlay\"", StringComparison.Ordinal) &&
                mainWindowXaml.Contains("x:Name=\"MathObjectActionsLayer\"", StringComparison.Ordinal) &&
                mainWindowXaml.Contains("x:Name=\"MathObjectActionsPopup\"", StringComparison.Ordinal),
                "Math construction feedback or object action overlay is missing.");
            True(
                mainWindowCode.Contains("MathObjectEditButton.Click += (_, _) => EditSelectedMathObject();", StringComparison.Ordinal) &&
                mainWindowCode.Contains("MathObjectMeasureButton.Click += (_, _) => MeasureSelectedMathObject();", StringComparison.Ordinal) &&
                mainWindowCode.Contains("MathObjectResetViewButton.Click += (_, _) => ResetSelectedMathObjectView();", StringComparison.Ordinal) &&
                mainWindowCode.Contains("MathObjectDeleteButton.Click += (_, _) => DeleteSelectedMathObject();", StringComparison.Ordinal) &&
                mainWindowCode.Contains("MathObjectCircumsphereButton.Click += (_, _) => AddSelectedSolidSphere(false);", StringComparison.Ordinal) &&
                mainWindowCode.Contains("MathObjectInsphereButton.Click += (_, _) => AddSelectedSolidSphere(true);", StringComparison.Ordinal) &&
                mainWindowCode.Contains("MathObjectCircumcircleButton.Click += (_, _) => AddSelectedTriangleCircle(TriangleCircleKind.Circumcircle);", StringComparison.Ordinal) &&
                mainWindowCode.Contains("MathObjectIncircleButton.Click += (_, _) => AddSelectedTriangleCircle(TriangleCircleKind.Incircle);", StringComparison.Ordinal) &&
                mainWindowCode.Contains("MathObjectActionsPopup.Visibility = Visibility.Visible;", StringComparison.Ordinal) &&
                mainWindowCode.Split(
                    new[] { "IsDescendantOf(e.OriginalSource as DependencyObject, MathObjectActionsPopup)" },
                    StringSplitOptions.None).Length - 1 >= 4,
                "Math object action popup is no longer wired for interactive use.");
            True(
                mainWindowCode.Contains("IsPointerInsideOpenMathActionsPopup(position)", StringComparison.Ordinal) &&
                mainWindowCode.Contains("MathObjectActionsPopup.PointFromScreen(screenPoint)", StringComparison.Ordinal),
                "A click routed through the math action popup can again be treated as a blank-canvas click.");
            True(
                mainWindowCode.Contains("RefreshMathConstructionPreview();", StringComparison.Ordinal) &&
                mainWindowCode.Contains("RefreshMathAnnotations();", StringComparison.Ordinal) &&
                mainWindowCode.Contains("AddMathFunctionAnnotation(", StringComparison.Ordinal) &&
                mainWindowCode.Contains("coordinate.X:0.##", StringComparison.Ordinal) &&
                mainWindowCode.Contains("UpdateMathToolStatus();", StringComparison.Ordinal) &&
                mainWindowCode.Contains("UpdateMathSelectionOverlay();", StringComparison.Ordinal),
                "Math construction preview, status, or persistent selection wiring is missing.");
            True(
                xaml.Contains("GeometrySectionLabel", StringComparison.Ordinal) &&
                xaml.Contains("ConstraintSectionLabel", StringComparison.Ordinal) &&
                xaml.Contains("SolidSectionLabel", StringComparison.Ordinal),
                "Math menu is no longer grouped by task.");
            True(
                floatingBarCode.Contains("BoardMathInsertPopup.IsOpen = false;", StringComparison.Ordinal) &&
                !floatingBarCode.Contains("HidePopupWithSlideAndFade(BoardMathInsertPopup)", StringComparison.Ordinal),
                "Math popup can again be closed by a stale hide animation.");
            True(
                floatingBarCode.Contains("internal async void HideSubPanels", StringComparison.Ordinal) &&
                floatingBarCode.Contains("CancelMathInsertMode();", StringComparison.Ordinal),
                "Switching to another toolbar menu no longer exits math input mode.");
            True(
                mainWindowCode.Contains("UpdateMathToolbarVisual();", StringComparison.Ordinal) &&
                mainWindowCode.Contains("BeginMathObjectActionsInput", StringComparison.Ordinal) &&
                floatingBarCode.Contains("ClearMathSceneForUserClear();", StringComparison.Ordinal) &&
                floatingBarCode.Contains("HasMathObjectsOnCurrentPage()", StringComparison.Ordinal),
                "Math toolbar state, popup focus, or user clear binding is missing.");
        }

        private static void CoordinatePlaneRendersBehindLaterObjects()
        {
            var scene = new MathScene();
            var service = new MathSceneService(scene);
            service.Add(new SegmentObject
            {
                Start = new MathPoint(200, 250),
                End = new MathPoint(600, 250)
            });
            service.Add(new CoordinatePlaneObject
            {
                Center = new MathPoint(400, 250),
                Width = 500,
                Height = 300,
                GridSpacing = 40
            });

            True(scene.Objects[0] is CoordinatePlaneObject, "Coordinate plane is not below later math objects.");
            var strokes = new MathStrokeRenderer().Render(scene);
            True(strokes.Count > 0, "Coordinate plane projection produced no native strokes.");
            Equal(
                ((CoordinatePlaneObject)scene.Objects[0]).Id.ToString("D"),
                (string)strokes[0].GetPropertyData(MathStrokeRenderer.MathObjectIdProperty),
                "Coordinate plane native strokes are not below later math objects.");
        }

        private static void StructuredSceneProjectsToGroupedNativeStrokes()
        {
            var scene = new MathScene();
            var service = new MathSceneService(scene);
            var coordinatePlane = new CoordinatePlaneObject
            {
                Center = new MathPoint(400, 250),
                Width = 500,
                Height = 300,
                GridSpacing = 40
            };
            var function = new FunctionObject
            {
                Expression = "x^2-4",
                DomainMin = -5,
                DomainMax = 5,
                Origin = new MathPoint(400, 250),
                ShowIntersections = false
            };
            service.Add(coordinatePlane);
            service.Add(function);
            var solids = Enum.GetValues(typeof(SolidType))
                .Cast<SolidType>()
                .Select((type, index) => new SolidObject
                {
                    SolidType = type,
                    Center = new MathPoint(610 + index % 4 * 85, 130 + index / 4 * 190),
                    Scale = 28
                })
                .ToList();
            for (var i = 0; i < solids.Count; i++)
                service.Add(solids[i]);

            var strokes = new MathStrokeRenderer().Render(scene);
            True(strokes.Count > 20, "Structured math scene produced too few native strokes.");

            var ids = strokes
                .Where(stroke => stroke.ContainsPropertyData(MathStrokeRenderer.MathObjectIdProperty))
                .Select(stroke => (string)stroke.GetPropertyData(MathStrokeRenderer.MathObjectIdProperty))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            True(ids.Contains(coordinatePlane.Id.ToString("D")), "Coordinate plane stroke group is missing.");
            True(ids.Contains(function.Id.ToString("D")), "Function stroke group is missing.");
            for (var i = 0; i < solids.Count; i++)
                True(
                    ids.Contains(solids[i].Id.ToString("D")),
                    $"{solids[i].SolidType} stroke group is missing.");
            True(
                strokes.All(stroke => stroke.ContainsPropertyData(MathStrokeRenderer.MathGeneratedStrokeProperty)),
                "A projected math stroke is missing its generated-stroke marker.");

            var host = new Grid
            {
                Width = 900,
                Height = 600,
                Background = Brushes.White
            };
            var presenter = new InkPresenter
            {
                Strokes = strokes.Clone(),
                IsHitTestVisible = false
            };
            Panel.SetZIndex(presenter, 5);
            host.Children.Add(presenter);
            var regularInk = new System.Windows.Controls.InkCanvas
            {
                Background = Brushes.Transparent,
                EditingMode = InkCanvasEditingMode.None
            };
            Panel.SetZIndex(regularInk, 10);
            host.Children.Add(regularInk);
            host.Children.Add(new Canvas());
            var window = ShowOffscreen(host, new Size(900, 600));
            try
            {
                var bitmap = new RenderTargetBitmap(900, 600, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(host);
                var pixels = new byte[900 * 600 * 4];
                bitmap.CopyPixels(pixels, 900 * 4, 0);
                var nonWhitePixels = 0;
                for (var i = 0; i < pixels.Length; i += 4)
                {
                    if (pixels[i] < 245 || pixels[i + 1] < 245 || pixels[i + 2] < 245)
                        nonWhitePixels++;
                }
                True(nonWhitePixels > 1000, "Native math presenter rendered no visible content.");
            }
            finally
            {
                window.Close();
            }
        }

        private static void FrontViewSkipsDegenerateSolidStrokes()
        {
            var scene = new MathScene();
            var service = new MathSceneService(scene);
            foreach (SolidType type in Enum.GetValues(typeof(SolidType)))
            {
                service.Add(new SolidObject
                {
                    SolidType = type,
                    Center = new MathPoint(180 + (int)type * 100, 240),
                    Scale = 30,
                    ViewMode = SolidViewMode.Front
                });
            }

            var strokes = new MathStrokeRenderer().Render(scene);
            True(strokes.Count > 0, "Front-view solids produced no native strokes.");
            foreach (System.Windows.Ink.Stroke stroke in strokes)
            {
                var points = stroke.StylusPoints;
                var bounds = stroke.GetBounds();
                True(
                    points.Count > 1 &&
                    bounds.Width + bounds.Height >= 0.001,
                    "Front-view rendering produced a degenerate native stroke.");
            }
        }

        private static void SphereUsesContinuousOuterContour()
        {
            var scene = new MathScene();
            new MathSceneService(scene).Add(new SolidObject
            {
                SolidType = SolidType.Sphere,
                Center = new MathPoint(320, 240),
                Radius = 3,
                Scale = 40
            });

            var strokes = new MathStrokeRenderer().Render(scene);
            True(strokes.Count > 3, "Sphere should render an outline plus solid and dashed equator parts.");
            var outline = strokes[0].GetBounds();
            for (var i = 1; i < strokes.Count; i++)
            {
                var equatorPart = strokes[i].GetBounds();
                True(
                    equatorPart.Left >= outline.Left - 1 &&
                    equatorPart.Top >= outline.Top - 1 &&
                    equatorPart.Right <= outline.Right + 1 &&
                    equatorPart.Bottom <= outline.Bottom + 1,
                    "Sphere construction circle extends beyond its outer contour.");
            }
            var first = strokes[0].StylusPoints[0];
            var last = strokes[0].StylusPoints[strokes[0].StylusPoints.Count - 1];
            True(System.Math.Abs(first.X - last.X) + System.Math.Abs(first.Y - last.Y) < 0.001,
                "Sphere outer contour is not closed.");
        }

        private static void ConeUsesProjectedSilhouetteGenerators()
        {
            var solid = new SolidObject
            {
                SolidType = SolidType.Cone,
                Center = new MathPoint(320, 240),
                Radius = 3,
                Height = 5,
                Scale = 40
            };
            var scene = new MathScene();
            new MathSceneService(scene).Add(solid);

            var strokes = new MathStrokeRenderer().Render(scene);
            True(strokes.Count > 4, "Cone should render two generators and split base ellipse parts.");
            var apex = SolidProjectionService.ProjectModelPoint(
                solid,
                new MathPoint3D(0, solid.Height / 2, 0));
            var strokesAtApex = 0;
            foreach (System.Windows.Ink.Stroke stroke in strokes)
            {
                var first = stroke.StylusPoints[0];
                var last = stroke.StylusPoints[stroke.StylusPoints.Count - 1];
                if (System.Math.Sqrt(
                        System.Math.Pow(first.X - apex.X, 2) +
                        System.Math.Pow(first.Y - apex.Y, 2)) < 1 ||
                    System.Math.Sqrt(
                        System.Math.Pow(last.X - apex.X, 2) +
                        System.Math.Pow(last.Y - apex.Y, 2)) < 1)
                    strokesAtApex++;
            }
            Equal(2, strokesAtApex, "Cone should have exactly two silhouette generators at its apex.");
        }

        private static void RepeatedFunctionRenderingUsesCachedGeometry()
        {
            var scene = new MathScene();
            var service = new MathSceneService(scene);
            for (var i = 0; i < 20; i++)
            {
                service.Add(new FunctionObject
                {
                    Expression = i % 3 == 0 ? "sin(x)" : i % 3 == 1 ? "x^2-4" : "1/x",
                    DomainMin = -10,
                    DomainMax = 10,
                    Origin = new MathPoint(400, 300),
                    SampleQuality = 2,
                    ShowIntersections = false
                });
            }

            var renderer = new MathStrokeRenderer();
            renderer.Render(scene);
            const int Iterations = 100;
            var stopwatch = Stopwatch.StartNew();
            for (var i = 0; i < Iterations; i++)
                renderer.Render(scene);
            stopwatch.Stop();

            True(
                stopwatch.Elapsed < TimeSpan.FromSeconds(2),
                $"Repeated function rendering exceeded budget: {stopwatch.Elapsed.TotalMilliseconds:0.###} ms.");
            Console.WriteLine(
                $"METRIC 20-function native-stroke render: {stopwatch.Elapsed.TotalMilliseconds / Iterations:0.###} ms/frame");

            var movingFunction = new FunctionObject
            {
                Expression = "x",
                DomainMin = -2,
                DomainMax = 2,
                Origin = new MathPoint(200, 200),
                ShowIntersections = false
            };
            var movingScene = new MathScene();
            new MathSceneService(movingScene).Add(movingFunction);
            var movingRenderer = new MathStrokeRenderer();
            var before = movingRenderer.Render(movingScene).GetBounds();
            movingFunction.Origin = new MathPoint(300, 200);
            var after = movingRenderer.Render(movingScene).GetBounds();
            True(after.Left > before.Left + 90, "Function geometry cache did not invalidate after moving the origin.");

            var rotatingFunction = new FunctionObject
            {
                Expression = "0",
                DomainMin = -2,
                DomainMax = 2,
                Origin = new MathPoint(200, 200),
                ShowZeros = false,
                ShowExtrema = false,
                ShowIntersections = false
            };
            var rotatingScene = new MathScene();
            new MathSceneService(rotatingScene).Add(rotatingFunction);
            var rotatingRenderer = new MathStrokeRenderer();
            var horizontal = rotatingRenderer.Render(rotatingScene).GetBounds();
            rotatingFunction.RotationDegrees = 90;
            var vertical = rotatingRenderer.Render(rotatingScene).GetBounds();
            True(
                horizontal.Width > horizontal.Height + 100 &&
                vertical.Height > vertical.Width + 100,
                "Function rotation did not change the rendered orientation.");
        }

        private static void FunctionMarkersRenderCoordinateLabelsAndIntersections()
        {
            var renderer = new MathStrokeRenderer();
            var withoutMarkers = new MathScene();
            withoutMarkers.Objects.Add(new FunctionObject
            {
                Expression = "x^2-4",
                Origin = new MathPoint(400, 300),
                ShowZeros = false,
                ShowExtrema = false,
                ShowIntersections = false
            });
            var baselineCount = renderer.Render(withoutMarkers).Count;

            var withMarkers = new MathScene();
            withMarkers.Objects.Add(new FunctionObject
            {
                Expression = "x^2-4",
                Origin = new MathPoint(400, 300),
                ShowZeros = true,
                ShowExtrema = true,
                ShowIntersections = true
            });
            withMarkers.Objects.Add(new FunctionObject
            {
                Expression = "0",
                Origin = new MathPoint(400, 300),
                ShowZeros = false,
                ShowExtrema = false,
                ShowIntersections = true
            });
            var markedCount = renderer.Render(withMarkers).Count;
            True(markedCount > baselineCount + 2,
                "Function key-point or intersection markers did not add visible native strokes.");
        }

        private static Window ShowOffscreen(FrameworkElement element, Size size)
        {
            var window = new Window
            {
                Content = element,
                Width = size.Width,
                Height = size.Height,
                Left = -10000,
                Top = -10000,
                ShowActivated = false,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None
            };
            window.Show();
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.Loaded);
            element.UpdateLayout();
            return window;
        }

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Ink Canvas.sln")))
                    return directory.FullName;
                directory = directory.Parent;
            }
            throw new InvalidOperationException("Repository root was not found.");
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root)
            where T : DependencyObject
        {
            if (root == null) yield break;
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T match) yield return match;
                foreach (var descendant in FindVisualChildren<T>(child))
                    yield return descendant;
            }
        }

        private static bool IsSelfOrDescendant(DependencyObject candidate, DependencyObject ancestor)
        {
            while (candidate != null)
            {
                if (ReferenceEquals(candidate, ancestor)) return true;
                candidate = VisualTreeHelper.GetParent(candidate);
            }
            return false;
        }

        private static void Equal<T>(T expected, T actual, string message)
        {
            if (!Equals(expected, actual))
                throw new InvalidOperationException($"{message} Expected {expected}, got {actual}.");
        }

        private static void True(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }
    }
}
