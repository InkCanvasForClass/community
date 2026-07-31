namespace Ink_Canvas.Mathematics.Models
{
    public enum SolidType
    {
        Cube,
        Cuboid,
        Prism,
        Pyramid,
        Cylinder,
        Cone,
        Sphere
    }

    public enum SolidProjectionMode
    {
        Orthographic,
        Perspective
    }

    public enum SolidViewMode
    {
        Projection,
        Front
    }

    public sealed class SolidObject : MathObject
    {
        public SolidObject() : base(MathObjectType.Solid)
        {
            SolidType = SolidType.Cube;
            Center = new MathPoint(0, 0);
            Width = 3;
            Height = 3;
            Depth = 3;
            Radius = 1.5;
            Scale = 55;
            HorizontalScale = 1;
            VerticalScale = 1;
            RotationX = 0;
            RotationY = 0;
            RotationZ = 0;
            ViewMode = SolidViewMode.Projection;
            ProjectionMode = SolidProjectionMode.Orthographic;
            ShowHiddenEdges = true;
            ShowLabels = true;
            ShowAxes = false;
            RenderQuality = 2;
        }

        public SolidType SolidType { get; set; }

        public MathPoint Center { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }

        public double Depth { get; set; }

        public double Radius { get; set; }

        public double Scale { get; set; }

        public double HorizontalScale { get; set; }

        public double VerticalScale { get; set; }

        public double RotationX { get; set; }

        public double RotationY { get; set; }

        public double RotationZ { get; set; }

        public SolidViewMode ViewMode { get; set; }

        public SolidProjectionMode ProjectionMode { get; set; }

        public bool ShowHiddenEdges { get; set; }

        public bool ShowLabels { get; set; }

        public bool ShowAxes { get; set; }

        public int RenderQuality { get; set; }
    }
}
