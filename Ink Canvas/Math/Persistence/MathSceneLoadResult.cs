using System.Collections.Generic;
using Ink_Canvas.Mathematics.Models;

namespace Ink_Canvas.Mathematics.Persistence
{
    public sealed class MathSceneLoadResult
    {
        public MathSceneLoadResult(MathScene scene, IReadOnlyList<string> issues)
        {
            Scene = scene;
            Issues = issues;
        }

        public MathScene Scene { get; }

        public IReadOnlyList<string> Issues { get; }
    }
}
