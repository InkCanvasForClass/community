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
            Run(nameof(PredictionBakedIntoCommitPayload), PredictionBakedIntoCommitPayload);
            Run(nameof(ControllerHonorsPredictionSettingOnBegin), ControllerHonorsPredictionSettingOnBegin);
            Run(nameof(ControllerSkipsPredictionForDuplicateHistory), ControllerSkipsPredictionForDuplicateHistory);
            Run(nameof(ControllerClearsPredictionWhenDisabled), ControllerClearsPredictionWhenDisabled);
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
            Run(nameof(RouterAllowsPromotedPenMouseInk), RouterAllowsPromotedPenMouseInk);
            Run(nameof(RouterKeepsVideoGesturesAndPenAnnotationsSeparate), RouterKeepsVideoGesturesAndPenAnnotationsSeparate);
            Run(nameof(RouterRoutesInvertedPenToPointErase), RouterRoutesInvertedPenToPointErase);
            Run(nameof(RouterMapsLogicalTools), RouterMapsLogicalTools);
            Run(nameof(RouterPrefersMultiTouchWritingOverPalmErase), RouterPrefersMultiTouchWritingOverPalmErase);
            Run(nameof(RouterAppliesQuadIrPalmThresholdAndSpecialMultiplier), RouterAppliesQuadIrPalmThresholdAndSpecialMultiplier);
            Run(nameof(RouterAllowsDelayedTwoFingerTakeover), RouterAllowsDelayedTwoFingerTakeover);
            Run(nameof(RouterKeepsCapturedInkAndSuppressesBarrelPoints), RouterKeepsCapturedInkAndSuppressesBarrelPoints);
            Run(nameof(PointerBatchCopiesSamples), PointerBatchCopiesSamples);
            Run(nameof(PointerBatchOwnedSamplesSkipCopy), PointerBatchOwnedSamplesSkipCopy);
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
            Run(nameof(PredictionHorizonStaysWithinAdaptiveBounds), PredictionHorizonStaysWithinAdaptiveBounds);
            Run(nameof(PredictionHorizonGrowsWithSpeed), PredictionHorizonGrowsWithSpeed);
            Run(nameof(LowSpeedPredictionStaysShortInPixels), LowSpeedPredictionStaysShortInPixels);
            Run(nameof(PredictionHorizonIsCappedByReach), PredictionHorizonIsCappedByReach);
            Run(nameof(PredictionDoesNotProduceSeewoStyleLongTail), PredictionDoesNotProduceSeewoStyleLongTail);
            Run(nameof(PredictionHorizonShrinksOnSharpTurn), PredictionHorizonShrinksOnSharpTurn);
            Run(nameof(PredictionHorizonShrinksOnStaleSamples), PredictionHorizonShrinksOnStaleSamples);
            Run(nameof(PredictionIsEmptyBelowMinimumSpeed), PredictionIsEmptyBelowMinimumSpeed);
            Run(nameof(PredictionCrawlingProducesShortTail), PredictionCrawlingProducesShortTail);
            Run(nameof(PredictionSurvivesSpeedDip), PredictionSurvivesSpeedDip);
            Run(nameof(PredictionStaysChronologicalAndFinite), PredictionStaysChronologicalAndFinite);
            Run(nameof(SmoothedHorizonSuppressesJitter), SmoothedHorizonSuppressesJitter);
            Run(nameof(SmoothedHorizonConvergesToSteadyState), SmoothedHorizonConvergesToSteadyState);
            Run(nameof(SmoothedHorizonShrinksQuicklyOnTurn), SmoothedHorizonShrinksQuicklyOnTurn);
            Run(nameof(UpdatePumpKeepsLatestPendingWork), UpdatePumpKeepsLatestPendingWork);
            Run(nameof(UpdatePumpDropsStaleSessionWork), UpdatePumpDropsStaleSessionWork);
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

        private static void ControllerHonorsPredictionSettingOnBegin()
        {
            var disabledController = Controller(out _, out var disabledMailbox);
            var disabled = disabledController.Begin(
                4,
                NativeInkInputKind.Pen,
                Style(),
                new InkSampleProcessorSettings(),
                10,
                new[] { Sample(20, 2, 2, 4), Sample(10, 1, 1, 4) },
                predictionEnabled: false);
            var disabledSnapshot = disabledMailbox.Drain().RenderSnapshots[0];
            Equal(0, disabled.PredictedPoints.Count);
            Equal(0, disabledSnapshot.PredictedPoints.Count);

            var enabledController = Controller(out _, out var enabledMailbox);
            var enabled = enabledController.Begin(
                4,
                NativeInkInputKind.Pen,
                Style(),
                new InkSampleProcessorSettings(),
                10,
                new[] { Sample(20, 2, 2, 4), Sample(10, 1, 1, 4) },
                predictionEnabled: true);
            var enabledSnapshot = enabledMailbox.Drain().RenderSnapshots[0];
            True(enabled.PredictedPoints.Count > 0);
            True(enabledSnapshot.PredictedPoints.Count > 0);
        }

        private static void ControllerSkipsPredictionForDuplicateHistory()
        {
            var controller = Controller(out _, out var mailbox);
            var session = controller.Begin(
                4,
                NativeInkInputKind.Pen,
                Style(),
                new InkSampleProcessorSettings(),
                10,
                new[] { Sample(20, 2, 2, 4), Sample(10, 1, 1, 4) },
                predictionEnabled: true);
            mailbox.Drain();

            True(!controller.TryUpdateSessionWithPrediction(
                4,
                session.SessionId,
                new[] { Sample(20, 2, 2, 4), Sample(10, 1, 1, 4) },
                predictionEnabled: true));
            Equal(0, mailbox.Drain().RenderSnapshots.Count);
            True(session.PredictedPoints.Count > 0);
        }

        private static void ControllerClearsPredictionWhenDisabled()
        {
            var controller = Controller(out _, out var mailbox);
            var session = controller.Begin(
                4,
                NativeInkInputKind.Pen,
                Style(),
                new InkSampleProcessorSettings(),
                10,
                new[] { Sample(20, 2, 2, 4), Sample(10, 1, 1, 4) },
                predictionEnabled: true);
            mailbox.Drain();
            True(session.PredictedPoints.Count > 0);

            True(controller.TryUpdateSessionWithPrediction(
                4,
                session.SessionId,
                Array.Empty<RawInkSample>(),
                predictionEnabled: false));
            var snapshot = mailbox.Drain().RenderSnapshots[0];
            Equal(0, snapshot.PredictedPoints.Count);
            Equal(0, session.PredictedPoints.Count);
        }

        private static void PredictionNeverEntersCommitPayload()
        {
            var manager = new NativeInkSessionManager();
            var session = manager.Begin(4, NativeInkInputKind.Pen, Style(), new InkSampleProcessorSettings(), 10);
            session.AppendReverseChronologicalHistory(new[] { Sample(20, 2, 2, 4), Sample(10, 1, 1, 4) });
            session.ReplacePrediction(new[] { new PredictedInkPoint(99, 99, 0.5f, 30) });
            // 默认不烘焙：预测点不得进入干墨提交。
            var payload = session.End(40, bakePredictionIntoRealInk: false);
            NotNull(payload);
            Equal(2, payload.Points.Count);
            Equal(0, session.PredictedPoints.Count);
            for (var i = 0; i < payload.Points.Count; i++)
                True(payload.Points[i].X != 99);
        }

        private static void PredictionBakedIntoCommitPayload()
        {
            var manager = new NativeInkSessionManager();
            var session = manager.Begin(4, NativeInkInputKind.Pen, Style(), new InkSampleProcessorSettings(), 10);
            session.AppendReverseChronologicalHistory(new[] { Sample(20, 2, 2, 4), Sample(10, 1, 1, 4) });
            session.ReplacePrediction(new[]
            {
                new PredictedInkPoint(30, 2, 0.4f, 30),
                new PredictedInkPoint(40, 2, 0.3f, 40),
            });
            var payload = session.End(50, bakePredictionIntoRealInk: true);
            NotNull(payload);
            Equal(4, payload.Points.Count);
            Equal(0, session.PredictedPoints.Count);
            Equal(30.0, payload.Points[2].X);
            Equal(40.0, payload.Points[3].X);
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

        private static void RouterAllowsPromotedPenMouseInk()
        {
            var decision = NativeInkInputRouter.DecideDown(
                Pointer(NativeInkInputKind.Pen, isPromotedMouse: true),
                Context(LogicalInkTool.Pen));
            Equal(NativeInputRoute.Ink, decision.Route);
            True(decision.ConsumeNativeMessage);
            True(!decision.AllowWpfPromotion);
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

        private static void PointerBatchOwnedSamplesSkipCopy()
        {
            var samples = new[] { Sample(10, 1, 1) };
            var batch = NativePointerInputBatch.CreateFromOwnedSamples(
                7,
                NativeInkInputKind.Pen,
                NativePointerMessageKind.Update,
                samples,
                false,
                false,
                true);
            samples[0] = Sample(20, 2, 2);
            Equal(20L, batch.SamplesNewestFirst[0].TimestampMicroseconds);
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
                (IReadOnlyList<RealInkPoint>)new RealInkPoint[]
                {
                    new RealInkPoint(0, 0, 0, 1),
                    new RealInkPoint(10, 0, 1, 2)
                },
                (IReadOnlyList<PredictedInkPoint>)null,
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
                InkStylusTipShape.Rectangle,
                InkRenderMode.Standard);
            var geometry = new WetInkGeometryBuilder().Build(
                (IReadOnlyList<RealInkPoint>)new[] { new RealInkPoint(0, 0, 0.5f, 1) },
                (IReadOnlyList<PredictedInkPoint>)null,
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
                (IReadOnlyList<RealInkPoint>)new[]
                {
                    new RealInkPoint(1, 2, 0.25f, 1),
                    new RealInkPoint(1, 2, 0.75f, 2)
                },
                (IReadOnlyList<PredictedInkPoint>)null,
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
            var geometry = builder.Build(
                (IReadOnlyList<RealInkPoint>)real,
                (IReadOnlyList<PredictedInkPoint>)predicted,
                Style());
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

        /// <summary>
        /// 沿 X 轴匀速直线：每段间隔 intervalMs，步进 stepPx，速度 = stepPx / intervalMs。
        /// </summary>
        private static RealInkPoint[] StraightStroke(
            int count,
            double stepPx,
            double intervalMs)
        {
            var points = new RealInkPoint[count];
            for (var i = 0; i < count; i++)
            {
                points[i] = new RealInkPoint(
                    i * stepPx,
                    0,
                    0.5f,
                    (long)(i * intervalMs * 1000.0));
            }

            return points;
        }

        /// <summary>
        /// 最后一段才转向 +Y 的折线：模拟“笔尖此刻正在拐弯”。
        /// </summary>
        private static RealInkPoint[] CornerAtTipStroke(
            int count,
            double stepPx,
            double intervalMs)
        {
            var points = new RealInkPoint[count];
            double x = 0, y = 0;
            for (var i = 0; i < count; i++)
            {
                points[i] = new RealInkPoint(x, y, 0.5f, (long)(i * intervalMs * 1000.0));
                if (i < count - 2)
                    x += stepPx;
                else
                    y += stepPx;
            }

            return points;
        }

        /// <summary>
        /// 匀速圆弧：整笔总共转过 totalDegrees，用于连续曲率场景。
        /// </summary>
        private static RealInkPoint[] ArcStroke(
            int count,
            double stepPx,
            double intervalMs,
            double totalDegrees)
        {
            var points = new RealInkPoint[count];
            var perSegment = totalDegrees / (count - 1) * Math.PI / 180.0;
            double x = 0, y = 0, angle = 0;
            for (var i = 0; i < count; i++)
            {
                points[i] = new RealInkPoint(x, y, 0.5f, (long)(i * intervalMs * 1000.0));
                x += stepPx * Math.Cos(angle);
                y += stepPx * Math.Sin(angle);
                angle += perSegment;
            }

            return points;
        }

        /// <summary>
        /// 预测笔尾覆盖的时长（毫秒）：最后一个预测点相对最后一个真实点的时间跨度。
        /// </summary>
        private static double HorizonMs(
            IReadOnlyList<RealInkPoint> real,
            IReadOnlyList<PredictedInkPoint> predicted)
        {
            if (predicted.Count == 0)
                return 0;
            var last = real[real.Count - 1].TimestampMicroseconds;
            return (predicted[predicted.Count - 1].TimestampMicroseconds - last) / 1000.0;
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
            return new InkStrokeStyleSnapshot(
                0xFF112233,
                5,
                5,
                false,
                false,
                false,
                0,
                0.5f,
                1,
                1,
                InkStylusTipShape.Ellipse,
                InkRenderMode.Standard);
        }

        /// <summary>
        /// 任何速度/曲率组合下，预测视界都必须落在 10~50ms 内。
        /// </summary>
        private static void PredictionHorizonStaysWithinAdaptiveBounds()
        {
            var intervals = new[] { 4.0, 8.0, 16.0, 33.0, 60.0 };
            var steps = new[] { 1.0, 5.0, 20.0, 60.0, 200.0 };
            var checkedCases = 0;
            foreach (var intervalMs in intervals)
            {
                foreach (var stepPx in steps)
                {
                    foreach (var straight in new[] { true, false })
                    {
                        var real = straight
                            ? StraightStroke(8, stepPx, intervalMs)
                            : CornerAtTipStroke(8, stepPx, intervalMs);
                        var predicted = InkTailPredictor.Build(real);
                        if (predicted.Count == 0)
                            continue;

                        var horizon = HorizonMs(real, predicted);
                        True(horizon >= InkTailPredictor.MinHorizonMilliseconds - 0.001);
                        True(horizon <= InkTailPredictor.MaxHorizonMilliseconds + 0.001);
                        checkedCases++;
                    }
                }
            }

            True(checkedCases >= 10);
        }

        /// <summary>
        /// 视界随速度单调变长；超低速也要拿到可感知的预测量，而不是贴死下限。
        /// </summary>
        private static void PredictionHorizonGrowsWithSpeed()
        {
            // 约 62px/s：刚过 40px/s 门限的超低速。
            var crawl = StraightStroke(8, 0.5, 8);
            // 约 250px/s：慢写。
            var slow = StraightStroke(8, 2, 8);
            // 约 1250px/s：中速。
            var medium = StraightStroke(8, 10, 8);
            // 约 2500px/s：速度映射到接近满视界，且未被 80 DIP 距离上限完全截断。
            var fast = StraightStroke(8, 20, 8);

            var crawlHorizon = HorizonMs(crawl, InkTailPredictor.Build(crawl));
            var slowHorizon = HorizonMs(slow, InkTailPredictor.Build(slow));
            var mediumHorizon = HorizonMs(medium, InkTailPredictor.Build(medium));
            var fastHorizon = HorizonMs(fast, InkTailPredictor.Build(fast));

            True(slowHorizon > crawlHorizon);
            True(mediumHorizon > slowHorizon);
            True(fastHorizon > mediumHorizon);

            // 超低速与慢写都要比下限有可感知的余量，否则等同于关掉低速预测。
            True(crawlHorizon >= InkTailPredictor.MinHorizonMilliseconds + 2.0);
            True(slowHorizon >= InkTailPredictor.MinHorizonMilliseconds + 10.0);
            True(fastHorizon >= 30.0);
        }

        /// <summary>
        /// 低速视界变长不得把笔尾甩出笔尖太远——超低速的外推距离应始终是像素级。
        /// </summary>
        private static void LowSpeedPredictionStaysShortInPixels()
        {
            var crawl = StraightStroke(8, 0.5, 8);
            var slow = StraightStroke(8, 2, 8);

            True(Reach(crawl, InkTailPredictor.Build(crawl)) <= 3.0);
            True(Reach(slow, InkTailPredictor.Build(slow)) <= 12.0);
        }

        /// <summary>
        /// 极快书写时距离上限收敛视界，避免甩出过长的笔尾。
        /// </summary>
        private static void PredictionHorizonIsCappedByReach()
        {
            // 约 10000px/s：若取满 36ms 视界，笔尾仍必须受距离上限约束。
            var veryFast = StraightStroke(8, 80, 8);
            var predicted = InkTailPredictor.Build(veryFast);

            True(predicted.Count > 0);
            True(HorizonMs(veryFast, predicted) < InkTailPredictor.MaxHorizonMilliseconds);
            // 允许一个步长的越界余量。
            True(Reach(veryFast, predicted) <= 200.0);
        }

        /// <summary>
        /// 相同速率下，笔尖正在转弯时的视界与外推距离都必须明显小于直线；
        /// 连续弧线也应有一定收敛。
        /// </summary>
        private static void PredictionDoesNotProduceSeewoStyleLongTail()
        {
            // 高速直线也不应产生一条接近整段距离上限的长尾。
            var fast = StraightStroke(8, 40, 8);
            var predicted = InkTailPredictor.Build(fast);

            True(predicted.Count > 0);
            True(HorizonMs(fast, predicted) <= 36.0 + 4.0);
            True(Reach(fast, predicted) <= 80.0);
        }

        private static void PredictionHorizonShrinksOnSharpTurn()
        {
            var straight = StraightStroke(8, 20, 8);
            var turning = CornerAtTipStroke(8, 20, 8);

            var straightPredicted = InkTailPredictor.Build(straight);
            var turningPredicted = InkTailPredictor.Build(turning);

            True(HorizonMs(turning, turningPredicted) < HorizonMs(straight, straightPredicted) * 0.75);
            True(Reach(turning, turningPredicted) < Reach(straight, straightPredicted) * 0.75);

            // 连续 180° 弧线：每段夹角较小，抑制更温和但仍应收敛。
            var arc = ArcStroke(8, 20, 8, 180);
            var arcPredicted = InkTailPredictor.Build(arc);
            True(HorizonMs(arc, arcPredicted) < HorizonMs(straight, straightPredicted));
        }

        /// <summary>
        /// 报点停滞（间隔远大于正常帧）时速度已陈旧，视界应收敛。
        /// </summary>
        private static void PredictionHorizonShrinksOnStaleSamples()
        {
            var fresh = StraightStroke(8, 20, 8);
            // 同一速率（2500px/s）但报点间隔 48ms，属于停滞样本。
            var stale = StraightStroke(8, 120, 48);

            var freshHorizon = HorizonMs(fresh, InkTailPredictor.Build(fresh));
            var staleHorizon = HorizonMs(stale, InkTailPredictor.Build(stale));

            True(freshHorizon > 0);
            True(staleHorizon < freshHorizon);
        }

        /// <summary>
        /// 仅在真正无方向（停驻）或输入退化时返回空；低速爬行仍产出短笔尾，避免加减速阶段闪烁。
        /// </summary>
        private static void PredictionIsEmptyBelowMinimumSpeed()
        {
            // 笔尖停驻：没有可信方向，不外推。
            var stationary = new[]
            {
                new RealInkPoint(10, 10, 0.5f, 0),
                new RealInkPoint(10, 10, 0.5f, 8_000),
                new RealInkPoint(10, 10, 0.5f, 16_000),
            };
            Equal(0, InkTailPredictor.Build(stationary).Count);

            // 输入退化：空集或单点。
            Equal(0, InkTailPredictor.Build(new RealInkPoint[0]).Count);
            Equal(0, InkTailPredictor.Build(new[] { new RealInkPoint(0, 0, 0.5f, 0) }).Count);
        }

        /// <summary>
        /// 低速爬行（曾因低于旧硬门限而整帧返回空）现在仍产出笔尾，但视界与外推距离都极小。
        /// </summary>
        private static void PredictionCrawlingProducesShortTail()
        {
            // 约 50px/s：曾返回空，现在应产出短笔尾。
            var crawling = StraightStroke(8, 0.4, 8);
            var predicted = InkTailPredictor.Build(crawling);
            True(predicted.Count > 0);
            True(HorizonMs(crawling, predicted) <= 15.0);
            True(Reach(crawling, predicted) <= 2.0);
        }

        /// <summary>
        /// 速度低于旧硬门限（曾整帧返回空）时仍产出短笔尾，避免加减速阶段笔尾闪烁消失。
        /// </summary>
        private static void PredictionSurvivesSpeedDip()
        {
            // 约 18px/s：曾低于 40px/s 硬门限返回空，现在钳到最小有效速度产出短尾。
            var slowButMoving = StraightStroke(8, 0.15, 8);
            var predicted = InkTailPredictor.Build(slowButMoving);
            True(predicted.Count > 0);
            True(HorizonMs(slowButMoving, predicted) <= InkTailPredictor.MinHorizonMilliseconds + 2.0);
            // 速度极低，外推距离必须是子像素级，不会“甩”出去。
            True(Reach(slowButMoving, predicted) <= 1.0);

            // 约 50px/s：同样曾返回空。
            var crawling = StraightStroke(8, 0.4, 8);
            var crawlPredicted = InkTailPredictor.Build(crawling);
            True(crawlPredicted.Count > 0);
            True(Reach(crawling, crawlPredicted) <= 2.0);
        }

        /// <summary>
        /// 报点间隔抖动下，平滑器必须显著压低视界的帧间跳变（“一抽一抽”的直接成因）。
        /// </summary>
        private static void SmoothedHorizonSuppressesJitter()
        {
            // 名义 8ms 报点、±1ms 间隔抖动、每点 12px：真实触摸屏的常见量级。
            var stroke = JitteryIntervalStroke(24, 12, 8, 1.0);

            var rawJump = MaxFrameToFrameJump(HorizonSeries(stroke, null));
            var smoothedJump = MaxFrameToFrameJump(
                HorizonSeries(stroke, new InkTailPredictionSmoother()));

            True(rawJump > 0.5);
            // 平滑后帧间跳变至少降到无平滑的一半。
            True(smoothedJump < rawJump * 0.5);
        }

        /// <summary>
        /// 平滑不能改变稳态视界：匀速书写足够多帧后，平滑视界应收敛到无平滑视界附近。
        /// 否则平滑会系统性地把笔尾变短或变长。
        /// </summary>
        private static void SmoothedHorizonConvergesToSteadyState()
        {
            var stroke = StraightStroke(40, 20, 8);
            var smoothed = HorizonSeries(stroke, new InkTailPredictionSmoother());
            var raw = HorizonSeries(stroke, null);

            True(smoothed.Count > 0 && raw.Count > 0);
            var steadySmoothed = smoothed[smoothed.Count - 1];
            var steadyRaw = raw[raw.Count - 1];
            True(Math.Abs(steadySmoothed - steadyRaw) < steadyRaw * 0.1);
        }

        /// <summary>
        /// 拐弯时笔尾必须快速收回（收缩用小时间常数），否则会甩到弯道外侧。
        /// 平滑不得把这个收缩拖慢成多帧才生效。
        /// </summary>
        private static void SmoothedHorizonShrinksQuicklyOnTurn()
        {
            // 先直行建立较长视界的平滑状态。
            var smoother = new InkTailPredictionSmoother();
            var straight = StraightStroke(12, 20, 8);
            var straightSeries = HorizonSeries(straight, smoother);
            var beforeTurn = straightSeries[straightSeries.Count - 1];

            // 接着在笔尖处急转，喂入同一个 smoother。
            var corner = CornerAtTipStroke(13, 20, 8);
            var turned = InkTailPredictor.Build(corner, smoother);
            True(turned.Count > 0);
            var afterTurn = HorizonMs(corner, turned);

            // 一帧内就要出现明显收缩，而不是缓慢回落。
            True(afterTurn < beforeTurn * 0.85);
        }

        /// <summary>
        /// 预测点必须有限、时间戳严格递增，才能通过会话与几何层的校验。
        /// </summary>
        private static void PredictionStaysChronologicalAndFinite()
        {            var real = StraightStroke(8, 40, 8);
            var predicted = InkTailPredictor.Build(real);
            True(predicted.Count > 0);

            var previous = real[real.Length - 1].TimestampMicroseconds;
            for (var i = 0; i < predicted.Count; i++)
            {
                var point = predicted[i];
                True(!double.IsNaN(point.X) && !double.IsInfinity(point.X));
                True(!double.IsNaN(point.Y) && !double.IsInfinity(point.Y));
                True(point.Pressure > 0f && point.Pressure <= 1f);
                True(point.TimestampMicroseconds > previous);
                previous = point.TimestampMicroseconds;
            }

            // 会话层用同一套不变量校验，应当接受。
            var manager = new NativeInkSessionManager();
            var session = manager.Begin(9, NativeInkInputKind.Pen, Style(), new InkSampleProcessorSettings(), 0);
            session.AppendReverseChronologicalHistory(new[] { Sample(20, 2, 2, 9), Sample(10, 1, 1, 9) });
            session.ReplacePrediction(InkTailPredictor.Build(session.RealPoints));
        }

        private static void UpdatePumpKeepsLatestPendingWork()
        {
            var controller = Controller(out _, out var mailbox);
            var session = controller.Begin(
                7,
                NativeInkInputKind.Pen,
                Style(),
                new InkSampleProcessorSettings(),
                0,
                new[] { Sample(10, 1, 10) });
            mailbox.Drain();

            var workSignals = 0;
            using var pump = new NativePointerUpdatePump(controller, () => workSignals++);
            pump.Enqueue(7, session.SessionId, new[] { Sample(20, 2, 20) }, predictionEnabled: false);
            pump.Enqueue(7, session.SessionId, new[] { Sample(30, 3, 30) }, predictionEnabled: false);
            pump.FlushPointer(7);

            var batch = mailbox.Drain();
            Equal(1, batch.RenderSnapshots.Count);
            Equal(30L, batch.RenderSnapshots[0].RealPoints[batch.RenderSnapshots[0].RealPoints.Count - 1].TimestampMicroseconds);
            True(workSignals >= 1);
        }

        private static void UpdatePumpDropsStaleSessionWork()
        {
            var controller = Controller(out var manager, out var mailbox);
            var first = controller.Begin(
                7,
                NativeInkInputKind.Pen,
                Style(),
                new InkSampleProcessorSettings(),
                0,
                new[] { Sample(10, 1, 10) });
            mailbox.Drain();

            using var pump = new NativePointerUpdatePump(controller, () => { });

            var payload = controller.End(7, 30, new[] { Sample(30, 3, 30) });
            NotNull(payload);
            var second = controller.Begin(
                7,
                NativeInkInputKind.Pen,
                Style(),
                new InkSampleProcessorSettings(),
                40,
                new[] { Sample(40, 4, 40) });
            True(second.SessionId != first.SessionId);

            pump.Enqueue(7, first.SessionId, new[] { Sample(20, 2, 20) }, predictionEnabled: false);
            pump.FlushAll();

            True(manager.TryGetSession(first.SessionId, out var retained));
            Equal(NativeInkSessionState.Ending, retained.State);
            var batch = mailbox.Drain();
            Equal(2, batch.RenderSnapshots.Count);
            Equal(first.SessionId, batch.RenderSnapshots[0].SessionId);
            Equal(3L, batch.RenderSnapshots[0].Version);
            Equal(second.SessionId, batch.RenderSnapshots[1].SessionId);
            Equal(1L, batch.RenderSnapshots[1].Version);
        }

        /// <summary>
        /// 预测笔尾相对最后一个真实点的最远直线距离。
        /// </summary>
        private static double Reach(
            IReadOnlyList<RealInkPoint> real,
            IReadOnlyList<PredictedInkPoint> predicted)
        {
            var last = real[real.Count - 1];
            var maxReach = 0.0;
            for (var i = 0; i < predicted.Count; i++)
            {
                var dx = predicted[i].X - last.X;
                var dy = predicted[i].Y - last.Y;
                maxReach = Math.Max(maxReach, Math.Sqrt(dx * dx + dy * dy));
            }

            return maxReach;
        }

        /// <summary>
        /// 匀速直线，但报点间隔按 ±jitterMs 交替抖动：真实触摸屏的常见形态。
        /// 位置按名义匀速推进，只有时间戳抖，因此单段差分算出的速度会被放大抖动。
        /// </summary>
        private static RealInkPoint[] JitteryIntervalStroke(
            int count,
            double stepPx,
            double intervalMs,
            double jitterMs)
        {
            var points = new RealInkPoint[count];
            var timestampMs = 0.0;
            for (var i = 0; i < count; i++)
            {
                points[i] = new RealInkPoint(
                    i * stepPx,
                    0,
                    0.5f,
                    (long)(timestampMs * 1000.0));
                timestampMs += intervalMs + (i % 2 == 0 ? jitterMs : -jitterMs);
            }

            return points;
        }

        /// <summary>
        /// 逐帧喂入笔画前缀（模拟实时书写），返回每帧的视界序列。
        /// smoother 为 null 时走无状态路径。
        /// </summary>
        private static List<double> HorizonSeries(
            RealInkPoint[] stroke,
            InkTailPredictionSmoother smoother)
        {
            var series = new List<double>();
            for (var n = 2; n <= stroke.Length; n++)
            {
                var prefix = new RealInkPoint[n];
                Array.Copy(stroke, prefix, n);
                var predicted = smoother == null
                    ? InkTailPredictor.Build(prefix)
                    : InkTailPredictor.Build(prefix, smoother);
                if (predicted.Count > 0)
                    series.Add(HorizonMs(prefix, predicted));
            }

            return series;
        }

        /// <summary>
        /// 相邻帧视界变化的最大绝对值：抖动的直接度量。
        /// </summary>
        private static double MaxFrameToFrameJump(List<double> series)
        {
            var maxJump = 0.0;
            for (var i = 1; i < series.Count; i++)
                maxJump = Math.Max(maxJump, Math.Abs(series[i] - series[i - 1]));
            return maxJump;
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
