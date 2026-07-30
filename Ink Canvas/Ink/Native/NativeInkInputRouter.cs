using System;

namespace Ink_Canvas.Ink.Native
{
    internal enum LogicalInkTool
    {
        Cursor,
        Pen,
        PointEraser,
        StrokeEraser,
        Select,
        Shape,
        BoardRoam
    }

    internal enum CanvasHitZone
    {
        Outside,
        UiChrome,
        SelectionOverlay,
        InteractiveCanvasChild,
        CanvasContent,
        CanvasSurface,
        EraserOverlay
    }

    internal enum NativeInputRoute
    {
        PassThrough,
        DeferToWpfUi,
        Ink,
        PointErase,
        StrokeErase,
        Select,
        Shape,
        BoardRoam,
        CanvasGesture,
        VideoGesture,
        BlockedFrozen,
        IgnorePromotedInput
    }

    internal readonly struct NativePointerFacts
    {
        public NativePointerFacts(
            uint pointerId,
            NativeInkInputKind kind,
            NativeInkSampleFlags flags,
            bool secondaryBarrelButtonDown,
            bool isPromotedMouse,
            double xDip,
            double yDip,
            double contactWidthDip,
            double contactHeightDip)
        {
            PointerId = pointerId;
            Kind = kind;
            Flags = flags;
            SecondaryBarrelButtonDown = secondaryBarrelButtonDown;
            IsPromotedMouse = isPromotedMouse;
            XDip = xDip;
            YDip = yDip;
            ContactWidthDip = contactWidthDip;
            ContactHeightDip = contactHeightDip;
        }

        public uint PointerId { get; }
        public NativeInkInputKind Kind { get; }
        public NativeInkSampleFlags Flags { get; }
        public bool SecondaryBarrelButtonDown { get; }
        public bool IsPromotedMouse { get; }
        public double XDip { get; }
        public double YDip { get; }
        public double ContactWidthDip { get; }
        public double ContactHeightDip { get; }
    }

    internal readonly struct PalmRoutePolicy
    {
        public PalmRoutePolicy(
            bool enabled,
            bool isActive,
            bool isQuadIr,
            bool isSpecialScreen,
            double boundsWidthDip,
            double thresholdFactor,
            double sensitivityMultiplier,
            double eraserSizeFactor,
            double touchMultiplier)
        {
            Enabled = enabled;
            IsActive = isActive;
            IsQuadIr = isQuadIr;
            IsSpecialScreen = isSpecialScreen;
            BoundsWidthDip = boundsWidthDip;
            ThresholdFactor = thresholdFactor;
            SensitivityMultiplier = sensitivityMultiplier;
            EraserSizeFactor = eraserSizeFactor;
            TouchMultiplier = touchMultiplier;
        }

        public bool Enabled { get; }
        public bool IsActive { get; }
        public bool IsQuadIr { get; }
        public bool IsSpecialScreen { get; }
        public double BoundsWidthDip { get; }
        public double ThresholdFactor { get; }
        public double SensitivityMultiplier { get; }
        public double EraserSizeFactor { get; }
        public double TouchMultiplier { get; }
    }

    internal readonly struct NativeInkRouteContext
    {
        public NativeInkRouteContext(
            CanvasHitZone hitZone,
            LogicalInkTool tool,
            bool canvasInputEnabled,
            bool pageFrozen,
            bool videoPresenter,
            bool multiTouchWriting,
            bool twoFingerGestureAllowed,
            int activeTouchCount,
            PalmRoutePolicy palm)
        {
            HitZone = hitZone;
            Tool = tool;
            CanvasInputEnabled = canvasInputEnabled;
            PageFrozen = pageFrozen;
            VideoPresenter = videoPresenter;
            MultiTouchWriting = multiTouchWriting;
            TwoFingerGestureAllowed = twoFingerGestureAllowed;
            ActiveTouchCount = activeTouchCount;
            Palm = palm;
        }

        public CanvasHitZone HitZone { get; }
        public LogicalInkTool Tool { get; }
        public bool CanvasInputEnabled { get; }
        public bool PageFrozen { get; }
        public bool VideoPresenter { get; }
        public bool MultiTouchWriting { get; }
        public bool TwoFingerGestureAllowed { get; }
        public int ActiveTouchCount { get; }
        public PalmRoutePolicy Palm { get; }
    }

