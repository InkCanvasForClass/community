using System;
using System.Collections.Generic;
using Ink_Canvas.Ink.Native;

namespace InkCanvas.NativeInk.Tests
{
    internal static class Program
    {
        private static int _passed;

        private static void Main()
        {
            Run(nameof(HistoryIsChronologicalAndDeduplicated), HistoryIsChronologicalAndDeduplicated);
            Run(nameof(OutOfOrderHistoryIsSorted), OutOfOrderHistoryIsSorted);
            Run(nameof(OverlappingHistoryAcceptsOnlyNewFrames), OverlappingHistoryAcceptsOnlyNewFrames);
            Run(nameof(RepeatedDownCancelsPreviousSession), RepeatedDownCancelsPreviousSession);
            Run(nameof(ControllerIgnoresUpdateWithoutDown), ControllerIgnoresUpdateWithoutDown);
            Run(nameof(ControllerKeepsEndingSessionAcrossPointerReuse), ControllerKeepsEndingSessionAcrossPointerReuse);
            Run(nameof(ControllerRetiresOnlyAfterDryAndWpfFences), ControllerRetiresOnlyAfterDryAndWpfFences);
            Run(nameof(PredictionNeverEntersCommitPayload), PredictionNeverEntersCommitPayload);
            Run(nameof(CommitFenceTransitionsAreOrdered), CommitFenceTransitionsAreOrdered);
            Run(nameof(InvalidCommitFenceTransitionIsRejected), InvalidCommitFenceTransitionIsRejected);
            Run(nameof(CancelAllDropsConcurrentSessions), CancelAllDropsConcurrentSessions);
            Run(nameof(DisablePressurePersistsUniformPressure), DisablePressurePersistsUniformPressure);
            Run(nameof(VelocityBrushTipMarksProcessedStroke), VelocityBrushTipMarksProcessedStroke);
            Run(nameof(PointSetBrushTipTapersAtPenUp), PointSetBrushTipTapersAtPenUp);
            Run(nameof(RateBrushTipVariesWithPointSpeed), RateBrushTipVariesWithPointSpeed);
            Run(nameof(SessionFinalBrushTipRebuildsWetGeometry), SessionFinalBrushTipRebuildsWetGeometry);
            Run(nameof(RouterDefersUiAndSelectionContent), RouterDefersUiAndSelectionContent);
            Run(nameof(RouterBlocksFrozenMutationButAllowsRoam), RouterBlocksFrozenMutationButAllowsRoam);
            Run(nameof(RouterIgnoresPromotedMouse), RouterIgnoresPromotedMouse);
            Run(nameof(RouterLetsPromotedMouseReachUi), RouterLetsPromotedMouseReachUi);
            Run(nameof(RouterKeepsVideoGesturesAndPenAnnotationsSeparate), RouterKeepsVideoGesturesAndPenAnnotationsSeparate);
            Run(nameof(RouterRoutesInvertedPenToPointErase), RouterRoutesInvertedPenToPointErase);
            Run(nameof(RouterMapsLogicalTools), RouterMapsLogicalTools);
            Run(nameof(RouterPrefersMultiTouchWritingOverPalmErase), RouterPrefersMultiTouchWritingOverPalmErase);
            Run(nameof(RouterAppliesQuadIrPalmThresholdAndSpecialMultiplier), RouterAppliesQuadIrPalmThresholdAndSpecialMultiplier);
            Run(nameof(RouterAllowsDelayedTwoFingerTakeover), RouterAllowsDelayedTwoFingerTakeover);
            Run(nameof(RouterKeepsCapturedInkAndSuppressesBarrelPoints), RouterKeepsCapturedInkAndSuppressesBarrelPoints);
            Run(nameof(PointerBatchCopiesSamples), PointerBatchCopiesSamples);
            Run(nameof(PointerTimestampConversionAvoidsOverflow), PointerTimestampConversionAvoidsOverflow);
            Run(nameof(PointerTickTimestampHandlesWraparound), PointerTickTimestampHandlesWraparound);
            Run(nameof(MailboxPreservesBoundariesAndCoalescesMoves), MailboxPreservesBoundariesAndCoalescesMoves);
            Run(nameof(MailboxPreservesGlobalSequence), MailboxPreservesGlobalSequence);
            Run(nameof(MailboxRejectsStaleSnapshots), MailboxRejectsStaleSnapshots);
            Run(nameof(MailboxRejectsStaleSnapshotsAfterDrain), MailboxRejectsStaleSnapshotsAfterDrain);
            Run(nameof(MailboxBoundaryCommandsAreLossless), MailboxBoundaryCommandsAreLossless);
            Run(nameof(MailboxSnapshotCapacityIsBounded), MailboxSnapshotCapacityIsBounded);
            Run(nameof(GeometryBuildsVariableWidthRibbonWithCaps), GeometryBuildsVariableWidthRibbonWithCaps);
            Run(nameof(GeometryPreservesRectangularTipDimensions), GeometryPreservesRectangularTipDimensions);
            Run(nameof(GeometryMergesDegeneratePoints), GeometryMergesDegeneratePoints);
            Run(nameof(GeometryIncludesPredictionOnlyInWetOutline), GeometryIncludesPredictionOnlyInWetOutline);
            Run(nameof(GeometryStateFixesStableBlocksAndRebuildsTail), GeometryStateFixesStableBlocksAndRebuildsTail);
            Run(nameof(GeometryStateRejectsStaleVersions), GeometryStateRejectsStaleVersions);
            Run(nameof(GeometryStateRejectsShrinkingAndMutatedPoints), GeometryStateRejectsShrinkingAndMutatedPoints);
            Run(nameof(GeometryStateRejectsInvalidPredictions), GeometryStateRejectsInvalidPredictions);
            Run(nameof(GeometryStateResetsOnGenerationChange), GeometryStateResetsOnGenerationChange);
            Run(nameof(SessionStraightenReplacesPointsAndBumpsGeneration), SessionStraightenReplacesPointsAndBumpsGeneration);
            Run(nameof(FirstPointLookAheadMatchesSegmentSpeed), FirstPointLookAheadMatchesSegmentSpeed);
            Console.WriteLine($"Native ink contract tests passed: {_passed}.");
        }

