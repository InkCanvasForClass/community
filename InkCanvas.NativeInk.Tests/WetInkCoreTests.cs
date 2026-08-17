using System;
using System.Collections.Generic;
using Ink_Canvas.Ink.WetInk;
using Windows.UI;
using Windows.UI.Input.Inking;

namespace InkCanvas.NativeInk.Tests
{
    internal static class WetInkCoreTests
    {
        private static int _passed;

        public static void RunAll()
        {
            Run(nameof(RealSamplesStayOutOfPredictionHistory), RealSamplesStayOutOfPredictionHistory);
            Run(nameof(RealSampleArrivalReplacesPredictionTail), RealSampleArrivalReplacesPredictionTail);
            Run(nameof(PenUpClearsPredictionAndHistory), PenUpClearsPredictionAndHistory);
            Run(nameof(AdaptiveHorizonStaysWithinBounds), AdaptiveHorizonStaysWithinBounds);
            Run(nameof(LowSpeedPredictionStaysShort), LowSpeedPredictionStaysShort);
            Run(nameof(TurnSuppressionShrinksPredictionTail), TurnSuppressionShrinksPredictionTail);
            Run(nameof(StaleSamplesShrinkPredictionTail), StaleSamplesShrinkPredictionTail);
            Run(nameof(PredictionStaysChronologicalAndFinite), PredictionStaysChronologicalAndFinite);
            Run(nameof(ClassifierDistinguishesFingerPalmAndPen), ClassifierDistinguishesFingerPalmAndPen);
            Run(nameof(ClassifierHonorsDisabledAndSpecialScreens), ClassifierHonorsDisabledAndSpecialScreens);
            Run(nameof(ClassifierClearsContactsOnPointerUp), ClassifierClearsContactsOnPointerUp);
            Run(nameof(PalmEraserWidthFollowsPolicy), PalmEraserWidthFollowsPolicy);
            Run(nameof(ChromeSkipNamesExcludeCanvasContainers), ChromeSkipNamesExcludeCanvasContainers);
            Run(nameof(ChromeAlwaysNamesKeepInteractiveOverlays), ChromeAlwaysNamesKeepInteractiveOverlays);
            Run(nameof(FrozenPageDisablesPenTool), FrozenPageDisablesPenTool);
            Run(nameof(StyleSnapshotCarriesHighlighterAndLaser), StyleSnapshotCarriesHighlighterAndLaser);
            Console.WriteLine($"Wet ink core tests passed: {_passed}.");
        }

        private static void RealSamplesStayOutOfPredictionHistory()
        {
            var session = new WetInkPredictionSession();
            FeedFastStroke(session, 20);

            True(session.PredictedPoints.Count > 0);
            Equal(20, session.GetRecentRealPoints().Count);

            var lastReal = session.GetRecentRealPoints()[session.GetRecentRealPoints().Count - 1];
            foreach (var predicted in session.PredictedPoints)
            {
                True(predicted.TimestampMicroseconds > lastReal.TimestampMicroseconds,
                    "预测点进入真实采样历史时其时间戳不应早于最新真实点。");
            }
        }

        private static void RealSampleArrivalReplacesPredictionTail()
        {
            var session = new WetInkPredictionSession();
            FeedFastStroke(session, 6);
            var before = session.PredictedPoints;
            True(before.Count > 0);

            var lastReal = session.GetRecentRealPoints()[session.GetRecentRealPoints().Count - 1];
            session.OnRealSample(
                lastReal.X + 13.333,
                lastReal.Y,
                lastReal.Pressure,
                lastReal.TimestampMicroseconds + 8_000);

            var after = session.PredictedPoints;
            True(after.Count > 0);
            True(after[after.Count - 1].TimestampMicroseconds > lastReal.TimestampMicroseconds,
                "真实采样到达后预测尾应重建到新位置。");
        }

