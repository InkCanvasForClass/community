using System;
using System.Collections.Generic;

namespace Ink_Canvas.Ink.Native
{
    internal enum NativePointerMessageKind
    {
        Down,
        Update,
        Up,
        CaptureLost
    }

    internal sealed class NativePointerInputBatch
    {
        private readonly RawInkSample[] _samplesNewestFirst;

        public NativePointerInputBatch(
            uint pointerId,
            NativeInkInputKind inputKind,
            NativePointerMessageKind messageKind,
            IReadOnlyList<RawInkSample> samplesNewestFirst,
            bool secondaryBarrelButtonDown,
            bool isPromotedMouse,
            bool historyComplete,
            int historyReadError = 0,
            bool isWpfFallback = false)
            : this(
                pointerId,
                inputKind,
                messageKind,
                CopySamples(samplesNewestFirst),
                secondaryBarrelButtonDown,
                isPromotedMouse,
                historyComplete,
                historyReadError,
                isWpfFallback,
                takeOwnership: true)
        {
        }

        private NativePointerInputBatch(
            uint pointerId,
            NativeInkInputKind inputKind,
            NativePointerMessageKind messageKind,
            RawInkSample[] samplesNewestFirst,
            bool secondaryBarrelButtonDown,
            bool isPromotedMouse,
            bool historyComplete,
            int historyReadError,
            bool isWpfFallback,
            bool takeOwnership)
        {
            if (samplesNewestFirst == null)
                throw new ArgumentNullException(nameof(samplesNewestFirst));
            if (messageKind != NativePointerMessageKind.CaptureLost && samplesNewestFirst.Length == 0)
                throw new ArgumentException("Pointer input messages require at least one sample.", nameof(samplesNewestFirst));

            PointerId = pointerId;
            InputKind = inputKind;
            MessageKind = messageKind;
            SecondaryBarrelButtonDown = secondaryBarrelButtonDown;
            IsPromotedMouse = isPromotedMouse;
            HistoryComplete = historyComplete;
            HistoryReadError = historyReadError;
            IsWpfFallback = isWpfFallback;
            _samplesNewestFirst = takeOwnership
                ? samplesNewestFirst
                : CopySamples(samplesNewestFirst);
        }

        public uint PointerId { get; }
        public NativeInkInputKind InputKind { get; }
        public NativePointerMessageKind MessageKind { get; }
        public IReadOnlyList<RawInkSample> SamplesNewestFirst => _samplesNewestFirst;
        internal RawInkSample[] SamplesNewestFirstArray => _samplesNewestFirst;
        public bool SecondaryBarrelButtonDown { get; }
        public bool IsPromotedMouse { get; }
        public bool HistoryComplete { get; }
        public int HistoryReadError { get; }
        /// <summary>True when samples came from WPF Stylus/Touch because WM_POINTER was unavailable.</summary>
        public bool IsWpfFallback { get; }

        internal static NativePointerInputBatch CreateFromOwnedSamples(
            uint pointerId,
            NativeInkInputKind inputKind,
            NativePointerMessageKind messageKind,
            RawInkSample[] samplesNewestFirst,
            bool secondaryBarrelButtonDown,
            bool isPromotedMouse,
            bool historyComplete,
            int historyReadError = 0,
            bool isWpfFallback = false)
        {
            return new NativePointerInputBatch(
                pointerId,
                inputKind,
                messageKind,
                samplesNewestFirst,
                secondaryBarrelButtonDown,
                isPromotedMouse,
                historyComplete,
                historyReadError,
                isWpfFallback,
                takeOwnership: true);
        }

        private static RawInkSample[] CopySamples(IReadOnlyList<RawInkSample> samplesNewestFirst)
        {
            if (samplesNewestFirst == null)
                throw new ArgumentNullException(nameof(samplesNewestFirst));
            var copy = new RawInkSample[samplesNewestFirst.Count];
            for (var i = 0; i < samplesNewestFirst.Count; i++)
                copy[i] = samplesNewestFirst[i];
            return copy;
        }

        private static RawInkSample[] CopySamples(RawInkSample[] samplesNewestFirst)
        {
            if (samplesNewestFirst == null)
                throw new ArgumentNullException(nameof(samplesNewestFirst));
            var copy = new RawInkSample[samplesNewestFirst.Length];
            Array.Copy(samplesNewestFirst, copy, copy.Length);
            return copy;
        }
    }

    internal delegate bool NativePointerInputHandler(NativePointerInputBatch batch);
}
