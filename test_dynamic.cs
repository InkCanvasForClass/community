using System.Windows.Input.StylusPlugIns;
using System.Windows.Controls;

public class Test
{
    public void M(InkCanvas canvas)
    {
        canvas.DynamicRenderer.Enabled = false;
    }
}
