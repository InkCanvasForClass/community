using System;

namespace Ink_Canvas.Mathematics.Models
{
    public enum MathObjectType
    {
        Point,
        Segment,
        Circle,
        TextLabel,
        Line,
        Ray,
        AngleMeasurement,
        Function,
        Solid,
        CoordinatePlane,
        Triangle
    }

    public enum MathObjectSource
    {
        Manual,
        Imported
    }

    public abstract class MathObject
    {
        protected MathObject(MathObjectType type)
        {
            Id = Guid.NewGuid();
            ObjectVersion = 1;
            Type = type;
            Source = MathObjectSource.Manual;
            IsVisible = true;
            StrokeColor = "#FF000000";
            StrokeWidth = 2;
        }

        public Guid Id { get; set; }

        public int ObjectVersion { get; set; }

        public MathObjectType Type { get; }

        public MathObjectSource Source { get; set; }

        public bool IsVisible { get; set; }

        public bool IsLocked { get; set; }

        public string StrokeColor { get; set; }

        public double StrokeWidth { get; set; }

        public int ZIndex { get; set; }

        public SolidAttachment SolidAttachment { get; set; }
    }
}
