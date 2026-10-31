namespace Ink_Canvas.WordLink
{
    internal sealed class WordBoundaryToolState
    {
        private (string Mode, int Shape)? _resume;

        internal (string Mode, int Shape)? Update(bool inside, bool chrome, bool inputActive, string mode, int shape)
        {
            if (chrome || inputActive) return null;
            if (inside)
            {
                var restore = mode == "cursor" ? _resume : null;
                _resume = null;
                return restore;
            }
            if (mode == "cursor") return null;
            _resume = (mode, shape);
            return ("cursor", 0);
        }

        internal void UserSelectedTool() => _resume = null;
    }
}
