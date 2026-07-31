namespace Ink_Canvas.Mathematics.Models
{
    public sealed class CoordinatePlaneObject : MathObject
    {
        public CoordinatePlaneObject() : base(MathObjectType.CoordinatePlane)
        {
            Center = new MathPoint(0, 0);
            Width = 640;
            Height = 400;
            GridSpacing = 40;
            ShowGrid = true;
            ShowAxes = true;
            ZIndex = -100;
        }

        public MathPoint Center { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }

        public double GridSpacing { get; set; }

        public bool ShowGrid { get; set; }

        public bool ShowAxes { get; set; }
    }
}