        private static void HistoryIsChronologicalAndDeduplicated()
        {
            var input = new[] { Sample(30, 3, 30), Sample(20, 2, 20), Sample(10, 1, 10) };
            var result = InkSampleHistoryNormalizer.NormalizeReverseChronological(input, 10, 1);
            Equal(2, result.Count);
            Equal(20L, result[0].TimestampMicroseconds);
            Equal(30L, result[1].TimestampMicroseconds);
        }

        private static void OutOfOrderHistoryIsSorted()
        {
            var input = new[] { Sample(20, 2, 20), Sample(30, 3, 30), Sample(10, 1, 10) };
            var result = InkSampleHistoryNormalizer.NormalizeReverseChronological(input, -1, 0);
            Equal(10L, result[0].TimestampMicroseconds);
            Equal(20L, result[1].TimestampMicroseconds);
            Equal(30L, result[2].TimestampMicroseconds);
        }

        private static void OverlappingHistoryAcceptsOnlyNewFrames()
        {
            var session = new NativeInkSessionManager().Begin(7, NativeInkInputKind.Pen, Style(), new InkSampleProcessorSettings(), 10);
            Equal(2, session.AppendReverseChronologicalHistory(new[] { Sample(20, 2, 2), Sample(10, 1, 1) }));
            Equal(1, session.AppendReverseChronologicalHistory(new[] { Sample(30, 3, 3), Sample(20, 2, 2), Sample(10, 1, 1) }));
            Equal(30L, session.LastAcceptedTimestampMicroseconds);
            Equal(3U, session.LastAcceptedFrameId);
        }

        private static void RepeatedDownCancelsPreviousSession()
        {
            var manager = new NativeInkSessionManager();
            var first = manager.Begin(9, NativeInkInputKind.Pen, Style(), new InkSampleProcessorSettings(), 10);
            var second = manager.Begin(9, NativeInkInputKind.Pen, Style(), new InkSampleProcessorSettings(), 20);
            Equal(NativeInkSessionState.Canceled, first.State);
            Equal(NativeInkSessionState.Active, second.State);
            True(first.SessionId != second.SessionId);
            True(manager.TryGet(9, out var active));
            True(ReferenceEquals(second, active));
        }

        private static void ControllerIgnoresUpdateWithoutDown()
        {
            var controller = Controller(out _, out var mailbox);
            True(!controller.Update(42, new[] { Sample(10, 1, 1, 42) }));
            Equal(0, mailbox.PendingBoundaryCount);
            Equal(0, mailbox.PendingSnapshotCount);
        }

        private static void ControllerKeepsEndingSessionAcrossPointerReuse()
        {
            var controller = Controller(out var manager, out _);
            var first = controller.Begin(7, NativeInkInputKind.Pen, Style(), new InkSampleProcessorSettings(), 10,
                new[] { Sample(10, 1, 1) });
            var payload = controller.End(7, 20, new[] { Sample(20, 2, 2) });
            NotNull(payload);
            Equal(NativeInkSessionState.Ending, first.State);

            var second = controller.Begin(7, NativeInkInputKind.Pen, Style(), new InkSampleProcessorSettings(), 30,
                new[] { Sample(30, 3, 3) });
            True(first.SessionId != second.SessionId);
            Equal(NativeInkSessionState.Ending, first.State);
            True(manager.TryGetSession(first.SessionId, out var retained));
            True(ReferenceEquals(first, retained));
            True(manager.TryGet(7, out var active));
            True(ReferenceEquals(second, active));
        }

        private static void ControllerRetiresOnlyAfterDryAndWpfFences()
        {
            var controller = Controller(out var manager, out var mailbox);
            var session = controller.Begin(7, NativeInkInputKind.Pen, Style(), new InkSampleProcessorSettings(), 10,
                new[] { Sample(10, 1, 1) });
            controller.End(7, 20, new[] { Sample(20, 2, 2) });
            mailbox.Drain();

            controller.MarkDryCommitted(session.SessionId);
            Equal(NativeInkSessionState.DryCommittedAwaitingWpfFrame, session.State);
            controller.MarkWpfFrameRendered(session.SessionId);
            Equal(NativeInkSessionState.RetiringWetVisual, session.State);
            var retireBatch = mailbox.Drain();
            Equal(1, retireBatch.BoundaryCommands.Count);
            Equal(WetInkBoundaryCommandKind.RetireStroke, retireBatch.BoundaryCommands[0].Kind);
            True(manager.TryGetSession(session.SessionId, out _));

            True(controller.TryMarkWetVisualRetired(
                session.SessionId,
                retireBatch.BoundaryCommands[0].Version));
            Equal(NativeInkSessionState.Completed, session.State);
            True(!manager.TryGetSession(session.SessionId, out _));
            True(!controller.TryMarkWetVisualRetired(
                session.SessionId,
                retireBatch.BoundaryCommands[0].Version));
        }

