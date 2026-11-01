using System;
using System.Threading;

namespace Ink_Canvas.Helpers
{
    internal static class DrawingActivity
    {
        private static long _lastInput;
        internal static void Record() => Interlocked.Exchange(ref _lastInput, Environment.TickCount64);
        internal static bool IsQuiet(int milliseconds) => Environment.TickCount64 - Interlocked.Read(ref _lastInput) >= milliseconds;
    }
}
