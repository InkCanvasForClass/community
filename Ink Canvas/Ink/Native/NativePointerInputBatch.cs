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
            int historyReadError = 0)
        {
            if (samplesNewestFirst == null)
                throw new ArgumentNullException(nameof(samplesNewestFirst));
            if (messageKind != NativePointerMessageKind.CaptureLost && samplesNewestFirst.Count == 0)
                throw new ArgumentException("Pointer input messages require at least one sample.", nameof(samplesNewestFirst));

            PointerId = pointerId;
            InputKind = inputKind;
            MessageKind = messageKind;
            SecondaryBarrelButtonDown = secondaryBarrelButtonDown;
            IsPromotedMouse = isPromotedMouse;
            HistoryComplete = historyComplete;
            HistoryReadError = historyReadError;
            _samplesNewestFirst = new RawInkSample[samplesNewestFirst.Count];
            for (var i = 0; i < samplesNewestFirst.Count; i++)
                _samplesNewestFirst[i] = samplesNewestFirst[i];
        }

        public uint PointerId { get; }
        public NativeInkInputKind InputKind { get; }
        public NativePointerMessageKind MessageKind { get; }
        public IReadOnlyList<RawInkSample> SamplesNewestFirst => _samplesNewestFirst;
        public bool SecondaryBarrelButtonDown { get; }
        public bool IsPromotedMouse { get; }
        public bool HistoryComplete { get; }
        public int HistoryReadError { get; }
    }

    internal delegate bool NativePointerInputHandler(NativePointerInputBatch batch);
}