        private static void PredictionNeverEntersCommitPayload()
        {
            var manager = new NativeInkSessionManager();
            var session = manager.Begin(4, NativeInkInputKind.Pen, Style(), new InkSampleProcessorSettings(), 10);
            session.AppendReverseChronologicalHistory(new[] { Sample(20, 2, 2, 4), Sample(10, 1, 1, 4) });
            session.ReplacePrediction(new[] { new PredictedInkPoint(99, 99, 0.5f, 30) });
            var payload = session.End(40);
            NotNull(payload);
            Equal(2, payload.Points.Count);
            Equal(0, session.PredictedPoints.Count);
            for (var i = 0; i < payload.Points.Count; i++)
                True(payload.Points[i].X != 99);
        }

        private static void CommitFenceTransitionsAreOrdered()
        {
            var session = new NativeInkSessionManager().Begin(4, NativeInkInputKind.Pen, Style(), new InkSampleProcessorSettings(), 10);
            session.AppendReverseChronologicalHistory(new[] { Sample(10, 1, 1, 4) });
            session.End(20);
            session.MarkDryCommitted();
            session.MarkWpfFrameRendered();
            session.MarkWetVisualRetired();
            Equal(NativeInkSessionState.Completed, session.State);
        }

        private static void InvalidCommitFenceTransitionIsRejected()
        {
            var session = new NativeInkSessionManager().Begin(4, NativeInkInputKind.Pen, Style(), new InkSampleProcessorSettings(), 10);
            Throws<InvalidOperationException>(session.MarkDryCommitted);
            Equal(NativeInkSessionState.Active, session.State);
        }

        private static void CancelAllDropsConcurrentSessions()
        {
            var manager = new NativeInkSessionManager();
            var first = manager.Begin(1, NativeInkInputKind.Touch, Style(), new InkSampleProcessorSettings(), 10);
            var second = manager.Begin(2, NativeInkInputKind.Touch, Style(), new InkSampleProcessorSettings(), 10);
            manager.CancelAll();
            Equal(NativeInkSessionState.Canceled, first.State);
            Equal(NativeInkSessionState.Canceled, second.State);
            Equal(0, manager.Sessions.Count);
        }

        private static void DisablePressurePersistsUniformPressure()
        {
            var processor = new InkSampleProcessor(new InkSampleProcessorSettings
            {
                DisablePressure = true,
                UseVelocityBrushTip = true,
                VelocityBrushTipMix = 1
            });
            var points = new List<RealInkPoint>();
            processor.Append(new[] { Sample(10, 1, 0, 1, 0.1f), Sample(20_000, 2, 4, 1, 0.9f) }, points);
            for (var i = 0; i < points.Count; i++)
                Equal(0.5f, points[i].Pressure);
            True(!processor.VelocityBrushTipApplied);
        }

        private static void VelocityBrushTipMarksProcessedStroke()
        {
            var processor = new InkSampleProcessor(new InkSampleProcessorSettings
            {
                UseVelocityBrushTip = true,
                VelocityBrushTipMix = 0.5f,
                BaseWidth = 5,
                InkStyle = 3
            });
            var points = new List<RealInkPoint>();
            processor.Append(new[] { Sample(10, 1, 0), Sample(20_000, 2, 10) }, points);
            True(processor.VelocityBrushTipApplied);
            True(points.Count != 0);
        }

        private static void PointSetBrushTipTapersAtPenUp()
        {
            var processor = new InkSampleProcessor(new InkSampleProcessorSettings
            {
                InkStyle = 0
            });
            var points = new List<RealInkPoint>();
            for (var i = 0; i < 20; i++)
                points.Add(new RealInkPoint(i * 5, 0, 0.5f, i * 1000));

            processor.ApplyFinalBrushTip(points);

            True(processor.FinalBrushTipApplied);
            Equal(20, points.Count);
            Equal(0.5f, points[0].Pressure);
            True(points[points.Count - 1].Pressure < points[points.Count - 2].Pressure);
            True(Math.Abs(points[points.Count - 1].Pressure - 0.1f) < 0.001f);
        }

        private static void RateBrushTipVariesWithPointSpeed()
        {
            var processor = new InkSampleProcessor(new InkSampleProcessorSettings
            {
                InkStyle = 1
            });
            var points = new List<RealInkPoint>
            {
                new RealInkPoint(0, 0, 0.5f, 0),
                new RealInkPoint(1, 0, 0.5f, 1000),
                new RealInkPoint(2, 0, 0.5f, 2000),
                new RealInkPoint(50, 0, 0.5f, 3000),
                new RealInkPoint(100, 0, 0.5f, 4000)
            };

            processor.ApplyFinalBrushTip(points);

            True(processor.FinalBrushTipApplied);
            True(points[1].Pressure > points[3].Pressure);
        }

