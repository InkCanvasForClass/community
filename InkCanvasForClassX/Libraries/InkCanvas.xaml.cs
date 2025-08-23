using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace InkCanvasForClassX.Libraries
{
    public partial class InkCanvas : UserControl
    {

        public static readonly DependencyProperty InkStrokesProperty =
            DependencyProperty.Register(
                name: "InkStrokes",
                propertyType: typeof(StrokeCollection),
                ownerType: typeof(InkCanvas),
                typeMetadata: new FrameworkPropertyMetadata(
                    defaultValue: new StrokeCollection(),
                    propertyChangedCallback: new PropertyChangedCallback(OnInkStrokesChanged))
            );

        public StrokeCollection InkStrokes {
            get => (StrokeCollection)GetValue(InkStrokesProperty);
            set {
                Trace.WriteLine("Set InkStrokes");
                SetValue(InkStrokesProperty, value);
            }
        }

        private static void OnInkStrokesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            Trace.WriteLine("Update");
            var control = (InkCanvas)d;
            if (e.OldValue is StrokeCollection oldStrokes) {
                oldStrokes.StrokesChanged -= control.OnStrokesChanged;
            }

            if (e.NewValue is StrokeCollection newStrokes) {
                newStrokes.StrokesChanged += control.OnStrokesChanged;
                control.inkProjector.Strokes = newStrokes;
            }
        }
        private void OnStrokesChanged(object sender, StrokeCollectionChangedEventArgs e)
        {
            Trace.WriteLine("Strokes Collection Changed");
            // Ensure that the InkStrokes dependency property updates
            SetValue(InkStrokesProperty, sender as StrokeCollection);
            inkProjector.Strokes = sender as StrokeCollection;
        }

        public InkCanvas()
        {
            InitializeComponent();
            InkStrokes.StrokesChanged += OnStrokesChanged;
        }
    }
}
