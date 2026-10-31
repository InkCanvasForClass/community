using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Ink;
using InkCanvasWordLink;

namespace Ink_Canvas.WordLink
{
    internal sealed class WordInkDocument
    {
        internal Dictionary<int, StrokeCollection> Pages = new();
        internal readonly Dictionary<int, string> Layout = new();
        internal readonly Stack<Dictionary<int, StrokeCollection>> Undo = new();
        internal readonly Stack<Dictionary<int, StrokeCollection>> Redo = new();
        internal bool Dirty;
        internal int Version;
        internal bool Saving;
        internal bool Superseded;
        internal string SaveKey, SaveRevision;
        internal long RetryAfter;
    }

    internal sealed class WordAnnotationStore
    {
        private readonly string _root;
        private Task<bool> _pending = Task.FromResult(true);
        private readonly object _sync = new();
        private readonly ConditionalWeakTable<StrokeCollection, Stroke[]> _canonicalSnapshots = new();
        private readonly ConditionalWeakTable<Stroke, Stroke> _strokeSnapshots = new();
        private readonly Dictionary<string, (Dictionary<int, Stroke[]> Pages, Dictionary<int, string> Layout)> _latest = new();
        internal WordAnnotationStore(string root) { _root = root; }
        private string PathFor(string key, string revision)
        {
            if (!WordFrame.IsHash(key) || !WordFrame.IsHash(revision)) throw new InvalidDataException("Invalid document identity.");
            return Path.Combine(_root, key, revision + ".json");
        }

        internal Task<WordInkDocument> LoadAsync(string key, string revision) => Task.Run(() =>
        {
            string path = PathFor(key, revision);
            if (!File.Exists(path)) return new WordInkDocument();
            if (new FileInfo(path).Length > 64 * 1024 * 1024) throw new InvalidDataException("Annotation file exceeds limit.");
            var data = JsonSerializer.Deserialize<SavedDocument>(File.ReadAllText(path));
            if (data?.Version != 1 || data.Pages == null || data.Pages.Count > 10000) throw new InvalidDataException("Unknown annotation schema.");
            var result = new WordInkDocument();
            foreach (var page in data.Pages)
            {
                if (page.Number <= 0 || page.Number > 100000 || page.Ink == null || page.Ink.Length > 16 * 1024 * 1024)
                    throw new InvalidDataException("Invalid annotation page.");
                using var stream = new MemoryStream(Convert.FromBase64String(page.Ink));
                var strokes = new StrokeCollection(stream);
                if (!result.Pages.TryAdd(page.Number, strokes)) throw new InvalidDataException("Duplicate annotation page.");
                result.Layout[page.Number] = page.Layout;
            }
            return result;
        });

        internal Task<bool> SaveAsync(string key, string revision, WordInkDocument document)
        {
            // Stroke data is cloned on its owning UI thread. Serialization and disk I/O
            // are ordered in the background, including during document switches.
            var pages = document.Pages.ToDictionary(p => p.Key, p => p.Value.Select(stroke => stroke.Clone()).ToArray());
            return Enqueue(key, revision, pages, document.Layout);
        }

        // Controller canonical pages are immutable snapshots: projection always
        // edits separate screen-space clones and replaces a page on commit/undo.
        // Copy each stroke once per edit, reusing unchanged page snapshots. Worker
        // serialization never touches the canonical strokes read by the UI.
        // Mutable callers must continue to use SaveAsync above.
        internal Task<bool> SaveCanonicalAsync(string key, string revision, WordInkDocument document)
            => Enqueue(key, revision, document.Pages.ToDictionary(p => p.Key,
                p => _canonicalSnapshots.GetValue(p.Value, page => page.Select(
                    stroke => _strokeSnapshots.GetValue(stroke, ink => ink.Clone())).ToArray())), document.Layout);

        private Task<bool> Enqueue(string key, string revision, Dictionary<int, Stroke[]> pages,
            Dictionary<int, string> layout)
        {
            var layouts = new Dictionary<int, string>(layout);
            string path = PathFor(key, revision);
            lock (_sync)
            {
                if (!_latest.ContainsKey(path) && _latest.Count >= 8)
                    return Task.FromResult(false);
                _latest[path] = (pages, layouts);
                if (_pending.IsCompleted) _pending = Task.Run(DrainAsync);
                return _pending;
            }
        }

        private async Task<bool> DrainAsync()
        {
            bool success = true;
            while (true)
            {
                string path;
                (Dictionary<int, Stroke[]> Pages, Dictionary<int, string> Layout) snapshot;
                lock (_sync)
                {
                    if (_latest.Count == 0)
                    {
                        _pending = Task.FromResult(success);
                        return success;
                    }
                    var first = _latest.First(); path = first.Key; snapshot = first.Value;
                    _latest.Remove(path);
                }
                try
                {
                    var saved = new SavedDocument();
                    foreach (var page in snapshot.Pages.OrderBy(p => p.Key))
                    {
                        using var stream = new MemoryStream();
                        // Arrays keep cached strokes independent of transient page
                        // collections. Release collection subscriptions after ISF
                        // encoding so one unchanged stroke cannot retain old pages.
                        var strokes = new StrokeCollection(page.Value);
                        try { strokes.Save(stream); }
                        finally { strokes.Clear(); }
                        saved.Pages.Add(new SavedPage { Number = page.Key, Layout = snapshot.Layout.GetValueOrDefault(page.Key),
                            Ink = Convert.ToBase64String(stream.ToArray()) });
                    }
                    success &= await Helpers.AtomicFileWriter.WriteLatestAsync(path, JsonSerializer.Serialize(saved)).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Helpers.LogHelper.WriteLogToFile($"[WordLink] Annotation save failed: {ex.GetType().Name}", Helpers.LogHelper.LogType.Error);
                    success = false;
                }
            }
        }

        internal bool Flush(TimeSpan timeout)
        {
            Task<bool> pending; lock (_sync) pending = _pending;
            return pending.Wait(timeout) && pending.Result;
        }
        internal Task<bool> WaitPendingAsync() { lock (_sync) return _pending; }
        private sealed class SavedDocument { public int Version { get; set; } = 1; public List<SavedPage> Pages { get; set; } = new(); }
        private sealed class SavedPage { public int Number { get; set; } public string Layout { get; set; } public string Ink { get; set; } }
    }
}