        private static void SessionFinalBrushTipRebuildsWetGeometry()
        {
            var settings = new InkSampleProcessorSettings
            {
                InkStyle = 0
            };
            var session = new NativeInkSessionManager().Begin(
                7,
                NativeInkInputKind.Mouse,
                Style(),
                settings,
                0);
            for (var i = 0; i < 20; i++)
                session.AppendReverseChronologicalHistory(new[] { RawSample(i * 5, 0, i * 1000) });
            var generationBefore = session.GeometryGeneration;

            var payload = session.End(20_000);

            True(payload.FinalBrushTipApplied);
            True(session.GeometryGeneration == generationBefore + 1);
            True(payload.Points[payload.Points.Count - 1].Pressure < payload.Points[0].Pressure);
        }

        private static void RouterDefersUiAndSelectionContent()
        {
            var ui = NativeInkInputRouter.DecideDown(
                Pointer(NativeInkInputKind.Touch),
                Context(LogicalInkTool.Pen, CanvasHitZone.UiChrome));
            Equal(NativeInputRoute.DeferToWpfUi, ui.Route);
            True(!ui.ConsumeNativeMessage);
            True(ui.AllowWpfPromotion);

            var selection = NativeInkInputRouter.DecideDown(
                Pointer(NativeInkInputKind.Pen),
                Context(LogicalInkTool.Select, CanvasHitZone.CanvasContent));
            Equal(NativeInputRoute.DeferToWpfUi, selection.Route);
        }

        private static void RouterBlocksFrozenMutationButAllowsRoam()
        {
            var blocked = NativeInkInputRouter.DecideDown(
                Pointer(NativeInkInputKind.Pen),
                Context(LogicalInkTool.Pen, pageFrozen: true));
            Equal(NativeInputRoute.BlockedFrozen, blocked.Route);
            True(blocked.ConsumeNativeMessage);
            True(!blocked.AllowWpfPromotion);

            var roam = NativeInkInputRouter.DecideDown(
                Pointer(NativeInkInputKind.Touch),
                Context(LogicalInkTool.BoardRoam, pageFrozen: true));
            Equal(NativeInputRoute.BoardRoam, roam.Route);
        }

        private static void RouterIgnoresPromotedMouse()
        {
            var decision = NativeInkInputRouter.DecideDown(
                Pointer(NativeInkInputKind.Mouse, isPromotedMouse: true),
                Context(LogicalInkTool.Pen));
            Equal(NativeInputRoute.IgnorePromotedInput, decision.Route);
            True(decision.ConsumeNativeMessage);
            True(!decision.AllowWpfPromotion);
        }

        private static void RouterLetsPromotedMouseReachUi()
        {
            var decision = NativeInkInputRouter.DecideDown(
                Pointer(NativeInkInputKind.Mouse, isPromotedMouse: true),
                Context(LogicalInkTool.Pen, CanvasHitZone.UiChrome));
            Equal(NativeInputRoute.DeferToWpfUi, decision.Route);
            True(!decision.ConsumeNativeMessage);
            True(decision.AllowWpfPromotion);
        }

        private static void RouterKeepsVideoGesturesAndPenAnnotationsSeparate()
        {
            var gesture = NativeInkInputRouter.DecideDown(
                Pointer(NativeInkInputKind.Touch),
                Context(LogicalInkTool.Cursor, videoPresenter: true));
            Equal(NativeInputRoute.VideoGesture, gesture.Route);
            True(gesture.AllowWpfPromotion);

            var annotation = NativeInkInputRouter.DecideDown(
                Pointer(NativeInkInputKind.Touch),
                Context(LogicalInkTool.Pen, videoPresenter: true));
            Equal(NativeInputRoute.Ink, annotation.Route);
            True(annotation.ConsumeNativeMessage);
        }

        private static void RouterRoutesInvertedPenToPointErase()
        {
            var decision = NativeInkInputRouter.DecideDown(
                Pointer(
                    NativeInkInputKind.Pen,
                    NativeInkSampleFlags.InContact | NativeInkSampleFlags.Inverted),
                Context(LogicalInkTool.Pen));
            Equal(NativeInputRoute.PointErase, decision.Route);
            True(decision.AllowWpfPromotion);
        }

        private static void RouterMapsLogicalTools()
        {
            Equal(NativeInputRoute.PassThrough, Route(LogicalInkTool.Cursor));
            Equal(NativeInputRoute.PointErase, Route(LogicalInkTool.PointEraser));
            Equal(NativeInputRoute.StrokeErase, Route(LogicalInkTool.StrokeEraser));
            Equal(NativeInputRoute.Select, Route(LogicalInkTool.Select));
            Equal(NativeInputRoute.Shape, Route(LogicalInkTool.Shape));
            Equal(NativeInputRoute.BoardRoam, Route(LogicalInkTool.BoardRoam));
            Equal(NativeInputRoute.Ink, Route(LogicalInkTool.Pen));
        }

        private static void RouterPrefersMultiTouchWritingOverPalmErase()
        {
            var decision = NativeInkInputRouter.DecideDown(
                Pointer(NativeInkInputKind.Touch, contactWidthDip: 80, contactHeightDip: 80),
                Context(
                    LogicalInkTool.Pen,
                    multiTouchWriting: true,
                    palm: Palm(enabled: true)));
            Equal(NativeInputRoute.Ink, decision.Route);
            Equal(0d, decision.PalmEraserWidthDip);
        }

