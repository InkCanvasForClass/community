using System.IO;
using System.Windows.Ink;

namespace Ink_Canvas.Ink
{
    internal sealed class StrokeSaveSnapshot
    {
        private readonly StrokeCollection _strokes;
        private readonly object _sync = new();
        // Capture is called on the owner thread; ISF compression uses only this deep copy.
        private StrokeSaveSnapshot(StrokeCollection strokes) => _strokes = strokes.Clone();
        internal static StrokeSaveSnapshot Capture(StrokeCollection strokes) => new(strokes);
        internal byte[] Encode()
        {
            lock (_sync)
            {
                using var buffer = new MemoryStream();
                _strokes.Save(buffer);
                return buffer.ToArray();
            }
        }
    }
}
