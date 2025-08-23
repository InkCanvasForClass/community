using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Pen = System.Windows.Media.Pen;

namespace Ink_Canvas.Helpers
{
    
    public class DrawingVisualCanvas : FrameworkElement
    {
        private VisualCollection _children;
        public DrawingVisual DrawingVisual = new DrawingVisual();

        public DrawingVisualCanvas()
        {
            _children = new VisualCollection(this) {
                DrawingVisual // 初始化DrawingVisual
            };
        }

        protected override int VisualChildrenCount => _children.Count;

        protected override Visual GetVisualChild(int index)
        {
            if (index < 0 || index >= _children.Count) throw new ArgumentOutOfRangeException();
            return _children[index];
        }
    }
}
