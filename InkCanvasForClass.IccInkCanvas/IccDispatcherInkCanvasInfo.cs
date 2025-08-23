using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using InkCanvasForClass.IccInkCanvas.Utils.Threading;

namespace InkCanvasForClass.IccInkCanvas {
    public class IccDispatcherInkCanvasInfo {

        public Dispatcher Dispatcher { get; private set; }

        public Guid GUID { get; private set; } = Guid.NewGuid();

        public async Task InitDispatcher() {
            Dispatcher = await UIDispatcher.RunNewAsync("IccInkCanvas_" + GUID.ToString());
        }
    }
}