        private static void RouterAppliesQuadIrPalmThresholdAndSpecialMultiplier()
        {
            var decision = NativeInkInputRouter.DecideDown(
                Pointer(NativeInkInputKind.Touch, contactWidthDip: 64, contactHeightDip: 36),
                Context(
                    LogicalInkTool.Pen,
                    palm: Palm(
                        enabled: true,
                        isQuadIr: true,
                        isSpecialScreen: true,
                        boundsWidthDip: 10,
                        thresholdFactor: 2,
                        sensitivityMultiplier: 2,
                        eraserSizeFactor: 0.5,
                        touchMultiplier: 1.5)));
            Equal(NativeInputRoute.PointErase, decision.Route);
            Equal(36d, decision.PalmEraserWidthDip);

            var disabledOnSpecialScreen = NativeInkInputRouter.DecideDown(
                Pointer(NativeInkInputKind.Touch, contactWidthDip: 100, contactHeightDip: 100),
                Context(
                    LogicalInkTool.Pen,
                    palm: Palm(enabled: true, isSpecialScreen: true, touchMultiplier: 0)));
            Equal(NativeInputRoute.Ink, disabledOnSpecialScreen.Route);
        }

        private static void RouterAllowsDelayedTwoFingerTakeover()
        {
            var decision = NativeInkInputRouter.DecideDown(
                Pointer(NativeInkInputKind.Touch),
                Context(
                    LogicalInkTool.Pen,
                    twoFingerGestureAllowed: true,
                    activeTouchCount: 2));
            Equal(NativeInputRoute.CanvasGesture, decision.Route);
            Equal(100, decision.GestureTakeoverDelayMilliseconds);
            True(decision.AllowWpfPromotion);
        }

        private static void RouterKeepsCapturedInkAndSuppressesBarrelPoints()
        {
            var captured = NativeInkInputRouter.DecideDown(
                Pointer(NativeInkInputKind.Pen),
                Context(LogicalInkTool.Pen));
            var decision = NativeInkInputRouter.DecideCaptured(
                Pointer(NativeInkInputKind.Pen, secondaryBarrelButtonDown: true),
                Context(LogicalInkTool.PointEraser, CanvasHitZone.UiChrome),
                captured);
            Equal(NativeInputRoute.Ink, decision.Route);
            True(decision.SuppressPointEmission);
            True(decision.ConsumeNativeMessage);
            True(!decision.AllowWpfPromotion);
        }

        private static void PointerBatchCopiesSamples()
        {
            var samples = new[] { Sample(10, 1, 1) };
            var batch = new NativePointerInputBatch(
                7,
                NativeInkInputKind.Pen,
                NativePointerMessageKind.Update,
                samples,
                false,
                false,
                true);
            samples[0] = Sample(20, 2, 2);
            Equal(10L, batch.SamplesNewestFirst[0].TimestampMicroseconds);
        }

        private static void PointerTimestampConversionAvoidsOverflow()
        {
            const long frequency = 10_000_000;
            var performanceCount = (ulong)frequency * 60UL * 60UL * 24UL * 365UL;
            Equal(
                31_536_000_000_000L,
                NativePointerTimestampConverter.FromPerformanceCount(performanceCount, frequency));
        }

        private static void PointerTickTimestampHandlesWraparound()
        {
            var currentTickCount = (long)uint.MaxValue + 5000;
            var messageTime = unchecked((uint)(currentTickCount - 25));
            Equal(
                (currentTickCount - 25) * 1000,
                NativePointerTimestampConverter.FromTickCount(messageTime, currentTickCount));
        }

        private static void MailboxPreservesBoundariesAndCoalescesMoves()
        {
            var mailbox = new WetInkCommandMailbox();
            var style = Style();
            mailbox.EnqueueBoundary(new WetInkBoundaryCommand(WetInkBoundaryCommandKind.BeginStroke, 1));
            mailbox.PublishSnapshot(new WetInkRenderSnapshot(1, 1, style, new[] { new RealInkPoint(1, 1, 0.5f, 1) }, null));
            mailbox.PublishSnapshot(new WetInkRenderSnapshot(1, 2, style, new[] { new RealInkPoint(2, 2, 0.5f, 2) }, null));
            mailbox.EnqueueBoundary(new WetInkBoundaryCommand(WetInkBoundaryCommandKind.EndStroke, 1));
            var batch = mailbox.Drain();
            Equal(2, batch.BoundaryCommands.Count);
            Equal(1, batch.RenderSnapshots.Count);
            Equal(2L, batch.RenderSnapshots[0].Version);
            Equal(1, mailbox.CoalescedSnapshotCount);
        }

        private static void MailboxPreservesGlobalSequence()
        {
            var mailbox = new WetInkCommandMailbox();
            mailbox.EnqueueBoundary(new WetInkBoundaryCommand(
                WetInkBoundaryCommandKind.BeginStroke,
                1));
            mailbox.PublishSnapshot(new WetInkRenderSnapshot(
                1,
                1,
                Style(),
                Points(1),
                null));
            mailbox.EnqueueBoundary(new WetInkBoundaryCommand(
                WetInkBoundaryCommandKind.Reset,
                0));

            var batch = mailbox.Drain();
            Equal(2, batch.OrderedItems.Count);
            Equal(WetInkMailboxItemKind.Boundary, batch.OrderedItems[0].Kind);
            Equal(WetInkBoundaryCommandKind.BeginStroke, batch.OrderedItems[0].BoundaryCommand.Kind);
            Equal(WetInkMailboxItemKind.Boundary, batch.OrderedItems[1].Kind);
            Equal(WetInkBoundaryCommandKind.Reset, batch.OrderedItems[1].BoundaryCommand.Kind);
        }

