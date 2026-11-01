using System.ComponentModel;
using System.Windows.Media.Imaging;

namespace Ink_Canvas.Controls
{
    public partial class PPTNavBar
    {
        public sealed class PreviewItem : INotifyPropertyChanged
        {
            private BitmapSource _thumbnail;
            public int SlideNumber { get; set; }
            public BitmapSource Thumbnail
            {
                get => _thumbnail;
                set { if (ReferenceEquals(value, _thumbnail)) return; _thumbnail = value; PropertyChanged?.Invoke(this, new(nameof(Thumbnail))); }
            }
            public event PropertyChangedEventHandler PropertyChanged;
        }
    }
}
