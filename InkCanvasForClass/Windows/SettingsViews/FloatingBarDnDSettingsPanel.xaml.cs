using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using GongSolutions.Wpf.DragDrop;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Menu;

namespace Ink_Canvas.Windows.SettingsViews {
    
    public partial class FloatingBarDnDSettingsPanel : UserControl {

        public class BarItemsDropTarget : IDropTarget {
            public ObservableCollection<FloatingBarItem> BarItems { get; set; } =
                new ObservableCollection<FloatingBarItem>();

            void IDropTarget.DragOver(IDropInfo info) {
                info.Effects = DragDropEffects.Move;
                info.DropTargetAdorner = DropTargetAdorners.Insert;
            }

            void IDropTarget.Drop(IDropInfo info) {
                if (info.Data is FloatingBarItem draggedItem) {
                    var targetCollection = info.TargetCollection as ObservableCollection<FloatingBarItem>;
                    var sourceCollection = info.DragInfo.SourceCollection as ObservableCollection<FloatingBarItem>;

                    Trace.WriteLine(info.InsertIndex);

                    // 在同一个 ObservableCollection 中移动
                    if (targetCollection.Equals(sourceCollection)) {
                        if (info.InsertIndex == 0) {
                            targetCollection.Move(targetCollection.IndexOf(info.Data as FloatingBarItem),0);
                        } else if (info.InsertIndex == targetCollection.Count) {
                            targetCollection.Remove(info.Data as FloatingBarItem);
                            targetCollection.Add(info.Data as FloatingBarItem);
                        } else if ((info.InsertIndex - targetCollection.IndexOf(info.Data as FloatingBarItem) == 1 &&
                                    info.InsertPosition == RelativeInsertPosition.AfterTargetItem) || 
                                   (info.InsertIndex - targetCollection.IndexOf(info.Data as FloatingBarItem) == 0 &&
                                    info.InsertPosition == RelativeInsertPosition.BeforeTargetItem)) { } else {
                            targetCollection.Move(targetCollection.IndexOf(info.Data as FloatingBarItem),info.InsertIndex - 1);
                        }
                    } else { // 跨 ObservableCollection 移动
                        sourceCollection.Remove(info.Data as FloatingBarItem);
                        targetCollection.Insert(info.InsertIndex, info.Data as FloatingBarItem);
                    }
                }
            }

            void IDropTarget.DragEnter(IDropInfo info) {

            }

            void IDropTarget.DragLeave(IDropInfo info) {
                
            }

        }

        public class BarDrawerItemsDropTarget : IDropTarget {
            public ObservableCollection<FloatingBarItem> BarDrawerItems { get; set; } =
                new ObservableCollection<FloatingBarItem>();

            void IDropTarget.DragOver(IDropInfo info) {
                info.Effects = DragDropEffects.Move;
                info.DropTargetAdorner = DropTargetAdorners.Insert;
            }

            void IDropTarget.Drop(IDropInfo info) {
                if (info.Data is FloatingBarItem draggedItem) {
                    var targetCollection = info.TargetCollection as ObservableCollection<FloatingBarItem>;
                    var sourceCollection = info.DragInfo.SourceCollection as ObservableCollection<FloatingBarItem>;

                    // 在同一个 ObservableCollection 中移动
                    if (targetCollection.Equals(sourceCollection)) {
                        targetCollection.Insert(info.InsertIndex, info.Data as FloatingBarItem);
                    } else { // 跨 ObservableCollection 移动
                        sourceCollection.Remove(info.Data as FloatingBarItem);
                        targetCollection.Insert(info.InsertIndex, info.Data as FloatingBarItem);
                    }
                }
            }

            void IDropTarget.DragEnter(IDropInfo info) {

            }

            void IDropTarget.DragLeave(IDropInfo info) {

            }

        }

        public BarItemsDropTarget barItems { get; set; } = new BarItemsDropTarget();
        public BarDrawerItemsDropTarget barDrawerItems { get; set; } = new BarDrawerItemsDropTarget();

        public FloatingBarDnDSettingsPanel() {
            InitializeComponent();

            ToolbarItemsControl.DataContext = barItems;
            ToolbarDrawerItemsControl.DataContext = barDrawerItems;

            barItems.BarItems.Add(new FloatingBarItem() {
                IconSource = FindResource("EraserIcon") as DrawingImage,
            });
            barDrawerItems.BarDrawerItems.Add(new FloatingBarItem() {
                IconSource = FindResource("CursorIcon") as DrawingImage,
            });
            barDrawerItems.BarDrawerItems.Add(new FloatingBarItem() {
                IconSource = FindResource("PenIcon") as DrawingImage,
            });
        }
    }
}
