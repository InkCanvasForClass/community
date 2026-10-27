using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ink_Canvas.Helpers
{
    /// <summary>One ordered writer; repeated config updates coalesce per path.</summary>
    internal static class AtomicFileWriter
    {
        private sealed class PendingWrite
        {
            public string Text;
            public readonly List<TaskCompletionSource<bool>> Completions = new();
        }
        private static readonly Dictionary<string, PendingWrite> Pending = new(StringComparer.OrdinalIgnoreCase);
        private static readonly object Sync = new();
        private static Task _writer = Task.CompletedTask;

        internal static void WriteLatest(string path, string text)
            => Queue(path, text, null);

        internal static Task<bool> WriteLatestAsync(string path, string text)
        {
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Queue(path, text, completion);
            return completion.Task;
        }

        private static void Queue(string path, string text, TaskCompletionSource<bool> completion)
        {
            path = Path.GetFullPath(path);
            lock (Sync)
            {
                if (!Pending.TryGetValue(path, out var pending))
                    Pending[path] = pending = new PendingWrite();
                pending.Text = text;
                if (completion != null) pending.Completions.Add(completion);
                if (_writer.IsCompleted) _writer = Task.Run(Drain);
            }
        }

        private static void Drain()
        {
            while (true)
            {
                string path;
                PendingWrite pending;
                lock (Sync)
                {
                    if (Pending.Count == 0) { _writer = Task.CompletedTask; return; }
                    var next = Pending.First();
                    path = next.Key;
                    pending = next.Value;
                    Pending.Remove(path);
                }
                bool success = false;
                try { WriteNow(path, pending.Text); success = true; }
                catch (Exception ex) { LogHelper.WriteLogToFile($"[FileWriter] {path}: {ex}", LogHelper.LogType.Error); }
                foreach (var completion in pending.Completions) completion.TrySetResult(success);
            }
        }

        internal static void WriteNow(string path, string text)
            => WriteBytesNow(path, new UTF8Encoding(false).GetBytes(text));

        internal static void WriteBytesNow(string path, byte[] bytes)
        {
            path = Path.GetFullPath(path);
            var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                ProcessProtectionManager.WithWriteAccess(path, () =>
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllBytes(temp, bytes);
                    if (File.Exists(path)) File.Replace(temp, path, null);
                    else File.Move(temp, path);
                });
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }

        internal static bool Flush(TimeSpan timeout)
        {
            Task writer;
            lock (Sync) writer = _writer;
            return writer.Wait(timeout);
        }
    }
}
