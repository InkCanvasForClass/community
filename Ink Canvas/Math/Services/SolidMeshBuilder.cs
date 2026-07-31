using System;
using Ink_Canvas.Mathematics.Models;

namespace Ink_Canvas.Mathematics.Services
{
    public static class SolidMeshBuilder
    {
        public static SolidMesh Build(SolidObject solid)
        {
            if (solid == null) throw new ArgumentNullException(nameof(solid));
            return solid.SolidType switch
            {
                SolidType.Cube => BuildBox(solid.Width, solid.Width, solid.Width),
                SolidType.Cuboid => BuildBox(solid.Width, solid.Height, solid.Depth),
                SolidType.Prism => BuildPrism(solid.Width, solid.Height, solid.Depth),
                SolidType.Pyramid => BuildPyramid(solid.Width, solid.Height, solid.Depth),
                SolidType.Cylinder => BuildCylinder(solid.Radius, solid.Height, SegmentCount(solid)),
                SolidType.Cone => BuildCone(solid.Radius, solid.Height, SegmentCount(solid)),
                SolidType.Sphere => BuildSphere(solid.Radius, SegmentCount(solid)),
                _ => throw new ArgumentOutOfRangeException(nameof(solid))
            };
        }

        private static SolidMesh BuildBox(double width, double height, double depth)
        {
            var mesh = new SolidMesh();
            var x = width / 2;
            var y = height / 2;
            var z = depth / 2;
            mesh.Vertices.AddRange(new[]
            {
                new MathPoint3D(-x, -y, -z), new MathPoint3D(x, -y, -z),
                new MathPoint3D(x, y, -z), new MathPoint3D(-x, y, -z),
                new MathPoint3D(-x, -y, z), new MathPoint3D(x, -y, z),
                new MathPoint3D(x, y, z), new MathPoint3D(-x, y, z)
            });
            AddLoop(mesh, 0, 1, 2, 3);
            AddLoop(mesh, 4, 5, 6, 7);
            AddEdges(mesh, (0, 4), (1, 5), (2, 6), (3, 7));
            mesh.Faces.AddRange(new[]
            {
                new[] { 0, 1, 2, 3 }, new[] { 4, 7, 6, 5 },
                new[] { 0, 4, 5, 1 }, new[] { 1, 5, 6, 2 },
                new[] { 2, 6, 7, 3 }, new[] { 3, 7, 4, 0 }
            });
            return mesh;
        }

        private static SolidMesh BuildPrism(double width, double height, double depth)
        {
            var mesh = new SolidMesh();
            var x = width / 2;
            var y = height / 2;
            var z = depth / 2;
            mesh.Vertices.AddRange(new[]
            {
                new MathPoint3D(-x, y, -z), new MathPoint3D(-x, -y, -z), new MathPoint3D(x, -y, -z),
                new MathPoint3D(-x, y, z), new MathPoint3D(-x, -y, z), new MathPoint3D(x, -y, z)
            });
            AddLoop(mesh, 0, 1, 2);
            AddLoop(mesh, 3, 4, 5);
            AddEdges(mesh, (0, 3), (1, 4), (2, 5));
            mesh.Faces.AddRange(new[]
            {
                new[] { 0, 2, 1 }, new[] { 3, 4, 5 },
                new[] { 0, 3, 5, 2 }, new[] { 0, 1, 4, 3 },
                new[] { 1, 2, 5, 4 }
            });
            return mesh;
        }

        private static SolidMesh BuildPyramid(double width, double height, double depth)
        {
            var mesh = new SolidMesh();
            var x = width / 2;
            var z = depth / 2;
            var y = height / 2;
            mesh.Vertices.AddRange(new[]
            {
                new MathPoint3D(-x, -y, -z), new MathPoint3D(x, -y, -z),
                new MathPoint3D(x, -y, z), new MathPoint3D(-x, -y, z),
                new MathPoint3D(0, y, 0)
            });
            AddLoop(mesh, 0, 1, 2, 3);
            AddEdges(mesh, (0, 4), (1, 4), (2, 4), (3, 4));
            mesh.Faces.AddRange(new[]
            {
                new[] { 0, 3, 2, 1 }, new[] { 0, 1, 4 },
                new[] { 1, 2, 4 }, new[] { 2, 3, 4 }, new[] { 3, 0, 4 }
            });
            return mesh;
        }

        private static SolidMesh BuildCylinder(double radius, double height, int segments)
        {
            var mesh = new SolidMesh();
            var halfHeight = height / 2;
            for (var i = 0; i < segments; i++)
            {
                var angle = 2 * Math.PI * i / segments;
                var x = radius * Math.Cos(angle);
                var z = radius * Math.Sin(angle);
                mesh.Vertices.Add(new MathPoint3D(x, -halfHeight, z));
                mesh.Vertices.Add(new MathPoint3D(x, halfHeight, z));
            }
            for (var i = 0; i < segments; i++)
            {
                var next = (i + 1) % segments;
                AddEdges(mesh, (i * 2, next * 2), (i * 2 + 1, next * 2 + 1));
                mesh.Faces.Add(new[] { i * 2, next * 2, next * 2 + 1, i * 2 + 1 });
            }
            AddEdges(mesh, (0, 1), (segments, segments + 1));
            return mesh;
        }

        private static SolidMesh BuildCone(double radius, double height, int segments)
        {
            var mesh = new SolidMesh();
            var halfHeight = height / 2;
            for (var i = 0; i < segments; i++)
            {
                var angle = 2 * Math.PI * i / segments;
                mesh.Vertices.Add(new MathPoint3D(
                    radius * Math.Cos(angle),
                    -halfHeight,
                    radius * Math.Sin(angle)));
            }
            var apex = mesh.Vertices.Count;
            mesh.Vertices.Add(new MathPoint3D(0, halfHeight, 0));
            for (var i = 0; i < segments; i++)
            {
                var next = (i + 1) % segments;
                AddEdges(mesh, (i, next));
                mesh.Faces.Add(new[] { i, next, apex });
            }
            AddEdges(mesh, (0, apex), (segments / 2, apex));
            return mesh;
        }

        private static SolidMesh BuildSphere(double radius, int segments)
        {
            var mesh = new SolidMesh();
            for (var circle = 0; circle < 3; circle++)
            {
                var start = mesh.Vertices.Count;
                for (var index = 0; index < segments; index++)
                {
                    var angle = 2 * Math.PI * index / segments;
                    var cosine = radius * Math.Cos(angle);
                    var sine = radius * Math.Sin(angle);
                    mesh.Vertices.Add(circle switch
                    {
                        0 => new MathPoint3D(cosine, sine, 0),
                        1 => new MathPoint3D(cosine, 0, sine),
                        _ => new MathPoint3D(0, cosine, sine)
                    });
                }
                for (var index = 0; index < segments; index++)
                    AddEdges(mesh, (start + index, start + (index + 1) % segments));
            }
            return mesh;
        }

        private static int SegmentCount(SolidObject solid)
        {
            return solid.RenderQuality switch
            {
                <= 1 => 12,
                2 => 20,
                _ => 32
            };
        }

        private static void AddLoop(SolidMesh mesh, params int[] indices)
        {
            for (var i = 0; i < indices.Length; i++)
                mesh.Edges.Add(new SolidEdge(indices[i], indices[(i + 1) % indices.Length]));
        }

        private static void AddEdges(SolidMesh mesh, params (int Start, int End)[] edges)
        {
            for (var i = 0; i < edges.Length; i++)
                mesh.Edges.Add(new SolidEdge(edges[i].Start, edges[i].End));
        }
    }
}