        private static void PenUpClearsPredictionAndHistory()
        {
            var session = new WetInkPredictionSession();
            FeedFastStroke(session, 10);
            True(session.PredictedPoints.Count > 0);
            True(session.RealPointCount > 0);

            session.EndStroke();

            Equal(0, session.PredictedPoints.Count);
            Equal(0, session.RealPointCount);
            True(!session.InStroke);

            // 下一笔不能继承上一笔的预测点。
            FeedFastStroke(session, 3);
            foreach (var predicted in session.PredictedPoints)
            {
                True(predicted.TimestampMicroseconds > 0);
            }
        }

        private static void AdaptiveHorizonStaysWithinBounds()
        {
            var points = FastStrokePoints(24);
            var predicted = WetInkTailPredictor.Build(points);

            True(predicted.Count > 0);
            True(predicted.Count <= 18);
            True(MaxReach(points, predicted) <= 50.001, "预测尾距离不能超过 50px 上限。");

            var horizonMs = MaxHorizonMilliseconds(points, predicted);
            True(horizonMs >= 13.9, $"预测视界低于下限: {horizonMs:F2}ms");
            True(horizonMs <= 30.1, $"预测视界超过上限: {horizonMs:F2}ms");
        }

        private static void LowSpeedPredictionStaysShort()
        {
            var fast = FastStrokePoints(24);
            var slow = SlowStrokePoints(24);

            var fastReach = MaxReach(fast, WetInkTailPredictor.Build(fast));
            var slowReach = MaxReach(slow, WetInkTailPredictor.Build(slow));

            True(fastReach > 10);
            True(slowReach < fastReach, $"低速预测尾应显著短于高速: slow={slowReach:F1}, fast={fastReach:F1}");
        }

        private static void TurnSuppressionShrinksPredictionTail()
        {
            var straight = new List<WetInkRealPoint>();
            var turned = new List<WetInkRealPoint>();
            long stamp = 10_000;
            for (var i = 0; i < 10; i++)
            {
                straight.Add(new WetInkRealPoint(i * 8.0, 0, 0.5f, stamp + i * 8_000));
            }

            double[][] arc =
            {
                new[] { 0.0, 0.0 },
                new[] { 8.0, 0.0 },
                new[] { 16.0, 0.0 },
                new[] { 24.0, 0.0 },
                new[] { 32.0, 0.0 },
                new[] { 40.0, 8.0 },
                new[] { 40.0, 16.0 },
                new[] { 40.0, 24.0 },
                new[] { 32.0, 32.0 },
                new[] { 24.0, 32.0 }
            };
            for (var i = 0; i < arc.Length; i++)
            {
                turned.Add(new WetInkRealPoint(arc[i][0], arc[i][1], 0.5f, stamp + i * 8_000));
            }

            var straightReach = MaxReach(straight, WetInkTailPredictor.Build(straight));
            var turnedReach = MaxReach(turned, WetInkTailPredictor.Build(turned));
            True(turnedReach < straightReach - 2.0,
                $"拐弯应明显抑制预测尾: turn={turnedReach:F1}, straight={straightReach:F1}");
        }

        private static void StaleSamplesShrinkPredictionTail()
        {
            var fresh = FastStrokePoints(20);
            var stale = new List<WetInkRealPoint>();
            for (var i = 0; i < 20; i++)
            {
                stale.Add(new WetInkRealPoint(i * 83.333, 0, 0.5f, 10_000L + i * 50_000L));
            }

            var freshReach = MaxReach(fresh, WetInkTailPredictor.Build(fresh));
            var staleReach = MaxReach(stale, WetInkTailPredictor.Build(stale));
            True(staleReach < freshReach - 10.0,
                $"报点停滞应抑制预测尾: stale={staleReach:F1}, fresh={freshReach:F1}");
        }

