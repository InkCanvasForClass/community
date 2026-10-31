namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// Exposes presentation-neutral canvas appearance information to plugins.
    /// </summary>
    public interface ICanvasAppearanceService
    {
        /// <summary>
        /// Returns an ARGB hex color suitable for foreground content on the current canvas background.
        /// </summary>
        string GetContrastingForegroundColor();
    }
}
