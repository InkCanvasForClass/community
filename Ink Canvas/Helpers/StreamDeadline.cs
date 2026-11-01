using System;
using System.IO;
using System.Threading;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// Bounds the entire synchronous exchange, including writes and partial reads.
    /// Closing the owned stream interrupts pending Windows pipe I/O. Disposing the
    /// registration prevents a late timeout from closing a reused connection.
    /// </summary>
    internal sealed class StreamDeadline : IDisposable
    {
        private readonly CancellationTokenSource _timeout;
        private readonly CancellationTokenRegistration _registration;

        internal StreamDeadline(Stream stream, int milliseconds)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (milliseconds <= 0) throw new ArgumentOutOfRangeException(nameof(milliseconds));
            _timeout = new CancellationTokenSource(milliseconds);
            _registration = _timeout.Token.Register(() =>
            {
                try { stream.Dispose(); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
            });
        }

        public void Dispose()
        {
            _registration.Dispose();
            _timeout.Dispose();
        }
    }
}