        private static void MailboxRejectsStaleSnapshots()
        {
            var mailbox = new WetInkCommandMailbox();
            var style = Style();
            mailbox.PublishSnapshot(new WetInkRenderSnapshot(2, 4, style, new[] { new RealInkPoint(4, 4, 0.5f, 4) }, null));
            mailbox.PublishSnapshot(new WetInkRenderSnapshot(2, 3, style, new[] { new RealInkPoint(3, 3, 0.5f, 3) }, null));
            var batch = mailbox.Drain();
            Equal(1, batch.RenderSnapshots.Count);
            Equal(4L, batch.RenderSnapshots[0].Version);
            Equal(0, mailbox.CoalescedSnapshotCount);
        }

        private static void MailboxRejectsStaleSnapshotsAfterDrain()
        {
            var mailbox = new WetInkCommandMailbox();
            mailbox.PublishSnapshot(new WetInkRenderSnapshot(
                2,
                4,
                Style(),
                Points(1),
                null));
            mailbox.Drain();
            mailbox.PublishSnapshot(new WetInkRenderSnapshot(
                2,
                3,
                Style(),
                Points(1),
                null));
            Equal(0, mailbox.Drain().RenderSnapshots.Count);
        }

        private static void MailboxBoundaryCommandsAreLossless()
        {
            var mailbox = new WetInkCommandMailbox(1, 1);
            mailbox.EnqueueBoundary(new WetInkBoundaryCommand(
                WetInkBoundaryCommandKind.BeginStroke,
                1));
            mailbox.EnqueueBoundary(new WetInkBoundaryCommand(
                WetInkBoundaryCommandKind.EndStroke,
                1));
            mailbox.EnqueueBoundary(new WetInkBoundaryCommand(
                WetInkBoundaryCommandKind.RetireStroke,
                1));

            var batch = mailbox.Drain();
            Equal(3, batch.BoundaryCommands.Count);
            Equal(WetInkBoundaryCommandKind.BeginStroke, batch.BoundaryCommands[0].Kind);
            Equal(WetInkBoundaryCommandKind.EndStroke, batch.BoundaryCommands[1].Kind);
            Equal(WetInkBoundaryCommandKind.RetireStroke, batch.BoundaryCommands[2].Kind);
        }

        private static void MailboxSnapshotCapacityIsBounded()
        {
            var mailbox = new WetInkCommandMailbox(2, 1);
            mailbox.PublishSnapshot(new WetInkRenderSnapshot(1, 1, Style(), null, null));
            Throws<InvalidOperationException>(() =>
                mailbox.PublishSnapshot(new WetInkRenderSnapshot(2, 1, Style(), null, null)));

            var batch = mailbox.Drain();
            Equal(1, batch.RenderSnapshots.Count);
            Equal(1L, batch.RenderSnapshots[0].SessionId);
        }

        private static void GeometryBuildsVariableWidthRibbonWithCaps()
        {
            var builder = new WetInkGeometryBuilder();
            var geometry = builder.Build(
                new[]
                {
                    new RealInkPoint(0, 0, 0, 1),
                    new RealInkPoint(10, 0, 1, 2)
                },
                null,
                Style());
            Equal(4, geometry.Outline.Count);
            True(geometry.StartRadius < geometry.EndRadius);
            Equal(0f, geometry.StartCenter.X);
            Equal(10f, geometry.EndCenter.X);
        }

        private static void GeometryPreservesRectangularTipDimensions()
        {
            var style = new InkStrokeStyleSnapshot(
                0x80112233,
                4,
                12,
                true,
                true,
                false,
                0,
                0.5f,
                1,
                1,
                InkStylusTipShape.Rectangle);
            var geometry = new WetInkGeometryBuilder().Build(
                new[] { new RealInkPoint(0, 0, 0.5f, 1) },
                null,
                style);
            True(geometry.IsSinglePoint);
            Equal(2f, geometry.StartTip.RadiusX);
            Equal(6f, geometry.StartTip.RadiusY);
            Equal(InkStylusTipShape.Rectangle, geometry.StartTip.Shape);
        }

        private static void GeometryMergesDegeneratePoints()
        {
            var builder = new WetInkGeometryBuilder();
            var geometry = builder.Build(
                new[]
                {
                    new RealInkPoint(1, 2, 0.25f, 1),
                    new RealInkPoint(1, 2, 0.75f, 2)
                },
                null,
                Style());
            Equal(0, geometry.Outline.Count);
            Equal(1f, geometry.StartCenter.X);
            Equal(2f, geometry.StartCenter.Y);
            Equal(geometry.StartRadius, geometry.EndRadius);
        }

        private static void GeometryIncludesPredictionOnlyInWetOutline()
        {
            var builder = new WetInkGeometryBuilder();
            var real = new[] { new RealInkPoint(0, 0, 0.5f, 1) };
            var predicted = new[] { new PredictedInkPoint(5, 0, 0.5f, 2) };
            var geometry = builder.Build(real, predicted, Style());
            Equal(4, geometry.Outline.Count);
            Equal(5f, geometry.EndCenter.X);

            var payload = new NativeStrokeCommitPayload(1, 7, NativeInkInputKind.Pen, Style(), real, 1, 2, false);
            Equal(1, payload.Points.Count);
            Equal(0d, payload.Points[0].X);
        }

