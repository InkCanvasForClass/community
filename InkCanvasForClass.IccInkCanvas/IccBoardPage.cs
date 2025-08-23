using InkCanvasForClass.IccInkCanvas.Utils.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace InkCanvasForClass.IccInkCanvas {
    public class IccBoardPage {
        public Guid GUID { get; set; }
        public Dispatcher Dispatcher { get; set; }
        public DispatcherContainer Container { get; set; }
        public InkCanvas InkCanvas { get; set; }
        public TimeMachine TimeMachine { get; set; } = new TimeMachine();
        public long LastStrokeID { get; set; } = 0;
        public Matrix TransformMatrix { get; set; } = new Matrix();
    }
}