        private static void PredictionStaysChronologicalAndFinite()
        {
            var session = new WetInkPredictionSession();
            FeedFastStroke(session, 40);
            var predicted = session.PredictedPoints;
            True(predicted.Count > 0);

            var lastReal = session.GetRecentRealPoints()[session.GetRecentRealPoints().Count - 1];
            long previousStamp = lastReal.TimestampMicroseconds;
            foreach (var point in predicted)
            {
                True(IsFinite(point.X) && IsFinite(point.Y));
                True(IsFinite(point.Pressure) && point.Pressure >= 0 && point.Pressure <= 1);
                True(point.TimestampMicroseconds >= previousStamp,
                    "预测点必须保持时间有序且不早于最新真实点。");
                previousStamp = point.TimestampMicroseconds;
            }
        }

        private static void ClassifierDistinguishesFingerPalmAndPen()
        {
            var classifier = new WetInkTouchClassifier();
            var policy = PalmPolicy(thresholdFactor: 0.03);

            Equal(WetInkContactKind.Finger, classifier.Classify(policy, 1, true, 10, 10));
            Equal(WetInkContactKind.Palm, classifier.Classify(policy, 2, true, 200, 200));
            True(classifier.HasActivePalm);

            // 笔/鼠标设备永远不判为手掌。
            Equal(WetInkContactKind.Pen, classifier.Classify(policy, 3, false, 200, 200));
            True(!classifier.HasActivePalm || classifier.PalmContactCount == 1);
        }

        private static void ClassifierHonorsDisabledAndSpecialScreens()
        {
            var classifier = new WetInkTouchClassifier();
            var disabled = PalmPolicy(thresholdFactor: 0.03, enabled: false);
            Equal(WetInkContactKind.Finger, classifier.Classify(disabled, 1, true, 500, 500));
            True(!classifier.HasActivePalm);

            var specialDisabled = PalmPolicy(thresholdFactor: 0.03, specialScreen: true, touchMultiplier: 0);
            Equal(WetInkContactKind.Finger, classifier.Classify(specialDisabled, 2, true, 500, 500));
            True(!classifier.HasActivePalm);

            var quadIr = PalmPolicy(thresholdFactor: 0.03, quadIr: true);
            Equal(WetInkContactKind.Palm, classifier.Classify(quadIr, 3, true, 50, 200));
        }

        private static void ClassifierClearsContactsOnPointerUp()
        {
            var classifier = new WetInkTouchClassifier();
            var policy = PalmPolicy(thresholdFactor: 0.03);
            Equal(WetInkContactKind.Palm, classifier.Classify(policy, 1, true, 300, 300));
            True(classifier.HasActivePalm);

            classifier.OnPointerUp(1);
            True(!classifier.HasActivePalm);
            Equal(0, classifier.PalmContactCount);
        }

        private static void PalmEraserWidthFollowsPolicy()
        {
            var classifier = new WetInkTouchClassifier();
            var policy = PalmPolicy(thresholdFactor: 0.03, specialScreen: true, touchMultiplier: 2);

            Equal(60.0, classifier.GetPalmEraserWidthDip(policy, 200, 200));
            Equal(60.0, classifier.GetPalmEraserWidthDip(policy, 50, 800));
        }

        private static void ChromeSkipNamesExcludeCanvasContainers()
        {
            True(WetInkRouter.ShouldSkipChromeName("InkCanvasGridForInkReplay"));
            True(WetInkRouter.ShouldSkipChromeName("GridBackgroundCoverHolder"));
            True(!WetInkRouter.ShouldSkipChromeName("FloatingbarUIForInkReplay"));
            True(!WetInkRouter.ShouldSkipChromeName("BoardToolbar"));
            True(!WetInkRouter.ShouldSkipChromeName(null));
        }

        private static void ChromeAlwaysNamesKeepInteractiveOverlays()
        {
            True(WetInkRouter.IsAlwaysChromeName("GridForFloatingBarDraging"));
            True(WetInkRouter.IsAlwaysChromeName("GridInkCanvasSelectionCover"));
            True(!WetInkRouter.IsAlwaysChromeName("Main_Grid"));
        }