        private static void GeometryStateFixesStableBlocksAndRebuildsTail()
        {
            var state = new WetInkStrokeGeometryState(new WetInkGeometryBuilder());
            var firstPoints = Points(72);
            var first = state.Update(new WetInkRenderSnapshot(1, 1, Style(), firstPoints, null));
            Equal(1, first.FixedSegments.Count);
            Equal(48, first.FixedRealPointCount);
            Equal(48, first.FixedSegments[0].Outline.Count / 2);
            Equal(25, first.DynamicTail.Outline.Count / 2);

            var second = state.Update(new WetInkRenderSnapshot(
                1,
                2,
                Style(),
                Points(80),
                new[] { new PredictedInkPoint(82, 0, 0.5f, 82) }));
            Equal(1, second.FixedSegments.Count);
            Equal(48, second.FixedRealPointCount);
            Equal(34, second.DynamicTail.Outline.Count / 2);
        }

        private static void GeometryStateRejectsStaleVersions()
        {
            var state = new WetInkStrokeGeometryState(new WetInkGeometryBuilder());
            state.Update(new WetInkRenderSnapshot(1, 2, Style(), Points(2), null));
            Throws<InvalidOperationException>(() =>
                state.Update(new WetInkRenderSnapshot(1, 2, Style(), Points(2), null)));
        }

        private static void GeometryStateRejectsShrinkingAndMutatedPoints()
        {
            var shrinking = new WetInkStrokeGeometryState(new WetInkGeometryBuilder());
            shrinking.Update(new WetInkRenderSnapshot(1, 1, Style(), Points(20), null));
            Throws<InvalidOperationException>(() =>
                shrinking.Update(new WetInkRenderSnapshot(1, 2, Style(), Points(10), null)));

            var mutated = Points(20);
            mutated[5] = new RealInkPoint(500, 0, 0.5f, 5);
            Throws<InvalidOperationException>(() =>
                shrinking.Update(new WetInkRenderSnapshot(1, 3, Style(), mutated, null)));
        }

        private static void GeometryStateRejectsInvalidPredictions()
        {
            var state = new WetInkStrokeGeometryState(new WetInkGeometryBuilder());
            Throws<InvalidOperationException>(() =>
                state.Update(new WetInkRenderSnapshot(
                    1,
                    1,
                    Style(),
                    new[] { new RealInkPoint(0, 0, 0.5f, 10) },
                    new[] { new PredictedInkPoint(1, 0, 0.5f, 9) })));
        }

        private static NativeInputRoute Route(LogicalInkTool tool)
        {
            return NativeInkInputRouter.DecideDown(
                Pointer(NativeInkInputKind.Pen),
                Context(tool)).Route;
        }

        private static NativePointerFacts Pointer(
            NativeInkInputKind kind,
            NativeInkSampleFlags flags = NativeInkSampleFlags.InContact,
            bool secondaryBarrelButtonDown = false,
            bool isPromotedMouse = false,
            double contactWidthDip = 0,
            double contactHeightDip = 0)
        {
            return new NativePointerFacts(
                7,
                kind,
                flags,
                secondaryBarrelButtonDown,
                isPromotedMouse,
                10,
                20,
                contactWidthDip,
                contactHeightDip);
        }

        private static NativeInkRouteContext Context(
            LogicalInkTool tool,
            CanvasHitZone hitZone = CanvasHitZone.CanvasSurface,
            bool canvasInputEnabled = true,
            bool pageFrozen = false,
            bool videoPresenter = false,
            bool multiTouchWriting = false,
            bool twoFingerGestureAllowed = false,
            int activeTouchCount = 1,
            PalmRoutePolicy palm = default)
        {
            return new NativeInkRouteContext(
                hitZone,
                tool,
                canvasInputEnabled,
                pageFrozen,
                videoPresenter,
                multiTouchWriting,
                twoFingerGestureAllowed,
                activeTouchCount,
                palm);
        }

        private static PalmRoutePolicy Palm(
            bool enabled,
            bool isActive = false,
            bool isQuadIr = false,
            bool isSpecialScreen = false,
            double boundsWidthDip = 10,
            double thresholdFactor = 2,
            double sensitivityMultiplier = 2,
            double eraserSizeFactor = 0.5,
            double touchMultiplier = 1)
        {
            return new PalmRoutePolicy(
                enabled,
                isActive,
                isQuadIr,
                isSpecialScreen,
                boundsWidthDip,
                thresholdFactor,
                sensitivityMultiplier,
                eraserSizeFactor,
                touchMultiplier);
        }