    internal readonly struct NativeInkRouteDecision
    {
        public NativeInkRouteDecision(
            NativeInputRoute route,
            bool consumeNativeMessage,
            bool allowWpfPromotion,
            bool suppressPointEmission = false,
            double palmEraserWidthDip = 0,
            int gestureTakeoverDelayMilliseconds = 0)
        {
            Route = route;
            ConsumeNativeMessage = consumeNativeMessage;
            AllowWpfPromotion = allowWpfPromotion;
            SuppressPointEmission = suppressPointEmission;
            PalmEraserWidthDip = palmEraserWidthDip;
            GestureTakeoverDelayMilliseconds = gestureTakeoverDelayMilliseconds;
        }

        public NativeInputRoute Route { get; }
        public bool ConsumeNativeMessage { get; }
        public bool AllowWpfPromotion { get; }
        public bool SuppressPointEmission { get; }
        public double PalmEraserWidthDip { get; }
        public int GestureTakeoverDelayMilliseconds { get; }
    }

    internal static class NativeInkInputRouter
    {
        private const int MultiTouchTakeoverDelayMilliseconds = 100;

        public static NativeInkRouteDecision DecideDown(
            NativePointerFacts pointer,
            NativeInkRouteContext context)
        {
            if (!context.CanvasInputEnabled || context.HitZone == CanvasHitZone.Outside)
                return Decision(NativeInputRoute.PassThrough, false, true);
            if (IsUiZone(context.HitZone))
                return Decision(NativeInputRoute.DeferToWpfUi, false, true);
            if (context.HitZone == CanvasHitZone.CanvasContent && context.Tool == LogicalInkTool.Select)
                return Decision(NativeInputRoute.DeferToWpfUi, false, true);
            if (pointer.IsPromotedMouse && pointer.Kind == NativeInkInputKind.Mouse)
                return Decision(NativeInputRoute.IgnorePromotedInput, true, false);

            if (context.VideoPresenter && context.Tool != LogicalInkTool.Pen)
                return Decision(NativeInputRoute.VideoGesture, false, true);

            if (context.PageFrozen && IsMutation(context.Tool, pointer))
                return Decision(NativeInputRoute.BlockedFrozen, true, false);

            if (pointer.Kind == NativeInkInputKind.Pen
                && (HasFlag(pointer.Flags, NativeInkSampleFlags.Inverted)
                    || HasFlag(pointer.Flags, NativeInkSampleFlags.Eraser)))
            {
                return Decision(NativeInputRoute.PointErase, false, true);
            }

            switch (context.Tool)
            {
                case LogicalInkTool.Cursor:
                    return Decision(NativeInputRoute.PassThrough, false, true);
                case LogicalInkTool.PointEraser:
                    return Decision(NativeInputRoute.PointErase, false, true);
                case LogicalInkTool.StrokeEraser:
                    return Decision(NativeInputRoute.StrokeErase, false, true);
                case LogicalInkTool.Select:
                    return Decision(NativeInputRoute.Select, false, true);
                case LogicalInkTool.Shape:
                    return Decision(NativeInputRoute.Shape, false, true);
                case LogicalInkTool.BoardRoam:
                    return Decision(NativeInputRoute.BoardRoam, false, true);
                case LogicalInkTool.Pen:
                    return DecidePen(pointer, context);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static NativeInkRouteDecision DecideCaptured(
            NativePointerFacts pointer,
            NativeInkRouteContext context,
            NativeInkRouteDecision capturedDecision)
        {
            if (HasFlag(pointer.Flags, NativeInkSampleFlags.Canceled))
                return Decision(NativeInputRoute.PassThrough, true, false);
            if (context.PageFrozen && capturedDecision.Route == NativeInputRoute.Ink)
                return Decision(NativeInputRoute.BlockedFrozen, true, false);
            if (capturedDecision.Route == NativeInputRoute.Ink
                && pointer.Kind == NativeInkInputKind.Touch
                && !context.MultiTouchWriting
                && context.TwoFingerGestureAllowed
                && context.ActiveTouchCount >= 2)
            {
                return new NativeInkRouteDecision(
                    NativeInputRoute.CanvasGesture,
                    false,
                    true,
                    gestureTakeoverDelayMilliseconds: MultiTouchTakeoverDelayMilliseconds);
            }

            if (capturedDecision.Route == NativeInputRoute.Ink && pointer.SecondaryBarrelButtonDown)
            {
                return new NativeInkRouteDecision(
                    capturedDecision.Route,
                    capturedDecision.ConsumeNativeMessage,
                    capturedDecision.AllowWpfPromotion,
                    suppressPointEmission: true);
            }

            return capturedDecision;
        }

        private static NativeInkRouteDecision DecidePen(
            NativePointerFacts pointer,
            NativeInkRouteContext context)
        {
            if (pointer.Kind == NativeInkInputKind.Touch)
            {
                if (context.MultiTouchWriting)
                    return Decision(NativeInputRoute.Ink, true, false);
                if (TryGetPalmEraserWidth(pointer, context.Palm, out var eraserWidth))
                {
                    return new NativeInkRouteDecision(
                        NativeInputRoute.PointErase,
                        false,
                        true,
                        palmEraserWidthDip: eraserWidth);
                }
                if (context.TwoFingerGestureAllowed && context.ActiveTouchCount >= 2)
                {
                    return new NativeInkRouteDecision(
                        NativeInputRoute.CanvasGesture,
                        false,
                        true,
                        gestureTakeoverDelayMilliseconds: MultiTouchTakeoverDelayMilliseconds);
                }
            }

            return new NativeInkRouteDecision(
                NativeInputRoute.Ink,
                true,
                false,
                suppressPointEmission: pointer.SecondaryBarrelButtonDown);
        }

        private static bool TryGetPalmEraserWidth(
            NativePointerFacts pointer,
            PalmRoutePolicy policy,
            out double eraserWidthDip)
        {
            eraserWidthDip = 0;
            if (!policy.Enabled || policy.IsActive)
                return policy.IsActive;
            if (policy.IsSpecialScreen && policy.TouchMultiplier == 0)
                return false;

            var boundWidth = policy.IsQuadIr
                ? Math.Sqrt(Math.Max(0, pointer.ContactWidthDip * pointer.ContactHeightDip))
                : pointer.ContactWidthDip;
            var threshold = policy.BoundsWidthDip
                            * policy.ThresholdFactor
                            * policy.SensitivityMultiplier;
            if (boundWidth <= policy.BoundsWidthDip || boundWidth <= threshold)
                return false;

            eraserWidthDip = boundWidth
                             * policy.EraserSizeFactor
                             * (policy.IsSpecialScreen ? policy.TouchMultiplier : 1);
            return true;
        }

        private static bool IsUiZone(CanvasHitZone hitZone)
        {
            return hitZone == CanvasHitZone.UiChrome
                   || hitZone == CanvasHitZone.SelectionOverlay
                   || hitZone == CanvasHitZone.InteractiveCanvasChild;
        }

        private static bool IsMutation(LogicalInkTool tool, NativePointerFacts pointer)
        {
            return tool != LogicalInkTool.Cursor
                   && tool != LogicalInkTool.BoardRoam
                   || pointer.Kind == NativeInkInputKind.Pen
                   && (HasFlag(pointer.Flags, NativeInkSampleFlags.Inverted)
                       || HasFlag(pointer.Flags, NativeInkSampleFlags.Eraser));
        }

        private static bool HasFlag(NativeInkSampleFlags value, NativeInkSampleFlags flag)
        {
            return (value & flag) != 0;
        }

        private static NativeInkRouteDecision Decision(
            NativeInputRoute route,
            bool consumeNativeMessage,
            bool allowWpfPromotion)
        {
            return new NativeInkRouteDecision(route, consumeNativeMessage, allowWpfPromotion);
        }
    }
}
