namespace Ink_Canvas.Mathematics.Models
{
    public sealed class TextLabelObject : MathObject
    {
        public TextLabelObject() : base(MathObjectType.TextLabel)
        {
            Text = string.Empty;
        }

        public MathPoint Position { get; set; }

        public string Text { get; set; }
    }
}
