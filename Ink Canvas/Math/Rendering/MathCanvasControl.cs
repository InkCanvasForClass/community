using System.Windows;
using Ink_Canvas.Mathematics.Models;

namespace Ink_Canvas.Mathematics.Rendering
{
    public sealed class MathCanvasControl : FrameworkElement
    {
        public static readonly DependencyProperty SceneProperty = DependencyProperty.Register(
            nameof(Scene),
            typeof(MathScene),
            typeof(MathCanvasControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowMeasurementsProperty = DependencyProperty.Register(
            nameof(ShowMeasurements),
            typeof(bool),
            typeof(MathCanvasControl),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public MathCanvasControl()
        {
            Scene = new MathScene();
            IsHitTestVisible = false;
        }

        public MathScene Scene
        {
            get => (MathScene)GetValue(SceneProperty);
            set => SetValue(SceneProperty, value);
        }

        public bool ShowMeasurements
        {
            get => (bool)GetValue(ShowMeasurementsProperty);
            set => SetValue(ShowMeasurementsProperty, value);
        }

        public void Refresh()
        {
            InvalidateVisual();
        }

    }
}
