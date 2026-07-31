using System.Collections.Generic;
using Ink_Canvas.Mathematics.Models;

namespace Ink_Canvas.Mathematics.Services
{
    public readonly struct SolidEdge
    {
        public SolidEdge(int start, int end)
        {
            Start = start;
            End = end;
        }

        public int Start { get; }

        public int End { get; }
    }

    public sealed class SolidMesh
    {
        public SolidMesh()
        {
            Vertices = new List<MathPoint3D>();
            Edges = new List<SolidEdge>();
            Faces = new List<int[]>();
        }

        public List<MathPoint3D> Vertices { get; }

        public List<SolidEdge> Edges { get; }

        public List<int[]> Faces { get; }
    }
}
