using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace InkCanvasWordLinkHost;

/// <summary>One outstanding pipe request. All COM remains on a pumping STA.</summary>
internal sealed class StaRequestLoop
{
    private const uint WorkMessage = 0x8001;
    private readonly ConcurrentQueue<Pending> _requests = new();
    private readonly Action<string> _handle;
    private readonly uint _thread;
    private volatile bool _closing;
    internal StaRequestLoop(Action<string> handle)
    {
        _handle = handle; _thread = GetCurrentThreadId();
        PeekMessage(out _, IntPtr.Zero, 0, 0, 0); // Create the queue before posting.
    }

    internal void Run()
    {
        _ = Task.Run(ReadAsync);
        try
        {
            int result;
            while ((result = GetMessage(out var message, IntPtr.Zero, 0, 0)) > 0)
            {
                if (message.Window == IntPtr.Zero && message.Id == WorkMessage)
                {
                    if (!_requests.TryDequeue(out var request)) continue;
                    try { _handle(request.Line); }
                    finally { request.Completion.TrySetResult(); }
                }
                else { TranslateMessage(ref message); DispatchMessage(ref message); }
            }
            if (result < 0) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
        finally
        {
            _closing = true;
            while (_requests.TryDequeue(out var request)) request.Completion.TrySetResult();
        }
    }

    private async Task ReadAsync()
    {
        try
        {
            while (!_closing)
            {
                string line = await Console.In.ReadLineAsync().ConfigureAwait(false);
                if (line == null || _closing) break;
                if (line.Length > 8192) break;
                var request = new Pending(line);
                _requests.Enqueue(request);
                if (!PostThreadMessage(_thread, WorkMessage, UIntPtr.Zero, IntPtr.Zero)) break;
                await request.Completion.Task.ConfigureAwait(false);
            }
        }
        catch (Exception ex) { Console.Error.WriteLine($"[WordLinkHost] Input: {ex.GetType().Name}"); }
        finally { PostThreadMessage(_thread, 0x0012, UIntPtr.Zero, IntPtr.Zero); }
    }

    private sealed class Pending(string line)
    {
        internal readonly string Line = line;
        internal readonly TaskCompletionSource Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    [StructLayout(LayoutKind.Sequential)] private struct Point { internal int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeMessage
    {
        internal IntPtr Window; internal uint Id; internal UIntPtr WParam; internal IntPtr LParam;
        internal uint Time; internal Point Point; internal uint Private;
    }
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] private static extern bool PeekMessage(out NativeMessage message, IntPtr window, uint first, uint last, uint flags);
    [DllImport("user32.dll", SetLastError = true)] private static extern int GetMessage(out NativeMessage message, IntPtr window, uint first, uint last);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref NativeMessage message);
    [DllImport("user32.dll")] private static extern IntPtr DispatchMessage(ref NativeMessage message);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool PostThreadMessage(uint thread, uint message, UIntPtr wParam, IntPtr lParam);
}