        private static void FrozenPageDisablesPenTool()
        {
            True(WetInkEnginePolicy.IsPenToolActive(WetInkLogicalTool.Pen, false));
            True(!WetInkEnginePolicy.IsPenToolActive(WetInkLogicalTool.Pen, true));
            True(!WetInkEnginePolicy.IsPenToolActive(WetInkLogicalTool.PointEraser, false));
            True(!WetInkEnginePolicy.IsPenToolActive(WetInkLogicalTool.BoardRoam, false));
        }

        private static void StyleSnapshotCarriesHighlighterAndLaser()
        {
            var style = new WetInkStyleSnapshot(
                Color.FromArgb(255, 255, 0, 0),
                4,
                4,
                true,
                false,
                true,
                PenTipShape.Circle,
                isLaser: true);

            True(style.DrawAsHighlighter);
            True(style.IsLaser);
            Equal(4.0, style.Width);
            Equal(Color.FromArgb(255, 255, 0, 0), style.Color);

            var normal = new WetInkStyleSnapshot(
                Color.FromArgb(255, 0, 0, 0),
                2,
                2,
                false,
                true,
                false,
                PenTipShape.Rectangle);
            True(!normal.IsLaser);
            True(!normal.DrawAsHighlighter);
            Equal(PenTipShape.Rectangle, normal.PenTip);
        }

        // ---------------- helpers ----------------

        private static void FeedFastStroke(WetInkPredictionSession session, int count)
        {
            for (var i = 0; i < count; i++)
            {
                session.OnRealSample(i * 13.333, 0, 0.5f, 10_000L + i * 8_000L);
            }
        }

        private static IReadOnlyList<WetInkRealPoint> FastStrokePoints(int count)
        {
            var points = new List<WetInkRealPoint>(count);
            for (var i = 0; i < count; i++)
            {
                points.Add(new WetInkRealPoint(i * 13.333, 0, 0.5f, 10_000L + i * 8_000L));
            }
            return points;
        }

        private static IReadOnlyList<WetInkRealPoint> SlowStrokePoints(int count)
        {
            var points = new List<WetInkRealPoint>(count);
            for (var i = 0; i < count; i++)
            {
                points.Add(new WetInkRealPoint(i * 0.4, 0, 0.5f, 10_000L + i * 10_000L));
            }
            return points;
        }

        private static WetInkPalmPolicy PalmPolicy(
            double thresholdFactor,
            bool enabled = true,
            bool quadIr = false,
            bool specialScreen = false,
            double touchMultiplier = 1)
        {
            return new WetInkPalmPolicy(
                enabled,
                quadIr,
                specialScreen,
                boundsWidthDip: 1200,
                thresholdFactor,
                sensitivityMultiplier: 2.0,
                eraserSizeFactor: 0.15,
                touchMultiplier);
        }

        private static double MaxReach(
            IReadOnlyList<WetInkRealPoint> real,
            IReadOnlyList<WetInkPredictedPoint> predicted)
        {
            var last = real[real.Count - 1];
            var max = 0.0;
            foreach (var point in predicted)
            {
                var dx = point.X - last.X;
                var dy = point.Y - last.Y;
                max = Math.Max(max, Math.Sqrt(dx * dx + dy * dy));
            }
            return max;
        }

        private static double MaxHorizonMilliseconds(
            IReadOnlyList<WetInkRealPoint> real,
            IReadOnlyList<WetInkPredictedPoint> predicted)
        {
            var last = real[real.Count - 1].TimestampMicroseconds;
            var max = 0.0;
            foreach (var point in predicted)
            {
                max = Math.Max(max, (point.TimestampMicroseconds - last) / 1000.0);
            }
            return max;
        }

        private static bool IsFinite(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value);

        private static void Run(string name, Action test)
        {
            try
            {
                test();
                _passed++;
                Console.WriteLine($"PASS {name}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"FAIL {name}: {ex.Message}");
                Environment.ExitCode = 1;
                throw;
            }
        }

        private static void Equal<T>(T expected, T actual)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException($"Expected {expected}; actual {actual}.");
        }

        private static void True(bool value, string message = null)
        {
            if (!value)
                throw new InvalidOperationException(message ?? "Expected true.");
        }
    }
}