        private static void GeometryStateResetsOnGenerationChange()
        {
            var state = new WetInkStrokeGeometryState(new WetInkGeometryBuilder());
            var firstPoints = Points(72);
            var first = state.Update(new WetInkRenderSnapshot(1, 1, Style(), firstPoints, null, geometryGeneration: 0));
            Equal(1, first.FixedSegments.Count);
            Equal(48, first.FixedRealPointCount);
            True(!first.Reset);

            // Mid-stroke straightening: generation flips, points shrink to a 2-point line.
            // State must discard accumulated fixed segments and rebuild from the line.
            var baseline = new[]
            {
                new RealInkPoint(0, 0, 0.5f, 0),
                new RealInkPoint(100, 0, 0.5f, 100)
            };
            var reset = state.Update(new WetInkRenderSnapshot(1, 2, Style(), baseline, null, geometryGeneration: 1));
            True(reset.Reset);
            Equal(0, reset.FixedSegments.Count);
            Equal(0, reset.FixedRealPointCount);
            True(reset.DynamicTail.Outline.Count > 0);

            // Subsequent append on the new baseline proceeds normally.
            // Need >= 72 points (48 stable + 24 tail) to emit a fixed segment.
            var appended = Points(96);
            appended[0] = new RealInkPoint(0, 0, 0.5f, 0);
            appended[1] = new RealInkPoint(100, 0, 0.5f, 100);
            var after = state.Update(new WetInkRenderSnapshot(1, 3, Style(), appended, null, geometryGeneration: 1));
            True(!after.Reset);
            Equal(1, after.FixedSegments.Count);
            Equal(48, after.FixedRealPointCount);
        }

        private static void SessionStraightenReplacesPointsAndBumpsGeneration()
        {
            var session = new NativeInkSessionManager().Begin(7, NativeInkInputKind.Pen, Style(), new InkSampleProcessorSettings(), 0);
            for (var i = 0; i < 20; i++)
                session.AppendReverseChronologicalHistory(new[] { Sample((long)i, (uint)i, i * 5) });
            Equal(20, session.RealPoints.Count);
            var generationBefore = session.GeometryGeneration;

            session.StraightenToLine();
            Equal(2, session.RealPoints.Count);
            // First point preserved exactly; last point is the final raw sample
            // (slightly smoothed by the One-Euro filter, so assert approximate).
            Equal(0.0, session.RealPoints[0].X);
            True(Math.Abs(session.RealPoints[1].X - 95.0) < 10.0);
            True(session.RealPoints[1].X > session.RealPoints[0].X);
            True(session.GeometryGeneration == generationBefore + 1);

            // Straightening a 1-point session is a no-op.
            var single = new NativeInkSessionManager().Begin(8, NativeInkInputKind.Pen, Style(), new InkSampleProcessorSettings(), 0);
            single.AppendReverseChronologicalHistory(new[] { Sample(0, 0, 10) });
            single.StraightenToLine();
            Equal(1, single.RealPoints.Count);
            True(single.GeometryGeneration == 0);
        }

        private static void FirstPointLookAheadMatchesSegmentSpeed()
        {
            // 高速书写：第二点到达后，首点压感应按首段速度回修（与第二点一致），避免起笔粗点闪变。
            var settings = new InkSampleProcessorSettings
            {
                UseVelocityBrushTip = true,
                VelocityBrushTipMix = 1.0f,
                BaseWidth = 2.5,
                DisablePressure = false,
                EnablePressureForTouch = false,
                MinimumDistanceScale = 0.5f,
            };
            var processor = new InkSampleProcessor(settings);
            var points = new List<RealInkPoint>();

            // 首点 (0,0)
            processor.Append(new[] { RawSample(0, 0, 0) }, points);
            Equal(1, points.Count);
            var firstPressureBefore = points[0].Pressure;

            // 第二点快速移动到 (100,0)，时间差 8ms (~120Hz) → 高速
            processor.Append(new[] { RawSample(100, 0, 8000) }, points);
            Equal(2, points.Count);

            // 首点已被回修：其压感应与第二点接近（均按高速计算），不再保持默认 0.5。
            var firstPressureAfter = points[0].Pressure;
            var secondPressure = points[1].Pressure;
            True(Math.Abs(firstPressureAfter - secondPressure) < 0.05);
            // 高速下压感应偏低（细），回修后的首点压感应低于回修前的默认值。
            True(firstPressureAfter < firstPressureBefore);
        }

        private static RawInkSample RawSample(double x, double y, long timestamp, float pressure = 0.5f)
        {
            return new RawInkSample(7, NativeInkInputKind.Mouse, x, y, pressure, false, timestamp, 0, NativeInkSampleFlags.None);
        }

        private static RealInkPoint[] Points(int count)
        {
            var points = new RealInkPoint[count];
            for (var i = 0; i < count; i++)
                points[i] = new RealInkPoint(i, 0, 0.5f, i);
            return points;
        }

        private static NativeInkController Controller(
            out NativeInkSessionManager manager,
            out WetInkCommandMailbox mailbox)
        {
            manager = new NativeInkSessionManager();
            mailbox = new WetInkCommandMailbox();
            return new NativeInkController(manager, mailbox);
        }

        private static RawInkSample Sample(
            long timestamp,
            uint frameId,
            double coordinate,
            uint pointerId = 7,
            float pressure = 0.5f)
        {
            return new RawInkSample(
                pointerId,
                NativeInkInputKind.Pen,
                coordinate,
                coordinate,
                pressure,
                true,
                timestamp,
                frameId,
                NativeInkSampleFlags.InContact);
        }

        private static InkStrokeStyleSnapshot Style()
        {
            return new InkStrokeStyleSnapshot(0xFF112233, 5, 5, false, false, false, 0, 0.5f, 1, 1);
        }

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

        private static void True(bool value)
        {
            if (!value) throw new InvalidOperationException("Expected true.");
        }

        private static void Throws<TException>(Action action) where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException)
            {
                return;
            }

            throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
        }

        private static void NotNull(object value)
        {
            if (value == null) throw new InvalidOperationException("Expected a non-null value.");
        }
    }
}
