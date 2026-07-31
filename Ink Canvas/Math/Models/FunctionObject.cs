namespace Ink_Canvas.Mathematics.Models
{
    public sealed class FunctionObject : MathObject
    {
        public FunctionObject() : base(MathObjectType.Function)
        {
            Expression = "x";
            DomainMin = -10;
            DomainMax = 10;
            Origin = new MathPoint(0, 0);
            PixelsPerUnit = 40;
            RotationDegrees = 0;
            SampleQuality = 2;
            ShowZeros = true;
            ShowExtrema = true;
            ShowIntersections = true;
        }

        public string Expression { get; set; }

        public double DomainMin { get; set; }

        public double DomainMax { get; set; }

        public MathPoint Origin { get; set; }

        public System.Guid? CoordinatePlaneId { get; set; }

        public double PixelsPerUnit { get; set; }

        public double RotationDegrees { get; set; }

        public int SampleQuality { get; set; }

        public bool ShowZeros { get; set; }

        public bool ShowExtrema { get; set; }

        public bool ShowIntersections { get; set; }
    }
}
