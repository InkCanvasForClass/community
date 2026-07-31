using System.Collections.Generic;

namespace Ink_Canvas.Mathematics.Models
{
    public sealed class MathScene
    {
        public const int CurrentSchemaVersion = 3;

        public MathScene()
        {
            SchemaVersion = CurrentSchemaVersion;
            Objects = new List<MathObject>();
            Constraints = new List<MathConstraint>();
        }

        public int SchemaVersion { get; set; }

        public List<MathObject> Objects { get; set; }

        public List<MathConstraint> Constraints { get; set; }
    }
}
